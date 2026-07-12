# Importing a model

Save the editable Blockbench project as `.bbmodel`, then drag it anywhere under `Assets/`. Unity creates one main asset with the generated pieces tucked underneath it as sub-assets.

Drag the main asset into a scene to use it.

## Before importing

Embedded textures are easiest to move between machines. External textures work too, but their saved paths still need to resolve after the model enters your Unity project.

Clear, unique outliner names help. Duplicate names are made safe for Unity animation paths, but they are still harder to debug.

## Import settings

Select the `.bbmodel` in the Project window. Change a setting, then press **Apply**.

### Geometry

- **Units Per Pixel** sets the scale. At the default `0.0625`, sixteen Blockbench pixels equal one Unity unit.
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
