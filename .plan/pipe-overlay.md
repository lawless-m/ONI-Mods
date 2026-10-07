# Pipe Network Overlay Mod - Implementation Plan

## Goal
Patch the existing plumbing and ventilation overlays to tint each separate pipe network a different color, so players can visually distinguish connected networks at a glance.

## Approach: Two Harmony Patches

### Patch 1: Pipe building sprites — `OverlayModes.ConduitMode.Update()`
- **What it does now:** Sets `KBatchedAnimController.TintColour` on each visible pipe building based on pipe type (normal/insulated/radiant) using named colors from `GlobalAssets.colorSet`
- **What we change:** Postfix patch. After the base `Update()` runs and sets tint colors, iterate visible conduit targets, look up each cell's network ID via `ConduitFlow.GetNetwork()`, and multiply the existing tint by a network-derived color from our palette
- This preserves insulated/radiant visual differences (tint overlay, not replace)

### Patch 2: Flow ball colors — `ConduitFlowVisualizer.GetCellTintColour()`
- **What it does now:** Returns the tint color (normal/insulated/radiant) for a given cell
- **What we change:** Postfix patch. Take the returned Color32 and multiply it by the same network color used in Patch 1
- This keeps the balls consistent with the pipe tint

### Network → Color Mapping
- Use `ConduitFlow.GetNetwork(conduit)` to get `UtilityNetwork` with its `.id`
- Map `network.id % N` to a palette of N visually distinct colors (12-16 colors, HSV-spaced)
- Colors are applied as a multiplicative tint so pipe type differences remain visible
- Disconnected/no-network pipes keep their default color

## Files
```
pipe-overlay/
├── PipeOverlayMod.cs      # UserMod2 entry point + both Harmony patches
├── NetworkColorPalette.cs  # Color palette and network→color lookup
├── pipe-overlay.csproj     # Standard project file (same template as other mods)
└── mod.yaml                # Mod metadata
```

## Verification
1. `dotnet build` compiles without errors
2. Manual test: load ONI, open plumbing overlay, confirm different networks show different colors
