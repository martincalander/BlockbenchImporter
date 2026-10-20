using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MartinCalander.BlockbenchImporter.Builders;
using MartinCalander.BlockbenchImporter.Model;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MartinCalander.BlockbenchImporter.Tests
{
    public sealed class BlockbenchAnimationTests
    {
        [TestCase("once", WrapMode.Once, false)]
        [TestCase("loop", WrapMode.Loop, true)]
        [TestCase("hold", WrapMode.ClampForever, false)]
        public void ClipsPreservePlaybackMode(string mode, WrapMode expectedWrap, bool expectedLoop)
        {
            var animator = new BbAnimator();
            animator.keyframes.Add(Key("position", 0f, 0f));
            var model = Model(animator, 1f);
            model.animations[0].loop = mode;
            AnimationClip clip = Build(model);
            try
            {
                Assert.That(clip.wrapMode, Is.EqualTo(expectedWrap));
                Assert.That(AnimationUtility.GetAnimationClipSettings(clip).loopTime, Is.EqualTo(expectedLoop));
            }
            finally { Object.DestroyImmediate(clip); }
        }

        [TestCase("position", "m_LocalPosition.x", -1f)]
        [TestCase("scale", "m_LocalScale.x", 1f)]
        public void ScalarClipsRetainDeclaredDurationAndHoldTheLastValue(string channel, string property, float sign)
        {
            WithClip(channel, new[] { Key(channel, 0f, 1f), Key(channel, 0.5f, 3f) }, 2f, clip =>
            {
                Assert.That(clip.length, Is.EqualTo(2f).Within(0.00001f));
                Assert.That(Curve(clip, property).Evaluate(1.5f), Is.EqualTo(3f * sign).Within(0.00001f));
            });
        }

        [TestCase("position", "m_LocalPosition.x", -1f)]
        [TestCase("scale", "m_LocalScale.x", 1f)]
        public void SplitKeysUseIncomingValueAtTheKeyAndOutgoingValueAfterIt(string channel, string property, float sign)
        {
            BbKeyframe split = Key(channel, 1f, 10f);
            split.data_points.Add(Point(20f));
            WithClip(channel, new[] { Key(channel, 0f, 2f), split, Key(channel, 2f, 30f) }, 2f, clip =>
            {
                AnimationCurve curve = Curve(clip, property);
                Assert.That(curve.Evaluate(0.5f), Is.EqualTo(6f * sign).Within(0.001f));
                Assert.That(curve.Evaluate(1f), Is.EqualTo(10f * sign).Within(0.001f));
                Assert.That(curve.Evaluate(1.5f), Is.EqualTo(25f * sign).Within(0.001f));
            });
        }

        [Test]
        public void SmoothCurvesUseBlockbenchCatmullRomIncludingOvershootAndUnevenTimes()
        {
            BbKeyframe[] keys = { Key("position", 0f, 0f), Key("position", 1f, 10f),
                Key("position", 3f, 0f), Key("position", 4f, 0f) };
            foreach (BbKeyframe key in keys) key.interpolation = "catmullrom";
            WithClip("position", keys, 4f, clip =>
            {
                AnimationCurve curve = Curve(clip, "m_LocalPosition.x");
                Assert.That(curve.Evaluate(2f), Is.EqualTo(-5.625f).Within(0.001f));
                Assert.That(curve.Evaluate(3.5f), Is.EqualTo(0.625f).Within(0.001f));
            });
        }

        [Test]
        public void BezierCurvesFollowBothIndependentHandles()
        {
            BbKeyframe left = Key("position", 0f, 0f);
            BbKeyframe right = Key("position", 1f, 10f);
            left.interpolation = right.interpolation = "bezier";
            left.bezier_right_time = new[] { 1f / 3f, 1f / 3f, 1f / 3f };
            left.bezier_right_value = new[] { 6f, 0f, 0f };
            right.bezier_left_time = new[] { -1f / 3f, -1f / 3f, -1f / 3f };
            right.bezier_left_value = new[] { -2f, 0f, 0f };
            WithClip("position", new[] { left, right }, 1f, clip =>
                Assert.That(Curve(clip, "m_LocalPosition.x").Evaluate(0.5f), Is.EqualTo(-6.5f).Within(0.001f)));
        }

        [Test]
        public void LinearSegmentsEnteringSteppedKeysRemainLinear()
        {
            BbKeyframe middle = Key("position", 1f, 10f);
            middle.interpolation = "step";
            WithClip("position", new[] { Key("position", 0f, 0f), middle, Key("position", 2f, 20f) }, 2f, clip =>
            {
                AnimationCurve curve = Curve(clip, "m_LocalPosition.x");
                Assert.That(curve.Evaluate(0.5f), Is.EqualTo(-5f).Within(0.001f));
                Assert.That(curve.Evaluate(1.5f), Is.EqualTo(-10f).Within(0.001f));
            });
        }

        [Test]
        public void EulerAnimationAddsAnglesToTheRestPoseBeforeConvertingRotation()
        {
            var root = new GameObject("Animation test");
            var target = new GameObject("target");
            target.transform.SetParent(root.transform, false);
            target.transform.localRotation = Quaternion.AngleAxis(-40f, Vector3.up) * Quaternion.AngleAxis(30f, Vector3.right);
            var model = new BbModel();
            model.groups.Add(new BbGroup { uuid = "target", rotation = new[] { 30f, 40f, 0f } });
            var animator = new BbAnimator();
            BbKeyframe key = Key("rotation", 0f, 0f);
            key.data_points[0] = new BbDataPoint { x = "0", y = "0", z = "60" };
            animator.keyframes.Add(key);
            var animation = new BbAnimation { length = 1f };
            animation.animators["target"] = animator;
            model.animations.Add(animation);
            AnimationClip clip = null;
            try
            {
                clip = Build(model, new Dictionary<string, string> { ["target"] = "target" },
                    new Dictionary<string, Transform> { ["target"] = target.transform });
                clip.SampleAnimation(root, 0.5f);
                Quaternion expected = Quaternion.AngleAxis(-60f, Vector3.forward) *
                    Quaternion.AngleAxis(-40f, Vector3.up) * Quaternion.AngleAxis(30f, Vector3.right);
                Assert.That(Quaternion.Angle(target.transform.localRotation, expected), Is.LessThan(0.05f));
            }
            finally { if (clip != null) Object.DestroyImmediate(clip); Object.DestroyImmediate(root); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SteppedRotationDoesNotEaseOrSlerpBeforeTheNextKey(bool quaternionInterpolation)
        {
            BbKeyframe left = Key("rotation", 0f, 0f);
            left.interpolation = "step";
            var animator = new BbAnimator { quaternion_interpolation = quaternionInterpolation };
            animator.keyframes.Add(left);
            animator.keyframes.Add(Key("rotation", 1f, 90f));
            var model = Model(animator, 1f);
            AnimationClip clip = Build(model);
            try
            {
                Assert.That(Quaternion.Angle(Rotation(clip, 0.999999f), Quaternion.identity), Is.LessThan(0.05f));
                Assert.That(Quaternion.Angle(Rotation(clip, 1f), Quaternion.AngleAxis(90f, Vector3.right)), Is.LessThan(0.05f));
            }
            finally { Object.DestroyImmediate(clip); }
        }

        [Test]
        public void SerializedSteppedRotationRetainsItsDiscontinuity()
        {
            BbKeyframe left = Key("rotation", 0f, 0f);
            left.interpolation = "step";
            var animator = new BbAnimator();
            animator.keyframes.Add(left);
            animator.keyframes.Add(Key("rotation", 1f, 90f));
            AnimationClip clip = Build(Model(animator, 1f));
            string assetPath = "Assets/BlockbenchStepped-" + System.Guid.NewGuid().ToString("N") + ".anim";
            try
            {
                AssetDatabase.CreateAsset(clip, assetPath);
                AssetDatabase.SaveAssetIfDirty(clip);
                Resources.UnloadAsset(clip);
                clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
                Assert.That(Quaternion.Angle(Rotation(clip, 0.999999f), Quaternion.identity), Is.LessThan(0.05f));
                Assert.That(Quaternion.Angle(Rotation(clip, 1f), Quaternion.AngleAxis(90f, Vector3.right)), Is.LessThan(0.05f));
            }
            finally
            {
                AssetDatabase.DeleteAsset(assetPath);
                if (clip != null && !EditorUtility.IsPersistent(clip)) Object.DestroyImmediate(clip);
            }
        }

        [Test]
        public void RotationSplitKeysPreserveIncomingAndOutgoingValues()
        {
            BbKeyframe split = Key("rotation", 1f, 90f);
            split.data_points.Add(Point(180f));
            WithClip("rotation", new[] { Key("rotation", 0f, 0f), split, Key("rotation", 2f, 270f) }, 2f, clip =>
            {
                Assert.That(Quaternion.Angle(Rotation(clip, 1f), Quaternion.AngleAxis(90f, Vector3.right)), Is.LessThan(0.1f));
                Assert.That(Quaternion.Angle(Rotation(clip, 1.5f), Quaternion.AngleAxis(225f, Vector3.right)), Is.LessThan(0.1f));
            });
        }

        [Test]
        public void GlobalRotationCancelsTheAnimatedParentAtEverySample()
        {
            var root = new GameObject("Animation test");
            var parent = new GameObject("parent");
            var child = new GameObject("child");
            parent.transform.SetParent(root.transform, false);
            child.transform.SetParent(parent.transform, false);
            var parentAnimator = new BbAnimator();
            parentAnimator.keyframes.Add(Key("rotation", 0f, 0f));
            parentAnimator.keyframes.Add(Key("rotation", 1f, 90f));
            var childAnimator = new BbAnimator { rotation_global = true };
            BbKeyframe childKey = Key("rotation", 0f, 0f);
            childKey.data_points[0] = new BbDataPoint { x = "0", y = "0", z = "30" };
            childAnimator.keyframes.Add(childKey);
            var animation = new BbAnimation { length = 1f };
            animation.animators["parent"] = parentAnimator;
            animation.animators["child"] = childAnimator;
            var model = new BbModel();
            model.animations.Add(animation);
            AnimationClip clip = null;
            try
            {
                clip = Build(model, new Dictionary<string, string> { ["parent"] = "parent", ["child"] = "parent/child" },
                    new Dictionary<string, Transform> { ["parent"] = parent.transform, ["child"] = child.transform });
                foreach (float time in new[] { 0f, 0.25f, 0.5f, 0.75f, 1f })
                {
                    clip.SampleAnimation(root, time);
                    Assert.That(Quaternion.Angle(child.transform.rotation, Quaternion.AngleAxis(-30f, Vector3.forward)),
                        Is.LessThan(0.1f), "Global rotation drifted at time " + time);
                }
            }
            finally { if (clip != null) Object.DestroyImmediate(clip); Object.DestroyImmediate(root); }
        }

        private static void WithClip(string channel, BbKeyframe[] keys, float duration, System.Action<AnimationClip> check)
        {
            var animator = new BbAnimator();
            animator.keyframes.AddRange(keys);
            AnimationClip clip = Build(Model(animator, duration));
            try { check(clip); }
            finally { Object.DestroyImmediate(clip); }
        }

        private static BbModel Model(BbAnimator animator, float duration)
        {
            var animation = new BbAnimation { name = "Test animation", length = duration };
            animation.animators["target"] = animator;
            var model = new BbModel();
            model.animations.Add(animation);
            return model;
        }

        private static AnimationClip Build(BbModel model, Dictionary<string, string> paths = null,
            Dictionary<string, Transform> transforms = null) => new AnimationBuilder(model,
            paths ?? new Dictionary<string, string> { ["target"] = "target" },
            transforms ?? new Dictionary<string, Transform>(), new BlockbenchImporterSettings { importScale = 1f })
            .BuildAnimationClips().Single();

        private static AnimationCurve Curve(AnimationClip clip, string property) => AnimationUtility.GetEditorCurve(clip,
            AnimationUtility.GetCurveBindings(clip).Single(binding => binding.propertyName == property));

        private static Quaternion Rotation(AnimationClip clip, float time) => new Quaternion(
            Curve(clip, "m_LocalRotation.x").Evaluate(time), Curve(clip, "m_LocalRotation.y").Evaluate(time),
            Curve(clip, "m_LocalRotation.z").Evaluate(time), Curve(clip, "m_LocalRotation.w").Evaluate(time)).normalized;

        private static BbKeyframe Key(string channel, float time, float x) => new BbKeyframe
        { channel = channel, time = time, data_points = new List<BbDataPoint> { Point(x) } };

        private static BbDataPoint Point(float x) => new BbDataPoint { x = x.ToString(CultureInfo.InvariantCulture), y = "0", z = "0" };
    }
}
