using UnityEngine;

namespace RetroFPS
{
    /// <summary>
    /// Gotik zindan atmosferi için meşale ve alev çanağı ışık titremesi (Gothic Torch Light Flicker).
    /// - Sıfır GC tahsisi ile Perlin noise tabanlı organik alev dansı.
    /// - Sıcak kor ateşi ve alev dalgalanması ile dramatik yüksek kontrastlı gölgeler üretir.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public class GothicTorchFlicker : MonoBehaviour
    {
        [Header("Alev Titreme Ayarları")]
        [SerializeField] private float baseIntensity = 1.9f;
        [SerializeField] private float flickerIntensity = 0.45f;
        [SerializeField] private float flickerSpeed = 7.5f;

        [Header("Renk Dalgalanması")]
        [SerializeField] private Color flameColor = new Color(1.0f, 0.52f, 0.14f);
        [SerializeField] private Color emberColor = new Color(0.95f, 0.35f, 0.08f);

        [Header("Alev Kor / Kıvılcım Parçacıkları")]
        [SerializeField] private bool enableEmbers = true;
        [SerializeField] private float emberRateOverTime = 3.5f;

        private Light _light;
        private float _seed;
        private Vector3 _originalPos;
        private ParticleSystem _emberSystem;

        private void Awake()
        {
            _light = GetComponent<Light>();
            _seed = Random.Range(0f, 1000f);
            _originalPos = transform.localPosition;

            if (_light != null)
            {
                _light.type = LightType.Point;
                _light.color = flameColor;
                _light.intensity = baseIntensity;
            }

            if (enableEmbers)
            {
                SetupTorchEmbers();
            }
        }

        private void SetupTorchEmbers()
        {
            GameObject emberObj = new GameObject("Torch_Embers");
            emberObj.transform.SetParent(transform, false);
            emberObj.transform.localPosition = Vector3.zero;

            _emberSystem = emberObj.AddComponent<ParticleSystem>();
            _emberSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = _emberSystem.main;
            main.playOnAwake = true;
            main.loop = true;
            main.duration = 1.0f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.0f, 1.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.055f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.65f, 0.15f, 0.9f), new Color(1f, 0.25f, 0.05f, 0.8f));
            main.gravityModifier = -0.15f; // Yukarı hafif kaldırma kuvveti
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 30;

            var emission = _emberSystem.emission;
            emission.enabled = true;
            emission.rateOverTime = emberRateOverTime;

            var shape = _emberSystem.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.08f;

            var sizeOverLifetime = _emberSystem.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve curve = new AnimationCurve();
            curve.AddKey(0f, 1f);
            curve.AddKey(0.7f, 0.8f);
            curve.AddKey(1f, 0f);
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, curve);

            var renderer = _emberSystem.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Sprites/Default")
                         ?? Shader.Find("Unlit/Color");
            if (shader != null)
            {
                Material mat = new Material(shader)
                {
                    color = new Color(1f, 0.5f, 0.1f, 1f)
                };
                renderer.material = mat;
            }

            _emberSystem.Play();
        }

        private void Update()
        {
            if (_light == null) return;

            float time = Time.time * flickerSpeed;
            float noise1 = Mathf.PerlinNoise(time, _seed);
            float noise2 = Mathf.PerlinNoise(_seed, time * 1.5f);

            // 1. Organik alev parlama dalgalanması
            float mod = (noise1 - 0.5f) * 2f;
            _light.intensity = Mathf.Max(0.5f, baseIntensity + mod * flickerIntensity);

            // 2. Alev ve kor rengi geçişi
            _light.color = Color.Lerp(emberColor, flameColor, noise2);

            // 3. Hafif alev kıpırdama hareketi
            float jitterX = (Mathf.PerlinNoise(time * 0.8f, _seed) - 0.5f) * 0.04f;
            float jitterZ = (Mathf.PerlinNoise(_seed, time * 0.8f) - 0.5f) * 0.04f;
            transform.localPosition = _originalPos + new Vector3(jitterX, 0f, jitterZ);
        }
    }
}
