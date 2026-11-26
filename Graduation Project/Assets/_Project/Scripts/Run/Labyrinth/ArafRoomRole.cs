namespace RetroFPS.Run.Labyrinth
{
    /// <summary>Oda yuvasının üretim sırasında belirlenen karşılaşma rolü.</summary>
    public enum ArafRoomRole
    {
        /// <summary>Sakin tapınak veya kemik mahzeni.</summary>
        Calm = 0,
        /// <summary>Kart sunağı kazandıran isteğe bağlı arena.</summary>
        OptionalArena = 1,
        /// <summary>Temizlendiğinde mühür kazandıran arena.</summary>
        SealArena = 2
    }
}
