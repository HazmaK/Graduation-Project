using System;
using RetroFPS.Combat;
using RetroFPS.Presentation;
using RetroFPS.Run;
using RetroFPS.Run.Labyrinth;
using RetroFPS.Settings;
using RetroFPS.Weapons;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RetroFPS.UI.HUD
{
    /// <summary>Player-owned source injection, subscriptions and full snapshots for the nested HUD.</summary>
    [DisallowMultipleComponent]
    public sealed class HudBindings : MonoBehaviour
    {
        [SerializeField] private PlayerHealth health;
        [SerializeField] private PlayerMovement movement;
        [SerializeField] private PlayerParryController parry;
        [SerializeField] private WeaponController weapons;
        [SerializeField] private PlayerAmmoInventory ammo;
        [SerializeField] private HudConfig config;
        [SerializeField] private GameObject hud;
        [SerializeField] private HealthHudView healthView;
        [SerializeField] private AbilityHudView abilityView;
        [SerializeField] private WeaponAmmoHudView weaponView;
        [SerializeField] private EncounterRunHudView encounterView;
        [SerializeField] private HudBannerView bannerView;
        [SerializeField] private TrueDeathView deathView;
        private WaveSpawnDirector director;
        private bool started, subscribed, araf, suppressBanners;
        private Action encounterStarted, encounterCompleted, encounterRetried;
        private Action<int, int, string> waveStarted;
        private Action<int, int> remainingChanged;
        private Action<int> waveCompleted;
        /// <summary>Gets the shared scalar configuration assigned to gameplay feedback too.</summary>
        public HudConfig Config => config;

        private void Awake()
        {
            HudNumberCache.Prewarm();
            encounterStarted = EncounterStarted; encounterCompleted = EncounterCompleted; encounterRetried = EncounterRetried;
            waveStarted = WaveStarted; remainingChanged = RemainingChanged; waveCompleted = WaveCompleted;
            // Complete the global TMP pass before view initialization and before OnEnable subscribes to gameplay.
            if (hud != null)
            {
                // ForceMeshUpdate cannot initialize TMP components whose Awake has never run.
                // Wake every reserved/hidden cell for this startup pass, then restore authored visibility.
                Transform[] nodes = hud.GetComponentsInChildren<Transform>(true);
                bool[] activeStates = new bool[nodes.Length];
                for (int i = 0; i < nodes.Length; i++) activeStates[i] = nodes[i].gameObject.activeSelf;
                for (int i = 0; i < nodes.Length; i++) nodes[i].gameObject.SetActive(true);
                TMP_Text[] labels = hud.GetComponentsInChildren<TMP_Text>(true);
                for (int i = 0; i < labels.Length; i++) HudText.Prewarm(labels[i], labels[i].text);
                for (int i = nodes.Length - 1; i >= 0; i--) nodes[i].gameObject.SetActive(activeStates[i]);
            }
            PrepareViews();
        }
        private void OnEnable()
        {
            Subscribe();
            if (started) Snapshot();
        }
        private void Start()
        {
            // Recovery is attempted once. Authored Player.prefab references avoid these searches entirely.
            // Detach before resolving missing sources, then attach the final source set below.
            if (health == null || movement == null || parry == null || weapons == null || ammo == null) OnDisable();
            if (health == null) health = FindAnyObjectByType<PlayerHealth>();
            if (movement == null) movement = FindAnyObjectByType<PlayerMovement>();
            if (parry == null) parry = FindAnyObjectByType<PlayerParryController>();
            if (weapons == null) weapons = FindAnyObjectByType<WeaponController>();
            if (ammo == null) ammo = FindAnyObjectByType<PlayerAmmoInventory>();
            if (health == null || movement == null || parry == null || weapons == null || ammo == null || config == null)
                Debug.LogWarning("[CombatHud] Missing local sources/config; dependent content is hidden. No further searches will run.", this);
            if (healthView != null) { healthView.Initialize(); healthView.gameObject.SetActive(health != null); }
            abilityView?.Initialize(movement, parry); weaponView?.Initialize(ammo);
            encounterView?.Initialize(); bannerView?.Initialize(); deathView?.Initialize();
            started = true; Subscribe(); Snapshot();
        }
        private void PrepareViews()
        {
            healthView?.Initialize(); abilityView?.Initialize(movement, parry); weaponView?.Initialize(ammo);
            encounterView?.Initialize(); bannerView?.Initialize(); deathView?.Initialize();
        }
        private void OnDisable()
        {
            if (!subscribed) return;
            subscribed = false;
            if (health != null) { health.OnHealthChanged -= HealthChanged; health.OnDied -= HealthSnapshot; health.OnRevived -= HealthSnapshot; }
            if (movement != null) movement.OnDashChargesChanged -= DashChanged;
            if (parry != null) parry.OnParrySuccess -= ParrySuccess;
            if (weapons != null) weapons.OnWeaponSwitched -= WeaponChanged;
            if (ammo != null) { ammo.OnAmmoChanged -= AmmoChanged; ammo.OnAmmoCollected -= AmmoCollected; }
            WaveSpawnDirector.OnActiveDirectorChanged -= BindDirector;
            RunEvents.OnRunChanged -= RunChanged; RunEvents.OnPhaseChanged -= PhaseChanged;
            RunEvents.OnArafChargeChanged -= ChargeChanged; RunEvents.OnArafRoomsChanged -= RoomsChanged;
            RunEvents.OnTrueDeath -= TrueDeath; GameSettings.Changed -= SettingsChanged;
            SceneManager.activeSceneChanged -= SceneChanged;
            BindDirector(null);
            bannerView?.Clear();
        }

        private void Subscribe()
        {
            if (subscribed) return;
            subscribed = true;
            if (health != null) { health.OnHealthChanged += HealthChanged; health.OnDied += HealthSnapshot; health.OnRevived += HealthSnapshot; }
            if (movement != null) movement.OnDashChargesChanged += DashChanged;
            if (parry != null) parry.OnParrySuccess += ParrySuccess;
            if (weapons != null) weapons.OnWeaponSwitched += WeaponChanged;
            if (ammo != null) { ammo.OnAmmoChanged += AmmoChanged; ammo.OnAmmoCollected += AmmoCollected; }
            WaveSpawnDirector.OnActiveDirectorChanged += BindDirector;
            RunEvents.OnRunChanged += RunChanged; RunEvents.OnPhaseChanged += PhaseChanged;
            RunEvents.OnArafChargeChanged += ChargeChanged; RunEvents.OnArafRoomsChanged += RoomsChanged;
            RunEvents.OnTrueDeath += TrueDeath; GameSettings.Changed += SettingsChanged;
            SceneManager.activeSceneChanged += SceneChanged;
        }

        private void Snapshot()
        {
            SettingsChanged(GameSettings.Current); HealthSnapshot();
            if (movement != null) DashChanged(movement.CurrentDashCharges, movement.MaxDashCharges);
            WeaponChanged(weapons != null ? weapons.CurrentSlot : WeaponSlot.RepeaterArbalest);
            RunChanged(RunEvents.CurrentRun); BindDirector(WaveSpawnDirector.ActiveDirector);
            SnapshotRooms();
        }
        private void HealthSnapshot() { if (health != null) HealthChanged(health.CurrentHealth, health.MaxHealth); }
        private void HealthChanged(float current, float max) => healthView?.Render(current, max);
        private void DashChanged(int current, int max) => abilityView?.SetDash(current, max);
        private void ParrySuccess(bool projectile) => abilityView?.ParrySucceeded(projectile);
        private void AmmoChanged(AmmoType type, int current, int max) => weaponView?.AmmoChanged(type, current, max);
        private void AmmoCollected(AmmoType type, int added) => weaponView?.AmmoCollected(type, added);
        private void WeaponChanged(WeaponSlot slot)
        {
            WeaponBase weapon = weapons != null ? weapons.ActiveWeapon : null;
            abilityView?.SetWeapon(weapon); weaponView?.SetWeapon(weapon);
        }

        private void BindDirector(WaveSpawnDirector next)
        {
            if (director == next) { SnapshotDirector(); return; }
            if (director != null)
            {
                director.OnEncounterStarted -= encounterStarted; director.OnEncounterCompleted -= encounterCompleted;
                director.OnEncounterRetried -= encounterRetried; director.OnWaveStarted -= waveStarted;
                director.OnWaveCompleted -= waveCompleted; director.OnEnemiesRemainingChanged -= remainingChanged;
            }
            director = next; bannerView?.Clear();
            if (director != null)
            {
                director.OnEncounterStarted += encounterStarted; director.OnEncounterCompleted += encounterCompleted;
                director.OnEncounterRetried += encounterRetried; director.OnWaveStarted += waveStarted;
                director.OnWaveCompleted += waveCompleted; director.OnEnemiesRemainingChanged += remainingChanged;
            }
            SnapshotDirector();
        }
        private void SnapshotDirector()
        {
            if (director == null) { encounterView?.SetEncounter(false); return; }
            encounterView?.SetWave(Mathf.Max(0, director.CurrentWaveIndex + 1), director.TotalWaves);
            encounterView?.SetRemaining(director.EnemiesRemainingInWave, director.TotalEnemiesInCurrentWave);
            encounterView?.SetEncounter(!araf && director.State != EncounterState.Inactive && director.State != EncounterState.EncounterCleared);
        }
        private void EncounterStarted() { encounterView?.SetEncounter(!araf); Announce(HudBannerKind.ArenaSealed); }
        private void WaveStarted(int wave, int total, string name)
        { encounterView?.SetWave(wave, total); encounterView?.SetEncounter(!araf); Announce(HudBannerKind.Wave, wave); }
        private void RemainingChanged(int current, int total) => encounterView?.SetRemaining(current, total);
        private void WaveCompleted(int wave) => Announce(HudBannerKind.WaveCleared, wave);
        private void EncounterCompleted() { encounterView?.EncounterCompleted(); Announce(HudBannerKind.ArenaCleared); }
        private void EncounterRetried() { bannerView?.Clear(); SnapshotDirector(); }
        private void Announce(HudBannerKind kind, int wave = 0)
        { if (!araf && !suppressBanners) bannerView?.Enqueue(kind, wave); }
        private void RunChanged(RunState run)
        {
            suppressBanners = deathView != null && deathView.IsOpen;
            encounterView?.SetRun(run);
            if (run == null) { araf = false; SnapshotDirector(); }
            else PhaseChanged(run.Phase);
        }
        private void PhaseChanged(RunPhase phase)
        {
            if (phase == RunPhase.Tower) araf = false;
            else if (phase == RunPhase.InAraf || phase == RunPhase.EscapingAraf) araf = true;
            encounterView?.SetPhase(phase);
            if (araf) { bannerView?.Clear(); encounterView?.SetEncounter(false); SnapshotRooms(); }
            else if (phase == RunPhase.Tower) SnapshotDirector();
        }
        private void ChargeChanged(bool used) => encounterView?.SetCharge(used);
        private void RoomsChanged(int revealed, int total, bool exit) => encounterView?.SetRooms(revealed, total, exit);
        private void SnapshotRooms()
        {
            ArafLabyrinthDirector labyrinth = ArafLabyrinthDirector.Active;
            if (labyrinth != null) RoomsChanged(labyrinth.RevealedRooms, labyrinth.TotalRooms, labyrinth.ExitRevealed);
        }
        private void TrueDeath() { suppressBanners = true; bannerView?.Clear(); deathView?.Show(); }
        private void SceneChanged(Scene previous, Scene next) => bannerView?.Clear();
        private void SettingsChanged(GameSettingsData data)
        {
            healthView?.SetReduceMotion(data.ReduceMotion); abilityView?.SetReduceMotion(data.ReduceMotion);
            weaponView?.SetReduceMotion(data.ReduceMotion); encounterView?.SetReduceMotion(data.ReduceMotion);
            bannerView?.SetReduceMotion(data.ReduceMotion); deathView?.SetReduceMotion(data.ReduceMotion);
        }
    }

    /// <summary>Shared fixed-buffer text writes; all capacity and glyph work happens at initialization.</summary>
    internal static class HudText
    {
        private static readonly string WarmText = ("ÇçĞğİıÖöŞşÜü%/+!—'[]0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz").PadRight(256, '8');
        // Initialization only: the owning view must mark its text dirty after this restores authored text.
        internal static void Prewarm(TMP_Text label, string sample = null)
        {
            if (label == null) return;
            string original = label.text;
            label.vertexBufferAutoSizeReduction = false;
            label.text = WarmText; label.ForceMeshUpdate(true, true);
            if (!string.IsNullOrEmpty(sample)) { label.text = sample; label.ForceMeshUpdate(true, true); }
            label.text = original; label.ForceMeshUpdate(true, true);
        }
        internal static void Number(TMP_Text label, int value, char[] buffer)
        {
            if (label == null) return;
            if ((uint)value < 1000u) label.text = HudNumberCache.GetInteger(value);
            else label.SetCharArray(buffer, 0, HudNumberCache.WriteInteger(value, buffer));
        }
        internal static void Template(TMP_Text label, string template, int first, int second, char[] buffer)
        {
            if (label == null || template == null) return;
            int count = HudNumberCache.WriteTemplate(template, first, second, buffer);
            label.SetCharArray(buffer, 0, count);
        }
    }
}
