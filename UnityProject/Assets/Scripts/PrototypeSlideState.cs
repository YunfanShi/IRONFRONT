using UnityEngine;

namespace Ironfront.UnityPrototype
{
    // Shared by the local capsule and the authoritative remote-player simulation.
    // Pose time is independent of distance: collision can stop travel immediately
    // while the body still gathers its legs during the recovery.
    public sealed class PrototypeSlideState
    {
        public const float EnterDuration = .26f, GlideDuration = .52f, ExitDuration = .30f;
        public const float ExitStart = EnterDuration + GlideDuration;
        public const float Duration = ExitStart + ExitDuration;
        public const float MinimumSpeed = 5.2f, Cooldown = .55f;
        public float StartedAt, EndsAt, ReadyAt, InitialSpeed, RecoveryAt, FrozenPoseTime;
        public Vector3 Direction;
        public bool ForcedExit, Blocked, StandExit;
        public bool Active => EndsAt > Time.time;
        public bool Recovering => Active && (ForcedExit || Elapsed >= ExitStart);
        public float Elapsed => Mathf.Clamp(Time.time - StartedAt, 0f, Duration);
        public float Recovery => ForcedExit ? Mathf.Clamp01((Time.time - RecoveryAt) / ExitDuration) :
            Mathf.Clamp01((Elapsed - ExitStart) / ExitDuration);
        public float PoseTime => ForcedExit ? FrozenPoseTime : Elapsed;
        public float Weight => !Active ? 0f : Mathf.SmoothStep(0f, 1f, Elapsed / .09f) *
            (Recovering ? 1f - Mathf.SmoothStep(0f, 1f, StandExit && !ForcedExit ?
                Mathf.InverseLerp(.65f, 1f, Recovery) : Recovery) : 1f);
        public float Speed => !Active || Blocked ? 0f : ForcedExit ?
            Mathf.Lerp(Mathf.Lerp(InitialSpeed, 2f, FrozenPoseTime / Duration), 2f, Recovery) :
            Mathf.Lerp(InitialSpeed, 2f, Mathf.SmoothStep(0f, 1f, Elapsed / Duration));

        public bool TryStart(float speed, Vector3 direction, bool grounded, bool sprinting)
        {
            direction.y = 0f;
            if (Active || Time.time < ReadyAt || !grounded || !sprinting ||
                speed < MinimumSpeed || direction.sqrMagnitude < .01f) return false;
            StartedAt = Time.time; EndsAt = Time.time + Duration;
            InitialSpeed = Mathf.Clamp(speed, MinimumSpeed, 6.6f);
            Direction = direction.normalized;
            ForcedExit = Blocked = StandExit = false;
            RecoveryAt = FrozenPoseTime = 0f;
            return true;
        }

        public void Recover(bool blocked = false)
        {
            if (!Active) return;
            Blocked |= blocked;
            if (ForcedExit) return;
            FrozenPoseTime = Mathf.Min(Elapsed, ExitStart);
            RecoveryAt = Time.time; EndsAt = Time.time + ExitDuration;
            ForcedExit = true; StandExit = false;
        }

        public void Finish()
        {
            EndsAt = 0f; ReadyAt = Time.time + Cooldown;
        }

        public void Reset()
        {
            StartedAt = EndsAt = ReadyAt = RecoveryAt = FrozenPoseTime = 0f;
            ForcedExit = Blocked = StandExit = false;
        }

        public void ReadSnapshot(PrototypeLanActor actor)
        {
            if (!actor.alive || !actor.sliding) { EndsAt = 0f; return; }
            StartedAt = Time.time - Mathf.Clamp(actor.slideTime, 0f, Duration);
            ForcedExit = actor.slideForcedExit; StandExit = actor.slideStandExit;
            RecoveryAt = Time.time - Mathf.Clamp(actor.slideRecovery, 0f, 1f) * ExitDuration;
            FrozenPoseTime = Mathf.Clamp(actor.slidePoseTime, 0f, ExitStart);
            EndsAt = ForcedExit ? RecoveryAt + ExitDuration : StartedAt + Duration;
            Direction = Quaternion.Euler(0f, actor.slideYaw, 0f) * Vector3.forward;
            InitialSpeed = 6.6f;
        }

        public void WriteSnapshot(PrototypeLanActor actor)
        {
            actor.sliding = Active; actor.slideTime = Elapsed;
            actor.slideForcedExit = ForcedExit; actor.slideStandExit = StandExit;
            actor.slideRecovery = Recovery; actor.slidePoseTime = FrozenPoseTime;
            actor.slideYaw = Mathf.Atan2(Direction.x, Direction.z) * Mathf.Rad2Deg;
        }
    }
}
