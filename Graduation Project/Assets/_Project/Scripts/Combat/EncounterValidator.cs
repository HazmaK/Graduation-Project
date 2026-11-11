using System.Collections.Generic;
using UnityEngine;

namespace RetroFPS.Combat
{
    public enum ValidationSeverity
    {
        Info,
        Warning,
        Error
    }

    /// <summary>
    /// Tek bir doğrulama bulgusu (struct: liste doldurulurken ek nesne tahsisi yok).
    /// </summary>
    public readonly struct ValidationIssue
    {
        public readonly ValidationSeverity Severity;
        public readonly string Message;

        public ValidationIssue(ValidationSeverity severity, string message)
        {
            Severity = severity;
            Message = message;
        }
    }

    /// <summary>
    /// Dalga ve oda (WaveSpawnDirector + EncounterArea) kurulumu için saf doğrulama mantığı (Level Designer Validate paneli).
    /// Yan etkisizdir: sahneye/asset'e hiçbir şey yazmaz ve doğuş noktası üretmez. Yalnızca Editor/araç yolunda çağrılır.
    /// </summary>
    public static class EncounterValidator
    {
        /// <summary>
        /// Yalnızca dalga tanımlarını doğrular (WaveSet asset'i ve direktör ortak kullanır).
        /// </summary>
        public static void ValidateWaves(WaveDefinition[] waves, List<ValidationIssue> issues)
        {
            if (waves == null || waves.Length == 0)
            {
                issues.Add(new ValidationIssue(ValidationSeverity.Error, "Dalga listesi boş. En az 1 dalga tanımlayın (Editor'de varsayılan dalgalar otomatik üretilir, build'de ÜRETİLMEZ)."));
                return;
            }

            for (int w = 0; w < waves.Length; w++)
            {
                WaveDefinition wave = waves[w];
                string label = "Dalga " + (w + 1);

                if (wave == null)
                {
                    issues.Add(new ValidationIssue(ValidationSeverity.Error, label + ": tanım boş (null)."));
                    continue;
                }

                label += " (" + wave.WaveName + ")";
                bool isFinal = w == waves.Length - 1;
                EnemySpawnEntry[] entries = wave.SpawnEntries;

                if (entries == null || entries.Length == 0)
                {
                    issues.Add(new ValidationIssue(ValidationSeverity.Error, label + ": düşman grubu yok."));
                    continue;
                }

                for (int e = 0; e < entries.Length; e++)
                {
                    GameObject prefab = entries[e].EnemyPrefab;
                    string entryLabel = label + " / Grup " + (e + 1);

                    if (prefab == null)
                    {
                        issues.Add(new ValidationIssue(ValidationSeverity.Error, entryLabel + ": düşman prefabı atanmamış."));
                        continue;
                    }

#if UNITY_EDITOR
                    if (!UnityEditor.EditorUtility.IsPersistent(prefab))
                    {
                        issues.Add(new ValidationIssue(ValidationSeverity.Error, entryLabel + ": '" + prefab.name + "' bir sahne objesi; kalıcı Prefab asset'i olmalı."));
                    }
#endif

                    if (prefab.GetComponent<IEncounterEnemy>() == null)
                    {
                        issues.Add(new ValidationIssue(ValidationSeverity.Error, entryLabel + ": '" + prefab.name + "' IEncounterEnemy uygulamıyor (BasicEnemyAI / FlyingEyeAI)."));
                    }

                    if (wave.ThreatBudget > 0f)
                    {
                        float cost = WaveDefinition.ResolveThreatCost(entries[e]);
                        if (cost > wave.ThreatBudget)
                        {
                            issues.Add(new ValidationIssue(ValidationSeverity.Warning, entryLabel + ": tek düşmanın tehdidi (" + cost + ") dalga bütçesinden (" + wave.ThreatBudget + ") büyük; yalnızca saha boşken tek tek doğar."));
                        }
                    }

                    bool timedTransition = wave.TransitionMode == WaveTransitionMode.TimeLimit || wave.TransitionMode == WaveTransitionMode.PercentageOrTimeLimit;
                    if (!isFinal && timedTransition && wave.TimeLimit > 0f && entries[e].DelayFromWaveStart >= wave.TimeLimit)
                    {
                        issues.Add(new ValidationIssue(ValidationSeverity.Warning, entryLabel + ": gecikme (" + entries[e].DelayFromWaveStart + "s) dalga süre sınırından (" + wave.TimeLimit + "s) uzun; bu grup hiç doğmadan dalga geçebilir."));
                    }
                }

                if (!isFinal)
                {
                    switch (wave.TransitionMode)
                    {
                        case WaveTransitionMode.TimeLimit:
                            if (wave.TimeLimit <= 0f)
                            {
                                issues.Add(new ValidationIssue(ValidationSeverity.Error, label + ": TimeLimit modunda süre 0; dalga asla ilerlemez."));
                            }
                            break;

                        case WaveTransitionMode.RemainingEnemyThreshold:
                            if (wave.RemainingThreshold >= wave.TotalEnemies)
                            {
                                issues.Add(new ValidationIssue(ValidationSeverity.Warning, label + ": kalan düşman eşiği (" + wave.RemainingThreshold + ") toplamdan (" + wave.TotalEnemies + ") büyük/eşit; kuyruk biter bitmez sonraki dalga başlar."));
                            }
                            break;
                    }
                }
                else if (wave.TransitionMode != WaveTransitionMode.StrictKillAll)
                {
                    issues.Add(new ValidationIssue(ValidationSeverity.Info, label + ": son dalga seçilen geçiş modundan bağımsız olarak her zaman 'hepsini öldür' ile biter."));
                }
            }
        }

        /// <summary>
        /// Bir odayı (direktör + alan + doğuş noktaları + dalgalar) bütün olarak doğrular.
        /// </summary>
        public static void ValidateRoom(WaveSpawnDirector director, List<ValidationIssue> issues)
        {
            if (director == null) return;

            WaveDefinition[] waves = director.EffectiveWaves;
            ValidateWaves(waves, issues);

            // --- Oda bağlantıları ---
            EncounterArea area = director.EncounterArea;
            if (area == null) area = director.GetComponent<EncounterArea>();
            if (area == null) area = director.GetComponentInParent<EncounterArea>();
            if (area == null) area = director.GetComponentInChildren<EncounterArea>(true);

            bool hasTriggerVolume = director.GetComponentInChildren<EncounterTriggerVolume>(true) != null
                                    || director.GetComponentInParent<EncounterTriggerVolume>() != null;

            if (area == null && !hasTriggerVolume && !director.AutoStartOnPlay)
            {
                issues.Add(new ValidationIssue(ValidationSeverity.Warning, "Bu odayı başlatacak hiçbir şey yok (EncounterArea, EncounterTriggerVolume veya Auto Start On Play)."));
            }

            if (area != null && area.WaveDirector != null && area.WaveDirector != director)
            {
                issues.Add(new ValidationIssue(ValidationSeverity.Error, "EncounterArea başka bir odanın direktörüne ('" + area.WaveDirector.name + "') bağlı! Dalgalar odalar arasında karışır."));
            }

            // --- Doğuş noktaları ---
            EncounterSpawnPoint[] points = director.PeekRoomSpawnPoints();
            int groundPoints = 0;
            int airPoints = 0;
            int validPoints = 0;

            for (int i = 0; i < points.Length; i++)
            {
                EncounterSpawnPoint sp = points[i];
                if (sp == null)
                {
                    issues.Add(new ValidationIssue(ValidationSeverity.Warning, "Doğuş noktası listesinde boş (null) eleman var."));
                    continue;
                }

                validPoints++;
                if (sp.AllowGroundEnemies) groundPoints++;
                if (sp.AllowAirEnemies) airPoints++;

                bool inRoomHierarchy = sp.transform.IsChildOf(director.transform)
                                       || (area != null && sp.transform.IsChildOf(area.transform));
                if (!inRoomHierarchy)
                {
                    bool insideBounds = area != null && area.WorldBounds.Contains(sp.Position);
                    issues.Add(new ValidationIssue(ValidationSeverity.Warning,
                        "'" + sp.name + "' bu odanın hiyerarşisi dışında" + (insideBounds ? " (ama alan sınırları içinde)." : " ve alan sınırları dışında; başka odaya ait olabilir.")));
                }
            }

            if (validPoints == 0)
            {
                issues.Add(new ValidationIssue(ValidationSeverity.Error, "Doğuş noktası yok. Odaya EncounterSpawnPoint ekleyin veya EncounterArea'da prosedürel üretimi çalıştırın."));
            }
            else
            {
                if (validPoints < 3)
                {
                    issues.Add(new ValidationIssue(ValidationSeverity.Warning, "Yalnızca " + validPoints + " doğuş noktası var; cooldown yüzünden dalgalar yavaşlayabilir (öneri: en az 4)."));
                }

                // Dalga başına grup uyumluluğu (hava/zemin ve bölge etiketi)
                if (waves != null)
                {
                    bool warnedGround = false, warnedAir = false;
                    for (int w = 0; w < waves.Length; w++)
                    {
                        EnemySpawnEntry[] entries = waves[w]?.SpawnEntries;
                        if (entries == null) continue;

                        for (int e = 0; e < entries.Length; e++)
                        {
                            EnemySpawnEntry entry = entries[e];
                            if (entry.IsAirEnemy && airPoints == 0 && !warnedAir)
                            {
                                warnedAir = true;
                                issues.Add(new ValidationIssue(ValidationSeverity.Error, "Hava düşmanı var ama 'Allow Air Enemies' açık doğuş noktası yok; bu düşmanlar doğmaz."));
                            }
                            else if (!entry.IsAirEnemy && groundPoints == 0 && !warnedGround)
                            {
                                warnedGround = true;
                                issues.Add(new ValidationIssue(ValidationSeverity.Error, "Zemin düşmanı var ama 'Allow Ground Enemies' açık doğuş noktası yok; bu düşmanlar doğmaz."));
                            }

                            int compatible = entry.IsAirEnemy ? airPoints : groundPoints;
                            if (compatible > 0 && entry.BatchSize > compatible)
                            {
                                issues.Add(new ValidationIssue(ValidationSeverity.Warning,
                                    "Dalga " + (w + 1) + " / Grup " + (e + 1) + ": parti boyutu (" + entry.BatchSize + ") uyumlu nokta sayısından (" + compatible + ") büyük; parti birkaç kareye bölünür veya aynı noktada üst üste doğar."));
                            }

                            if (!string.IsNullOrEmpty(entry.TargetZoneTag) && !HasZoneTag(points, entry.TargetZoneTag))
                            {
                                issues.Add(new ValidationIssue(ValidationSeverity.Warning,
                                    "Dalga " + (w + 1) + " / Grup " + (e + 1) + ": '" + entry.TargetZoneTag + "' bölge etiketli nokta yok; genel noktalara düşülür (yazım hatası olabilir)."));
                            }

                            if (director.AbsoluteMaxAlive > 0 && waves[w].MaxConcurrentEnemies > director.AbsoluteMaxAlive)
                            {
                                issues.Add(new ValidationIssue(ValidationSeverity.Warning,
                                    "Dalga " + (w + 1) + ": eşzamanlı sınır (" + waves[w].MaxConcurrentEnemies + ") 'Absolute Max Alive' (" + director.AbsoluteMaxAlive + ") değerini aşıyor; sınır fiilen küçülür."));
                                break;
                            }
                        }
                    }
                }
            }

            // --- Mesafe ayarları ---
            if (director.MinSpawnDistance > director.MaxSpawnDistance)
            {
                issues.Add(new ValidationIssue(ValidationSeverity.Error, "Min Spawn Distance, Max Spawn Distance değerinden büyük; hiçbir nokta uygun sayılmaz."));
            }

            // --- Bariyerler ---
            GameObject[] barriers = director.ArenaBarriers;
            if (barriers == null || barriers.Length == 0)
            {
                issues.Add(new ValidationIssue(ValidationSeverity.Info, "Arena bariyeri atanmamış; oyuncu savaş sırasında odadan çıkabilir."));
            }
            else
            {
                for (int i = 0; i < barriers.Length; i++)
                {
                    if (barriers[i] == null)
                    {
                        issues.Add(new ValidationIssue(ValidationSeverity.Warning, "Bariyer listesinde boş (null) eleman var."));
                        break;
                    }
                }
            }

            // --- Dalga olayları ---
            WaveEventBinding[] events = director.WaveEvents;
            int waveCount = waves != null ? waves.Length : 0;
            if (events != null)
            {
                for (int i = 0; i < events.Length; i++)
                {
                    if (events[i] != null && events[i].WaveNumber > waveCount)
                    {
                        issues.Add(new ValidationIssue(ValidationSeverity.Warning, "Dalga olayı #" + (i + 1) + " " + events[i].WaveNumber + ". dalgaya bağlı ama odada " + waveCount + " dalga var; hiç tetiklenmez."));
                    }
                }
            }

            // --- Diğer odalarla çakışma (oda izolasyonu) ---
            ValidateAgainstOtherRooms(director, area, points, issues);
        }

        private static void ValidateAgainstOtherRooms(WaveSpawnDirector director, EncounterArea area, EncounterSpawnPoint[] points, List<ValidationIssue> issues)
        {
            WaveSpawnDirector[] others = Object.FindObjectsByType<WaveSpawnDirector>(FindObjectsInactive.Include);
            for (int i = 0; i < others.Length; i++)
            {
                WaveSpawnDirector other = others[i];
                if (other == null || other == director) continue;

                // Aynı doğuş noktasını iki oda paylaşıyor mu?
                EncounterSpawnPoint[] otherPoints = other.PeekRoomSpawnPoints();
                int shared = 0;
                for (int a = 0; a < points.Length; a++)
                {
                    if (points[a] == null) continue;
                    for (int b = 0; b < otherPoints.Length; b++)
                    {
                        if (points[a] == otherPoints[b]) { shared++; break; }
                    }
                }

                if (shared > 0)
                {
                    issues.Add(new ValidationIssue(ValidationSeverity.Error, "'" + other.name + "' odasıyla " + shared + " doğuş noktasını paylaşıyor; düşmanlar yanlış odada doğabilir."));
                }

                // Alan sınırları çakışıyor mu? (muhafız algılama ve başlatma karışır)
                EncounterArea otherArea = other.EncounterArea != null ? other.EncounterArea : other.GetComponent<EncounterArea>();
                if (area != null && otherArea != null && otherArea != area && area.WorldBounds.Intersects(otherArea.WorldBounds))
                {
                    issues.Add(new ValidationIssue(ValidationSeverity.Warning, "Alan sınırları '" + other.name + "' ile çakışıyor; oyuncu iki odayı birden tetikleyebilir."));
                }
            }
        }

        private static bool HasZoneTag(EncounterSpawnPoint[] points, string tag)
        {
            for (int i = 0; i < points.Length; i++)
            {
                if (points[i] != null && string.Equals(points[i].ZoneTag, tag, System.StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        public static int CountSeverity(List<ValidationIssue> issues, ValidationSeverity severity)
        {
            int n = 0;
            for (int i = 0; i < issues.Count; i++)
            {
                if (issues[i].Severity == severity) n++;
            }
            return n;
        }
    }
}
