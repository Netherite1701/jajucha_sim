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
- **Trigger Layer** — optional `TriggerType` per tile (SlowZone, SpeedGate,
  EventTrigger, Start, Finish, None).

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
      "id": "speed_gate_001",
      "type": "speed_gate",
      "cellX": 20,
      "cellZ": 30,
      "edge": "north"
    }
  ]
}
```

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

- `PlaceTunnel` / `PlaceRamp` / `PlaceObject` / `PlaceTrigger` / `PlaceSpeedGate`
- Move / resize / rotate / remove by id
- `ToJson` / `FromJson` / `ToData` / `FromData`
- `PaintTriggerTiles` for slow-zone / start / finish painting

### TriggerDetectionSystem

- Region enter/exit → `TriggerEnteredEvent` / `TriggerExitedEvent` (once each)
- Generic event → `CourseEventTriggeredEvent(eventId, …)`
- Speed gate → segment P0→P1 vs gate line → `SpeedGateCrossedEvent`

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
- Scenario/scoring reactions to trigger events (Step 8).
