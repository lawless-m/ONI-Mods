#!/bin/bash
# Prepare mod for Steam Workshop upload

UPLOAD_DIR="$HOME/ONI_Workshop_Upload/StorageTooltipMod"

echo "Creating upload directory..."
mkdir -p "$UPLOAD_DIR"

echo "Building mod..."
dotnet build -c Release

echo "Copying files to upload directory..."
cp bin/Release/net472/StorageTooltipMod.dll "$UPLOAD_DIR/"
cp mod.yaml "$UPLOAD_DIR/"
cp mod_info.yaml "$UPLOAD_DIR/"
cp preview.png "$UPLOAD_DIR/"

echo ""
echo "Upload directory ready at: $UPLOAD_DIR"
echo ""
echo "Files included:"
ls -lh "$UPLOAD_DIR"
echo ""
echo "Next steps:"
echo "1. Launch Oxygen Not Included"
echo "2. Go to Mods menu"
echo "3. Click 'Upload Mod' or use the workshop upload feature"
echo "4. Select folder: $UPLOAD_DIR"
echo ""
echo "See STEAM_WORKSHOP_UPLOAD.md for detailed instructions"
