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

## Package audit verification on 2026-10-20

The local working tree based on `ec6675c` was audited across all 107 tracked
paths: Editor and runtime code, shaders, tests, sample assets, metadata,
documentation, and packaging workflows. The changes tighten malformed-input
handling, release objects after failed imports, reduce mesh index memory and
temporary allocations, and share rest-pose preparation across animation clips.
The importer revision is 22, so Unity reimports existing models.

Verification used Unity CLI `1.0.0-beta.13` on macOS ARM64:

| Editor | Importer tests passed | Skipped | Failed |
| --- | ---: | ---: | ---: |
| `7000.0.0a11` | 136 | 0 | 0 |
| `6000.7.0b2` | 134 | 2 | 0 |
| `6000.5.5f1` | 134 | 2 | 0 |

All three Editors compiled the package without C# warnings or errors. The
Unity 7 run used Pipeline `0.8.0-exp.1`; the isolated Unity 6 projects used
one-shot CLI tests. All used Test Framework `1.9.0`. Every Editor launch used
`-automated`. The final runs had a graphics device and exercised both bundled
shaders. The two Unity 6 skips are the URP material cases because those minimal
projects do not install URP; both cases passed in the Unity 7 workspace.

Coverage includes malformed and truncated JSON, escaped Unicode, numeric
limits, depth limits, cyclic and repeated hierarchy references, failure cleanup
for textures/materials/clips/controllers, small and large mesh index formats,
large degenerate polygons, UV edge cases, stable reimports, and Play mode with
domain reload disabled. The initial headless run skipped rendering assertions;
the final graphics-enabled run covered them successfully.

Package validation, changed-document Markdown lint, metadata checks, GitHub
Actions syntax checks, package dry run, and whitespace checks passed. Existing
GUIDs, runtime code, shaders, dependencies, and public helper APIs remain intact.

Not tested: Unity `2021.3` or other Editor versions beyond the three listed,
Windows, Linux, player builds, signed release artifacts, and hosted CI. These
are local working-tree checks, not release evidence for another commit,
platform, Editor version, or package archive.

## Test suite cleanup verification on 2026-10-20

Test source decreased from 2,285 to 2,178 lines, and the suite from 136 to
132 cases. Four removed cases duplicated null-face texture, coordinate,
discarded-face, and reimport-ID coverage retained elsewhere. Texture precedence
and clip cleanup share parameterized setup; the global-rotation test now uses
the animation fixture's helpers. Explicit model setup avoids five unnecessary
imports per suite. Product source, test files, and metadata were retained.

The focused integration checks and final full suites passed on macOS ARM64:
132 cases on `7000.0.0a11`, and 130 on each of `6000.5.5f1` and `6000.7.0b2`.
Each Unity 6 run skipped the two URP cases because its project lacks URP;
both passed on Unity 7. All three used CLI `1.0.0-beta.13`, Test Framework
`1.9.0`, and `-automated`, with no C# warnings or errors. Package validation,
Markdown lint, package dry run, and whitespace checks passed.

Other Editor versions, Windows, Linux, player builds, and release or hosted CI
were not rerun. Cleanup evidence is under `Logs/test-cleanup/` in the workspace.
