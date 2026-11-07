# Importing a model

Save the editable Blockbench project as `.bbmodel`, then drag it anywhere under `Assets/`. Unity creates one main asset with the generated pieces tucked underneath it as sub-assets.

Drag the main asset into a scene to use it.

## Before importing

Embedded textures are easiest to move between machines. External textures work too, but their saved paths still need to resolve after the model enters your Unity project.

Clear, unique outliner names help. Duplicate names are made safe for Unity animation paths, but they are still harder to debug.

## Import settings

Select the `.bbmodel` in the Project window. Change a setting, then press **Apply**.

### Geometry

- **Import Scale** sets the number of Unity units generated for each Blockbench model unit. The default `0.0625` means sixteen Blockbench units equal one Unity unit, matching Blockbench's default 3D export convention. The scale is baked into generated transforms, meshes, colliders, skinning, and position animation while the prefab root remains at `(1, 1, 1)`. Import Scale must be finite; NaN and infinity are rejected.
- **Include Hidden Elements** includes things hidden in the Blockbench editor.
- **Include Non-Exported Elements** includes things with export turned off.
- **Generate Colliders** turns supported bounding boxes into `BoxCollider` components.

### Materials and textures

- **Unlit** is the closest starting point to Blockbench's viewport.
- **Lit** lets the model react to Unity lighting.
- **Point** filtering keeps hard pixel edges. **Bilinear** smooths them.
- **Clamp** wrapping helps stop texture-atlas edges from bleeding.
- **Alpha Mode** can detect transparency or force opaque/cutout rendering.
- **Animate Textures** plays animated texture strips in Play mode.

A custom Blockbench shader, Unity post-processing, tone mapping, or render-pipeline setting can still change the final look.

When both an external image and an embedded snapshot are present, the importer uses the external image, matching Blockbench. It tries the saved relative path first, then the saved path, then the embedded image. Referenced images under `Assets/` or `Packages/` trigger reimport when added, edited, or deleted. Files outside the Unity project require a manual model reimport after editing.

#### Animated textures

Blockbench treats a texture as a flipbook when its image is taller than its UV size, with frames stacked from the top. Materials made from these textures show the first frame. With **Animate Textures** on, the root also gets a **Blockbench Texture Animation** component that plays the frames in Play mode at the texture's frame rate and order: loop, backwards, back and forth, or a custom list. Java block models use Minecraft frame times in ticks, including per-frame `index:ticks` entries. The component sets each animated material slot through its renderer's property block, so the imported materials stay unchanged. Use its **Speed** and **PlaybackTime** properties to control playback from scripts. Frame blending is not reproduced; the importer warns when a texture uses it.

#### PBR texture groups

A Blockbench texture group marked as a material combines a color texture with optional normal, height, and MER textures. With **Material Mode** set to **Lit**, the color texture's material becomes a URP Lit or Standard material:

- a normal texture becomes its normal map; Bedrock normal maps are flipped to Unity's convention;
- without a normal texture, a height texture is converted into a normal map;
- MER red becomes metalness, blue becomes smoothness after inversion, and green masks the color texture into an emission map;
- without a MER texture, the group's metalness, emissive, and roughness values are used.

Generated maps are stored as texture sub-assets beside the originals. Normal, height, and MER textures are imported as linear data and do not get their own materials unless a face uses them directly. In Unlit mode, or with HDRP, the material uses only the color texture and the importer warns.

Automatic alpha detection includes isolated transparent pixels. Built-in pipeline materials use the package's Unlit or Lit shader to preserve marker colors, cutout, and front, back, or double-sided rendering. URP uses its native shaders.

### Animation

- **Import Animations** creates one `AnimationClip` for each supported animation.
- **Create Animator Controller** adds an Animator and a controller with one state per clip.
- **Import Effect Events** turns sound, particle, and instruction keyframes into animation events.

Numeric position, rotation, and scale tracks support linear, stepped, Catmull-Rom, and Bezier interpolation. Molang expressions are not run; see [format support](format-support.md).

#### Effect events

Sound, particle, and instruction (timeline) keyframes become animation events that call a **Blockbench Animation Events** component on the root, beside the generated Animator. The component lists each effect's name, particle locator, script, and file. Subscribe to `EffectTriggered` to play sounds or spawn particles:

```csharp
using MartinCalander.BlockbenchImporter;
using UnityEngine;

public sealed class FootstepSounds : MonoBehaviour
{
    [SerializeField] private AudioSource source;
    [SerializeField] private AudioClip step;

    private void OnEnable() => GetComponent<BlockbenchAnimationEvents>().EffectTriggered += OnEffect;
    private void OnDisable() => GetComponent<BlockbenchAnimationEvents>().EffectTriggered -= OnEffect;

    private void OnEffect(BlockbenchAnimationEvents.Effect effect)
    {
        if (effect.Type == BlockbenchAnimationEvents.EffectType.Sound && effect.Name == "mob.step")
            source.PlayOneShot(step);
    }
}
```

A clip that only contains effect keyframes is still created. If you play the clips with an Animator on another object, add a **Blockbench Animation Events** component there as well, or Unity reports events without a receiver. Instruction scripts are passed through as text and are never run.

Clips retain the declared animation duration and hold their final values when the last key is earlier. Split incoming/outgoing values and independent Bezier handles are preserved. Global rotation tracks compensate for animated parent rotations. Projects saved before format 5 receive the legacy animation axis conversion.

## Faster Play mode

The package supports entering Play mode with **Reload Domain** disabled. Enable the Enter Play Mode options in **Project Settings > Editor** and leave **Reload Scene** enabled when you want scene objects reset on every run.

Import builders keep their working data within each import, and billboards read the current camera without retaining a reference between Play sessions. Generated meshes, materials, animation clips, and Animator Controllers remain ordinary Unity assets.

Custom postprocessors and gameplay scripts must manage their own static state and event subscriptions. Follow Unity's [code lifecycle guidance](https://docs.unity.com/en-us/engine/7000.0/manual/programming-environment/code-reload-serialization/code-reload/code-lifecycle) when adding state that must reset on Play mode entry or release on exit.

## Source control

Commit the `.bbmodel`, its `.meta` file, and every external texture it needs. Pin the importer to a release tag in `manifest.json` so everyone gets the same result.

When reporting a visual bug, a Blockbench screenshot and a very small model are much more useful than a large production file.

## Scripted post-processing

`BlockbenchImporter.OnPostBlockbenchImport` runs after the generated hierarchy and all sub-assets have been registered. Subscribe from an `AssetPostprocessor` to customize the current import without editing the package:

```csharp
using System.Collections.Generic;
using MartinCalander.BlockbenchImporter;
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
