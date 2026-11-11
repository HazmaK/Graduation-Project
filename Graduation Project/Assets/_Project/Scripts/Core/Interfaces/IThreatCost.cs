namespace RetroFPS.Combat
{
    /// <summary>
    /// Bir düşmanın dalga zorluk bütçesindeki ağırlığı (Örn: Ghoul = 1, Flying Eye = 2, Elit = 4).
    /// WaveSpawnDirector, dalganın <c>threatBudget</c> değeri doluysa eşzamanlı sınırı adet yerine bu ağırlıkların toplamıyla uygular.
    /// Prefab üzerinde okunur (dalga başlarken / editörde); oyun döngüsünde GetComponent ile sorgulanmaz.
    /// </summary>
    public interface IThreatCost
    {
        /// <summary>Düşmanın tehdit puanı (0'dan büyük olmalı).</summary>
        float ThreatCost { get; }
    }
}
