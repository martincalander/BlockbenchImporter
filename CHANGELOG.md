# Changelog

Changes are listed here by release. Versions follow [Semantic Versioning](https://semver.org/) where practical.

## [Unreleased]

### Changed

- Renamed the package ID to `com.martincalander.blockbenchimporter` and public namespaces and assembly names to `MartinCalander.BlockbenchImporter`.
- Replaced custom README artwork with the standard Blockbench and Unity logos.
- Replaced the root package icon with Blockbench's standard application icon.
- Rewrote the public and UPM documentation in a shorter, more direct maintainer voice.
- Moved GitHub community files under `.github/` and package guidance under `Documentation~/` so Unity ignores them.
- Moved governance, maintainer, and release-process documents under `.github/`.
- Renamed **Units Per Pixel** to **Import Scale** and baked the conversion into generated spatial data so prefab roots keep unit scale.
- Reduced import allocations when applying Bezier tangents, checking texture transparency, and assigning armature weights to vertices shared by multiple faces.
- Reused mesh-building buffers and cached rotation keyframe values to reduce repeated allocations and parsing during import.
- Removed unused private helpers and repeated camera lookups while preserving public APIs.
- Excluded repository automation files from npm package archives.

### Added

- Added `BlockbenchImporter.OnPostBlockbenchImport` for deterministic scripted customization of generated roots and sub-assets during import.
- Added a Package Manager sample demonstrating opt-in post-import component, material, and sub-asset customization.
- Added coverage for two consecutive Play sessions with domain reload disabled, including billboard camera changes and stable model reimports, and documented the package's lifecycle behavior.

### Fixed

- Stopped empty and unsupported animations from leaving unregistered animation clips in memory.
- Corrected mixed-axis rotations, polygon front faces, nested armature pivots, rotated cube rescaling, and mirrored billboard textures.
- Preserved animation duration, split keyframe values, stepped rotations, independent Bezier handles, Catmull-Rom overshoot, and global rotations under animated parents.
- Corrected animation axis conversion for older projects and defaults for omitted scale axes, texture UV dimensions, and default face textures.
- Updated external textures now take precedence over embedded snapshots. Adding, changing, or deleting a referenced texture inside the Unity project triggers a model reimport.
- Detect transparency in every texture pixel and preserve marker colors, alpha cutout, and face culling with dedicated Built-in render pipeline shaders.

## [1.0.0] - 2026-07-12

### Added

- Blockbench project format 5 import through Unity's Scripted Importer.
- Shared-space pivot hierarchy construction, cube inflate/stretch, face enablement, face UV rotation, and concave polygon triangulation.
- Embedded and external texture import, unlit fidelity materials, alpha controls, per-face assignments, and double-sided handling.
- Weighted armature skinning, camera-facing billboards, marker-color materials, locator/null preservation, and optional bounding-box colliders.
- Position, rotation, and scale animation clips with linear, stepped, Catmull-Rom, and Bezier interpolation plus optional Animator Controller generation.
- Parser, geometry, and full importer EditMode regression tests.
- UPM manual, contribution guide, support notes, and security reporting instructions.
- GitHub issue forms, pull-request template, package checks, Dependabot, and tagged release archives with checksums.

### Changed

- Renamed public namespaces and the assembly root namespace to `MartinCalander.BlockbenchImporter`.
- Replaced the former proprietary terms with the Apache License 2.0 and a retained attribution notice for Martin Calander.
- Unknown or plugin-defined element types now remain visible as transforms and produce contextual warnings instead of being silently discarded.

### Fixed

- Corrected coordinate handedness across transforms, vertices, normals, winding, pivots, skinning, and animation values.
- Corrected stable hierarchy naming and animation bindings for duplicate source names.
- Suppressed Unity serialization analyzer warnings for manually parsed Blockbench dictionary data.

## [0.1.0] - 2026-01-16

### Added

- Initial `.bbmodel` Scripted Importer with hierarchy, cube and mesh elements, embedded textures, materials, and basic animation generation.

[Unreleased]: https://github.com/martincalander/BlockbenchImporter/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/martincalander/BlockbenchImporter/releases/tag/v1.0.0
[0.1.0]: https://github.com/martincalander/BlockbenchImporter/releases/tag/v0.1.0
