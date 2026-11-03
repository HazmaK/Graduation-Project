using System.Text;
using UnityEngine;
using RetroFPS.Weapons;

namespace RetroFPS.UI
{
    /// <summary>
    /// Sektör standardı, sıfır GC tahsisli Retro Boomer Shooter Cephane Arayüzü (Ammo HUD).
    /// - Aktif silahın cephane durumunu sağ alt köşede gotik/retro font ve dinamik doluluk barıyla gösterir.
    /// - Arbalest için Parlayan Turkuaz (Cyan), Pompalı için Koyu Altın/Amber (Gold) renk teması.
    /// - 1080p çözünürlük bağımsız otomatik GUI matrisi ölçeklemesi.
    /// - Sıfır GC: Önceden tahsis edilmiş StringBuilder, statik dokular ve stiller.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("RetroFPS/Ammo HUD")]
    public class AmmoHUD : MonoBehaviour
    {
        [Header("Görsel Ayarlar")]
        [SerializeField] private bool showHUD = true;
        [SerializeField] private Color boltsColor = new Color(0.0f, 0.90f, 1.0f, 0.95f);
        [SerializeField] private Color shellsColor = new Color(1.0f, 0.70f, 0.15f, 0.95f);
        [SerializeField] private Color emptyWarningColor = new Color(1.0f, 0.20f, 0.20f, 1.0f);
        [SerializeField] private Color barBackgroundColor = new Color(0.06f, 0.06f, 0.08f, 0.75f);

        private WeaponController _weaponController;
        private PlayerAmmoInventory _ammoInventory;
        private readonly StringBuilder _sb = new StringBuilder(64);

        // GUI Önbellekleri
        private static GUIStyle _titleStyle;
        private static GUIStyle _countStyle;
        private static GUIStyle _warningStyle;
        private static Texture2D _whiteTex;
        private static Texture2D _panelBgTex;

        private void Awake()
        {
            ResolveReferences();
        }

        private void ResolveReferences()
        {
            if (_weaponController == null)
            {
                _weaponController = GetComponentInParent<WeaponController>()
                                 ?? FindAnyObjectByType<WeaponController>();
            }

            if (_ammoInventory == null)
            {
                _ammoInventory = GetComponentInParent<PlayerAmmoInventory>()
                              ?? FindAnyObjectByType<PlayerAmmoInventory>();
            }
        }

        private static void EnsureStyles()
        {
            if (_whiteTex == null)
            {
                _whiteTex = Texture2D.whiteTexture;
            }

            if (_panelBgTex == null)
            {
                _panelBgTex = new Texture2D(1, 1);
                _panelBgTex.SetPixel(0, 0, new Color(0.04f, 0.04f, 0.05f, 0.85f));
                _panelBgTex.Apply();
            }

            if (_titleStyle == null)
            {
                _titleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 13,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.UpperLeft
                };
            }

            if (_countStyle == null)
            {
                _countStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 28,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleRight
                };
            }

            if (_warningStyle == null)
            {
                _warningStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.LowerRight
                };
            }
        }

        private void OnGUI()
        {
            if (!showHUD) return;

            ResolveReferences();
            if (_weaponController == null || _weaponController.ActiveWeapon == null) return;

            WeaponBase activeWeapon = _weaponController.ActiveWeapon;
            AmmoType ammoType = activeWeapon.RequiredAmmoType;

            // Matris Ölçekleme: 1920x1080 referans çözünürlük
            float nativeWidth = 1920f;
            float nativeHeight = 1080f;
            Matrix4x4 oldMatrix = GUI.matrix;
            float scaleX = Screen.width / nativeWidth;
            float scaleY = Screen.height / nativeHeight;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scaleX, scaleY, 1f));

            EnsureStyles();

            // Ekranın sağ alt köşesi
            float panelW = 210f;
            float panelH = 75f;
            float x = nativeWidth - panelW - 35f;
            float y = nativeHeight - panelH - 35f;

            // Panel Arka Planı
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(x, y, panelW, panelH), _panelBgTex);

            // Çerçeve Kenarlığı
            GUI.color = new Color(0.35f, 0.35f, 0.40f, 0.6f);
            GUI.DrawTexture(new Rect(x, y, panelW, 2), _whiteTex);
            GUI.DrawTexture(new Rect(x, y + panelH - 2, panelW, 2), _whiteTex);
            GUI.DrawTexture(new Rect(x, y, 2, panelH), _whiteTex);
            GUI.DrawTexture(new Rect(x + panelW - 2, y, 2, panelH), _whiteTex);

            if (ammoType == AmmoType.None)
            {
                // Yakın Dövüş / Sınırsız
                _titleStyle.normal.textColor = new Color(0.8f, 0.8f, 0.8f, 0.8f);
                GUI.Label(new Rect(x + 12, y + 10, 120, 20), "MELEE", _titleStyle);

                _countStyle.normal.textColor = Color.white;
                GUI.Label(new Rect(x + panelW - 90, y + 15, 75, 40), "∞", _countStyle);
            }
            else
            {
                int current = (_ammoInventory != null) ? _ammoInventory.GetCurrentAmmo(ammoType) : 0;
                int max = (_ammoInventory != null) ? _ammoInventory.GetMaxAmmo(ammoType) : 1;
                float ratio = Mathf.Clamp01((float)current / Mathf.Max(max, 1));

                Color themeColor = (ammoType == AmmoType.Shells) ? shellsColor : boltsColor;
                string typeLabel = (ammoType == AmmoType.Shells) ? "SHELLS [3]" : "BOLTS [1]";

                // Başlık
                _titleStyle.normal.textColor = themeColor;
                GUI.Label(new Rect(x + 12, y + 10, 120, 20), typeLabel, _titleStyle);

                // Sayaç Metni
                _sb.Clear();
                _sb.Append(current).Append(" <size=15><color=#888888>/ ").Append(max).Append("</color></size>");

                if (current <= 0)
                {
                    _countStyle.normal.textColor = emptyWarningColor;
                    _warningStyle.normal.textColor = emptyWarningColor;
                    GUI.Label(new Rect(x + 12, y + 32, 100, 20), "EMPTY!", _warningStyle);
                }
                else if (ratio < 0.25f)
                {
                    _countStyle.normal.textColor = Color.Lerp(emptyWarningColor, themeColor, Mathf.PingPong(Time.time * 4f, 1f));
                }
                else
                {
                    _countStyle.normal.textColor = themeColor;
                }

                GUI.Label(new Rect(x + panelW - 130, y + 10, 118, 38), _sb.ToString(), _countStyle);

                // Alt Doluluk Barı
                float barX = x + 12f;
                float barY = y + panelH - 14f;
                float barW = panelW - 24f;
                float barH = 5f;

                GUI.color = barBackgroundColor;
                GUI.DrawTexture(new Rect(barX, barY, barW, barH), _whiteTex);

                GUI.color = (current <= 0) ? emptyWarningColor : themeColor;
                GUI.DrawTexture(new Rect(barX, barY, barW * ratio, barH), _whiteTex);
            }

            GUI.matrix = oldMatrix;
            GUI.color = Color.white;
        }
    }
}
