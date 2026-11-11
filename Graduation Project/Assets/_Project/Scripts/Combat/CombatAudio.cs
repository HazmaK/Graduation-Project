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
        private static bool _isQuitting;

        /// <summary>
        /// Tembel (lazy) erişim: sahne-yükleme ön ısıtmasından önce (ör. Awake içinde) veya additive
        /// sahnelerde de null dönmez. Sıcak yolda yalnızca tek bir null kontrolü maliyeti vardır.
        /// </summary>
        public static CombatAudio Instance
        {
            get
            {
                if (_instance == null && !_isQuitting) EnsureInstance();
                return _instance;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetInstance()
        {
            _instance = null;
            _isQuitting = false;
            Application.quitting -= HandleQuitting;
            Application.quitting += HandleQuitting;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= EnsureSceneInstance;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += EnsureSceneInstance;
        }

        private static void HandleQuitting() => _isQuitting = true;

        private static void EnsureSceneInstance(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            if (_instance != null || _isQuitting) return;
            EnsureInstance();
        }

        private static void EnsureInstance()
        {
            _instance = Object.FindAnyObjectByType<CombatAudio>();
            if (_instance == null) _instance = new GameObject("CombatAudio").AddComponent<CombatAudio>();
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        [Header("Menzilli Silah Sesleri")]
        [Tooltip("Arbalest birincil seri atış sesi. Boşsa dinamik yay teli sesi sentezlenir.")]
        [SerializeField] private AudioClip arbalestFireSound;

        [Tooltip("Arbalest ikincil yay (shotgun) atış sesi.")]
        [SerializeField] private AudioClip arbalestAltSound;

        [Header("Gotik Çift Namlulu Pompalı & Grapple Hook Sesleri")]
        [Tooltip("Çift namlulu pompalı gök gürültüsü patlama sesi.")]
        [SerializeField] private AudioClip shotgunFireSound;

        [Tooltip("Kanca fırlatma ve zincir boşalma sesi.")]
        [SerializeField] private AudioClip grappleLaunchSound;

        [Tooltip("Kancanın hedefe saplanma ve metalik kilitlenme sesi.")]
        [SerializeField] private AudioClip grappleHookLatchSound;

        [Tooltip("Boş tetik tıkı (Kuru tetik / Dry Fire).")]
        [SerializeField] private AudioClip dryFireSound;

        [Tooltip("Cephane toplandığında çalan metalik kurma/doldurma sesi.")]
        [SerializeField] private AudioClip ammoPickupSound;

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

        [Header("Ödül & İyileşme Sesleri")]
        [Tooltip("Can küresi toplandığında çalan ferahlatıcı/okült kristal ses.")]
        [SerializeField] private AudioClip healthPickupSound;

        [Header("Ses Seviyesi & Ayarlar")]
        [Range(0f, 1f)]
        [SerializeField] private float masterCombatVolume = 0.95f;

        [Range(0f, 0.06f)]
        [SerializeField] private float pitchVariation = 0.06f;

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
        private static AudioClip _synthHealthPickup;
        private static AudioClip _synthShotgunFire;
        private static AudioClip _synthGrappleLaunch;
        private static AudioClip _synthGrappleHookLatch;
        private static AudioClip _synthDryFire;
        private static AudioClip _synthAmmoPickup;

        // Ses çalma için ayrılmış havuz AudioSource'ları
        private readonly AudioSource[] _sources2D = new AudioSource[6];
        private readonly AudioSource[] _sources3D = new AudioSource[12];
        private int _next2D;
        private int _next3D;
        private float _lastHitmarkerTime = float.NegativeInfinity;
        private float _lastFleshTime = float.NegativeInfinity;
        private float _lastBounceTime = float.NegativeInfinity;
        private static AudioClip _synthJump, _synthLand, _synthDash, _synthSlide;
        private static AudioClip _synthFootstep, _synthSwitch, _synthKill, _synthCriticalKill;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(this);
                return;
            }
            _instance = this;

            InitializeSources(_sources2D, false);
            InitializeSources(_sources3D, true);
            PrewarmSynthesizedClips();
        }

        private void InitializeSources(AudioSource[] sources, bool spatial)
        {
            for (int i = 0; i < sources.Length; i++)
            {
                GameObject child = new GameObject(spatial ? "Combat_3D_Voice" : "Combat_2D_Voice");
                child.transform.SetParent(transform, false);
                AudioSource source = child.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = spatial ? 1f : 0f;
                source.minDistance = 1f;
                source.maxDistance = 500f;
                source.rolloffMode = AudioRolloffMode.Logarithmic;
                source.dopplerLevel = 0f;
                sources[i] = source;
            }
        }

        private void PrewarmSynthesizedClips()
        {
            if (_synthJump == null) _synthJump = CreateMovementClip("Jump", 0.09f, 0);
            if (_synthLand == null) _synthLand = CreateMovementClip("Land", 0.13f, 1);
            if (_synthDash == null) _synthDash = CreateMovementClip("Dash", 0.18f, 2);
            if (_synthSlide == null) _synthSlide = CreateMovementClip("Slide", 0.25f, 3);
            if (_synthFootstep == null) _synthFootstep = CreateMovementClip("Footstep", 0.05f, 4);
            if (_synthSwitch == null) _synthSwitch = CreateMovementClip("WeaponSwitch", 0.07f, 5);
            if (_synthKill == null) _synthKill = CreateKillConfirmClip(false);
            if (_synthCriticalKill == null) _synthCriticalKill = CreateKillConfirmClip(true);
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
            if (healthPickupSound == null && _synthHealthPickup == null) _synthHealthPickup = CreateHealthPickupClip();
            if (shotgunFireSound == null && _synthShotgunFire == null) _synthShotgunFire = CreateShotgunFireClip();
            if (grappleLaunchSound == null && _synthGrappleLaunch == null) _synthGrappleLaunch = CreateGrappleLaunchClip();
            if (grappleHookLatchSound == null && _synthGrappleHookLatch == null) _synthGrappleHookLatch = CreateGrappleHookLatchClip();
            if (dryFireSound == null && _synthDryFire == null) _synthDryFire = CreateDryFireClip();
            if (ammoPickupSound == null && _synthAmmoPickup == null) _synthAmmoPickup = CreateAmmoPickupClip();
        }

        #region Public Ses Çalma Metotları

        public void PlayShotgunFire()
        {
            Play2D(shotgunFireSound != null ? shotgunFireSound : _synthShotgunFire, 1.0f, Random.Range(0.94f, 1.06f));
        }

        public void PlayGrappleLaunch()
        {
            AudioClip clip = grappleLaunchSound != null ? grappleLaunchSound : _synthGrappleLaunch;
            Play2DWithPitchVariation(clip, 0.95f, 0.04f);
        }

        public void PlayGrappleHookLatch(Vector3 position)
        {
            AudioClip clip = grappleHookLatchSound != null ? grappleHookLatchSound : _synthGrappleHookLatch;
            if (clip != null)
            {
                PlayClipAt(clip, position);
            }
            Play2DWithPitchVariation(clip, 0.85f, 0.05f);
        }

        public void PlayDryFire()
        {
            AudioClip clip = dryFireSound != null ? dryFireSound : _synthDryFire;
            Play2DWithPitchVariation(clip, 0.85f, 0.04f);
        }

        public void PlayAmmoPickup()
        {
            AudioClip clip = ammoPickupSound != null ? ammoPickupSound : _synthAmmoPickup;
            Play2DWithPitchVariation(clip, 0.95f, 0.05f);
        }

        public void PlayArbalestFire()
        {
            Play2D(arbalestFireSound != null ? arbalestFireSound : _synthArbalestFire, 0.90f, Random.Range(0.94f, 1.06f));
        }

        public void PlayArbalestAlt()
        {
            Play2D(arbalestAltSound != null ? arbalestAltSound : _synthArbalestAlt, 1.0f, Random.Range(0.94f, 1.06f));
        }

        public void PlayBladeSwing(float pitchMult = 1.0f)
        {
            AudioClip clip = bladeSwingSound != null ? bladeSwingSound : _synthBladeSwing;
            Play2D(clip, 0.95f, pitchMult * Random.Range(0.94f, 1.06f));
        }

        public void PlayBladeThrust()
        {
            AudioClip clip = bladeThrustSound != null ? bladeThrustSound : _synthBladeThrust;
            Play2DWithPitchVariation(clip, 1.0f, 0.05f);
        }

        public void PlayBladeFinisher()
        {
            AudioClip clip = bladeThrustSound != null ? bladeThrustSound : _synthBladeThrust;
            Play2DWithPitchVariation(clip, 1.20f, 0.04f);
        }

        public void PlayBladeLunge()
        {
            AudioClip clip = grappleLaunchSound != null ? grappleLaunchSound : _synthGrappleLaunch;
            Play2DWithPitchVariation(clip, 1.10f, 0.05f);
        }

        public void PlayBladeSlam(Vector3 position)
        {
            AudioClip clip = gibExplosionSound != null ? gibExplosionSound : _synthGibExplosion;
            if (clip != null)
            {
                PlayClipAt(clip, position, 1.2f);
            }
            Play2DWithPitchVariation(clip, 0.85f, 0.05f);
        }

        public void PlayCritCrunch()
        {
            AudioClip critClip = critCrunchSound != null ? critCrunchSound : _synthCritCrunch;
            Play2DWithPitchVariation(critClip, 1.0f, 0.05f);
        }

        public void PlayHitmarker(bool isCrit)
        {
            float now = Time.unscaledTime;
            if (now - _lastHitmarkerTime < 0.045f) return;
            _lastHitmarkerTime = now;
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
            float now = Time.unscaledTime;
            if (now - _lastFleshTime < 0.03f) return;
            _lastFleshTime = now;
            AudioClip clip = isCrit
                ? (critCrunchSound != null ? critCrunchSound : _synthCritCrunch)
                : (fleshImpactSound != null ? fleshImpactSound : _synthFleshImpact);

            if (clip != null)
            {
                float volume = isCrit ? 1.0f : 0.85f;
                PlayClipAt(clip, position, volume, Random.Range(0.94f, 1.06f));
            }
        }

        public void PlayGibExplosion(Vector3 position)
        {
            AudioClip clip = gibExplosionSound != null ? gibExplosionSound : _synthGibExplosion;
            if (clip != null)
            {
                PlayClipAt(clip, position);
            }
        }

        public void PlayDecapitation(Vector3 position)
        {
            AudioClip clip = decapitationSound != null ? decapitationSound : _synthDecapitation;
            if (clip != null)
            {
                PlayClipAt(clip, position, 1.15f);
            }
        }

        public void PlayGibBounce(Vector3 position)
        {
            float now = Time.unscaledTime;
            if (now - _lastBounceTime < 0.05f) return;
            _lastBounceTime = now;
            AudioClip clip = gibBounceSound != null ? gibBounceSound : _synthGibBounce;
            if (clip != null)
            {
                PlayClipAt(clip, position, 0.65f);
            }
        }

        public void PlayPlayerHurt(float damage, bool isLowHealth)
        {
            AudioClip clip = playerPainSound != null ? playerPainSound : _synthPlayerPain;
            Play2DWithPitchVariation(clip, 1.0f, 0.08f);

            if (isLowHealth)
            {
                AudioClip hbClip = lowHealthHeartbeatSound != null ? lowHealthHeartbeatSound : _synthHeartbeat;
                Play2D(hbClip, 0.85f, 1f);
            }
        }

        public void PlayHealthPickup()
        {
            AudioClip clip = healthPickupSound != null ? healthPickupSound : _synthHealthPickup;
            Play2DWithPitchVariation(clip, 0.95f, 0.05f);
        }

        private void Play2DWithPitchVariation(AudioClip clip, float volumeScale, float extraVariation)
        {
            float variation = Mathf.Min(0.06f, Mathf.Max(0f, pitchVariation + extraVariation));
            Play2D(clip, volumeScale, 1f + Random.Range(-variation, variation));
        }

        public void PlayJump() => Play2D(_synthJump, 0.55f, 1f);
        public void PlayLand(float intensity01)
        {
            float intensity = Mathf.Clamp01(intensity01);
            Play2D(_synthLand, Mathf.Lerp(0.25f, 0.95f, intensity), Mathf.Lerp(1.15f, 0.75f, intensity));
        }
        public void PlayDash() => Play2D(_synthDash, 0.75f, 1f);
        public void PlaySlideStart() => Play2D(_synthSlide, 0.65f, 1f);
        public void PlayFootstep() => Play2D(_synthFootstep, 0.25f, Random.Range(0.94f, 1.06f));
        public void PlayWeaponSwitch() => Play2D(_synthSwitch, 0.60f, 1f);
        public void PlayKillConfirm(bool wasCrit) => Play2D(wasCrit ? _synthCriticalKill : _synthKill, 0.85f, 1f);

        public void PlayClipAt(AudioClip clip, Vector3 pos, float volume = 1f, float pitch = 1f)
        {
            if (clip == null) return;
            AudioSource source = AcquireVoice(_sources3D, ref _next3D);
            if (source == null) return;
            source.Stop();
            source.transform.position = pos;
            source.clip = clip;
            source.pitch = pitch;
            source.volume = Mathf.Clamp01(volume * masterCombatVolume);
            source.Play();
        }

        private void Play2D(AudioClip clip, float volume, float pitch)
        {
            if (clip == null) return;
            AudioSource source = AcquireVoice(_sources2D, ref _next2D);
            if (source == null) return;
            source.Stop();
            source.clip = clip;
            source.pitch = pitch;
            source.volume = Mathf.Clamp01(volume * masterCombatVolume);
            source.Play();
        }

        /// <summary>
        /// Önce çalmayan ilk sesi (halka sırasından başlayarak) seçer; hepsi meşgulse halka sırasındaki
        /// sıradaki sesi çalar (voice stealing). Tahsis yok.
        /// </summary>
        private static AudioSource AcquireVoice(AudioSource[] pool, ref int next)
        {
            int count = pool.Length;
            for (int i = 0; i < count; i++)
            {
                int index = (next + i) % count;
                AudioSource candidate = pool[index];
                if (candidate != null && !candidate.isPlaying)
                {
                    next = (index + 1) % count;
                    return candidate;
                }
            }

            AudioSource stolen = pool[next];
            next = (next + 1) % count;
            return stolen;
        }

        #endregion

        private static AudioClip CreateMovementClip(string clipName, float duration, int kind)
        {
            const int rate = 44100;
            int count = Mathf.CeilToInt(rate * duration);
            float[] samples = new float[count];
            float filteredNoise = 0f;
            float phase = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / rate;
                float progress = t / duration;
                filteredNoise = Mathf.Lerp(filteredNoise, Random.Range(-1f, 1f), kind == 3 ? 0.65f : 0.18f);
                float frequency = kind == 1 ? Mathf.Lerp(140f, 45f, progress) :
                    kind == 4 ? 115f : kind == 5 ? 1850f : Mathf.Lerp(480f, 130f, progress);
                phase += 2f * Mathf.PI * frequency / rate;
                float tone = Mathf.Sin(phase);
                float envelope = Mathf.Min(t * 1500f, 1f) * Mathf.Pow(1f - progress, kind == 3 ? 1.4f : 2.8f);
                float signal = kind == 1 ? tone * 0.8f + filteredNoise * 0.25f :
                    kind == 4 ? tone * 0.45f + filteredNoise * 0.45f :
                    kind == 5 ? tone * 0.4f + Mathf.Sin(phase * 1.47f) * 0.25f + filteredNoise * 0.5f :
                    filteredNoise * 1.6f + tone * (kind == 0 ? 0.16f : 0.04f);
                samples[i] = Mathf.Clamp(signal * envelope, -1f, 1f);
            }
            AudioClip clip = AudioClip.Create(clipName, count, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateKillConfirmClip(bool critical)
        {
            const int rate = 44100;
            const float duration = 0.14f;
            int count = Mathf.CeilToInt(rate * duration);
            float[] samples = new float[count];
            float frequency = critical ? 1760f : 1320f;
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / rate;
                float env = Mathf.Min(t * 2000f, 1f) * Mathf.Pow(1f - t / duration, 2f);
                float secondTime = Mathf.Max(0f, t - 0.045f);
                float first = Mathf.Sin(2f * Mathf.PI * frequency * t) * Mathf.Exp(-t * 25f);
                float second = Mathf.Sin(2f * Mathf.PI * frequency * 1.5f * secondTime) * Mathf.Exp(-secondTime * 18f);
                float overtone = critical ? Mathf.Sin(2f * Mathf.PI * frequency * 3f * t) * 0.15f : 0f;
                samples[i] = (first * 0.5f + second * 0.45f + overtone) * env;
            }
            AudioClip clip = AudioClip.Create(critical ? "CriticalKillConfirm" : "KillConfirm", count, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }


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

        private static AudioClip CreateHealthPickupClip()
        {
            int sampleRate = 44100;
            float duration = 0.28f;
            int count = (int)(sampleRate * duration);
            float[] data = new float[count];

            for (int i = 0; i < count; i++)
            {
                float t = (float)i / sampleRate;
                float progress = t / duration;

                // İki aşamalı yükselen kristal tınısı (520Hz -> 880Hz -> 1320Hz arpej)
                float freq = Mathf.Lerp(520f, 1320f, Mathf.Pow(progress, 0.65f));
                float env = Mathf.Exp(-t * 11f);

                // Temel tını + 2. harmonik kristal çınlaması
                float fundamental = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.70f;
                float overtone = Mathf.Sin(2f * Mathf.PI * freq * 2.02f * t) * 0.30f;
                float shimmer = Mathf.Sin(2f * Mathf.PI * 2640f * t) * Mathf.Exp(-t * 22f) * 0.25f;

                data[i] = Mathf.Clamp((fundamental + overtone + shimmer) * env, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("Synth_HealthPickup", count, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateShotgunFireClip()
        {
            int sampleRate = 44100;
            float duration = 0.42f;
            int count = (int)(sampleRate * duration);
            float[] data = new float[count];

            for (int i = 0; i < count; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 9.5f);

                // Çift namlulu patlama: 75Hz derin gök gürültüsü bası + 180Hz göğüs darbesi
                float bass1 = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(90f, 38f, t / duration) * t) * 0.85f;
                float bass2 = Mathf.Sin(2f * Mathf.PI * 55f * t) * 0.45f;
                // Barut alevi ve saçma patlaması gürültüsü
                float noise = (Random.value * 2f - 1f) * Mathf.Exp(-t * 32f) * 0.95f;
                float lowRumble = (Random.value * 2f - 1f) * Mathf.Exp(-t * 6f) * 0.25f;

                data[i] = Mathf.Clamp((bass1 + bass2 + noise + lowRumble) * env, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("Synth_ShotgunFire", count, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateGrappleLaunchClip()
        {
            int sampleRate = 44100;
            float duration = 0.18f;
            int count = (int)(sampleRate * duration);
            float[] data = new float[count];

            for (int i = 0; i < count; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 18f);

                // Basınçlı kanca fırlatma ıslığı ve çelik kablo boşalması
                float freq = Mathf.Lerp(1400f, 420f, t / duration);
                float whoosh = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.65f;
                float cableNoise = (Random.value * 2f - 1f) * Mathf.Exp(-t * 22f) * 0.45f;

                data[i] = Mathf.Clamp((whoosh + cableNoise) * env, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("Synth_GrappleLaunch", count, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateGrappleHookLatchClip()
        {
            int sampleRate = 44100;
            float duration = 0.16f;
            int count = (int)(sampleRate * duration);
            float[] data = new float[count];

            for (int i = 0; i < count; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 24f);

                // Sivri çengelin hedefe kilitlenme çınlaması (1850Hz metal tınısı + et darbesi)
                float metalRing = Mathf.Sin(2f * Mathf.PI * 1850f * t) * 0.7f;
                float boneCrunch = (Random.value * 2f - 1f) * Mathf.Exp(-t * 65f) * 0.6f;

                data[i] = Mathf.Clamp((metalRing + boneCrunch) * env, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("Synth_GrappleLatch", count, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateDryFireClip()
        {
            int sampleRate = 44100;
            float duration = 0.07f;
            int count = (int)(sampleRate * duration);
            float[] data = new float[count];

            for (int i = 0; i < count; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 75f);

                // İğnenin boş yuvaya vurma tıkı (Kuru horoz düşmesi)
                float click = Mathf.Sin(2f * Mathf.PI * 2200f * t) * 0.5f;
                float metallicHollow = Mathf.Sin(2f * Mathf.PI * 650f * t) * 0.5f;

                data[i] = Mathf.Clamp((click + metallicHollow) * env, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("Synth_DryFire", count, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateAmmoPickupClip()
        {
            int sampleRate = 44100;
            float duration = 0.20f;
            int count = (int)(sampleRate * duration);
            float[] data = new float[count];

            for (int i = 0; i < count; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 16f);

                // Pirinç fişeklerin şarjör yatağına oturma çift çınlaması (980Hz & 1420Hz)
                float clink1 = Mathf.Sin(2f * Mathf.PI * 980f * t) * 0.5f;
                float clink2 = (t > 0.05f) ? Mathf.Sin(2f * Mathf.PI * 1420f * (t - 0.05f)) * 0.6f * Mathf.Exp(-(t - 0.05f) * 22f) : 0f;

                data[i] = Mathf.Clamp((clink1 + clink2) * env, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("Synth_AmmoPickup", count, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        #endregion
    }
}
