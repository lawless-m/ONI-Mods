# How to Upload to Steam Workshop

## Method 1: Using ONI's Built-in Uploader (Recommended)

1. **Prepare the mod folder** - Create a clean upload folder:
   ```bash
   mkdir -p ~/ONI_Workshop_Upload/StorageTooltipMod
   cp bin/Release/net472/StorageTooltipMod.dll ~/ONI_Workshop_Upload/StorageTooltipMod/
   cp mod.yaml ~/ONI_Workshop_Upload/StorageTooltipMod/
   cp mod_info.yaml ~/ONI_Workshop_Upload/StorageTooltipMod/
   cp preview.png ~/ONI_Workshop_Upload/StorageTooltipMod/
   ```

2. **Update mod_info.yaml** with proper metadata:
   - Make sure version is set correctly
   - supportedContent should list DLCs you support

3. **Launch Oxygen Not Included**

4. **Go to Mods menu** from the main menu

5. **Click "Upload Mod"** button (or similar - look for Workshop/Upload option)

6. **Select your mod folder**: `~/ONI_Workshop_Upload/StorageTooltipMod`

7. **Fill in details**:
   - Title: "Storage Tooltip Mod"
   - Description: Copy from steam_description.txt
   - Preview Image: preview.png
   - Tags: Quality of Life, UI, Information
   - Visibility: Public

8. **Click Upload/Publish**

## Method 2: Using SteamCMD (Advanced)

If ONI doesn't have a built-in uploader, you'll need to use SteamCMD:

1. Install steamcmd:
   ```bash
   sudo apt-get install steamcmd
   ```

2. Create a workshop item VDF file (workshop_item.vdf)

3. Upload using steamcmd commands

## Important Notes

- **First time publishing**: The mod will need to go through Steam's review (usually quick)
- **Updates**: Use the same process but the mod will update existing workshop item
- **Preview Image**: Make sure preview.png is eye-catching! Consider taking a screenshot of the tooltip in action
- **Test First**: Make sure the mod works perfectly before publishing

## Better Preview Image (Optional)

The current preview.png is very basic. For a better one:

1. **Take a screenshot** in ONI showing the storage tooltip in action
2. **Crop it** to 512x512 or 256x256
3. **Add text overlay** with the mod name
4. Replace preview.png with your new image

Use GIMP, Krita, or any image editor.
