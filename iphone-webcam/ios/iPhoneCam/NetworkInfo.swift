import Foundation

enum NetworkInfo {
    /// The phone's IPv4 addresses on Wi-Fi (en0) and, when it is acting as a
    /// hotspot, on the hotspot bridge (bridge*). These are what the PC
    /// viewer connects to.
    static func localIPv4Addresses() -> [String] {
        var addresses: [String] = []
        var ifaddrPointer: UnsafeMutablePointer<ifaddrs>?
        guard getifaddrs(&ifaddrPointer) == 0, let first = ifaddrPointer else { return [] }
        defer { freeifaddrs(ifaddrPointer) }

        for pointer in sequence(first: first, next: { $0.pointee.ifa_next }) {
            let interface = pointer.pointee
            guard let address = interface.ifa_addr,
                  address.pointee.sa_family == UInt8(AF_INET) else { continue }

            let name = String(cString: interface.ifa_name)
            guard name == "en0" || name.hasPrefix("bridge") else { continue }

            var host = [CChar](repeating: 0, count: Int(NI_MAXHOST))
            let result = getnameinfo(address, socklen_t(address.pointee.sa_len),
                                     &host, socklen_t(host.count),
                                     nil, 0, NI_NUMERICHOST)
            if result == 0 {
                let ip = String(cString: host)
                if !addresses.contains(ip) { addresses.append(ip) }
            }
        }
        return addresses
    }
}
