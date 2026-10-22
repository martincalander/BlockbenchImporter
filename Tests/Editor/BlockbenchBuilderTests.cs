using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MartinCalander.BlockbenchImporter.Builders;
using MartinCalander.BlockbenchImporter.Model;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MartinCalander.BlockbenchImporter.Tests
{
    public sealed class BlockbenchBuilderTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void FailedAnimationBuildReleasesCreatedClips(bool failAfterFirstClip)
        {
            string name = "FailedClip_" + Guid.NewGuid().ToString("N");
            var animator = new BbAnimator();
            animator.keyframes.Add(new BbKeyframe { channel = "position" });
            var animation = new BbAnimation { name = name };
            animation.animators["target"] = animator;
            var model = new BbModel();
            model.animations.Add(animation);
            if (failAfterFirstClip) model.animations.Add(null);
            var builder = new AnimationBuilder(model,
                new Dictionary<string, string> { ["target"] = "target" },
                new Dictionary<string, Transform>(), failAfterFirstClip ? new BlockbenchImporterSettings() : null);
            Assert.Throws<NullReferenceException>(() => builder.BuildAnimationClips());
            Assert.That(Resources.FindObjectsOfTypeAll<AnimationClip>().Where(clip => clip.name == name), Is.Empty);
        }

        [Test]
        public void FailedControllerConstructionReleasesOwnedObjectsAndKeepsInputClips()
        {
            var clip = new AnimationClip { name = "ControllerInput_" + Guid.NewGuid().ToString("N") };
            var existing = new HashSet<Object>(Resources.FindObjectsOfTypeAll<Object>());
            try
            {
                Assert.Throws<NullReferenceException>(() => CreateAnimationBuilder(new BbModel())
                    .BuildAnimatorController(new List<AnimationClip> { clip, null }, "Assets/Failure.bbmodel"));
                Assert.That(clip != null, Is.True, "The caller still owns its input clips.");
                Assert.That(Resources.FindObjectsOfTypeAll<Object>().Where(value => !existing.Contains(value) &&
                    (value is AnimatorController || value is AnimatorStateMachine || value is AnimatorState)), Is.Empty);
            }
            finally { Object.DestroyImmediate(clip); }
        }

        [TestCase("position", "m_LocalPosition.x", -0.5f)]
        [TestCase("scale", "m_LocalScale.x", 1f)]
        public void BezierCurvesPreserveValuesTangentsAndWeights(string channel, string property, float scale)
        {
            var animator = new BbAnimator();
            for (int index = 0; index < 3; index++)
            {
                animator.keyframes.Add(new BbKeyframe
                {
                    channel = channel,
                    time = index,
                    interpolation = "bezier",
                    bezier_left_time = new[] { -0.25f, -0.25f, -0.25f },
                    bezier_right_time = new[] { 0.25f, 0.25f, 0.25f },
                    bezier_left_value = new[] { -1f, -1f, -1f },
                    bezier_right_value = new[] { 2f, 2f, 2f },
                    data_points = new List<BbDataPoint>
                    {
                        new BbDataPoint { x = (index + 1).ToString(), y = "1", z = "1" }
                    }
                });
            }
            var animation = new BbAnimation { name = "Bezier", loop = "loop", snapping = 30 };
            animation.animators["target"] = animator;
            var model = new BbModel();
            model.animations.Add(animation);
            var builder = CreateAnimationBuilder(model, 0.5f);
            List<AnimationClip> clips = builder.BuildAnimationClips();
            try
            {
                AnimationClip clip = clips.Single();
                EditorCurveBinding binding = AnimationUtility.GetCurveBindings(clip)
                    .Single(candidate => candidate.propertyName == property);
                AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                Assert.That(curve.length, Is.EqualTo(3));
                Keyframe middle = curve[1];
                Assert.That(middle.time, Is.EqualTo(1f));
                Assert.That(middle.value, Is.EqualTo(2f * scale));
                Assert.That(middle.inTangent, Is.EqualTo(4f * scale).Within(0.00001f));
                Assert.That(middle.outTangent, Is.EqualTo(8f * scale).Within(0.00001f));
                Assert.That(middle.inWeight, Is.EqualTo(0.25f).Within(0.00001f));
                Assert.That(middle.outWeight, Is.EqualTo(0.25f).Within(0.00001f));
                Assert.That(middle.weightedMode, Is.EqualTo(WeightedMode.Both));
                Assert.That(AnimationUtility.GetKeyLeftTangentMode(curve, 1),
                    Is.EqualTo(AnimationUtility.TangentMode.Free));
                Assert.That(AnimationUtility.GetKeyRightTangentMode(curve, 1),
                    Is.EqualTo(AnimationUtility.TangentMode.Free));
                Assert.That(AnimationUtility.GetKeyBroken(curve, 1), Is.True,
                    "Incoming and outgoing Bezier handles must retain their independent slopes.");
                Assert.That(clip.frameRate, Is.EqualTo(30f));
                Assert.That(AnimationUtility.GetAnimationClipSettings(clip).loopTime, Is.True);
            }
            finally
            {
                foreach (AnimationClip clip in clips)
                    Object.DestroyImmediate(clip);
            }
        }

        [TestCase(TextureFormat.RGBA32, -1, false)]
        [TestCase(TextureFormat.RGBA32, 0, true)]
        [TestCase(TextureFormat.RGBA32, 4, true)]
        [TestCase(TextureFormat.RGBA32, 1, true)]
        [TestCase(TextureFormat.ARGB32, 4, true)]
        [TestCase(TextureFormat.ARGB32, 1, true)]
        [TestCase(TextureFormat.RGB24, 0, false)]
        public void TransparencyScanFindsIsolatedTransparentPixels(
            TextureFormat format, int transparentPixel, bool expected)
        {
            var texture = new Texture2D(20, 20, format, false);
            try
            {
                var pixels = new Color32[400];
                for (int index = 0; index < pixels.Length; index++)
                    pixels[index] = new Color32(32, 64, 96, 255);
                if (transparentPixel >= 0)
                    pixels[transparentPixel].a = 0;
                texture.SetPixels32(pixels);
                texture.Apply(false, false);
                Assert.That(CreateTransparencyCheck()(texture), Is.EqualTo(expected));
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void TransparencyScanDoesNotReadUnreadableTextures()
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                texture.Apply(false, true);
                Assert.That(CreateTransparencyCheck()(texture), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        [TestCase("empty")]
        [TestCase("unsupported")]
        [TestCase("missing-target")]
        public void OmittedAnimationsDoNotRetainNativeClips(string kind)
        {
            string name = "OmittedAnimation_" + Guid.NewGuid().ToString("N");
            var animation = new BbAnimation { name = name };
            if (kind != "empty")
            {
                var animator = new BbAnimator();
                animator.keyframes.Add(new BbKeyframe { channel = kind == "unsupported" ? "sound" : "position" });
                animation.animators[kind == "missing-target" ? "missing" : "target"] = animator;
            }
            var model = new BbModel();
            model.animations.Add(animation);
            var existing = new HashSet<AnimationClip>(Resources.FindObjectsOfTypeAll<AnimationClip>());
            try
            {
                var builder = CreateAnimationBuilder(model);
                for (int import = 0; import < 2; import++)
                {
                    if (kind == "missing-target")
                        LogAssert.Expect(LogType.Warning, "Animation references unknown node UUID: missing");
                    Assert.That(builder.BuildAnimationClips(), Is.Empty);
                }
                Assert.That(Resources.FindObjectsOfTypeAll<AnimationClip>()
                    .Where(clip => !existing.Contains(clip) && clip.name == name), Is.Empty,
                    "Skipped animations must not leave unregistered native clips behind.");
            }
            finally
            {
                foreach (AnimationClip clip in Resources.FindObjectsOfTypeAll<AnimationClip>())
                    if (!existing.Contains(clip) && clip.name == name)
                        Object.DestroyImmediate(clip);
            }
        }

        [TestCase("position")]
        [TestCase("rotation")]
        [TestCase("scale")]
        public void RecognizedChannelsWithEmptyDataStillProduceAClip(string channel)
        {
            var animator = new BbAnimator();
            animator.keyframes.Add(new BbKeyframe { channel = channel });
            var animation = new BbAnimation { name = "Empty data", snapping = 20 };
            animation.animators["target"] = animator;
            var model = new BbModel();
            model.animations.Add(animation);
            List<AnimationClip> clips = CreateAnimationBuilder(model).BuildAnimationClips();
            try
            {
                Assert.That(clips, Has.Count.EqualTo(1));
                Assert.That(clips[0].name, Is.EqualTo("Empty data"));
                Assert.That(clips[0].frameRate, Is.EqualTo(20f));
            }
            finally
            {
                foreach (AnimationClip clip in clips)
                    Object.DestroyImmediate(clip);
            }
        }

        private static AnimationBuilder CreateAnimationBuilder(BbModel model, float importScale = 1f)
        {
            return new AnimationBuilder(model,
                new Dictionary<string, string> { ["target"] = "target" },
                new Dictionary<string, Transform>(),
                new BlockbenchImporterSettings { importScale = importScale });
        }

        private static Func<Texture2D, bool> CreateTransparencyCheck()
        {
            var builder = new MaterialBuilder(new BbModel(), "Assets/Transparency.bbmodel",
                new BlockbenchImporterSettings());
            MethodInfo method = typeof(MaterialBuilder).GetMethod("HasTransparency",
                BindingFlags.Instance | BindingFlags.NonPublic);
            return (Func<Texture2D, bool>)Delegate.CreateDelegate(typeof(Func<Texture2D, bool>), builder, method);
        }
    }
}
