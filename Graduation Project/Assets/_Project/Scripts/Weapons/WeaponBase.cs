using System;
using UnityEngine;
using RetroFPS.Combat;

namespace RetroFPS.Weapons
{
    /// <summary>
    /// Tüm oyuncu silahları için soyut temel sınıf.
    /// Slot özelliği soyuttur; GameObject'in aktif/inaktif durumundan veya Awake sırasından etkilenmez.
    /// </summary>
    public abstract class WeaponBase : MonoBehaviour
    {
        [Header("Temel Silah Tanımı")]
        [SerializeField] protected string weaponDisplayName = "Weapon";
        [SerializeField] protected GameObject weaponVisual;
        [SerializeField] protected Transform firePoint;
        [SerializeField] protected ProceduralViewmodel viewmodel;

        protected PlayerLook _look;
        protected Camera _cam;
        protected PlayerAmmoInventory _ammoInventory;

        /// <summary>
        /// Silahın ait olduğu yuva (Slot). Her alt sınıf tarafından kesin ve değişmez olarak tanımlanır.
        /// </summary>
        public abstract WeaponSlot Slot { get; }

        public string WeaponDisplayName => weaponDisplayName;
        public GameObject WeaponVisual => weaponVisual;
        public ProceduralViewmodel Viewmodel => viewmodel;
        public PlayerAmmoInventory AmmoInventory => _ammoInventory;

        public virtual AmmoType RequiredAmmoType => AmmoType.None;
        public virtual int PrimaryAmmoCost => 0;
        public virtual int AltAmmoCost => 0;

        public abstract bool IsFullAuto { get; }
        public abstract bool CanFirePrimary { get; }
        public abstract bool CanFireAlt { get; }

        /// <summary>
        /// Hedefe başarılı darbe vurulduğunda tetiklenen olay (isCrit: kritik/headshot mı?).
        /// </summary>
        public event Action<bool> OnHitFeedback;

        protected Camera PlayerCamera
        {
            get
            {
                if (_cam != null) return _cam;

                if (_look == null)
                {
                    _look = GetComponentInParent<PlayerLook>() ?? FindAnyObjectByType<PlayerLook>();
                }

                if (_look != null && _look.PlayerCam != null)
                {
                    _cam = _look.PlayerCam;
                }

                if (_cam == null)
                {
                    _cam = Camera.main ?? FindAnyObjectByType<Camera>();
                }

                return _cam;
            }
        }

        protected Transform FirePoint
        {
            get
            {
                if (firePoint != null) return firePoint;

                Transform fp = transform.Find("FirePoint");
                if (fp != null)
                {
                    firePoint = fp;
                    return firePoint;
                }

                return transform;
            }
        }

        public virtual void Initialize(PlayerLook look, Camera playerCamera)
        {
            _look = look;
            _cam = playerCamera;

            ResolveViewmodel();
            ResolveAmmoInventory();
        }

        protected virtual void Start()
        {
            ResolveViewmodel();
            ResolveAmmoInventory();
            _ = PlayerCamera;
        }

        private void ResolveViewmodel()
        {
            if (viewmodel != null) return;

            viewmodel = GetComponent<ProceduralViewmodel>()
                     ?? GetComponentInParent<ProceduralViewmodel>()
                     ?? GetComponentInChildren<ProceduralViewmodel>();
        }

        private void ResolveAmmoInventory()
        {
            if (_ammoInventory != null) return;

            _ammoInventory = GetComponentInParent<PlayerAmmoInventory>()
                          ?? FindAnyObjectByType<PlayerAmmoInventory>();
        }

        protected bool HasAmmo(int amount)
        {
            if (RequiredAmmoType == AmmoType.None || amount <= 0) return true;
            ResolveAmmoInventory();
            return _ammoInventory == null || _ammoInventory.HasAmmo(RequiredAmmoType, amount);
        }

        protected bool TryConsumeAmmo(int amount)
        {
            if (RequiredAmmoType == AmmoType.None || amount <= 0) return true;
            ResolveAmmoInventory();
            return _ammoInventory == null || _ammoInventory.TryConsumeAmmo(RequiredAmmoType, amount);
        }

        public virtual void OnEquip()
        {
            if (weaponVisual != null)
            {
                weaponVisual.SetActive(true);
            }
            else
            {
                gameObject.SetActive(true);
            }
        }

        public virtual void OnUnequip()
        {
            if (weaponVisual != null)
            {
                weaponVisual.SetActive(false);
            }
        }

        public abstract void Tick(float deltaTime);
        public abstract void FirePrimary();
        public abstract void FireAlt();

        protected void NotifyHit(bool isCrit)
        {
            OnHitFeedback?.Invoke(isCrit);
        }
    }
}
