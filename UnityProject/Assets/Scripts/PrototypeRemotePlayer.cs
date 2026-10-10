using UnityEngine;

namespace Ironfront.UnityPrototype
{
    // The host owns position, damage, deaths and tickets. Guests submit only input.
    public sealed class PrototypeRemotePlayer : MonoBehaviour
    {
        private const float WalkSpeed = 8.6f;
        private PrototypeRuntime runtime;
        private readonly PrototypeInfantryKit kit =
            new PrototypeInfantryKit(PrototypeInfantryClass.Assault);
        private CapsuleCollider hitbox;
        private PrototypeSoldierVisual visual;
        private float respawnAt;
        private float downedUntil;
        private bool rescueCalled;
        private float pitch;
        private float lastInputAt;
        private bool fireHeld;

        public int Id { get; private set; }
        public PrototypeTeam Team { get; private set; }
        public float Health { get; private set; } = 100f;
        public int Ammo => kit.Ammo;
        public int Reserve => kit.Reserve;
        public bool Alive => Health > 0f;
        public float RespawnRemaining => Alive ? 0f : Mathf.Max(0f, respawnAt - Time.time);
        public bool IsDowned => !Alive && downedUntil > Time.time;
        public float DownedRemaining => IsDowned ? downedUntil - Time.time : 0f;
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

        // Called by the host after validating the connection's player ID. Never accepts
        // client position, health, hit target or claimed team as an authority source.
        public void ApplyInput(float forward, float side, float yaw, float aimPitch,
            bool sprint, bool fire, float dt)
        {
            if (runtime == null || runtime.IsNetworkReplica || !runtime.MatchStarted ||
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
            float speed = sprint && input.y > 0.1f ? 13.7f : WalkSpeed;
            Vector3 delta = (transform.right * input.x + transform.forward * input.y) *
                (speed * dt);
            Vector2 start = MapPosition;
            Vector2 end = start + new Vector2(delta.x, delta.z);
            if (!PrototypeLayout.Collides(end, 0.38f) &&
                !PrototypeLayout.LineBlocked(start, end, 0.38f))
                transform.position = new Vector3(end.x,
                    PrototypeLayout.HeightAt(end.x, end.y) + 0.3f, end.y);
        }

        public void TakeDamage(float amount)
        {
            if (runtime == null || runtime.IsNetworkReplica || !Alive ||
                runtime.Match.Winner.HasValue || !IsFinite(amount) || amount <= 0f) return;
            Health = Mathf.Max(0f, Health - amount);
            if (Alive) return;
            SetVisible(false);
            downedUntil = Time.time + 30f;
            rescueCalled = false;
        }

        public void SetNetworkState(Vector3 position, float yaw, float health, bool alive)
        {
            if (runtime == null || !runtime.IsNetworkReplica ||
                !IsFinite(position.x) || !IsFinite(position.y) || !IsFinite(position.z) ||
                !IsFinite(yaw) || !IsFinite(health)) return;
            transform.position = position;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            Health = alive ? Mathf.Clamp(health, 0.01f, 100f) : 0f;
            SetVisible(alive);
        }

        private void Update()
        {
            if (runtime == null || runtime.IsNetworkReplica ||
                !runtime.MatchStarted || runtime.Match.Winner.HasValue) return;
            if (!Alive)
            {
                if (downedUntil > 0f && Time.time >= downedUntil) Eliminate();
                return;
            }
            if (fireHeld && Time.unscaledTime - lastInputAt <= 0.2f) Fire();
        }

        public void RequestRescue()
        {
            if (IsDowned) rescueCalled = true;
        }

        public void GiveUp(float dt)
        {
            if (IsDowned) downedUntil -= Mathf.Clamp(dt, 0f, .15f) * 12f;
            if (downedUntil > 0f && Time.time >= downedUntil) Eliminate();
        }

        public void GiveUpImmediately()
        {
            if (IsDowned) Eliminate();
        }

        public bool TryRevive(PrototypeBot medic)
        {
            if (runtime == null || runtime.IsNetworkReplica || !IsDowned ||
                medic == null || !medic.Alive || medic.Team != Team ||
                medic.Class != PrototypeInfantryClass.Medic ||
                Vector2.Distance(MapPosition, medic.MapPosition) > 3.5f) return false;
            downedUntil = 0f;
            rescueCalled = false;
            Health = 55f;
            SetVisible(true);
            return true;
        }

        private void Eliminate()
        {
            if (respawnAt > 0f || runtime == null || runtime.IsNetworkReplica) return;
            downedUntil = 0f;
            rescueCalled = false;
            runtime.Match.RecordDeath(Team);
            respawnAt = Time.time + 4.5f;
        }

        public bool TryDeploy(string locationId)
        {
            if (runtime == null || runtime.IsNetworkReplica || Alive ||
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
            downedUntil = 0f;
            rescueCalled = false;
            respawnAt = 0f;
            kit.ResetForSpawn();
            fireHeld = false;
            SetVisible(true);
        }

        private void SetVisible(bool visible)
        {
            if (visual != null) visual.SetVisible(visible);
            if (hitbox != null) hitbox.enabled = visible;
        }

        private void Fire()
        {
            if (!kit.TryFire(Time.time)) return;
            PrototypeWeaponDefinition weapon = kit.Weapon;
            Vector3 origin = transform.position + Vector3.up * 1.55f;
            Vector3 direction = Quaternion.Euler(pitch, transform.eulerAngles.y, 0f) *
                Vector3.forward;
            float closest = weapon.MaxRange;
            Collider target = null;
            Vector3 end = origin + direction * weapon.MaxRange;
            Physics.SyncTransforms();
            foreach (RaycastHit hit in Physics.RaycastAll(origin, direction, weapon.MaxRange))
            {
                if (hit.collider == hitbox || hit.collider.transform.IsChildOf(transform) ||
                    hit.distance >= closest) continue;
                closest = hit.distance;
                target = hit.collider;
                end = hit.point;
            }
            // The local first-person player's collider uses Ignore Raycast.
            PrototypePlayer local = runtime.Player;
            bool hitLocal = false;
            if (local != null && local.Alive && local.Team != Team &&
                local.CurrentVehicle == null)
            {
                Vector3 center = local.transform.position + Vector3.up * 1.1f;
                float distance = Vector3.Dot(center - origin, direction);
                if (distance > 0f && distance < closest &&
                    (center - (origin + direction * distance)).sqrMagnitude < 0.55f * 0.55f)
                {
                    closest = distance;
                    end = origin + direction * distance;
                    hitLocal = true;
                }
            }
            runtime.ShowTracer(origin, end, Team);
            float damage = weapon.DamageAtRange(closest);
            if (hitLocal) { local.TakeDamage(damage); return; }
            if (target == null) return;
            PrototypeBot bot = target.GetComponent<PrototypeBot>();
            if (bot != null && bot.Team != Team) { bot.TakeDamage(damage); return; }
            PrototypeRemotePlayer other = target.GetComponentInParent<PrototypeRemotePlayer>();
            if (other != null && other != this && other.Team != Team)
            { other.TakeDamage(damage); return; }
            IPrototypeVehicle vehicle = PrototypeVehicleHit.Find(target);
            if (vehicle != null && vehicle.Team != Team)
                vehicle.TakeDamage(damage * 0.55f);
        }

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
