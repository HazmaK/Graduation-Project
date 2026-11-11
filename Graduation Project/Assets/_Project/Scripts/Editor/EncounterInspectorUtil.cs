using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using RetroFPS.Combat;

namespace RetroFPS.Editor
{
    /// <summary>
    /// WaveSet ve WaveSpawnDirector Inspector'ları için ortak çizim yardımcıları (özet tablosu + Validate paneli).
    /// Yalnızca Editor assembly'sinde çalışır; oyun koduna bağımlılık eklemez.
    /// </summary>
    public static class EncounterInspectorUtil
    {
        private static readonly Color ErrorColor = new Color(1f, 0.35f, 0.30f);
        private static readonly Color WarnColor = new Color(1f, 0.80f, 0.25f);
        private static readonly Color OkColor = new Color(0.40f, 0.90f, 0.45f);

        /// <summary>
        /// Bir dalganın en erken doğuş süresi tahmini (eşzamanlılık sınırı yok sayılır).
        /// Grup akışlarını simüle eder: sıralı gruplar öncekinin bitişini bekler, paraleller kendi gecikmesiyle başlar,
        /// her parti (batch) bir aralık (interval) tüketir.
        /// </summary>
        public static float EstimateSpawnSeconds(WaveDefinition wave)
        {
            if (wave == null || wave.SpawnEntries == null) return 0f;

            float latestEnd = 0f;
            float prevEnd = 0f;
            float prevInterval = 0f;
            bool first = true;

            for (int i = 0; i < wave.SpawnEntries.Length; i++)
            {
                EnemySpawnEntry e = wave.SpawnEntries[i];
                if (e.EnemyPrefab == null) continue;

                int batches = Mathf.CeilToInt(e.Count / (float)e.BatchSize);
                float duration = Mathf.Max(0, batches - 1) * e.SpawnInterval;
                float start = (first || e.RunInParallel) ? e.DelayFromWaveStart : Mathf.Max(prevEnd + prevInterval, e.DelayFromWaveStart);
                float end = start + duration;

                prevEnd = end;
                prevInterval = e.SpawnInterval;
                latestEnd = Mathf.Max(latestEnd, end);
                first = false;
            }
            return latestEnd;
        }

        /// <summary>Dalga başına özet kartları + toplam satırı.</summary>
        public static void DrawWaveSummary(WaveDefinition[] waves)
        {
            if (waves == null || waves.Length == 0)
            {
                EditorGUILayout.HelpBox("Dalga yok.", MessageType.None);
                return;
            }

            int totalEnemies = 0;
            float totalSpawnSeconds = 0f;

            for (int i = 0; i < waves.Length; i++)
            {
                WaveDefinition w = waves[i];
                if (w == null) continue;

                w.InvalidateCache();
                w.CalculateMetrics();
                totalEnemies += w.TotalEnemies;
                totalSpawnSeconds += w.PreWaveDelay + EstimateSpawnSeconds(w);
            }

            EditorGUILayout.LabelField($"{waves.Length} dalga  |  {totalEnemies} düşman  |  en erken doğuş süresi ≈ {totalSpawnSeconds:F0} sn", EditorStyles.boldLabel);

            for (int i = 0; i < waves.Length; i++)
            {
                WaveDefinition w = waves[i];
                bool isFinal = i == waves.Length - 1;

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    if (w == null)
                    {
                        EditorGUILayout.LabelField($"{i + 1}. <boş>");
                        continue;
                    }

                    EditorGUILayout.LabelField($"{i + 1}. {w.WaveName}   [{w.DensityRating}]", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField($"{w.CompositionSummary}   (Zemin {w.GroundEnemies} / Hava {w.AirEnemies})", EditorStyles.miniLabel);

                    string transition = isFinal ? "Hepsini öldür (son dalga)" : DescribeTransition(w);
                    string concurrency = w.ThreatBudget > 0f
                        ? $"Tehdit bütçesi {w.ThreatBudget:0.#} (toplam {w.TotalThreat:0.#})"
                        : $"Eşzamanlı ≤ {w.MaxConcurrentEnemies}";
                    EditorGUILayout.LabelField($"{concurrency}  |  Geçiş: {transition}  |  Devir: {(w.CarryOverRemainingEnemies ? "evet" : "hayır")}", EditorStyles.miniLabel);
                    EditorGUILayout.LabelField($"Ön bekleme {w.PreWaveDelay:F1}s  |  Doğuş ≈ {EstimateSpawnSeconds(w):F1}s  |  Ödül: {w.RewardHealthOrbs} can, {w.RewardAmmoPickups} cephane", EditorStyles.miniLabel);

                    if (w.SpawnEntries != null)
                    {
                        for (int e = 0; e < w.SpawnEntries.Length; e++)
                        {
                            EnemySpawnEntry entry = w.SpawnEntries[e];
                            string prefabName = entry.EnemyPrefab != null ? entry.EnemyPrefab.name : "<boş>";
                            string order = e == 0 ? "ilk" : (entry.RunInParallel ? "paralel" : "sıralı");
                            EditorGUILayout.LabelField(
                                $"   • {entry.Count}x {prefabName}  [{order}, +{entry.DelayFromWaveStart:0.#}s, parti {entry.BatchSize}, her {entry.SpawnInterval:0.##}s, {entry.Pattern}{(string.IsNullOrEmpty(entry.TargetZoneTag) ? "" : ", bölge " + entry.TargetZoneTag)}]",
                                EditorStyles.miniLabel);
                        }
                    }
                }
            }
        }

        private static string DescribeTransition(WaveDefinition w)
        {
            switch (w.TransitionMode)
            {
                case WaveTransitionMode.StrictKillAll: return "Hepsini öldür";
                case WaveTransitionMode.RemainingEnemyThreshold: return $"Kalan ≤ {w.RemainingThreshold}";
                case WaveTransitionMode.PercentageKilled: return $"%{w.PercentageThreshold * 100f:F0} öldü";
                case WaveTransitionMode.TimeLimit: return $"{w.TimeLimit:F0}s sonra";
                case WaveTransitionMode.PercentageOrTimeLimit: return $"%{w.PercentageThreshold * 100f:F0} veya {w.TimeLimit:F0}s";
                default: return w.TransitionMode.ToString();
            }
        }

        /// <summary>Validate paneli: sayaç rozeti + bulgu listesi.</summary>
        public static void DrawIssues(List<ValidationIssue> issues)
        {
            int errors = EncounterValidator.CountSeverity(issues, ValidationSeverity.Error);
            int warnings = EncounterValidator.CountSeverity(issues, ValidationSeverity.Warning);

            Color prev = GUI.color;
            GUI.color = errors > 0 ? ErrorColor : (warnings > 0 ? WarnColor : OkColor);
            string badge = errors > 0 ? $"✖ {errors} hata, {warnings} uyarı"
                         : warnings > 0 ? $"⚠ {warnings} uyarı"
                         : "✔ Sorun bulunamadı";
            EditorGUILayout.LabelField(badge, EditorStyles.boldLabel);
            GUI.color = prev;

            // Önce hatalar, sonra uyarılar, sonra bilgiler
            DrawSeverity(issues, ValidationSeverity.Error, MessageType.Error);
            DrawSeverity(issues, ValidationSeverity.Warning, MessageType.Warning);
            DrawSeverity(issues, ValidationSeverity.Info, MessageType.Info);
        }

        private static void DrawSeverity(List<ValidationIssue> issues, ValidationSeverity severity, MessageType type)
        {
            for (int i = 0; i < issues.Count; i++)
            {
                if (issues[i].Severity == severity)
                {
                    EditorGUILayout.HelpBox(issues[i].Message, type);
                }
            }
        }
    }
}
