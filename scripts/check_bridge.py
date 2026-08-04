#!/usr/bin/env python3
"""check_bridge.py - Simulator bridge readiness check (Step 11.25).

Connects to the simulator bridge, verifies the protocol version, sends a ping,
and reports ready/not-ready with a useful exit status code:

    exit 0  - bridge reachable, protocol v1, simulation READY
    exit 1  - reachable but protocol mismatch or not ready
    exit 2  - bridge not reachable

Usage:
    python scripts/check_bridge.py [--host 127.0.0.1] [--port 8765] [--timeout 3]
"""

import argparse
import sys

sys.path.insert(0, "python")

from jchm._protocol import SimulatorProtocol  # noqa: E402
from jchm.errors import JchmConnectionError, JchmError  # noqa: E402


def main() -> int:
    parser = argparse.ArgumentParser(description="Check simulator bridge readiness.")
    parser.add_argument("--host", default="127.0.0.1", help="Bridge host (default 127.0.0.1)")
    parser.add_argument("--port", type=int, default=8765, help="Bridge port (default 8765)")
    parser.add_argument("--timeout", type=float, default=3.0, help="Connection timeout seconds")
    args = parser.parse_args()

    from jchm._sim_backend import SimulatorBackend

    backend = SimulatorBackend(host=args.host, port=args.port)

    try:
        # Connect + handshake (protocol version verified inside).
        backend._ensure_connected()
        print("[OK] Simulator bridge reachable")
    except JchmConnectionError as exc:
        print(f"[FAIL] Simulator bridge NOT reachable at {args.host}:{args.port}")
        print(f"       {exc}")
        return 2
    except JchmError as exc:
        print(f"[FAIL] Protocol error during handshake: {exc}")
        return 1

    # Verify protocol version explicitly.
    if not backend._handshake_done:
        print("[FAIL] Handshake did not complete")
        return 1
    print(f"[OK] Protocol v{SimulatorProtocol.PROTOCOL_VERSION}")

    # Ping.
    try:
        response = backend.ping()
        if not response.get("ok", False):
            print("[FAIL] Ping rejected by simulator")
            return 1
        print("[OK] Simulation READY" if response.get("ok") else "[OK] Simulation reachable")
    except JchmError as exc:
        print(f"[FAIL] Ping failed: {exc}")
        return 1

    # Optional: status for extra detail.
    try:
        status = backend.get_status()
        payload = status.get("payload", status)
        state = payload.get("state", "?")
        tick = payload.get("tick", "?")
        print(f"[OK] Simulation state={state} tick={tick}")
    except JchmError:
        pass  # status is informational only

    return 0


if __name__ == "__main__":
    sys.exit(main())
