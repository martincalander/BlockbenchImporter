<div align="center">
  <img src="Documentation~/Images/logo-mark.svg" alt="Unity Blockbench Importer logo" width="128">
  <h1>Unity Blockbench Importer</h1>
  <p>Drop a <code>.bbmodel</code> into Unity and use it like any other asset.</p>

[![Validation](https://github.com/martincalander/UnityBlockbenchImporter/actions/workflows/validate.yml/badge.svg)](https://github.com/martincalander/UnityBlockbenchImporter/actions/workflows/validate.yml)
[![Unity 2021.3+](https://img.shields.io/badge/Unity-2021.3%2B-222?logo=unity)](https://unity.com/releases/editor/whats-new/2021.3.0)
[![Apache-2.0](https://img.shields.io/badge/license-Apache--2.0-6f8df4)](LICENSE.md)
</div>

The importer reads the Blockbench project directly. There is no separate export step: edit the model in Blockbench, save it, and Unity reimports it.

The result is one prefab-like asset with its hierarchy, meshes, textures, materials, animation clips, and optional Animator Controller stored as sub-assets.

## What comes across

- Groups, pivots, cubes, polygon meshes, locators, nulls, and bounding boxes
- Face UVs, UV rotation, per-face textures, inflate, stretch, and element rotation
- Embedded PNGs and external textures
- Unlit or lit materials, alpha cutout, and double-sided faces
- Bones, weights, bind poses, and skinned meshes
- Position, rotation, and scale animation
- Camera-facing billboards

The importer handles the coordinate-system conversion in one place, including pivots, normals, winding, skinning, and animation values. If something is unsupported, it should produce a warning instead of quietly disappearing.

The full list is in [format support](Documentation~/format-support.md).

## Install

In Unity, open **Window → Package Manager**, choose **+ → Add package from git URL…**, and paste:

```text
https://github.com/martincalander/UnityBlockbenchImporter.git
```

Or add it to `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.essentials.blockbenchimporter": "https://github.com/martincalander/UnityBlockbenchImporter.git"
  }
}
```

Unity `2021.3` or newer is required.

There is no published version tag yet, so this currently follows `main`. Once releases are tagged, append `#vX.Y.Z` to pin one.

## Use it

1. Save the editable project as `.bbmodel`.
2. Drag it into `Assets/`.
3. Drag the imported asset into a scene.

Select the `.bbmodel` in the Project window to change its import settings. Press **Apply** when you are done. The source file is never rewritten, and embedded data stays inside the imported asset.

Projects that need custom generated components, material changes, or additional sub-assets can subscribe to `BlockbenchImporter.OnPostBlockbenchImport` from an `AssetPostprocessor`. See [scripted post-processing](Documentation~/importing-models.md#scripted-post-processing).

The Package Manager includes a **Post-Import Customization** sample with a working model and documented callback implementation.

The useful settings are:

- **Import Scale** — Unity units per Blockbench model unit; the default `0.0625` matches Blockbench's 16-units-per-meter export convention
- **Include Hidden / Non-Exported Elements** — bring in authoring-only content
- **Material Mode** — Unlit for Blockbench-like color, or Lit for scene lighting
- **Texture Filter / Wrap** — Point and Clamp by default
- **Alpha Mode** — detect alpha, force opaque, or force cutout
- **Import Animations** — create clips
- **Create Animator Controller** — add a controller with one state per clip

See [importing models](Documentation~/importing-models.md) for the details.

## A few honest limits

Blockbench plugins can add their own element types and expressions. This importer does not run plugin code or Molang from an asset file. Unknown elements are kept as transforms where possible and reported in the Console.

Texture meshes, splines, PBR texture packing, and animated texture playback still need dedicated implementations. Custom shaders and render-pipeline lighting can also make the Unity result look different from the Blockbench viewport.

If a supported feature imports incorrectly, that is a bug. Please [report it](https://github.com/martincalander/UnityBlockbenchImporter/issues/new/choose) with the smallest model that shows the problem.

## Docs

- [Manual](Documentation~/index.md)
- [Installation](Documentation~/installation.md)
- [Import settings](Documentation~/importing-models.md)
- [Format support](Documentation~/format-support.md)
- [Troubleshooting](Documentation~/troubleshooting.md)
- [How the importer is put together](Documentation~/architecture.md)
- [Roadmap](Documentation~/roadmap.md)

## Contributing

Bug fixes, tests, docs, and new format support are welcome. Start with [CONTRIBUTING.md](https://github.com/martincalander/UnityBlockbenchImporter/blob/main/.github/CONTRIBUTING.md). Import changes should come with a small regression test or a model that is safe to redistribute.

## License

[Apache 2.0](LICENSE.md), with attribution to Martin Calander in [NOTICE.md](NOTICE.md).

Unity and Blockbench belong to their respective owners. This is an independent project and is not endorsed by either of them.
