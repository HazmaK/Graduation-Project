using RetroFPS.Combat;
using UnityEngine;

namespace RetroFPS.Run.Labyrinth
{
    /// <summary>Arena dalga ayarlarını ve düşman prefablarını bir arada tutar.</summary>
    [CreateAssetMenu(menuName = "RetroFPS/Araf/Arena Wave Table")]
    public sealed class ArafArenaWaveTable : ScriptableObject
    {
        [SerializeField] private GameObject ghoulPrefab;
        [SerializeField] private GameObject eyePrefab;
        [SerializeField] private GameObject penitentPrefab;
        [SerializeField] private ArafArenaWaveSettings settings = new ArafArenaWaveSettings();
        [SerializeField, Tooltip("Uyanış 0..N saldırganlık profilleri; boş eleman prefab varsayılanını kullanır.")]
        private EnemyAggressionProfile[] aggressionByAwakening = new EnemyAggressionProfile[0];
        /// <summary>Ghoul doğuş prefabı.</summary>
        public GameObject GhoulPrefab => ghoulPrefab;
        /// <summary>Uçan göz doğuş prefabı.</summary>
        public GameObject EyePrefab => eyePrefab;
        /// <summary>Kefaretçi doğuş prefabı.</summary>
        public GameObject PenitentPrefab => penitentPrefab;
        /// <summary>Dalga ve doğuş ayarları.</summary>
        public ArafArenaWaveSettings Settings => settings;

        /// <summary>Uyanışa göre saldırganlık profili; aralık dışı değer son profile sabitlenir, tablo boşsa null.</summary>
        public EnemyAggressionProfile GetAggressionProfile(int awakening)
        {
            if (aggressionByAwakening == null || aggressionByAwakening.Length == 0) return null;
            return aggressionByAwakening[Mathf.Clamp(awakening, 0, aggressionByAwakening.Length - 1)];
        }
#if UNITY_EDITOR
        /// <summary>Editör üreticisinde düşman prefablarını ve uyanış profillerini bağlar.</summary>
        public void Configure(GameObject ghoul, GameObject eye, GameObject penitent, EnemyAggressionProfile[] profiles)
        {
            ghoulPrefab = ghoul;
            eyePrefab = eye;
            penitentPrefab = penitent;
            aggressionByAwakening = profiles ?? new EnemyAggressionProfile[0];
        }
#endif
    }
}
