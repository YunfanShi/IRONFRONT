using UnityEngine;

namespace Ironfront.UnityPrototype
{
    // The host owns position, damage, deaths and tickets. Guests submit only input.
    public sealed class PrototypeRemotePlayer : MonoBehaviour
    {
        private const float WalkSpeed = 8.6f;
        private const float ReviveDuration = 4f;
        private PrototypeRuntime runtime;
        private readonly PrototypeInfantryKit kit =
            new PrototypeInfantryKit(PrototypeInfantryClass.Assault);
        private CapsuleCollider hitbox;
        private PrototypeSoldierVisual visual;
        private float respawnAt;
        private float downedUntil;
        private float lastDownedClockAt;
        private bool rescueCalled;
        private float reviveStartedAt;
        private float lastRevivePulseAt;
        private PrototypeBot revivingMedic;
        private float pitch;
        private float lastInputAt;
        private bool fireHeld;

        public int Id { get; private set; }
        public PrototypeTeam Team { get; private set; }
        public bool IsConnected { get; private set; } = true;
        public float Health { get; private set; } = 100f;
        public int Ammo => kit.Ammo;
        public int Reserve => kit.Reserve;
        public bool Alive => Health > 0f;
        public float RespawnRemaining => Alive ? 0f : Mathf.Max(0f, respawnAt - Time.time);
        public bool IsDowned
        {
            get { SyncDownedTimer(); return !Alive && downedUntil > Time.time; }
        }
        public float DownedRemaining
        {
            get { SyncDownedTimer(); return Alive ? 0f : Mathf.Max(0f, downedUntil - Time.time); }
        }
        public bool IsBeingRevived => IsDowned && HasFreshValidMedic();
        public float ReviveProgress => IsBeingRevived ?
            Mathf.Clamp01((Time.time - reviveStartedAt) / ReviveDuration) : 0f;
        public bool RescueCalled => rescueCalled;
        public Vector2 MapPosition => new Vector2(transform.position.x, transform.position.z);

        public void Initialize(PrototypeRuntime game, int id, PrototypeTeam team)
        {
            runtime = game;
            Id = id;
            Team = team;
            name = "Remote Player " + id;
            hitbox = gameObject.AddComponent<CapsuleCollider>();
            hitbox.center = new Vector3(0f, 0.9f, 0f);
            hitbox.height = 1.8f;
            hitbox.radius = 0.34f;
            visual = gameObject.AddComponent<PrototypeSoldierVisual>();
            visual.Initialize(game, team, PrototypeInfantryClass.Assault, -.3f);
            Respawn();
        }

        public void SetConnected(bool connected)
        {
            if (IsConnected == connected) return;
            IsConnected = connected;
            // A held trigger must not keep firing after its owner loses the socket.
            if (!connected) fireHeld = false;
        }

        // Called by the host after validating the connection's player ID. Never accepts
        // client position, health, hit target or claimed team as an authority source.
        public void ApplyInput(float forward, float side, float yaw, float aimPitch,
            bool sprint, bool fire, float dt)
        {
            if (runtime == null || runtime.IsNetworkReplica || !IsConnected ||
                !runtime.MatchStarted ||
                runtime.Match.Winner.HasValue || !Alive) return;
            if (!IsFinite(forward) || !IsFinite(side) || !IsFinite(yaw) ||
                !IsFinite(aimPitch) || !IsFinite(dt)) return;
            dt = Mathf.Clamp(dt, 0f, 0.1f);
            transform.rotation = Quaternion.Euler(0f, Mathf.Repeat(yaw, 360f), 0f);
            pitch = Mathf.Clamp(aimPitch, -86f, 86f);
            fireHeld = fire;
            lastInputAt = Time.unscaledTime;
            Vector2 input = Vector2.ClampMagnitude(new Vector2(
                Mathf.Clamp(side, -1f, 1f), Mathf.Clamp(forward, -1f, 1f)), 1f);
            float speed = sprint && !fire && input.y > 0.1f ? 13.7f : WalkSpeed;
            Vector3 delta = (transform.right * input.x + transform.forward * input.y) *
                (speed * dt);
            Vector2 start = MapPosition;
            Vector2 end = start + new Vector2(delta.x, delta.z);
            if (!PrototypeLayout.Collides(end, 0.38f) &&
                !PrototypeLayout.LineBlocked(start, end, 0.38f))
                transform.position = new Vector3(end.x,
                    PrototypeLayout.HeightAt(end.x, end.y) + 0.3f, end.y);
        }

        public PrototypeIncomingDamage LastIncomingDamage { get; private set; }
        public void TakeDamage(float amount) => TakeDamageFrom(amount, null);

        public void TakeDamageFrom(float amount, Vector3? sourcePosition)
        {
            if (runtime == null || runtime.IsNetworkReplica || !Alive ||
                runtime.Match.Winner.HasValue || !IsFinite(amount) || amount <= 0f) return;
            float previousHealth = Health;
            Health = Mathf.Max(0f, Health - amount);
            LastIncomingDamage = PrototypeIncomingDamage.Record(LastIncomingDamage, previousHealth - Health, sourcePosition);
            if (Alive) return;
            runtime.Scores?.ResolveDefeat(this, Team);
            downedUntil = Time.time + 30f;
            ResetReviveTracking();
            rescueCalled = false;
            SetVisible(true);
            visual.SetDowned(true);
        }

        public void SetNetworkState(Vector3 position, float yaw, float health,
            bool alive, bool downed)
        {
            if (runtime == null || !runtime.IsNetworkReplica ||
                !IsFinite(position.x) || !IsFinite(position.y) || !IsFinite(position.z) ||
                !IsFinite(yaw) || !IsFinite(health)) return;
            transform.position = position;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            Health = alive ? Mathf.Clamp(health, 0.01f, 100f) : 0f;
            downedUntil = 0f;
            ResetReviveTracking();
            SetVisible(alive || downed);
            visual.SetDowned(downed);
        }

        private void Update()
        {
            if (runtime == null || runtime.IsNetworkReplica ||
                !runtime.MatchStarted || runtime.Match.Winner.HasValue) return;
            if (!Alive)
            {
                SyncDownedTimer();
                if (downedUntil > 0f && Time.time >= downedUntil) Eliminate();
                return;
            }
            if (IsConnected && fireHeld && Time.unscaledTime - lastInputAt <= 0.2f) Fire();
        }

        private bool HasValidMedic() => revivingMedic != null && revivingMedic.Alive &&
            revivingMedic.Team == Team && revivingMedic.Class == PrototypeInfantryClass.Medic &&
            Vector2.Distance(MapPosition, revivingMedic.MapPosition) <= 3.5f;

        private bool HasFreshValidMedic() => HasValidMedic() &&
            Time.time >= lastRevivePulseAt && Time.time - lastRevivePulseAt <= .25f;

        // Integrate only the interval covered by an actual recent medic pulse.
        // Properties also synchronize so component update order cannot let a
        // near-expiry casualty bleed out during a valid four-second rescue.
        private void SyncDownedTimer()
        {
            float now = Time.time;
            if (runtime != null && runtime.IsNetworkReplica)
            { lastDownedClockAt = now; return; }
            if (Alive || downedUntil <= 0f) { ResetReviveTracking(); return; }
            if (HasValidMedic() && now >= lastDownedClockAt)
            {
                float pauseFrom = Mathf.Max(lastDownedClockAt, lastRevivePulseAt);
                float pauseUntil = Mathf.Min(now, lastRevivePulseAt + .25f);
                downedUntil += Mathf.Max(0f, pauseUntil - pauseFrom);
            }
            lastDownedClockAt = now;
            if (!HasFreshValidMedic()) ResetReviveTracking();
        }

        private void ResetReviveTracking()
        {
            revivingMedic = null; reviveStartedAt = lastRevivePulseAt = 0f;
            lastDownedClockAt = Time.time;
        }

        public void RequestRescue()
        {
            if (IsConnected && IsDowned) rescueCalled = true;
        }

        public void GiveUp(float dt)
        {
            if (float.IsNaN(dt) || float.IsInfinity(dt) || dt <= 0f) return;
            SyncDownedTimer();
            if (!IsConnected) return;
            if (IsDowned) downedUntil -= Mathf.Clamp(dt, 0f, .15f) * 12f;
            if (downedUntil > 0f && Time.time >= downedUntil) Eliminate();
        }

        public void GiveUpImmediately()
        {
            if (IsConnected && IsDowned) Eliminate();
        }

        public bool TryRevive(PrototypeBot medic)
        {
            if (runtime == null || runtime.IsNetworkReplica || !IsDowned ||
                medic == null || !medic.Alive || medic.Team != Team ||
                medic.Class != PrototypeInfantryClass.Medic ||
                Vector2.Distance(MapPosition, medic.MapPosition) > 3.5f) return false;
            if (revivingMedic != medic && HasFreshValidMedic()) return false;
            if (revivingMedic != medic)
            {
                revivingMedic = medic;
                reviveStartedAt = Time.time;
            }
            lastRevivePulseAt = Time.time;
            lastDownedClockAt = Time.time;
            if (Time.time - reviveStartedAt < ReviveDuration) return false;
            downedUntil = 0f;
            ResetReviveTracking();
            rescueCalled = false;
            revivingMedic = null;
            Health = 55f;
            runtime.Scores?.BeginLife(this);
            SetVisible(true);
            visual.SetDowned(false);
            return true;
        }

        private void Eliminate()
        {
            if (respawnAt > 0f || runtime == null || runtime.IsNetworkReplica) return;
            downedUntil = 0f;
            ResetReviveTracking();
            rescueCalled = false;
            revivingMedic = null;
            runtime.Match.RecordDeath(Team);
            respawnAt = Time.time + 4.5f;
            SetVisible(false);
        }

        public bool TryDeploy(string locationId)
        {
            if (runtime == null || runtime.IsNetworkReplica || !IsConnected || Alive ||
                downedUntil > 0f ||
                !runtime.MatchStarted || runtime.Match.Winner.HasValue ||
                Time.time < respawnAt ||
                !PrototypeDeployment.TryResolve(runtime, Team, locationId, false,
                    out Vector2 point, out _)) return false;
            Respawn(point);
            return true;
        }

        private void Respawn()
        {
            Respawn(Team == PrototypeTeam.Blue ?
                PrototypeLayout.BlueBase : PrototypeLayout.RedBase);
        }

        private void Respawn(Vector2 spawn)
        {
            transform.position = new Vector3(spawn.x,
                PrototypeLayout.HeightAt(spawn.x, spawn.y) + 0.3f, spawn.y);
            transform.rotation = Quaternion.Euler(0f,
                Team == PrototypeTeam.Blue ? 48f : 228f, 0f);
            Health = 100f;
            runtime.Scores?.BeginLife(this);
            downedUntil = 0f;
            ResetReviveTracking();
            rescueCalled = false;
            revivingMedic = null;
            respawnAt = 0f;
            kit.ResetForSpawn();
            fireHeld = false;
            SetVisible(true);
            visual.SetDowned(false);
        }

        private void SetVisible(bool visible)
        {
            if (visual != null) visual.SetVisible(visible);
            if (hitbox != null) hitbox.enabled = visible && Alive;
        }

        private void Fire()
        {
            if (!kit.TryFire(Time.time)) return;
            PrototypeWeaponDefinition weapon = kit.Weapon;
            Vector3 origin = transform.position + Vector3.up * 1.55f;
            Vector3 direction = Quaternion.Euler(pitch, transform.eulerAngles.y, 0f) *
                Vector3.forward;
            PrototypeBallisticProjectile.LaunchBullet(runtime, Team, transform,
                origin, direction, weapon);
        }

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
