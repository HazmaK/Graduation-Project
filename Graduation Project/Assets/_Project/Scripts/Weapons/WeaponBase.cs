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

        /// <summary>
        /// Silahın ait olduğu yuva (Slot). Her alt sınıf tarafından kesin ve değişmez olarak tanımlanır.
        /// </summary>
        public abstract WeaponSlot Slot { get; }

        public string WeaponDisplayName => weaponDisplayName;
        public GameObject WeaponVisual => weaponVisual;
        public ProceduralViewmodel Viewmodel => viewmodel;

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
        }

        protected virtual void Start()
        {
            ResolveViewmodel();
            _ = PlayerCamera;
        }

        private void ResolveViewmodel()
        {
            if (viewmodel != null) return;

            viewmodel = GetComponent<ProceduralViewmodel>()
                     ?? GetComponentInParent<ProceduralViewmodel>()
                     ?? GetComponentInChildren<ProceduralViewmodel>();
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
