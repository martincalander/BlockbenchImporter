using System.IO;
using System.Linq;
using Essentials.BlockbenchImporter.Utils;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Essentials.BlockbenchImporter.Tests
{
    public sealed class BlockbenchImporterIntegrationTests
    {
        private const string Folder = "Assets/__BlockbenchImporterTests";
        private const string AssetPath = Folder + "/Integration.bbmodel";
        private const string ArmaturePath = Folder + "/Armature.bbmodel";
        private const string Model = @"{
          ""meta"":{""format_version"":""5.0"",""model_format"":""free"",""box_uv"":false},
          ""name"":""Integration Model"",""resolution"":{""width"":16,""height"":16},
          ""elements"":[{""name"":""body"",""type"":""cube"",""uuid"":""cube"",""from"":[-1,0,-1],""to"":[1,2,1],""origin"":[0,1,0],""faces"":{""north"":{""uv"":[0,0,2,2],""texture"":0},""south"":{""uv"":[0,0,2,2],""texture"":0}}}],
          ""groups"":[{""name"":""root"",""uuid"":""group"",""origin"":[0,0,0]}],
          ""outliner"":[{""name"":""root"",""uuid"":""group"",""children"":[""cube""]}],
          ""textures"":[{""name"":""orange"",""id"":""0"",""uuid"":""texture"",""width"":16,""height"":16,""uv_width"":16,""uv_height"":16,""render_sides"":""double"",""source"":""data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAABAAAAAQCAYAAAAf8/9hAAAAK0lEQVR4AeyTsQkAAAzCgsf2/w8svcGhi4KrQzDy4KQiTAegDMrgNPr/wQIAAP//u+MxzwAAAAZJREFUAwDd+iZhI2QuxgAAAABJRU5ErkJggg==""}],
          ""animations"":[{""name"":""turn"",""loop"":""loop"",""length"":1,""snapping"":24,""animators"":{""group"":{""name"":""root"",""type"":""bone"",""keyframes"":[{""channel"":""rotation"",""time"":0,""interpolation"":""linear"",""data_points"":[{""x"":""0"",""y"":""0"",""z"":""0""}]},{""channel"":""rotation"",""time"":1,""interpolation"":""linear"",""data_points"":[{""x"":""0"",""y"":""90"",""z"":""0""}]}]}}}]
        }";
        private const string ArmatureModel = @"{
          ""meta"":{""format_version"":""5.0"",""model_format"":""free"",""box_uv"":false},
          ""name"":""Armature"",""resolution"":{""width"":16,""height"":16},
          ""elements"":[
            {""name"":""rig"",""type"":""armature"",""uuid"":""armature"",""children"":[""bone"",""mesh0001""]},
            {""name"":""bone"",""type"":""armature_bone"",""uuid"":""bone"",""origin"":[0,0,0],""rotation"":[0,0,0],""children"":[],""vertex_weights"":{""a"":1,""b"":1,""c"":1}},
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
        public void TearDown() => AssetDatabase.DeleteAsset(Folder);

        [Test]
        public void Import_CreatesPrefabAndAllConfiguredSubAssets()
        {
            var root = AssetDatabase.LoadMainAssetAtPath(AssetPath) as GameObject;
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(AssetPath);

            Assert.That(root, Is.Not.Null);
            // Unity names a scripted importer's main object after the source file.
            Assert.That(root.name, Is.EqualTo("Integration"));
            Assert.That(root.transform.localScale, Is.EqualTo(Vector3.one / 16f));
            Assert.That(root.transform.Find("root/body"), Is.Not.Null);
            Assert.That(root.GetComponentsInChildren<MeshRenderer>(), Has.Length.EqualTo(1));
            Assert.That(assets.OfType<Mesh>().Count(), Is.EqualTo(1));
            Assert.That(assets.OfType<Texture2D>().Count(), Is.EqualTo(1));
            Assert.That(assets.OfType<Material>().Count(), Is.EqualTo(1));
            Assert.That(assets.OfType<AnimationClip>().Count(), Is.EqualTo(1));
            Assert.That(assets.OfType<AnimatorController>().Count(), Is.EqualTo(1));
            Assert.That(root.GetComponent<Animator>(), Is.Not.Null);
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
        }

        [Test]
        public void Reimport_PreservesSubAssetLocalIdentifiers()
        {
            string before = LocalIdSnapshot();
            AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            Assert.That(LocalIdSnapshot(), Is.EqualTo(before));
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
            Assert.That(renderer.bones, Has.Length.EqualTo(2));
            Assert.That(renderer.sharedMesh.boneWeights, Has.Length.EqualTo(3));
            Assert.That(renderer.sharedMesh.boneWeights.All(weight =>
                weight.boneIndex0 == 0 && Mathf.Approximately(weight.weight0, 1f)), Is.True);
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

        private static float EvaluateCurve(AnimationClip clip, string property, float time)
        {
            EditorCurveBinding binding = AnimationUtility.GetCurveBindings(clip).Single(candidate =>
                candidate.path == "root" && candidate.propertyName == property);
            return AnimationUtility.GetEditorCurve(clip, binding).Evaluate(time);
        }
    }
}
