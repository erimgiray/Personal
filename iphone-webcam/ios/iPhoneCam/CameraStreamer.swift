import AVFoundation
import Foundation

/// Owns the camera, the encoder and the network server, and publishes
/// status for the UI.
///
/// Threads:
///   sessionQueue - camera configuration (start/stop/switch)
///   videoQueue   - camera frames arrive here and are handed to the encoder
///   main         - all @Published updates
final class CameraStreamer: NSObject, ObservableObject, AVCaptureVideoDataOutputSampleBufferDelegate {
    static let port: UInt16 = 5050

    @Published var addresses: [String] = []
    @Published var clientDescription: String?
    @Published var status = "Starting…"
    @Published var configurationID = 0

    let session = AVCaptureSession()

    private let sessionQueue = DispatchQueue(label: "iphonecam.session")
    private let videoQueue = DispatchQueue(label: "iphonecam.video")
    private let videoOutput = AVCaptureVideoDataOutput()
    private let server = StreamServer(port: CameraStreamer.port)

    // Only touched on videoQueue.
    private var encoder: H264Encoder?
    private var encoderWidth = 0
    private var encoderHeight = 0

    private var started = false
    private var usingFrontCamera = false

    // MARK: - Public

    func start() {
        guard !started else { return }
        started = true
        refreshAddresses()

        server.onClientChange = { [weak self] description in
            DispatchQueue.main.async { self?.clientDescription = description }
        }
        server.onNeedKeyframe = { [weak self] in
            self?.videoQueue.async { self?.encoder?.requestKeyframe() }
        }

        do {
            try server.start()
        } catch {
            status = "Couldn't open port \(CameraStreamer.port): \(error.localizedDescription)"
            return
        }

        AVCaptureDevice.requestAccess(for: .video) { [weak self] granted in
            guard let self else { return }
            guard granted else {
                DispatchQueue.main.async {
                    self.status = "Camera access is off. Turn it on in Settings > iPhoneCam."
                }
                return
            }
            self.sessionQueue.async {
                self.configureSession(front: false)
                self.session.startRunning()
            }
        }
    }

    func switchCamera() {
        let front = !usingFrontCamera
        usingFrontCamera = front
        sessionQueue.async {
            self.configureSession(front: front)
            self.videoQueue.async { self.encoder?.requestKeyframe() }
        }
    }

    func refreshAddresses() {
        addresses = NetworkInfo.localIPv4Addresses()
    }

    // MARK: - Camera setup (sessionQueue)

    private func configureSession(front: Bool) {
        session.beginConfiguration()
        defer {
            session.commitConfiguration()
            DispatchQueue.main.async { self.configurationID += 1 }
        }

        if session.canSetSessionPreset(.hd1280x720) {
            session.sessionPreset = .hd1280x720
        }

        for input in session.inputs {
            session.removeInput(input)
        }

        let position: AVCaptureDevice.Position = front ? .front : .back
        guard
            let device = AVCaptureDevice.default(.builtInWideAngleCamera, for: .video, position: position),
            let input = try? AVCaptureDeviceInput(device: device),
            session.canAddInput(input)
        else {
            DispatchQueue.main.async { self.status = "Couldn't open the \(front ? "front" : "back") camera" }
            return
        }
        session.addInput(input)

        if session.outputs.isEmpty {
            videoOutput.videoSettings = [
                kCVPixelBufferPixelFormatTypeKey as String: kCVPixelFormatType_420YpCbCr8BiPlanarFullRange
            ]
            videoOutput.alwaysDiscardsLateVideoFrames = true
            videoOutput.setSampleBufferDelegate(self, queue: videoQueue)
            if session.canAddOutput(videoOutput) {
                session.addOutput(videoOutput)
            }
        }

        // Upright frames when the phone is held sideways (UI is locked to landscape right).
        if let connection = videoOutput.connection(with: .video), connection.isVideoOrientationSupported {
            connection.videoOrientation = .landscapeRight
        }

        // Pin to 30 fps if the camera format allows it.
        let supports30 = device.activeFormat.videoSupportedFrameRateRanges.contains {
            $0.minFrameRate <= 30 && $0.maxFrameRate >= 30
        }
        if supports30, (try? device.lockForConfiguration()) != nil {
            device.activeVideoMinFrameDuration = CMTime(value: 1, timescale: 30)
            device.activeVideoMaxFrameDuration = CMTime(value: 1, timescale: 30)
            device.unlockForConfiguration()
        }

        DispatchQueue.main.async { self.status = front ? "Front camera" : "Back camera" }
    }

    // MARK: - Frames (videoQueue)

    func captureOutput(_ output: AVCaptureOutput,
                       didOutput sampleBuffer: CMSampleBuffer,
                       from connection: AVCaptureConnection) {
        // Don't spend battery encoding when nobody is watching.
        guard server.hasClient, let pixelBuffer = CMSampleBufferGetImageBuffer(sampleBuffer) else { return }

        let width = CVPixelBufferGetWidth(pixelBuffer)
        let height = CVPixelBufferGetHeight(pixelBuffer)

        if encoder == nil || width != encoderWidth || height != encoderHeight {
            let newEncoder = H264Encoder(width: Int32(width), height: Int32(height))
            newEncoder.onEncoded = { [weak self] data, isKeyframe in
                self?.server.send(data, isKeyframe: isKeyframe)
            }
            encoder = newEncoder
            encoderWidth = width
            encoderHeight = height
        }

        encoder?.encode(pixelBuffer, presentationTime: CMSampleBufferGetPresentationTimeStamp(sampleBuffer))
    }
}
