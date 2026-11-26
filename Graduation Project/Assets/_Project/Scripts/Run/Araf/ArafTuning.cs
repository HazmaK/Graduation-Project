using System;
using RetroFPS.Run.Cards;
using UnityEngine;

namespace RetroFPS.Run.Araf
{
    /// <summary>Shared settings for the Araf wound, seals and awakening progression.</summary>
    [CreateAssetMenu(menuName = "RetroFPS/Araf/Tuning", fileName = "ArafTuning_New")]
    public sealed class ArafTuning : ScriptableObject
    {
        /// <summary>Wound used when no tuning asset is assigned.</summary>
        public const float DefaultWoundMaxHealth = 25f;

        [SerializeField] private float[] fogStartByLevel = { 20f, 16f, 12f, 8f };
        [SerializeField] private float[] fogEndByLevel = { 140f, 115f, 90f, 70f };
        [SerializeField] private Color fogColor = new Color(0.10f, 0.05f, 0.16f);
        [SerializeField] private int debrisCount = 40;
        [SerializeField] private float debrisMargin = 25f;
        [SerializeField] private float debrisShellThickness = 60f;

        [SerializeField] private float woundMaxHealth = DefaultWoundMaxHealth;
        [SerializeField] private int sealCount = 3;
        [SerializeField] private int maxAwakening = 3;
        [SerializeField] private int[] patrolsAddedAtLevel = { 2, 2, 3, 3 };
        [SerializeField] private int maxAlivePatrols = 12;
        [SerializeField] private int[] waveSizeByLevel = { 0, 3, 4, 5 };
        [SerializeField] private float minSpawnDistance = 14f;
        [SerializeField] private int[] islandEyesAddedAtLevel = { 0, 2, 2, 3 };
        [SerializeField] private int[] islandPenitentsAddedAtLevel = { 0, 0, 1, 1 };
        [SerializeField] private int maxAliveIslandEnemies = 10;
        [SerializeField] private float eyeHoverHeight = 3f;
        [SerializeField] private int optionalArenasPerWing = 1;
        [SerializeField] private CardRarityMultipliers[] arenaRarityByAwakening =
        {
            new CardRarityMultipliers(1f, 0f, 0f),
            new CardRarityMultipliers(1f, 0.5f, 0f),
            new CardRarityMultipliers(1f, 1f, 0f),
            new CardRarityMultipliers(0.5f, 1.5f, 0.5f)
        };

        /// <summary>Number of optional arenas requested in each wing.</summary>
        public int OptionalArenasPerWing => Mathf.Max(0, optionalArenasPerWing);

        /// <summary>Returns clamped awakening rarity weights for optional arenas, or neutral weights if empty.</summary>
        public CardRarityMultipliers GetArenaRarity(int awakening)
            => arenaRarityByAwakening == null || arenaRarityByAwakening.Length == 0
                ? CardRarityMultipliers.One : arenaRarityByAwakening[Mathf.Clamp(awakening, 0, arenaRarityByAwakening.Length - 1)];
        [SerializeField] private CardRarityMultipliers[] sealRarity =
        {
            new CardRarityMultipliers(1f, 0f, 0f),
            new CardRarityMultipliers(1f, 1f, 0f),
            new CardRarityMultipliers(0.5f, 1.5f, 1f)
        };

        /// <summary>Linear fog tint while in Araf.</summary>
        public Color FogColor => fogColor;
        /// <summary>Number of cosmetic debris pieces.</summary>
        public int DebrisCount => debrisCount;
        /// <summary>Minimum horizontal clearance from the world bounds.</summary>
        public float DebrisMargin => debrisMargin;
        /// <summary>Width of the horizontal debris shell.</summary>
        public float DebrisShellThickness => debrisShellThickness;

        /// <summary>Fog start at this awakening; out-of-range levels use the last entry, empty settings use zero.</summary>
        public float GetFogStart(int level) => GetFogValue(fogStartByLevel, level);
        /// <summary>Fog end at this awakening; out-of-range levels use the last entry, empty settings use zero.</summary>
        public float GetFogEnd(int level) => GetFogValue(fogEndByLevel, level);

        private static float GetFogValue(float[] values, int level)
        {
            if (values == null || values.Length == 0) return 0f;
            return values[(uint)level < (uint)values.Length ? level : values.Length - 1];
        }

        /// <summary>Flat maximum-health penalty while in Araf.</summary>
        public float WoundMaxHealth => woundMaxHealth;
        /// <summary>Number of seals required to open the portal.</summary>
        public int SealCount => sealCount;
        /// <summary>Maximum awakening level.</summary>
        public int MaxAwakening => maxAwakening;

        /// <summary>Maximum number of living corridor patrols.</summary>
        public int MaxAlivePatrols => maxAlivePatrols;
        /// <summary>Minimum spawn distance from the player in metres.</summary>
        public float MinSpawnDistance => minSpawnDistance;

        /// <summary>Maximum number of living island enemies across both types.</summary>
        public int MaxAliveIslandEnemies => maxAliveIslandEnemies;
        /// <summary>Eye spawn height above the platform surface.</summary>
        public float EyeHoverHeight => eyeHoverHeight;

        /// <summary>Returns island eye additions, or zero outside the configured range.</summary>
        public int GetIslandEyesAddedAtLevel(int level)
            => islandEyesAddedAtLevel != null && (uint)level < (uint)islandEyesAddedAtLevel.Length
                ? islandEyesAddedAtLevel[level] : 0;

        /// <summary>Returns island penitent additions, or zero outside the configured range.</summary>
        public int GetIslandPenitentsAddedAtLevel(int level)
            => islandPenitentsAddedAtLevel != null && (uint)level < (uint)islandPenitentsAddedAtLevel.Length
                ? islandPenitentsAddedAtLevel[level] : 0;

        /// <summary>Returns patrol additions at this level, or zero outside the configured range.</summary>
        public int GetPatrolsAddedAtLevel(int level)
            => patrolsAddedAtLevel != null && (uint)level < (uint)patrolsAddedAtLevel.Length
                ? patrolsAddedAtLevel[level] : 0;

        /// <summary>Returns wave size at this level; out-of-range levels use the last entry, empty settings use zero.</summary>
        public int GetWaveSize(int level)
        {
            if (waveSizeByLevel == null || waveSizeByLevel.Length == 0) return 0;
            return waveSizeByLevel[(uint)level < (uint)waveSizeByLevel.Length ? level : waveSizeByLevel.Length - 1];
        }

        /// <summary>Returns the clamped seal-order weights, or neutral weights when none are configured.</summary>
        public CardRarityMultipliers GetSealRarity(int sealIndex)
        {
            if (sealRarity == null || sealRarity.Length == 0) return CardRarityMultipliers.One;
            return sealRarity[Mathf.Clamp(sealIndex, 0, sealRarity.Length - 1)];
        }

#if UNITY_EDITOR
        /// <summary>Initializes arena settings for editor builders and tests, copying supplied weights.</summary>
        public void ConfigureArenas(int optionalPerWing, CardRarityMultipliers[] rarity)
        {
            optionalArenasPerWing = Mathf.Max(0, optionalPerWing);
            arenaRarityByAwakening = rarity != null ? (CardRarityMultipliers[])rarity.Clone() : Array.Empty<CardRarityMultipliers>();
        }

        /// <summary>Initializes atmosphere settings for editor builders and tests, copying supplied arrays.</summary>
        public void ConfigureAtmosphere(float[] fogStartByLevel, float[] fogEndByLevel, Color fogColor,
            int debrisCount, float debrisMargin, float debrisShellThickness)
        {
            this.fogStartByLevel = fogStartByLevel != null ? (float[])fogStartByLevel.Clone() : Array.Empty<float>();
            this.fogEndByLevel = fogEndByLevel != null ? (float[])fogEndByLevel.Clone() : Array.Empty<float>();
            this.fogColor = fogColor;
            this.debrisCount = debrisCount;
            this.debrisMargin = debrisMargin;
            this.debrisShellThickness = debrisShellThickness;
        }

        /// <summary>Initializes island threat settings for editor builders and tests, copying supplied arrays.</summary>
        public void ConfigureIslandThreat(int[] eyesAddedAtLevel, int[] penitentsAddedAtLevel, int maxAlive, float eyeHoverHeight)
        {
            islandEyesAddedAtLevel = eyesAddedAtLevel != null ? (int[])eyesAddedAtLevel.Clone() : Array.Empty<int>();
            islandPenitentsAddedAtLevel = penitentsAddedAtLevel != null ? (int[])penitentsAddedAtLevel.Clone() : Array.Empty<int>();
            maxAliveIslandEnemies = maxAlive;
            this.eyeHoverHeight = eyeHoverHeight;
        }

        /// <summary>Initializes threat settings for editor builders and tests, copying supplied arrays.</summary>
        public void ConfigureThreat(int[] patrolsAddedAtLevel, int maxAlivePatrols, int[] waveSizeByLevel, float minSpawnDistance)
        {
            this.patrolsAddedAtLevel = patrolsAddedAtLevel != null ? (int[])patrolsAddedAtLevel.Clone() : Array.Empty<int>();
            this.maxAlivePatrols = maxAlivePatrols;
            this.waveSizeByLevel = waveSizeByLevel != null ? (int[])waveSizeByLevel.Clone() : Array.Empty<int>();
            this.minSpawnDistance = minSpawnDistance;
        }

        /// <summary>Initializes tuning for editor builders and tests, copying the supplied weights.</summary>
        public void Configure(float woundMaxHealth, int sealCount, int maxAwakening, CardRarityMultipliers[] sealRarity)
        {
            this.woundMaxHealth = woundMaxHealth;
            this.sealCount = sealCount;
            this.maxAwakening = maxAwakening;
            this.sealRarity = sealRarity != null ? (CardRarityMultipliers[])sealRarity.Clone()
                : Array.Empty<CardRarityMultipliers>();
        }
#endif
    }
}
