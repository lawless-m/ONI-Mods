# Storage Tooltip Mod for Oxygen Not Included

A simple mod that displays storage contents when hovering over storage bins, lockers, and other storage buildings.

**[Available on Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3637756953)**

## Features

- Shows item names and quantities when hovering over any storage building
- Displays total mass stored vs capacity
- Groups identical items together (e.g., "Iron Ore x5 (127.3 kg)")
- Limits display to 10 item types (shows "... and X more types" if needed)
- Works with all storage buildings (Storage Bin, Smart Storage Bin, Refrigerator, etc.)

## Building the Mod

### Prerequisites

1. Oxygen Not Included installed on your system
2. .NET Framework 4.7.2 or higher
3. MSBuild (comes with Visual Studio or .NET SDK)

### Build Steps

1. Set the ONI_INSTALL_DIR environment variable to your ONI installation path:

   **Linux:**
   ```bash
   export ONI_INSTALL_DIR="$HOME/.local/share/Steam/steamapps/common/OxygenNotIncluded"
   ```

   **Windows:**
   ```cmd
   set ONI_INSTALL_DIR=C:\Program Files (x86)\Steam\steamapps\common\OxygenNotIncluded
   ```

2. Build the project:

   **Linux:**
   ```bash
   msbuild StorageTooltipMod.csproj /p:Configuration=Release
   ```

   **Windows:**
   ```cmd
   msbuild StorageTooltipMod.csproj /p:Configuration=Release
   ```

3. The compiled DLL will be in `bin/Release/StorageTooltipMod.dll`

## Installation

1. Locate your ONI mods folder:
   - **Windows:** `%USERPROFILE%\Documents\Klei\OxygenNotIncluded\mods`
   - **Linux:** `~/.config/unity3d/Klei/Oxygen Not Included/mods`

2. Create a folder for this mod:
   ```
   mods/StorageTooltipMod/
   ```

3. Copy both files to the mod folder:
   ```
   mods/StorageTooltipMod/StorageTooltipMod.dll
   mods/StorageTooltipMod/mod.yaml
   ```

   **Quick install script:**
   ```bash
   mkdir -p ~/.config/unity3d/Klei/Oxygen\ Not\ Included/mods/StorageTooltipMod
   cp bin/Release/net472/StorageTooltipMod.dll ~/.config/unity3d/Klei/Oxygen\ Not\ Included/mods/StorageTooltipMod/
   cp mod.yaml ~/.config/unity3d/Klei/Oxygen\ Not\ Included/mods/StorageTooltipMod/
   ```

4. Launch Oxygen Not Included

5. The mod should automatically load. Check the console log for:
   ```
   StorageTooltipMod: Initializing...
   StorageTooltipMod: Loaded successfully!
   ```

## Usage

Simply hover your mouse over any storage building (Storage Bin, Locker, Refrigerator, etc.) and you'll see a new section in the tooltip showing:

- Item names with quantities
- Mass per item type
- Total stored mass vs capacity

## Troubleshooting

### Mod doesn't load

1. Check that you have the correct mod folder structure
2. Verify the DLL is in the right location
3. Check the game's output log for errors:
   - **Windows:** `%USERPROFILE%\AppData\LocalLow\Klei\Oxygen Not Included\output_log.txt`
   - **Linux:** `~/.config/unity3d/Klei/Oxygen Not Included/Player.log`

### Build errors

- Make sure `ONI_INSTALL_DIR` environment variable is set correctly
- Verify all DLL references exist in your ONI installation
- Check that you have .NET Framework 4.7.2 or higher installed

## Development

The mod uses Harmony to patch `SelectToolHoverTextCard.UpdateHoverElements()` with a postfix patch that:

1. Checks each hovered object for a Storage component
2. Gets the list of stored items
3. Groups identical items and sums their masses
4. Renders the information using ONI's HoverTextDrawer

## License

This mod is provided as-is for educational and entertainment purposes.

## Credits

Created with assistance from Claude Code
