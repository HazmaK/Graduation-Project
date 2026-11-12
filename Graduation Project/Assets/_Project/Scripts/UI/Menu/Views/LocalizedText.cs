using TMPro;
using UnityEngine;

namespace RetroFPS.UI.Menu
{
    /// <summary>Resolves an authored, pre-cased string when its label becomes active.</summary>
    [RequireComponent(typeof(TMP_Text))]
    public sealed class LocalizedText : MonoBehaviour
    {
        [SerializeField] private StringTable strings;
        [SerializeField] private string key;
        [SerializeField] private TMP_Text label;

        private void Awake() { if (label == null) label = GetComponent<TMP_Text>(); }
        private void OnEnable() => Refresh();

        /// <summary>Refreshes the label after a table edit or locale change.</summary>
        public void Refresh()
        {
            if (label != null && strings != null && !string.IsNullOrEmpty(key)) label.text = strings.Get(key);
        }
    }
}
