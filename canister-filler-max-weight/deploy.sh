#!/bin/bash
# Quick deploy script - copies the built mod to the game folder

SRC_DIR="$HOME/Git/ONI-Mods/canister-filler-max-weight"
BUILD_DIR="$SRC_DIR/bin/Release/net472"
GAME_DIR="$HOME/.config/unity3d/Klei/Oxygen Not Included/mods/Dev/CanisterFillerMaxWeight"

if [ ! -f "$BUILD_DIR/CanisterFillerMaxWeight.dll" ]; then
    echo "Build not found. Run: dotnet build CanisterFillerMaxWeight.csproj -c Release"
    exit 1
fi

mkdir -p "$GAME_DIR"
cp "$BUILD_DIR/CanisterFillerMaxWeight.dll" "$GAME_DIR/"
cp "$SRC_DIR/mod.yaml" "$GAME_DIR/"
cp "$SRC_DIR/mod_info.yaml" "$GAME_DIR/"
echo "Deployed to $GAME_DIR"
ls -lh "$GAME_DIR/"
