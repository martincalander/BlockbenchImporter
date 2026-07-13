using System.Collections.Generic;
using MartinCalander.BlockbenchImporter.Model;
using MartinCalander.BlockbenchImporter.Utils;
using UnityEngine;

namespace MartinCalander.BlockbenchImporter.MeshBuilders
{
    /// <summary>
    /// Builds Unity Mesh objects from Blockbench cube elements.
    /// Creates 24-vertex meshes (4 per face) for proper normals and UVs.
    /// </summary>
    public static class CubeMeshBuilder
    {
        private static readonly string[] FaceNames = { "north", "south", "east", "west", "up", "down" };

        /// <summary>
        /// Build a mesh from a Blockbench cube element.
        /// </summary>
        /// <param name="element">The cube element</param>
        /// <param name="textureResolution">Resolution from model (width, height)</param>
        /// <param name="textureCount">Number of textures in the model</param>
        /// <returns>Mesh with submeshes per texture</returns>
        public static CubeMeshResult BuildCubeMesh(BbElement element, int[] textureResolution, int textureCount,
            bool generateBackfaces = false, System.Func<int, bool> isDoubleSidedTexture = null,
            IReadOnlyList<BbTexture> textures = null)
        {
            var result = new CubeMeshResult();

            if (element.type != "cube")
            {
                Debug.LogWarning($"CubeMeshBuilder called on non-cube element: {element.name}");
                return result;
            }

            // Blockbench stretches the inflated half-size around the cube center.
            var adjustedFrom = new float[3];
            var adjustedTo = new float[3];
            for (int axis = 0; axis < 3; axis++)
            {
                float center = (element.from[axis] + element.to[axis]) * 0.5f;
                float halfSize = Mathf.Abs(element.to[axis] - element.from[axis]) * 0.5f;
                float stretch = element.stretch != null && element.stretch.Length > axis ? element.stretch[axis] : 1f;
                adjustedFrom[axis] = center - (halfSize + element.inflate) * stretch;
                adjustedTo[axis] = center + (halfSize + element.inflate) * stretch;
            }
            Vector3 from = new Vector3(-adjustedFrom[0], adjustedFrom[1], adjustedFrom[2]);
            Vector3 to = new Vector3(-adjustedTo[0], adjustedTo[1], adjustedTo[2]);

            // Convert to element-local space (subtract origin)
            // Geometry is built relative to the element's origin/pivot
            // Origin is also negated on X
            Vector3 origin = new Vector3(-element.origin[0], element.origin[1], element.origin[2]);
            Vector3 localMin = from - origin;
            Vector3 localMax = to - origin;

            // No axis conversion needed - Blockbench and Unity use same Y-up orientation
            // Scale will be applied at the root GameObject level
            Vector3 min = Vector3.Min(localMin, localMax);
            Vector3 max = Vector3.Max(localMin, localMax);

            // Build 8 corners of the cube (in Blockbench pixel units, centered on origin)
            Vector3[] corners = new Vector3[8];
            corners[0] = new Vector3(min.x, min.y, min.z);
            corners[1] = new Vector3(max.x, min.y, min.z);
            corners[2] = new Vector3(max.x, max.y, min.z);
            corners[3] = new Vector3(min.x, max.y, min.z);
            corners[4] = new Vector3(min.x, min.y, max.z);
            corners[5] = new Vector3(max.x, min.y, max.z);
            corners[6] = new Vector3(max.x, max.y, max.z);
            corners[7] = new Vector3(min.x, max.y, max.z);

            // Collect vertices, normals, UVs per texture
            var verticesList = new List<Vector3>();
            var normalsList = new List<Vector3>();
            var uvsList = new List<Vector2>();
            var trianglesByTexture = new Dictionary<int, List<int>>();

            foreach (var faceName in FaceNames)
            {
                if (!element.faces.TryGetValue(faceName, out var face))
                    continue;

                if (!face.enabled)
                    continue;

                // Get texture index (-1 means no texture, use default material)
                int textureIndex = face.texture;
                if (textureIndex < -1) textureIndex = -1;

                // Use -1 as "no texture" material slot, map to index 0 for submesh if no textures
                int submeshKey = textureIndex < 0 ? -1 - Mathf.Abs(element.color % 10) : textureIndex;
                if (!trianglesByTexture.TryGetValue(submeshKey, out List<int> tris))
                {
                    tris = new List<int>(6);
                    trianglesByTexture[submeshKey] = tris;
                }

                Vector3 normal = CoordinateConverter.GetFaceNormal(faceName);
                int[] cornerIndices = CoordinateConverter.GetFaceCornerIndices(faceName);

                int baseVertex = verticesList.Count;

                int texWidth = textureResolution != null && textureResolution.Length > 0 ? textureResolution[0] : 16;
                int texHeight = textureResolution != null && textureResolution.Length > 1 ? textureResolution[1] : 16;
                if (textures != null && textureIndex >= 0 && textureIndex < textures.Count)
                {
                    texWidth = Mathf.Max(1, textures[textureIndex].uv_width);
                    texHeight = Mathf.Max(1, textures[textureIndex].uv_height);
                }

                // Add 4 vertices for this face
                for (int i = 0; i < 4; i++)
                {
                    verticesList.Add(corners[cornerIndices[i]]);
                    normalsList.Add(normal);
                }

                // Calculate UVs
                float u0 = face.uv[0] / texWidth;
                float v0 = 1f - face.uv[1] / texHeight; // Flip V (BB top-left origin -> Unity bottom-left)
                float u1 = face.uv[2] / texWidth;
                float v1 = 1f - face.uv[3] / texHeight;

                // Base UV corners (before rotation)
                Vector2[] baseUvs = new Vector2[4];
                baseUvs[0] = new Vector2(u0, v1); // bottom-left
                baseUvs[1] = new Vector2(u1, v1); // bottom-right
                baseUvs[2] = new Vector2(u1, v0); // top-right
                baseUvs[3] = new Vector2(u0, v0); // top-left

                // Apply UV rotation (in 90° steps)
                int rotation = face.rotation / 90;
                for (int i = 0; i < 4; i++)
                {
                    int rotatedIndex = (i + rotation) % 4;
                    uvsList.Add(baseUvs[rotatedIndex]);
                }

                bool reverse = Vector3.Dot(Vector3.Cross(verticesList[baseVertex + 1] - verticesList[baseVertex],
                    verticesList[baseVertex + 2] - verticesList[baseVertex]), normal) < 0f;
                AddQuad(tris, baseVertex, reverse);
                if (generateBackfaces && isDoubleSidedTexture != null && isDoubleSidedTexture(textureIndex))
                    AddQuad(tris, baseVertex, !reverse);
            }

            // Build mesh
            var mesh = new Mesh();
            mesh.name = element.name ?? "Cube";

            // Use UInt32 index format to match glTF export
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            mesh.SetVertices(verticesList);
            mesh.SetNormals(normalsList);
            mesh.SetUVs(0, uvsList);

            // Build submeshes
            var sortedTextureIndices = new List<int>(trianglesByTexture.Keys);
            sortedTextureIndices.Sort();

            mesh.subMeshCount = sortedTextureIndices.Count;
            result.textureIndices = new int[sortedTextureIndices.Count];

            for (int i = 0; i < sortedTextureIndices.Count; i++)
            {
                int texIdx = sortedTextureIndices[i];
                mesh.SetTriangles(trianglesByTexture[texIdx], i, false);
                result.textureIndices[i] = texIdx;
            }

            mesh.RecalculateBounds();
            // Don't calculate tangents - match glTF export which doesn't include them

            result.mesh = mesh;
            return result;
        }

        private static void AddQuad(List<int> triangles, int start, bool reverse)
        {
            triangles.Add(start);
            triangles.Add(start + (reverse ? 2 : 1));
            triangles.Add(start + (reverse ? 1 : 2));
            triangles.Add(start);
            triangles.Add(start + (reverse ? 3 : 2));
            triangles.Add(start + (reverse ? 2 : 3));
        }

        /// <summary>
        /// Merge multiple cube meshes into a single mesh with submeshes.
        /// Useful for combining elements under a single node.
        /// </summary>
        public static CubeMeshResult MergeMeshes(List<CubeMeshResult> meshResults)
        {
            if (meshResults == null || meshResults.Count == 0)
                return new CubeMeshResult();

            if (meshResults.Count == 1)
                return meshResults[0];

            var allTextureIndices = new HashSet<int>();

            // Collect all unique texture indices
            foreach (var result in meshResults)
            {
                if (result.mesh == null) continue;
                foreach (var texIdx in result.textureIndices)
                    allTextureIndices.Add(texIdx);
            }

            var sortedTexIndices = new List<int>(allTextureIndices);
            sortedTexIndices.Sort();

            // For each texture, combine all submeshes using that texture
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var trianglesByTexture = new Dictionary<int, List<int>>();

            foreach (var texIdx in sortedTexIndices)
                trianglesByTexture[texIdx] = new List<int>();

            foreach (var result in meshResults)
            {
                if (result.mesh == null) continue;

                int baseVertex = vertices.Count;
                vertices.AddRange(result.mesh.vertices);
                normals.AddRange(result.mesh.normals);
                uvs.AddRange(result.mesh.uv);

                for (int submesh = 0; submesh < result.mesh.subMeshCount; submesh++)
                {
                    int texIdx = result.textureIndices[submesh];
                    var tris = result.mesh.GetTriangles(submesh);
                    for (int i = 0; i < tris.Length; i++)
                    {
                        trianglesByTexture[texIdx].Add(tris[i] + baseVertex);
                    }
                }

                // Don't destroy the original meshes - they might be needed
            }

            var mergedMesh = new Mesh();
            mergedMesh.name = "MergedMesh";

            // Use UInt32 index format to match glTF export
            mergedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            mergedMesh.vertices = vertices.ToArray();
            mergedMesh.normals = normals.ToArray();
            mergedMesh.uv = uvs.ToArray();

            mergedMesh.subMeshCount = sortedTexIndices.Count;
            var resultTexIndices = new int[sortedTexIndices.Count];

            for (int i = 0; i < sortedTexIndices.Count; i++)
            {
                int texIdx = sortedTexIndices[i];
                mergedMesh.SetTriangles(trianglesByTexture[texIdx].ToArray(), i);
                resultTexIndices[i] = texIdx;
            }

            mergedMesh.RecalculateBounds();
            // Don't calculate tangents - match glTF export which doesn't include them

            return new CubeMeshResult
            {
                mesh = mergedMesh,
                textureIndices = resultTexIndices
            };
        }
    }

    public class CubeMeshResult
    {
        public Mesh mesh;
        public int[] textureIndices = new int[0]; // Texture index for each submesh
        public string[] sourceVertexIds = new string[0];
    }
}
