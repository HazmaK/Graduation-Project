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

        protected Camera PlayerCamera => _cam;
        protected Transform FirePoint => firePoint != null ? firePoint : transform;

        private void ResolveCameraAndFirePoint()
        {
            if (_look == null) _look = GetComponentInParent<PlayerLook>() ?? FindAnyObjectByType<PlayerLook>();
            if (_cam == null && _look != null) _cam = _look.PlayerCam;
            if (_cam == null) _cam = Camera.main;
            if (firePoint == null) firePoint = transform.Find("FirePoint");
        }

        public virtual void Initialize(PlayerLook look, Camera playerCamera)
        {
            _look = look;
            _cam = playerCamera;

            ResolveCameraAndFirePoint();
            ResolveViewmodel();
            ResolveAmmoInventory();
        }

        protected virtual void Start()
        {
            ResolveCameraAndFirePoint();
            ResolveViewmodel();
            ResolveAmmoInventory();
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
            return _ammoInventory == null || _ammoInventory.HasAmmo(RequiredAmmoType, amount);
        }

        protected bool TryConsumeAmmo(int amount)
        {
            if (RequiredAmmoType == AmmoType.None || amount <= 0) return true;
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
            if (viewmodel != null) viewmodel.PlayEquip();
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

namespace RetroFPS.Combat
{
    // Bind at spawn/scene initialization. Hit paths only read cached references.
    internal static class CombatTargetLookup
    {
        internal readonly struct Binding
        {
            internal Hitbox Hitbox { get; }
            internal IDamageable Damageable { get; }
            internal IStunnable Stunnable { get; }
            internal IEncounterEnemy Enemy { get; }
            internal IDamageable Entity => Hitbox != null && Hitbox.Target != null ? Hitbox.Target : Damageable;

            internal bool IsEmpty => Hitbox == null && Damageable == null && Stunnable == null && Enemy == null;

            // Unity objects destroyed after binding compare == null only through UnityEngine.Object's operator;
            // interface references must be cast back to detect it. No allocation (reference casts only).
            internal bool HasDestroyedReference =>
                IsDestroyed(Hitbox) || IsDestroyed(Damageable) || IsDestroyed(Stunnable) || IsDestroyed(Enemy);

            private static bool IsDestroyed(object reference)
            {
                return reference is UnityEngine.Object unityObject && unityObject == null;
            }

            internal Binding(Collider collider)
            {
                // Orijinal çözümleme sırası: Hitbox -> IDamageable.
                Hitbox = collider.GetComponentInParent<Hitbox>();
                Damageable = Hitbox != null ? Hitbox : collider.GetComponentInParent<IDamageable>();
                Stunnable = collider.GetComponentInParent<IStunnable>();
                Enemy = collider.GetComponentInParent<IEncounterEnemy>();
            }
        }

        // Also holds cached misses (empty bindings) for non-damageable colliders hit at runtime;
        // the whole cache is purged on single-mode scene loads.
        private static readonly System.Collections.Generic.Dictionary<Collider, Binding> _bindings =
            new System.Collections.Generic.Dictionary<Collider, Binding>(2048);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _bindings.Clear();
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            // Additive yüklemede mevcut sahnelerin kayıtlarını silme; yalnızca yeni kayıt ekle.
            if (mode == UnityEngine.SceneManagement.LoadSceneMode.Single) _bindings.Clear();
            Collider[] colliders = UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsInactive.Include);
            for (int i = 0; i < colliders.Length; i++) Register(colliders[i]);
        }

        internal static void RegisterHierarchy(Component entity)
        {
            Collider[] colliders = entity.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++) Register(colliders[i]);
        }

        private static void Register(Collider collider)
        {
            if (collider == null) return;
            Binding binding = new Binding(collider);
            if (!binding.IsEmpty)
                _bindings[collider] = binding;
            else
                _bindings.Remove(collider); // stale (cached miss or old) entry; re-resolved lazily on next hit
        }

        /// <summary>
        /// Cache hit: dictionary lookup + destroyed-reference check, no allocation.
        /// Cache miss (or entry with destroyed objects): resolve via GetComponentInParent and cache the
        /// result, including empty bindings for non-damageable colliders.
        /// </summary>
        internal static Binding Get(Collider collider)
        {
            if (collider == null) return default;

            if (_bindings.TryGetValue(collider, out Binding binding))
            {
                if (!binding.HasDestroyedReference) return binding;
                _bindings.Remove(collider);
            }

            binding = new Binding(collider);
            _bindings[collider] = binding;
            return binding;
        }
    }

    internal static class CombatRaycast
    {
        // NonAlloc results are unordered; choose the nearest result explicitly.
        internal static bool Nearest(Vector3 origin, Vector3 direction, float distance, int mask,
            RaycastHit[] buffer, out RaycastHit hit)
        {
            int count = Physics.RaycastNonAlloc(origin, direction, buffer, distance, mask, QueryTriggerInteraction.Ignore);
            hit = default;
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                if (buffer[i].distance >= nearest) continue;
                nearest = buffer[i].distance;
                hit = buffer[i];
            }
            return count > 0;
        }
    }
}
