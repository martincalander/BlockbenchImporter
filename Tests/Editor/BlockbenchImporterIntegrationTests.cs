using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MartinCalander.BlockbenchImporter.Utils;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.TestTools;

namespace MartinCalander.BlockbenchImporter.Tests
{
    public sealed class BlockbenchImporterIntegrationTests
    {
        private bool restorePlayModeSettings;
        private bool previousPlayModeOptionsEnabled;
        private EnterPlayModeOptions previousPlayModeOptions;
        private readonly List<Camera> temporarilyUntaggedCameras = new List<Camera>();

        private const string Folder = "Assets/__BlockbenchImporterTests";
        private const string AssetPath = Folder + "/Integration.bbmodel";
        private const string ArmaturePath = Folder + "/Armature.bbmodel";
        private const string Model = @"{
          ""meta"":{""format_version"":""5.0"",""model_format"":""free"",""box_uv"":false},
          ""name"":""Integration Model"",""resolution"":{""width"":16,""height"":16},
          ""elements"":[{""name"":""body"",""type"":""cube"",""uuid"":""cube"",""from"":[-1,0,-1],""to"":[1,2,1],""origin"":[0,1,0],""faces"":{""north"":{""uv"":[0,0,2,2],""texture"":0},""south"":{""uv"":[0,0,2,2],""texture"":0}}},{""name"":""hitbox"",""type"":""bounding_box"",""uuid"":""hitbox"",""from"":[-2,0,-2],""to"":[2,4,2],""origin"":[0,2,0]}],
          ""groups"":[{""name"":""root"",""uuid"":""group"",""origin"":[0,0,0]}],
          ""outliner"":[{""name"":""root"",""uuid"":""group"",""children"":[""cube"",""hitbox""]}],
          ""textures"":[{""name"":""orange"",""id"":""0"",""uuid"":""texture"",""width"":16,""height"":16,""uv_width"":16,""uv_height"":16,""render_sides"":""double"",""source"":""data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAABAAAAAQCAYAAAAf8/9hAAAAK0lEQVR4AeyTsQkAAAzCgsf2/w8svcGhi4KrQzDy4KQiTAegDMrgNPr/wQIAAP//u+MxzwAAAAZJREFUAwDd+iZhI2QuxgAAAABJRU5ErkJggg==""}],
          ""animations"":[{""name"":""turn"",""loop"":""loop"",""length"":1,""snapping"":24,""animators"":{""group"":{""name"":""root"",""type"":""bone"",""keyframes"":[{""channel"":""rotation"",""time"":0,""interpolation"":""linear"",""data_points"":[{""x"":""0"",""y"":""0"",""z"":""0""}]},{""channel"":""rotation"",""time"":1,""interpolation"":""linear"",""data_points"":[{""x"":""0"",""y"":""90"",""z"":""0""}]},{""channel"":""position"",""time"":0,""interpolation"":""linear"",""data_points"":[{""x"":""0"",""y"":""0"",""z"":""0""}]},{""channel"":""position"",""time"":1,""interpolation"":""linear"",""data_points"":[{""x"":""0"",""y"":""16"",""z"":""0""}]}]}}}]
        }";
        private const string ArmatureModel = @"{
          ""meta"":{""format_version"":""5.0"",""model_format"":""free"",""box_uv"":false},
          ""name"":""Armature"",""resolution"":{""width"":16,""height"":16},
          ""elements"":[
            {""name"":""rig"",""type"":""armature"",""uuid"":""armature"",""children"":[""bone_a"",""bone_b"",""mesh0001""]},
            {""name"":""bone a"",""type"":""armature_bone"",""uuid"":""bone_a"",""origin"":[0,0,0],""rotation"":[0,0,0],""children"":[],""vertex_weights"":{""a"":1,""c"":0.25}},
            {""name"":""bone b"",""type"":""armature_bone"",""uuid"":""bone_b"",""origin"":[0,0,0],""rotation"":[0,0,0],""children"":[],""vertex_weights"":{""b"":1,""c"":0.75}},
            {""name"":""triangle"",""type"":""mesh"",""uuid"":""mesh0001"",""origin"":[0,0,0],""vertices"":{""a"":[0,0,0],""b"":[1,0,0],""c"":[0,1,0]},""faces"":{""face"":{""vertices"":[""a"",""b"",""c""],""uv"":{""a"":[0,0],""b"":[1,0],""c"":[0,1]},""texture"":0}}}
          ],
          ""outliner"":[""armature""],
          ""textures"":[{""name"":""orange"",""id"":""0"",""uuid"":""texture"",""width"":16,""height"":16,""uv_width"":16,""uv_height"":16,""source"":""data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAABAAAAAQCAYAAAAf8/9hAAAAK0lEQVR4AeyTsQkAAAzCgsf2/w8svcGhi4KrQzDy4KQiTAegDMrgNPr/wQIAAP//u+MxzwAAAAZJREFUAwDd+iZhI2QuxgAAAABJRU5ErkJggg==""}]
        }";

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.DeleteAsset(Folder);
            AssetDatabase.CreateFolder("Assets", "__BlockbenchImporterTests");
            File.WriteAllText(AssetPath, Model);
            AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        }

        [TearDown]
        public void TearDown()
        {
            if (!restorePlayModeSettings)
                AssetDatabase.DeleteAsset(Folder);
        }

        [UnityTearDown]
        public IEnumerator RestorePlayModeSettings()
        {
            if (!restorePlayModeSettings)
                yield break;

            try
            {
                if (EditorApplication.isPlaying)
                    yield return new ExitPlayMode();
                RestoreMainCameraTags();
            }
            finally
            {
                BlockbenchImportHookTestPostprocessor.Reset();
                EditorSettings.enterPlayModeOptionsEnabled = previousPlayModeOptionsEnabled;
                EditorSettings.enterPlayModeOptions = previousPlayModeOptions;
                restorePlayModeSettings = false;
                AssetDatabase.DeleteAsset(Folder);
            }
        }

        [UnityTest]
        [Category("PlayModeLifecycle")]
        public IEnumerator ReimportAndBillboardsSurviveTwoPlaySessionsWithoutDomainReload()
        {
            previousPlayModeOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            previousPlayModeOptions = EditorSettings.enterPlayModeOptions;
            restorePlayModeSettings = true;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;

            int callbackCount = 0;
            BlockbenchImportHookTestPostprocessor.PathToHandle = AssetPath;
            BlockbenchImportHookTestPostprocessor.Callback = _ => callbackCount++;
            string identifiers = LocalIdSnapshot();
            Camera previousCamera = null;

            for (int cycle = 0; cycle < 2; cycle++)
            {
                AssetDatabase.ImportAsset(AssetPath,
                    ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                Assert.That(callbackCount, Is.EqualTo(cycle + 1),
                    "A reused importer must invoke its post-import callback once per import.");
                Assert.That(LocalIdSnapshot(), Is.EqualTo(identifiers));

                yield return new EnterPlayMode(false);
                Assert.That(previousCamera == null, Is.True,
                    "The previous Play session's camera must not survive scene reload.");

                foreach (Camera existingCamera in Camera.allCameras)
                {
                    if (!existingCamera.CompareTag("MainCamera"))
                        continue;
                    temporarilyUntaggedCameras.Add(existingCamera);
                    existingCamera.tag = "Untagged";
                }

                var cameraObject = new GameObject("Blockbench lifecycle camera");
                Camera camera = cameraObject.AddComponent<Camera>();
                cameraObject.tag = "MainCamera";
                cameraObject.transform.rotation = Quaternion.Euler(15f, cycle == 0 ? 35f : 140f, 0f);

                var billboardObject = new GameObject("Blockbench lifecycle billboard");
                var billboard = billboardObject.AddComponent<BlockbenchBillboard>();
                billboard.Mode = BlockbenchBillboard.FacingMode.CameraRotation;

                yield return null;
                yield return null;

                Assert.That(Camera.main, Is.SameAs(camera));
                Assert.That(Quaternion.Angle(billboard.transform.rotation, camera.transform.rotation),
                    Is.LessThan(0.001f), "The billboard must use this Play session's camera.");
                previousCamera = camera;
                RestoreMainCameraTags();
                yield return new ExitPlayMode();
                Assert.That(LocalIdSnapshot(), Is.EqualTo(identifiers));
            }
        }

        [Test]
        public void Import_CreatesPrefabAndAllConfiguredSubAssets()
        {
            var root = AssetDatabase.LoadMainAssetAtPath(AssetPath) as GameObject;
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(AssetPath);

            Assert.That(root, Is.Not.Null);
            // Unity names a scripted importer's main object after the source file.
            Assert.That(root.name, Is.EqualTo("Integration"));
            Assert.That(root.transform.localScale, Is.EqualTo(Vector3.one));
            Transform body = root.transform.Find("root/body");
            Assert.That(body, Is.Not.Null);
            Assert.That(body.localPosition.y, Is.EqualTo(1f / 16f).Within(0.000001f));
            Assert.That(root.GetComponentsInChildren<MeshRenderer>(), Has.Length.EqualTo(1));
            Assert.That(assets.OfType<Mesh>().Count(), Is.EqualTo(1));
            Assert.That(assets.OfType<Texture2D>().Count(), Is.EqualTo(1));
            Assert.That(assets.OfType<Material>().Count(), Is.EqualTo(1));
            Assert.That(assets.OfType<AnimationClip>().Count(), Is.EqualTo(1));
            Assert.That(assets.OfType<AnimatorController>().Count(), Is.EqualTo(1));
            Assert.That(root.GetComponent<Animator>(), Is.Not.Null);
            Assert.That(assets.OfType<Mesh>().Single().bounds.size.x,
                Is.EqualTo(2f / 16f).Within(0.000001f));
            BoxCollider collider = root.transform.Find("root/hitbox").GetComponent<BoxCollider>();
            Assert.That(collider.center, Is.EqualTo(Vector3.zero));
            Assert.That(collider.size, Is.EqualTo(Vector3.one * (4f / 16f)));
            Color pixel = assets.OfType<Texture2D>().Single().GetPixel(8, 8);
            Assert.That(pixel.r, Is.EqualTo(1f).Within(1f / 255f));
            Assert.That(pixel.g, Is.EqualTo(0.4f).Within(1f / 255f));
            Assert.That(pixel.b, Is.EqualTo(0f).Within(1f / 255f));
            Assert.That(pixel.a, Is.EqualTo(1f).Within(1f / 255f));
            Assert.That(AnimationUtility.GetAnimationClipSettings(assets.OfType<AnimationClip>().Single()).loopTime,
                Is.True);
            AnimationClip clip = assets.OfType<AnimationClip>().Single();
            var rotation = new Quaternion(
                EvaluateCurve(clip, "m_LocalRotation.x", 0.5f),
                EvaluateCurve(clip, "m_LocalRotation.y", 0.5f),
                EvaluateCurve(clip, "m_LocalRotation.z", 0.5f),
                EvaluateCurve(clip, "m_LocalRotation.w", 0.5f));
            Quaternion expected = CoordinateConverter.ConvertRotation(0f, 45f, 0f);
            Assert.That(Quaternion.Angle(rotation.normalized, expected), Is.LessThan(0.1f));
            Assert.That(EvaluateCurve(clip, "m_LocalPosition.y", 1f), Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void CustomImportScale_IsBakedWithoutScalingPrefabRoot()
        {
            var importer = AssetImporter.GetAtPath(AssetPath) as BlockbenchImporter;
            Assert.That(importer, Is.Not.Null);
            importer.ImportScale = 0.5f;
            importer.SaveAndReimport();

            var root = AssetDatabase.LoadMainAssetAtPath(AssetPath) as GameObject;
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(AssetPath);
            Assert.That(root, Is.Not.Null);
            Assert.That(root.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(root.transform.Find("root/body").localPosition.y,
                Is.EqualTo(0.5f).Within(0.000001f));
            Assert.That(assets.OfType<Mesh>().Single().bounds.size.x,
                Is.EqualTo(1f).Within(0.000001f));
            Assert.That(EvaluateCurve(assets.OfType<AnimationClip>().Single(), "m_LocalPosition.y", 1f),
                Is.EqualTo(8f).Within(0.0001f));
        }

        [Test]
        public void Reimport_PreservesSubAssetLocalIdentifiers()
        {
            string before = LocalIdSnapshot();
            AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            Assert.That(LocalIdSnapshot(), Is.EqualTo(before));
        }

        [Test]
        public void PostImportHook_CanCustomizeRootAndAddSubAsset()
        {
            var callbackCount = 0;
            var callbackSawGeneratedAssets = false;
            BlockbenchImportHookTestPostprocessor.PathToHandle = AssetPath;
            BlockbenchImportHookTestPostprocessor.Callback = args =>
            {
                callbackCount++;
                var generatedAssets = new List<Object>();
                args.context.GetObjects(generatedAssets);
                callbackSawGeneratedAssets = generatedAssets.OfType<Mesh>().Any() &&
                                             generatedAssets.OfType<Material>().Any() &&
                                             generatedAssets.OfType<AnimationClip>().Any();
                args.rootObject.AddComponent<BoxCollider>();
                var customData = new TextAsset("created by post-import hook") { name = "Custom Import Data" };
                args.context.AddObjectToAsset("custom_import_data", customData);
            };

            try
            {
                AssetDatabase.ImportAsset(AssetPath,
                    ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

                var root = AssetDatabase.LoadMainAssetAtPath(AssetPath) as GameObject;
                Object[] assets = AssetDatabase.LoadAllAssetsAtPath(AssetPath);
                Assert.That(callbackCount, Is.EqualTo(1));
                Assert.That(callbackSawGeneratedAssets, Is.True);
                Assert.That(root, Is.Not.Null);
                Assert.That(root.GetComponent<BoxCollider>(), Is.Not.Null);
                Assert.That(assets.OfType<TextAsset>().Single().text, Is.EqualTo("created by post-import hook"));
            }
            finally
            {
                BlockbenchImportHookTestPostprocessor.Reset();
            }
        }

        [Test]
        public void Import_ArmatureCreatesWeightedSkinnedMesh()
        {
            File.WriteAllText(ArmaturePath, ArmatureModel);
            AssetDatabase.ImportAsset(ArmaturePath,
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var root = AssetDatabase.LoadMainAssetAtPath(ArmaturePath) as GameObject;
            var renderer = root.GetComponentInChildren<SkinnedMeshRenderer>();
            Assert.That(renderer, Is.Not.Null);
            Assert.That(renderer.bones, Has.Length.EqualTo(3));
            Assert.That(renderer.sharedMesh.boneWeights, Has.Length.EqualTo(3));
            Assert.That(root.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(renderer.sharedMesh.bounds.size.x, Is.EqualTo(1f / 16f).Within(0.000001f));
            BoneWeight[] weights = renderer.sharedMesh.boneWeights;
            Assert.That(weights[0].boneIndex0, Is.EqualTo(0));
            Assert.That(weights[0].weight0, Is.EqualTo(1f).Within(0.000001f));
            Assert.That(weights[1].boneIndex0, Is.EqualTo(1));
            Assert.That(weights[1].weight0, Is.EqualTo(1f).Within(0.000001f));
            Assert.That(weights[2].boneIndex0, Is.EqualTo(1));
            Assert.That(weights[2].weight0, Is.EqualTo(0.75f).Within(0.000001f));
            Assert.That(weights[2].boneIndex1, Is.EqualTo(0));
            Assert.That(weights[2].weight1, Is.EqualTo(0.25f).Within(0.000001f));
        }

        [Test]
        public void ArmatureWeightsRemainScopedToEachMeshAndSourceVertex()
        {
            const string model = @"{
              ""meta"":{""format_version"":""5.0"",""model_format"":""free""},
              ""name"":""Shared vertices"",
              ""elements"":[
                {""name"":""rig"",""type"":""armature"",""uuid"":""rig"",""children"":[""bone0"",""bone1"",""bone2"",""bone3"",""bone4"",""bone5"",""first001"",""second01""]},
                {""name"":""bone0"",""type"":""armature_bone"",""uuid"":""bone0"",""vertex_weights"":{""a"":1,""first0:a"":0,""second:a"":9,""b"":-2,""c"":1}},
                {""name"":""bone1"",""type"":""armature_bone"",""uuid"":""bone1"",""vertex_weights"":{""a"":2,""b"":0,""c"":1}},
                {""name"":""bone2"",""type"":""armature_bone"",""uuid"":""bone2"",""vertex_weights"":{""a"":3}},
                {""name"":""bone3"",""type"":""armature_bone"",""uuid"":""bone3"",""vertex_weights"":{""a"":4}},
                {""name"":""bone4"",""type"":""armature_bone"",""uuid"":""bone4"",""vertex_weights"":{""a"":5}},
                {""name"":""bone5"",""type"":""armature_bone"",""uuid"":""bone5"",""vertex_weights"":{""a"":6}},
                {""name"":""first"",""type"":""mesh"",""uuid"":""first001"",
                 ""vertices"":{""a"":[0,0,0],""b"":[1,0,0],""c"":[0,1,0],""d"":[1,1,0]},
                 ""faces"":{""one"":{""vertices"":[""a"",""b"",""c""]},""two"":{""vertices"":[""a"",""c"",""d""]}}},
                {""name"":""second"",""type"":""mesh"",""uuid"":""second01"",
                 ""vertices"":{""a"":[0,0,0],""b"":[1,0,0],""c"":[0,1,0],""d"":[1,1,0]},
                 ""faces"":{""one"":{""vertices"":[""a"",""b"",""c""]},""two"":{""vertices"":[""a"",""c"",""d""]}}}
              ],
              ""outliner"":[""rig""]
            }";
            File.WriteAllText(ArmaturePath, model);
            AssetDatabase.ImportAsset(ArmaturePath,
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var root = AssetDatabase.LoadMainAssetAtPath(ArmaturePath) as GameObject;
            var first = root.transform.Find("rig/first").GetComponent<SkinnedMeshRenderer>();
            var second = root.transform.Find("rig/second").GetComponent<SkinnedMeshRenderer>();
            Assert.That(first.bones, Has.Length.EqualTo(7));
            Assert.That(second.bones, Has.Length.EqualTo(7));
            BoneWeight[] firstWeights = first.sharedMesh.boneWeights;
            BoneWeight[] secondWeights = second.sharedMesh.boneWeights;
            Assert.That(firstWeights, Has.Length.EqualTo(6));
            Assert.That(secondWeights, Has.Length.EqualTo(6));
            Assert.That(firstWeights[0], Is.EqualTo(firstWeights[3]),
                "Face copies of a source vertex must receive the same weights.");
            Assert.That(secondWeights[0], Is.EqualTo(secondWeights[3]));
            Assert.That(firstWeights[0].boneIndex0, Is.EqualTo(5));
            Assert.That(firstWeights[0].boneIndex1, Is.EqualTo(4));
            Assert.That(firstWeights[0].boneIndex2, Is.EqualTo(3));
            Assert.That(firstWeights[0].boneIndex3, Is.EqualTo(2));
            Assert.That(firstWeights[0].weight0, Is.EqualTo(6f / 18f).Within(0.000001f));
            Assert.That(firstWeights[0].weight3, Is.EqualTo(3f / 18f).Within(0.000001f));
            Assert.That(secondWeights[0].boneIndex0, Is.EqualTo(0),
                "A second mesh with the same source IDs must use its own prefixed weights.");
            Assert.That(secondWeights[0].boneIndex1, Is.EqualTo(5));
            Assert.That(secondWeights[0].boneIndex2, Is.EqualTo(4));
            Assert.That(secondWeights[0].boneIndex3, Is.EqualTo(3));
            Assert.That(secondWeights[0].weight0, Is.EqualTo(9f / 24f).Within(0.000001f));
            foreach (BoneWeight[] weights in new[] { firstWeights, secondWeights })
            {
                foreach (int index in new[] { 1, 5 })
                {
                    Assert.That(weights[index].boneIndex0, Is.EqualTo(6));
                    Assert.That(weights[index].weight0, Is.EqualTo(1f),
                        "Unweighted vertices must not inherit the previous vertex's influences.");
                }
                Assert.That(weights[2], Is.EqualTo(weights[4]));
                Assert.That(weights[2].boneIndex0, Is.EqualTo(0));
                Assert.That(weights[2].boneIndex1, Is.EqualTo(1));
                Assert.That(weights[2].weight0, Is.EqualTo(0.5f));
                Assert.That(weights[2].weight1, Is.EqualTo(0.5f));
            }
        }

        private static string LocalIdSnapshot()
        {
            return string.Join("|", AssetDatabase.LoadAllAssetsAtPath(AssetPath)
                .Where(asset => asset is GameObject || asset is Mesh || asset is Material ||
                                asset is Texture2D || asset is AnimationClip || asset is AnimatorController)
                .OrderBy(asset => asset.GetType().Name).ThenBy(asset => asset.name)
                .Select(asset =>
                {
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string _, out long id);
                    return asset.GetType().Name + ":" + asset.name + ":" + id;
                }));
        }

        private void RestoreMainCameraTags()
        {
            foreach (Camera camera in temporarilyUntaggedCameras)
            {
                if (camera != null)
                    camera.tag = "MainCamera";
            }
            temporarilyUntaggedCameras.Clear();
        }

        private static float EvaluateCurve(AnimationClip clip, string property, float time)
        {
            EditorCurveBinding binding = AnimationUtility.GetCurveBindings(clip).Single(candidate =>
                candidate.path == "root" && candidate.propertyName == property);
            return AnimationUtility.GetEditorCurve(clip, binding).Evaluate(time);
        }
    }

#if UNITY_6000_5_OR_NEWER
    // Tests own Reset() and keep this callback across Play mode transitions.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal sealed class BlockbenchImportHookTestPostprocessor : AssetPostprocessor
    {
        internal static string PathToHandle;
        internal static System.Action<BlockbenchImporter.ImportEventArgs> Callback;

        private void OnPreprocessAsset()
        {
            if (assetPath != PathToHandle || !(assetImporter is BlockbenchImporter importer))
                return;

            importer.OnPostBlockbenchImport -= HandlePostImport;
            importer.OnPostBlockbenchImport += HandlePostImport;
        }

        private static void HandlePostImport(BlockbenchImporter.ImportEventArgs args)
        {
            Callback?.Invoke(args);
        }

        internal static void Reset()
        {
            PathToHandle = null;
            Callback = null;
        }
    }
}
