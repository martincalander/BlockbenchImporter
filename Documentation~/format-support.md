# Format support

The importer targets Blockbench project format 5 and reads older compatible fields defensively. “Supported” means the data has a deliberate Unity representation; it does not imply that every custom shader or plugin behaves identically outside Blockbench.

## Support matrix

| Blockbench data | Unity representation | Status |
|---|---|---|
| Outliner groups and nested pivots | Transform hierarchy | Supported |
| Cubes, inflate, stretch, rotation | Mesh sub-assets | Supported |
| Six cube faces and disabled faces | Submeshes and omitted faces | Supported |
| Face UV rectangles and 90° rotation | Unity UV coordinates | Supported |
| Polygon meshes and concave faces | Triangulated Mesh | Supported |
| Armatures, bones, weights, bind pose | SkinnedMeshRenderer | Supported |
| Camera-facing billboards | Runtime billboard component | Supported |
| Embedded PNG textures | Texture2D sub-assets | Supported |
| Relative/absolute external textures | Texture2D sub-assets | Supported when readable |
| Per-face textures and materials | Material slots/submeshes | Supported |
| Locators and null objects | Transform nodes | Supported |
| Bounding boxes | Transform; optional BoxCollider | Supported |
| Numeric position/rotation/scale channels | AnimationClip curves | Supported |
| Linear and stepped interpolation | Animation curves | Supported |
| Catmull-Rom and Bezier interpolation | Baked Animation curves | Supported |
| Once and loop playback modes | AnimationClip wrap behavior | Supported |
| Marker colors | Generated utility materials | Supported |
| Plugin-defined unknown elements | Transform plus warning | Preserved, not rendered |
| Texture meshes and splines | Transform plus warning | Geometry not generated |
| PBR texture groups | Individual textures plus warning | No channel packing |
| Animated texture atlases | Individual texture plus warning | No runtime playback |
| Molang/executable expressions | Warning; numeric value resolves to zero | Never executed |

## Coordinate translation

Blockbench and Unity use different coordinate conventions. The importer converts positions, rotations, normals, winding, pivots, and animation values together; changing only one of those would produce mirrored geometry or incorrect motion. Element origins are interpreted in Blockbench's shared model space before they are converted into parent-local Unity transforms.

## Diagnostics policy

The importer prefers visible warnings over silent loss. Data with a safe structural representation is preserved as a named transform even when specialized geometry cannot yet be generated. Warnings identify the element or feature so users can simplify the source project or open a focused feature request.

## Reporting a fidelity issue

Provide the smallest `.bbmodel` that reproduces the issue, a screenshot from Blockbench, the expected and actual result, Unity version, render pipeline, package version, and importer settings. Remove private textures or proprietary geometry before attaching a file to a public issue.
