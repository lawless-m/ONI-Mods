# ONI Mods Collection

A monorepo containing various mods for Oxygen Not Included.

## Mods

### [Storage Tooltip](storage-tooltip/)
Enhanced storage container tooltips showing current contents with icons, item counts, and mass information.

**Features:**
- Shows item names and quantities when hovering over storage buildings
- Displays total mass stored vs capacity
- Groups identical items together
- Works with all storage buildings

### [Magic Storage](magic-storage/) (WIP)
Storage containers with infinite capacity that duplicate items when stored.

**Features:**
- Infinite storage capacity for all storage buildings
- Automatically duplicates items placed in storage
- Works with storage bins, refrigerators, ration boxes, and reservoirs
- Creative mode style unlimited resources

## General Build Instructions

All mods in this repository follow a similar build process:

### Prerequisites

1. Oxygen Not Included installed on your system
2. .NET Framework 4.7.2 or higher
3. MSBuild (comes with Visual Studio or .NET SDK)

### Environment Setup

Set the `ONI_INSTALL_DIR` environment variable to your ONI installation path:

**Linux:**
```bash
export ONI_INSTALL_DIR="$HOME/.local/share/Steam/steamapps/common/OxygenNotIncluded"
```

**Windows:**
```cmd
set ONI_INSTALL_DIR=C:\Program Files (x86)\Steam\steamapps\common\OxygenNotIncluded
```

### Building a Mod

Navigate to the specific mod folder and build:

```bash
cd <mod-folder>
msbuild *.csproj /p:Configuration=Release
```

The compiled DLL will be in `bin/Release/`.

## Installation

1. Locate your ONI mods folder:
   - **Windows:** `%USERPROFILE%\Documents\Klei\OxygenNotIncluded\mods`
   - **Linux:** `~/.config/unity3d/Klei/Oxygen Not Included/mods`

2. Each mod has specific installation instructions in its own README

## Repository Structure

```
ONI-Mods/
├── README.md                 (this file)
├── LICENSE
├── storage-tooltip/          (Storage Tooltip Mod)
│   ├── README.md
│   ├── StorageTooltipMod.cs
│   └── ...
├── magic-storage/            (Magic Storage Mod - WIP)
│   ├── InfiniteStorage.cs
│   ├── InfiniteStorage.csproj
│   └── ...
└── <future-mod>/            (more mods to come)
```

## Contributing

Each mod is self-contained in its own subfolder with its own build configuration and documentation.

## License

See LICENSE file for details.
