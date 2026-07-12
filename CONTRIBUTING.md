# Contributing

Thank you for helping make Unity Blockbench Importer more faithful, dependable, and approachable. Contributions of code, tests, documentation, reproducible model fixtures, and careful bug reports are welcome.

By participating, you agree to follow the [Code of Conduct](CODE_OF_CONDUCT.md).

## Before you begin

- Use [GitHub Discussions](https://github.com/martincalander/UnityBlockbenchImporter/discussions) for design questions and usage ideas when available.
- Search [existing issues](https://github.com/martincalander/UnityBlockbenchImporter/issues) before opening a duplicate.
- Report vulnerabilities privately according to [SECURITY.md](SECURITY.md).
- Keep public fixtures free of private, purchased, or otherwise non-redistributable art.

Small fixes can go directly to a pull request. For a broad format change, open an issue first so the Blockbench semantics and Unity representation can be agreed before implementation.

## Development setup

1. Fork and clone the repository.
2. Add the clone as an embedded package in a Unity `2021.3` or newer test project, or clone it beneath that project's `Packages/` directory.
3. Open the project and allow Unity to compile.
4. Run **Window → General → Test Runner → EditMode**.
5. Import a representative `.bbmodel`, inspect its hierarchy and sub-assets, then compare it against Blockbench.

The package has no required third-party runtime dependency.

## Fidelity rules

Changes to the importer must account for the whole translation, not only the visible mesh. Depending on the feature, that includes:

- source coordinate space, handedness, pivots, and parent-local conversion;
- vertex order, normals, tangents, face direction, UV origin, and texture rotation;
- hidden/export flags, element types, per-face material assignment, and alpha behavior;
- animation channel semantics, interpolation, loop mode, and stable binding paths;
- malformed, absent, legacy, and plugin-defined data;
- deterministic naming and sub-asset generation across reimports.

Never execute expressions or plugin code embedded in an asset. Unsupported data should be preserved structurally when safe and produce an actionable warning.

## Tests

Every behavior change should include the narrowest regression test that would have failed before the change.

- Parser tests cover accepted forms, missing fields, and malformed input.
- Geometry tests cover coordinates, winding, pivots, UVs, and triangulation.
- Integration tests import a complete asset and inspect generated hierarchy/sub-assets.
- Documentation-only changes run repository validation but do not require a new Unity test.

When a model fixture is necessary, minimize it in Blockbench and state its license in the fixture directory. Do not commit the user's production artwork as a test.

## Pull requests

Before requesting review:

1. Run all EditMode tests in the oldest supported Unity line when practical.
2. Confirm the package compiles without new warnings.
3. Reimport any affected model twice and confirm deterministic output.
4. Update the support matrix and changelog when behavior changes.
5. Run `python3 .github/scripts/validate_package.py` from the repository root.
6. Complete the pull-request checklist and explain the Blockbench-to-Unity semantics.

Keep a pull request focused. Avoid unrelated formatting or mass-renaming, and do not commit generated `Library/`, IDE, build, or test-result files.

## Commit and review expectations

Use clear imperative commit subjects such as `Fix rotated north-face UVs`. Maintainers may request a smaller fixture, additional Unity versions, or primary Blockbench format evidence before accepting a translation-layer change.

Contributions are licensed under the [Apache License 2.0](LICENSE.md). Unless you explicitly state otherwise, submitting a contribution means you have the right to do so and agree to the license's contribution terms.
