# How the importer works

The import is split into a few ordinary steps:

1. `ScriptedImporter` receives the `.bbmodel`.
2. The parser reads its JSON into small internal model classes.
3. `CoordinateConverter` handles the Blockbench-to-Unity axis conversion.
4. Builders create the hierarchy, meshes, materials, skinning, and animation.
5. Unity stores the results as sub-assets of the imported model.

Parsing and Unity object creation are kept apart on purpose. A format change should not require mesh-building code to understand raw JSON, and coordinate conversion should not be reimplemented in several places.

## Asset safety

The file is data, not code:

- Molang and plugin expressions are not evaluated.
- Blockbench plugins and managed assemblies are not loaded.
- The source `.bbmodel` is never rewritten.
- Embedded textures are decoded in memory.
- Missing external textures produce warnings.
- Unknown elements are kept as named transforms when that is useful.

Please do not work around one model by weakening those rules.

## Adding format support

When adding a field or element type:

1. Find out exactly how Blockbench stores and evaluates it.
2. Add parser coverage for valid, missing, and malformed data.
3. Put the Unity conversion in the appropriate builder.
4. Add a geometry or import test.
5. Update [format support](format-support.md) and the [changelog](../CHANGELOG.md).

The rest of the development setup is in [CONTRIBUTING.md](https://github.com/martincalander/com.martincalander.blockbenchimporter/blob/main/.github/CONTRIBUTING.md).
