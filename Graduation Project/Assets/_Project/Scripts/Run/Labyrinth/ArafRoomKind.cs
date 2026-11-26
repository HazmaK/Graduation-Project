namespace RetroFPS.Run.Labyrinth
{
    /// <summary>Araf labirentindeki oda arketiplerini tanımlar.</summary>
    public enum ArafRoomKind
    {
        /// <summary>Eski çatışma odası; kullanılmıyor (Faz 4).</summary>
        Combat = 0,
        /// <summary>Eski pusu odası; kullanılmıyor (Faz 4).</summary>
        Ambush = 1,
        /// <summary>Tapınak odası; oyuncuya bir kez can kutsaması verir.</summary>
        Shrine = 2,
        /// <summary>Kemik mahzeni; ganimet veya atmosferik iç mekan odası.</summary>
        Ossuary = 3,
        /// <summary>Mühür odası; muhafızları öldüğünde mührü alınan ve kart sunağı çıkaran oda.</summary>
        Seal = 4,
        /// <summary>İsteğe bağlı dalgalı arena odası.</summary>
        Arena = 5
    }
}
