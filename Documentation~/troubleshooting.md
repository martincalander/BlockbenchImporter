# Troubleshooting

## The model is pink

Pink usually means the generated material's shader is unavailable in the active render pipeline. Confirm the importer Material Mode, then reimport. If a pipeline was installed or changed after the first import, select the `.bbmodel` and choose **Reimport**.

## The scale is wrong

Check **Units Per Pixel**. The default `0.0625` maps sixteen Blockbench pixels to one Unity unit. A project authored for a different export scale needs a matching value.

## A texture is missing

- Prefer embedded textures for portable source models.
- For external textures, verify the path stored in the `.bbmodel` and keep the file accessible beside the project.
- Check the Console for the exact unresolved path.
- Reimport after moving either the model or its textures.

## An element is absent

Check whether it is hidden or has its export flag disabled in Blockbench, then inspect **Include Hidden Elements** and **Include Non-Exported Elements**. Plugin-defined elements may be preserved only as transforms; the Console will explain that case.

## An animation does not move the expected object

Use unique source names and avoid renaming generated children in an instance. Confirm that **Import Animations** is enabled. If clips exist but no controller is attached, enable **Create Animator Controller** or connect the clips to your own controller.

## The Blockbench and Unity views differ

Compare with an Unlit material first; scene lighting, tone mapping, fog, post-processing, and color space can all change a Lit result. Also confirm texture filtering, alpha mode, camera projection, and billboard mode.

## The Console reports Molang or an unknown element

This is intentional. The importer does not execute expressions embedded in asset files. Unknown/plugin-defined element data is retained structurally where possible and reported. Bake the result to numeric keyframes or supported geometry in Blockbench before import.

## Still stuck?

Search [existing issues](https://github.com/martincalander/UnityBlockbenchImporter/issues), then use the bug-report form. Include a minimal, redistributable model and the Unity Editor log excerpt around the import warning. See [SUPPORT.md](../SUPPORT.md) for response expectations.
