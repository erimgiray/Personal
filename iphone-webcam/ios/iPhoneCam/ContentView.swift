import SwiftUI
import AVFoundation
import UIKit

struct ContentView: View {
    @StateObject private var streamer = CameraStreamer()
    @Environment(\.scenePhase) private var scenePhase

    var body: some View {
        ZStack(alignment: .bottomLeading) {
            Color.black.ignoresSafeArea()

            CameraPreview(session: streamer.session, refreshToken: streamer.configurationID)
                .ignoresSafeArea()

            VStack(alignment: .leading, spacing: 6) {
                Text(streamer.clientDescription.map { "Streaming to \($0)" } ?? "Waiting for PC…")
                    .font(.headline)

                if streamer.addresses.isEmpty {
                    Text("Not on Wi-Fi or hotspot")
                } else {
                    Text("On the PC, connect to:")
                        .font(.caption)
                    ForEach(streamer.addresses, id: \.self) { address in
                        Text("\(address)  port \(String(CameraStreamer.port))")
                            .font(.system(.title3, design: .monospaced))
                    }
                }

                Text(streamer.status)
                    .font(.caption)
            }
            .padding(12)
            .background(Color.black.opacity(0.55))
            .foregroundColor(.white)
            .cornerRadius(10)
            .padding()
        }
        .overlay(alignment: .bottomTrailing) {
            Button {
                streamer.switchCamera()
            } label: {
                Image(systemName: "arrow.triangle.2.circlepath.camera")
                    .font(.title)
                    .padding(14)
                    .background(Color.black.opacity(0.55))
                    .foregroundColor(.white)
                    .clipShape(Circle())
            }
            .padding()
        }
        .onAppear {
            // Keep the screen from locking while it's being used as a webcam.
            UIApplication.shared.isIdleTimerDisabled = true
            streamer.start()
        }
        .onChange(of: scenePhase) { phase in
            if phase == .active { streamer.refreshAddresses() }
        }
    }
}

/// Live camera preview. Locked to landscape so it matches what the PC receives.
struct CameraPreview: UIViewRepresentable {
    let session: AVCaptureSession
    let refreshToken: Int

    func makeUIView(context: Context) -> PreviewView {
        let view = PreviewView()
        view.backgroundColor = .black
        view.previewLayer.session = session
        view.previewLayer.videoGravity = .resizeAspect
        return view
    }

    func updateUIView(_ uiView: PreviewView, context: Context) {
        uiView.fixOrientation()
    }
}

final class PreviewView: UIView {
    override class var layerClass: AnyClass { AVCaptureVideoPreviewLayer.self }

    var previewLayer: AVCaptureVideoPreviewLayer { layer as! AVCaptureVideoPreviewLayer }

    override func layoutSubviews() {
        super.layoutSubviews()
        fixOrientation()
    }

    func fixOrientation() {
        if let connection = previewLayer.connection, connection.isVideoOrientationSupported {
            connection.videoOrientation = .landscapeRight
        }
    }
}
