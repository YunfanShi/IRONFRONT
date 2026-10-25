using UnityEngine;

namespace Ironfront.UnityPrototype
{
    // New acquisition stays precise; an existing visible target gets a modest
    // retention cone so passing aircraft do not steal a continuous lock.
    public sealed class PrototypeAntiAirLock
    {
        public const float RequiredSeconds = 5f;
        public const float Range = 420f;
        public const float AimHalfAngle = 6f;
        public const float RetentionHalfAngle = 8f;
        private readonly PrototypeRuntime runtime;
        private readonly PrototypePlayer player;
        private double aimedSeconds;
        private float nextTickCueAt;
        public IPrototypeVehicle Target { get; private set; }
        public float Progress => Mathf.Clamp01((float)(aimedSeconds / RequiredSeconds));
        public bool Locked => Target != null && aimedSeconds >= RequiredSeconds;
        public float CompletedAt { get; private set; } = -10f;

        public PrototypeAntiAirLock(PrototypeRuntime game, PrototypePlayer shooter)
        { runtime = game; player = shooter; }

        public void Tick(bool aiming, float dt)
        {
            if (!CanAcquire(aiming) || float.IsNaN(dt) || float.IsInfinity(dt) || dt < 0f)
            { Reset(); return; }
            IPrototypeVehicle selected = SelectTarget();
            if (selected == null) { Reset(); return; }
            if (!ReferenceEquals(selected, Target))
            { Reset(); Target = selected; nextTickCueAt = Time.time; }
            bool alreadyLocked = Locked;
            // Stalled frames cannot complete acquisition instantly; optical
            // zoom changes never affect this accumulated continuous interval.
            aimedSeconds = System.Math.Min(RequiredSeconds, aimedSeconds + Mathf.Min(dt, .1f));
            if (Target is PrototypeCombinedArmsVehicle warned) warned.WarnLock(player);
            if (!alreadyLocked && Locked)
            {
                CompletedAt = Time.time;
                PrototypePlayerAudio.PlayCue(PrototypePlayerCue.LockReady);
            }
            else if (!Locked && Time.time >= nextTickCueAt)
            {
                PrototypePlayerAudio.PlayCue(PrototypePlayerCue.LockTick);
                nextTickCueAt = Time.time + Mathf.Lerp(.6f, .16f, Progress);
            }
        }

        public bool CanFire(bool aiming)
        {
            if (!CanAcquire(aiming) || Target == null || !ReferenceEquals(Target, SelectTarget()))
            { Reset(); return false; }
            return Locked;
        }

        private bool CanAcquire(bool aiming) => runtime != null && player != null && aiming &&
            player.CanUseHeldEquipment && player.Class == PrototypeInfantryClass.Engineer &&
            player.HeldEquipment == PrototypeHeldEquipment.AntiAirLauncher &&
            player.AntiAirMissileCount > 0 && player.ViewCamera != null;

        private IPrototypeVehicle SelectTarget()
        {
            if (Eligible(Target, RetentionHalfAngle, out _, out _)) return Target;
            IPrototypeVehicle selected = null;
            float bestAngle = AimHalfAngle + .001f, bestDistance = Range + 1f;
            foreach (IPrototypeVehicle candidate in runtime.Vehicles)
            {
                if (!Eligible(candidate, AimHalfAngle, out float angle, out float distance) ||
                    angle > bestAngle || Mathf.Approximately(angle, bestAngle) && distance >= bestDistance) continue;
                selected = candidate; bestAngle = angle; bestDistance = distance;
            }
            return selected;
        }

        private bool Eligible(IPrototypeVehicle candidate, float cone, out float angle, out float distance)
        {
            angle = distance = 0f;
            if (candidate == null || !candidate.Alive || candidate.Team == player.Team ||
                candidate.VehicleTransform == null || !(candidate is IPrototypeAircraftVehicle aircraft) ||
                !aircraft.IsAircraft || aircraft.Altitude < 5f ||
                candidate is PrototypeCombinedArmsVehicle protectedAircraft && protectedAircraft.CountermeasureActive)
                return false;
            Vector3 center = candidate.VehicleTransform.position + Vector3.up * 1.7f;
            Camera camera = player.ViewCamera;
            Vector3 travel = center - camera.transform.position;
            distance = travel.magnitude; angle = Vector3.Angle(camera.transform.forward, travel);
            if (distance < 5f || distance > Range || angle > cone) return false;
            Vector3 eye = player.transform.position + Vector3.up *
                (player.IsProne ? .65f : player.IsCrouched ? 1.05f : 1.65f);
            return SightClear(camera.transform.position, center, candidate.VehicleTransform) &&
                SightClear(eye, center, candidate.VehicleTransform);
        }

        private bool SightClear(Vector3 origin, Vector3 destination, Transform target)
        {
            if (PrototypeSmokeCloud.SegmentBlocked(origin, destination)) return false;
            Vector3 delta = destination - origin;
            Physics.SyncTransforms();
            foreach (RaycastHit hit in Physics.RaycastAll(origin, delta.normalized, delta.magnitude,
                ~(1 << 2), QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(player.transform) || hit.collider.transform.IsChildOf(target) ||
                    hit.collider.GetComponentInParent<PrototypePulseVisual>() != null ||
                    hit.collider.GetComponentInParent<PrototypeVehicleCountermeasure>() != null) continue;
                return false;
            }
            return true;
        }

        public void Reset(bool notifyLost = true)
        {
            bool hadAcquisition = Target != null && aimedSeconds > 0d;
            if (Target is PrototypeCombinedArmsVehicle warned) warned.ClearLockWarning(player);
            Target = null; aimedSeconds = 0f; CompletedAt = -10f; nextTickCueAt = 0f;
            if (notifyLost && hadAcquisition) PrototypePlayerAudio.PlayCue(PrototypePlayerCue.LockLost);
        }
    }
}
