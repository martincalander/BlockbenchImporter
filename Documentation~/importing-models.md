# Importing a model

Save the editable Blockbench project as `.bbmodel`, then drag it anywhere under `Assets/`. Unity creates one main asset with the generated pieces tucked underneath it as sub-assets.

Drag the main asset into a scene to use it.

## Before importing

Embedded textures are easiest to move between machines. External textures work too, but their saved paths still need to resolve after the model enters your Unity project.

Clear, unique outliner names help. Duplicate names are made safe for Unity animation paths, but they are still harder to debug.

## Import settings

Select the `.bbmodel` in the Project window. Change a setting, then press **Apply**.

### Geometry

- **Import Scale** sets the number of Unity units generated for each Blockbench model unit. The default `0.0625` means sixteen Blockbench units equal one Unity unit, matching Blockbench's default 3D export convention. The scale is baked into generated transforms, meshes, colliders, skinning, and position animation while the prefab root remains at `(1, 1, 1)`.
- **Include Hidden Elements** includes things hidden in the Blockbench editor.
- **Include Non-Exported Elements** includes things with export turned off.
- **Generate Colliders** turns supported bounding boxes into `BoxCollider` components.

### Materials and textures

- **Unlit** is the closest starting point to Blockbench's viewport.
- **Lit** lets the model react to Unity lighting.
- **Point** filtering keeps hard pixel edges. **Bilinear** smooths them.
- **Clamp** wrapping helps stop texture-atlas edges from bleeding.
- **Alpha Mode** can detect transparency or force opaque/cutout rendering.

A custom Blockbench shader, Unity post-processing, tone mapping, or render-pipeline setting can still change the final look.

### Animation

- **Import Animations** creates one `AnimationClip` for each supported animation.
- **Create Animator Controller** adds an Animator and a controller with one state per clip.

Numeric position, rotation, and scale tracks support linear, stepped, Catmull-Rom, and Bezier interpolation. Molang expressions are not run; see [format support](format-support.md).

## Source control

Commit the `.bbmodel`, its `.meta` file, and every external texture it needs. Pin the importer to a release tag in `manifest.json` so everyone gets the same result.

When reporting a visual bug, a Blockbench screenshot and a very small model are much more useful than a large production file.

## Scripted post-processing

`BlockbenchImporter.OnPostBlockbenchImport` runs after the generated hierarchy and all sub-assets have been registered. Subscribe from an `AssetPostprocessor` to customize the current import without editing the package:

```csharp
using System.Collections.Generic;
using Essentials.BlockbenchImporter;
using UnityEditor;
using UnityEngine;

internal sealed class MyBlockbenchPostprocessor : AssetPostprocessor
{
    private void OnPreprocessAsset()
    {
        if (!(assetImporter is BlockbenchImporter importer))
            return;

        // Importer instances can be reused, so make the subscription idempotent.
        importer.OnPostBlockbenchImport -= OnPostBlockbenchImport;
        importer.OnPostBlockbenchImport += OnPostBlockbenchImport;
    }

    private static void OnPostBlockbenchImport(BlockbenchImporter.ImportEventArgs args)
    {
        if (!args.rootObject.TryGetComponent<BoxCollider>(out _))
            args.rootObject.AddComponent<BoxCollider>();

        var importedObjects = new List<Object>();
        args.context.GetObjects(importedObjects);
        foreach (Object importedObject in importedObjects)
        {
            if (importedObject is Material material)
                material.enableInstancing = true;
        }
    }
}
```

The callback receives the `BlockbenchImporter`, active `AssetImportContext`, and generated root `GameObject`. It can change generated components and assets, inspect registered objects with `context.GetObjects`, add deterministic sub-assets with `context.AddObjectToAsset`, and report warnings or errors through the context.

Keep callbacks deterministic and limit them to the asset currently being imported. An exception from a callback is treated as an import failure so partially customized output is not silently accepted.

For a complete working example, import **Post-Import Customization** from the package's **Samples** tab in Unity Package Manager. It demonstrates an opt-in filename rule, modifying generated materials, adding a component to the root, and registering an additional text sub-asset.
