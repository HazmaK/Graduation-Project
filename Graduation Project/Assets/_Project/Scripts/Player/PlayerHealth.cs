using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using RetroFPS.Weapons;

namespace RetroFPS
{
    /// <summary>
    /// Oyuncunun can ve hasar alma sistemi.
    /// - Can 0'a indiğinde karakter ölür, kontroller ve silahlar kapanır, imleç serbest kalır.
    /// - Dash I-Frame (Hasar Almama Penceresi) ile entegre çalışır.
    /// - Darbe anında kamera sarsıntısı ve hasar geribildirimi tetikler.
    /// - Çatışmadan çıkınca yavaşça can yeniler.
    /// </summary>
    [RequireComponent(typeof(PlayerMovement))]
    public class PlayerHealth : MonoBehaviour, IDamageable
    {
        [Header("Can Ayarları")]
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float autoHealDelay = 4.5f;
        [SerializeField] private float autoHealRate = 12f;

        private float _currentHealth;
        private float _timeSinceLastDamage;
        private bool _isDead = false;
        private PlayerMovement _movement;
        private PlayerLook _look;
        private PlayerParryController _parry;
        private PlayerDamageFeedback _damageFeedback;

        public float CurrentHealth => _currentHealth;
        public float MaxHealth => maxHealth;
        public float HealthPercent => Mathf.Clamp01(_currentHealth / maxHealth);
        public bool IsDead => _isDead;

        public event System.Action<float> OnDamageTaken;
        public event System.Action OnDied;

        private void Awake()
        {
            _currentHealth = maxHealth;
            _isDead = false;
            _movement = GetComponent<PlayerMovement>();
            _look = GetComponent<PlayerLook>();
            _parry = GetComponent<PlayerParryController>();
            if (_parry == null) _parry = gameObject.AddComponent<PlayerParryController>();
            _damageFeedback = GetComponent<PlayerDamageFeedback>();
            if (_damageFeedback == null) _damageFeedback = gameObject.AddComponent<PlayerDamageFeedback>();
        }

        public void TakeDamage(float amount, Vector3 hitPoint, Vector3 hitNormal, bool isCritical = false)
        {
            TakeDamage(amount, hitPoint, hitNormal, null, isCritical);
        }

        public void TakeDamage(float amount, Vector3 hitPoint, Vector3 hitNormal, GameObject attacker, bool isCritical = false)
        {
            if (_isDead) return;

            // 1. Dash sırasında I-Frame kontrolü (Hasar almama penceresi)
            if (_movement != null && _movement.IsInvincible)
            {
                Debug.Log("<color=cyan><b>[DASH I-FRAME DODGE!]</b></color> Enemy attack successfully evaded with Dash!");
                return;
            }

            // 2. Kalkan Savuşturma (Parry) kontrolü
            if (_parry == null) _parry = GetComponent<PlayerParryController>();
            if (_parry != null && _parry.TryParryMelee(hitPoint, hitNormal, amount, attacker))
            {
                _timeSinceLastDamage = 0f;
                return;
            }

            _currentHealth = Mathf.Max(0f, _currentHealth - amount);
            _timeSinceLastDamage = 0f;

            // 3. Darbe Geri Bildirimi & Game Feel (Vignette, Hit Indicator, Flinch, Hurt Audio)
            if (_damageFeedback == null) _damageFeedback = GetComponent<PlayerDamageFeedback>();
            if (_damageFeedback != null)
            {
                _damageFeedback.OnPlayerDamaged(amount, hitPoint, hitNormal, attacker);
            }
            else if (_look != null)
            {
                _look.AddTrauma(0.35f);
            }

            OnDamageTaken?.Invoke(amount);
            Debug.Log($"<color=red><b>[PLAYER DAMAGED]</b></color> -{amount:F0} HP | Remaining Health: <b>{_currentHealth:F0}/{maxHealth}</b>");

            if (_currentHealth <= 0f && !_isDead)
            {
                Die();
            }
        }

        private void Die()
        {
            if (_isDead) return;
            _isDead = true;
            _currentHealth = 0f;

            // 1. Hareket, Bakış, Silah ve Savunma Kontrollerini Devre Dışı Bırak
            if (_movement != null) _movement.enabled = false;
            if (_look != null)
            {
                _look.UnlockCursor();
                _look.enabled = false;
            }

            WeaponController weapons = GetComponent<WeaponController>();
            if (weapons != null) weapons.enabled = false;

            if (_parry != null) _parry.enabled = false;

            // 2. İmleci serbest bırak (Restart butonuna rahat tıklanabilsin)
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // 3. Retro Ölüm Kamerası: Kamerayı hafifçe yere düşür ve yana yatır
            if (_look != null && _look.CameraHolder != null)
            {
                Vector3 pos = _look.CameraHolder.localPosition;
                pos.y = 0.40f;
                _look.CameraHolder.localPosition = pos;
                _look.CameraHolder.localRotation = Quaternion.Euler(15f, 0f, 22f);
            }

            OnDied?.Invoke();
            Debug.Log("<color=red><b>[PLAYER DIED]</b></color> Player perished. Awaiting restart.");
        }

        /// <summary>
        /// Oyunu/Sahneyi sıfırdan yeniden başlatır.
        /// </summary>
        public void RestartGame()
        {
            Time.timeScale = 1.0f; // Mikro dondurma varsa zamanı normale çek
            Time.fixedDeltaTime = 0.02f;
            _isDead = false;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        private void Update()
        {
            if (_isDead)
            {
                // Klavye Kısayolu: 'R', 'Space' veya 'Enter' tuşuyla da hemen yeniden başlatılabilir
                if (Keyboard.current != null && (Keyboard.current.rKey.wasPressedThisFrame || 
                                                Keyboard.current.spaceKey.wasPressedThisFrame || 
                                                Keyboard.current.enterKey.wasPressedThisFrame))
                {
                    RestartGame();
                }
                return;
            }

            // Çatışma dışı can yenilenmesi
            _timeSinceLastDamage += Time.deltaTime;
            if (_timeSinceLastDamage >= autoHealDelay && _currentHealth < maxHealth)
            {
                _currentHealth = Mathf.MoveTowards(_currentHealth, maxHealth, autoHealRate * Time.deltaTime);
            }
        }
    }
}
