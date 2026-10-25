using UnityEngine;

namespace Ironfront.UnityPrototype
{
    public enum PrototypeDamageClass
    { InfantryBullet, MountedMachineGun, Cannon, AntiArmorRocket, AntiAirMissile, Frag }

    public interface IPrototypeArmoredVehicle : IPrototypeVehicle
    {
        int ArmorRating { get; }
        float MaximumHealth { get; }
    }

    // Armor is a hull protection rating, independent of remaining hit points.
    // This policy is shared by every projectile/blast vehicle receiver.
    public static class PrototypeVehicleArmor
    {
        public static int Rating(IPrototypeVehicle vehicle) =>
            vehicle is IPrototypeArmoredVehicle armored ? armored.ArmorRating : 6;

        public static float Resolve(IPrototypeVehicle vehicle, float rawDamage, PrototypeDamageClass weapon)
        {
            if (vehicle == null || !vehicle.Alive || float.IsNaN(rawDamage) ||
                float.IsInfinity(rawDamage) || rawDamage <= 0f) return 0f;
            int armor = Mathf.Max(0, Rating(vehicle));
            float factor;
            switch (weapon)
            {
                // An infantry rifle never chips a hull, including light aircraft
                // and motorcycles. The impact still stops at the hull collider.
                case PrototypeDamageClass.InfantryBullet: return 0f;
                case PrototypeDamageClass.MountedMachineGun:
                    if (armor >= 3) return 0f;
                    factor = armor <= 1 ? .40f : .25f; break;
                case PrototypeDamageClass.Frag:
                    if (armor >= 2) return 0f;
                    factor = .15f; break;
                case PrototypeDamageClass.Cannon:
                    factor = Mathf.Clamp(1.02f - armor * .075f, .50f, 1f); break;
                case PrototypeDamageClass.AntiArmorRocket:
                    factor = Mathf.Clamp(1.10f - armor * .05f, .75f, 1f); break;
                case PrototypeDamageClass.AntiAirMissile:
                    if (armor >= 5) return 0f;
                    factor = vehicle is IPrototypeAircraftVehicle aircraft && aircraft.IsAircraft ?
                        1f : .35f; break;
                default: return 0f;
            }
            return rawDamage * factor;
        }
    }
}
