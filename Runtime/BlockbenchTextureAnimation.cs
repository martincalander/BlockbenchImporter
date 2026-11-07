using System;
using UnityEngine;

namespace MartinCalander.BlockbenchImporter
{
    /// <summary>
    /// Plays imported Blockbench flipbook textures while the application is playing.
    /// Each animated material slot shows one frame at a time through its renderer's per-material property block,
    /// so shared materials and assets are never modified. Edit mode shows the first frame stored in the material.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BlockbenchTextureAnimation : MonoBehaviour
    {
        [Serializable]
        internal struct Flipbook
        {
            public string scaleOffsetProperty;
            public int frameCount;
            public int[] frames;
            public float[] durations;
        }

        [Serializable]
        internal struct Slot
        {
            public Renderer renderer;
            public int materialIndex;
            public int flipbook;
        }

        [SerializeField] private Flipbook[] flipbooks = Array.Empty<Flipbook>();
        [SerializeField] private Slot[] slots = Array.Empty<Slot>();
        [SerializeField, Min(0f)] private float speed = 1f;

        private MaterialPropertyBlock block;
        private int[] shownFrames = Array.Empty<int>();
        private bool[] changed = Array.Empty<bool>();
        private int[] propertyIds = Array.Empty<int>();
        private float time;

        /// <summary>Playback speed multiplier. Zero pauses every flipbook.</summary>
        public float Speed
        {
            get => speed;
            set => speed = Mathf.Max(0f, value);
        }

        /// <summary>Playback position in seconds since the component was enabled.</summary>
        public float PlaybackTime
        {
            get => time;
            set
            {
                time = Mathf.Max(0f, value);
                ApplyFrames(false);
            }
        }

        private void OnEnable()
        {
            time = 0f;
            block ??= new MaterialPropertyBlock();
            shownFrames = new int[flipbooks.Length];
            changed = new bool[flipbooks.Length];
            propertyIds = new int[flipbooks.Length];
            for (int index = 0; index < flipbooks.Length; index++)
            {
                shownFrames[index] = -1;
                propertyIds[index] = Shader.PropertyToID(flipbooks[index].scaleOffsetProperty ?? "_MainTex_ST");
            }
            if (Application.isPlaying)
                ApplyFrames(true);
        }

        private void OnDisable()
        {
            // The animated slots own their property blocks; clearing them restores the material's first frame.
            foreach (Slot slot in slots)
                if (slot.renderer != null)
                    slot.renderer.SetPropertyBlock(null, slot.materialIndex);
            shownFrames = Array.Empty<int>();
        }

        private void Update()
        {
            if (!Application.isPlaying || speed <= 0f)
                return;
            time += Time.deltaTime * speed;
            ApplyFrames(false);
        }

        private void ApplyFrames(bool force)
        {
            if (shownFrames.Length != flipbooks.Length)
                return;
            for (int index = 0; index < flipbooks.Length; index++)
            {
                int frame = FrameAt(flipbooks[index], time);
                changed[index] = force || frame != shownFrames[index];
                shownFrames[index] = frame;
            }
            foreach (Slot slot in slots)
            {
                if (slot.renderer == null || (uint)slot.flipbook >= (uint)flipbooks.Length || !changed[slot.flipbook])
                    continue;
                slot.renderer.GetPropertyBlock(block, slot.materialIndex);
                block.SetVector(propertyIds[slot.flipbook],
                    ScaleOffset(shownFrames[slot.flipbook], flipbooks[slot.flipbook].frameCount));
                slot.renderer.SetPropertyBlock(block, slot.materialIndex);
            }
        }

        /// <summary>Returns the texture frame a flipbook shows at a playback time, in seconds.</summary>
        internal static int FrameAt(Flipbook flipbook, float seconds)
        {
            int[] frames = flipbook.frames;
            if (frames == null || frames.Length == 0 || flipbook.frameCount <= 0)
                return 0;
            float total = 0f;
            for (int index = 0; index < frames.Length; index++)
                total += StepDuration(flipbook, index);
            if (total <= 0f)
                return Mathf.Clamp(frames[0], 0, flipbook.frameCount - 1);
            float position = Mathf.Repeat(seconds, total);
            for (int index = 0; index < frames.Length; index++)
            {
                position -= StepDuration(flipbook, index);
                if (position < 0f)
                    return Mathf.Clamp(frames[index], 0, flipbook.frameCount - 1);
            }
            return Mathf.Clamp(frames[frames.Length - 1], 0, flipbook.frameCount - 1);
        }

        /// <summary>Texture scale and offset that show one frame of a vertical flipbook strip, first frame at the top.</summary>
        internal static Vector4 ScaleOffset(int frame, int frameCount)
        {
            frameCount = Mathf.Max(1, frameCount);
            return new Vector4(1f, 1f / frameCount, 0f, 1f - (frame + 1f) / frameCount);
        }

        private static float StepDuration(Flipbook flipbook, int step) =>
            flipbook.durations != null && step < flipbook.durations.Length ? Mathf.Max(0f, flipbook.durations[step]) : 0f;

        internal void SetFlipbooks(Flipbook[] value, Slot[] targets)
        {
            flipbooks = value ?? Array.Empty<Flipbook>();
            slots = targets ?? Array.Empty<Slot>();
        }
    }
}
