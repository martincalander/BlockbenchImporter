# Blockbench Importer for Unity

Import Blockbench `.bbmodel` files directly into Unity with full support for hierarchy, textures, and animations.

## Features

- **Direct Import**: Drop `.bbmodel` files into your Unity project
- **Full Hierarchy**: Groups and elements maintain correct pivot points and parent-child relationships
- **Cube & Mesh Elements**: Support for both cube primitives and arbitrary mesh geometry
- **Multiple Textures**: Per-face texture assignment with automatic submesh/material generation
- **Embedded Textures**: Base64-encoded textures are automatically extracted
- **Animation Support**: Blockbench animations converted to Unity AnimationClips with Mecanim support
- **Render Pipeline Agnostic**: Automatically detects and configures materials for Built-in, URP, or HDRP

## Installation

### Via Package Manager (Git URL)

1. Open Unity Package Manager (Window > Package Manager)
2. Click the `+` button and select "Add package from git URL..."
3. Enter the repository URL

### Via Local Folder

1. Clone or download this repository
2. Copy to your project's `Packages/` folder

## Usage

1. Export your model from Blockbench as `.bbmodel` (File > Save Project As...)
2. Drag the `.bbmodel` file into your Unity project
3. The model will be automatically imported as a prefab

### Import Settings

Select the `.bbmodel` file in the Project window to access import settings:

| Setting | Description |
|---------|-------------|
| **Scale** | Global scale multiplier |
| **Units Mode** | How to interpret Blockbench units |
| **Import Textures** | Import embedded textures |
| **Pixel Art Mode** | Use point filtering (recommended for voxel art) |
| **Force Alpha Clip** | Enable cutout mode to prevent see-through artifacts |
| **Alpha Cutoff** | Threshold for alpha clipping (0.0 - 1.0) |
| **Import Animations** | Import Blockbench animations |
| **Create Animator Controller** | Generate a Mecanim AnimatorController |

## Coordinate System

Blockbench and Unity use compatible Y-up coordinate systems:
- **Blockbench**: X = right, Y = up, Z = depth (front face "north" looks toward -Z)
- **Unity**: X = right, Y = up, Z = forward

The importer matches Blockbench's glTF export behavior:
- Positions: Scaled by 1/16 (16 Blockbench pixels = 1 Unity unit)
- Rotations: Applied directly (same Euler angle convention)
- Scale: Applied to root transform (0.0625 = 1/16)
- Front face ("north"): Faces -Z direction in Unity

## Supported Features

### Elements
- ✅ Cube elements with from/to bounds
- ✅ Cube rotation and origin/pivot
- ✅ Cube inflate property
- ✅ Mesh elements with arbitrary vertices/faces
- ✅ Per-face textures and UVs
- ✅ UV rotation (0°, 90°, 180°, 270°)

### Textures
- ✅ Embedded base64 PNG textures
- ✅ External texture file references
- ✅ Multiple textures per model
- ✅ Per-face texture assignment
- ✅ Point/Bilinear filtering options

### Hierarchy
- ✅ Groups as GameObjects
- ✅ Nested groups
- ✅ Element parenting
- ✅ Pivot/origin handling
- ✅ Group rotation

### Animations
- ✅ Position keyframes
- ✅ Rotation keyframes (quaternion interpolation)
- ✅ Scale keyframes
- ✅ Linear interpolation
- ✅ Step interpolation
- ✅ Loop modes (once, loop, hold)
- ✅ AnimatorController generation

## Troubleshooting

### Model appears inside-out
- Ensure winding order is correct in the mesh builder
- Check that normals are pointing outward

### See-through/transparency artifacts
- Enable "Force Alpha Clip" in import settings
- Increase the Alpha Cutoff value

### Animations don't play
- Ensure "Import Animations" is enabled
- Check that "Create Animator Controller" is enabled
- Verify the Animator component is on the root object

### Textures appear wrong
- Check that textures are embedded in the .bbmodel file
- Verify UV coordinates in Blockbench

## License

MIT License - See LICENSE file for details.

## Credits

Developed by Martin Calander
