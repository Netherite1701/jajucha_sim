# Course Format

> **Steps 6–7 — Implemented.** Shared tile grid + structures/objects/triggers.

## Shared tile grid

One fixed-size tile grid is the authoritative course representation. Roads and
structures use identical tile dimensions and coordinates. The renderer may
generate smooth visual geometry (mesh, lines, colliders, markings) from tiles,
but the underlying course data stays tile-based; a spline is never the source of
truth.

Layers (may overlap — a single tile can contain data on all four layers):

- **Road Layer** — boolean: is this tile a drivable road?
- **Structure Layer** — optional `StructureType` per tile (Tunnel, Ramp, None).
- **Object Layer** — optional `ObjectType` per tile (Obstacle, Sign,
  StartSignal, None).
- **Trigger Layer** — optional `TriggerType` per tile (SlowZone, SpeedTerminal,
  EventTrigger, Start, Finish, None). `SpeedGate` remains a legacy enum alias
  for `SpeedTerminal`.

`Tile (12, 8)` may simultaneously be `Road=true`, `Structure=Tunnel`,
`Trigger=SlowZone`. A tunnel does **not** replace the road underneath it.

## Configuration

`CourseConfig.tileSizeCm` (default 20 cm) is stored in physical centimetres
(matching the world-scale convention: 1 Unity unit = 1 cm). All placement and
snapping uses the grid.

## Coordinate system

`GridCoordinate(x, z)` uses integers. The physical centre of a tile in world
space is at `((x + 0.5) * tileSizeCm, 0, (z + 0.5) * tileSizeCm)`.

Rectangular footprints use `GridRegion { x, z, width, height }` (whole tiles).

## Serialization schema (Step 7)

Each structure/object/trigger is an individual entry with a unique `id`:

```json
{
  "tileSizeCm": 20,
  "road": [
    {"x": 10, "z": 10},
    {"x": 10, "z": 11}
  ],
  "structures": [
    {
      "id": "tunnel_001",
      "type": "tunnel",
      "region": {"x": 20, "z": 30, "width": 4, "height": 8},
      "heightCm": 55,
      "wallThicknessCm": 2
    },
    {
      "id": "ramp_001",
      "type": "ramp",
      "region": {"x": 12, "z": 30, "width": 3, "height": 6},
      "direction": "north",
      "riseCm": 30
    }
  ],
  "objects": [
    {
      "id": "obstacle_001",
      "type": "obstacle",
      "tile": {"x": 8, "z": 8},
      "rotationDeg": 0,
      "footprint": "1x1"
    },
    {
      "id": "slow_sign_001",
      "type": "slow_sign",
      "tile": {"x": 18, "z": 29},
      "rotationDeg": 90
    },
    {
      "id": "start_signal_001",
      "type": "start_signal",
      "tile": {"x": 0, "z": 0},
      "rotationDeg": 0
    }
  ],
  "triggers": [
    {
      "id": "slow_zone_001",
      "type": "slow_zone",
      "region": {"x": 20, "z": 40, "width": 4, "height": 10}
    },
    {
      "id": "start_001",
      "type": "start",
      "region": {"x": 0, "z": 0, "width": 2, "height": 1}
    },
    {
      "id": "finish_001",
      "type": "finish",
      "region": {"x": 40, "z": 0, "width": 2, "height": 1}
    },
    {
      "id": "event_001",
      "type": "event",
      "region": {"x": 20, "z": 50, "width": 2, "height": 4},
      "eventId": "tunnel_entry"
    },
    {
      "id": "speed_a",
      "type": "speed_terminal",
      "pairId": "speed_zone_01",
      "terminal": "A",
      "cellX": 20,
      "cellZ": 30,
      "edge": "north",
      "widthTiles": 1
    },
    {
      "id": "speed_b",
      "type": "speed_terminal",
      "pairId": "speed_zone_01",
      "terminal": "B",
      "cellX": 20,
      "cellZ": 40,
      "edge": "north",
      "widthTiles": 1
    }
  ]
}
```

### Speed terminal pairs

Competition speed uses **two** edge-snapped terminals that share a `pairId`:

```
speed_zone_01
├─ Terminal A  (role A)
└─ Terminal B  (role B)
```

Distance `d` is **derived** from the terminals' world-space line midpoints
(never entered by hand). Official measured speed is:

```
v = d / (t2 - t1)
```

where `t1` / `t2` are `SimulationClock` times when the vehicle segment `P0→P1`
crosses each terminal line. Rigidbody / internal vehicle velocity is **not**
the official result.

Fields per terminal:

| Field | Meaning |
|---|---|
| `type` | `"speed_terminal"` (legacy `"speed_gate"` still loads) |
| `pairId` | Links A and B into one measurement zone |
| `terminal` | `"A"` or `"B"` (A→B is the valid direction) |
| `cellX` / `cellZ` | Anchor cell |
| `edge` | `north` / `south` / `east` / `west` |
| `widthTiles` | Line span across the road (≥1) |

Reverse order (B then A) is ignored by default.

Auto-generated IDs look like `tunnel_001`. Users may rename; uniqueness is
validated before save.

Legacy Step-6 JSON (structures/triggers grouped by type with `tiles: [...]`
and no `region`/`id`) still loads via a compatibility path that expands each
tile into a 1×1 instance.

## Runtime API

### CourseGrid (compact lookup)

- Road/structure/object/trigger set/clear/query helpers
- `GridToWorld` / `WorldToGrid`
- `GetTileInfo(coord)` — snapshot of all four layers
- `CourseSerializer.ToJson(grid) / FromJson(json)` — grid-centric round-trip

### CourseDocument (instances + grid)

- `PlaceTunnel` / `PlaceRamp` / `PlaceObject` / `PlaceTrigger` / `PlaceSpeedTerminal`
- `PlaceSpeedGate` remains as a thin legacy wrapper (Terminal A, default pair)
- Move / resize / rotate / remove by id
- `ToJson` / `FromJson` / `ToData` / `FromData`
- `PaintTriggerTiles` for slow-zone / start / finish painting

### TriggerDetectionSystem

- Region enter/exit → `TriggerEnteredEvent` / `TriggerExitedEvent` (once each)
- Generic event → `CourseEventTriggeredEvent(eventId, …)`
- Speed terminal → segment P0→P1 vs terminal line → `SpeedTerminalCrossedEvent`
  (legacy `SpeedGateCrossedEvent` still published for older subscribers)

### SpeedTerminalPairRule

- Subscribes to `SpeedTerminalCrossedEvent`
- Arms on Terminal A, completes on Terminal B
- Computes `v = d / (t2 - t1)` with `d` from `SpeedTerminalGeometry.DistanceCm`
- Publishes `SpeedMeasuredEvent` (official competition speed for scoring)
- Debug panel text via `FormatDebugPanel()`; event log lines:
  `31.240  speed_a CROSS` / `31.890  SPEED = 30.77 cm/s`

### Map editor (standalone)

`MapEditorSession` + `MapEditorHud` provide the runtime palette, preview,
inspector, layers, undo/redo, save/load, and test-drive loop inside the player
build. Debug overlays use `SimLayers.SimulatorDebug` (observer only).

## CourseSystem

`CourseSystem` implements `ISimulationSystem` and exposes the active
`CourseDocument` / `CourseGrid` to other systems. Trigger evaluation is owned
by `TriggerDetectionSystem`.

## Planned additions

- Road mesh generation from neighbour connectivity (smooth continuous roads).
- Course image import (pixel→grid projection).
- Full scenario/scoring stack consuming `SpeedMeasuredEvent` (Step 9).

## Shipped 2026 courses

`Courses/2026_preliminary.json` and `Courses/2026_final.json` are the only
shipped courses. Both use a 5 cm logical mask and include a `competition2026`
block with edition, stage, 990×540 cm physical size, the exact 41-panel
inventory, ordered checkpoints, visual profile, and five mission candidates.

Structures store both a validation region and `pathPoints`. Tunnel entries
also store the 39 cm opening, 22 cm height, and 26/9.8 cm roof dimensions.
Hill entries store the 0/10/10/0 cm height profile for slope, 900×900×100 mm
flat block, and descent.

Mission objects are injected at run start. Yellow flag mode creates one pair
of speed terminals exactly 30 cm apart; dynamic obstacle mode creates the
configured moving obstacle. Legacy tile-array course files are not loaded.

`Competition2026CourseTests` and `Competition2026Specification.ValidateDocument`
verify the official inventory, dimensions, order, structure profiles, and
candidate geometry.
