namespace RetroFPS.Combat
{
    /// <summary>
    /// Oyuncu öldüğünde Araf (Purgatory) sistemi tarafından askıya alınıp,
    /// Araf'tan kaçış sonrası oda başlangıç noktasından (checkpoint) yeniden başlatılabilen
    /// karşılaşmaların uyguladığı arayüz.
    /// </summary>
    public interface IRetryableEncounter
    {
        /// <summary>
        /// Karşılaşmanın şu anda aktif çatışmada olup olmadığını, geçerli bir checkpoint'e sahip olduğunu
        /// ve yeniden deneme (retry) özelliğinin açık olduğunu belirtir.
        /// </summary>
        bool CanResumeAfterDeath { get; }

        /// <summary>
        /// Oyuncu Araf'a düşerken çağrılır: ölümü kaydeder, sahadaki düşmanları temizler,
        /// karşılaşmayı sıfırlar ve dönüşte kaldığı odadan devam etmek üzere beklemeye alır.
        /// </summary>
        void SuspendForPurgatory();

        /// <summary>
        /// Oyuncu Araf'tan kan portalı ile kaçtığında çağrılır: oyuncuyu oda checkpoint noktasında diriltir
        /// ve karşılaşmayı 1. dalgadan yeniden başlatır.
        /// </summary>
        void ResumeAfterPurgatory();
    }
}
