using RetroFPS.Weapons;
using UnityEngine;

namespace RetroFPS.Combat
{
    /// <summary>Shared wave and arena ammo selection, spawning and safe ground placement.</summary>
    public static class AmmoRewardUtility
    {
        /// <summary>Chooses the lowest inventory fill ratio; ties and missing inventory use randomness.</summary>
        public static AmmoType ChooseType(PlayerAmmoInventory inventory)
        {
            if (inventory != null)
            {
                float shells = inventory.GetAmmoRatio(AmmoType.Shells);
                float bolts = inventory.GetAmmoRatio(AmmoType.Bolts);
                if (shells < bolts) return AmmoType.Shells;
                if (bolts < shells) return AmmoType.Bolts;
            }
            return Random.value > 0.5f ? AmmoType.Shells : AmmoType.Bolts;
        }

        /// <summary>Uses the existing AmmoPickup prefab or the wave reward's procedural mechanism.</summary>
        public static AmmoPickup Spawn(AmmoPickup prefab, PlayerAmmoInventory inventory,
            Vector3 position, int shellAmount, int boltAmount)
        {
            AmmoType type = ChooseType(inventory);
            int amount = type == AmmoType.Shells ? shellAmount : boltAmount;
            if (prefab == null) return AmmoPickup.CreatePickupGameObject(position, type, amount);
            AmmoPickup pickup = Object.Instantiate(prefab, position, Quaternion.identity);
            pickup.Initialize(type, amount, position);
            return pickup;
        }

        /// <summary>Clips scatter against walls, snaps down onto ground, and rejects occupied landing points.</summary>
        public static bool TryGroundPosition(Vector3 origin, Vector3 candidate, float probeLift,
            float probeDistance, float clearance, RaycastHit[] hits, Collider[] overlaps, out Vector3 position)
        {
            Vector3 delta = candidate - origin;
            if (delta.sqrMagnitude > 0f && CombatRaycast.Nearest(origin, delta.normalized,
                delta.magnitude, GameLayers.GroundMask, hits, out RaycastHit wall))
                candidate = origin + delta.normalized * Mathf.Max(0f, wall.distance - clearance);
            Vector3 probe = candidate + Vector3.up * probeLift;
            if (!CombatRaycast.Nearest(probe, Vector3.down, probeDistance, GameLayers.GroundMask,
                hits, out RaycastHit ground))
            {
                position = default;
                return false;
            }
            position = ground.point + Vector3.up * clearance;
            // Half-clearance leaves a gap above the floor while keeping boxes away from walls.
            return Physics.OverlapSphereNonAlloc(position, clearance * 0.5f, overlaps,
                GameLayers.GroundMask, QueryTriggerInteraction.Ignore) == 0;
        }
    }
}
