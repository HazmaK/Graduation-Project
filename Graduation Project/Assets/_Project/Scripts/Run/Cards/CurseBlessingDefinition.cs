using System;
using RetroFPS.Stats;
using UnityEngine;

namespace RetroFPS.Run.Cards
{
    /// <summary>Localized boon and curse presentation with its stat modifiers.</summary>
    [CreateAssetMenu(menuName = "RetroFPS/Araf/Curse Blessing Card")]
    public sealed class CurseBlessingDefinition : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string nameKey;
        [SerializeField] private string boonKey;
        [SerializeField] private string curseKey;
        [SerializeField] private string flavorKey;
        [SerializeField] private Color accentColor;
        [SerializeField] private StatModifier[] modifiers = Array.Empty<StatModifier>();

        /// <summary>Stable card identity.</summary>
        public string Id => id;
        /// <summary>Localized name key.</summary>
        public string NameKey => nameKey;
        /// <summary>Localized boon key.</summary>
        public string BoonKey => boonKey;
        /// <summary>Localized curse key.</summary>
        public string CurseKey => curseKey;
        /// <summary>Localized flavor text key.</summary>
        public string FlavorKey => flavorKey;
        /// <summary>Presentation accent.</summary>
        public Color AccentColor => accentColor;
        /// <summary>Number of modifiers granted by this card.</summary>
        public int ModifierCount => modifiers != null ? modifiers.Length : 0;

        /// <summary>Returns the modifier at a zero-based index.</summary>
        public StatModifier GetModifier(int index)
        {
            if ((uint)index >= (uint)ModifierCount) throw new ArgumentOutOfRangeException(nameof(index));
            return modifiers[index];
        }

#if UNITY_EDITOR
        /// <summary>Initializes definition data for editor builders and tests.</summary>
        public void Configure(string id, string nameKey, string boonKey, string curseKey, string flavorKey,
            Color accent, StatModifier[] modifiers)
        {
            this.id = id;
            this.nameKey = nameKey;
            this.boonKey = boonKey;
            this.curseKey = curseKey;
            this.flavorKey = flavorKey;
            accentColor = accent;
            this.modifiers = modifiers != null ? (StatModifier[])modifiers.Clone() : Array.Empty<StatModifier>();
        }
#endif
    }
}
