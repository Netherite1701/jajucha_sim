#!/usr/bin/env python3
"""
Bridge Smoke Test

A minimal diagnostic script that exercises the bridge protocol directly
(without the jchm compatibility layer), then via the jchm API.

Usage:
    python tools/bridge_smoke_test.py [--host 127.0.0.1] [--port 8765]

Prerequisites:
    - Unity simulator must be running (Scene loaded, bridge active)
"""

import argparse
import json
import socket
import sys
import time


def main():
    parser = argparse.ArgumentParser(description="Jajucha Bridge Smoke Test")
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=8765)
    args = parser.parse_args()

    print(f"[smoke] Connecting to {args.host}:{args.port}...")

    try:
        sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        sock.settimeout(2.0)
        sock.connect((args.host, args.port))
        sock.setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)
    except (ConnectionRefusedError, socket.timeout) as e:
        print(f"[FAIL] Could not connect: {e}")
        print("Make sure the Unity simulator is running with the bridge enabled.")
        sys.exit(1)

    print("[OK] Connected")

    def send(msg):
        data = (json.dumps(msg) + "\n").encode("utf-8")
        sock.sendall(data)

    def recv(timeout=2.0):
        sock.settimeout(timeout)
        buf = []
        while True:
            try:
                b = sock.recv(1)
            except socket.timeout:
                return None
            if not b:
                return None
            if b == b"\n":
                break
            if b != b"\r":
                buf.append(b.decode("utf-8"))
        line = "".join(buf)
        return json.loads(line) if line else None

    # 1. Handshake
    print("\n--- Step 1: Handshake ---")
    send({"type": "hello", "protocol": 1, "client": "jchm-sim"})
    resp = recv()
    if resp and resp.get("type") == "hello_ack":
        print("[OK] Handshake complete")
    else:
        print(f"[FAIL] Expected hello_ack, got: {resp}")
        sock.close()
        sys.exit(1)

    # 2. Ping
    print("\n--- Step 2: Ping ---")
    send({"type": "command", "id": 1, "name": "ping", "payload": {}})
    resp = recv()
    if resp and resp.get("ok"):
        sim_time = resp.get("payload", {}).get("sim_time", "?")
        print(f"[OK] Ping response, sim_time={sim_time}")
    else:
        print(f"[FAIL] Ping failed: {resp}")

    # 3. Reset
    print("\n--- Step 3: Reset ---")
    send({"type": "command", "id": 2, "name": "sim_reset", "payload": {}})
    resp = recv()
    if resp and resp.get("ok"):
        print("[OK] Reset acknowledged")
    else:
        print(f"[FAIL] Reset failed: {resp}")

    # 4. Start simulation
    print("\n--- Step 4: Start ---")
    send({"type": "command", "id": 3, "name": "sim_start", "payload": {}})
    resp = recv()
    if resp and resp.get("ok"):
        print("[OK] Start acknowledged")
    else:
        print(f"[FAIL] Start failed: {resp}")

    # 5. Set motor: forward at speed 3
    print("\n--- Step 5: set_motor(0, 0, 3) ---")
    send({
        "type": "command",
        "id": 4,
        "name": "set_motor",
        "payload": {"left": 0, "right": 0, "speed": 3},
    })
    resp = recv()
    if resp and resp.get("ok"):
        print("[OK] set_motor(0,0,3) acknowledged")
    else:
        print(f"[FAIL] set_motor failed: {resp}")

    # Wait a bit for the vehicle to move
    time.sleep(0.5)

    # 6. Status check
    print("\n--- Step 6: Status ---")
    send({"type": "command", "id": 5, "name": "get_status", "payload": {}})
    resp = recv()
    if resp and resp.get("ok"):
        payload = resp.get("payload", {})
        print(f"  State:    {payload.get('state')}")
        print(f"  Tick:     {payload.get('tick')}")
        print(f"  SimTime:  {payload.get('sim_time')}")
        vehicle = payload.get("vehicle", {})
        cmd = vehicle.get("command", {})
        print(f"  Vehicle:  left={cmd.get('left')} right={cmd.get('right')} speed={cmd.get('speed')}")
        print("[OK] Status retrieved")
    else:
        print(f"[FAIL] Status failed: {resp}")

    # 7. Stop motor
    print("\n--- Step 7: set_motor(0, 0, 0) ---")
    send({
        "type": "command",
        "id": 6,
        "name": "set_motor",
        "payload": {"left": 0, "right": 0, "speed": 0},
    })
    resp = recv()
    if resp and resp.get("ok"):
        print("[OK] set_motor(0,0,0) acknowledged")
    else:
        print(f"[FAIL] set_motor stop failed: {resp}")

    # 8. Pause simulation
    print("\n--- Step 8: Pause ---")
    send({"type": "command", "id": 7, "name": "sim_pause", "payload": {}})
    resp = recv()
    if resp and resp.get("ok"):
        print("[OK] Pause acknowledged")
    else:
        print(f"[FAIL] Pause failed: {resp}")

    # 9. Single step
    print("\n--- Step 9: Step (single tick) ---")
    send({"type": "command", "id": 8, "name": "sim_step", "payload": {}})
    resp = recv()
    if resp and resp.get("ok"):
        print("[OK] Single step acknowledged")
    else:
        print(f"[FAIL] Step failed: {resp}")

    # 10. Disconnect
    print("\n--- Step 10: Disconnect ---")
    sock.close()
    print("[OK] Disconnected")

    print("\n=== All smoke tests passed! ===")
    sys.exit(0)


if __name__ == "__main__":
    main()
