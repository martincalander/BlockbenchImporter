# Importing models

## Prepare the Blockbench project

Save the editable project as `.bbmodel`. Embedded textures are the most portable option. If textures are external, keep them at paths Unity can resolve from the model's location and commit them with the model.

Use meaningful, unique outliner names. The importer makes duplicate hierarchy names stable and unique for Unity animation binding, but descriptive source names remain easier to debug.

## Import

Drag the `.bbmodel` into any folder under `Assets/`. Unity creates one main asset with generated sub-assets. Drag the main asset into a scene to instantiate it.

Select the source file to show the importer inspector. Change an option and press **Apply** to regenerate the asset. Reimporting is deterministic and does not modify the source `.bbmodel` or extract textures beside it.

## Settings

### Geometry and hierarchy

- **Units Per Pixel** controls the model scale. `0.0625` means sixteen Blockbench pixels equal one Unity unit.
- **Include Hidden Elements** imports elements hidden in the Blockbench editor.
- **Include Non-Exported Elements** imports elements whose export flag is disabled.
- **Generate Colliders** converts supported bounding-box elements to `BoxCollider` components.

### Materials and textures

- **Material Mode — Unlit** prioritizes Blockbench viewport-like color independent of scene lighting.
- **Material Mode — Lit** creates materials intended to react to the active render pipeline's lighting.
- **Texture Filter** defaults to Point for pixel art; choose Bilinear when texture smoothing is desired.
- **Texture Wrap** defaults to Clamp to avoid sampling across atlas edges.
- **Alpha Mode — Detect** inspects texture alpha. Force Opaque or Cutout when project art requires a fixed policy.

Material appearance can still differ when a project depends on a custom Blockbench shader, color-management setup, or Unity render-pipeline feature. Keep those runtime presentation choices outside the source model when possible.

### Animation

- **Import Animations** creates one `AnimationClip` per supported Blockbench animation.
- **Create Animator Controller** embeds a controller, creates one state per clip, and adds an `Animator` to the imported root.

Position, rotation, and scale channels support numeric linear, stepped, Catmull-Rom, and Bezier interpolation. Molang and other executable expressions are never evaluated; see [format support](format-support.md).

## Recommended source-control practice

- Commit the `.bbmodel` and every external texture it references.
- Commit the Unity `.meta` file generated for the model.
- Pin a tagged package version in `manifest.json`.
- Review importer warnings after Blockbench or package upgrades.
- Keep a screenshot of the intended Blockbench viewport when reporting a fidelity regression.
