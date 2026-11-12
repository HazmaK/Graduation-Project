using RetroFPS.UI.Menu;
using UnityEngine;

namespace RetroFPS.UI.HUD
{
    /// <summary>HUD appearance tokens; shared palette and typography remain owned by MenuTheme.</summary>
    [CreateAssetMenu(fileName = "HudTheme", menuName = "RetroFPS/HUD/Theme")]
    public sealed class HudTheme : ScriptableObject
    {
        [SerializeField] private MenuTheme menuTheme;
        /// <summary>Gets the shared menu palette and font asset.</summary>
        public MenuTheme MenuTheme => menuTheme;
        [SerializeField] private float plateAlpha = 0.90f;
        /// <summary>Gets the plate opacity over gameplay.</summary>
        public float PlateAlpha => plateAlpha;
        [SerializeField] private Color border = new Color32(122, 110, 94, 255);
        /// <summary>Gets the decorative plate border colour.</summary>
        public Color Border => border;
        [SerializeField] private Color track = new Color32(42, 32, 32, 255);
        /// <summary>Gets the opaque resource track colour.</summary>
        public Color Track => track;
        [SerializeField] private Color typeBolts = new Color32(0, 230, 255, 255);
        /// <summary>Gets the bolt glyph and fill colour, never text.</summary>
        public Color TypeBolts => typeBolts;
        [SerializeField] private Color typeShells = new Color32(255, 179, 38, 255);
        /// <summary>Gets the shell glyph and fill colour, never text.</summary>
        public Color TypeShells => typeShells;
        [SerializeField] private Color deathOverlay = new Color(0f, 0f, 0f, 0.88f);
        /// <summary>Gets the full-screen death backdrop colour and opacity.</summary>
        public Color DeathOverlay => deathOverlay;
        [SerializeField] private Sprite plate;
        /// <summary>Gets the Plate sprite.</summary>
        public Sprite Plate => plate;
        [SerializeField] private Sprite chip;
        /// <summary>Gets the Chip sprite.</summary>
        public Sprite Chip => chip;
        [SerializeField] private Sprite barTrack;
        /// <summary>Gets the BarTrack sprite.</summary>
        public Sprite BarTrack => barTrack;
        [SerializeField] private Sprite barFill;
        /// <summary>Gets the BarFill sprite.</summary>
        public Sprite BarFill => barFill;
        [SerializeField] private Sprite cross;
        /// <summary>Gets the Cross sprite.</summary>
        public Sprite Cross => cross;
        [SerializeField] private Sprite diamond;
        /// <summary>Gets the Diamond sprite.</summary>
        public Sprite Diamond => diamond;
        [SerializeField] private Sprite shield;
        /// <summary>Gets the Shield sprite.</summary>
        public Sprite Shield => shield;
        [SerializeField] private Sprite hook;
        /// <summary>Gets the Hook sprite.</summary>
        public Sprite Hook => hook;
        [SerializeField] private Sprite weaponBolt;
        /// <summary>Gets the WeaponBolt sprite.</summary>
        public Sprite WeaponBolt => weaponBolt;
        [SerializeField] private Sprite weaponShell;
        /// <summary>Gets the WeaponShell sprite.</summary>
        public Sprite WeaponShell => weaponShell;
        [SerializeField] private Sprite weaponBlade;
        /// <summary>Gets the WeaponBlade sprite.</summary>
        public Sprite WeaponBlade => weaponBlade;
        [SerializeField] private Sprite sigilAraf;
        /// <summary>Gets the SigilAraf sprite.</summary>
        public Sprite SigilAraf => sigilAraf;
        [SerializeField] private Sprite arrow;
        /// <summary>Gets the Arrow sprite.</summary>
        public Sprite Arrow => arrow;
        [SerializeField] private Sprite hatch;
        /// <summary>Gets the Hatch sprite.</summary>
        public Sprite Hatch => hatch;
        [SerializeField] private Sprite deathSeal;
        /// <summary>Gets the DeathSeal sprite.</summary>
        public Sprite DeathSeal => deathSeal;
        [SerializeField] private Sprite deathSealPressed;
        /// <summary>Gets the DeathSealPressed sprite.</summary>
        public Sprite DeathSealPressed => deathSealPressed;
        [SerializeField] private Vector2 healthPlateSize = new Vector2(400f, 150f);
        /// <summary>Gets the bottom-left plate reference size.</summary>
        public Vector2 HealthPlateSize => healthPlateSize;
        [SerializeField] private Vector2 ammoPlateSize = new Vector2(320f, 120f);
        /// <summary>Gets the bottom-right plate reference size.</summary>
        public Vector2 AmmoPlateSize => ammoPlateSize;
        [SerializeField] private Vector2 encounterZoneSize = new Vector2(400f, 190f);
        /// <summary>Gets the top-right reserved zone reference size.</summary>
        public Vector2 EncounterZoneSize => encounterZoneSize;
        [SerializeField] private float encounterPlateWidth = 340f;
        /// <summary>Gets the encounter plate reference width.</summary>
        public float EncounterPlateWidth => encounterPlateWidth;
        [SerializeField] private float explorationPlateHeight = 40f;
        /// <summary>Gets the exploration plate reference height.</summary>
        public float ExplorationPlateHeight => explorationPlateHeight;
        [SerializeField] private float combatPlateHeight = 120f;
        /// <summary>Gets the combat plate reference height.</summary>
        public float CombatPlateHeight => combatPlateHeight;
        [SerializeField] private float arafPlateHeight = 150f;
        /// <summary>Gets the Araf plate reference height.</summary>
        public float ArafPlateHeight => arafPlateHeight;
        [SerializeField] private Vector2 bannerSize = new Vector2(600f, 60f);
        /// <summary>Gets the single-line banner reference size.</summary>
        public Vector2 BannerSize => bannerSize;
        [SerializeField] private Vector2 mergedBannerSize = new Vector2(600f, 96f);
        /// <summary>Gets the two-line banner reference size.</summary>
        public Vector2 MergedBannerSize => mergedBannerSize;
        [SerializeField] private Vector2 deathButtonSize = new Vector2(360f, 72f);
        /// <summary>Gets the death button reference size.</summary>
        public Vector2 DeathButtonSize => deathButtonSize;
        [SerializeField] private Vector2 healthBarSize = new Vector2(376f, 12f);
        /// <summary>Gets the health bar reference size.</summary>
        public Vector2 HealthBarSize => healthBarSize;
        [SerializeField] private Vector2 ammoBarSize = new Vector2(296f, 12f);
        /// <summary>Gets the ammo bar reference size.</summary>
        public Vector2 AmmoBarSize => ammoBarSize;
        [SerializeField] private Vector2 abilityGlyphSize = new Vector2(27f, 27f);
        /// <summary>Gets the health and ability glyph reference size.</summary>
        public Vector2 AbilityGlyphSize => abilityGlyphSize;
        [SerializeField] private Vector2 weaponGlyphSize = new Vector2(36f, 27f);
        /// <summary>Gets the weapon glyph reference size.</summary>
        public Vector2 WeaponGlyphSize => weaponGlyphSize;
        [SerializeField] private Vector2 arafGlyphSize = new Vector2(15f, 21f);
        /// <summary>Gets the Araf sigil reference size.</summary>
        public Vector2 ArafGlyphSize => arafGlyphSize;
        [SerializeField] private Vector2 deathSealSize = new Vector2(27f, 33f);
        /// <summary>Gets the reused menu seal reference size.</summary>
        public Vector2 DeathSealSize => deathSealSize;
        [SerializeField] private float healthNumberSize = 44f;
        /// <summary>Gets the health number size in 1080p reference units.</summary>
        public float HealthNumberSize => healthNumberSize;
        [SerializeField] private float healthMaximumSize = 20f;
        /// <summary>Gets the health maximum size in 1080p reference units.</summary>
        public float HealthMaximumSize => healthMaximumSize;
        [SerializeField] private float overhealNumberSize = 28f;
        /// <summary>Gets the overheal number size in 1080p reference units.</summary>
        public float OverhealNumberSize => overhealNumberSize;
        [SerializeField] private float labelSize = 20f;
        /// <summary>Gets the label size in 1080p reference units.</summary>
        public float LabelSize => labelSize;
        [SerializeField] private float ammoNumberSize = 48f;
        /// <summary>Gets the ammo number size in 1080p reference units.</summary>
        public float AmmoNumberSize => ammoNumberSize;
        [SerializeField] private float ammoMaximumSize = 24f;
        /// <summary>Gets the ammo maximum size in 1080p reference units.</summary>
        public float AmmoMaximumSize => ammoMaximumSize;
        [SerializeField] private float waveNumberSize = 28f;
        /// <summary>Gets the wave number size in 1080p reference units.</summary>
        public float WaveNumberSize => waveNumberSize;
        [SerializeField] private float remainingNumberSize = 24f;
        /// <summary>Gets the remaining number size in 1080p reference units.</summary>
        public float RemainingNumberSize => remainingNumberSize;
        [SerializeField] private float roomNumberSize = 24f;
        /// <summary>Gets the room number size in 1080p reference units.</summary>
        public float RoomNumberSize => roomNumberSize;
        [SerializeField] private float bannerTextSize = 40f;
        /// <summary>Gets the banner text size in 1080p reference units.</summary>
        public float BannerTextSize => bannerTextSize;
        [SerializeField] private float mergedBannerTextSize = 28f;
        /// <summary>Gets the merged banner text size in 1080p reference units.</summary>
        public float MergedBannerTextSize => mergedBannerTextSize;
        [SerializeField] private float deathTitleSize = 64f;
        /// <summary>Gets the death title size in 1080p reference units.</summary>
        public float DeathTitleSize => deathTitleSize;
        [SerializeField] private float deathButtonTextSize = 32f;
        /// <summary>Gets the death button text size in 1080p reference units.</summary>
        public float DeathButtonTextSize => deathButtonTextSize;
        [SerializeField] private float minimumTextSize = 20f;
        /// <summary>Gets the minimum text size in 1080p reference units.</summary>
        public float MinimumTextSize => minimumTextSize;
        [SerializeField] private float labelTracking = 8f;
        /// <summary>Gets the label tracking in 1080p reference units.</summary>
        public float LabelTracking => labelTracking;
        [SerializeField] private float deathTitleTracking = 2f;
        /// <summary>Gets the death title tracking in 1080p reference units.</summary>
        public float DeathTitleTracking => deathTitleTracking;
        [SerializeField] private float artPixelSize = 3f;
        /// <summary>Gets the art pixel size in 1080p reference units.</summary>
        public float ArtPixelSize => artPixelSize;
        [SerializeField] private float platePadding = 12f;
        /// <summary>Gets the plate padding in 1080p reference units.</summary>
        public float PlatePadding => platePadding;
        [SerializeField] private float pipGap = 8f;
        /// <summary>Gets the pip gap in 1080p reference units.</summary>
        public float PipGap => pipGap;
        [SerializeField] private float warningChipSize = 36f;
        /// <summary>Gets the warning chip size in 1080p reference units.</summary>
        public float WarningChipSize => warningChipSize;
        [SerializeField] private float warningTextSize = 28f;
        /// <summary>Gets the warning text size in 1080p reference units.</summary>
        public float WarningTextSize => warningTextSize;
        [SerializeField] private float lowHealthBorderWidth = 9f;
        /// <summary>Gets the low health border width in 1080p reference units.</summary>
        public float LowHealthBorderWidth => lowHealthBorderWidth;
        [SerializeField] private float deathButtonBorderWidth = 6f;
        /// <summary>Gets the death button border width in 1080p reference units.</summary>
        public float DeathButtonBorderWidth => deathButtonBorderWidth;
        [SerializeField] private float remainingBarHeight = 6f;
        /// <summary>Gets the remaining bar height in 1080p reference units.</summary>
        public float RemainingBarHeight => remainingBarHeight;
        [SerializeField] private float bannerRuleHeight = 3f;
        /// <summary>Gets the banner rule height in 1080p reference units.</summary>
        public float BannerRuleHeight => bannerRuleHeight;
        [SerializeField] private float arenaClearedRuleHeight = 6f;
        /// <summary>Gets the arena cleared rule height in 1080p reference units.</summary>
        public float ArenaClearedRuleHeight => arenaClearedRuleHeight;
        [SerializeField] private float deathTitleRuleWidth = 120f;
        /// <summary>Gets the death title rule width in 1080p reference units.</summary>
        public float DeathTitleRuleWidth => deathTitleRuleWidth;
        [SerializeField] private float deathSealGap = 16f;
        /// <summary>Gets the death seal gap in 1080p reference units.</summary>
        public float DeathSealGap => deathSealGap;
    }
}
