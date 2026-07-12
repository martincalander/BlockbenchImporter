# Contributing

Thanks for taking a look. Bug fixes, tests, docs, and new Blockbench format support are all useful.

For a small fix, feel free to open a pull request. If the change affects a large part of the file format or changes the generated hierarchy, open an issue first so we can agree on what the Unity result should be.

Please follow the [Code of Conduct](CODE_OF_CONDUCT.md), and use [SECURITY.md](SECURITY.md) instead of a public issue for vulnerabilities.

## Set up the package

1. Fork and clone this repository.
2. Put the clone under a Unity project's `Packages/` folder, or add it as an embedded package.
3. Open the project in Unity `2021.3` or newer.
4. Run the EditMode tests from **Window → General → Test Runner**.
5. Import a `.bbmodel` and compare the Unity result with Blockbench.

There are no required third-party runtime dependencies.

## Importer changes

A visual fix often touches more than the visible mesh. Check the parts that apply:

- coordinate space, pivots, and parent transforms;
- winding, normals, UV origin, and UV rotation;
- hidden/export flags and per-face materials;
- animation paths, interpolation, and loop mode;
- old, missing, malformed, and plugin-defined data;
- stable names and sub-asset IDs after reimporting.

Never run expressions or plugin code from an imported asset. When a feature is unsupported, keep whatever useful structure you can and emit a clear warning.

## Tests and fixtures

Add the smallest test that proves the change:

- parser tests for JSON fields and edge cases;
- geometry tests for coordinates, winding, pivots, UVs, and triangulation;
- import tests for hierarchy, components, and sub-assets.

If a `.bbmodel` fixture is necessary, strip it down to the problem and make sure you are allowed to redistribute it. Do not commit someone's production art.

## Before opening a pull request

- Run the relevant EditMode tests.
- Make sure Unity compiles without new warnings.
- Reimport an affected model twice and check that the result is stable.
- Update [format support](../Documentation~/format-support.md) and the [changelog](../CHANGELOG.md) when behavior changes.
- Run `python3 .github/scripts/validate_package.py`.

Keep the pull request focused and explain how Blockbench stores the data you changed. A commit subject like `Fix rotated north-face UVs` is more helpful than `Update importer`.

Contributions are submitted under the [Apache License 2.0](../LICENSE.md).
