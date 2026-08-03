# Jajucha Simulator Bridge Protocol v1

## Overview

- Transport: TCP, newline-delimited JSON (`\n` terminates each message).
- Default endpoint: `127.0.0.1:8765`.
- Single-client: the server accepts at most one control client at a time.
- Protocol is defined once and versioned; fields are never silently changed.

## Handshake

Every connection **must** begin with a `hello` message. The server responds
with `hello_ack` on success or an error on version mismatch.

### Client → Server

```json
{
  "type": "hello",
  "protocol": 1,
  "client": "jchm-sim"
}
```

### Server → Client (success)

```json
{
  "type": "hello_ack",
  "protocol": 1,
  "simulator": "JajuchaSim"
}
```

### Server → Client (version mismatch)

```json
{
  "type": "error",
  "code": "PROTOCOL_VERSION_MISMATCH",
  "message": "Expected protocol v1, got v99"
}
```

## Command Envelope

All commands use a consistent request/response envelope:

### Request

```json
{
  "type": "command",
  "id": <number>,
  "name": "<command-name>",
  "payload": { <command-specific-fields> }
}
```

### Success Response

```json
{
  "type": "response",
  "id": <number>,
  "ok": true
}
```

Some commands include additional data in the response:

```json
{
  "type": "response",
  "id": <number>,
  "ok": true,
  "payload": { <response-fields> }
}
```

### Error Response

```json
{
  "type": "response",
  "id": <number>,
  "ok": false,
  "error": {
    "code": "<ERROR_CODE>",
    "message": "<human-readable description>"
  }
}
```

## Commands

### ping

Health check. No payload required.

**Request:**
```json
{ "type": "command", "id": 1, "name": "ping", "payload": {} }
```

**Response:**
```json
{ "type": "response", "id": 1, "ok": true, "payload": { "sim_time": 12.34 } }
```

---

### set_motor

Set front steering and rear drive command.

**Request:**
```json
{
  "type": "command",
  "id": 2,
  "name": "set_motor",
  "payload": {
    "left": -5,
    "right": -5,
    "speed": 3
  }
}
```

Fields:
- `left` (int, -10..10): front-left steering.
- `right` (int, -10..10): front-right steering.
- `speed` (int, -30..30): rear drive speed. 0 = stop.

**Policy:** Latest-wins: if multiple `set_motor` arrive before the next
simulation tick, only the last one is applied.

**Response:** `{ "type": "response", "id": 2, "ok": true }`

If speed is 0, propulsion force is **exactly zero** while steering values
are preserved (zero-speed invariant).

---

### get_status

Request full simulator and vehicle state.

**Request:**
```json
{ "type": "command", "id": 3, "name": "get_status", "payload": {} }
```

**Response:**
```json
{
  "type": "response",
  "id": 3,
  "ok": true,
  "payload": {
    "state": "Running",
    "tick": 5230,
    "sim_time": 52.30,
    "vehicle": {
      "command": {
        "left": -5,
        "right": -5,
        "speed": 3
      }
    }
  }
}
```

---

### sim_start

Start or resume the simulation.

**Request:**
```json
{ "type": "command", "id": 4, "name": "sim_start", "payload": {} }
```

**Response:** `{ "type": "response", "id": 4, "ok": true }`

---

### sim_pause

Pause the simulation.

**Request:**
```json
{ "type": "command", "id": 5, "name": "sim_pause", "payload": {} }
```

**Response:** `{ "type": "response", "id": 5, "ok": true }`

---

### sim_step

Advance exactly one simulation tick (useful when paused for deterministic
stepping).

**Request:**
```json
{ "type": "command", "id": 6, "name": "sim_step", "payload": {} }
```

**Response:** `{ "type": "response", "id": 6, "ok": true }`

---

### sim_reset

Reset the simulation to its initial state (vehicle returns to spawn, tick
counter resets to 0, motor command cleared).

**Request:**
```json
{ "type": "command", "id": 7, "name": "sim_reset", "payload": {} }
```

**Response:** `{ "type": "response", "id": 7, "ok": true }`

## Error Codes

| Code                        | Description                                     |
|-----------------------------|-------------------------------------------------|
| `INVALID_MESSAGE`           | JSON parse failure or unknown message type      |
| `INVALID_ARGUMENT`          | Missing or invalid payload fields               |
| `UNKNOWN_COMMAND`           | The command name is not recognised              |
| `NOT_READY`                 | Command received before handshake completed     |
| `PROTOCOL_VERSION_MISMATCH` | Client hello protocol version does not match    |
| `INTERNAL_ERROR`            | Unexpected error on the simulator side          |

## Connection State Machine

```
Disconnected ──►startListening──► Listening
                                      │
                                 client connects
                                      │
                                      ▼
                                  Connected
                                      │
                                  handshake
                                      │
                                      ▼
                              HandshakeComplete
                                      │
                                  commands
                                      │
                          ┌───────────┴───────────┐
                          │                       │
                     disconnect              watchdog timeout
                          │                       │
                          ▼                       │
                     Disconnected ◄────────────────┘
```

## Protocol Compatibility

Once v1 is declared stable:
- Never silently change an existing field's meaning.
- Use protocol v2 or add optional fields for new features.
- The handshake ensures both sides agree on the protocol version.
