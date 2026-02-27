#!/bin/bash
# Quick deploy script - copies the built mod to the game folder

SRC_DIR="$(cd "$(dirname "$0")" && pwd)"
BUILD_DIR="$SRC_DIR/bin/Release/net472"
GAME_DIR="$HOME/.config/unity3d/Klei/Oxygen Not Included/mods/Dev/AutoTeleportMod"

if [ ! -f "$BUILD_DIR/AutoTeleportMod.dll" ]; then
    echo "Build not found. Run: dotnet build AutoTeleportMod.csproj -c Release"
    exit 1
fi

mkdir -p "$GAME_DIR"
cp "$BUILD_DIR/AutoTeleportMod.dll" "$GAME_DIR/"
cp "$SRC_DIR/mod.yaml" "$GAME_DIR/"
cp "$SRC_DIR/mod_info.yaml" "$GAME_DIR/"
echo "Deployed to $GAME_DIR"
ls -lh "$GAME_DIR/"
