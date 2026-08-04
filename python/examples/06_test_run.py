"""06_test_run.py - Simulator scenario test run (Step 11.18).

Uses simulator-only APIs from ``jchm_sim`` to:

    reset             - reset the simulation to the initial state
    start_run         - begin the scenario (start-signal sequence)
    get_run_status    - poll scenario state
    get_result        - read the final result

The distinction is important:

    jchm      = real-compatible vehicle API (also runs on the real Jajucha)
    jchm_sim  = simulator/testing controls (NOT available on the real vehicle)

This script is a simulator-only helper; student autonomous code should only
use ``jchm`` for normal driving.
"""

import time

import jchm
import jchm_sim
from jchm.errors import JchmConnectionError, JchmError


def main() -> None:
    print("[testrun] Starting a simulator scenario test run...")
    try:
        # Make sure we are connected and the scenario is prepared.
        jchm_sim.connect()
        jchm_sim.reset()

        print("[testrun] Starting scenario run (start-signal sequence).")
        jchm_sim.start_run()

        deadline = time.monotonic() + 120.0
        while time.monotonic() < deadline:
            status = jchm_sim.get_run_status()
            state = status.get("state", "?")
            signal = status.get("signal", "?")
            elapsed = status.get("elapsed_sec", 0.0)
            has_result = status.get("has_result", False)
            print(f"[testrun] state={state} signal={signal} elapsed={elapsed:.1f}s")

            if has_result:
                result = jchm_sim.get_result()
                print("[testrun] Run finished.")
                print("[testrun] status     :", result.get("status"))
                print("[testrun] elapsedSec :", result.get("elapsedSec"))
                print("[testrun] score      :", result.get("score"))
                print("[testrun] collisions :", result.get("collisions"))
                print("[testrun] runId      :", result.get("runId"))
                return

            time.sleep(0.5)

        print("[testrun] Timed out waiting for the run to finish.")
        jchm_sim.abort_run()

    except KeyboardInterrupt:
        print("[testrun] Interrupted.")
        try:
            jchm_sim.abort_run()
        except Exception:  # noqa: BLE001
            pass
    except JchmConnectionError as exc:
        print("[testrun] Simulator bridge is not reachable.")
        print("[testrun] Start Jajucha Simulator first, then run this script again.")
        print(f"[testrun] Details: {exc}")
    except JchmError as exc:
        print(f"[testrun] JCHM error: {exc}")
    finally:
        try:
            jchm.control.set_motor(0, 0, 0)
        except Exception:  # noqa: BLE001
            pass
        print("[testrun] Done.")


if __name__ == "__main__":
    main()
