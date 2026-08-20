"""Drive the official 2026 checkpoint route through the live bridge.

This is an evidence harness, not a replacement for a student's camera/lidar
policy: it uses only the public bridge pose/command contract to exercise the
same Rigidbody, trigger and scenario completion path with real motor inputs.
"""

from __future__ import annotations

import argparse
import collections
import json
import math
import sys
import time
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "python"))
from jchm._sim_backend import SimulatorBackend


def payload(response: dict) -> dict:
    if not response.get("ok", False):
        raise RuntimeError(response)
    return response.get("payload", {})


def pose(status: dict) -> tuple[float, float, float]:
    vehicle = payload(status).get("vehicle", {})
    position = vehicle.get("position_cm", {})
    rotation = vehicle.get("rotation_deg", {})
    return float(position.get("x", 0.0)), float(position.get("z", 0.0)), float(rotation.get("y", 0.0))


def clamp(value: float, low: float, high: float) -> float:
    return max(low, min(high, value))


def angle_error(target: float, current: float) -> float:
    return (target - current + 180.0) % 360.0 - 180.0


def load_checkpoints(path: Path) -> tuple[list[tuple[str, float, float]], set[tuple[int, int]]]:
    root = json.loads(path.read_text(encoding="utf-8"))
    tile = float(root.get("tileSizeCm", 5.0))
    checkpoints = root["competition2026"]["checkpoints"]
    ordered = sorted(checkpoints, key=lambda c: int(c["order"]))
    output = []
    for item in ordered:
        region = item["region"]
        x = (float(region["x"]) + float(region["width"]) * 0.5) * tile
        z = (float(region["z"]) + float(region["height"]) * 0.5) * tile
        output.append((str(item["id"]), x, z))
    road = {(int(item["x"]), int(item["z"])) for item in root.get("road", [])}
    return output, road


def load_official_structure_paths(path: Path) -> list[list[tuple[float, float]]]:
    """Read the authored structure centre lines from the official course JSON."""
    root = json.loads(path.read_text(encoding="utf-8"))
    output: list[list[tuple[float, float]]] = []
    for structure in root.get("structures", []):
        points = structure.get("pathPoints") or []
        if len(points) < 2:
            continue
        values = [(float(p["xCm"]), float(p["zCm"])) for p in points]
        if str(structure.get("profile", "")).lower() == "u_tunnel" and len(values) >= 5:
            entrance, first_turn, second_leg, exit_point = values[0], values[1], values[3], values[4]
            radius = abs(first_turn[0] - second_leg[0]) * 0.5
            center_x = (first_turn[0] + second_leg[0]) * 0.5
            center_z = first_turn[1]
            arc_steps = max(2, int(math.ceil(math.pi * radius / 5.0)))
            expanded = [entrance, first_turn]
            expanded.extend((center_x + radius * math.cos(math.pi * i / arc_steps),
                             center_z - radius * math.sin(math.pi * i / arc_steps))
                            for i in range(1, arc_steps + 1))
            expanded.append(exit_point)
            output.append(expanded)
        else:
            output.append(values)
    return output


def densify_polyline(points: list[tuple[float, float]], spacing_cm: float = 5.0) -> list[tuple[float, float]]:
    """Sample an authored centre line at roughly one road-mask tile."""
    if len(points) < 2:
        return points
    output = [points[0]]
    for start, end in zip(points, points[1:]):
        dx, dz = end[0] - start[0], end[1] - start[1]
        length = math.hypot(dx, dz)
        steps = max(1, int(math.ceil(length / spacing_cm)))
        for step in range(1, steps + 1):
            t = step / steps
            output.append((start[0] + dx * t, start[1] + dz * t))
    return output


def nearest_road(road: set[tuple[int, int]], point: tuple[float, float]) -> tuple[int, int]:
    if not road:
        raise RuntimeError("course has no road tiles")
    return min(road, key=lambda item: (item[0] - point[0]) ** 2 + (item[1] - point[1]) ** 2)


def grid_path(road: set[tuple[int, int]], start: tuple[int, int], goal: tuple[int, int]) -> list[tuple[int, int]]:
    """Find a 4-neighbour centreline path through the authoritative road mask."""
    if start == goal:
        return [start]
    queue = collections.deque([start])
    previous: dict[tuple[int, int], tuple[int, int] | None] = {start: None}
    while queue:
        current = queue.popleft()
        for dx, dz in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            candidate = (current[0] + dx, current[1] + dz)
            if candidate not in road or candidate in previous:
                continue
            previous[candidate] = current
            if candidate == goal:
                path = [candidate]
                while path[-1] != start:
                    path.append(previous[path[-1]])  # type: ignore[arg-type]
                path.reverse()
                return path
            queue.append(candidate)
    raise RuntimeError(f"no connected road path from {start} to {goal}")


def smooth_route(points: list[tuple[float, float]], radius: int = 5) -> list[tuple[float, float]]:
    """Round 5 cm-mask right-angle steps into drivable centreline samples."""
    if len(points) <= radius * 2 + 1:
        return points
    smoothed: list[tuple[float, float]] = []
    for index, point in enumerate(points):
        if index < radius or index >= len(points) - radius:
            smoothed.append(point)
            continue
        window = points[index - radius : index + radius + 1]
        smoothed.append((sum(p[0] for p in window) / len(window), sum(p[1] for p in window) / len(window)))
    return smoothed


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--course", default="Courses/2026_preliminary.json")
    parser.add_argument("--output", default="test-artifacts/scenario/checkpoint_drive.json")
    parser.add_argument("--settle", type=float, default=0.12)
    parser.add_argument("--overall-timeout", type=float, default=105.0)
    parser.add_argument("--waypoint-radius", type=float, default=14.0)
    parser.add_argument("--speed", type=int, default=4,
                        help="JCHM speed command used by this diagnostic driver")
    parser.add_argument("--structure-speed", type=int, default=2,
                        help="reduced JCHM speed for tunnel/ramp centreline turns")
    parser.add_argument("--curve-speed", type=int, default=1,
                        help="extra-reduced JCHM speed for high-curvature corners")
    parser.add_argument("--steer-gain", type=float, default=8.0,
                        help="heading-error divisor for proportional steering")
    args = parser.parse_args()

    checkpoints, road = load_checkpoints(Path(args.course))
    out_path = Path(args.output)
    out_path.parent.mkdir(parents=True, exist_ok=True)
    backend = SimulatorBackend()
    snapshots: list[dict] = []
    result: dict = {
        "passed": False,
        "course": str(Path(args.course)),
        "checkpoints": [{"id": i, "x_cm": x, "z_cm": z} for i, x, z in checkpoints],
        "visited": [],
        "snapshots": snapshots,
    }
    try:
        status = backend.get_status()
        run = backend.sim_get_run_status()
        result["handshake"] = bool(status.get("ok"))
        result["initial_run_state"] = payload(run).get("state")
        if payload(run).get("state") in {"Finished", "Aborted", "TimedOut", "FalseStart"}:
            backend.sim_reset()
            run = backend.sim_get_run_status()
            status = backend.get_status()
        if payload(status).get("state") != "Running":
            backend.sim_start()
            status = backend.get_status()
        if payload(run).get("state") != "Ready":
            raise RuntimeError(f"scenario is not Ready: {run}")
        backend._ensure_connected()  # evidence harness needs the start error body
        start_response = backend._send_command("start_run", {})
        result["start_response"] = start_response
        if not start_response.get("ok", False):
            raise RuntimeError(f"start_run rejected: {start_response}")
        release_deadline = time.monotonic() + 14.0
        while time.monotonic() < release_deadline:
            status = backend.get_status()
            run_status = payload(backend.sim_get_run_status())
            if run_status.get("state") == "Running" and run_status.get("signal") == "Released":
                break
            time.sleep(0.05)
        status = backend.get_status()
        run_status = payload(backend.sim_get_run_status())
        released = run_status.get("state") == "Running" and run_status.get("signal") == "Released"
        result["released"] = released
        if not released:
            raise RuntimeError("start signal did not release")
        # The bridge reports the released signal on the same frame that the
        # scenario transitions Countdown -> Running. Give that transition one
        # fixed-step boundary before the first non-zero motor command so the
        # false-start rule observes the released state deterministically.
        time.sleep(0.25)

        # Build the route from the authoritative 5 cm road mask instead of
        # cutting diagonally between checkpoint rectangles. This follows the
        # S/U/zigzag bends and keeps the Rigidbody inside the lane.
        checkpoint_cells = [
            nearest_road(road, (x / 5.0, z / 5.0)) for _, x, z in checkpoints
        ]
        route_cells: list[tuple[int, int]] = []
        for index in range(len(checkpoint_cells) - 1):
            segment = grid_path(road, checkpoint_cells[index], checkpoint_cells[index + 1])
            if route_cells:
                route_cells.extend(segment[1:])
            else:
                route_cells.extend(segment)
        route_points = smooth_route([(cell[0] * 5.0, cell[1] * 5.0) for cell in route_cells], radius=5)
        structure_route_start = None
        structure_route_end = None
        # The extracted 5 cm mask can contain multiple connected branches at
        # the tunnel mouth.  The official structure centre line is the
        # authoritative route through a tunnel/ramp; splice those points into
        # the mask route so this evidence driver never chooses a wall-side
        # shortcut at a U/S bend.
        structure_paths = load_official_structure_paths(Path(args.course))
        if structure_paths and str(Path(args.course)).lower().endswith("2026_preliminary.json"):
            explicit = densify_polyline(structure_paths[0] + structure_paths[1][1:])
            anchor_start = min(range(len(route_points)),
                               key=lambda i: math.hypot(route_points[i][0] - explicit[0][0],
                                                        route_points[i][1] - explicit[0][1]))
            anchor_end = min(range(anchor_start, len(route_points)),
                             key=lambda i: math.hypot(route_points[i][0] - explicit[-1][0],
                                                      route_points[i][1] - explicit[-1][1]))
            route_points = route_points[:anchor_start] + explicit + route_points[anchor_end + 1:]
            structure_route_start = anchor_start
            structure_route_end = anchor_start + len(explicit) - 1
            result["official_structure_route_override"] = explicit
        result["route_cell_count"] = len(route_cells)
        result["route_preview_cells"] = route_cells[:30]
        result["route_distance_cm"] = sum(
            math.hypot(route_points[i][0] - route_points[i - 1][0], route_points[i][1] - route_points[i - 1][1])
            for i in range(1, len(route_points))
        )
        next_index = 1
        route_index = 0
        # Keep a monotonic route cursor.  The previous fixed-cell lookahead
        # could point behind the vehicle after a bend, causing a saturated
        # steering command and a loop around the first S curve.  Pure pursuit
        # below always reacquires the closest *forward* centreline sample and
        # targets a distance ahead along that same route.
        route_cumulative = [0.0]
        for i in range(1, len(route_points)):
            route_cumulative.append(route_cumulative[-1] + math.hypot(
                route_points[i][0] - route_points[i - 1][0],
                route_points[i][1] - route_points[i - 1][1]))
        start_time = time.monotonic()
        last_command = None
        while time.monotonic() - start_time < args.overall_timeout:
            status = backend.get_status()
            snapshot = payload(status)
            x, z, yaw = pose(status)
            run_status = payload(backend.sim_get_run_status())
            state = str(run_status.get("state", ""))
            if state in {"Finished", "Completed"}:
                break
            # Reacquire only ahead of the monotonic cursor.  A bounded search
            # prevents a loop outside the course from jumping back to an
            # earlier branch while still recovering from small tracking error.
            search_end = min(len(route_points), route_index + 180)
            nearest_index = min(
                range(route_index, search_end),
                key=lambda i: (route_points[i][0] - x) ** 2 + (route_points[i][1] - z) ** 2,
            )
            if nearest_index > route_index:
                route_index = nearest_index
            if next_index < len(checkpoints):
                checkpoint_id, checkpoint_x, checkpoint_z = checkpoints[next_index]
                checkpoint_distance = math.hypot(checkpoint_x - x, checkpoint_z - z)
                if checkpoint_distance <= args.waypoint_radius:
                    result["visited"].append({"id": checkpoint_id, "x_cm": x, "z_cm": z, "distance_cm": checkpoint_distance})
                    next_index += 1
            in_structure = (structure_route_start is not None and
                            structure_route_start <= route_index <= structure_route_end)
            if route_index >= len(route_points) - 1:
                lookahead = route_index
            else:
                lookahead_cm = 12.0 if in_structure else 20.0
                desired_distance = route_cumulative[route_index] + lookahead_cm
                lookahead = route_index
                while (lookahead < len(route_points) - 1 and
                       route_cumulative[lookahead] < desired_distance):
                    lookahead += 1
            target_x, target_z = route_points[lookahead]
            dx, dz = target_x - x, target_z - z
            distance = math.hypot(dx, dz)
            desired_yaw = math.degrees(math.atan2(dx, dz))
            error = angle_error(desired_yaw, yaw)
            # The runtime maps one command unit to roughly two degrees of
            # wheel angle; this proportional controller keeps the car on the
            # centreline while allowing the official trigger tiles to fire.
            # The manual exposes the full [-10, 10] steering range.  The
            # earlier diagnostic capped this at 8, which cannot make the
            # first official S-curve at the calibrated wheelbase.
            steering = int(round(clamp(error / max(0.1, args.steer_gain), -10.0, 10.0)))
            # Slow before a sharp authored bend.  At the normal 4-unit
            # command the calibrated 20-degree steering limit has a turning
            # radius of roughly 58 cm, so taking a 90-degree corner at full
            # speed cuts across the checkpoint tile even when the heading is
            # correct.  A short 2-unit approach keeps the physical centreline
            # and makes the coordinate evidence useful for checkpoint checks.
            curve_speed = False
            if route_index < len(route_points) - 2:
                ahead_distance = route_cumulative[route_index] + 100.0
                curve_index = route_index
                while (curve_index < len(route_points) - 1 and
                       route_cumulative[curve_index] < ahead_distance):
                    curve_index += 1
                h0 = math.degrees(math.atan2(
                    route_points[min(route_index + 1, len(route_points) - 1)][0] - route_points[route_index][0],
                    route_points[min(route_index + 1, len(route_points) - 1)][1] - route_points[route_index][1]))
                h1 = math.degrees(math.atan2(
                    route_points[curve_index][0] - route_points[max(0, curve_index - 1)][0],
                    route_points[curve_index][1] - route_points[max(0, curve_index - 1)][1]))
                curve_speed = abs(angle_error(h1, h0)) > 15.0
            selected_speed = args.structure_speed if in_structure else args.speed
            if curve_speed:
                selected_speed = min(selected_speed, args.curve_speed)
            speed = int(clamp(selected_speed, -30, 30))
            command = (steering, steering, speed)
            # The bridge watchdog is intentionally part of the manual
            # contract: a motor command must be refreshed while driving.
            # Re-send even when the tuple is unchanged so a long straight or
            # a saturated steering segment is not mistaken for a vehicle
            # physics stall when the watchdog safely zeros the motor.
            backend.set_motor(*command)
            last_command = command
            snapshots.append({
                "time": time.time(), "state": state, "target": checkpoints[next_index][0] if next_index < len(checkpoints) else "finish",
                "position_cm": {"x": x, "z": z}, "yaw_deg": yaw,
                "velocity_cm_s": snapshot.get("vehicle", {}).get("velocity_cm_s", {}),
                "distance_cm": distance, "desired_yaw_deg": desired_yaw,
                "steering": steering, "speed": speed,
                "route_index": route_index, "nearest_index": nearest_index,
                "lookahead_index": lookahead,
            })
            time.sleep(args.settle)

        backend.set_motor(0, 0, 0)
        final_status = backend.get_status()
        final_payload = payload(final_status)
        final_state = str(payload(backend.sim_get_run_status()).get("state", ""))
        result["final_state_before_abort"] = final_state
        result["final_pose"] = pose(final_status)
        if final_state in {"Finished", "Completed"}:
            try:
                result["scenario_result"] = backend.sim_get_result()
            except Exception as result_error:
                result["scenario_result_error"] = str(result_error)
        result["visited_count"] = len(result["visited"])
        result["target_count"] = max(0, len(checkpoints) - 1)
        scenario_result = result.get("scenario_result") or {}
        completed = bool(scenario_result.get("completed")) or str(scenario_result.get("status", "")).lower() == "completed"
        route_completed = route_index >= len(route_points) - 1
        result["route_completed"] = route_completed
        result["passed"] = completed and route_completed
        if final_state not in {"Finished", "Completed"}:
            # Keep the result auditable; this is not a false pass if a trigger
            # was missed. The caller can inspect the last target/snapshot.
            backend.sim_abort_run()
            result["abort_reason"] = "finish_not_reached"
        return 0 if result["passed"] else 2
    except Exception as exc:
        result["error"] = str(exc)
        try:
            backend.set_motor(0, 0, 0)
            backend.sim_abort_run()
        except Exception:
            pass
        return 2
    finally:
        result["snapshot_count"] = len(snapshots)
        out_path.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
        backend.disconnect()
        print(out_path)


if __name__ == "__main__":
    raise SystemExit(main())
