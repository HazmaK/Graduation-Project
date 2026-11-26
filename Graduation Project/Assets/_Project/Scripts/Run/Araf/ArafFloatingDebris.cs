using RetroFPS.Run.Labyrinth;
using UnityEngine;

namespace RetroFPS.Run.Araf
{
    /// <summary>Creates nonphysical debris once navigation is ready and animates it while content is active.</summary>
    [DisallowMultipleComponent]
    public sealed class ArafFloatingDebris : MonoBehaviour
    {
        [SerializeField] private ArafLabyrinthDirector labyrinth;
        [SerializeField] private GameObject[] debrisPrefabs;
        [SerializeField] private float bobAmplitude = 1.5f;
        [SerializeField] private float bobSpeed = 0.25f;
        [SerializeField] private float spinSpeed = 4f;

        private ArafLevel _level;
        private Transform _debrisRoot;
        private Transform[] _pieces;
        private Vector3[] _basePositions;
        private float[] _phases;
        private Vector3[] _spinAxes;

        private void Awake()
        {
            if (labyrinth == null) labyrinth = GetComponent<ArafLabyrinthDirector>();
            _level = labyrinth != null ? labyrinth.GetComponent<ArafLevel>() : GetComponent<ArafLevel>();
        }

        private void OnEnable()
        {
            if (labyrinth == null) return;
            labyrinth.NavMeshReady += HandleNavMeshReady;
            if (labyrinth.IsNavMeshReady) HandleNavMeshReady();
        }

        private void Start()
        {
            if (labyrinth != null && labyrinth.IsNavMeshReady) HandleNavMeshReady();
        }

        private void OnDisable() => Unsubscribe();
        private void OnDestroy() => Unsubscribe();

        private void Unsubscribe()
        {
            if (labyrinth != null) labyrinth.NavMeshReady -= HandleNavMeshReady;
        }

        private void HandleNavMeshReady()
        {
            if (_debrisRoot != null || labyrinth == null || labyrinth.GeneratedRoot == null
                || debrisPrefabs == null || debrisPrefabs.Length == 0) return;
            ArafTuning tuning = _level != null ? _level.Tuning : null;
            int count = Mathf.Max(0, tuning != null ? tuning.DebrisCount : 40);
            _basePositions = new Vector3[count];
            var rotations = new Quaternion[count];
            var scales = new float[count];
            RunRandom rng = RunEvents.GetStream(RunStream.Cosmetic);
            count = ArafDebrisPlanner.Plan(labyrinth.WorldBoundsLocal, count,
                tuning != null ? tuning.DebrisMargin : 25f,
                tuning != null ? tuning.DebrisShellThickness : 60f,
                rng, _basePositions, rotations, scales);
            _pieces = new Transform[count];
            _phases = new float[count];
            _spinAxes = new Vector3[count];
            _debrisRoot = new GameObject("Debris").transform;
            _debrisRoot.SetParent(labyrinth.GeneratedRoot, false);
            // Keep planner coordinates in director space even if GeneratedRoot has a different transform.
            _debrisRoot.SetPositionAndRotation(labyrinth.transform.position, labyrinth.transform.rotation);
            _debrisRoot.localScale = Vector3.one;
            for (int i = 0; i < count; i++)
            {
                GameObject prefab = debrisPrefabs[i % debrisPrefabs.Length];
                if (prefab == null) continue;
                GameObject instance = Instantiate(prefab, _debrisRoot, false);
                Collider[] colliders = instance.GetComponentsInChildren<Collider>(true);
                for (int c = 0; c < colliders.Length; c++)
                {
                    colliders[c].enabled = false;
                    Destroy(colliders[c]);
                }
                Transform piece = instance.transform;
                _basePositions[i] = _debrisRoot.InverseTransformPoint(labyrinth.transform.TransformPoint(_basePositions[i]));
                piece.localPosition = _basePositions[i];
                piece.localRotation = rotations[i];
                piece.localScale *= scales[i];
                _pieces[i] = piece;
                _phases[i] = rng.Range(0f, 2f * Mathf.PI);
                _spinAxes[i] = (rotations[i] * Vector3.up).normalized;
            }
        }

        private void Update()
        {
            if (_debrisRoot == null || !_debrisRoot.gameObject.activeInHierarchy
                || (_level != null && !_level.IsContentActive)) return;
            float time = Time.time * bobSpeed;
            float spin = spinSpeed * Time.deltaTime;
            for (int i = 0; i < _pieces.Length; i++)
            {
                Transform piece = _pieces[i];
                if (piece == null) continue;
                Vector3 position = _basePositions[i];
                position.y += Mathf.Sin(time + _phases[i]) * bobAmplitude;
                piece.localPosition = position;
                piece.Rotate(_spinAxes[i], spin, Space.Self);
            }
        }
    }
}
