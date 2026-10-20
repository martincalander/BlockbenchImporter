using System.Collections.Generic;
using MartinCalander.BlockbenchImporter.Model;
using MartinCalander.BlockbenchImporter.Utils;
using UnityEngine;

namespace MartinCalander.BlockbenchImporter.MeshBuilders
{
    /// <summary>
    /// Builds Unity Mesh objects from Blockbench mesh elements (type:"mesh").
    /// Handles arbitrary vertex positions and polygon faces.
    /// </summary>
    public static class MeshElementBuilder
    {
        /// <summary>
        /// Build a mesh from a Blockbench mesh element.
        /// </summary>
        public static CubeMeshResult BuildMeshElement(BbElement element, int[] textureResolution, int textureCount,
            IReadOnlyList<BbTexture> textures = null)
        {
            var result = new CubeMeshResult();

            if (element.type != "mesh" || element.vertices == null || element.meshFaces == null)
            {
                Debug.LogWarning($"MeshElementBuilder called on invalid mesh element: {element.name}");
                return result;
            }

            int texWidth = textureResolution != null && textureResolution.Length > 0 ? textureResolution[0] : 16;
            int texHeight = textureResolution != null && textureResolution.Length > 1 ? textureResolution[1] : 16;

            // Build lookup for vertices
            // NOTE: For mesh elements, vertices are ALREADY stored in local space relative to origin
            // (unlike cube elements where from/to are in world coordinates)
            // So we only need to negate X to match glTF export - no origin subtraction needed
            var vertexLookup = new Dictionary<string, Vector3>(element.vertices.Count);
            foreach (var kvp in element.vertices)
            {
                // Negate X to match glTF export coordinate system
                // Vertices are already in local space - don't subtract origin
                Vector3 pos = new Vector3(-kvp.Value[0], kvp.Value[1], kvp.Value[2]);
                vertexLookup[kvp.Key] = pos;
            }

            var verticesList = new List<Vector3>();
            var normalsList = new List<Vector3>();
            var uvsList = new List<Vector2>();
            var sourceVertexIds = new List<string>();
            var trianglesByTexture = new Dictionary<int, List<int>>();
            var faceVerts = new List<Vector3>();
            var faceUvs = new List<Vector2>();
            var faceVertexIds = new List<string>();
            var triangles = new List<int>(6);
            var projectedPoints = new List<Vector2>(4);
            var faceIndices = new List<int>(4);

            foreach (var faceKvp in element.meshFaces)
            {
                var face = faceKvp.Value;
                if (!face.enabled || face.vertices == null || face.vertices.Count < 3)
                    continue;

                faceVerts.Clear();
                faceUvs.Clear();
                faceVertexIds.Clear();

                int textureIndex = face.texture;
                int submeshKey = textureIndex < 0 ? -1 - Mathf.Abs(element.color % 10) : textureIndex;
                if (!trianglesByTexture.TryGetValue(submeshKey, out List<int> submeshTriangles))
                {
                    submeshTriangles = new List<int>();
                    trianglesByTexture[submeshKey] = submeshTriangles;
                }

                // Get vertices for this face
                int faceTexWidth = texWidth;
                int faceTexHeight = texHeight;
                if (textures != null && textureIndex >= 0 && textureIndex < textures.Count)
                {
                    faceTexWidth = Mathf.Max(1, textures[textureIndex].uv_width);
                    faceTexHeight = Mathf.Max(1, textures[textureIndex].uv_height);
                }

                foreach (var vertId in face.vertices)
                {
                    if (vertexLookup.TryGetValue(vertId, out var pos))
                    {
                        faceVerts.Add(pos);
                        faceVertexIds.Add(vertId);

                        // Get UV for this vertex
                        if (face.uv != null && face.uv.TryGetValue(vertId, out var uvCoords) && uvCoords.Length >= 2)
                        {
                            float u = uvCoords[0] / faceTexWidth;
                            float v = 1f - uvCoords[1] / faceTexHeight; // Flip V
                            faceUvs.Add(new Vector2(u, v));
                        }
                        else
                        {
                            faceUvs.Add(Vector2.zero);
                        }
                    }
                }

                if (faceVerts.Count < 3)
                    continue;

                // A reflection reverses the cross product's orientation. Negate
                // it to preserve the source face's outward-facing normal.
                Vector3 normal = -CalculateFaceNormal(faceVerts);

                // Triangulate the face
                EarClipTriangulate(faceVerts, normal, triangles, projectedPoints, faceIndices);
                EnsureWindingMatchesNormal(triangles, faceVerts, normal);

                // Add vertices and triangles
                int baseVertex = verticesList.Count;

                for (int i = 0; i < faceVerts.Count; i++)
                {
                    verticesList.Add(faceVerts[i]);
                    normalsList.Add(normal);
                    uvsList.Add(faceUvs[i]);
                }
                sourceVertexIds.AddRange(faceVertexIds);

                foreach (var triIdx in triangles)
                {
                    submeshTriangles.Add(baseVertex + triIdx);
                }
            }

            if (verticesList.Count == 0)
                return result;

            // Build mesh
            var mesh = new Mesh();
            mesh.name = element.name ?? "MeshElement";

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
            result.sourceVertexIds = sourceVertexIds.ToArray();
            return result;
        }

        /// <summary>
        /// Calculate face normal from vertices.
        /// </summary>
        private static Vector3 CalculateFaceNormal(List<Vector3> vertices)
        {
            if (vertices.Count < 3)
                return Vector3.up;

            Vector3 v0 = vertices[0];
            Vector3 v1 = vertices[1];
            Vector3 v2 = vertices[2];

            Vector3 edge1 = v1 - v0;
            Vector3 edge2 = v2 - v0;

            Vector3 normal = Vector3.Cross(edge1, edge2).normalized;

            // If normal is zero (degenerate face), try other combinations
            if (normal.sqrMagnitude < 0.0001f && vertices.Count > 3)
            {
                for (int i = 1; i < vertices.Count - 1; i++)
                {
                    edge1 = vertices[i] - v0;
                    edge2 = vertices[i + 1] - v0;
                    normal = Vector3.Cross(edge1, edge2).normalized;
                    if (normal.sqrMagnitude > 0.0001f)
                        break;
                }
            }

            return normal.sqrMagnitude > 0.0001f ? normal : Vector3.up;
        }

        private static void EnsureWindingMatchesNormal(List<int> triangles, List<Vector3> vertices, Vector3 normal)
        {
            for (int index = 0; index + 2 < triangles.Count; index += 3)
            {
                Vector3 cross = Vector3.Cross(vertices[triangles[index + 1]] - vertices[triangles[index]],
                    vertices[triangles[index + 2]] - vertices[triangles[index]]);
                if (Vector3.Dot(cross, normal) >= 0f) continue;
                int swap = triangles[index + 1];
                triangles[index + 1] = triangles[index + 2];
                triangles[index + 2] = swap;
            }
        }

        /// <summary>
        /// Ear clipping triangulation for potentially concave polygons.
        /// Winding is reversed to compensate for X-axis negation.
        /// </summary>
        public static List<int> EarClipTriangulate(List<Vector3> vertices, Vector3 normal)
        {
            var triangles = new List<int>();
            var points2D = vertices.Count > 3 ? new List<Vector2>(vertices.Count) : null;
            var indices = vertices.Count > 3 ? new List<int>(vertices.Count) : null;
            EarClipTriangulate(vertices, normal, triangles, points2D, indices);
            return triangles;
        }

        private static void EarClipTriangulate(List<Vector3> vertices, Vector3 normal,
            List<int> triangles, List<Vector2> points2D, List<int> indices)
        {
            triangles.Clear();
            if (vertices.Count < 3)
                return;

            if (vertices.Count == 3)
            {
                // Reversed winding
                triangles.Add(0);
                triangles.Add(2);
                triangles.Add(1);
                return;
            }

            // Project to 2D
            Vector3 u, v;
            GetOrthonormalBasis(normal, out u, out v);

            points2D.Clear();
            foreach (var vert in vertices)
            {
                float px = Vector3.Dot(vert, u);
                float py = Vector3.Dot(vert, v);
                points2D.Add(new Vector2(px, py));
            }

            // Determine winding
            float signedArea = 0;
            for (int i = 0; i < points2D.Count; i++)
            {
                int j = (i + 1) % points2D.Count;
                signedArea += points2D[i].x * points2D[j].y;
                signedArea -= points2D[j].x * points2D[i].y;
            }

            bool ccw = signedArea > 0;

            // Create index list
            indices.Clear();
            for (int i = 0; i < vertices.Count; i++)
                indices.Add(i);

            // Ear clipping
            int safetyCounter = indices.Count * indices.Count;
            while (indices.Count > 3 && safetyCounter > 0)
            {
                safetyCounter--;
                bool earFound = false;

                for (int i = 0; i < indices.Count; i++)
                {
                    int prev = (i - 1 + indices.Count) % indices.Count;
                    int next = (i + 1) % indices.Count;

                    int iPrev = indices[prev];
                    int iCurr = indices[i];
                    int iNext = indices[next];

                    Vector2 a = points2D[iPrev];
                    Vector2 b = points2D[iCurr];
                    Vector2 c = points2D[iNext];

                    // Check if this is a convex vertex
                    float cross = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
                    bool isConvex = ccw ? cross > 0 : cross < 0;

                    if (!isConvex)
                        continue;

                    // Check if any other vertex is inside this triangle
                    bool hasPointInside = false;
                    for (int j = 0; j < indices.Count; j++)
                    {
                        if (j == prev || j == i || j == next)
                            continue;

                        if (PointInTriangle(points2D[indices[j]], a, b, c))
                        {
                            hasPointInside = true;
                            break;
                        }
                    }

                    if (!hasPointInside)
                    {
                        // This is an ear - add triangle (reversed winding)
                        triangles.Add(iPrev);
                        triangles.Add(iNext);
                        triangles.Add(iCurr);

                        indices.RemoveAt(i);
                        earFound = true;
                        break;
                    }
                }

                if (!earFound)
                {
                    // Fallback to fan triangulation if ear clipping fails (reversed winding)
                    Debug.LogWarning("Ear clipping failed, falling back to fan triangulation");
                    triangles.Clear();
                    for (int i = 1; i < vertices.Count - 1; i++)
                    {
                        triangles.Add(0);
                        triangles.Add(i + 1);
                        triangles.Add(i);
                    }
                    break;
                }
            }

            // Add last triangle (reversed winding)
            if (indices.Count == 3)
            {
                triangles.Add(indices[0]);
                triangles.Add(indices[2]);
                triangles.Add(indices[1]);
            }
        }

        private static void GetOrthonormalBasis(Vector3 normal, out Vector3 u, out Vector3 v)
        {
            Vector3 up = Mathf.Abs(Vector3.Dot(normal, Vector3.up)) < 0.9f ? Vector3.up : Vector3.right;
            u = Vector3.Cross(up, normal).normalized;
            v = Vector3.Cross(normal, u).normalized;
        }

        private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Sign(p, a, b);
            float d2 = Sign(p, b, c);
            float d3 = Sign(p, c, a);

            bool hasNeg = (d1 < 0) || (d2 < 0) || (d3 < 0);
            bool hasPos = (d1 > 0) || (d2 > 0) || (d3 > 0);

            return !(hasNeg && hasPos);
        }

        private static float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
        {
            return (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
        }
    }
}
