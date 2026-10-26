import CoreMedia
import Foundation
import VideoToolbox

/// Hardware H.264 encoder that outputs Annex B access units
/// (start-code separated NAL units), which is what decoders on the PC expect
/// from a raw .h264 stream. SPS/PPS are repeated before every keyframe so a
/// viewer can join mid-stream.
final class H264Encoder {
    /// Called on a VideoToolbox thread with one encoded frame.
    var onEncoded: ((Data, Bool) -> Void)?

    private var session: VTCompressionSession?
    private let lock = NSLock()
    private var keyframeRequested = true
    private static let startCode: [UInt8] = [0, 0, 0, 1]

    init(width: Int32, height: Int32, fps: Int = 30, bitrate: Int = 4_000_000) {
        var newSession: VTCompressionSession?
        let status = VTCompressionSessionCreate(
            allocator: kCFAllocatorDefault,
            width: width,
            height: height,
            codecType: kCMVideoCodecType_H264,
            encoderSpecification: nil,
            imageBufferAttributes: nil,
            compressedDataAllocator: nil,
            outputCallback: nil,
            refcon: nil,
            compressionSessionOut: &newSession
        )
        guard status == noErr, let newSession else {
            print("H264Encoder: VTCompressionSessionCreate failed (\(status))")
            return
        }

        // Low latency: real-time mode, no B-frames, a keyframe every second.
        VTSessionSetProperty(newSession, key: kVTCompressionPropertyKey_RealTime, value: kCFBooleanTrue)
        VTSessionSetProperty(newSession, key: kVTCompressionPropertyKey_ProfileLevel,
                             value: kVTProfileLevel_H264_Main_AutoLevel)
        VTSessionSetProperty(newSession, key: kVTCompressionPropertyKey_AllowFrameReordering, value: kCFBooleanFalse)
        VTSessionSetProperty(newSession, key: kVTCompressionPropertyKey_MaxKeyFrameInterval,
                             value: NSNumber(value: fps))
        VTSessionSetProperty(newSession, key: kVTCompressionPropertyKey_ExpectedFrameRate,
                             value: NSNumber(value: fps))
        VTSessionSetProperty(newSession, key: kVTCompressionPropertyKey_AverageBitRate,
                             value: NSNumber(value: bitrate))
        VTCompressionSessionPrepareToEncodeFrames(newSession)

        session = newSession
    }

    deinit {
        if let session {
            VTCompressionSessionCompleteFrames(session, untilPresentationTimeStamp: .invalid)
            VTCompressionSessionInvalidate(session)
        }
    }

    /// Makes the next encoded frame a keyframe (e.g. when a viewer connects).
    func requestKeyframe() {
        lock.lock()
        keyframeRequested = true
        lock.unlock()
    }

    func encode(_ pixelBuffer: CVPixelBuffer, presentationTime: CMTime) {
        guard let session else { return }

        lock.lock()
        let forceKeyframe = keyframeRequested
        keyframeRequested = false
        lock.unlock()

        let frameProperties: CFDictionary? = forceKeyframe
            ? [kVTEncodeFrameOptionKey_ForceKeyFrame as String: true] as CFDictionary
            : nil

        let status = VTCompressionSessionEncodeFrame(
            session,
            imageBuffer: pixelBuffer,
            presentationTimeStamp: presentationTime,
            duration: .invalid,
            frameProperties: frameProperties,
            infoFlagsOut: nil
        ) { [weak self] status, _, sampleBuffer in
            guard status == noErr, let sampleBuffer else { return }
            self?.handleEncoded(sampleBuffer)
        }

        if status != noErr {
            print("H264Encoder: encode failed (\(status))")
            if forceKeyframe { requestKeyframe() }
        }
    }

    // MARK: - AVCC -> Annex B

    private func handleEncoded(_ sampleBuffer: CMSampleBuffer) {
        guard CMSampleBufferDataIsReady(sampleBuffer),
              let blockBuffer = CMSampleBufferGetDataBuffer(sampleBuffer) else { return }

        let isKeyframe = Self.isKeyframe(sampleBuffer)
        var output = Data()
        var nalHeaderLength: Int32 = 4

        // Keyframes: prepend SPS and PPS.
        if let format = CMSampleBufferGetFormatDescription(sampleBuffer) {
            var parameterSetCount = 0
            CMVideoFormatDescriptionGetH264ParameterSetAtIndex(
                format, parameterSetIndex: 0,
                parameterSetPointerOut: nil, parameterSetSizeOut: nil,
                parameterSetCountOut: &parameterSetCount, nalUnitHeaderLengthOut: &nalHeaderLength)

            if isKeyframe {
                for index in 0..<parameterSetCount {
                    var pointer: UnsafePointer<UInt8>?
                    var size = 0
                    let status = CMVideoFormatDescriptionGetH264ParameterSetAtIndex(
                        format, parameterSetIndex: index,
                        parameterSetPointerOut: &pointer, parameterSetSizeOut: &size,
                        parameterSetCountOut: nil, nalUnitHeaderLengthOut: nil)
                    if status == noErr, let pointer {
                        output.append(contentsOf: Self.startCode)
                        output.append(pointer, count: size)
                    }
                }
            }
        }

        // The frame data is a run of [length][NAL] pairs. Swap each length for a start code.
        let totalLength = CMBlockBufferGetDataLength(blockBuffer)
        var bytes = [UInt8](repeating: 0, count: totalLength)
        guard CMBlockBufferCopyDataBytes(blockBuffer, atOffset: 0, dataLength: totalLength,
                                         destination: &bytes) == kCMBlockBufferNoErr else { return }

        let headerLength = Int(nalHeaderLength)
        var offset = 0
        while offset + headerLength <= bytes.count {
            var nalLength = 0
            for i in 0..<headerLength {
                nalLength = (nalLength << 8) | Int(bytes[offset + i])
            }
            offset += headerLength
            guard nalLength > 0, offset + nalLength <= bytes.count else { break }
            output.append(contentsOf: Self.startCode)
            output.append(contentsOf: bytes[offset..<(offset + nalLength)])
            offset += nalLength
        }

        if !output.isEmpty {
            onEncoded?(output, isKeyframe)
        }
    }

    private static func isKeyframe(_ sampleBuffer: CMSampleBuffer) -> Bool {
        guard let attachments = CMSampleBufferGetSampleAttachmentsArray(sampleBuffer, createIfNecessary: false),
              CFArrayGetCount(attachments) > 0 else { return true }
        let dictionary = unsafeBitCast(CFArrayGetValueAtIndex(attachments, 0), to: CFDictionary.self)
        let notSyncKey = Unmanaged.passUnretained(kCMSampleAttachmentKey_NotSync).toOpaque()
        return !CFDictionaryContainsKey(dictionary, notSyncKey)
    }
}
