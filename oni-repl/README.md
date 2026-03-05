# ONI REPL

A Forth-like in-game console for Oxygen Not Included. Open it with backtick (`` ` ``), type commands, build things, spawn stuff, define reusable words — all without leaving the game.

## Quick Start

Press `` ` `` to open the console. Press `` ` `` or `Esc` to close it. Type `help` for a full command listing.

```
> cursor info
Cell (127,84) #21887
  Element: Oxygen (Oxygen)
  Mass: 1.8kg
  Temp: 300.2K (27.0C)

> sandstone tile cursor build
Placed build order: Tile (SandStone) p5 at cell 21887

> water 500kg cursor spawn
Spawned 500kg of water at cell 21887
```

## Language

The REPL uses a Forth-style stack language. Values go on the stack, words consume and produce values.

### Value Types

| Type | Examples | Notes |
|------|----------|-------|
| Element | `sandstone`, `water`, `granite` | Prefix matching, case-insensitive |
| Quantity | `100kg`, `500g`, `1t` | Units: kg, g (÷1000), t (×1000) |
| Location | `cursor`, `printer`, `50,30` | Mouse position, printing pod, or x,y coords |
| Building | `tile`, `gaspipe`, `ladder` | Prefix matching against BuildingDefs |
| Critter | `hatch`, `pacu`, `drecko` | Prefix matching against creature prefabs |
| Number | `5`, `9` | Plain integers |

All name lookups are case-insensitive with prefix matching — type just enough to be unambiguous.

### Stack Manipulation

| Word | Stack Effect | Description |
|------|-------------|-------------|
| `dup` | `a -- a a` | Duplicate top |
| `drop` | `a --` | Discard top |
| `swap` | `a b -- b a` | Swap top two |
| `rot` | `a b c -- b c a` | Rotate third to top |
| `.s` | — | Print stack (non-destructive) |
| `reset` | — | Clear entire stack |

### Building

Build position and priority are **state variables**, not stack values. Set them and they persist across commands.

```
> 5 priority                    \ set priority (1-9, default 5)
> cursor                        \ set build position to mouse
> 50,30                         \ set build position to coordinates
```

#### `build` — Place a build order

Expects material and building on the stack. Peeks both (they stay on stack for chaining).

```
> sandstone tile cursor build
> granite ladder 50,30 build
```

#### `built` — Build and clean up

Like `build`, but drops the material and building from the stack afterward.

```
> sandstone tile cursor built
```

#### Direction words — Move build position

After setting an initial position, move it relative:

```
> sandstone tile cursor build right build right build up build
```

| Word | Effect |
|------|--------|
| `left` | x - 1 |
| `right` | x + 1 |
| `up` | y + 1 |
| `down` | y - 1 |

#### `dig` — Place a dig errand

```
> cursor dig                           \ dig at cursor
> cursor dig 4 do up dig loop          \ dig a shaft
> : tunnel 10 do dig right loop ;      \ define a horizontal tunnel
```

Respects priority. Chains with directions and loops.

#### `fill` — Fill a rectangle

```
> sandstone tile 50,30 55,33 fill
```

Pops two locations, peeks material and building.

#### `wait` — Wait for builds to complete

Suspends the engine until all tracked build orders are constructed by duplicants. Any further input is queued.

```
> granite tile cursor build right build right build wait
```

#### `clear` — Cancel wait and tracked builds

Unsticks a suspended engine (e.g. if you cancelled build errands).

### Spawning

```
> water 500kg cursor spawn       \ element with quantity
> water cursor spawn             \ element (defaults to 100kg)
> hatch cursor spawn             \ critter
```

Spawn suppresses achievements (tagged per-command, not blanket).

### Inspection

```
> cursor info                    \ cell element, mass, temperature
> buildings list                 \ placed buildings by type
> buildables list                \ all building defs
> elements list                  \ all element types
> critters list                  \ living critters by type
> dupes list                     \ duplicant names
```

### Defining Words

Standard Forth word definitions with `: name ... ;`

```
> : L build right build right build up build up built ;
```

Words are referentially transparent — shapes are generic, materials compose at the call site:

```
> : L build right build right build up build up built ;
> cursor granite gaspipe L wait
> cursor sandstone tile L wait
```

### Loops

`do`/`loop` repeats a body N times:

```
> 5 do build right loop              \ build 5 tiles rightward
> cursor dig 9 do up dig loop        \ dig a 10-tall shaft
> : corridor 10 do dig right loop ;  \ reusable tunnel word
```

Works inside word definitions. Supports nesting.

A practical build sequence:

```
> : floor 4 do build right loop built ;
> : wall 4 do build up loop built ;
> 9 priority granite tile cursor floor
> granite tile cursor wall wait
```

### Persistence

```
> save                           \ save user words to init.forth
> load                           \ reload init.forth
> blueprints load                \ load blueprints.forth
```

Words are saved as plain Forth source — editable in any text editor. `init.forth` auto-loads on startup. Put `28 fontsize` in there to set your preferred font size every session.

### Display

```
> 28 fontsize                    \ set console font size (auto-scales to screen by default)
```

### Comments

Backslash (`\`) comments to end of line:

```
> sandstone tile cursor build    \ place a sandstone tile at cursor
```

## Build & Install

### Prerequisites

- .NET SDK (targets .NET Framework 4.7.2)
- Oxygen Not Included installed

### Build

```bash
cd oni-repl
dotnet build -c Release
```

The DLL auto-deploys to `~/.config/unity3d/Klei/Oxygen Not Included/mods/Dev/OniRepl/` on build.

### Manual Install

Copy to your ONI mods folder:
- `OniRepl.dll`
- `mod.yaml`
- `mod_info.yaml`

## Architecture

| File | Purpose |
|------|---------|
| `ForthEngine.cs` | Tokenizer, stack, execution loop, suspension/continuation |
| `ReplConsole.cs` | IMGUI overlay, input handling, history |
| `ReplMod.cs` | Harmony patches: game hook, build completion tracking, input blocking |
| `Resolvers.cs` | Fuzzy name resolution for elements, buildings, critters, locations |
| `Words/BuildWord.cs` | `build`, `built`, `fill`, `priority`, `wait`, `clear` |
| `Words/DigWord.cs` | `dig` |
| `Words/DirectionWords.cs` | `left`, `right`, `up`, `down` |
| `Words/SpawnWord.cs` | `spawn` (elements and critters) |
| `Words/InfoWord.cs` | `info` (cell inspection) |
| `Words/ListWord.cs` | `list` (game object enumeration) |
| `Words/StackWords.cs` | `dup`, `drop`, `swap`, `rot`, `.s`, `reset` |
| `Words/FileWords.cs` | `save`, `load` |
| `Words/FontSizeWord.cs` | `fontsize` |
| `Words/HelpWord.cs` | `help` |

## How It Works

- **Harmony patches** attach the console to `Game.OnSpawn` and block game input while the REPL is open
- **Build tracking** patches `Constructable.OnCompleteWork` to detect when build orders complete, enabling `wait` to synchronize multi-step blueprints
- **Achievement suppression** is per-command — only `spawn` marks the save as debug-used
- **Fuzzy resolution** means you can type `sand` instead of `SandStone`, `gas` instead of `GasPipe`, etc.
