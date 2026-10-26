import Foundation
import Network

/// TCP server the PC connects to. Sends the raw H.264 (Annex B) byte stream
/// to one viewer at a time; a new viewer replaces the old one.
///
/// If the network falls behind, frames are dropped until the next keyframe
/// rather than queueing up, so latency stays low instead of drifting.
final class StreamServer {
    /// Called with the viewer's address when one connects, nil when it leaves.
    var onClientChange: ((String?) -> Void)?
    /// Called when the encoder should send a keyframe as soon as possible.
    var onNeedKeyframe: (() -> Void)?

    private let port: UInt16
    private let queue = DispatchQueue(label: "iphonecam.server")
    private var listener: NWListener?

    // Only touched on `queue`.
    private var connection: NWConnection?
    private var connectionReady = false
    private var framesInFlight = 0
    private var waitingForKeyframe = true
    private static let maxFramesInFlight = 8

    private let clientLock = NSLock()
    private var _hasClient = false
    /// True while a viewer is connected. Safe to read from any thread.
    var hasClient: Bool {
        clientLock.lock(); defer { clientLock.unlock() }
        return _hasClient
    }

    init(port: UInt16) {
        self.port = port
    }

    func start() throws {
        let parameters = NWParameters.tcp
        if let tcp = parameters.defaultProtocolStack.transportProtocol as? NWProtocolTCP.Options {
            tcp.noDelay = true
        }
        parameters.allowLocalEndpointReuse = true

        guard let nwPort = NWEndpoint.Port(rawValue: port) else { return }
        let listener = try NWListener(using: parameters, on: nwPort)
        listener.newConnectionHandler = { [weak self] connection in
            self?.accept(connection)
        }
        listener.stateUpdateHandler = { state in
            print("StreamServer listener: \(state)")
        }
        listener.start(queue: queue)
        self.listener = listener
    }

    /// Thread-safe. Queues one encoded frame for the current viewer.
    func send(_ data: Data, isKeyframe: Bool) {
        queue.async { [weak self] in
            guard let self, let connection = self.connection, self.connectionReady else { return }

            if self.waitingForKeyframe {
                guard isKeyframe else { return }
                self.waitingForKeyframe = false
            }

            if self.framesInFlight >= Self.maxFramesInFlight {
                // Network can't keep up: skip ahead to the next keyframe.
                self.waitingForKeyframe = true
                self.onNeedKeyframe?()
                return
            }

            self.framesInFlight += 1
            connection.send(content: data, completion: .contentProcessed { [weak self] error in
                // Runs on `queue`.
                guard let self else { return }
                if self.connection === connection {
                    self.framesInFlight = max(0, self.framesInFlight - 1)
                }
                if let error {
                    print("StreamServer send error: \(error)")
                }
            })
        }
    }

    // MARK: - Private (queue)

    private func accept(_ newConnection: NWConnection) {
        if let old = connection {
            old.cancel()
        }
        connection = newConnection
        connectionReady = false
        framesInFlight = 0
        waitingForKeyframe = true

        newConnection.stateUpdateHandler = { [weak self, weak newConnection] state in
            guard let self, let newConnection, self.connection === newConnection else { return }
            switch state {
            case .ready:
                self.connectionReady = true
                self.setHasClient(true)
                self.onClientChange?(Self.describe(newConnection.endpoint))
                self.onNeedKeyframe?()
            case .failed, .cancelled:
                self.connection = nil
                self.connectionReady = false
                self.setHasClient(false)
                self.onClientChange?(nil)
            default:
                break
            }
        }
        newConnection.start(queue: queue)
        receive(on: newConnection)
    }

    /// The viewer never sends anything; reading just lets us notice when it hangs up.
    private func receive(on connection: NWConnection) {
        connection.receive(minimumIncompleteLength: 1, maximumLength: 1024) { [weak self] _, _, isComplete, error in
            if isComplete || error != nil {
                connection.cancel()
                return
            }
            self?.receive(on: connection)
        }
    }

    private func setHasClient(_ value: Bool) {
        clientLock.lock()
        _hasClient = value
        clientLock.unlock()
    }

    private static func describe(_ endpoint: NWEndpoint) -> String {
        if case let .hostPort(host, _) = endpoint {
            return "\(host)"
        }
        return "\(endpoint)"
    }
}
