using UnityEngine;
using RetroFPS.Weapons;
using RetroFPS.Combat;

namespace RetroFPS.UI
{
    /// <summary>
    /// Sektör standardı, dinamik yaylanmalı Nişangah (Crosshair Bloom) ve
    /// Tok Vuruş İşaretçisi (Hitmarker Juice & Audio) bileşeni.
    /// Silah mantığından tamamen yalıtılmıştır (Decoupled & Single Responsibility).
    /// </summary>
    [DisallowMultipleComponent]
    public class CrosshairHUD : MonoBehaviour
    {
        [Header("Görsel Renkler")]
        [SerializeField] private Color normalCrosshairColor = new Color(1f, 1f, 1f, 0.85f);
        [SerializeField] private Color hitmarkerNormalColor = new Color(1f, 0.25f, 0.25f, 1f);
        [SerializeField] private Color hitmarkerCritColor = new Color(1f, 0.88f, 0.15f, 1f);

        [Header("Zamanlama & Boyut")]
        [SerializeField] private float hitmarkerDuration = 0.16f;
        [SerializeField] private float baseCrosshairSpread = 8f;
        [SerializeField] private float bloomRecoverySpeed = 24f;

        private WeaponController _weaponController;
        private float _hitmarkerTimer = 0f;
        private bool _hitmarkerIsCrit = false;
        private float _recoilBloom = 0f;

        private void Awake()
        {
            _weaponController = GetComponent<WeaponController>();
            if (_weaponController == null)
            {
                _weaponController = FindAnyObjectByType<WeaponController>();
            }
        }

        private void OnEnable()
        {
            if (_weaponController != null)
            {
                _weaponController.OnWeaponHit += HandleWeaponHit;
            }
        }

        private void OnDisable()
        {
            if (_weaponController != null)
            {
                _weaponController.OnWeaponHit -= HandleWeaponHit;
            }
        }

        /// <summary>
        /// Silah ateşlendiğinde nişangah çizgilerini dışa doğru açan kinetik yaylanma.
        /// </summary>
        public void TriggerRecoilBloom(float amount = 9f)
        {
            _recoilBloom = Mathf.Clamp(_recoilBloom + amount, 0f, 26f);
        }

        private void Update()
        {
            if (_hitmarkerTimer > 0f)
            {
                _hitmarkerTimer -= Time.deltaTime;
            }

            // Geri tepme açılmasının orijinal haline yaylanması
            if (_recoilBloom > 0.01f)
            {
                _recoilBloom = Mathf.Lerp(_recoilBloom, 0f, Time.deltaTime * bloomRecoverySpeed);
            }
            else
            {
                _recoilBloom = 0f;
            }
        }

        private void HandleWeaponHit(bool isCrit)
        {
            _hitmarkerTimer = hitmarkerDuration;
            _hitmarkerIsCrit = isCrit;

            // İşitsel geri bildirim: Tok tık veya kemik kırma sesi
            if (CombatAudio.Instance != null)
            {
                CombatAudio.Instance.PlayHitmarker(isCrit);
            }
        }

        private void OnGUI()
        {
            if (Cursor.lockState != CursorLockMode.Locked) return;

            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;

            Color oldCol = GUI.color;

            // 1. Merkez Nokta (Kritik darbede anlık parlar)
            bool isCritHitActive = _hitmarkerTimer > 0f && _hitmarkerIsCrit;
            GUI.color = isCritHitActive ? hitmarkerCritColor : normalCrosshairColor;
            float centerDotSize = isCritHitActive ? 5f : 4f;
            GUI.DrawTexture(new Rect(cx - centerDotSize * 0.5f, cy - centerDotSize * 0.5f, centerDotSize, centerDotSize), Texture2D.whiteTexture);

            // 2. Silah Türüne Göre Dinamik Nişangah Çizgileri
            WeaponSlot currentSlot = _weaponController != null ? _weaponController.CurrentSlot : WeaponSlot.RepeaterArbalest;

            if (currentSlot == WeaponSlot.RepeaterArbalest)
            {
                float spread = baseCrosshairSpread + _recoilBloom;
                GUI.color = normalCrosshairColor;

                // Sol, Sağ, Üst, Alt çizgiler
                GUI.DrawTexture(new Rect(cx - spread - 8, cy - 1, 8, 2), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx + spread, cy - 1, 8, 2), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx - 1, cy - spread - 8, 2, 8), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx - 1, cy + spread, 2, 8), Texture2D.whiteTexture);
            }
            else
            {
                // Kılıç için dinamik kavis çizgisi
                float swordWidth = 28f + _recoilBloom * 0.5f;
                GUI.color = normalCrosshairColor;
                GUI.DrawTexture(new Rect(cx - swordWidth * 0.5f, cy + 8, swordWidth, 2), Texture2D.whiteTexture);
            }

            // 3. Vuruş İşaretçisi (Hitmarker X) - Sıçrama Ölçeklendirmeli
            if (_hitmarkerTimer > 0f)
            {
                float t = _hitmarkerTimer / hitmarkerDuration;
                float popScale = Mathf.Lerp(1.35f, 1.0f, 1f - t); // Vuruşta 1.35x açılıp kapanır
                GUI.color = _hitmarkerIsCrit ? hitmarkerCritColor : hitmarkerNormalColor;
                float hSize = (_hitmarkerIsCrit ? 13f : 9f) * popScale;

                DrawLine(cx - hSize, cy - hSize, cx + hSize, cy + hSize, 2);
                DrawLine(cx - hSize, cy + hSize, cx + hSize, cy - hSize, 2);
            }

            GUI.color = oldCol;
        }

        private void DrawLine(float x1, float y1, float x2, float y2, int width)
        {
            Matrix4x4 matrix = GUI.matrix;
            Vector2 p1 = new Vector2(x1, y1);
            Vector2 p2 = new Vector2(x2, y2);
            float angle = Vector2.Angle(p2 - p1, Vector2.right);
            if (p1.y > p2.y) angle = -angle;

            GUIUtility.RotateAroundPivot(angle, p1);
            GUI.DrawTexture(new Rect(p1.x, p1.y, (p2 - p1).magnitude, width), Texture2D.whiteTexture);
            GUI.matrix = matrix;
        }
    }
}
