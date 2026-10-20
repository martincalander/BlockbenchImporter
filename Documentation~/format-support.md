# Format support

The importer is written for Blockbench project format 5. It also accepts older fields when their meaning is clear.

“Preserved” below means the item still appears in the hierarchy, but Unity cannot render or play all of its data yet.

| Blockbench data | Unity result | Support |
| --- | --- | --- |
| Groups and nested pivots | Transform hierarchy | Yes |
| Cubes, inflate, stretch, rotation, rescale | Mesh | Yes |
| Enabled and disabled cube faces | Generated or omitted faces | Yes |
| Face UV rectangles and 90° rotation | Mesh UVs | Yes |
| Polygon meshes and concave faces | Triangulated Mesh | Yes |
| Smooth mesh shading | Flat face normals | No smooth-normal reconstruction yet |
| Bones, weights, and bind poses | SkinnedMeshRenderer | Yes |
| Camera-facing billboards | Billboard component | Yes |
| Embedded PNG textures | Texture2D sub-assets | Yes |
| External textures | Texture2D sub-assets | When the path can be read |
| Per-face textures | Material slots and submeshes | Yes |
| Locators and null objects | Transform nodes | Yes |
| Bounding boxes | Transform; optional BoxCollider | Yes |
| Numeric position/rotation/scale tracks | AnimationClip curves | Yes |
| Linear and stepped interpolation | Animation curves | Yes |
| Catmull-Rom and Bezier interpolation | Tangent curves; sampled rotation | Yes |
| Split keys and global rotations | Curves with parent motion accounted for | Yes |
| Once and loop playback | AnimationClip wrap mode | Yes |
| Marker colors | Simple generated materials | Yes |
| Unknown plugin elements | Transform and Console warning | Preserved |
| Texture meshes and splines | Transform and Console warning | Preserved; no mesh yet |
| PBR texture groups | Separate textures and warning | No channel packing yet |
| Animated texture atlases | Texture and warning | No playback yet |
| Molang or other expressions | Warning; value becomes zero | Never executed |

## Coordinates

Blockbench and Unity do not agree on every axis convention. Positions, pivots, rotations, normals, winding, skinning, and animation values have to move together or the model ends up mirrored or animated around the wrong point.

Groups and their ordinary child pivots begin in shared model space. Armatures are containers whose bone origins are relative to their parent. The importer uses those distinctions when calculating Unity transforms, and uses Blockbench's ZYX order for mixed-axis rotations.

## When data is not supported

The importer warns instead of dropping data without a word. If it can keep a useful named transform, it does. The warning should point to the item that needs attention.

Malformed JSON, cyclic child references, non-finite geometry values, and nesting beyond 128 levels fail the import with an error. Repeated outliner references keep their first occurrence and produce a warning. Non-finite animation values use the same zero fallback as unsupported expressions.

If something marked **Yes** imports incorrectly, please send the smallest `.bbmodel` that reproduces it, screenshots from Blockbench and Unity, the package and Unity versions, your render pipeline, and the import settings you changed.
