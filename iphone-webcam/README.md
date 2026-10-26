# iPhone webcam for Windows

Use an iPhone's camera as a webcam on a Windows PC, wirelessly.

The plan copies how Apple's Continuity Camera works: Bluetooth LE finds the
phone and starts things off, Wi-Fi carries the video, and Windows sees a
normal camera.

| Stage | What it does | Status |
|---|---|---|
| 1 | iPhone streams H.264 over Wi-Fi to a viewer window on the PC | Built, needs testing on the phone |
| 2 | Feed shows up as a camera in Teams, Zoom, OBS (Windows virtual camera) | Next |
| 3 | Bluetooth LE handshake, so no typing IP addresses | Later |
| 4 | Lens switching, resolution choice, tray icon, auto-reconnect | Later |

## How stage 1 works

```
iPhone app                                   Windows PC
camera -> hardware H.264 -> TCP :5050  ====>  viewer.py -> decode -> window
```

- The iPhone app listens on port 5050 and shows its IP address on screen.
- The viewer connects to that IP and decodes the raw H.264 stream.
- Video: 1280x720, 30 fps, about 4 Mbps, a keyframe every second, no
  B-frames (keeps latency low).
- If Wi-Fi can't keep up, the phone skips ahead to the next keyframe instead
  of building up a delay.
- The phone only encodes while a viewer is connected, to save battery.

## Set up the iPhone app (on the Mac)

1. Install XcodeGen once: `brew install xcodegen`
2. Generate the Xcode project:
   ```
   cd iphone-webcam/ios
   xcodegen
   ```
3. Open `iPhoneCam.xcodeproj`.
4. Select the iPhoneCam target, then Signing & Capabilities, and pick your
   developer team. If the bundle ID is taken, change
   `com.erimgiray.iphonecam` to anything unique.
5. Plug in the iPhone, choose it as the run destination and press Run.
6. On the phone, allow camera access and local network access when asked.
   Without local network access the PC can't connect.

The app is locked to landscape. Hold the phone sideways with the home
indicator on the right. The button in the corner switches front and back
cameras.

**iOS limit:** the app must stay open on screen while streaming. iOS cuts off
camera access to apps in the background. The screen won't auto-lock while
the app is open.

## Run the viewer (on the PC)

Needs Python 3.10+ from python.org (the `py` launcher).

```
cd iphone-webcam\windows\viewer
run.cmd 192.168.1.23
```

Use the IP the phone shows. The first run installs the packages. Press Q or
Esc to close the window.

The PC and phone must be on the same Wi-Fi, or the PC can join the
phone's Personal Hotspot (the phone then shows a 172.20.10.x address).

If Windows Firewall asks about Python, allow it on private networks.

### Without Python

If you have FFmpeg installed, this does the same job:

```
ffplay -fflags nobuffer -flags low_delay -f h264 tcp://192.168.1.23:5050
```

## Folder layout

```
ios/
  project.yml          XcodeGen spec (generates the Xcode project)
  Info.plist           camera and local network permission text
  iPhoneCam/
    CameraStreamer.swift   camera setup, ties everything together
    H264Encoder.swift      VideoToolbox hardware encoder, outputs Annex B
    StreamServer.swift     TCP server, drops frames when the network lags
    NetworkInfo.swift      finds the phone's Wi-Fi / hotspot IP
    ContentView.swift      preview, IP display, camera switch button
windows/
  viewer/
    viewer.py          connects, decodes (PyAV) and displays (OpenCV)
    run.cmd            installs packages on first run, then starts viewer
```
