using UnityEngine;

namespace RetroFPS.UI.Menu
{
    /// <summary>Build-retained references used by localization boot composition.</summary>
    [CreateAssetMenu(fileName = "LocalizationCatalog", menuName = "RetroFPS/Menu/Localization Catalog")]
    public sealed class LocalizationCatalog : ScriptableObject
    {
        [SerializeField] private StringTable combatTable;
        /// <summary>Shared combat and gameplay string provider.</summary>
        public StringTable CombatTable => combatTable;
    }
}
