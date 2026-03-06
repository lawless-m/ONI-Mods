# ONI REPL

An in-game console for Oxygen Not Included. Open it with backtick (`` ` ``), type commands, build things, spawn stuff, define reusable words — all without leaving the game.

## Quick Start

Press `` ` `` to open the console. Press `` ` `` or `Esc` to close it. Type `help` for a full command listing.

```
> cursor info
Cell (127,84) #21887
  Element: Oxygen (Oxygen)
  Mass: 1.8kg
  Temp: 300.2K (27.0C)

> granite tile cursor build
Placed build order: Tile (Granite) p5 at cell 21887

> water 1000kg cursor spawn
Spawned 1000kg of Water at cell 21887
```

## How It Works

Type things to **set registers**, type commands to **do things**. That's it.

### Registers

Typing a value sets the corresponding register. It stays set until you change it.

| Register | Set by | Example |
|----------|--------|---------|
| Position | `cursor`, `printer`, `x,y` | `cursor`, `50,30` |
| Material | Any element name | `granite`, `sandstone`, `steel` |
| Building | Any building name | `tile`, `gaspipe`, `ladder` |
| Priority | `N priority` | `9 priority` |
| Count | Any plain number | `3`, `10` |
| Quantity | Number with unit | `100kg`, `500g`, `1t` |
| Critter | Any critter name | `hatch`, `pacu` |

All name lookups are case-insensitive with prefix matching — type just enough to be unambiguous.

### Commands

#### Building

```
> granite tile cursor build              \ set material, building, position, then build
> build right build right build up build \ chain builds — registers persist
> 9 priority                             \ set priority (1-9), affects subsequent builds
```

`build` reads position, material, and building registers. They stay set, so you can chain with direction words.

| Word | Effect |
|------|--------|
| `left` | Move position x - 1 |
| `right` | Move position x + 1 |
| `up` | Move position y + 1 |
| `down` | Move position y - 1 |
| `flip` | Toggle orientation (Neutral → FlipH → FlipV) |

`flip` is one-shot — `build` resets orientation to Neutral after placing.

#### Digging

```
> cursor dig                             \ dig at cursor
> cursor dig 4 do up dig loop            \ dig a 5-tall shaft
```

#### Spawning (DISABLES ACHIEVEMENTS)

```
> water 1000kg cursor spawn              \ spawn element (mass in grid)
> hatch cursor critter                   \ spawn critter
> mushbar cursor item                    \ spawn any item (food, ore, seeds, etc.)
```

`spawn` creates element mass in the simulation. `item` drops a loose object.

#### Reveal (DISABLES ACHIEVEMENTS)

```
> reveal                                 \ remove all fog of war
```

#### Waiting

```
> granite tile cursor build right build right build wait
```

`wait` suspends the engine until all tracked build orders are completed by duplicants. `clear` cancels a wait.

#### Inspection

```
> cursor info                            \ cell element, mass, temperature
> buildings list                         \ placed buildings
> buildables list                        \ all building types
> elements list                          \ all elements
> critters list                          \ living critters
> dupes list                             \ duplicant names
> items list                             \ spawnable items (food, seeds, medicine, ore)
> geysers list                           \ geyser types and locations (DISABLES ACHIEVEMENTS)
> 45,82 goto                             \ move camera to coordinates
```

### Defining Words

```
> : L build down build down build right build right build ;
> granite gaspipe cursor L
> sandstone tile cursor L               \ same shape, different material+building
```

Words are late-bound — redefining a word changes everything that uses it:

```
> : mat granite ;
> mat tile cursor build                  \ granite
> : mat sandstone ;
> mat tile cursor build                  \ now sandstone
```

### Loops

`do`/`loop` repeats a body N times:

```
> 5 do build right loop                  \ build 5 tiles rightward
> : corridor 10 do dig right loop ;      \ reusable tunnel word
> : floor 4 do build right loop build ;  \ build 5 tiles (4 moves + 1 initial)
```

### Persistence

```
> save                                   \ save user words to oni.repl
> load                                   \ reload oni.repl
> blueprints load                        \ load blueprints.repl
> "/path/to/my file.repl" load           \ full path (quotes for spaces)
```

Words are saved as plain source — editable in any text editor. `oni.repl` auto-loads on startup. Full paths work for `load` — use quotes if the path contains spaces.

### Display

```
> 28 fontsize                            \ set font size (auto-scales to screen by default)
```

Put `28 fontsize` in `oni.repl` to persist it.

### Comments

```
> granite tile cursor build              \ backslash comments to end of line
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
| `ForthEngine.cs` | Tokenizer, registers, execution loop, suspension/continuation |
| `ReplConsole.cs` | IMGUI overlay, input handling, history |
| `ReplMod.cs` | Harmony patches: game hook, build tracking, input/zoom blocking |
| `Resolvers.cs` | Fuzzy name resolution for elements, buildings, critters, locations |
| `Words/BuildWord.cs` | `build`, `priority`, `wait`, `clear` |
| `Words/DigWord.cs` | `dig` |
| `Words/DirectionWords.cs` | `left`, `right`, `up`, `down` |
| `Words/FlipWord.cs` | `flip` (building orientation) |
| `Words/RevealWord.cs` | `reveal` (remove fog of war) |
| `Words/GotoWord.cs` | `goto` (camera navigation) |
| `Words/SpawnWord.cs` | `spawn` (elements), `critter` |
| `Words/ItemWord.cs` | `item` (any prefab) |
| `Words/InfoWord.cs` | `info` (cell inspection) |
| `Words/ListWord.cs` | `list` (game object enumeration) |
| `Words/FileWords.cs` | `save`, `load` |
| `Words/FontSizeWord.cs` | `fontsize` |
| `Words/HelpWord.cs` | `help` |
