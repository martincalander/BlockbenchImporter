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
                    BuildRotationCurves(clip, transformPath, rotationKeyframes, restRotation);
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

                // Animation position offsets are in Blockbench units
                // They get scaled by the root transform, so keep them in BB units
                Vector3 unityPos = bbPos;

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

            clip.SetCurve(path, typeof(Transform), "localPosition.x", curveX);
            clip.SetCurve(path, typeof(Transform), "localPosition.y", curveY);
            clip.SetCurve(path, typeof(Transform), "localPosition.z", curveZ);
        }

        private void BuildRotationCurves(AnimationClip clip, string path, List<BbKeyframe> keyframes,
            Quaternion restRotation)
        {
            // Use quaternion curves for smooth interpolation without gimbal lock
            var curveX = new AnimationCurve();
            var curveY = new AnimationCurve();
            var curveZ = new AnimationCurve();
            var curveW = new AnimationCurve();

            // Sort keyframes by time
            keyframes.Sort((a, b) => a.time.CompareTo(b.time));

            Quaternion prevQuat = restRotation;

            foreach (var kf in keyframes)
            {
                if (kf.data_points == null || kf.data_points.Count == 0)
                    continue;

                var dp = kf.data_points[0];
                Vector3 bbEuler = dp.ToVector3();

                // Convert rotation from Blockbench to Unity quaternion
                Quaternion animQuat = CoordinateConverter.ConvertAnimationRotation(bbEuler);

                // Combine with rest rotation (animation is additive in Blockbench)
                Quaternion finalQuat = restRotation * animQuat;

                // Ensure shortest path interpolation
                if (Quaternion.Dot(prevQuat, finalQuat) < 0)
                {
                    finalQuat = new Quaternion(-finalQuat.x, -finalQuat.y, -finalQuat.z, -finalQuat.w);
                }

                prevQuat = finalQuat;

                var kfX = new Keyframe(kf.time, finalQuat.x);
                var kfY = new Keyframe(kf.time, finalQuat.y);
                var kfZ = new Keyframe(kf.time, finalQuat.z);
                var kfW = new Keyframe(kf.time, finalQuat.w);

                ApplyInterpolation(ref kfX, kf.interpolation);
                ApplyInterpolation(ref kfY, kf.interpolation);
                ApplyInterpolation(ref kfZ, kf.interpolation);
                ApplyInterpolation(ref kfW, kf.interpolation);

                curveX.AddKey(kfX);
                curveY.AddKey(kfY);
                curveZ.AddKey(kfZ);
                curveW.AddKey(kfW);
            }

            clip.SetCurve(path, typeof(Transform), "localRotation.x", curveX);
            clip.SetCurve(path, typeof(Transform), "localRotation.y", curveY);
            clip.SetCurve(path, typeof(Transform), "localRotation.z", curveZ);
            clip.SetCurve(path, typeof(Transform), "localRotation.w", curveW);
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

                // Scale is typically multiplicative in Blockbench
                // If values are 0, treat as 1 (no scale change)
                Vector3 scaleMultiplier = new Vector3(
                    bbScale.x != 0 ? bbScale.x : 1,
                    bbScale.y != 0 ? bbScale.y : 1,
                    bbScale.z != 0 ? bbScale.z : 1
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

            clip.SetCurve(path, typeof(Transform), "localScale.x", curveX);
            clip.SetCurve(path, typeof(Transform), "localScale.y", curveY);
            clip.SetCurve(path, typeof(Transform), "localScale.z", curveZ);
        }

        private void ApplyInterpolation(ref Keyframe kf, string interpolation)
        {
            switch (interpolation?.ToLowerInvariant())
            {
                case "step":
                case "catmullrom": // Approximate as step for now
                    kf.inTangent = float.PositiveInfinity;
                    kf.outTangent = float.PositiveInfinity;
                    break;
                case "linear":
                default:
                    // Use default linear tangents
                    break;
            }
        }

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
