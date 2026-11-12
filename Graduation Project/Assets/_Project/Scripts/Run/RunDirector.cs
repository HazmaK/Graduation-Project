using System;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;
using RetroFPS.Combat;

namespace RetroFPS.Run
{
    /// <summary>
    /// Koşu (Run) durum makinesini, ölüm sahipliğini ve Kule ↔ Araf (Purgatory) sahne geçişlerini
    /// yöneten ana orkestratör bileşen.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-500)]
    public class RunDirector : MonoBehaviour
    {
        [Header("Referanslar")]
        [Tooltip("Oyuncu can bileşeni. Boş bırakılırsa Awake'te otomatik bulunur.")]
        [SerializeField] private PlayerHealth player;

        [Tooltip("Ekran kararma ve mesaj geçiş sunumu. Boşsa aynı objeden alınır veya eklenir.")]
        [SerializeField] private ArafTransition transition;

        [Header("Sahne ve Doğuş Ayarları")]
        [Tooltip("Başlangıçta additive olarak yüklenecek Araf sahnesinin adı")]
        [SerializeField] private string arafSceneName = "Araf";

        [Tooltip("Gerçek ölüm sonrası yeniden yüklenecek kule sahnesinin adı (boşsa başlangıçtaki aktif sahne)")]
        [SerializeField] private string towerSceneName;

        [Tooltip("Oyuncu aktif bir karşılaşma (oda savaşı) dışında öldüğünde Araf'tan dönüşte doğacağı yedek nokta")]
        [SerializeField] private Transform towerFallbackRespawn;

        [Header("Koşu Tohumu ve Tanılama")]
        [Tooltip("0 ise çalışma zamanında rastgele tohum üretilir; sıfır dışı ise sabit tohum kullanılır")]
        [SerializeField] private int fixedSeed = 0;

        [Tooltip("Evre geçişlerini konsola yazdır")]
        [SerializeField] private bool logTransitions = true;

        private RunState _state;
        private Scene _towerScene;
        private Scene _arafScene;
        private ArafLevel _arafLevel;
        private IRetryableEncounter _pendingEncounter;
        private Vector3 _deathPosition;
        private Quaternion _deathRotation = Quaternion.identity;
        private bool _isTransitioning;
        private bool _restartRequested;

        /// <summary>
        /// Mevcut koşunun saf durum verisi.
        /// </summary>
        public RunState State => _state;

        /// <summary>
        /// Mevcut koşu evresi.
        /// </summary>
        public RunPhase Phase => _state != null ? _state.Phase : RunPhase.Tower;

        private void Awake()
        {
            int seed = fixedSeed != 0 ? fixedSeed : Environment.TickCount;
            _state = new RunState(seed);
            RunEvents.SetCurrentRun(_state);
            if (logTransitions)
            {
                Debug.Log($"<color=crimson><b>[RUN DIRECTOR]</b></color> Koşu başladı (Seed: {_state.Seed}).");
            }

            _towerScene = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(towerSceneName))
            {
                towerSceneName = _towerScene.name;
            }

            if (player == null)
            {
                player = FindAnyObjectByType<PlayerHealth>();
            }

            if (player != null)
            {
                player.ExternalDeathControl = true;
            }

            if (transition == null && !TryGetComponent(out transition))
            {
                transition = gameObject.AddComponent<ArafTransition>();
            }
        }

        private void OnEnable()
        {
            if (player != null)
            {
                player.OnDied += HandlePlayerDied;
            }
            RunEvents.OnEscapePortalReached += HandleEscapePortal;
        }

        private void OnDisable()
        {
            if (player != null)
            {
                player.OnDied -= HandlePlayerDied;
            }
            RunEvents.OnEscapePortalReached -= HandleEscapePortal;
        }

        private void OnDestroy()
        {
            if (RunEvents.CurrentRun == _state)
            {
                RunEvents.SetCurrentRun(null);
            }
            if (player != null)
            {
                player.ExternalDeathControl = false;
            }
        }

        private async void Start()
        {
            try
            {
                await PreloadArafSceneAsync(destroyCancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Sahne/nesne yok edildiğinde iptal normaldir.
            }
        }

        private void Update()
        {
            _state?.AdvanceElapsedTime(Time.unscaledDeltaTime);
        }

        /// <summary>Starts one fresh run in the saved tower scene after true death.</summary>
        public void RestartRun()
        {
            if (_state == null || _state.Phase != RunPhase.TrueDeath || _isTransitioning || _restartRequested ||
                string.IsNullOrEmpty(towerSceneName)) return;
            _restartRequested = true;
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;
            SceneManager.LoadScene(towerSceneName, LoadSceneMode.Single);
        }

        private async Awaitable PreloadArafSceneAsync(CancellationToken ct)
        {
            if (string.IsNullOrEmpty(arafSceneName))
            {
                Debug.LogWarning("[RunDirector] Araf sahne adı boş; Araf sistemi devre dışı (ölümler doğrudan Gerçek Ölüm olur).", this);
                return;
            }

            Scene existing = SceneManager.GetSceneByName(arafSceneName);
            if (!existing.IsValid() || !existing.isLoaded)
            {
                if (!Application.CanStreamedLevelBeLoaded(arafSceneName))
                {
                    Debug.LogWarning($"[RunDirector] '{arafSceneName}' sahnesi Build Settings içinde bulunamadı; Araf devre dışı bırakıldı.", this);
                    return;
                }

                AsyncOperation loadOp = SceneManager.LoadSceneAsync(arafSceneName, LoadSceneMode.Additive);
                if (loadOp == null)
                {
                    Debug.LogWarning($"[RunDirector] '{arafSceneName}' sahnesi additive olarak yüklenemedi; Araf devre dışı bırakıldı.", this);
                    return;
                }

                await loadOp;
                ct.ThrowIfCancellationRequested();
            }

            _arafScene = SceneManager.GetSceneByName(arafSceneName);
            if (!_arafScene.IsValid() || !_arafScene.isLoaded)
            {
                Debug.LogWarning($"[RunDirector] '{arafSceneName}' sahnesi yüklendikten sonra doğrulanamadı; Araf devre dışı.", this);
                return;
            }

            GameObject[] roots = _arafScene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                _arafLevel = roots[i].GetComponentInChildren<ArafLevel>(true);
                if (_arafLevel != null) break;
            }

            if (_arafLevel == null)
            {
                Debug.LogWarning($"[RunDirector] '{arafSceneName}' sahnesinde ArafLevel bileşeni bulunamadı; Araf devre dışı.", this);
                return;
            }

            _arafLevel.SetContentActive(false);

            if (logTransitions)
            {
                Debug.Log($"<color=crimson><b>[RUN DIRECTOR]</b></color> Araf sahnesi ('{arafSceneName}') arka planda hazırlandı (Seed: {_state.Seed}).");
            }
        }

        private void HandlePlayerDied()
        {
            if (_isTransitioning || _state == null) return;

            bool arafReady = _arafLevel != null && _arafScene.IsValid() && _arafScene.isLoaded;
            if (_state.Phase == RunPhase.Tower && _state.CanEnterAraf && arafReady)
            {
                _ = EnterArafAsync(destroyCancellationToken);
            }
            else
            {
                TrueDeath();
            }
        }

        private async Awaitable EnterArafAsync(CancellationToken ct)
        {
            _isTransitioning = true;
            try
            {
                SetPhase(RunPhase.FallingToAraf);

                if (player != null)
                {
                    _deathPosition = player.transform.position;
                    _deathRotation = Quaternion.Euler(0f, player.transform.eulerAngles.y, 0f);
                }

                IRetryableEncounter activeEncounter = RunEvents.ActiveEncounter;
                _pendingEncounter = (activeEncounter != null && activeEncounter.CanResumeAfterDeath)
                    ? activeEncounter
                    : null;

                _pendingEncounter?.SuspendForPurgatory();
                _state.ConsumeArafCharge();

                if (transition != null)
                {
                    transition.BeginSlowMo();
                    await transition.FadeOut(transition.ArafTint, transition.ArafEnterMessage, ct);
                }

                _arafLevel.SetContentActive(true);
                if (_arafScene.IsValid() && _arafScene.isLoaded)
                {
                    SceneManager.SetActiveScene(_arafScene);
                }

                if (player != null)
                {
                    Transform entry = _arafLevel.EntryPoint != null ? _arafLevel.EntryPoint : _arafLevel.transform;
                    Quaternion entryRot = Quaternion.Euler(0f, entry.eulerAngles.y, 0f);
                    player.Revive(entry.position, entryRot);
                }

                if (transition != null)
                {
                    transition.EndSlowMo();
                    await transition.FadeIn(ct);
                }

                SetPhase(RunPhase.InAraf);
                RunEvents.RaiseEnteredAraf();
            }
            catch (OperationCanceledException)
            {
                if (transition != null) transition.EndSlowMo();
            }
            finally
            {
                _isTransitioning = false;
            }
        }

        private void HandleEscapePortal()
        {
            if (_isTransitioning || _state == null || _state.Phase != RunPhase.InAraf) return;
            _ = EscapeArafAsync(destroyCancellationToken);
        }

        private async Awaitable EscapeArafAsync(CancellationToken ct)
        {
            _isTransitioning = true;
            try
            {
                SetPhase(RunPhase.EscapingAraf);

                if (transition != null)
                {
                    await transition.FadeOut(Color.black, transition.EscapeMessage, ct);
                }

                if (_towerScene.IsValid() && _towerScene.isLoaded)
                {
                    SceneManager.SetActiveScene(_towerScene);
                }

                if (_arafLevel != null)
                {
                    _arafLevel.SetContentActive(false);
                }

                if (_pendingEncounter != null)
                {
                    _pendingEncounter.ResumeAfterPurgatory();
                }
                else if (player != null)
                {
                    Vector3 respawnPos = towerFallbackRespawn != null ? towerFallbackRespawn.position : _deathPosition;
                    Quaternion respawnRot = towerFallbackRespawn != null
                        ? Quaternion.Euler(0f, towerFallbackRespawn.eulerAngles.y, 0f)
                        : _deathRotation;
                    player.Revive(respawnPos, respawnRot);
                }

                _pendingEncounter = null;

                if (transition != null)
                {
                    await transition.FadeIn(ct);
                }

                SetPhase(RunPhase.Tower);
                RunEvents.RaiseEscapedAraf();
            }
            catch (OperationCanceledException)
            {
                if (transition != null) transition.EndSlowMo();
            }
            finally
            {
                _isTransitioning = false;
            }
        }

        private void TrueDeath()
        {
            _pendingEncounter = null;
            SetPhase(RunPhase.TrueDeath);

            if (transition != null)
            {
                transition.Hide();
            }

            RunEvents.RaiseTrueDeath();
        }

        private void SetPhase(RunPhase newPhase)
        {
            _state.SetPhase(newPhase);
            if (logTransitions)
            {
                Debug.Log($"<color=crimson><b>[RUN PHASE]</b></color> -> <b>{newPhase}</b> (ArafUsed: {_state.ArafChargeUsed})");
            }
            RunEvents.RaisePhaseChanged(newPhase);
        }

        [ContextMenu("Debug: Kill Player")]
        private void DebugKillPlayer()
        {
            if (!Application.isPlaying || player == null) return;
            player.TakeDamage(9999f, player.transform.position, Vector3.up);
        }

        [ContextMenu("Debug: Escape Araf")]
        private void DebugEscapeAraf()
        {
            if (!Application.isPlaying) return;
            HandleEscapePortal();
        }
    }
}
