using System.Collections.Generic;
using Essentials.BlockbenchImporter.Model;
using UnityEngine;

namespace Essentials.BlockbenchImporter.MeshBuilders
{
    internal static class BillboardMeshBuilder
    {
        public static CubeMeshResult Build(BbElement element, int[] modelResolution,
            IReadOnlyList<BbTexture> textures)
        {
            if (!element.faces.TryGetValue("front", out BbFace face) || !face.enabled)
                return new CubeMeshResult();

            float width = element.size != null && element.size.Length > 0 ? element.size[0] : 2f;
            float height = element.size != null && element.size.Length > 1 ? element.size[1] : 2f;
            float offsetX = element.offset != null && element.offset.Length > 0 ? element.offset[0] : 0f;
            float offsetY = element.offset != null && element.offset.Length > 1 ? element.offset[1] : 0f;
            float left = offsetX - width * 0.5f;
            float right = offsetX + width * 0.5f;
            float bottom = offsetY - height * 0.5f;
            float top = offsetY + height * 0.5f;

            int textureWidth = modelResolution != null && modelResolution.Length > 0 ? modelResolution[0] : 16;
            int textureHeight = modelResolution != null && modelResolution.Length > 1 ? modelResolution[1] : 16;
            if (face.texture >= 0 && textures != null && face.texture < textures.Count)
            {
                textureWidth = Mathf.Max(1, textures[face.texture].uv_width);
                textureHeight = Mathf.Max(1, textures[face.texture].uv_height);
            }
            float u0 = face.uv[0] / textureWidth;
            float v0 = 1f - face.uv[1] / textureHeight;
            float u1 = face.uv[2] / textureWidth;
            float v1 = 1f - face.uv[3] / textureHeight;
            var baseUvs = new[]
            {
                new Vector2(u0, v1), new Vector2(u1, v1),
                new Vector2(u1, v0), new Vector2(u0, v0)
            };
            var uvs = new Vector2[4];
            int rotation = ((face.rotation % 360) + 360) % 360 / 90;
            for (int index = 0; index < 4; index++) uvs[index] = baseUvs[(index + rotation) % 4];

            var mesh = new Mesh { name = element.name ?? "Billboard" };
            mesh.vertices = new[]
            {
                new Vector3(-left, bottom, 0), new Vector3(-right, bottom, 0),
                new Vector3(-right, top, 0), new Vector3(-left, top, 0)
            };
            mesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            mesh.uv = uvs;
            mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            mesh.RecalculateBounds();
            return new CubeMeshResult
            {
                mesh = mesh,
                textureIndices = new[] { face.texture < 0 ? -1 - Mathf.Abs(element.color % 10) : face.texture }
            };
        }
    }
}
