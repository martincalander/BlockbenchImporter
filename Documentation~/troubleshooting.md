# Troubleshooting

## The model is pink

Unity cannot find the shader used by the generated material. Check **Material Mode** and reimport the `.bbmodel`. If you changed render pipelines after importing it, reimport once the new pipeline is ready.

## The scale is wrong

Check **Units Per Pixel**. The default is `0.0625`, or sixteen Blockbench pixels per Unity unit.

## A texture is missing

- Check the Console for the path Unity tried to read.
- Make sure an external texture moved with the `.bbmodel`.
- Reimport after moving either file.
- Embed the texture in Blockbench if the project needs to move between machines often.

## An element is missing

Was it hidden or marked not to export in Blockbench? Enable **Include Hidden Elements** or **Include Non-Exported Elements** if you want it anyway.

Plugin-defined elements may only appear as transforms for now. The Console warning will say so.

## An animation targets the wrong object

Use unique names in the Blockbench outliner and avoid renaming generated children in a scene instance. Make sure **Import Animations** is enabled.

If the clips exist but nothing plays them, enable **Create Animator Controller** or add the clips to your own controller.

## It looks different from Blockbench

Try **Unlit** first. Unity lighting, color space, tone mapping, fog, post-processing, texture filtering, and camera projection can all change the result even when the mesh and UVs are correct.

Also check alpha mode and billboard mode.

## Molang or unknown-element warnings

The importer does not run expressions from asset files. Bake expression-driven animation to numeric keyframes in Blockbench. Unknown plugin geometry needs a dedicated importer implementation before Unity can render it.

## Still stuck?

Search the [issues](https://github.com/martincalander/UnityBlockbenchImporter/issues), then open a bug report. A tiny model you can legally share, the relevant Console lines, and matching Blockbench/Unity screenshots are ideal.
