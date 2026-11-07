using System;
using System.Collections.Generic;
using UnityEngine;

namespace MartinCalander.BlockbenchImporter
{
    /// <summary>
    /// Receives imported Blockbench sound, particle, and instruction keyframes as animation events.
    /// The importer adds this component to the model root when its animations contain effect keyframes.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BlockbenchAnimationEvents : MonoBehaviour
    {
        /// <summary>The Blockbench effect channel that produced an event.</summary>
        public enum EffectType
        {
            Sound,
            Particle,
            Instruction
        }

        /// <summary>One imported effect keyframe value.</summary>
        [Serializable]
        public struct Effect
        {
            [SerializeField] private EffectType type;
            [SerializeField] private string effect;
            [SerializeField] private string locator;
            [SerializeField] private string script;
            [SerializeField] private string file;

            internal Effect(EffectType type, string effect, string locator, string script, string file)
            {
                this.type = type;
                this.effect = effect ?? string.Empty;
                this.locator = locator ?? string.Empty;
                this.script = script ?? string.Empty;
                this.file = file ?? string.Empty;
            }

            /// <summary>The channel the keyframe belongs to.</summary>
            public EffectType Type => type;

            /// <summary>The sound or particle identifier entered in Blockbench.</summary>
            public string Name => effect ?? string.Empty;

            /// <summary>The locator a particle is attached to, or an empty string.</summary>
            public string Locator => locator ?? string.Empty;

            /// <summary>The particle script or instruction text, or an empty string.</summary>
            public string Script => script ?? string.Empty;

            /// <summary>The effect file path saved by Blockbench, or an empty string.</summary>
            public string File => file ?? string.Empty;
        }

        internal const string EventFunctionName = nameof(OnBlockbenchEffect);

        [SerializeField] private Effect[] effects = Array.Empty<Effect>();

        /// <summary>The effects that the imported animation events refer to by index.</summary>
        public IReadOnlyList<Effect> Effects => effects;

        /// <summary>Raised when an animation reaches an imported effect keyframe.</summary>
        public event Action<Effect> EffectTriggered;

        /// <summary>Called by imported animation events. Use <see cref="EffectTriggered"/> to respond to effects.</summary>
        public void OnBlockbenchEffect(int index)
        {
            if (effects != null && (uint)index < (uint)effects.Length)
                EffectTriggered?.Invoke(effects[index]);
        }

        internal void SetEffects(Effect[] value) => effects = value ?? Array.Empty<Effect>();
    }
}
