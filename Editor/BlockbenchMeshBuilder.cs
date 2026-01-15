using System.Collections.Generic;
using UnityEngine;

namespace MartinCalander.BlockbenchImporter
{
    /// <summary>
    /// Result of building a Blockbench mesh, containing mesh and texture mapping info.
    /// </summary>
    public class BlockbenchMeshResult
    {
        public Mesh Mesh;
        public int[] TextureIndicesPerSubmesh; // Maps submesh index to texture index
    }
    
    /// <summary>
    /// Converts Blockbench elements to Unity meshes.
    /// </summary>
    public static class BlockbenchMeshBuilder
    {
        // Blockbench uses a coordinate system where Y is up, similar to Unity,
        // but Blockbench models are often centered differently.
        // Blockbench units are typically 1/16th of a block (1 unit = 0.0625 meters in Minecraft scale)
        private const float UnitScale = 0.0625f; // Can be adjusted
        
        public static Mesh BuildMesh(BlockbenchModel model)
        {
            return BuildMeshWithSubmeshes(model).Mesh;
        }
        
        public static BlockbenchMeshResult BuildMeshWithSubmeshes(BlockbenchModel model)
        {
            var result = new BlockbenchMeshResult();
            var mesh = new Mesh();
            mesh.name = model.name ?? "BlockbenchModel";
            
            // Collect all unique texture indices used
            var textureToSubmesh = new Dictionary<int, int>();
            var submeshToTexture = new List<int>();
            
            // First pass: identify all textures used
            if (model.elements != null)
            {
                foreach (var element in model.elements)
                {
                    if (element == null || !element.visibility || !element.export) continue;
                    
                    if (element.faces != null)
                    {
                        foreach (var face in element.faces.Values)
                        {
                            int texIndex = face.texture >= 0 ? face.texture : -1;
                            if (!textureToSubmesh.ContainsKey(texIndex))
                            {
                                textureToSubmesh[texIndex] = submeshToTexture.Count;
                                submeshToTexture.Add(texIndex);
                            }
                        }
                    }
                }
            }
            
            // If no textures found, use a single default submesh
            if (submeshToTexture.Count == 0)
            {
                textureToSubmesh[-1] = 0;
                submeshToTexture.Add(-1);
            }
            
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var normals = new List<Vector3>();
            var colors = new List<Color>();
            
            // Triangles per submesh
            var trianglesPerSubmesh = new List<List<int>>();
            for (int i = 0; i < submeshToTexture.Count; i++)
            {
                trianglesPerSubmesh.Add(new List<int>());
            }
            
            int vertexOffset = 0;
            
            if (model.elements != null)
            {
                foreach (var element in model.elements)
                {
                    if (element == null) continue;
                    if (!element.visibility || !element.export) continue;
                    
                    int addedVertices;
                    
                    if (element.type == "mesh")
                    {
                        addedVertices = BuildMeshElement(element, model.resolution, vertices, trianglesPerSubmesh, uvs, normals, colors, vertexOffset, textureToSubmesh);
                    }
                    else // cube or default
                    {
                        addedVertices = BuildCubeElement(element, model.resolution, vertices, trianglesPerSubmesh, uvs, normals, colors, vertexOffset, textureToSubmesh);
                    }
                    
                    vertexOffset += addedVertices;
                }
            }
            
            if (vertices.Count > 0)
            {
                mesh.SetVertices(vertices);
                mesh.SetUVs(0, uvs);
                
                if (normals.Count == vertices.Count)
                {
                    mesh.SetNormals(normals);
                }
                
                if (colors.Count == vertices.Count)
                {
                    mesh.SetColors(colors);
                }
                
                // Set submeshes
                mesh.subMeshCount = submeshToTexture.Count;
                for (int i = 0; i < submeshToTexture.Count; i++)
                {
                    mesh.SetTriangles(trianglesPerSubmesh[i], i);
                }
                
                if (normals.Count != vertices.Count)
                {
                    mesh.RecalculateNormals();
                }
                
                mesh.RecalculateBounds();
                mesh.RecalculateTangents();
            }
            
            result.Mesh = mesh;
            result.TextureIndicesPerSubmesh = submeshToTexture.ToArray();
            return result;
        }

        private static int BuildMeshElement(BlockbenchElement element, BlockbenchResolution resolution, 
            List<Vector3> vertices, List<List<int>> trianglesPerSubmesh, List<Vector2> uvs, 
            List<Vector3> normals, List<Color> colors, int vertexOffset, Dictionary<int, int> textureToSubmesh)
        {
            if (element.vertices == null || element.meshFaces == null)
                return 0;
            
            int addedVertices = 0;
            float resWidth = resolution?.width ?? 16;
            float resHeight = resolution?.height ?? 16;
            
            // Get element origin and rotation
            Vector3 origin = Vector3.zero;
            if (element.origin != null && element.origin.Length >= 3)
            {
                origin = new Vector3(element.origin[0], element.origin[1], element.origin[2]) * UnitScale;
            }
            
            Quaternion rotation = Quaternion.identity;
            if (element.rotation != null && element.rotation.Length >= 3)
            {
                rotation = Quaternion.Euler(element.rotation[0], element.rotation[1], element.rotation[2]);
            }
            
            // Process each face
            foreach (var facePair in element.meshFaces)
            {
                var face = facePair.Value;
                if (face.vertices == null || face.vertices.Length < 3) continue;
                
                // Get vertices for this face
                var faceVertices = new List<Vector3>();
                var faceUvs = new List<Vector2>();
                
                foreach (string vertId in face.vertices)
                {
                    if (!element.vertices.TryGetValue(vertId, out float[] coords))
                        continue;
                    
                    Vector3 pos = new Vector3(coords[0], coords[1], coords[2]) * UnitScale;
                    pos = rotation * (pos - origin) + origin;
                    faceVertices.Add(pos);
                    
                    // Get UV for this vertex
                    Vector2 uv = Vector2.zero;
                    if (face.uv != null && face.uv.TryGetValue(vertId, out float[] uvCoords))
                    {
                        uv = new Vector2(uvCoords[0] / resWidth, 1f - (uvCoords[1] / resHeight));
                    }
                    faceUvs.Add(uv);
                }
                
                if (faceVertices.Count < 3) continue;
                
                // Calculate normal
                Vector3 normal = Vector3.zero;
                if (faceVertices.Count >= 3)
                {
                    Vector3 v0 = faceVertices[0];
                    Vector3 v1 = faceVertices[1];
                    Vector3 v2 = faceVertices[2];
                    normal = Vector3.Cross(v1 - v0, v2 - v0).normalized;
                }
                
                int faceVertexStart = vertexOffset + addedVertices;
                
                // Add vertices
                for (int i = 0; i < faceVertices.Count; i++)
                {
                    vertices.Add(faceVertices[i]);
                    uvs.Add(faceUvs[i]);
                    normals.Add(normal);
                    colors.Add(Color.white);
                    addedVertices++;
                }
                
                // Get the correct submesh for this face's texture (mesh faces don't have texture info in the same way)
                int submeshIndex = textureToSubmesh.ContainsKey(-1) ? textureToSubmesh[-1] : 0;
                var triangles = trianglesPerSubmesh[submeshIndex];
                
                // Triangulate the face (fan triangulation)
                for (int i = 1; i < faceVertices.Count - 1; i++)
                {
                    // Reversed winding order for proper face culling in Unity
                    triangles.Add(faceVertexStart);
                    triangles.Add(faceVertexStart + i + 1);
                    triangles.Add(faceVertexStart + i);
                }
            }
            
            return addedVertices;
        }

        private static int BuildCubeElement(BlockbenchElement element, BlockbenchResolution resolution,
            List<Vector3> vertices, List<List<int>> trianglesPerSubmesh, List<Vector2> uvs, 
            List<Vector3> normals, List<Color> colors, int vertexOffset, Dictionary<int, int> textureToSubmesh)
        {
            if (element.from == null || element.to == null)
                return 0;
            
            float resWidth = resolution?.width ?? 16;
            float resHeight = resolution?.height ?? 16;
            
            Vector3 from = new Vector3(element.from[0], element.from[1], element.from[2]) * UnitScale;
            Vector3 to = new Vector3(element.to[0], element.to[1], element.to[2]) * UnitScale;
            
            // Get element origin and rotation
            Vector3 origin = Vector3.zero;
            if (element.origin != null && element.origin.Length >= 3)
            {
                origin = new Vector3(element.origin[0], element.origin[1], element.origin[2]) * UnitScale;
            }
            
            Quaternion rotation = Quaternion.identity;
            if (element.rotation != null && element.rotation.Length >= 3)
            {
                rotation = Quaternion.Euler(element.rotation[0], element.rotation[1], element.rotation[2]);
            }
            
            // Define cube corners
            Vector3[] corners = new Vector3[]
            {
                new Vector3(from.x, from.y, from.z), // 0: ---
                new Vector3(to.x, from.y, from.z),   // 1: +--
                new Vector3(to.x, to.y, from.z),     // 2: ++-
                new Vector3(from.x, to.y, from.z),   // 3: -+-
                new Vector3(from.x, from.y, to.z),   // 4: --+
                new Vector3(to.x, from.y, to.z),     // 5: +-+
                new Vector3(to.x, to.y, to.z),       // 6: +++
                new Vector3(from.x, to.y, to.z),     // 7: -++
            };
            
            // Apply rotation around origin
            for (int i = 0; i < 8; i++)
            {
                corners[i] = rotation * (corners[i] - origin) + origin;
            }
            
            // Define faces (name, vertices indices, normal)
            var faceDefinitions = new[]
            {
                ("north", new[] { 1, 0, 3, 2 }, Vector3.back),    // -Z
                ("south", new[] { 4, 5, 6, 7 }, Vector3.forward), // +Z
                ("east", new[] { 5, 1, 2, 6 }, Vector3.right),    // +X
                ("west", new[] { 0, 4, 7, 3 }, Vector3.left),     // -X
                ("up", new[] { 3, 7, 6, 2 }, Vector3.up),         // +Y
                ("down", new[] { 0, 1, 5, 4 }, Vector3.down),     // -Y
            };
            
            int addedVertices = 0;
            
            foreach (var (faceName, indices, normal) in faceDefinitions)
            {
                // Check if face exists and should be rendered
                if (element.faces == null || !element.faces.TryGetValue(faceName, out var face))
                    continue;
                
                Vector3 rotatedNormal = rotation * normal;
                
                // Get UVs
                float[] faceUv = face.uv;
                Vector2 uv0, uv1, uv2, uv3;
                
                if (faceUv != null && faceUv.Length >= 4)
                {
                    // UVs are in format [x1, y1, x2, y2]
                    float u1 = faceUv[0] / resWidth;
                    float v1 = 1f - (faceUv[1] / resHeight);
                    float u2 = faceUv[2] / resWidth;
                    float v2 = 1f - (faceUv[3] / resHeight);
                    
                    uv0 = new Vector2(u1, v2);
                    uv1 = new Vector2(u2, v2);
                    uv2 = new Vector2(u2, v1);
                    uv3 = new Vector2(u1, v1);
                    
                    // Apply UV rotation
                    if (face.rotation.HasValue)
                    {
                        int rot = face.rotation.Value / 90;
                        for (int r = 0; r < rot; r++)
                        {
                            var temp = uv0;
                            uv0 = uv1;
                            uv1 = uv2;
                            uv2 = uv3;
                            uv3 = temp;
                        }
                    }
                }
                else
                {
                    uv0 = new Vector2(0, 0);
                    uv1 = new Vector2(1, 0);
                    uv2 = new Vector2(1, 1);
                    uv3 = new Vector2(0, 1);
                }
                
                int startIndex = vertexOffset + addedVertices;
                
                // Add 4 vertices for this face
                vertices.Add(corners[indices[0]]);
                vertices.Add(corners[indices[1]]);
                vertices.Add(corners[indices[2]]);
                vertices.Add(corners[indices[3]]);
                
                uvs.Add(uv0);
                uvs.Add(uv1);
                uvs.Add(uv2);
                uvs.Add(uv3);
                
                normals.Add(rotatedNormal);
                normals.Add(rotatedNormal);
                normals.Add(rotatedNormal);
                normals.Add(rotatedNormal);
                
                colors.Add(Color.white);
                colors.Add(Color.white);
                colors.Add(Color.white);
                colors.Add(Color.white);
                
                // Get the correct submesh for this face's texture
                int texIndex = face.texture >= 0 ? face.texture : -1;
                int submeshIndex = textureToSubmesh.ContainsKey(texIndex) ? textureToSubmesh[texIndex] : 0;
                var triangles = trianglesPerSubmesh[submeshIndex];
                
                // Add triangles (two triangles per quad)
                triangles.Add(startIndex);
                triangles.Add(startIndex + 1);
                triangles.Add(startIndex + 2);
                
                triangles.Add(startIndex);
                triangles.Add(startIndex + 2);
                triangles.Add(startIndex + 3);
                
                addedVertices += 4;
            }
            
            return addedVertices;
        }
    }
}
