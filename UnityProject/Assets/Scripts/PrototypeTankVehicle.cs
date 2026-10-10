using UnityEngine;

namespace Ironfront.UnityPrototype
{
    // Player-driven T90. The web game's tank speed, armor and weapon cadence
    // are kept here; the Unity prototype still uses its own hit-scan combat.
    public sealed class PrototypeTankVehicle : MonoBehaviour, IPrototypeVehicle
    {
        private const float MaxHealth = 820f;
        private const float MaxForward = 10f;
        private const float MaxReverse = 4f;
        private const float Acceleration = 5f;
        private const float TurnRate = 0.7f;
        private const float Clearance = 3.5f;
        private const float CannonDamage = 145f;
        private const float CannonInterval = 2.4f;
        private const float CannonRange = 230f;
        private const float RespawnDelay = 120f;

        private PrototypeRuntime runtime;
        private PrototypeTankVisual model;
        private Renderer[] renderers;
        private Vector2 spawn;
        private float spawnYaw;
        private float nextShot;
        private float respawnAt;

        public string VehicleName => "T90 BASTION";
        public PrototypeTeam Team { get; private set; }
        public bool Alive => Health > 0f;
        public bool IsActiveThreat => Driver != null;
        public bool DriverCanFire => true;
        public float Health { get; private set; }
        public float Speed { get; private set; }
        public Vector2 MapPosition => new Vector2(transform.position.x, transform.position.z);
        public Transform VehicleTransform => transform;
        public PrototypePlayer Driver { get; private set; }
        public PrototypePlayer Occupant => Driver;
        public Vector3 SeatPosition => transform.TransformPoint(new Vector3(-0.65f, 0.65f, -0.55f));
        public Vector3 ExitPosition
        {
            get
            {
                Vector3 side = transform.TransformPoint(new Vector3(5f, 0f, -0.6f));
                return new Vector3(side.x, PrototypeLayout.HeightAt(side.x, side.z) + 0.3f, side.z);
            }
        }

        public void Initialize(PrototypeRuntime game, PrototypeTeam team, Vector2 position, float yaw)
        {
            runtime = game;
            Team = team;
            spawn = position;
            spawnYaw = yaw;
            Health = MaxHealth;
            model = gameObject.AddComponent<PrototypeTankVisual>();
            model.Initialize(game, team, position, yaw);
            renderers = GetComponentsInChildren<Renderer>();
        }

        public void ApplyNetworkState(Vector3 position, float yaw, float health, bool alive)
        {
            if (runtime == null || !runtime.IsNetworkReplica ||
                !Finite(position.x) || !Finite(position.y) || !Finite(position.z) ||
                !Finite(yaw) || !Finite(health)) return;
            transform.position = position;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            Health = alive ? Mathf.Clamp(health, 0.01f, MaxHealth) : 0f;
            Speed = 0f;
            SetVisible(alive);
        }

        public bool TrySetDriver(PrototypePlayer player)
        {
            if (runtime == null || runtime.IsNetworkReplica || !runtime.MatchStarted ||
                runtime.Match.Winner.HasValue || !Alive || Driver != null ||
                player == null || !player.Alive || player.Team != Team ||
                player != runtime.Player) return false;
            Driver = player;
            Speed = 0f;
            return true;
        }

        public void RemoveDriver(PrototypePlayer player)
        {
            if (Driver == player) Driver = null;
            Speed = 0f;
        }

        public void TakeDamage(float amount)
        {
            if (runtime == null || runtime.IsNetworkReplica || !runtime.MatchStarted ||
                runtime.Match.Winner.HasValue || !Alive || !Finite(amount) || amount <= 0f) return;
            Health = Mathf.Max(0f, Health - amount);
            if (Alive) return;
            Speed = 0f;
            PrototypePlayer occupant = Driver;
            if (occupant != null)
                occupant.TakeDamage(1000f);
            Driver = null;
            SetVisible(false);
            respawnAt = Time.time + RespawnDelay;
            runtime.Match.RecordVehicleLoss(Team);
        }

        private void Update()
        {
            if (runtime == null || runtime.IsNetworkReplica || !runtime.MatchStarted ||
                runtime.Match.Winner.HasValue) return;
            if (!Alive)
            {
                if (Time.time >= respawnAt && SpawnClear()) Restore();
                return;
            }
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            if (Driver == null)
            {
                Speed = Mathf.MoveTowards(Speed, 0f, Acceleration * dt);
                return;
            }
            if (!Driver.Alive) { Driver.ForceExitVehicle(); return; }
            if (Cursor.lockState != CursorLockMode.Locked) return;
            Drive(Input.GetAxisRaw("Vertical"), Input.GetAxisRaw("Horizontal"), dt);
            if (Input.GetMouseButton(0)) FireCannon();
        }

        private void Drive(float throttle, float steering, float dt)
        {
            float target = Mathf.Clamp(throttle, -1f, 1f) *
                (throttle < 0f ? MaxReverse : MaxForward);
            Speed = Mathf.MoveTowards(Speed, target,
                Acceleration * (Mathf.Abs(throttle) < 0.05f ? 1.5f : 1f) * dt);
            if (Mathf.Abs(Speed) < 0.05f) Speed = 0f;
            float turn = Mathf.Clamp01(Mathf.Abs(Speed) / 3f);
            transform.Rotate(0f, steering * TurnRate * Mathf.Rad2Deg *
                turn * Mathf.Sign(Speed) * dt, 0f, Space.World);
            Vector2 start = MapPosition;
            Vector3 delta = transform.forward * (Speed * dt);
            Vector2 end = start + new Vector2(delta.x, delta.z);
            if (PrototypeLayout.Collides(end, Clearance) ||
                PrototypeLayout.LineBlocked(start, end, Clearance) ||
                OtherVehicleAt(end))
            {
                Speed = 0f;
                return;
            }
            transform.position = new Vector3(end.x,
                PrototypeLayout.HeightAt(end.x, end.y) + 0.05f, end.y);
        }

        private bool OtherVehicleAt(Vector2 point)
        {
            foreach (IPrototypeVehicle other in runtime.Vehicles)
                if (!ReferenceEquals(other, this) && other.Alive &&
                    Vector2.Distance(point, other.MapPosition) < Clearance +
                    (other is PrototypeTankVehicle ? 3.5f : 2.1f))
                    return true;
            return false;
        }

        private bool SpawnClear() =>
            !PrototypeLayout.Collides(spawn, Clearance) && !OtherVehicleAt(spawn);

        private void Restore()
        {
            transform.position = new Vector3(spawn.x,
                PrototypeLayout.HeightAt(spawn.x, spawn.y) + 0.05f, spawn.y);
            transform.rotation = Quaternion.Euler(0f, spawnYaw, 0f);
            if (model.Turret != null) model.Turret.localRotation = Quaternion.identity;
            Health = MaxHealth;
            Speed = 0f;
            nextShot = Time.time + 1f;
            SetVisible(true);
        }

        private void FireCannon()
        {
            if (Time.time < nextShot || Driver == null || Driver.ViewCamera == null) return;
            nextShot = Time.time + CannonInterval;
            Camera camera = Driver.ViewCamera;
            Vector3 origin = camera.transform.position;
            Vector3 direction = camera.transform.forward;
            Vector3 target = origin + direction * CannonRange;
            Collider chosen = null;
            float nearest = CannonRange;
            Physics.SyncTransforms();
            foreach (RaycastHit hit in Physics.RaycastAll(origin, direction,
                CannonRange, ~(1 << 2), QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(transform) || hit.distance >= nearest)
                    continue;
                nearest = hit.distance;
                chosen = hit.collider;
                target = hit.point;
            }
            Vector3 flat = target - model.Turret.position;
            flat.y = 0f;
            if (flat.sqrMagnitude > 0.01f)
                model.Turret.rotation = Quaternion.LookRotation(flat, Vector3.up);
            Vector3 muzzle = model.Turret.TransformPoint(new Vector3(0f, 0.68f, 5.55f));
            runtime.ShowTracer(muzzle, target, Team);
            if (chosen == null) return;
            PrototypeBot bot = chosen.GetComponentInParent<PrototypeBot>();
            if (bot != null && bot.Team != Team) { bot.TakeDamage(CannonDamage); return; }
            PrototypeRemotePlayer remote = chosen.GetComponentInParent<PrototypeRemotePlayer>();
            if (remote != null && remote.Team != Team)
            { remote.TakeDamage(CannonDamage); return; }
            IPrototypeVehicle vehicle = PrototypeVehicleHit.Find(chosen);
            if (vehicle != null && vehicle.Team != Team) vehicle.TakeDamage(CannonDamage);
        }

        private void SetVisible(bool visible)
        {
            if (model.HullCollider != null) model.HullCollider.enabled = visible;
            if (renderers != null)
                foreach (Renderer part in renderers) part.enabled = visible;
        }

        private static bool Finite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
