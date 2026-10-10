using UnityEngine;

namespace Ironfront.UnityPrototype
{
    // The shared contract for player interaction and combat across vehicle kinds.
    public interface IPrototypeVehicle
    {
        string VehicleName { get; }
        PrototypeTeam Team { get; }
        bool Alive { get; }
        bool IsActiveThreat { get; }
        bool DriverCanFire { get; }
        float Health { get; }
        float Speed { get; }
        Vector2 MapPosition { get; }
        Transform VehicleTransform { get; }
        PrototypePlayer Driver { get; }
        PrototypePlayer Occupant { get; }
        Vector3 SeatPosition { get; }
        Vector3 ExitPosition { get; }
        bool TrySetDriver(PrototypePlayer player);
        void RemoveDriver(PrototypePlayer player);
        void TakeDamage(float amount);
        bool Repair(float amount);
    }

    public static class PrototypeVehicleHit
    {
        public static IPrototypeVehicle Find(Collider collider)
        {
            if (collider == null) return null;
            foreach (MonoBehaviour component in collider.GetComponentsInParent<MonoBehaviour>())
                if (component is IPrototypeVehicle vehicle) return vehicle;
            return null;
        }
    }
}
