using UnityEngine;

namespace RetroFPS.Combat
{
    /// <summary>
    /// Sektör standardı, sıfır GC tahsisli ve tam parametrik Dövüş Ses Sistemi (Combat Audio).
    /// Inspector'dan özel AudioClip'ler atanabilir; eğer boş bırakılırsa dinamik olarak
    /// yüksek kaliteli metalik/etsel vuruş sesleri sentezleyerek hiçbir harici ses dosyasına
    /// ihtiyaç duymadan kusursuz bir ses tokluğu (Audio Juice) sunar.
    /// </summary>
    [DisallowMultipleComponent]
    public class CombatAudio : MonoBehaviour
    {
        private static CombatAudio _instance;
        public static CombatAudio Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Object.FindAnyObjectByType<CombatAudio>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("CombatAudio");
                        _instance = go.AddComponent<CombatAudio>();
                    }
                }
                return _instance;
            }
        }

        [Header("Menzilli Silah Sesleri")]
        [Tooltip("Arbalest birincil seri atış sesi. Boşsa dinamik yay teli sesi sentezlenir.")]
        [SerializeField] private AudioClip arbalestFireSound;

        [Tooltip("Arbalest ikincil yay (shotgun) atış sesi.")]
        [SerializeField] private AudioClip arbalestAltSound;

        [Header("Yakın Dövüş Sesleri")]
        [Tooltip("Kılıç savurma hava sesi (Whoosh).")]
        [SerializeField] private AudioClip bladeSwingSound;

        [Tooltip("Kılıç saplama (Thrust) darbe sesi.")]
        [SerializeField] private AudioClip bladeThrustSound;

        [Header("Vuruş & Darbe Geri Bildirimi (Hit Feedback)")]
        [Tooltip("Standart hedefe isabet tıkı (Hitmarker Tick).")]
        [SerializeField] private AudioClip hitmarkerTickSound;

        [Tooltip("Kritik / Headshot kemik kırma sesi (Crit Bone Crunch).")]
        [SerializeField] private AudioClip critCrunchSound;

        [Tooltip("Organik et darbe sesi (Flesh Impact Thud).")]
        [SerializeField] private AudioClip fleshImpactSound;

        [Tooltip("Düşman parçalanma / ölüm patlaması sesi (Gib Explosion).")]
        [SerializeField] private AudioClip gibExplosionSound;

        [Tooltip("Kafa kopması ve basınçlı boyun kan fışkırması sesi (Decapitation Spurt).")]
        [SerializeField] private AudioClip decapitationSound;

        [Tooltip("Uçuşan et ve kemik parçalarının zemine çarpma sesi (Gib Bounce).")]
        [SerializeField] private AudioClip gibBounceSound;

        [Header("Oyuncu Hasar & Acı Sesleri")]
        [Tooltip("Karakter hasar aldığında çalan tok darbe ve acı sesi.")]
        [SerializeField] private AudioClip playerPainSound;

        [Tooltip("Can %25 altına düştüğünde çalan boğuk kalp atışı.")]
        [SerializeField] private AudioClip lowHealthHeartbeatSound;

        [Header("Ses Seviyesi & Ayarlar")]
        [Range(0f, 1f)]
        [SerializeField] private float masterCombatVolume = 0.95f;

        [Range(0.8f, 1.2f)]
        [SerializeField] private float pitchVariation = 0.08f;

        // Önbelleğe alınmış sentezlenmiş ses klipleri
        private static AudioClip _synthArbalestFire;
        private static AudioClip _synthArbalestAlt;
        private static AudioClip _synthBladeSwing;
        private static AudioClip _synthBladeThrust;
        private static AudioClip _synthHitmarkerTick;
        private static AudioClip _synthCritCrunch;
        private static AudioClip _synthFleshImpact;
        private static AudioClip _synthGibExplosion;
        private static AudioClip _synthDecapitation;
        private static AudioClip _synthGibBounce;
        private static AudioClip _synthPlayerPain;
        private static AudioClip _synthHeartbeat;

        // Ses çalma için ayrılmış havuz AudioSource'ları
        private AudioSource _audioSource2D;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(this);
                return;
            }
            _instance = this;

            _audioSource2D = gameObject.AddComponent<AudioSource>();
            _audioSource2D.playOnAwake = false;
            _audioSource2D.spatialBlend = 0f; // 2D net arayüz/oyuncu sesi
            _audioSource2D.volume = masterCombatVolume;

            PrewarmSynthesizedClips();
        }

        private void PrewarmSynthesizedClips()
        {
            if (arbalestFireSound == null && _synthArbalestFire == null) _synthArbalestFire = CreateArbalestFireClip();
            if (arbalestAltSound == null && _synthArbalestAlt == null) _synthArbalestAlt = CreateArbalestAltClip();
            if (bladeSwingSound == null && _synthBladeSwing == null) _synthBladeSwing = CreateBladeSwingClip();
            if (bladeThrustSound == null && _synthBladeThrust == null) _synthBladeThrust = CreateBladeThrustClip();
            if (hitmarkerTickSound == null && _synthHitmarkerTick == null) _synthHitmarkerTick = CreateHitmarkerTickClip();
            if (critCrunchSound == null && _synthCritCrunch == null) _synthCritCrunch = CreateCritCrunchClip();
            if (fleshImpactSound == null && _synthFleshImpact == null) _synthFleshImpact = CreateFleshImpactClip();
            if (gibExplosionSound == null && _synthGibExplosion == null) _synthGibExplosion = CreateGibExplosionClip();
            if (decapitationSound == null && _synthDecapitation == null) _synthDecapitation = CreateDecapitationClip();
            if (gibBounceSound == null && _synthGibBounce == null) _synthGibBounce = CreateGibBounceClip();
            if (playerPainSound == null && _synthPlayerPain == null) _synthPlayerPain = CreatePlayerPainClip();
            if (lowHealthHeartbeatSound == null && _synthHeartbeat == null) _synthHeartbeat = CreateHeartbeatClip();
        }

        #region Public Ses Çalma Metotları

        public void PlayArbalestFire()
        {
            AudioClip clip = arbalestFireSound != null ? arbalestFireSound : _synthArbalestFire;
            Play2DWithPitchVariation(clip, 0.90f, 0.06f);
        }

        public void PlayArbalestAlt()
        {
            AudioClip clip = arbalestAltSound != null ? arbalestAltSound : _synthArbalestAlt;
            Play2DWithPitchVariation(clip, 1.0f, 0.04f);
        }

        public void PlayBladeSwing()
        {
            AudioClip clip = bladeSwingSound != null ? bladeSwingSound : _synthBladeSwing;
            Play2DWithPitchVariation(clip, 0.95f, 0.08f);
        }

        public void PlayBladeThrust()
        {
            AudioClip clip = bladeThrustSound != null ? bladeThrustSound : _synthBladeThrust;
            Play2DWithPitchVariation(clip, 1.0f, 0.05f);
        }

        public void PlayHitmarker(bool isCrit)
        {
            if (isCrit)
            {
                AudioClip critClip = critCrunchSound != null ? critCrunchSound : _synthCritCrunch;
                Play2DWithPitchVariation(critClip, 1.0f, 0.05f);
            }
            else
            {
                AudioClip tickClip = hitmarkerTickSound != null ? hitmarkerTickSound : _synthHitmarkerTick;
                Play2DWithPitchVariation(tickClip, 0.85f, 0.07f);
            }
        }

        public void PlayFleshImpact(Vector3 position, bool isCrit = false)
        {
            AudioClip clip = isCrit
                ? (critCrunchSound != null ? critCrunchSound : _synthCritCrunch)
                : (fleshImpactSound != null ? fleshImpactSound : _synthFleshImpact);

            if (clip != null)
            {
                float volume = isCrit ? 1.0f : 0.85f;
                AudioSource.PlayClipAtPoint(clip, position, volume * masterCombatVolume);
            }
        }

        public void PlayGibExplosion(Vector3 position)
        {
            AudioClip clip = gibExplosionSound != null ? gibExplosionSound : _synthGibExplosion;
            if (clip != null)
            {
                AudioSource.PlayClipAtPoint(clip, position, 1.0f * masterCombatVolume);
            }
        }

        public void PlayDecapitation(Vector3 position)
        {
            AudioClip clip = decapitationSound != null ? decapitationSound : _synthDecapitation;
            if (clip != null)
            {
                AudioSource.PlayClipAtPoint(clip, position, 1.15f * masterCombatVolume);
            }
        }

        public void PlayGibBounce(Vector3 position)
        {
            AudioClip clip = gibBounceSound != null ? gibBounceSound : _synthGibBounce;
            if (clip != null)
            {
                AudioSource.PlayClipAtPoint(clip, position, 0.65f * masterCombatVolume);
            }
        }

        public void PlayPlayerHurt(float damage, bool isLowHealth)
        {
            AudioClip clip = playerPainSound != null ? playerPainSound : _synthPlayerPain;
            Play2DWithPitchVariation(clip, 1.0f, 0.08f);

            if (isLowHealth)
            {
                AudioClip hbClip = lowHealthHeartbeatSound != null ? lowHealthHeartbeatSound : _synthHeartbeat;
                if (hbClip != null && _audioSource2D != null)
                {
                    _audioSource2D.PlayOneShot(hbClip, 0.85f * masterCombatVolume);
                }
            }
        }

        private void Play2DWithPitchVariation(AudioClip clip, float volumeScale, float extraVariation)
        {
            if (clip == null || _audioSource2D == null) return;

            float variation = pitchVariation + extraVariation;
            _audioSource2D.pitch = 1.0f + Random.Range(-variation, variation);
            _audioSource2D.PlayOneShot(clip, volumeScale * masterCombatVolume);
        }

        #endregion

        #region Prosedürel Ses Sentezleyicileri (Procedural Audio Synthesis)

        private static AudioClip CreateArbalestFireClip()
        {
            int sampleRate = 44100;
            float duration = 0.12f;
            int count = (int)(sampleRate * duration);
            float[] data = new float[count];

            for (int i = 0; i < count; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 28f);

                // Gergin çelik yayın boşanması: 680 Hz'den 220 Hz'e anlık frekans kayması
                float freq = Mathf.Lerp(680f, 220f, t / duration);
                float tone = Mathf.Sin(2f * Mathf.PI * freq * t);
                float noise = (Random.value * 2f - 1f) * Mathf.Exp(-t * 80f) * 0.45f;

                data[i] = Mathf.Clamp((tone + noise) * env, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("Synth_ArbalestFire", count, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateArbalestAltClip()
        {
            int sampleRate = 44100;
            float duration = 0.22f;
            int count = (int)(sampleRate * duration);
            float[] data = new float[count];

            for (int i = 0; i < count; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 16f);

                // 5 yayın aynı anda patlaması: Ağır bas darbesi + kordal çınlama
                float bass = Mathf.Sin(2f * Mathf.PI * 140f * t) * 0.7f;
                float mid = Mathf.Sin(2f * Mathf.PI * 440f * t) * 0.4f;
                float noise = (Random.value * 2f - 1f) * Mathf.Exp(-t * 45f) * 0.5f;

                data[i] = Mathf.Clamp((bass + mid + noise) * env, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("Synth_ArbalestAlt", count, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateBladeSwingClip()
        {
            int sampleRate = 44100;
            float duration = 0.18f;
            int count = (int)(sampleRate * duration);
            float[] data = new float[count];

            for (int i = 0; i < count; i++)
            {
                float t = (float)i / sampleRate;
                // Çan eğrisi zarfı (ortada en yüksek)
                float env = Mathf.Sin((t / duration) * Mathf.PI);

                // Aerodinamik rüzgar süpürmesi: 280 Hz -> 520 Hz -> 190 Hz
                float freq = 280f + 240f * Mathf.Sin((t / duration) * Mathf.PI);
                float tone = Mathf.Sin(2f * Mathf.PI * freq * t);
                float noise = (Random.value * 2f - 1f) * 0.6f;

                data[i] = Mathf.Clamp((tone * 0.5f + noise * 0.5f) * env * 0.9f, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("Synth_BladeSwing", count, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateBladeThrustClip()
        {
            int sampleRate = 44100;
            float duration = 0.22f;
            int count = (int)(sampleRate * duration);
            float[] data = new float[count];

            for (int i = 0; i < count; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 18f);

                float bass = Mathf.Sin(2f * Mathf.PI * 95f * t) * 0.8f;
                float ring = Mathf.Sin(2f * Mathf.PI * 1250f * t) * 0.35f;
                float noise = (Random.value * 2f - 1f) * Mathf.Exp(-t * 50f) * 0.4f;

                data[i] = Mathf.Clamp((bass + ring + noise) * env, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("Synth_BladeThrust", count, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateHitmarkerTickClip()
        {
            int sampleRate = 44100;
            float duration = 0.045f;
            int count = (int)(sampleRate * duration);
            float[] data = new float[count];

            for (int i = 0; i < count; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 70f);

                // Keskin 2200 Hz yüksek frekanslı arayüz tıkı
                float tone = Mathf.Sin(2f * Mathf.PI * 2200f * t);
                data[i] = Mathf.Clamp(tone * env * 0.75f, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("Synth_HitmarkerTick", count, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateCritCrunchClip()
        {
            int sampleRate = 44100;
            float duration = 0.14f;
            int count = (int)(sampleRate * duration);
            float[] data = new float[count];

            for (int i = 0; i < count; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 20f);

                // Kemik kırılma rezonansı: 880 Hz ve 1760 Hz + 180 Hz tok gövde
                float bass = Mathf.Sin(2f * Mathf.PI * 180f * t) * 0.6f;
                float high = Mathf.Sin(2f * Mathf.PI * 880f * t) * 0.4f;
                float crunchNoise = (Random.value * 2f - 1f) * Mathf.Exp(-t * 60f) * 0.7f;

                data[i] = Mathf.Clamp((bass + high + crunchNoise) * env, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("Synth_CritCrunch", count, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateFleshImpactClip()
        {
            int sampleRate = 44100;
            float duration = 0.10f;
            int count = (int)(sampleRate * duration);
            float[] data = new float[count];

            for (int i = 0; i < count; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 30f);

                float low = Mathf.Sin(2f * Mathf.PI * 120f * t) * 0.8f;
                float slap = (Random.value * 2f - 1f) * Mathf.Exp(-t * 80f) * 0.5f;

                data[i] = Mathf.Clamp((low + slap) * env, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("Synth_FleshImpact", count, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateGibExplosionClip()
        {
            int sampleRate = 44100;
            float duration = 0.30f;
            int count = (int)(sampleRate * duration);
            float[] data = new float[count];

            for (int i = 0; i < count; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 12f);

                // Ağır patlama bası: 75 Hz -> 30 Hz
                float freq = Mathf.Lerp(75f, 30f, t / duration);
                float sub = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.9f;
                float goreNoise = (Random.value * 2f - 1f) * Mathf.Exp(-t * 22f) * 0.65f;

                data[i] = Mathf.Clamp((sub + goreNoise) * env, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("Synth_GibExplosion", count, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateDecapitationClip()
        {
            int sampleRate = 44100;
            float duration = 0.40f;
            int count = (int)(sampleRate * duration);
            float[] data = new float[count];

            for (int i = 0; i < count; i++)
            {
                float t = (float)i / sampleRate;
                // 1. Keskin kemik kopması / et kesiği (0 - 0.08s)
                float sliceEnv = Mathf.Exp(-t * 35f);
                float sliceFreq = Mathf.Lerp(620f, 180f, t / 0.08f);
                float sliceTone = Mathf.Sin(2f * Mathf.PI * sliceFreq * t) * sliceEnv * 0.75f;

                // 2. Basınçlı arter kan fışkırması (0.04s - 0.40s)
                float spurtT = Mathf.Max(0f, t - 0.04f);
                float spurtEnv = Mathf.Exp(-spurtT * 8f) * (t > 0.04f ? 1f : 0f);
                float spurtNoise = (Random.value * 2f - 1f) * spurtEnv * 0.70f;

                // 3. Tok boyun kütüğü
                float thud = Mathf.Sin(2f * Mathf.PI * 110f * t) * sliceEnv * 0.5f;

                data[i] = Mathf.Clamp(sliceTone + spurtNoise + thud, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("Synth_Decapitation", count, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateGibBounceClip()
        {
            int sampleRate = 44100;
            float duration = 0.10f;
            int count = (int)(sampleRate * duration);
            float[] data = new float[count];

            for (int i = 0; i < count; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 45f);

                // Islak zemin çarpması: 160 Hz -> 50 Hz tok et sesi + hafif çıtırtı
                float tone = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(160f, 50f, t / duration) * t) * 0.65f;
                float squelch = (Random.value * 2f - 1f) * Mathf.Exp(-t * 60f) * 0.50f;

                data[i] = Mathf.Clamp((tone + squelch) * env, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("Synth_GibBounce", count, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreatePlayerPainClip()
        {
            int sampleRate = 44100;
            float duration = 0.18f;
            int count = (int)(sampleRate * duration);
            float[] data = new float[count];

            for (int i = 0; i < count; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 22f);

                // Göğse/zırha inen ağır darbe kütüğü: 85 Hz sub-bass + 210 Hz rezonans + nefes hırıltısı
                float bass = Mathf.Sin(2f * Mathf.PI * 85f * t) * 0.85f;
                float mid = Mathf.Sin(2f * Mathf.PI * 210f * t) * 0.35f;
                float noise = (Random.value * 2f - 1f) * Mathf.Exp(-t * 35f) * 0.45f;

                data[i] = Mathf.Clamp((bass + mid + noise) * env, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("Synth_PlayerPain", count, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateHeartbeatClip()
        {
            int sampleRate = 44100;
            float duration = 0.32f;
            int count = (int)(sampleRate * duration);
            float[] data = new float[count];

            for (int i = 0; i < count; i++)
            {
                float t = (float)i / sampleRate;

                // Çift kalp vuruşu (LUB-DUB: 0.0s ve 0.14s)
                float t1 = t;
                float t2 = Mathf.Max(0f, t - 0.14f);

                float env1 = Mathf.Exp(-t1 * 32f);
                float env2 = t >= 0.14f ? Mathf.Exp(-t2 * 28f) * 0.75f : 0f;

                float thump1 = Mathf.Sin(2f * Mathf.PI * 55f * t1) * env1;
                float thump2 = Mathf.Sin(2f * Mathf.PI * 45f * t2) * env2;

                data[i] = Mathf.Clamp((thump1 + thump2) * 0.95f, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("Synth_Heartbeat", count, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        #endregion
    }
}
