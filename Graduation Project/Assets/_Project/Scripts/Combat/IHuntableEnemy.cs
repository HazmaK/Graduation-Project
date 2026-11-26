using UnityEngine;

namespace RetroFPS.Combat
{
    /// <summary>Arena/wave hook: the enemy knows its target immediately and never drops it.</summary>
    public interface IHuntableEnemy
    {
        /// <summary>Begins persistent pursuit; a null profile uses prefab defaults and a 0.35 second grace.</summary>
        void BeginHunt(Transform target, EnemyAggressionProfile profile);

        /// <summary>Whether persistent pursuit is active; cleared by spawn, despawn, respawn or death.</summary>
        bool IsHunting { get; }
    }
}
