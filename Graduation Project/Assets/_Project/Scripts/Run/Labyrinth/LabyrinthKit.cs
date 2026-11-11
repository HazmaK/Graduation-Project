using UnityEngine;

namespace RetroFPS.Run.Labyrinth
{
    /// <summary>Labirent üretiminde kullanılan birim boyutlu parçalar ve ölçüler.</summary>
    [CreateAssetMenu(fileName = "LabyrinthKit_New", menuName = "RetroFPS/Araf/Labyrinth Kit", order = 32)]
    public class LabyrinthKit : ScriptableObject
    {
        [SerializeField] private GameObject wallSegment;
        [SerializeField] private GameObject floorTile;
        [SerializeField] private GameObject ceilingTile;
        [SerializeField] private GameObject pillar;
        [SerializeField] private GameObject veil;
        [SerializeField] private GameObject doorBarrier;
        [SerializeField] private GameObject torch;
        [SerializeField, Min(0.1f)] private float wallHeight = 5f;
        [SerializeField, Min(0.01f)] private float wallThickness = 0.6f;
        [SerializeField, Min(1)] private int torchEveryNCells = 4;

        /// <summary>Birim küp duvar parçası.</summary>
        public GameObject WallSegment => wallSegment;
        /// <summary>XZ üzerinde 1x1 boyutlu Quad/Cube taban; Unity Plane'in 10x10 ölçüsü varsayılmaz.</summary>
        public GameObject FloorTile => floorTile;
        /// <summary>XZ üzerinde 1x1 boyutlu isteğe bağlı tavan.</summary>
        public GameObject CeilingTile => ceilingTile;
        /// <summary>İsteğe bağlı birim küp birleşim sütunu.</summary>
        public GameObject Pillar => pillar;
        /// <summary>Çarpıştırıcısız kapı perdesi.</summary>
        public GameObject Veil => veil;
        /// <summary>Kapıyı mühürleyen birim küp engel.</summary>
        public GameObject DoorBarrier => doorBarrier;
        /// <summary>İsteğe bağlı meşale; ileri yönü duvardan koridora bakar.</summary>
        public GameObject Torch => torch;
        /// <summary>Duvar yüksekliği.</summary>
        public float WallHeight => wallHeight;
        /// <summary>Duvar kalınlığı.</summary>
        public float WallThickness => wallThickness;
        /// <summary>Koridor hücrelerindeki meşale aralığı.</summary>
        public int TorchEveryNCells => torchEveryNCells;
    }
}
