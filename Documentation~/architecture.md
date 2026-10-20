# How the importer works

The import is split into a few ordinary steps:

1. `ScriptedImporter` receives the `.bbmodel`.
2. The parser reads its JSON into small internal model classes.
3. `CoordinateConverter` handles the Blockbench-to-Unity axis conversion.
4. Builders create the hierarchy, meshes, materials, skinning, and animation.
5. Unity stores the results as sub-assets of the imported model.

Parsing and Unity object creation are kept apart on purpose. A format change should not require mesh-building code to understand raw JSON, and coordinate conversion should not be reimplemented in several places.

## Import work and memory

Position and scale curves retain separate incoming and outgoing tangents. Rotation curves are sampled with extra keys around discontinuities, and global rotations account for animated ancestors. The parser converts the older animation axes described in Blockbench's [format history](https://www.blockbench.net/wiki/docs/bbmodel/).

Rotation tracks parse their incoming and outgoing numeric values once per clip, then locate surrounding keys with a binary search. Rest-pose metadata is collected once for the complete clip batch. These caches are rebuilt on each build, so edits to the source model are visible when a builder is reused.

Mesh builders reuse temporary UV and triangulation buffers within each mesh. Cube corner lookups are fixed data; public triangulation results remain independent lists. Meshes with at most 65,535 vertices use 16-bit indices. Larger polygon and merged meshes use 32-bit indices. Merging passes the existing lists directly to Unity and calculates bounds once.

Transparency checks scan every pixel and access readable RGBA32 data directly. Armature weights are calculated once for each source vertex in each mesh, then reused for copies created at face boundaries. These working caches belong to the current import.

Groups use shared model pivots, while armature bones use parent-local origins. Coordinate conversion mirrors X and composes rotations in Blockbench's ZYX order. Polygon winding and normals are converted together.

Animations only create a Unity clip when a supported transform channel has keyframes. Empty animations and unsupported tracks do not retain unused native clips.

## Asset safety

The file is data, not code:

- Molang and plugin expressions are not evaluated.
- Blockbench plugins and managed assemblies are not loaded.
- The source `.bbmodel` is never rewritten.
- Embedded textures are decoded in memory.
- JSON syntax and string escapes are validated before model conversion; JSON nesting and generated hierarchy depth are limited to 128 levels.
- Cyclic child references fail the import. Repeated element or group references keep their first occurrence and produce a warning.
- Geometry values must fit a finite float. Non-finite animation values produce the same warning and zero fallback as expressions.
- Failed imports release generated objects that have not been registered with Unity's import context.
- External images take precedence over embedded snapshots, with embedded fallback when the external file is missing.
- Missing texture data produces a warning. Project texture dependencies are registered even before the referenced file exists.
- Unknown elements are kept as named transforms when that is useful.

Please do not work around one model by weakening those rules.

## Play mode lifecycle

The importer has no mutable static session state or global event subscriptions. Its static face names, cube corner indices, and marker colors are fixed lookup data. Parsing, texture and material caches, hierarchy maps, and animation builders belong to each import.

`OnPostBlockbenchImport` belongs to the importer instance. Register it idempotently from `OnPreprocessAsset`, as shown in the user guide, because Unity can reuse importer instances. Custom callbacks should avoid capturing scene objects or keeping global session state.

`BlockbenchBillboard` has no cached camera or session state. It reads `Camera.main` during Play mode and `Camera.current` for Edit mode rendering. There is no package state that requires a Play mode reset callback, so disabling domain reload does not require an additional lifecycle hook.

The `PlayModeLifecycle` EditMode test performs two Play sessions with domain reload disabled. It checks post-import callback counts, stable generated asset identifiers, and billboard orientation against a new camera in each session.

## Adding format support

When adding a field or element type:

1. Find out exactly how Blockbench stores and evaluates it.
2. Add parser coverage for valid, missing, and malformed data.
3. Put the Unity conversion in the appropriate builder.
4. Add a geometry or import test.
5. Update [format support](format-support.md) and the [changelog](../CHANGELOG.md).

The rest of the development setup is in [CONTRIBUTING.md](https://github.com/martincalander/BlockbenchImporter/blob/main/.github/CONTRIBUTING.md).
