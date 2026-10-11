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

    // Exposes the real bore axis to the HUD. A driver's camera is not necessarily
    // looking where the slower turret has reached yet.
    public interface IPrototypeWeaponAim
    {
        bool PlayerControlsWeapon { get; }
        Transform WeaponMuzzle { get; }
        float WeaponRange { get; }
    }

    public static class PrototypeVehicleAim
    {
        public static Vector3 CameraTarget(Camera camera, Transform vehicle, float range)
        {
            Vector3 origin = camera.transform.position;
            Vector3 direction = camera.transform.forward;
            Vector3 target = origin + direction * range;
            float nearest = range;
            Physics.SyncTransforms();
            foreach (RaycastHit hit in Physics.RaycastAll(origin, direction, range,
                ~(1 << 2), QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(vehicle) || hit.distance >= nearest)
                    continue;
                nearest = hit.distance;
                target = hit.point;
            }
            return target;
        }

        public static Vector3 BorePoint(Transform muzzle, Transform vehicle, float range)
        {
            Vector3 origin = muzzle.position;
            Vector3 direction = muzzle.forward;
            Vector3 point = origin + direction * range;
            float nearest = range;
            Physics.SyncTransforms();
            foreach (RaycastHit hit in Physics.RaycastAll(origin, direction, range,
                ~(1 << 2), QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(vehicle) || hit.distance >= nearest)
                    continue;
                nearest = hit.distance;
                point = hit.point;
            }
            return point;
        }

        // yaw and pitch have independent traverse rates. Positive elevation is
        // negative local X in Unity's right-handed forward-facing gun model.
        public static void Turn(Transform turret, Transform barrel, Vector3 target,
            float yawStep, float pitchStep, float minElevation, float maxElevation)
        {
            if (turret == null || barrel == null) return;
            Vector3 flat = target - turret.position;
            flat.y = 0f;
            if (flat.sqrMagnitude > 0.01f)
                turret.rotation = Quaternion.RotateTowards(turret.rotation,
                    Quaternion.LookRotation(flat, Vector3.up), yawStep);
            Vector3 local = turret.InverseTransformPoint(target) - barrel.localPosition;
            float horizontal = new Vector2(local.x, local.z).magnitude;
            float elevation = Mathf.Clamp(Mathf.Atan2(local.y,
                Mathf.Max(0.1f, horizontal)) * Mathf.Rad2Deg,
                minElevation, maxElevation);
            float pitch = Mathf.MoveTowardsAngle(
                Mathf.DeltaAngle(0f, barrel.localEulerAngles.x), -elevation,
                pitchStep);
            barrel.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }
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
