namespace RetroFPS.Run.Labyrinth
{
    /// <summary>Araf labirentindeki oda arketiplerini tanımlar.</summary>
    public enum ArafRoomKind
    {
        /// <summary>Standart çatışma odası; kapılar temizlenene kadar mühürlenir.</summary>
        Combat = 0,
        /// <summary>Pusu odası; oyuncu iç tetikleyiciye girene kadar düşmanlar gizli kalır.</summary>
        Ambush = 1,
        /// <summary>Tapınak odası; oyuncuya bir kez can kutsaması verir.</summary>
        Shrine = 2,
        /// <summary>Kemik mahzeni; ganimet veya atmosferik iç mekan odası.</summary>
        Ossuary = 3,
        /// <summary>Çıkış odası; muhafızları öldüğünde açılan kaçış portalını barındırır.</summary>
        Exit = 4
    }
}
