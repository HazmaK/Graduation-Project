using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RetroFPS
{
    public enum WeaponSlot
    {
        RepeaterArbalest = 0, // Slot 1: Hızlı Menzilli Arbalest / Nailgun
        ExecutionerBlade = 1  // Slot 2: Ağır Yakın Dövüş Kılıcı / Cleaver
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
        [SerializeField] private Transform firePoint;

        [Header("Kayıtlı Silahlar")]
        [SerializeField] private WeaponBase[] registeredWeapons;

        private PlayerLook _look;
        private Camera _cam;
        private readonly Dictionary<WeaponSlot, WeaponBase> _weaponMap = new Dictionary<WeaponSlot, WeaponBase>();
        private WeaponBase _activeWeapon;

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

            // 3. Varsa diğer kayıtlı silahları tara
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
        }

        private void HandleTargetHit(bool isCrit)
        {
            OnWeaponHit?.Invoke(isCrit);
        }

        private void Update()
        {
            if (_activeWeapon != null)
            {
                _activeWeapon.Tick(Time.deltaTime);
            }

            HandleWeaponSwitchInput();
            HandleFireInput();
        }

        private void HandleWeaponSwitchInput()
        {
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

            // Fare Tekerleği
            if (Mouse.current != null)
            {
                float scroll = Mouse.current.scroll.ReadValue().y;
                if (scroll > 0.5f || scroll < -0.5f)
                {
                    WeaponSlot next = CurrentSlot == WeaponSlot.RepeaterArbalest
                        ? WeaponSlot.ExecutionerBlade
                        : WeaponSlot.RepeaterArbalest;
                    EquipWeapon(next);
                }
            }
        }

        public void EquipWeapon(WeaponSlot slot)
        {
            if (!_weaponMap.TryGetValue(slot, out WeaponBase newWeapon) || newWeapon == null)
            {
                InitializeWeapons();
                if (!_weaponMap.TryGetValue(slot, out newWeapon) || newWeapon == null)
                {
                    Debug.LogWarning($"[WeaponController] Slot {slot} not found!");
                    return;
                }
            }

            if (_activeWeapon != null)
            {
                _activeWeapon.OnUnequip();
            }

            _activeWeapon = newWeapon;
            _activeWeapon.OnEquip();

            if (rangedWeaponVisual != null)
            {
                rangedWeaponVisual.SetActive(slot == WeaponSlot.RepeaterArbalest);
            }
            if (meleeWeaponVisual != null)
            {
                meleeWeaponVisual.SetActive(slot == WeaponSlot.ExecutionerBlade);
            }

            OnWeaponSwitched?.Invoke(slot);
        }

        private void HandleFireInput()
        {
            if (Mouse.current == null) return;

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
