using System.Collections.Generic;
using System.Linq;
using MartinCalander.BlockbenchImporter.Builders;
using MartinCalander.BlockbenchImporter.MeshBuilders;
using MartinCalander.BlockbenchImporter.Model;
using MartinCalander.BlockbenchImporter.Utils;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MartinCalander.BlockbenchImporter.Tests
{
    public sealed class BlockbenchGeometryTests
    {
        [Test]
        public void CoordinateConversion_MirrorsXAndRotationHandedness()
        {
            Assert.That(CoordinateConverter.ConvertPositionRaw(2, 3, 4), Is.EqualTo(new Vector3(-2, 3, 4)));
            Quaternion expected = Quaternion.AngleAxis(-30, Vector3.forward) *
                                  Quaternion.AngleAxis(-20, Vector3.up) *
                                  Quaternion.AngleAxis(10, Vector3.right);
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

        [Test]
        public void AnimationBuilder_GlobalRotationIsBakedRelativeToParentRestPose()
        {
            var parent = new GameObject("parent");
            var target = new GameObject("target");
            target.transform.SetParent(parent.transform, false);
            parent.transform.rotation = Quaternion.AngleAxis(30f, Vector3.up);
            AnimationClip clip = null;

            try
            {
                var animator = new BbAnimator { rotation_global = true };
                animator.keyframes.Add(RotationKeyframe(0f, 0f));
                animator.keyframes.Add(RotationKeyframe(1f, 90f));
                var animation = new BbAnimation { name = "global", length = 1f, snapping = 15 };
                animation.animators["target"] = animator;
                var model = new BbModel();
                model.animations.Add(animation);

                var builder = new AnimationBuilder(model,
                    new Dictionary<string, string> { ["target"] = "target" },
                    new Dictionary<string, Transform> { ["target"] = target.transform },
                    new BlockbenchImporterSettings());
                clip = builder.BuildAnimationClips().Single();

                Quaternion actual = new Quaternion(
                    EvaluateCurve(clip, "m_LocalRotation.x", 0.5f),
                    EvaluateCurve(clip, "m_LocalRotation.y", 0.5f),
                    EvaluateCurve(clip, "m_LocalRotation.z", 0.5f),
                    EvaluateCurve(clip, "m_LocalRotation.w", 0.5f));
                Quaternion expected = Quaternion.Inverse(parent.transform.rotation) *
                                      CoordinateConverter.ConvertAnimationRotation(new Vector3(0f, 45f, 0f));

                Assert.That(Quaternion.Angle(actual.normalized, expected), Is.LessThan(0.1f));
            }
            finally
            {
                if (clip != null) Object.DestroyImmediate(clip);
                Object.DestroyImmediate(parent);
            }
        }

        private static BbKeyframe RotationKeyframe(float time, float y)
        {
            return new BbKeyframe
            {
                channel = "rotation",
                time = time,
                data_points = new List<BbDataPoint>
                {
                    new BbDataPoint { x = "0", y = y.ToString(System.Globalization.CultureInfo.InvariantCulture), z = "0" }
                }
            };
        }

        private static float EvaluateCurve(AnimationClip clip, string property, float time)
        {
            EditorCurveBinding binding = AnimationUtility.GetCurveBindings(clip).Single(candidate =>
                candidate.path == "target" && candidate.propertyName == property);
            return AnimationUtility.GetEditorCurve(clip, binding).Evaluate(time);
        }
    }
}
