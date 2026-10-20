using System.Collections.Generic;
using System.IO;
using MartinCalander.BlockbenchImporter.MeshBuilders;
using MartinCalander.BlockbenchImporter.Model;
using MartinCalander.BlockbenchImporter.Utils;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MartinCalander.BlockbenchImporter.Tests
{
    public sealed class BlockbenchGeometryRegressionTests
    {
        [Test]
        public void RotationUsesBlockbenchZyxOrderBeforeReflectingCoordinates()
        {
            // Applying X, then Y, then Z rotations of 90 degrees in Blockbench
            // moves [2,3,5] to [5,3,-2]. Reflect the input and output for Unity.
            Vector3 actual = CoordinateConverter.ConvertRotation(90f, 90f, 90f) *
                             new Vector3(-2f, 3f, 5f);
            AssertVector(actual, new Vector3(-5f, 3f, -2f));
            AssertVector(CoordinateConverter.ConvertDirection(new Vector3(1f, 2f, 3f)),
                new Vector3(-1f, 2f, 3f));
        }

        [Test]
        public void MeshNormalsAndTrianglesKeepTheSourceFrontAfterReflection()
        {
            var element = Polygon(new[]
            {
                new[] { 0f, 0f, 0f }, new[] { 2f, 0f, 0f }, new[] { 0f, 2f, 0f }
            });
            element.origin = new[] { 10f, 20f, 30f };
            CubeMeshResult result = MeshElementBuilder.BuildMeshElement(element, new[] { 16, 16 }, 0);
            try
            {
                // Mesh vertices are local to the pivot, unlike cube from/to.
                AssertVector(result.mesh.vertices[1], new Vector3(-2f, 0f, 0f));
                foreach (Vector3 normal in result.mesh.normals)
                    AssertVector(normal, Vector3.forward);
                AssertTrianglesFace(result.mesh, Vector3.forward);
            }
            finally
            {
                Object.DestroyImmediate(result.mesh);
            }
        }

        [Test]
        public void ConcavePolygonKeepsItsAreaAndDoesNotFillTheNotch()
        {
            var element = Polygon(new[]
            {
                new[] { 0f, 0f, 0f }, new[] { 2f, 0f, 0f },
                new[] { 2f, 1f, 0f }, new[] { 1f, 1f, 0f },
                new[] { 1f, 2f, 0f }, new[] { 0f, 2f, 0f }
            });
            CubeMeshResult result = MeshElementBuilder.BuildMeshElement(element, new[] { 16, 16 }, 0);
            try
            {
                int[] triangles = result.mesh.triangles;
                Vector3[] vertices = result.mesh.vertices;
                Assert.That(triangles.Length, Is.EqualTo(12));
                float area = 0f;
                for (int index = 0; index < triangles.Length; index += 3)
                {
                    Vector3 a = vertices[triangles[index]];
                    Vector3 b = vertices[triangles[index + 1]];
                    Vector3 c = vertices[triangles[index + 2]];
                    area += Vector3.Cross(b - a, c - a).magnitude * 0.5f;
                    Vector3 center = (a + b + c) / 3f;
                    Assert.That(-center.x <= 1f || center.y <= 1f, Is.True,
                        "A triangle must not cover the concave notch.");
                }
                Assert.That(area, Is.EqualTo(3f).Within(0.0001f));
                AssertTrianglesFace(result.mesh, Vector3.forward);
            }
            finally
            {
                Object.DestroyImmediate(result.mesh);
            }
        }

        [Test]
        public void DiscardedFaceDoesNotShiftSourceVertexIdsForSkinning()
        {
            var element = Polygon(new[]
            {
                new[] { 0f, 0f, 0f }, new[] { 1f, 0f, 0f }, new[] { 0f, 1f, 0f }
            });
            BbMeshFace validFace = element.meshFaces["face"];
            element.meshFaces = new Dictionary<string, BbMeshFace>
            {
                ["missing-vertex"] = new BbMeshFace
                {
                    vertices = new List<string> { "0", "1", "missing" }
                },
                ["valid"] = validFace
            };
            CubeMeshResult result = MeshElementBuilder.BuildMeshElement(element, new[] { 16, 16 }, 0);
            try
            {
                Assert.That(result.sourceVertexIds, Is.EqualTo(new[] { "0", "1", "2" }));
                Assert.That(result.sourceVertexIds.Length, Is.EqualTo(result.mesh.vertexCount));
            }
            finally
            {
                Object.DestroyImmediate(result.mesh);
            }
        }

        [Test]
        public void MeshTriangulationScratchDoesNotLeakBetweenDifferentFaceSizes()
        {
            var element = Polygon(new[]
            {
                new[] { 0f, 0f, 0f }, new[] { 2f, 0f, 0f },
                new[] { 2f, 1f, 0f }, new[] { 1f, 1f, 0f },
                new[] { 1f, 2f, 0f }, new[] { 0f, 2f, 0f }
            });
            var faces = new[]
            {
                element.meshFaces["face"],
                new BbMeshFace { vertices = new List<string> { "0", "1", "3" } },
                new BbMeshFace { vertices = new List<string> { "0", "1", "2", "3" } }
            };
            var expectedVertices = new List<Vector3>();
            var expectedNormals = new List<Vector3>();
            var expectedUvs = new List<Vector2>();
            var expectedTriangles = new List<int>();
            var expectedSourceIds = new List<string>();
            var combinedFaces = new Dictionary<string, BbMeshFace>();
            for (int index = 0; index < faces.Length; index++)
            {
                element.meshFaces = new Dictionary<string, BbMeshFace> { ["face"] = faces[index] };
                CubeMeshResult isolated = MeshElementBuilder.BuildMeshElement(element, new[] { 16, 16 }, 0);
                try
                {
                    int offset = expectedVertices.Count;
                    expectedVertices.AddRange(isolated.mesh.vertices);
                    expectedNormals.AddRange(isolated.mesh.normals);
                    expectedUvs.AddRange(isolated.mesh.uv);
                    expectedSourceIds.AddRange(isolated.sourceVertexIds);
                    foreach (int vertex in isolated.mesh.triangles) expectedTriangles.Add(vertex + offset);
                }
                finally
                {
                    Object.DestroyImmediate(isolated.mesh);
                }
                combinedFaces[index.ToString()] = faces[index];
            }

            element.meshFaces = combinedFaces;
            for (int build = 0; build < 2; build++)
            {
                CubeMeshResult combined = MeshElementBuilder.BuildMeshElement(element, new[] { 16, 16 }, 0);
                try
                {
                    Assert.That(combined.mesh.vertices, Is.EqualTo(expectedVertices));
                    Assert.That(combined.mesh.normals, Is.EqualTo(expectedNormals));
                    Assert.That(combined.mesh.uv, Is.EqualTo(expectedUvs));
                    Assert.That(combined.mesh.triangles, Is.EqualTo(expectedTriangles));
                    Assert.That(combined.sourceVertexIds, Is.EqualTo(expectedSourceIds));
                }
                finally
                {
                    Object.DestroyImmediate(combined.mesh);
                }
            }
        }

        [Test]
        public void NestedArmatureOriginsAndBindPosesPreserveRestGeometryAndDeformWeightedVertices()
        {
            const string model = @"{
              ""meta"":{""format_version"":""5.0"",""model_format"":""free""},
              ""groups"":[{""uuid"":""group"",""name"":""group"",""origin"":[10,0,0],""rotation"":[0,0,90]}],
              ""elements"":[
                {""type"":""armature"",""uuid"":""rig"",""name"":""rig"",""children"":[""parent"",""mesh0001""]},
                {""type"":""armature_bone"",""uuid"":""parent"",""name"":""parent"",""origin"":[0,4,0],""children"":[""child""]},
                {""type"":""armature_bone"",""uuid"":""child"",""name"":""child"",""origin"":[0,2,0],""vertex_weights"":{""mesh00:a"":1,""mesh00:b"":1,""mesh00:c"":1}},
                {""type"":""mesh"",""uuid"":""mesh0001"",""name"":""surface"",""origin"":[3,0,0],
                  ""vertices"":{""a"":[0,0,0],""b"":[2,0,0],""c"":[2,2,0],""d"":[0,2,0]},
                  ""faces"":{
                    ""first"":{""vertices"":[""a"",""b"",""c""]},
                    ""second"":{""vertices"":[""a"",""c"",""d""]}
                  }}
              ],
              ""outliner"":[{""uuid"":""group"",""children"":[""rig""]}]
            }";
            string folder = null;
            GameObject root = null;
            var baked = new Mesh();
            try
            {
                root = Object.Instantiate(ImportModel(model, 0.5f, out folder));
                AssertVector(root.transform.Find("group").localPosition, new Vector3(-5f, 0f, 0f));
                AssertVector(root.transform.Find("group/rig").localPosition, Vector3.zero);
                AssertVector(root.transform.Find("group/rig/parent").localPosition, new Vector3(0f, 2f, 0f));
                Transform child = root.transform.Find("group/rig/parent/child");
                AssertVector(child.localPosition, new Vector3(0f, 1f, 0f));
                AssertVector(root.transform.Find("group/rig/surface").localPosition, new Vector3(-1.5f, 0f, 0f));

                SkinnedMeshRenderer renderer = root.GetComponentInChildren<SkinnedMeshRenderer>();
                Assert.That(renderer, Is.Not.Null);
                Vector3[] restVertices = renderer.sharedMesh.vertices;
                renderer.BakeMesh(baked);
                for (int index = 0; index < restVertices.Length; index++)
                    AssertVector(baked.vertices[index], restVertices[index]);

                child.localPosition += new Vector3(0f, 0.5f, 0f);
                renderer.BakeMesh(baked);
                Vector3[] deformed = baked.vertices;
                for (int index = 0; index < deformed.Length; index++)
                {
                    bool unweighted = restVertices[index] == new Vector3(0f, 1f, 0f);
                    Vector3 offset = unweighted ? Vector3.zero : new Vector3(0f, 0.5f, 0f);
                    AssertVector(deformed[index], restVertices[index] + offset);
                }
            }
            finally
            {
                Object.DestroyImmediate(baked);
                if (root != null) Object.DestroyImmediate(root);
                if (folder != null) AssetDatabase.DeleteAsset(folder);
            }
        }

        [Test]
        public void CubeRescalePreservesPerpendicularSizeForFortyFiveDegreeRotation()
        {
            const string model = @"{
              ""meta"":{""format_version"":""5.0"",""model_format"":""java_block""},
              ""elements"":[{""type"":""cube"",""uuid"":""cube"",""name"":""cube"",
                ""rotation"":[0,45,0],""rescale"":true,""faces"":{""north"":{""uv"":[0,0,16,16]}}}],
              ""outliner"":[""cube""]
            }";
            string folder = null;
            try
            {
                GameObject root = ImportModel(model, CoordinateConverter.DefaultImportScale, out folder);
                AssertVector(root.transform.Find("cube").localScale,
                    new Vector3(Mathf.Sqrt(2f), 1f, Mathf.Sqrt(2f)));
            }
            finally
            {
                if (folder != null) AssetDatabase.DeleteAsset(folder);
            }
        }

        [Test]
        public void BillboardTextureAndOffsetRemainScreenRightInEveryFacingMode()
        {
            const string model = @"{
              ""meta"":{""format_version"":""5.0"",""model_format"":""free""},
              ""resolution"":{""width"":16,""height"":16},
              ""elements"":[{""type"":""billboard"",""uuid"":""card"",""name"":""card"",
                ""size"":[2,2],""offset"":[0.25,0],""faces"":{""front"":{""uv"":[0,0,16,16]}}}],
              ""outliner"":[""card""]
            }";
            string folder = null;
            GameObject root = null;
            var cameraObject = new GameObject("Billboard orientation camera");
            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.aspect = 1f;
                camera.transform.position = new Vector3(-3f, 2f, -10f);
                camera.transform.rotation = Quaternion.LookRotation(-camera.transform.position, Vector3.up);
                root = Object.Instantiate(ImportModel(model, 1f, out folder));
                BlockbenchBillboard billboard = root.GetComponentInChildren<BlockbenchBillboard>();
                Mesh mesh = billboard.GetComponent<MeshFilter>().sharedMesh;
                foreach (BlockbenchBillboard.FacingMode mode in System.Enum.GetValues(typeof(BlockbenchBillboard.FacingMode)))
                {
                    billboard.Mode = mode;
                    billboard.SendMessage("Face", camera, SendMessageOptions.RequireReceiver);

                    Vector3 left = Vector3.zero;
                    Vector3 right = Vector3.zero;
                    Vector3 center = Vector3.zero;
                    Vector3[] vertices = mesh.vertices;
                    Vector2[] uvs = mesh.uv;
                    for (int index = 0; index < vertices.Length; index++)
                    {
                        Vector3 world = billboard.transform.TransformPoint(vertices[index]);
                        Vector3 screen = camera.WorldToViewportPoint(world);
                        if (uvs[index].x == 0f) left += screen * 0.5f;
                        else right += screen * 0.5f;
                        center += world / vertices.Length;
                    }
                    Assert.That(left.x, Is.LessThan(right.x), mode + " must not mirror the image.");
                    Assert.That(camera.WorldToViewportPoint(center).x,
                        Is.GreaterThan(camera.WorldToViewportPoint(billboard.transform.position).x),
                        mode + " must keep a positive horizontal offset on screen-right.");

                    Vector3 normal = billboard.transform.TransformDirection(mesh.normals[0]);
                    Assert.That(Vector3.Dot(normal, camera.transform.position - center), Is.GreaterThan(0f),
                        mode + " must face its front toward the camera.");
                    AssertTrianglesFace(mesh, Vector3.back);
                }
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
                if (root != null) Object.DestroyImmediate(root);
                if (folder != null) AssetDatabase.DeleteAsset(folder);
            }
        }

        private static GameObject ImportModel(string model, float importScale, out string folder)
        {
            folder = AssetDatabase.GenerateUniqueAssetPath("Assets/__BlockbenchGeometryRegressionTests");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            string path = folder + "/Geometry.bbmodel";
            File.WriteAllText(path, model);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var importer = (BlockbenchImporter)AssetImporter.GetAtPath(path);
            if (!Mathf.Approximately(importer.ImportScale, importScale))
            {
                importer.ImportScale = importScale;
                importer.SaveAndReimport();
            }
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(root, Is.Not.Null);
            return root;
        }

        private static BbElement Polygon(float[][] points)
        {
            var element = new BbElement
            {
                type = "mesh",
                vertices = new Dictionary<string, float[]>(),
                meshFaces = new Dictionary<string, BbMeshFace>()
            };
            var face = new BbMeshFace();
            for (int index = 0; index < points.Length; index++)
            {
                string key = index.ToString();
                element.vertices[key] = points[index];
                face.vertices.Add(key);
            }
            element.meshFaces["face"] = face;
            return element;
        }

        private static void AssertTrianglesFace(Mesh mesh, Vector3 expectedNormal)
        {
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            for (int index = 0; index < triangles.Length; index += 3)
            {
                Vector3 normal = Vector3.Cross(vertices[triangles[index + 1]] - vertices[triangles[index]],
                    vertices[triangles[index + 2]] - vertices[triangles[index]]).normalized;
                AssertVector(normal, expectedNormal);
            }
        }

        private static void AssertVector(Vector3 actual, Vector3 expected)
        {
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(0.0001f),
                "Expected " + expected + ", got " + actual);
        }
    }
}
