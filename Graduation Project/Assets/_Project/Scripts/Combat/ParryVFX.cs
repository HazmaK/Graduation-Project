using UnityEngine;

namespace RetroFPS.Combat
{
    /// <summary>
    /// Kusursuz savuşturma (Parry) ve düşman saldırı telgrafı (Telegraph Glint) efekt yöneticisi.
    /// - Şok dalgası halkası (Expanding Shockwave Ring)
    /// - Kıvılcım patlaması (Radial Sparks)
    /// - Düşman saldırı uyarı parıltısı (Parry Glint Star Flash - Sekiro/Ultrakill tarzı)
    /// - Düşman sersemleme tacı (Rotating Stun Stars)
    /// </summary>
    public static class ParryVFX
    {
        private static Material _ringMaterial;
        private static Material _sparkMaterial;
        private static Material _glintMaterial;

        private static void EnsureMaterials()
        {
            Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit")
                              ?? Shader.Find("Universal Render Pipeline/Lit")
                              ?? Shader.Find("Standard");

            if (_ringMaterial == null)
            {
                _ringMaterial = new Material(unlitShader) { name = "Mat_ParryRing" };
                _ringMaterial.color = new Color(0.2f, 0.95f, 1.0f, 0.95f);
            }

            if (_sparkMaterial == null)
            {
                _sparkMaterial = new Material(unlitShader) { name = "Mat_ParrySpark" };
                _sparkMaterial.color = new Color(1.0f, 0.92f, 0.25f, 1.0f);
            }

            if (_glintMaterial == null)
            {
                _glintMaterial = new Material(unlitShader) { name = "Mat_ParryGlint" };
                _glintMaterial.color = new Color(1.0f, 0.98f, 0.40f, 1.0f);
            }
        }

        /// <summary>
        /// Savuşturma (Parry) gerçekleştiğinde şok dalgası ve kıvılcım patlaması yaratır.
        /// </summary>
        public static void Spawn(Vector3 position, Vector3 forward, bool isProjectile = false)
        {
            EnsureMaterials();

            // 1. Genişleyen Şok Dalgası Halkası
            GameObject ringGO = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ringGO.name = "Parry_ShockwaveRing";
            ringGO.transform.position = position;
            ringGO.transform.rotation = Quaternion.LookRotation(forward) * Quaternion.Euler(90f, 0f, 0f);
            ringGO.transform.localScale = new Vector3(0.3f, 0.005f, 0.3f);

            Collider ringCol = ringGO.GetComponent<Collider>();
            if (ringCol != null) Object.Destroy(ringCol);

            Renderer ringRend = ringGO.GetComponent<Renderer>();
            ringRend.material = isProjectile ? _ringMaterial : _sparkMaterial;

            ExpandingRing ringAnim = ringGO.AddComponent<ExpandingRing>();
            ringAnim.targetScale = isProjectile ? 2.8f : 2.2f;
            ringAnim.duration = 0.20f;

            // 2. Kıvılcım Patlaması (8-12 adet parçacık)
            int sparkCount = isProjectile ? 12 : 9;
            for (int i = 0; i < sparkCount; i++)
            {
                GameObject spark = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                spark.name = "Parry_Spark";
                spark.transform.position = position;
                spark.transform.localScale = Vector3.one * 0.12f;

                Collider sparkCol = spark.GetComponent<Collider>();
                if (sparkCol != null) Object.Destroy(sparkCol);

                Renderer sparkRend = spark.GetComponent<Renderer>();
                sparkRend.material = _sparkMaterial;

                Vector3 spreadDir = (forward + Random.insideUnitSphere * 1.2f).normalized;
                SparkMotion motion = spark.AddComponent<SparkMotion>();
                motion.velocity = spreadDir * Random.Range(7.0f, 14.0f);
                motion.lifetime = Random.Range(0.20f, 0.32f);
            }
        }

        /// <summary>
        /// Düşman vurmadan tam 0.20s önce gözlerinde parlayan altın sarısı "PARRY ŞİMDİ!" yıldızı.
        /// </summary>
        public static void SpawnGlint(Vector3 position)
        {
            EnsureMaterials();

            GameObject glintGO = new GameObject("Telegraph_ParryGlint");
            glintGO.transform.position = position;

            // 4 köşeli parlak yıldız geometrisi (Yatay ve dikey iki elips)
            GameObject barH = GameObject.CreatePrimitive(PrimitiveType.Cube);
            barH.name = "Glint_H";
            barH.transform.SetParent(glintGO.transform, false);
            barH.transform.localScale = new Vector3(0.55f, 0.08f, 0.05f);
            barH.GetComponent<Renderer>().material = _glintMaterial;
            Object.Destroy(barH.GetComponent<Collider>());

            GameObject barV = GameObject.CreatePrimitive(PrimitiveType.Cube);
            barV.name = "Glint_V";
            barV.transform.SetParent(glintGO.transform, false);
            barV.transform.localScale = new Vector3(0.08f, 0.55f, 0.05f);
            barV.GetComponent<Renderer>().material = _glintMaterial;
            Object.Destroy(barV.GetComponent<Collider>());

            GameObject center = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            center.name = "Glint_Center";
            center.transform.SetParent(glintGO.transform, false);
            center.transform.localScale = Vector3.one * 0.20f;
            center.GetComponent<Renderer>().material = _glintMaterial;
            Object.Destroy(center.GetComponent<Collider>());

            GlintAnim anim = glintGO.AddComponent<GlintAnim>();
            anim.duration = 0.22f;
        }

        /// <summary>
        /// Düşman sersemlediğinde (Stun) başının üstünde dönen altın yıldızlar.
        /// </summary>
        public static void SpawnStunEffect(Transform enemyTransform, float duration)
        {
            EnsureMaterials();

            GameObject stunRoot = new GameObject("StunEffect_Crown");
            stunRoot.transform.SetParent(enemyTransform, false);
            stunRoot.transform.localPosition = new Vector3(0f, 2.05f, 0f);

            int orbCount = 3;
            for (int i = 0; i < orbCount; i++)
            {
                GameObject orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                orb.name = $"Stun_Orb_{i}";
                orb.transform.SetParent(stunRoot.transform, false);
                orb.transform.localScale = Vector3.one * 0.12f;

                float angle = i * (360f / orbCount) * Mathf.Deg2Rad;
                orb.transform.localPosition = new Vector3(Mathf.Cos(angle) * 0.35f, 0f, Mathf.Sin(angle) * 0.35f);
                orb.GetComponent<Renderer>().material = _sparkMaterial;
                Object.Destroy(orb.GetComponent<Collider>());
            }

            StunCrownAnim anim = stunRoot.AddComponent<StunCrownAnim>();
            anim.duration = duration;
        }

        private class ExpandingRing : MonoBehaviour
        {
            public float targetScale = 2.2f;
            public float duration = 0.20f;
            private float _timer = 0f;
            private Vector3 _startScale;

            private void Start() => _startScale = transform.localScale;

            private void Update()
            {
                _timer += Time.deltaTime;
                float progress = Mathf.Clamp01(_timer / duration);
                float currentScale = Mathf.Lerp(_startScale.x, targetScale, Mathf.Sin(progress * Mathf.PI * 0.5f));
                transform.localScale = new Vector3(currentScale, _startScale.y, currentScale);

                if (_timer >= duration) Destroy(gameObject);
            }
        }

        private class SparkMotion : MonoBehaviour
        {
            public Vector3 velocity;
            public float lifetime = 0.25f;
            private float _timer = 0f;
            private Vector3 _initScale;

            private void Start() => _initScale = transform.localScale;

            private void Update()
            {
                _timer += Time.deltaTime;
                transform.position += velocity * Time.deltaTime;
                velocity.y -= 16f * Time.deltaTime;

                float shrink = 1.0f - Mathf.Clamp01(_timer / lifetime);
                transform.localScale = _initScale * shrink;

                if (_timer >= lifetime) Destroy(gameObject);
            }
        }

        private class GlintAnim : MonoBehaviour
        {
            public float duration = 0.22f;
            private float _timer = 0f;
            private Camera _cam;

            private void Start()
            {
                _cam = Camera.main ?? Object.FindAnyObjectByType<Camera>();
            }

            private void Update()
            {
                _timer += Time.deltaTime;

                // Kameraya dönük kalsın (Billboard)
                if (_cam != null)
                {
                    transform.rotation = Quaternion.LookRotation(transform.position - _cam.transform.position);
                }

                // Hızlı büyüme ve kaybolma
                float progress = Mathf.Clamp01(_timer / duration);
                float scale = progress < 0.25f
                    ? Mathf.Lerp(0.1f, 1.2f, progress / 0.25f)
                    : Mathf.Lerp(1.2f, 0.0f, (progress - 0.25f) / 0.75f);

                transform.localScale = Vector3.one * scale;

                if (_timer >= duration) Destroy(gameObject);
            }
        }

        private class StunCrownAnim : MonoBehaviour
        {
            public float duration = 2.0f;
            private float _timer = 0f;

            private void Update()
            {
                _timer += Time.deltaTime;
                transform.Rotate(Vector3.up, 240f * Time.deltaTime, Space.Self);

                if (_timer >= duration) Destroy(gameObject);
            }
        }
    }
}
