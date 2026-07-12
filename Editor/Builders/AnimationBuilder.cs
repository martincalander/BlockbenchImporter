using System.Collections.Generic;
using Essentials.BlockbenchImporter.Model;
using Essentials.BlockbenchImporter.Utils;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Essentials.BlockbenchImporter.Builders
{
    /// <summary>
    /// Builds AnimationClips and AnimatorController from Blockbench animations.
    /// </summary>
    public class AnimationBuilder
    {
        private readonly BbModel model;
        private readonly Dictionary<string, string> uuidToPath;
        private readonly Dictionary<string, Transform> uuidToTransform;
        private readonly BlockbenchImporterSettings settings;

        /// <summary>
        /// Create an animation builder.
        /// </summary>
        /// <param name="model">The parsed Blockbench model</param>
        /// <param name="uuidToPath">Mapping from node UUID to transform path (relative to root)</param>
        /// <param name="uuidToTransform">Mapping from node UUID to Transform</param>
        /// <param name="settings">Import settings</param>
        public AnimationBuilder(BbModel model, Dictionary<string, string> uuidToPath,
            Dictionary<string, Transform> uuidToTransform, BlockbenchImporterSettings settings)
        {
            this.model = model;
            this.uuidToPath = uuidToPath;
            this.uuidToTransform = uuidToTransform;
            this.settings = settings;
        }

        /// <summary>
        /// Build all animation clips from the model.
        /// </summary>
        public List<AnimationClip> BuildAnimationClips()
        {
            var clips = new List<AnimationClip>();

            foreach (var anim in model.animations)
            {
                var clip = BuildAnimationClip(anim);
                if (clip != null)
                {
                    clips.Add(clip);
                }
            }

            return clips;
        }

        /// <summary>
        /// Build an AnimatorController with states for each clip.
        /// Returns the controller and all internal objects that need to be added as sub-assets.
        /// </summary>
        public AnimatorControllerResult BuildAnimatorController(List<AnimationClip> clips, string assetPath)
        {
            if (clips == null || clips.Count == 0)
                return null;

            var result = new AnimatorControllerResult();

            // Create the controller
            var controller = new AnimatorController();
            controller.name = System.IO.Path.GetFileNameWithoutExtension(assetPath) + "_Controller";
            result.controller = controller;

            // Create a state machine manually (required for sub-asset controllers)
            var stateMachine = new AnimatorStateMachine();
            stateMachine.name = "Base Layer";
            stateMachine.hideFlags = HideFlags.HideInHierarchy;
            result.subAssets.Add(stateMachine);

            // Create layer with the state machine
            var layer = new AnimatorControllerLayer
            {
                name = "Base Layer",
                stateMachine = stateMachine,
                defaultWeight = 1f
            };

            // Add layer to controller
            controller.AddLayer(layer);

            // Create states for each clip
            AnimatorState defaultState = null;

            foreach (var clip in clips)
            {
                var state = stateMachine.AddState(clip.name);
                state.motion = clip;
                state.hideFlags = HideFlags.HideInHierarchy;
                result.subAssets.Add(state);

                if (defaultState == null)
                {
                    defaultState = state;
                }
            }

            // Set default state
            if (defaultState != null)
            {
                stateMachine.defaultState = defaultState;
            }

            return result;
        }

        private AnimationClip BuildAnimationClip(BbAnimation anim)
        {
            var clip = new AnimationClip();
            clip.name = SanitizeName(anim.name ?? "Animation");
            clip.frameRate = anim.snapping > 0 ? anim.snapping : 24;

            // Configure looping
            var clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
            clipSettings.loopTime = anim.loop == "loop";
            AnimationUtility.SetAnimationClipSettings(clip, clipSettings);

            bool hasAnyCurves = false;

            foreach (var animatorKvp in anim.animators)
            {
                string nodeUuid = animatorKvp.Key;
                var animator = animatorKvp.Value;

                // Find the transform path for this node
                if (!uuidToPath.TryGetValue(nodeUuid, out string transformPath))
                {
                    Debug.LogWarning($"Animation references unknown node UUID: {nodeUuid}");
                    continue;
                }

                // Get rest pose from the transform
                Vector3 restPosition = Vector3.zero;
                Quaternion restRotation = Quaternion.identity;
                Vector3 restScale = Vector3.one;

                if (uuidToTransform.TryGetValue(nodeUuid, out var transform))
                {
                    restPosition = transform.localPosition;
                    restRotation = transform.localRotation;
                    restScale = transform.localScale;
                }

                // Group keyframes by channel
                var positionKeyframes = new List<BbKeyframe>();
                var rotationKeyframes = new List<BbKeyframe>();
                var scaleKeyframes = new List<BbKeyframe>();

                foreach (var kf in animator.keyframes)
                {
                    switch (kf.channel?.ToLowerInvariant())
                    {
                        case "position":
                            positionKeyframes.Add(kf);
                            break;
                        case "rotation":
                            rotationKeyframes.Add(kf);
                            break;
                        case "scale":
                            scaleKeyframes.Add(kf);
                            break;
                    }
                }

                // Build position curves
                if (positionKeyframes.Count > 0)
                {
                    BuildPositionCurves(clip, transformPath, positionKeyframes, restPosition);
                    hasAnyCurves = true;
                }

                // Build rotation curves (using quaternions)
                if (rotationKeyframes.Count > 0)
                {
                    BuildRotationCurves(clip, transformPath, nodeUuid, rotationKeyframes, restRotation,
                        animator, anim);
                    hasAnyCurves = true;
                }

                // Build scale curves
                if (scaleKeyframes.Count > 0)
                {
                    BuildScaleCurves(clip, transformPath, scaleKeyframes, restScale);
                    hasAnyCurves = true;
                }
            }

            return hasAnyCurves ? clip : null;
        }

        private void BuildPositionCurves(AnimationClip clip, string path, List<BbKeyframe> keyframes,
            Vector3 restPosition)
        {
            var curveX = new AnimationCurve();
            var curveY = new AnimationCurve();
            var curveZ = new AnimationCurve();

            // Sort keyframes by time
            keyframes.Sort((a, b) => a.time.CompareTo(b.time));

            foreach (var kf in keyframes)
            {
                if (kf.data_points == null || kf.data_points.Count == 0)
                    continue;

                var dp = kf.data_points[0];
                Vector3 bbPos = dp.ToVector3();

                // Animation position offsets are in Blockbench units and are baked into Unity units.
                Vector3 unityPos = CoordinateConverter.ConvertPositionRaw(bbPos.x, bbPos.y, bbPos.z) *
                                   settings.importScale;

                // Add to rest position
                Vector3 finalPos = restPosition + unityPos;

                // Create keyframe with interpolation
                var kfX = new Keyframe(kf.time, finalPos.x);
                var kfY = new Keyframe(kf.time, finalPos.y);
                var kfZ = new Keyframe(kf.time, finalPos.z);

                ApplyInterpolation(ref kfX, kf.interpolation);
                ApplyInterpolation(ref kfY, kf.interpolation);
                ApplyInterpolation(ref kfZ, kf.interpolation);

                curveX.AddKey(kfX);
                curveY.AddKey(kfY);
                curveZ.AddKey(kfZ);
            }

            ApplyCurveInterpolation(curveX, keyframes, 0, -settings.importScale);
            ApplyCurveInterpolation(curveY, keyframes, 1, settings.importScale);
            ApplyCurveInterpolation(curveZ, keyframes, 2, settings.importScale);
            clip.SetCurve(path, typeof(Transform), "localPosition.x", curveX);
            clip.SetCurve(path, typeof(Transform), "localPosition.y", curveY);
            clip.SetCurve(path, typeof(Transform), "localPosition.z", curveZ);
        }

        private void BuildRotationCurves(AnimationClip clip, string path, string nodeUuid,
            List<BbKeyframe> keyframes, Quaternion restRotation, BbAnimator animator, BbAnimation animation)
        {
            var curveX = new AnimationCurve();
            var curveY = new AnimationCurve();
            var curveZ = new AnimationCurve();
            var curveW = new AnimationCurve();
            keyframes.Sort((a, b) => a.time.CompareTo(b.time));
            Quaternion prevQuat = restRotation;
            float duration = Mathf.Max(animation.length, keyframes[keyframes.Count - 1].time);
            int sampleRate = Mathf.Max(60, Mathf.Max(1, animation.snapping) * 4);
            var sampleTimes = new SortedSet<float> { 0f, duration };
            foreach (BbKeyframe keyframe in keyframes) sampleTimes.Add(keyframe.time);
            for (int frame = 1; frame < Mathf.CeilToInt(duration * sampleRate); frame++)
                sampleTimes.Add(frame / (float)sampleRate);

            foreach (float time in sampleTimes)
            {
                Quaternion finalQuat = EvaluateRotation(keyframes, time, restRotation,
                    animator.quaternion_interpolation);
                if (animator.rotation_global && uuidToTransform.TryGetValue(nodeUuid, out Transform target) &&
                    target.parent != null)
                    finalQuat = Quaternion.Inverse(target.parent.rotation) * finalQuat;
                if (Quaternion.Dot(prevQuat, finalQuat) < 0)
                    finalQuat = new Quaternion(-finalQuat.x, -finalQuat.y, -finalQuat.z, -finalQuat.w);
                prevQuat = finalQuat;
                curveX.AddKey(time, finalQuat.x);
                curveY.AddKey(time, finalQuat.y);
                curveZ.AddKey(time, finalQuat.z);
                curveW.AddKey(time, finalQuat.w);
            }
            SetAllLinear(curveX);
            SetAllLinear(curveY);
            SetAllLinear(curveZ);
            SetAllLinear(curveW);
            clip.SetCurve(path, typeof(Transform), "localRotation.x", curveX);
            clip.SetCurve(path, typeof(Transform), "localRotation.y", curveY);
            clip.SetCurve(path, typeof(Transform), "localRotation.z", curveZ);
            clip.SetCurve(path, typeof(Transform), "localRotation.w", curveW);
            clip.EnsureQuaternionContinuity();
        }

        private static Quaternion EvaluateRotation(List<BbKeyframe> keyframes, float time,
            Quaternion restRotation, bool quaternionInterpolation)
        {
            FindKeyframes(keyframes, time, out int before, out int after);
            if (quaternionInterpolation && before >= 0 && after >= 0 && before != after)
            {
                Quaternion left = restRotation * CoordinateConverter.ConvertAnimationRotation(
                    GetPoint(keyframes[before], true));
                Quaternion right = restRotation * CoordinateConverter.ConvertAnimationRotation(
                    GetPoint(keyframes[after], false));
                float alpha = Mathf.InverseLerp(keyframes[before].time, keyframes[after].time, time);
                return Quaternion.SlerpUnclamped(left, right, alpha);
            }
            return restRotation * CoordinateConverter.ConvertAnimationRotation(new Vector3(
                EvaluateAxis(keyframes, time, 0, before, after),
                EvaluateAxis(keyframes, time, 1, before, after),
                EvaluateAxis(keyframes, time, 2, before, after)));
        }

        private static void FindKeyframes(List<BbKeyframe> keyframes, float time, out int before, out int after)
        {
            before = -1;
            after = -1;
            for (int index = 0; index < keyframes.Count; index++)
            {
                if (keyframes[index].time <= time) before = index;
                if (keyframes[index].time >= time) { after = index; break; }
            }
            if (before < 0) before = after;
            if (after < 0) after = before;
        }

        private static float EvaluateAxis(List<BbKeyframe> keys, float time, int axis, int before, int after)
        {
            if (before < 0) return 0f;
            float left = GetAxis(GetPoint(keys[before], true), axis);
            if (after < 0 || before == after) return left;
            float right = GetAxis(GetPoint(keys[after], false), axis);
            if (keys[before].interpolation == "step") return left;
            float alpha = Mathf.InverseLerp(keys[before].time, keys[after].time, time);
            if (keys[before].interpolation == "bezier" || keys[after].interpolation == "bezier")
                return EvaluateBezier(keys[before], keys[after], axis, alpha, left, right);
            if (keys[before].interpolation == "catmullrom" || keys[after].interpolation == "catmullrom")
            {
                float p0 = before > 0 ? GetAxis(GetPoint(keys[before - 1], true), axis) : left;
                float p3 = after + 1 < keys.Count ? GetAxis(GetPoint(keys[after + 1], false), axis) : right;
                float a2 = alpha * alpha;
                float a3 = a2 * alpha;
                return 0.5f * ((2f * left) + (-p0 + right) * alpha +
                               (2f * p0 - 5f * left + 4f * right - p3) * a2 +
                               (-p0 + 3f * left - 3f * right + p3) * a3);
            }
            return Mathf.LerpUnclamped(left, right, alpha);
        }

        private static float EvaluateBezier(BbKeyframe before, BbKeyframe after, int axis, float alpha,
            float left, float right)
        {
            float gap = Mathf.Max(0.000001f, after.time - before.time);
            float x1 = Mathf.Clamp01(ReadAxis(before.bezier_right_time, axis) / gap);
            float x2 = Mathf.Clamp01(1f + ReadAxis(after.bezier_left_time, axis) / gap);
            float y1 = left + ReadAxis(before.bezier_right_value, axis);
            float y2 = right + ReadAxis(after.bezier_left_value, axis);
            float low = 0f, high = 1f, parameter = alpha;
            for (int iteration = 0; iteration < 14; iteration++)
            {
                parameter = (low + high) * 0.5f;
                float x = Cubic(0f, x1, x2, 1f, parameter);
                if (x < alpha) low = parameter; else high = parameter;
            }
            return Cubic(left, y1, y2, right, parameter);
        }

        private static float Cubic(float p0, float p1, float p2, float p3, float t)
        {
            float inverse = 1f - t;
            return inverse * inverse * inverse * p0 + 3f * inverse * inverse * t * p1 +
                   3f * inverse * t * t * p2 + t * t * t * p3;
        }

        private static Vector3 GetPoint(BbKeyframe keyframe, bool last)
        {
            if (keyframe.data_points == null || keyframe.data_points.Count == 0) return Vector3.zero;
            return keyframe.data_points[last ? keyframe.data_points.Count - 1 : 0].ToVector3();
        }

        private static float GetAxis(Vector3 value, int axis) => axis == 0 ? value.x : axis == 1 ? value.y : value.z;

        private static void SetAllLinear(AnimationCurve curve)
        {
            for (int index = 0; index < curve.length; index++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, index, AnimationUtility.TangentMode.Linear);
                AnimationUtility.SetKeyRightTangentMode(curve, index, AnimationUtility.TangentMode.Linear);
            }
        }

        private void BuildScaleCurves(AnimationClip clip, string path, List<BbKeyframe> keyframes, Vector3 restScale)
        {
            var curveX = new AnimationCurve();
            var curveY = new AnimationCurve();
            var curveZ = new AnimationCurve();

            // Sort keyframes by time
            keyframes.Sort((a, b) => a.time.CompareTo(b.time));

            foreach (var kf in keyframes)
            {
                if (kf.data_points == null || kf.data_points.Count == 0)
                    continue;

                var dp = kf.data_points[0];
                Vector3 bbScale = dp.ToVector3();

                Vector3 scaleMultiplier = new Vector3(
                    Mathf.Approximately(bbScale.x, 0f) ? 0.00001f : bbScale.x,
                    Mathf.Approximately(bbScale.y, 0f) ? 0.00001f : bbScale.y,
                    Mathf.Approximately(bbScale.z, 0f) ? 0.00001f : bbScale.z
                );

                Vector3 finalScale = Vector3.Scale(restScale, scaleMultiplier);

                var kfX = new Keyframe(kf.time, finalScale.x);
                var kfY = new Keyframe(kf.time, finalScale.y);
                var kfZ = new Keyframe(kf.time, finalScale.z);

                ApplyInterpolation(ref kfX, kf.interpolation);
                ApplyInterpolation(ref kfY, kf.interpolation);
                ApplyInterpolation(ref kfZ, kf.interpolation);

                curveX.AddKey(kfX);
                curveY.AddKey(kfY);
                curveZ.AddKey(kfZ);
            }

            ApplyCurveInterpolation(curveX, keyframes, 0, 1f);
            ApplyCurveInterpolation(curveY, keyframes, 1, 1f);
            ApplyCurveInterpolation(curveZ, keyframes, 2, 1f);
            clip.SetCurve(path, typeof(Transform), "localScale.x", curveX);
            clip.SetCurve(path, typeof(Transform), "localScale.y", curveY);
            clip.SetCurve(path, typeof(Transform), "localScale.z", curveZ);
        }

        private void ApplyInterpolation(ref Keyframe kf, string interpolation)
        {
            switch (interpolation?.ToLowerInvariant())
            {
                case "step":
                    kf.inTangent = float.PositiveInfinity;
                    kf.outTangent = float.PositiveInfinity;
                    break;
                case "linear":
                default:
                    // Use default linear tangents
                    break;
            }
        }

        private static void ApplyCurveInterpolation(AnimationCurve curve, List<BbKeyframe> source, int axis,
            float valueScale)
        {
            int count = Mathf.Min(curve.length, source.Count);
            for (int i = 0; i < count; i++)
            {
                string mode = source[i].interpolation ?? "linear";
                switch (mode)
                {
                    case "step":
                        AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Constant);
                        AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Constant);
                        break;
                    case "catmullrom":
                        AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
                        AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
                        break;
                    case "bezier":
                    {
                        Keyframe key = curve.keys[i];
                        float leftTime = ReadAxis(source[i].bezier_left_time, axis);
                        float rightTime = ReadAxis(source[i].bezier_right_time, axis);
                        key.inTangent = Mathf.Abs(leftTime) < 0.000001f
                            ? 0f
                            : ReadAxis(source[i].bezier_left_value, axis) * valueScale / leftTime;
                        key.outTangent = Mathf.Abs(rightTime) < 0.000001f
                            ? 0f
                            : ReadAxis(source[i].bezier_right_value, axis) * valueScale / rightTime;
                        float previousGap = i > 0 ? Mathf.Max(0.000001f, source[i].time - source[i - 1].time) : 1f;
                        float nextGap = i + 1 < source.Count ? Mathf.Max(0.000001f, source[i + 1].time - source[i].time) : 1f;
                        key.inWeight = Mathf.Clamp01(-leftTime / previousGap);
                        key.outWeight = Mathf.Clamp01(rightTime / nextGap);
                        key.weightedMode = WeightedMode.Both;
                        curve.MoveKey(i, key);
                        AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Free);
                        AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Free);
                        break;
                    }
                    default:
                        AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
                        AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
                        break;
                }
            }
        }

        private static float ReadAxis(float[] values, int axis) =>
            values != null && axis >= 0 && axis < values.Length ? values[axis] : 0f;

        private string SanitizeName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "Animation";

            char[] invalid = System.IO.Path.GetInvalidFileNameChars();
            foreach (char c in invalid)
            {
                name = name.Replace(c, '_');
            }

            return name;
        }
    }

    /// <summary>
    /// Result of building an AnimatorController, including internal objects that need to be added as sub-assets.
    /// </summary>
    public class AnimatorControllerResult
    {
        public AnimatorController controller;
        public List<Object> subAssets = new List<Object>();
    }
}
