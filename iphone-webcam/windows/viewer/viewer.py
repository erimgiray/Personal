"""Stage 1 viewer: shows the iPhoneCam stream in a window on the PC.

Usage:
    py viewer.py 192.168.1.23            # IP shown on the phone's screen
    py viewer.py 192.168.1.23 --port 5050

Keys: Q or Esc to quit.

The phone sends a raw H.264 (Annex B) byte stream over TCP. This connects,
decodes with FFmpeg (via PyAV) and draws frames with OpenCV. If the phone
drops off (app closed, Wi-Fi blip) it keeps retrying.
"""

import argparse
import socket
import sys
import time

import av

try:
    import cv2
except ImportError:  # only needed for the on-screen window
    cv2 = None

WINDOW = "iPhoneCam"
RECV_BYTES = 64 * 1024


def make_decoder():
    ctx = av.CodecContext.create("h264", "r")
    # Frame threading adds a frame or more of delay per thread; slice
    # threading doesn't. Low-latency flag stops the decoder holding frames.
    try:
        ctx.thread_type = "SLICE"
    except Exception:
        pass
    try:
        ctx.options = {"flags": "low_delay"}
    except Exception:
        pass
    return ctx


def connect(host, port):
    sock = socket.create_connection((host, port), timeout=5)
    sock.setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)
    sock.settimeout(5)
    return sock


def draw_overlay(image, fps, size):
    text = f"{size[0]}x{size[1]}  {fps:4.1f} fps"
    cv2.putText(image, text, (12, 28), cv2.FONT_HERSHEY_SIMPLEX, 0.7,
                (0, 0, 0), 4, cv2.LINE_AA)
    cv2.putText(image, text, (12, 28), cv2.FONT_HERSHEY_SIMPLEX, 0.7,
                (255, 255, 255), 1, cv2.LINE_AA)


def run_once(host, port, headless, max_frames, stats):
    """Stream until the connection drops. Returns True if the user quit."""
    sock = connect(host, port)
    print(f"Connected to {host}:{port}")
    decoder = make_decoder()
    fps_frames, fps_start, fps = 0, time.monotonic(), 0.0

    try:
        while True:
            try:
                chunk = sock.recv(RECV_BYTES)
            except socket.timeout:
                print("No data for 5s (is the app open on the phone?)")
                return False
            if not chunk:
                print("Phone closed the connection")
                return False

            for packet in decoder.parse(chunk):
                try:
                    frames = decoder.decode(packet)
                except av.error.InvalidDataError:
                    continue  # joined mid-stream; recovers at the next keyframe

                for frame in frames:
                    stats["frames"] += 1
                    stats["size"] = (frame.width, frame.height)

                    fps_frames += 1
                    elapsed = time.monotonic() - fps_start
                    if elapsed >= 1.0:
                        fps, fps_frames, fps_start = fps_frames / elapsed, 0, time.monotonic()

                    if not headless:
                        image = frame.to_ndarray(format="bgr24")
                        draw_overlay(image, fps, stats["size"])
                        cv2.imshow(WINDOW, image)
                        key = cv2.waitKey(1) & 0xFF
                        if key in (ord("q"), 27):
                            return True
                        if cv2.getWindowProperty(WINDOW, cv2.WND_PROP_VISIBLE) < 1:
                            return True

                    if max_frames and stats["frames"] >= max_frames:
                        return True
    finally:
        sock.close()


def main():
    parser = argparse.ArgumentParser(description="View the iPhoneCam stream.")
    parser.add_argument("host", help="phone IP address shown in the app")
    parser.add_argument("--port", type=int, default=5050)
    parser.add_argument("--headless", action="store_true",
                        help="decode without opening a window (for testing)")
    parser.add_argument("--frames", type=int, default=0,
                        help="stop after this many frames (0 = run forever)")
    args = parser.parse_args()

    if not args.headless and cv2 is None:
        sys.exit("OpenCV is missing. Run: py -m pip install -r requirements.txt")

    stats = {"frames": 0, "size": (0, 0)}
    try:
        while True:
            try:
                if run_once(args.host, args.port, args.headless, args.frames, stats):
                    break
            except OSError as error:
                print(f"Can't reach {args.host}:{args.port} ({error}). Retrying in 2s...")
            if args.frames and stats["frames"] >= args.frames:
                break
            time.sleep(2)
    except KeyboardInterrupt:
        pass
    finally:
        if cv2 is not None and not args.headless:
            cv2.destroyAllWindows()

    print(f"Decoded {stats['frames']} frames at {stats['size'][0]}x{stats['size'][1]}")


if __name__ == "__main__":
    main()
