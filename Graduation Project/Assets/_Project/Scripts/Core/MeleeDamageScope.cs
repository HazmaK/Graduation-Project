namespace RetroFPS
{
    /// <summary>
    /// Sıfır GC hasar kaynağı bağlamı: bir <see cref="IDamageable.TakeDamage"/> çağrısının
    /// yakın dövüş silahından gelip gelmediğini, IDamageable imzasını değiştirmeden bildirir.
    /// Yakın dövüş silahı çağrıyı Begin/End arasına alır; ölen düşman IsActive'i okuyarak
    /// "infaz öldürmesi" (Doom glory kill eşdeğeri) ödüllerini belirler.
    /// Yalnızca ana thread'de kullanılır.
    /// </summary>
    public static class MeleeDamageScope
    {
        private static int _depth;

        /// <summary>Şu anda yakın dövüş kaynaklı bir hasar çağrısının içinde miyiz?</summary>
        public static bool IsActive => _depth > 0;

        public static void Begin()
        {
            _depth++;
        }

        public static void End()
        {
            if (_depth > 0) _depth--;
        }
    }
}
