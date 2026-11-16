using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using RetroFPS.Combat;

namespace RetroFPS
{
    public enum WeaponSlot
    {
        RepeaterArbalest = 0, // Slot 1: Hızlı Menzilli Arbalest / Nailgun
        ExecutionerBlade = 1, // Slot 2: Ağır Yakın Dövüş Kılıcı / Cleaver
        GothicShotgun = 2     // Slot 3: Gotik Çift Namlulu Pompalı & Grapple Meat Hook
    }
}

namespace RetroFPS.Weapons
{
    /// <summary>
    /// Silah yuvalarını (Slots), silah değiştirme mantığını ve oyuncu ateş girdilerini yöneten
    /// yüksek seviyeli silah orkestratörü.
    /// Referanslar boş olsa dahi kendini otomatik kurar, atış girdilerini asla yutmaz (Bulletproof).
    /// </summary>
    [RequireComponent(typeof(PlayerLook))]
    [DisallowMultipleComponent]
    public class WeaponController : MonoBehaviour
    {
        [Header("Silah Görsel Objeleri")]
        [SerializeField] private GameObject rangedWeaponVisual;
        [SerializeField] private GameObject meleeWeaponVisual;
        [SerializeField] private GameObject shotgunWeaponVisual;
        [SerializeField] private Transform firePoint;

        [Header("Kayıtlı Silahlar")]
        [SerializeField] private WeaponBase[] registeredWeapons;

        [Header("Counter-Verdict Quick Blade")]
        [SerializeField, Min(0f)] private float quickBladeDuration = 0.25f;

        private PlayerLook _look;
        private Camera _cam;
        private readonly Dictionary<WeaponSlot, WeaponBase> _weaponMap = new Dictionary<WeaponSlot, WeaponBase>();
        private readonly List<WeaponBase> _tickWeapons = new List<WeaponBase>(3);
        private WeaponBase _activeWeapon;
        private bool _requireFreshFire;
        private float _quickBladeTimer;
        private int _quickBladeStartFrame = -1;
        private bool _isQuickBladeActive;
        private ExecutionerBladeWeapon _quickBladeWeapon;
        private GameObject _quickBladeSourceVisual;
        private GameObject _quickBladeShownVisual;

        /// <summary>Whether the quick blade temporarily owns weapon visuals and fire/switch input.</summary>
        public bool IsQuickBladeActive => _isQuickBladeActive;

        public event Action<bool> OnWeaponHit;
        public event Action<WeaponSlot> OnWeaponSwitched;

        public WeaponSlot CurrentSlot => _activeWeapon != null ? _activeWeapon.Slot : WeaponSlot.RepeaterArbalest;
        public WeaponBase ActiveWeapon => _activeWeapon;

        private void Awake()
        {
            SetupReferences();
            InitializeWeapons();
            EquipWeapon(WeaponSlot.RepeaterArbalest);
        }

        private void Start()
        {
            SetupReferences();
            if (_activeWeapon == null)
            {
                InitializeWeapons();
                EquipWeapon(WeaponSlot.RepeaterArbalest);
            }
        }

        private void SetupReferences()
        {
            if (_look == null) _look = GetComponent<PlayerLook>();
            if (_cam == null && _look != null) _cam = _look.PlayerCam;
            if (_cam == null) _cam = Camera.main ?? FindAnyObjectByType<Camera>();

            // Eğer görsel referanslar Inspector'da boşsa alt objelerden otomatik bağla
            if (rangedWeaponVisual == null)
            {
                Transform found = FindDeepChild(transform, "Weapon_Crossbow");
                if (found != null) rangedWeaponVisual = found.gameObject;
            }

            if (meleeWeaponVisual == null)
            {
                Transform found = FindDeepChild(transform, "Weapon_Greatsword");
                if (found != null) meleeWeaponVisual = found.gameObject;
            }

            if (shotgunWeaponVisual == null)
            {
                Transform found = FindDeepChild(transform, "Weapon_Shotgun")
                               ?? FindDeepChild(transform, "Weapon_GothicShotgun");
                if (found != null) shotgunWeaponVisual = found.gameObject;
            }

            if (firePoint == null && rangedWeaponVisual != null)
            {
                Transform found = rangedWeaponVisual.transform.Find("FirePoint");
                if (found != null) firePoint = found;
            }
        }

        private Transform FindDeepChild(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name) return child;
                Transform result = FindDeepChild(child, name);
                if (result != null) return result;
            }
            return null;
        }

        private void InitializeWeapons()
        {
            _weaponMap.Clear();
            _tickWeapons.Clear();

            // 1. Slot 1: Menzilli Arbalest'i kesin olarak bağla
            RepeaterArbalestWeapon rangedWep = null;
            if (rangedWeaponVisual != null)
            {
                rangedWep = rangedWeaponVisual.GetComponent<RepeaterArbalestWeapon>()
                         ?? rangedWeaponVisual.AddComponent<RepeaterArbalestWeapon>();
            }
            if (rangedWep == null)
            {
                rangedWep = GetComponentInChildren<RepeaterArbalestWeapon>(true)
                         ?? gameObject.AddComponent<RepeaterArbalestWeapon>();
            }
            RegisterWeapon(WeaponSlot.RepeaterArbalest, rangedWep);

            // 2. Slot 2: İnfaz Kılıcını kesin olarak bağla
            ExecutionerBladeWeapon meleeWep = null;
            if (meleeWeaponVisual != null)
            {
                meleeWep = meleeWeaponVisual.GetComponent<ExecutionerBladeWeapon>()
                        ?? meleeWeaponVisual.AddComponent<ExecutionerBladeWeapon>();
            }
            if (meleeWep == null)
            {
                meleeWep = GetComponentInChildren<ExecutionerBladeWeapon>(true)
                        ?? gameObject.AddComponent<ExecutionerBladeWeapon>();
            }
            RegisterWeapon(WeaponSlot.ExecutionerBlade, meleeWep);

            // 3. Slot 3: Gotik Çift Namlulu Pompalı & Grapple Hook'u bağla
            GothicShotgunWeapon shotgunWep = null;
            if (shotgunWeaponVisual != null)
            {
                shotgunWep = shotgunWeaponVisual.GetComponent<GothicShotgunWeapon>()
                          ?? shotgunWeaponVisual.AddComponent<GothicShotgunWeapon>();
            }
            if (shotgunWep == null)
            {
                shotgunWep = GetComponentInChildren<GothicShotgunWeapon>(true);
            }
            if (shotgunWep != null)
            {
                RegisterWeapon(WeaponSlot.GothicShotgun, shotgunWep);
            }

            // 4. Varsa diğer kayıtlı silahları tara
            if (registeredWeapons != null)
            {
                foreach (var wep in registeredWeapons)
                {
                    if (wep != null && !_weaponMap.ContainsKey(wep.Slot))
                    {
                        RegisterWeapon(wep.Slot, wep);
                    }
                }
            }
        }

        private void RegisterWeapon(WeaponSlot slot, WeaponBase weapon)
        {
            if (weapon == null) return;
            weapon.Initialize(_look, _cam);
            weapon.OnHitFeedback -= HandleTargetHit;
            weapon.OnHitFeedback += HandleTargetHit;
            _weaponMap[slot] = weapon;
            _tickWeapons.Add(weapon);
        }

        private void HandleTargetHit(bool isCrit)
        {
            OnWeaponHit?.Invoke(isCrit);
        }

        private void Update()
        {
            if (Pause.GamePause.IsGameplayInputBlocked)
            {
                RestoreQuickBlade();
                _requireFreshFire = true;
                return;
            }
            float deltaTime = Time.deltaTime;
            if (_activeWeapon != null) _activeWeapon.Tick(deltaTime);
            for (int i = 0; i < _tickWeapons.Count; i++)
            {
                WeaponBase weapon = _tickWeapons[i];
                if (weapon != null && weapon != _activeWeapon) weapon.TickHolstered(deltaTime);
            }
            if (_isQuickBladeActive)
            {
                if (Time.frameCount != _quickBladeStartFrame)
                    _quickBladeTimer = Mathf.Max(0f, _quickBladeTimer - deltaTime);
                if (_quickBladeTimer <= 0f) RestoreQuickBlade();
                return;
            }
            HandleWeaponSwitchInput();
            HandleFireInput();
        }

        private void OnDisable()
        {
            RestoreQuickBlade();
            for (int i = 0; i < _tickWeapons.Count; i++)
            {
                WeaponBase weapon = _tickWeapons[i];
                if (weapon != null) weapon.CancelPendingActions();
            }
            _requireFreshFire = true;
        }

        /// <summary>Strikes with the registered blade, temporarily borrowing its visual from any weapon.</summary>
        /// <returns>False when no Executioner Blade is registered or gameplay input is blocked.</returns>
        public bool TryPlayQuickBlade()
        {
            if (!isActiveAndEnabled || Pause.GamePause.IsGameplayInputBlocked) return false;
            if (!_weaponMap.TryGetValue(WeaponSlot.ExecutionerBlade, out WeaponBase weapon)
                || !(weapon is ExecutionerBladeWeapon blade) || blade == null) return false;

            if (!_isQuickBladeActive)
            {
                _quickBladeWeapon = blade;
                _isQuickBladeActive = true;
                if (_activeWeapon != blade)
                {
                    _quickBladeSourceVisual = GetWeaponVisual(_activeWeapon);
                    _quickBladeShownVisual = GetWeaponVisual(blade);
                    SetWeaponVisuals(WeaponSlot.ExecutionerBlade);
                    if (_quickBladeSourceVisual != null) _quickBladeSourceVisual.SetActive(false);
                    if (_quickBladeShownVisual != null) _quickBladeShownVisual.SetActive(true);
                }
            }

            // A successful parry can restart the strike before this visual timer expires.
            _quickBladeTimer = Mathf.Max(0f, quickBladeDuration);
            _quickBladeStartFrame = Time.frameCount;
            blade.QuickSwipe();
            if (_quickBladeTimer <= 0f) RestoreQuickBlade();
            return true;
        }

        private GameObject GetWeaponVisual(WeaponBase weapon)
        {
            if (weapon == null) return null;
            if (weapon.WeaponVisual != null) return weapon.WeaponVisual;
            GameObject visual = null;
            switch (weapon.Slot)
            {
                case WeaponSlot.RepeaterArbalest: visual = rangedWeaponVisual; break;
                case WeaponSlot.ExecutionerBlade: visual = meleeWeaponVisual; break;
                case WeaponSlot.GothicShotgun: visual = shotgunWeaponVisual; break;
            }
            if (visual != null) return visual;
            return weapon.gameObject != gameObject ? weapon.gameObject : null;
        }

        private void RestoreQuickBlade()
        {
            if (!_isQuickBladeActive) return;
            _isQuickBladeActive = false;
            _quickBladeTimer = 0f;
            SetWeaponVisuals(CurrentSlot);
            GameObject activeVisual = GetWeaponVisual(_activeWeapon);
            if (_quickBladeShownVisual != null && _activeWeapon != _quickBladeWeapon
                && _quickBladeShownVisual != activeVisual)
                _quickBladeShownVisual.SetActive(false);
            if (_quickBladeSourceVisual != null && _quickBladeSourceVisual == activeVisual)
                _quickBladeSourceVisual.SetActive(true);
            if (activeVisual != null) activeVisual.SetActive(true);
            _quickBladeSourceVisual = null;
            _quickBladeShownVisual = null;
            _quickBladeWeapon = null;
        }

        private void HandleWeaponSwitchInput()
        {
            if (_isQuickBladeActive) return;
            if (Keyboard.current == null) return;

            // 1 Tuşu: Arbalest
            if (Keyboard.current.digit1Key.wasPressedThisFrame && CurrentSlot != WeaponSlot.RepeaterArbalest)
            {
                EquipWeapon(WeaponSlot.RepeaterArbalest);
            }
            // 2 Tuşu: İnfaz Kılıcı
            else if (Keyboard.current.digit2Key.wasPressedThisFrame && CurrentSlot != WeaponSlot.ExecutionerBlade)
            {
                EquipWeapon(WeaponSlot.ExecutionerBlade);
            }
            // 3 Tuşu: Gotik Çift Namlulu Pompalı
            else if (Keyboard.current.digit3Key.wasPressedThisFrame && CurrentSlot != WeaponSlot.GothicShotgun)
            {
                EquipWeapon(WeaponSlot.GothicShotgun);
            }

            // Fare Tekerleği (Tüm Silahları Sırayla Gez)
            if (Mouse.current != null)
            {
                float scroll = Mouse.current.scroll.ReadValue().y;
                if (scroll > 0.5f)
                {
                    int next = ((int)CurrentSlot + 1) % 3;
                    EquipWeapon((WeaponSlot)next);
                }
                else if (scroll < -0.5f)
                {
                    int prev = ((int)CurrentSlot + 2) % 3;
                    EquipWeapon((WeaponSlot)prev);
                }
            }
        }

        /// <summary>Equips a registered weapon; switch attempts during a quick blade strike are ignored.</summary>
        public void EquipWeapon(WeaponSlot slot)
        {
            if (_isQuickBladeActive) return;
            if (!_weaponMap.TryGetValue(slot, out WeaponBase newWeapon) || newWeapon == null)
            {
                return;
            }

            if (_activeWeapon == newWeapon) return;
            bool isSwitch = _activeWeapon != null;

            if (_activeWeapon != null)
            {
                _activeWeapon.OnUnequip();
            }

            _activeWeapon = newWeapon;
            _activeWeapon.OnEquip();

            SetWeaponVisuals(slot);

            if (isSwitch && CombatAudio.Instance != null) CombatAudio.Instance.PlayWeaponSwitch();
            OnWeaponSwitched?.Invoke(slot);
        }

        private void SetWeaponVisuals(WeaponSlot slot)
        {
            if (rangedWeaponVisual != null)
            {
                rangedWeaponVisual.SetActive(slot == WeaponSlot.RepeaterArbalest);
            }
            if (meleeWeaponVisual != null)
            {
                meleeWeaponVisual.SetActive(slot == WeaponSlot.ExecutionerBlade);
            }
            if (shotgunWeaponVisual != null)
            {
                shotgunWeaponVisual.SetActive(slot == WeaponSlot.GothicShotgun);
            }
        }

        private void HandleFireInput()
        {
            if (_isQuickBladeActive) return;
            if (Pause.GamePause.IsGameplayInputBlocked) return;
            if (Mouse.current == null) return;

            if (_requireFreshFire)
            {
                if (Mouse.current.leftButton.isPressed || Mouse.current.rightButton.isPressed) return;
                _requireFreshFire = false;
                return;
            }

            // İmleç kilitli değilse, tıklama anında imleci kilitle ancak ATEŞ ETMEYİ ASLA ENGELLEME!
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                if (Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
            }

            // Aktif silah yoksa Arbalest'e zorla bağla
            if (_activeWeapon == null)
            {
                EquipWeapon(WeaponSlot.RepeaterArbalest);
                if (_activeWeapon == null) return;
            }

            // Birincil Atış (Sol Tık)
            bool firePrimary = _activeWeapon.IsFullAuto
                ? Mouse.current.leftButton.isPressed
                : Mouse.current.leftButton.wasPressedThisFrame;

            if (firePrimary && _activeWeapon.CanFirePrimary)
            {
                _activeWeapon.FirePrimary();
            }

            // İkincil Atış (Sağ Tık / Alt-Fire)
            if (Mouse.current.rightButton.wasPressedThisFrame && _activeWeapon.CanFireAlt)
            {
                _activeWeapon.FireAlt();
            }
        }
    }
}
