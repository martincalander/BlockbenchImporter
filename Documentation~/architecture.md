# Architecture and safety model

## Import flow

1. Unity's `ScriptedImporter` receives the `.bbmodel` source.
2. The tolerant JSON parser maps known format fields into internal data models.
3. A coordinate converter consistently translates Blockbench vectors, rotations, normals, and winding into Unity space.
4. Specialized builders generate the hierarchy, meshes, materials, skinning, animation clips, and optional controller.
5. Generated objects are attached to the import context as deterministic sub-assets.
6. Unsupported but structurally safe data emits contextual warnings.

The builders are intentionally separated from parsing. Format compatibility decisions belong in the parser/model layer; Unity object construction belongs in builders; coordinate-system rules remain centralized.

## Safety boundaries

- Input is treated as untrusted data, not code.
- Molang and plugin expressions are never evaluated.
- The importer never rewrites the source model.
- Embedded textures are decoded in memory and stored as imported sub-assets.
- External texture references are read only when a path resolves; missing files produce diagnostics.
- Generated objects remain inside the imported asset rather than scattering files through the project.
- Unknown element types do not invoke reflection or dynamically load plugin code.

Do not weaken these boundaries to make one model import. New executable formats require a separate security design and maintainer approval.

## Extending support

For each new Blockbench field or element type:

1. Document the source semantics and coordinate space.
2. Add parser/model coverage for missing, malformed, and valid data.
3. Implement the smallest dedicated builder or converter change.
4. Add a geometry or integration regression test.
5. Update the [format support matrix](format-support.md) and [changelog](../CHANGELOG.md).

See [CONTRIBUTING.md](../CONTRIBUTING.md) for the complete workflow.
