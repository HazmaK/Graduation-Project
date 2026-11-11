namespace RetroFPS.Combat
{
    /// <summary>Elit düşmanların doğuş başına bir kez uygulanan güçlendirme sözleşmesi.</summary>
    public interface IEliteScalable
    {
        /// <summary>Hasarı, hareket hızını ve azami/mevcut canı çarpar; tekrarlanan çağrılar birikmez.</summary>
        void ApplyEliteScaling(float damageMultiplier, float speedMultiplier, float healthMultiplier);

        /// <summary>Bu doğuşta elit güçlendirmesi uygulanıp uygulanmadığı.</summary>
        bool IsElite { get; }
    }
}
