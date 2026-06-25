# Changelog

All notable changes to the Blockbench Importer package will be documented in this file.

## [Unreleased]

### Changed
- Renamed public namespaces and the asmdef root namespace to `Essentials.BlockbenchImporter`.

### Fixed
- Suppressed Unity serialization analyzer warnings for manually parsed Blockbench dictionary data.

## [0.1.0] - 2026-01-16

### Added
- Initial implementation of Blockbench .bbmodel importer
- **Hierarchy Support**: Groups and elements with correct pivot points and transforms
- **Cube Elements**: Full support for cube primitives with per-face UV and texture mapping
- **Mesh Elements**: Support for arbitrary mesh elements with polygon triangulation
- **Texture Import**: Embedded base64 PNG textures automatically decoded and imported
- **Multiple Textures**: Per-face texture assignment with proper submesh/material slot handling
- **Material Generation**: Auto-detects render pipeline (Built-in, URP, HDRP) and configures materials appropriately
- **Alpha Clip**: Automatic alpha cutout mode to prevent see-through/transparency artifacts
- **Pixel Art Mode**: Point filtering for pixel art style models
- **Animation Import**: Blockbench animations converted to Unity AnimationClips
- **Mecanim Support**: Automatic AnimatorController generation with states for each animation
- **Quaternion Curves**: Rotation animations use quaternion interpolation to avoid gimbal lock
- **Coordinate Conversion**: Proper Blockbench to Unity coordinate system transformation (Z-axis negation)
- **Custom Editor**: Inspector UI with grouped settings for scale, textures, and animations

### Technical Details
- ScriptedImporter implementation for seamless Unity integration
- Tolerant JSON parser handles various Blockbench format versions
- Support for both array `[w,h]` and object `{width, height}` resolution formats
- Mesh generation with 24 vertices per cube (4 per face) for proper normals and UVs
- UV coordinate conversion handles Blockbench top-left origin to Unity bottom-left
- Per-face UV rotation support (0°, 90°, 180°, 270°)
- Ear-clipping triangulation for mesh elements with n-gon faces

### Import Settings
- `Scale`: Global scale multiplier
- `Units Mode`: How to interpret Blockbench units (16 pixels per unit, 1 pixel per unit, custom)
- `Import Textures`: Enable/disable embedded texture import
- `Pixel Art Mode`: Use point filtering for textures
- `Force Alpha Clip`: Enable cutout mode on materials
- `Alpha Cutoff`: Threshold for alpha clipping
- `Import Animations`: Enable/disable animation import
- `Create Animator Controller`: Generate Mecanim controller

### Known Limitations
- Mesh element support uses simple fan/ear-clip triangulation; complex concave polygons may not triangulate perfectly
- Bezier interpolation in animations is approximated as linear
- Expression-based keyframe values are not evaluated (treated as 0)
