# Changelog

All notable changes to Unity Blockbench Importer are documented here. The project follows [Semantic Versioning](https://semver.org/) where practical.

## [Unreleased]

No unreleased changes yet.

## [1.0.0] - 2026-07-12

### Added

- Faithful Blockbench project format 5 import through Unity's Scripted Importer.
- Shared-space pivot hierarchy construction, cube inflate/stretch, face enablement, face UV rotation, and concave polygon triangulation.
- Embedded and external texture import, unlit fidelity materials, alpha controls, per-face assignments, and double-sided handling.
- Weighted armature skinning, camera-facing billboards, marker-color materials, locator/null preservation, and optional bounding-box colliders.
- Position, rotation, and scale animation clips with linear, stepped, Catmull-Rom, and Bezier interpolation plus optional Animator Controller generation.
- Parser, geometry, and full importer EditMode regression tests.
- Complete UPM manual, format-support matrix, troubleshooting, architecture, contribution, support, governance, security, and release documentation.
- Original project identity, public landing graphics, issue forms, pull-request template, CODEOWNERS, and citation metadata.
- Package validation, dependency review, CodeQL analysis, Dependabot, and checksum-producing release automation.

### Changed

- Renamed public namespaces and the assembly root namespace to `Essentials.BlockbenchImporter`.
- Replaced the former proprietary terms with the Apache License 2.0 and a retained attribution notice for Martin Calander.
- Unknown or plugin-defined element types now remain visible as transforms and produce contextual warnings instead of being silently discarded.

### Fixed

- Corrected coordinate handedness across transforms, vertices, normals, winding, pivots, skinning, and animation values.
- Corrected stable hierarchy naming and animation bindings for duplicate source names.
- Suppressed Unity serialization analyzer warnings for manually parsed Blockbench dictionary data.

## [0.1.0] - 2026-01-16

### Added

- Initial `.bbmodel` Scripted Importer with hierarchy, cube and mesh elements, embedded textures, materials, and basic animation generation.

[Unreleased]: https://github.com/martincalander/UnityBlockbenchImporter/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/martincalander/UnityBlockbenchImporter/releases/tag/v1.0.0
[0.1.0]: https://github.com/martincalander/UnityBlockbenchImporter/releases/tag/v0.1.0
