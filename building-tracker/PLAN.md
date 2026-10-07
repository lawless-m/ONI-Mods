# Building Tracker Mod for ONI

## Context

ONI's built-in reports show per-cycle totals but no per-building breakdown of resource consumption/production. No existing mod fills this gap. This mod will track what every building consumes and produces each cycle, giving players detailed visibility into their colony's resource flows.

## Approach

**Harmony patches** on the core resource flow methods, accumulating per-building stats into a dictionary. At end-of-cycle, flush the stats to a SQLite database and reset.

## Storage

**SQLite** via [sqlite-net](https://github.com/praeclarum/sqlite-net) (single embedded `SQLite.cs` file, MIT licensed). P/Invokes against the native `sqlite3` that ships with Unity/Mono — no native binaries to bundle.

**Database location:** `<SaveFilesDir>/BuildingTracker/tracker.db`

### Schema

```sql
CREATE TABLE buildings (
    cycle       INTEGER NOT NULL,
    prefab_id   TEXT NOT NULL,
    name        TEXT NOT NULL,
    cell_x      INTEGER NOT NULL,
    cell_y      INTEGER NOT NULL,
    width       INTEGER NOT NULL,
    height      INTEGER NOT NULL,
    PRIMARY KEY (cycle, prefab_id, cell_x, cell_y)
);

CREATE TABLE resource_flows (
    cycle       INTEGER NOT NULL,
    prefab_id   TEXT NOT NULL,
    cell_x      INTEGER NOT NULL,
    cell_y      INTEGER NOT NULL,
    resource    TEXT NOT NULL,
    amount      REAL NOT NULL,
    direction   TEXT NOT NULL,  -- 'consumed' | 'produced'
    category    TEXT NOT NULL,  -- 'element' | 'conduit_in' | 'conduit_out' | 'power_consumed' | 'power_generated'
    PRIMARY KEY (cycle, prefab_id, cell_x, cell_y, resource, direction, category)
);
```

## Files to Create

### 1. `BuildingTrackerMod.csproj`
Copy from `critter-dispatch` template — same references, `net472`, `LangVersion latest`.

### 2. `mod.yaml` / `mod_info.yaml`
Standard mod metadata. `staticID: BuildingTrackerMod`.

### 3. `BuildingTrackerMod.cs`
Entry point — `UserMod2` subclass. `OnLoad` calls `base.OnLoad(harmony)` to auto-apply patches.

### 4. `SQLite.cs`
Embedded from sqlite-net (praeclarum/sqlite-net). Single-file ORM, MIT licensed.

### 5. `Tracker.cs` — Core tracking logic
- Static `Dictionary<(int cellX, int cellY, string prefabID, string resource, string direction, string category), float>` to accumulate mass/joules per tick.
- `Record(...)` — adds to accumulator.
- Subscribe to `GameHashes.NewDay` (hash `631075836`) on `GameClock.Instance` to flush at cycle end.
- `Flush()` — opens SQLite db, inserts resource_flows rows, calls ExportBuildings(), clears dictionary.
- `ExportBuildings()` — iterates `Components.BuildingCompletes`, inserts into buildings table for current cycle.

### 6. `Patches.cs` — Harmony patches

Four patches:

| Patch | Target | What it records |
|---|---|---|
| `ElementConverterPatch` | `ElementConverter.ConvertMass` (private, via AccessTools) Postfix | Loop `consumedElements`/`outputElements`, read `.Rate` (live average from accumulators) × dt. Records per-element mass consumed/produced. |
| `ConduitConsumerPatch` | `ConduitConsumer.ConduitUpdate` Postfix | If `consumedLastTick`, record `lastConsumedElement` mass from the conduit contents. |
| `EnergyConsumerPatch` | `EnergyConsumer.EnergySim200ms` Postfix | Record `WattsUsed * dt` joules as power consumed. |
| `GeneratorPatch` | `Generator.GenerateJoules` Postfix | Record `joulesAvailable` parameter as power generated. |

Each patch gets the building name via `__instance.GetComponent<BuildingComplete>()?.Def.PrefabID` and the cell via `Grid.PosToCell(__instance.transform.position)`.

## Key Design Decisions

- **SQLite over CSV**: Single file, queryable, no file-per-cycle clutter. The web viewer can use sql.js (WASM) to query directly in the browser.
- **sqlite-net embedded source**: Single `SQLite.cs` file avoids NuGet/native bundling headaches. P/Invokes against Unity's bundled sqlite3.
- **Aggregate per cycle, not per tick**: Keeps memory usage low. One dictionary entry per (building-instance, resource, direction) combo.
- **Use `.Rate` on ElementConverter where possible**: The game already maintains running averages via its accumulator system.
- **ConduitConsumer over ConduitFlow**: Patch at the building level, not the pipe level — gives us the building identity directly.

## Build & Deploy

```bash
cd /home/matt/Git/ONI-Mods/building-tracker
dotnet build
# DLL goes to bin/Debug/net472/BuildingTrackerMod.dll
# Copy mod folder to ~/.config/unity3d/Klei/Oxygen Not Included/mods/Dev/building-tracker/
```

## Verification

1. `dotnet build` compiles without errors
2. Manual test: load a save with electrolyzers, generators, pumps — check SQLite db after one cycle
3. Verify db contains expected buildings and reasonable values (e.g., electrolyzer consuming ~1 kg/s water = ~600 kg/cycle)

---

# Phase 2: Web Viewer

## Context

A browser-based viewer that reads the mod's SQLite database (via sql.js WASM) and renders an interactive map of the base, with per-building annotations showing resource flows per cycle.

## Location

`/home/matt/Git/ONI-Mods/building-tracker/viewer/` — static HTML/JS, no build step.

## Files

### `viewer/index.html`
Single-page app. File input to load `tracker.db` (drag-and-drop).

### `viewer/app.js`
Core logic:

- **SQL.js loading** — Load tracker.db into sql.js, query buildings and resource_flows tables.
- **Grid renderer** — Canvas-based. Each building drawn as a rectangle at its (X, Y) position with correct width/height. Color-coded by building category (power, production, plumbing, etc.).
- **Click interaction** — Click a building to see a tooltip/panel with:
  - Building name and position
  - Resources consumed/produced this cycle (kg, joules)
  - Sparkline or bar chart of consumption over all loaded cycles
- **Cycle slider** — Scrub through cycles. Buildings light up based on activity (idle = dim, active = bright). Stats panel updates to selected cycle.
- **Zoom/pan** — Mouse wheel zoom, click-drag pan. ONI maps can be 256×384 tiles.

### `viewer/style.css`
Minimal styling for the controls panel, tooltip, and layout.

## Design Decisions

- **Static files, no server** — Open `index.html` directly or via `python -m http.server`. No npm/webpack.
- **sql.js** — SQLite compiled to WASM, queries the db directly in the browser. No server-side processing.
- **Canvas over DOM** — Hundreds of buildings on a large grid, DOM nodes would be too slow.
- **Cycle slider over animation** — Let the user scrub rather than auto-play. Simpler and more useful.

## Future Enhancements (not in v1)

- Terrain/tile export from mod (background layer showing natural tiles, dug-out areas)
- Pipe network overlay (conduit connections between buildings)
- Heat map overlay (temperature per tile)
- Search/filter buildings by type or resource
- Export annotated map as image
