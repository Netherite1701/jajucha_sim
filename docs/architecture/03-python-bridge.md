# Python Bridge — Architecture

## Purpose

The Python bridge connects a Python `jchm` client to the Unity vehicle
simulator over TCP. Its goal is to make student code written for the real
Jajucha vehicle work unchanged in the simulator:

```python
import jchm

# This must work identically on real vehicle and in simulator:
jchm.control.set_motor(-5, -5, 3)
```

The bridge is the **only** component that knows about the network boundary.
It does not understand vehicle physics, camera images, or course logic.

## Transport

- **TCP over localhost** (`127.0.0.1`, default port `8765`).
- Persistent single-client connection — no reconnect-per-command overhead.
- Newline-delimited JSON (`\n` as record terminator).
- Background receive thread → thread-safe queue → main-thread dispatch.

## Protocol

See `docs/protocol-v1.md` for the exact message schemas.

### Version handshake

Every connection begins with a version handshake:

1. Client sends `{"type":"hello","protocol":1,"client":"jchm-sim"}`
2. Server responds with `{"type":"hello_ack","protocol":1,"simulator":"JajuchaSim"}`

If versions mismatch, the server sends an error and closes.

### Command envelope

All commands use a consistent envelope:

```json
{
  "type": "command",
  "id": 17,
  "name": "set_motor",
  "payload": { "left": -5, "right": -5, "speed": 3 }
}
```

Responses match the request id:

```json
{
  "type": "response",
  "id": 17,
  "ok": true
}
```

### Supported commands (v1)

| Command       | Description                        | Policy       |
|---------------|------------------------------------|--------------|
| `ping`        | Health check, returns sim_time     | request-reply |
| `set_motor`   | Set left/right steering + speed    | latest-wins  |
| `get_status`  | Return full simulator/vehicle state| request-reply |
| `sim_start`   | Start or resume simulation         | ordered      |
| `sim_pause`   | Pause simulation                   | ordered      |
| `sim_step`    | Advance exactly one tick           | ordered      |
| `sim_reset`   | Reset to initial state             | ordered      |

## Threading

```
Network Thread (receive)
    │
    │ reads TCP, splits by '\n'
    ▼
ConcurrentQueue<string> (incoming lines)
    │
    │ dequeued on Unity main thread (Update or simulation tick)
    ▼
CommandDispatcher.ProcessQueue()
    │
    │ routes by command name
    ├──► VehicleSystem.SetMotorCommand()
    ├──► SimulationManager.StartSimulation()
    ├──► SimulationManager.Pause()
    └──► ...
```

- **Never** mutate Unity objects from the receive thread.
- **Only** the main thread touches vehicle, simulation, or dispatch logic.
- The queue is a `ConcurrentQueue<string>` (lock-free, thread-safe).

## Simulation tick boundary

Commands are consumed at the start of the simulation tick cycle (or in
`Update()` when the simulation is paused/ready). This makes command timing
deterministic and independent of network jitter.

## Motor watchdog

If no `set_motor` command is received within `commandTimeoutMs` (default
1000 ms) of wall-clock time, the bridge automatically sets speed to 0
while preserving the current steering values. This prevents runaway
vehicles if the Python process crashes.

## Disconnect safety

On client disconnect, the bridge immediately sets speed to 0 (within one
simulation tick). Steering position is unchanged. Unity does not crash.

## Bridge assembly

```
JajuchaSim.Bridge
  ├── BridgeConfig.cs          — ScriptableObject config
  ├── BridgeMessage.cs         — Protocol DTOs
  ├── BridgeProtocol.cs        — JSON serialize/deserialize
  ├── BridgeConnection.cs      — TCP server + background I/O
  ├── CommandDispatcher.cs     — Main-thread command routing
  └── JajuchaBridgeServer.cs   — MonoBehaviour (wires everything)
```

Dependencies:
- `JajuchaSim.Core` (SimulationManager)
- `JajuchaSim.Vehicle` (VehicleSystem)

Not the other way around — the bridge never knows about physics internals.

## Python package layout

```
python/
  jchm/
    __init__.py      — Public API surface
    control.py       — jchm.control.set_motor(...)
    _backend.py      — Backend selection (real vs sim)
    _sim_backend.py  — TCP client implementation
    _protocol.py     — Message construction helpers
    errors.py        — JCHM-specific exceptions
  jchm_sim/
    __init__.py      — Simulator-only tools
    simulation.py    — start/pause/step/reset/status
  tests/
    test_control.py
    test_protocol.py
```

## Key design rules

1. **Bridge must not understand vehicle physics** — it only translates
   messages to `VehicleSystem.SetMotorCommand()` and `SimulationManager.*`.
2. **Python backend abstraction** — `jchm.control` delegates to a backend;
   the simulator backend talks TCP; a real backend would talk to real hardware.
3. **latest-wins for motor commands** — if multiple `set_motor` arrive before
   the next simulation tick, only the last one is applied.
4. **Default to quiet** — no debug output unless `JCHM_SIM_DEBUG=1`.
5. **Fail clearly** — Python gets `JchmConnectionError` with a descriptive
   message, not a raw `ConnectionRefusedError`.

## Testing

- **EditMode tests**: protocol serialize/deserialize, command dispatch logic,
  error handling, latest-wins semantic, unknown commands.
- **PlayMode tests**: full TCP integration — connect, handshake, set_motor,
  zero-speed invariant, disconnect safety, reconnect, protocol mismatch.
- **Python tests**: control function argument forwarding, clamping, backend
  error propagation, protocol message construction.

## Performance target

Motor control round-trip over localhost: **< 5 ms** (well below one
simulation frame at 100 Hz = 10 ms). Correctness matters more than
micro-optimisation at this stage.
