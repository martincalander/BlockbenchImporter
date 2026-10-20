using System.Collections.Generic;
using MartinCalander.BlockbenchImporter.Model;
using MartinCalander.BlockbenchImporter.Utils;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace MartinCalander.BlockbenchImporter.Builders
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
            var restRotations = new Dictionary<string, Vector3>();
            ReadInlineRotations(model.outliner, restRotations);
            foreach (BbGroup group in model.groups)
                if (!string.IsNullOrEmpty(group.uuid)) restRotations[group.uuid] = ReadVector(group.rotation);
            foreach (BbElement element in model.elements)
                if (!string.IsNullOrEmpty(element.uuid)) restRotations[element.uuid] = ReadVector(element.rotation);


            try
            {
                foreach (var anim in model.animations)
                {
                    var clip = BuildAnimationClip(anim, restRotations);
                    if (clip != null)
                    {
                        clips.Add(clip);
                    }
                }

                return clips;
            }
            catch
            {
                foreach (AnimationClip clip in clips) Object.DestroyImmediate(clip);
                throw;
            }
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

            try
            {
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
            catch
            {
                foreach (Object child in result.subAssets) Object.DestroyImmediate(child);
                Object.DestroyImmediate(controller);
                throw;
            }
        }

        private sealed class AnimationTarget
        {
            public string path;
            public Transform transform;
            public BbAnimator animator;
            public Vector3 restPosition;
            public Vector3 restScale = Vector3.one;
            public Quaternion restRotation = Quaternion.identity;
            public Vector3 restEuler;
            public bool hasRestEuler;
            public readonly List<BbKeyframe> position = new List<BbKeyframe>();
            public readonly List<BbKeyframe> rotation = new List<BbKeyframe>();
            public readonly List<BbKeyframe> scale = new List<BbKeyframe>();
            public Vector3[] rotationIncoming;
            public Vector3[] rotationOutgoing;

            public void CacheRotationPoints()
            {
                if (rotation.Count == 0) return;
                rotationIncoming = new Vector3[rotation.Count];
                rotationOutgoing = new Vector3[rotation.Count];
                for (int index = 0; index < rotation.Count; index++)
                {
                    BbKeyframe key = rotation[index];
                    rotationIncoming[index] = GetPoint(key, false);
                    rotationOutgoing[index] = key.data_points != null && key.data_points.Count > 1
                        ? GetPoint(key, true) : rotationIncoming[index];
                }
            }
        }

        private AnimationClip BuildAnimationClip(BbAnimation anim, Dictionary<string, Vector3> restRotations)
        {
            var targets = new List<AnimationTarget>();
            var targetsByTransform = new Dictionary<Transform, AnimationTarget>();
            float duration = Mathf.Max(0f, anim.length);
            foreach (var pair in anim.animators)
            {
                if (!uuidToPath.TryGetValue(pair.Key, out string path))
                {
                    Debug.LogWarning($"Animation references unknown node UUID: {pair.Key}");
                    continue;
                }
                var target = new AnimationTarget { path = path, animator = pair.Value };
                if (uuidToTransform.TryGetValue(pair.Key, out Transform transform))
                {
                    target.transform = transform;
                    target.restPosition = transform.localPosition;
                    target.restRotation = transform.localRotation;
                    target.restScale = transform.localScale;
                    targetsByTransform[transform] = target;
                }
                target.hasRestEuler = restRotations.TryGetValue(pair.Key, out target.restEuler);
                foreach (BbKeyframe key in pair.Value.keyframes)
                {
                    List<BbKeyframe> channel;
                    switch (key.channel?.ToLowerInvariant())
                    {
                        case "position": channel = target.position; break;
                        case "rotation": channel = target.rotation; break;
                        case "scale": channel = target.scale; break;
                        default: continue;
                    }
                    channel.Add(key);
                    duration = Mathf.Max(duration, key.time);
                }
                target.position.Sort((left, right) => left.time.CompareTo(right.time));
                target.rotation.Sort((left, right) => left.time.CompareTo(right.time));
                target.CacheRotationPoints();
                target.scale.Sort((left, right) => left.time.CompareTo(right.time));
                if (target.position.Count != 0 || target.rotation.Count != 0 || target.scale.Count != 0 ||
                    target.animator.rotation_global)
                    targets.Add(target);
            }
            if (targets.Count == 0) return null;

            var clip = new AnimationClip
            {
                name = SanitizeName(anim.name ?? "Animation"),
                frameRate = anim.snapping > 0 ? anim.snapping : 24,
                wrapMode = anim.loop == "loop" ? WrapMode.Loop :
                    anim.loop == "hold" ? WrapMode.ClampForever : WrapMode.Once
            };
            try
            {
                foreach (AnimationTarget target in targets)
                {
                    BuildVectorCurves(clip, target.path, target.position, "localPosition", target.restPosition,
                        new Vector3(-settings.importScale, settings.importScale, settings.importScale), duration, anim.loop);
                    BuildVectorCurves(clip, target.path, target.scale, "localScale", Vector3.zero,
                        target.restScale, duration, anim.loop);
                    if (target.rotation.Count != 0 || target.animator.rotation_global)
                        BuildRotationCurves(clip, target, targetsByTransform, duration, anim);
                }
                var clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
                clipSettings.loopTime = anim.loop == "loop";
                clipSettings.startTime = 0f;
                clipSettings.stopTime = duration;
                AnimationUtility.SetAnimationClipSettings(clip, clipSettings);
                return clip;
            }
            catch
            {
                Object.DestroyImmediate(clip);
                throw;
            }
        }

        private static Vector3 ReadVector(float[] value) => value != null && value.Length >= 3
            ? new Vector3(value[0], value[1], value[2]) : Vector3.zero;

        private static void ReadInlineRotations(List<object> nodes, Dictionary<string, Vector3> rotations)
        {
            foreach (object item in nodes)
            {
                if (!(item is Dictionary<string, object> node)) continue;
                if (node.TryGetValue("uuid", out object uuid) && uuid is string id)
                {
                    Vector3 rotation = Vector3.zero;
                    if (node.TryGetValue("rotation", out object values) && values is List<object> axes && axes.Count >= 3)
                        rotation = new Vector3(ReadNumber(axes[0]), ReadNumber(axes[1]), ReadNumber(axes[2]));
                    rotations[id] = rotation;
                }
                if (node.TryGetValue("children", out object children) && children is List<object> childNodes)
                    ReadInlineRotations(childNodes, rotations);
            }
        }

        private static float ReadNumber(object value) => float.TryParse(
            System.Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture),
            System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture,
            out float number) ? number : 0f;

        private static void BuildVectorCurves(AnimationClip clip, string path, List<BbKeyframe> source,
            string property, Vector3 offset, Vector3 multiplier, float duration, string loop)
        {
            if (source.Count == 0) return;
            var valid = source.FindAll(key => key.data_points != null && key.data_points.Count != 0);
            if (valid.Count == 0) return;
            for (int axis = 0; axis < 3; axis++)
            {
                AnimationCurve curve = BuildScalarCurve(valid, axis, duration, loop, property == "localScale");
                for (int index = 0; index < curve.length; index++)
                {
                    Keyframe key = curve[index];
                    key.value = GetAxis(offset, axis) + key.value * GetAxis(multiplier, axis);
                    if (!float.IsInfinity(key.inTangent)) key.inTangent *= GetAxis(multiplier, axis);
                    if (!float.IsInfinity(key.outTangent)) key.outTangent *= GetAxis(multiplier, axis);
                    curve.MoveKey(index, key);
                }
                string serializedProperty = "m_" + char.ToUpperInvariant(property[0]) + property.Substring(1);
                AnimationUtility.SetEditorCurve(clip,
                    EditorCurveBinding.FloatCurve(path, typeof(Transform), serializedProperty + "." + "xyz"[axis]), curve);
            }
        }

        private static AnimationCurve BuildScalarCurve(List<BbKeyframe> source, int axis, float duration,
            string loop, bool scale)
        {
            var keys = new List<Keyframe>();
            var incoming = new int[source.Count];
            var outgoing = new int[source.Count];
            for (int index = 0; index < source.Count; index++)
            {
                BbKeyframe key = source[index];
                incoming[index] = keys.Count;
                keys.Add(new Keyframe(key.time, ScalarValue(GetPoint(key, false), axis, scale)));
                outgoing[index] = incoming[index];
                if (key.data_points.Count > 1 && key.time < duration)
                {
                    float gap = index + 1 < source.Count ? source[index + 1].time - key.time : duration - key.time;
                    float afterTime = key.time + Mathf.Min(0.00001f, gap * 0.001f);
                    if (afterTime > key.time)
                    {
                        Keyframe beforeJump = keys[incoming[index]];
                        beforeJump.outTangent = float.PositiveInfinity;
                        keys[incoming[index]] = beforeJump;
                        outgoing[index] = keys.Count;
                        keys.Add(new Keyframe(afterTime, ScalarValue(GetPoint(key, true), axis, scale)));
                    }
                }
            }
            for (int index = 0; index + 1 < source.Count; index++)
            {
                int leftIndex = outgoing[index], rightIndex = incoming[index + 1];
                Keyframe left = keys[leftIndex], right = keys[rightIndex];
                float gap = right.time - left.time;
                if (gap <= 0f) continue;
                string mode = SegmentMode(source[index], source[index + 1]);
                if (mode == "step")
                    left.outTangent = right.inTangent = float.PositiveInfinity;
                else if (mode == "catmullrom")
                {
                    float previous = left.value, next = right.value;
                    int previousIndex = index - 1, nextIndex = index + 2;
                    if (loop == "loop" && source.Count >= 3)
                    {
                        if (previousIndex < 0) previousIndex = source.Count - 2;
                        if (nextIndex >= source.Count) nextIndex = 1;
                    }
                    if (previousIndex >= 0 && source[index].data_points.Count == 1)
                        previous = ScalarValue(GetPoint(source[previousIndex], true), axis, scale);
                    if (nextIndex < source.Count && source[index + 1].data_points.Count == 1)
                        next = ScalarValue(GetPoint(source[nextIndex], false), axis, scale);
                    left.outTangent = (right.value - previous) * 0.5f / gap;
                    right.inTangent = (next - left.value) * 0.5f / gap;
                }
                else if (mode == "bezier")
                {
                    float rightTime = Mathf.Clamp(ReadAxis(source[index].bezier_right_time, axis, 0.1f), 0f, gap);
                    float leftTime = Mathf.Clamp(-ReadAxis(source[index + 1].bezier_left_time, axis, -0.1f), 0f, gap);
                    float rightValue = ReadAxis(source[index].bezier_right_value, axis);
                    float leftValue = ReadAxis(source[index + 1].bezier_left_value, axis);
                    // Tiny nonzero handle times also represent vertical handles without creating a stepped tangent.
                    if (rightTime == 0f && rightValue != 0f) rightTime = gap * 0.000001f;
                    if (leftTime == 0f && leftValue != 0f) leftTime = gap * 0.000001f;
                    left.outTangent = rightTime == 0f ? 0f : rightValue / rightTime;
                    right.inTangent = leftTime == 0f ? 0f : -leftValue / leftTime;
                    left.outWeight = rightTime / gap;
                    right.inWeight = leftTime / gap;
                    left.weightedMode |= WeightedMode.Out;
                    right.weightedMode |= WeightedMode.In;
                }
                else
                    left.outTangent = right.inTangent = (right.value - left.value) / gap;
                keys[leftIndex] = left;
                keys[rightIndex] = right;
            }
            if (keys[0].time > 0f) keys.Insert(0, new Keyframe(0f, keys[0].value));
            if (keys[keys.Count - 1].time < duration)
                keys.Add(new Keyframe(duration, keys[keys.Count - 1].value));
            var curve = new AnimationCurve(keys.ToArray());
            for (int index = 0; index < curve.length; index++)
            {
                AnimationUtility.SetKeyBroken(curve, index, true);
                AnimationUtility.SetKeyLeftTangentMode(curve, index, AnimationUtility.TangentMode.Free);
                AnimationUtility.SetKeyRightTangentMode(curve, index, AnimationUtility.TangentMode.Free);
                Keyframe key = curve[index];
                key.inTangent = keys[index].inTangent;
                key.outTangent = keys[index].outTangent;
                key.inWeight = keys[index].inWeight;
                key.outWeight = keys[index].outWeight;
                key.weightedMode = keys[index].weightedMode;
                curve.MoveKey(index, key);
            }
            return curve;
        }

        private static string SegmentMode(BbKeyframe left, BbKeyframe right)
        {
            if (left.interpolation == "step") return "step";
            if (left.interpolation == "catmullrom" || right.interpolation == "catmullrom") return "catmullrom";
            if (left.interpolation == "bezier" || right.interpolation == "bezier") return "bezier";
            return "linear";
        }

        private static float ScalarValue(Vector3 value, int axis, bool scale)
        {
            float result = GetAxis(value, axis);
            return scale && result == 0f ? 0.00001f : result;
        }

        private void BuildRotationCurves(AnimationClip clip, AnimationTarget target,
            Dictionary<Transform, AnimationTarget> targetsByTransform, float duration, BbAnimation animation)
        {
            var curves = new[] { new AnimationCurve(), new AnimationCurve(), new AnimationCurve(), new AnimationCurve() };
            int sampleRate = Mathf.Max(60, Mathf.Max(1, animation.snapping) * 4);
            var sampleTimes = new SortedSet<float> { 0f, duration };
            var heldTimes = new HashSet<float>();
            // Global rotations also need the ancestor keys that can change their local-space correction.
            var dependencies = new List<AnimationTarget> { target };
            if (target.animator.rotation_global && target.transform != null)
            {
                for (Transform parent = target.transform.parent; parent != null; parent = parent.parent)
                {
                    if (!targetsByTransform.TryGetValue(parent, out AnimationTarget ancestor)) continue;
                    dependencies.Add(ancestor);
                    if (ancestor.animator.rotation_global) break;
                }
            }
            foreach (AnimationTarget candidate in dependencies)
            foreach (BbKeyframe key in candidate.rotation)
            {
                sampleTimes.Add(key.time);
                if (key.time > 0f)
                {
                    float beforeTime = Mathf.Max(0f, key.time - 0.00001f);
                    sampleTimes.Add(beforeTime);
                    heldTimes.Add(beforeTime);
                }
                if (key.data_points.Count > 1 && key.time < duration)
                {
                    sampleTimes.Add(Mathf.Min(duration, key.time + 0.00001f));
                    heldTimes.Add(key.time);
                }
            }
            for (int frame = 1; frame < Mathf.CeilToInt(duration * sampleRate); frame++)
                sampleTimes.Add(frame / (float)sampleRate);
            Quaternion previous = target.restRotation;
            var timeList = new List<float>(sampleTimes);
            foreach (float time in timeList)
            {
                Quaternion value = EvaluateLocalRotation(target, time, targetsByTransform, animation.loop);
                if (Quaternion.Dot(previous, value) < 0f)
                    value = new Quaternion(-value.x, -value.y, -value.z, -value.w);
                previous = value;
                curves[0].AddKey(time, value.x);
                curves[1].AddKey(time, value.y);
                curves[2].AddKey(time, value.z);
                curves[3].AddKey(time, value.w);
            }
            var bindings = new EditorCurveBinding[4];
            for (int axis = 0; axis < 4; axis++)
            {
                AnimationCurve curve = curves[axis];
                for (int index = 0; index < curve.length; index++)
                {
                    AnimationUtility.SetKeyBroken(curve, index, true);
                    AnimationUtility.SetKeyLeftTangentMode(curve, index, AnimationUtility.TangentMode.Linear);
                    AnimationUtility.SetKeyRightTangentMode(curve, index, heldTimes.Contains(curve[index].time)
                        ? AnimationUtility.TangentMode.Constant : AnimationUtility.TangentMode.Linear);
                }
                bindings[axis] = EditorCurveBinding.FloatCurve(target.path, typeof(Transform),
                    "m_LocalRotation." + "xyzw"[axis]);
            }
            // Quaternion signs are already continuous. Reprocessing them here changes stepped tangents.
            AnimationUtility.SetEditorCurves(clip, bindings, curves);
        }

        private static Quaternion EvaluateWorldRotation(Transform transform, float time,
            Dictionary<Transform, AnimationTarget> targets, string loop)
        {
            if (transform == null) return Quaternion.identity;
            bool animated = targets.TryGetValue(transform, out AnimationTarget target);
            if (animated && target.animator.rotation_global) return EvaluateRotation(target, time, loop);
            Quaternion parent = EvaluateWorldRotation(transform.parent, time, targets, loop);
            return parent * (animated ? EvaluateRotation(target, time, loop) : transform.localRotation);
        }

        private static Quaternion EvaluateLocalRotation(AnimationTarget target, float time,
            Dictionary<Transform, AnimationTarget> targets, string loop)
        {
            Quaternion value = EvaluateRotation(target, time, loop);
            if (target.animator.rotation_global && target.transform != null)
                value = Quaternion.Inverse(EvaluateWorldRotation(target.transform.parent, time, targets, loop)) * value;
            return value;
        }

        private static Quaternion EvaluateRotation(AnimationTarget target, float time, string loop)
        {
            List<BbKeyframe> keys = target.rotation;
            if (keys.Count == 0) return target.restRotation;
            FindKeyframes(keys, time, out int before, out int after);
            if (target.animator.quaternion_interpolation && before != after &&
                keys[before].interpolation != "step")
            {
                Quaternion left = target.restRotation * CoordinateConverter.ConvertAnimationRotation(target.rotationOutgoing[before]);
                Quaternion right = target.restRotation * CoordinateConverter.ConvertAnimationRotation(target.rotationIncoming[after]);
                return Quaternion.SlerpUnclamped(left, right, Mathf.InverseLerp(keys[before].time, keys[after].time, time));
            }
            Vector3 offset = new Vector3(EvaluateAxis(target, time, 0, before, after, loop),
                EvaluateAxis(target, time, 1, before, after, loop), EvaluateAxis(target, time, 2, before, after, loop));
            return target.animator.quaternion_interpolation || !target.hasRestEuler
                ? target.restRotation * CoordinateConverter.ConvertAnimationRotation(offset)
                : CoordinateConverter.ConvertAnimationRotation(target.restEuler + offset);
        }

        private static void FindKeyframes(List<BbKeyframe> keys, float time, out int before, out int after)
        {
            // Sorted NaN times retain the previous scan behavior for malformed source data.
            if (keys.Count == 0 || float.IsNaN(time) || float.IsNaN(keys[0].time))
            {
                before = after = 0;
                for (int index = 0; index < keys.Count; index++)
                {
                    if (keys[index].time <= time) before = index;
                    if (keys[index].time >= time) { after = index; return; }
                }
                after = before;
                return;
            }

            int low = 0, high = keys.Count;
            while (low < high)
            {
                int middle = low + (high - low) / 2;
                if (keys[middle].time < time) low = middle + 1;
                else high = middle;
            }
            if (low == keys.Count)
            {
                before = after = keys.Count - 1;
                return;
            }
            after = low;
            // An exact time uses the first equal key; later times use the last duplicate before them.
            before = keys[low].time <= time ? low : Mathf.Max(0, low - 1);
        }

        private static float EvaluateAxis(AnimationTarget target, float time, int axis, int before, int after, string loop)
        {
            List<BbKeyframe> keys = target.rotation;
            if (before == after)
                return GetAxis(time > keys[before].time ? target.rotationOutgoing[before] : target.rotationIncoming[before], axis);
            float left = GetAxis(target.rotationOutgoing[before], axis);
            float right = GetAxis(target.rotationIncoming[after], axis);
            string mode = SegmentMode(keys[before], keys[after]);
            if (mode == "step") return left;
            float alpha = Mathf.InverseLerp(keys[before].time, keys[after].time, time);
            if (mode == "bezier") return EvaluateBezier(keys[before], keys[after], axis, alpha, left, right);
            if (mode == "catmullrom")
            {
                int previous = before - 1, next = after + 1;
                if (loop == "loop" && keys.Count >= 3)
                {
                    if (previous < 0) previous = keys.Count - 2;
                    if (next >= keys.Count) next = 1;
                }
                float p0 = previous >= 0 && keys[before].data_points.Count == 1
                    ? GetAxis(target.rotationOutgoing[previous], axis) : left;
                float p3 = next < keys.Count && keys[after].data_points.Count == 1
                    ? GetAxis(target.rotationIncoming[next], axis) : right;
                float a2 = alpha * alpha, a3 = a2 * alpha;
                return 0.5f * ((2f * left) + (-p0 + right) * alpha +
                    (2f * p0 - 5f * left + 4f * right - p3) * a2 + (-p0 + 3f * left - 3f * right + p3) * a3);
            }
            return Mathf.LerpUnclamped(left, right, alpha);
        }

        private static float EvaluateBezier(BbKeyframe before, BbKeyframe after, int axis, float alpha, float left, float right)
        {
            float gap = Mathf.Max(0.000001f, after.time - before.time);
            float x1 = Mathf.Clamp01(ReadAxis(before.bezier_right_time, axis, 0.1f) / gap);
            float x2 = Mathf.Clamp01(1f + ReadAxis(after.bezier_left_time, axis, -0.1f) / gap);
            float y1 = left + ReadAxis(before.bezier_right_value, axis);
            float y2 = right + ReadAxis(after.bezier_left_value, axis);
            float low = 0f, high = 1f, parameter = alpha;
            for (int iteration = 0; iteration < 20; iteration++)
            {
                parameter = (low + high) * 0.5f;
                if (Cubic(0f, x1, x2, 1f, parameter) < alpha) low = parameter; else high = parameter;
            }
            return Cubic(left, y1, y2, right, parameter);
        }

        private static float Cubic(float p0, float p1, float p2, float p3, float t)
        {
            float inverse = 1f - t;
            return inverse * inverse * inverse * p0 + 3f * inverse * inverse * t * p1 +
                3f * inverse * t * t * p2 + t * t * t * p3;
        }

        private static Vector3 GetPoint(BbKeyframe key, bool last) => key.data_points == null || key.data_points.Count == 0
            ? Vector3.zero : key.data_points[last ? key.data_points.Count - 1 : 0].ToVector3();
        private static float GetAxis(Vector3 value, int axis) => axis == 0 ? value.x : axis == 1 ? value.y : value.z;
        private static float ReadAxis(float[] values, int axis, float defaultValue = 0f) =>
            values != null && axis >= 0 && axis < values.Length ? values[axis] : defaultValue;

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
