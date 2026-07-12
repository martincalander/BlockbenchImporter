using System.Collections.Generic;
using Essentials.BlockbenchImporter.MeshBuilders;
using Essentials.BlockbenchImporter.Model;
using Essentials.BlockbenchImporter.Utils;
using NUnit.Framework;
using UnityEngine;

namespace Essentials.BlockbenchImporter.Tests
{
    public sealed class BlockbenchGeometryTests
    {
        [Test]
        public void CoordinateConversion_MirrorsXAndRotationHandedness()
        {
            Assert.That(CoordinateConverter.ConvertPositionRaw(2, 3, 4), Is.EqualTo(new Vector3(-2, 3, 4)));
            Quaternion expected = Quaternion.AngleAxis(10, Vector3.right) *
                                  Quaternion.AngleAxis(-20, Vector3.up) *
                                  Quaternion.AngleAxis(-30, Vector3.forward);
            Assert.That(Quaternion.Angle(CoordinateConverter.ConvertRotation(10, 20, 30), expected), Is.LessThan(0.001f));
        }

        [Test]
        public void CubeBuilder_AppliesInflateAndStretchAroundCenter()
        {
            var cube = new BbElement
            {
                name = "stretched",
                type = "cube",
                from = new[] { 0f, 0f, 0f },
                to = new[] { 2f, 4f, 6f },
                origin = new[] { 1f, 2f, 3f },
                inflate = 1f,
                stretch = new[] { 2f, 1f, 0.5f },
                faces = new Dictionary<string, BbFace>
                {
                    ["north"] = new BbFace { uv = new[] { 0f, 0f, 2f, 4f }, texture = 0 }
                }
            };

            CubeMeshResult result = CubeMeshBuilder.BuildCubeMesh(cube, new[] { 16, 16 }, 1);
            Assert.That(result.mesh.vertexCount, Is.EqualTo(4));
            Assert.That(result.mesh.bounds.size.x, Is.EqualTo(8f).Within(0.0001f));
            Assert.That(result.mesh.bounds.size.y, Is.EqualTo(6f).Within(0.0001f));
            Object.DestroyImmediate(result.mesh);
        }

        [Test]
        public void CubeBuilder_RespectsUvRotationAndNullFaces()
        {
            var cube = new BbElement
            {
                type = "cube",
                from = new[] { 0f, 0f, 0f },
                to = new[] { 1f, 1f, 1f },
                origin = new[] { 0f, 0f, 0f },
                faces = new Dictionary<string, BbFace>
                {
                    ["north"] = new BbFace { uv = new[] { 0f, 0f, 8f, 4f }, rotation = 90, texture = 0 },
                    ["south"] = new BbFace { enabled = false, texture = 0 }
                }
            };
            CubeMeshResult result = CubeMeshBuilder.BuildCubeMesh(cube, new[] { 16, 16 }, 1);
            Assert.That(result.mesh.vertexCount, Is.EqualTo(4));
            Assert.That(result.mesh.uv[0], Is.EqualTo(new Vector2(0.5f, 0.75f)));
            int[] triangles = result.mesh.triangles;
            Vector3[] vertices = result.mesh.vertices;
            Vector3 triangleNormal = Vector3.Cross(vertices[triangles[1]] - vertices[triangles[0]],
                vertices[triangles[2]] - vertices[triangles[0]]).normalized;
            Assert.That(Vector3.Dot(triangleNormal, result.mesh.normals[triangles[0]]), Is.GreaterThan(0.999f));
            Object.DestroyImmediate(result.mesh);
        }

        [Test]
        public void CubeBuilder_AllFaceWindingsMatchOutwardNormals()
        {
            var faces = new Dictionary<string, BbFace>();
            foreach (string face in new[] { "north", "south", "east", "west", "up", "down" })
                faces[face] = new BbFace { texture = 0 };
            var cube = new BbElement
            {
                type = "cube", from = new[] { 0f, 0f, 0f }, to = new[] { 1f, 1f, 1f },
                origin = new[] { 0f, 0f, 0f }, faces = faces
            };
            CubeMeshResult result = CubeMeshBuilder.BuildCubeMesh(cube, new[] { 16, 16 }, 1);
            int[] triangles = result.mesh.triangles;
            Vector3[] vertices = result.mesh.vertices;
            Vector3[] normals = result.mesh.normals;
            for (int index = 0; index < triangles.Length; index += 3)
            {
                Vector3 actual = Vector3.Cross(vertices[triangles[index + 1]] - vertices[triangles[index]],
                    vertices[triangles[index + 2]] - vertices[triangles[index]]).normalized;
                Assert.That(Vector3.Dot(actual, normals[triangles[index]]), Is.GreaterThan(0.999f));
            }
            Object.DestroyImmediate(result.mesh);
        }
    }
}
