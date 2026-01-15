using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MartinCalander.BlockbenchImporter
{
    /// <summary>
    /// Builds Unity AnimationClips from Blockbench animation data.
    /// </summary>
    public static class BlockbenchAnimationBuilder
    {
        /// <summary>
        /// Result of building animations
        /// </summary>
        public class AnimationBuildResult
        {
            public List<AnimationClip> Clips;
            public Dictionary<string, string> ElementUuidToPath;
        }

        /// <summary>
        /// Builds all animation clips from a Blockbench model
        /// </summary>
        public static AnimationBuildResult BuildAnimations(BlockbenchModel model, float scaleFactor = 1f)
        {
            var result = new AnimationBuildResult
            {
                Clips = new List<AnimationClip>(),
                ElementUuidToPath = BuildElementUuidMap(model)
            };

            if (model.animations == null || model.animations.Count == 0)
            {
                return result;
            }

            foreach (var bbAnim in model.animations)
            {
                var clip = BuildAnimationClip(bbAnim, model, result.ElementUuidToPath, scaleFactor);
                if (clip != null)
                {
                    result.Clips.Add(clip);
                }
            }

            return result;
        }

        /// <summary>
        /// Builds a mapping from element UUID to hierarchy path
        /// </summary>
        private static Dictionary<string, string> BuildElementUuidMap(BlockbenchModel model)
        {
            var map = new Dictionary<string, string>();
            
            if (model.elements == null) return map;
            
            foreach (var element in model.elements)
            {
                if (!string.IsNullOrEmpty(element.uuid) && !string.IsNullOrEmpty(element.name))
                {
                    // For now, elements are direct children of root
                    // In a full implementation, you'd traverse the outliner hierarchy
                    map[element.uuid] = element.name;
                }
            }
            
            return map;
        }

        /// <summary>
        /// Builds a single AnimationClip from Blockbench animation data
        /// </summary>
        public static AnimationClip BuildAnimationClip(
            BlockbenchAnimation bbAnim, 
            BlockbenchModel model,
            Dictionary<string, string> elementUuidToPath,
            float scaleFactor = 1f)
        {
            if (bbAnim == null) return null;

            var clip = new AnimationClip();
            
            // Clean up animation name (remove "animation." prefix if present)
            string clipName = bbAnim.name ?? "Unnamed";
            if (clipName.StartsWith("animation."))
            {
                clipName = clipName.Substring("animation.".Length);
            }
            clip.name = clipName;
            
            // Configure loop mode
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = bbAnim.loop == "loop";
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            // If there are no animators (keyframe data), create an empty clip
            if (bbAnim.animators == null || bbAnim.animators.Count == 0)
            {
                // Create a minimal clip with the correct length
                // Add a single keyframe at the end to establish the clip length
                if (bbAnim.length > 0)
                {
                    // We'll add a dummy property to establish the length
                    // This is a workaround for empty animations
                    var dummyCurve = new AnimationCurve();
                    dummyCurve.AddKey(0, 0);
                    dummyCurve.AddKey(bbAnim.length, 0);
                    
                    // We can't set curves on the root without a target, so we just set the settings
                    settings = AnimationUtility.GetAnimationClipSettings(clip);
                    settings.stopTime = bbAnim.length;
                    AnimationUtility.SetAnimationClipSettings(clip, settings);
                }
                
                return clip;
            }

            // Build animation curves for each animator
            foreach (var kvp in bbAnim.animators)
            {
                string elementUuid = kvp.Key;
                var animator = kvp.Value;
                
                // Find the element path
                if (!elementUuidToPath.TryGetValue(elementUuid, out string path))
                {
                    // Try using animator name as fallback
                    path = animator.name;
                }
                
                if (string.IsNullOrEmpty(path)) continue;

                BuildAnimatorCurves(clip, path, animator, scaleFactor);
            }

            return clip;
        }

        /// <summary>
        /// Builds animation curves for a single animator (bone/element)
        /// </summary>
        private static void BuildAnimatorCurves(
            AnimationClip clip, 
            string path, 
            BlockbenchAnimator animator,
            float scaleFactor)
        {
            if (animator.keyframes == null || animator.keyframes.Count == 0) return;

            // Group keyframes by channel
            var positionKeyframes = new List<BlockbenchKeyframe>();
            var rotationKeyframes = new List<BlockbenchKeyframe>();
            var scaleKeyframes = new List<BlockbenchKeyframe>();

            foreach (var kf in animator.keyframes)
            {
                switch (kf.channel)
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
                BuildPositionCurves(clip, path, positionKeyframes, scaleFactor);
            }

            // Build rotation curves
            if (rotationKeyframes.Count > 0)
            {
                BuildRotationCurves(clip, path, rotationKeyframes);
            }

            // Build scale curves
            if (scaleKeyframes.Count > 0)
            {
                BuildScaleCurves(clip, path, scaleKeyframes);
            }
        }

        /// <summary>
        /// Builds position animation curves
        /// </summary>
        private static void BuildPositionCurves(
            AnimationClip clip, 
            string path, 
            List<BlockbenchKeyframe> keyframes,
            float scaleFactor)
        {
            var curveX = new AnimationCurve();
            var curveY = new AnimationCurve();
            var curveZ = new AnimationCurve();

            foreach (var kf in keyframes)
            {
                if (kf.data_points == null || kf.data_points.Length < 3) continue;

                // Blockbench uses different coordinate system, convert to Unity
                // Blockbench: X right, Y up, Z forward
                // Unity: X right, Y up, Z forward (but may need sign flip)
                float x = kf.data_points[0] * scaleFactor;
                float y = kf.data_points[1] * scaleFactor;
                float z = -kf.data_points[2] * scaleFactor; // Flip Z for Unity

                curveX.AddKey(CreateKeyframe(kf.time, x, kf.interpolation));
                curveY.AddKey(CreateKeyframe(kf.time, y, kf.interpolation));
                curveZ.AddKey(CreateKeyframe(kf.time, z, kf.interpolation));
            }

            clip.SetCurve(path, typeof(Transform), "localPosition.x", curveX);
            clip.SetCurve(path, typeof(Transform), "localPosition.y", curveY);
            clip.SetCurve(path, typeof(Transform), "localPosition.z", curveZ);
        }

        /// <summary>
        /// Builds rotation animation curves
        /// </summary>
        private static void BuildRotationCurves(
            AnimationClip clip, 
            string path, 
            List<BlockbenchKeyframe> keyframes)
        {
            var curveX = new AnimationCurve();
            var curveY = new AnimationCurve();
            var curveZ = new AnimationCurve();
            var curveW = new AnimationCurve();

            foreach (var kf in keyframes)
            {
                if (kf.data_points == null || kf.data_points.Length < 3) continue;

                // Blockbench rotations are in degrees (Euler angles)
                // Convert to Unity quaternion
                float rotX = kf.data_points[0];
                float rotY = kf.data_points[1];
                float rotZ = -kf.data_points[2]; // Flip Z rotation

                Quaternion rotation = Quaternion.Euler(rotX, -rotY, rotZ);

                curveX.AddKey(CreateKeyframe(kf.time, rotation.x, kf.interpolation));
                curveY.AddKey(CreateKeyframe(kf.time, rotation.y, kf.interpolation));
                curveZ.AddKey(CreateKeyframe(kf.time, rotation.z, kf.interpolation));
                curveW.AddKey(CreateKeyframe(kf.time, rotation.w, kf.interpolation));
            }

            clip.SetCurve(path, typeof(Transform), "localRotation.x", curveX);
            clip.SetCurve(path, typeof(Transform), "localRotation.y", curveY);
            clip.SetCurve(path, typeof(Transform), "localRotation.z", curveZ);
            clip.SetCurve(path, typeof(Transform), "localRotation.w", curveW);
        }

        /// <summary>
        /// Builds scale animation curves
        /// </summary>
        private static void BuildScaleCurves(
            AnimationClip clip, 
            string path, 
            List<BlockbenchKeyframe> keyframes)
        {
            var curveX = new AnimationCurve();
            var curveY = new AnimationCurve();
            var curveZ = new AnimationCurve();

            foreach (var kf in keyframes)
            {
                if (kf.data_points == null || kf.data_points.Length < 3) continue;

                float scaleX = kf.data_points[0];
                float scaleY = kf.data_points[1];
                float scaleZ = kf.data_points[2];

                curveX.AddKey(CreateKeyframe(kf.time, scaleX, kf.interpolation));
                curveY.AddKey(CreateKeyframe(kf.time, scaleY, kf.interpolation));
                curveZ.AddKey(CreateKeyframe(kf.time, scaleZ, kf.interpolation));
            }

            clip.SetCurve(path, typeof(Transform), "localScale.x", curveX);
            clip.SetCurve(path, typeof(Transform), "localScale.y", curveY);
            clip.SetCurve(path, typeof(Transform), "localScale.z", curveZ);
        }

        /// <summary>
        /// Creates a keyframe with the appropriate tangent mode based on interpolation
        /// </summary>
        private static Keyframe CreateKeyframe(float time, float value, string interpolation)
        {
            var keyframe = new Keyframe(time, value);

            switch (interpolation)
            {
                case "step":
                    keyframe.inTangent = float.PositiveInfinity;
                    keyframe.outTangent = float.PositiveInfinity;
                    break;
                case "linear":
                    // Unity will calculate linear tangents automatically
                    keyframe.inTangent = 0;
                    keyframe.outTangent = 0;
                    break;
                case "catmullrom":
                case "bezier":
                default:
                    // Smooth interpolation - let Unity auto-calculate
                    keyframe.inTangent = 0;
                    keyframe.outTangent = 0;
                    break;
            }

            return keyframe;
        }
    }
}
