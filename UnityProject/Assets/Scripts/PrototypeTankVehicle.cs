using UnityEngine;

namespace Ironfront.UnityPrototype
{
    // Player-driven T90 with a traversing, elevating, gravity-driven main gun.
    public sealed class PrototypeTankVehicle : MonoBehaviour, IPrototypeSeatedVehicle,
        IPrototypeWeaponAim
    {
        private const float MaxHealth = 820f;
        private const float MaxForward = 10f;
        private const float MaxReverse = 4f;
        private const float Acceleration = 5f;
        private const float TurnRate = 0.7f;
        private const float Clearance = 3.5f;
        private const float CannonInterval = 2.4f;
        private const float CannonRange = 230f;
        private const float CannonVelocity = 170f;
        private const float RespawnDelay = 120f;

        private PrototypeRuntime runtime;
        private PrototypeTankVisual model;
        private Renderer[] renderers;
        private Vector2 spawn;
        private float spawnYaw;
        private float nextShot;
        private float respawnAt;
        private PrototypeVehicleSeats seats;
        private Transform gunnerMount;
        private Transform gunnerBarrel;
        private Transform gunnerMuzzle;
        private float nextGunnerShot;

        public string VehicleName => "T90 BASTION";
        public PrototypeTeam Team { get; private set; }
        public bool Alive => Health > 0f;
        public bool IsActiveThreat => Occupant != null;
        public bool DriverCanFire => true;
        public float Health { get; private set; }
        public float Speed { get; private set; }
        public float CannonReadyIn => Mathf.Max(0f, nextShot - Time.time);
        public Vector2 MapPosition => new Vector2(transform.position.x, transform.position.z);
        public Transform VehicleTransform => transform;
        public PrototypePlayer Driver => seats?.Driver;
        public PrototypePlayer Occupant => seats?.Player;
        public int SeatCount => seats?.SeatCount ?? 4;
        public int PlayerSeat => seats?.PlayerSeat ?? -1;
        public PrototypeVehicleSeatRole PlayerSeatRole => seats != null ? seats.PlayerRole :
            PrototypeVehicleSeatRole.Driver;
        public bool SeatCanDrive => Alive && seats != null && seats.CanDrive;
        public bool SeatCanFire => Alive && seats != null && seats.CanFire;
        public MonoBehaviour GetSeatOccupant(int seat) => seats?.GetOccupant(seat);
        public string GetSeatName(int seat) => seats != null ? seats.GetName(seat) : "DRIVER";
        public bool PlayerControlsWeapon => SeatCanFire;
        public Transform WeaponMuzzle => PlayerSeat == 1 ? gunnerMuzzle :
            model != null ? model.CannonMuzzle : null;
        public float WeaponRange => PlayerSeat == 1 ? 110f : CannonRange;
        public Vector3 SeatPosition => GetSeatPosition(PlayerSeat);
        public Vector3 CameraPosition => GetSeatCameraPosition(PlayerSeat);
        public Vector3 GetSeatPosition(int seat) => transform.TransformPoint(
            seat == 1 ? new Vector3(-.67f, 2.1f, -.87f) :
            seat == 2 ? new Vector3(-.8f, .65f, -1.65f) :
            seat == 3 ? new Vector3(.8f, .65f, -1.65f) :
            new Vector3(-.65f, .65f, -.55f));
        public Vector3 GetSeatCameraPosition(int seat) => seat == 1 && gunnerMount != null ?
            gunnerMount.TransformPoint(new Vector3(-.30f, .20f, .60f)) :
            seat >= 2 ? transform.TransformPoint(new Vector3(seat == 2 ? -1.3f : 1.3f,
                3.35f, -2.75f)) : model.Turret.TransformPoint(new Vector3(.73f, 1.22f, .90f));
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
            seats = new PrototypeVehicleSeats(this, game, 4, true);
            BuildGunnerWeapon();
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

        public bool TrySetDriver(PrototypePlayer player) => TrySetPlayerSeat(player, 0);

        public bool TrySetPlayerSeat(PrototypePlayer player, int seat)
        {
            int previousSeat = PlayerSeat;
            if (seats == null || !seats.TrySetPlayerSeat(player, seat)) return false;
            if (previousSeat != seat) Speed = 0f;
            return true;
        }

        public void RemoveDriver(PrototypePlayer player)
        {
            if (Occupant != player) return;
            seats.RemovePlayer(player);
            Speed = 0f;
        }

        public void TakeDamage(float amount)
        {
            if (runtime == null || runtime.IsNetworkReplica || !runtime.MatchStarted ||
                runtime.Match.Winner.HasValue || !Alive || !Finite(amount) || amount <= 0f) return;
            Health = Mathf.Max(0f, Health - amount);
            if (Alive) return;
            Speed = 0f;
            PrototypePlayer occupant = Occupant;
            if (occupant != null) occupant.TakeDamage(1000f);
            seats.Clear();
            PrototypeCombatFeedback.Explosion(runtime,
                transform.position + Vector3.up * 1.4f, 5f,
                PrototypeCombatSound.TankExplosion);
            SetVisible(false);
            respawnAt = Time.time + RespawnDelay;
            runtime.Match.RecordVehicleLoss(Team);
        }

        public bool Repair(float amount)
        {
            if (runtime == null || runtime.IsNetworkReplica || !Alive ||
                !runtime.MatchStarted || amount <= 0f || Health >= MaxHealth) return false;
            Health = Mathf.Min(MaxHealth, Health + amount);
            return true;
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
            if (Occupant == null)
            {
                Speed = Mathf.MoveTowards(Speed, 0f, Acceleration * dt);
                return;
            }
            if (!Occupant.Alive) { Occupant.ForceExitVehicle(); return; }
            // Losing cursor lock (for example after switching windows) must not
            // disable W or swallow the click used to return to the game.
            if (runtime.PauseOpen || (runtime.Frontend != null && runtime.Frontend.IsOpen))
                return;
            ApplyPlayerInput(Input.GetAxisRaw("Vertical"),
                Input.GetAxisRaw("Horizontal"), Input.GetMouseButton(0),
                Cursor.lockState == CursorLockMode.Locked, dt);
        }

        private void ApplyPlayerInput(float throttle, float steering, bool fire,
            bool cursorLocked, float dt)
        {
            if (SeatCanDrive) Drive(throttle, steering, dt);
            else Speed = Mathf.MoveTowards(Speed, 0f, Acceleration * dt);
            if (!SeatCanFire) return;
            if (PlayerSeat == 1)
            {
                if (cursorLocked && Occupant.ViewCamera != null)
                    PrototypeVehicleAim.Turn(gunnerMount, gunnerBarrel,
                        PrototypeVehicleAim.CameraTarget(Occupant.ViewCamera, transform, 110f),
                        95f * dt, 70f * dt, -15f, 45f);
                if (fire) FireGunnerWeapon();
            }
            else
            {
                if (cursorLocked) AimCannon(dt);
                if (fire) FireCannon();
            }
        }

        private void Drive(float throttle, float steering, float dt)
        {
            float target = Mathf.Clamp(throttle, -1f, 1f) *
                (throttle < 0f ? MaxReverse : MaxForward);
            Speed = Mathf.MoveTowards(Speed, target,
                Acceleration * (Mathf.Abs(throttle) < 0.05f ? 1.5f : 1f) * dt);
            float turn = Mathf.Clamp01(Mathf.Abs(Speed) / 3f);
            transform.Rotate(0f, steering * TurnRate * Mathf.Rad2Deg *
                turn * Mathf.Sign(Speed) * dt, 0f, Space.World);
            Vector2 start = MapPosition;
            Vector3 delta = transform.forward * (Speed * dt);
            Vector2 end = start + new Vector2(delta.x, delta.z);
            if (PrototypeLayout.CirclePathBlocked(start, end, Clearance) ||
                OtherVehicleAt(end))
            {
                Speed = 0f;
                return;
            }
            transform.position = new Vector3(end.x,
                PrototypeLayout.HeightAt(end.x, end.y) + 0.05f, end.y);
        }

        private bool OtherVehicleAt(Vector2 point) =>
            PrototypeVehicleSpace.BlockedByAnother(runtime, this, point, Clearance);

        private bool SpawnClear() =>
            !PrototypeLayout.Collides(spawn, Clearance) && !OtherVehicleAt(spawn);

        private void Restore()
        {
            transform.position = new Vector3(spawn.x,
                PrototypeLayout.HeightAt(spawn.x, spawn.y) + 0.05f, spawn.y);
            transform.rotation = Quaternion.Euler(0f, spawnYaw, 0f);
            if (model.Turret != null) model.Turret.localRotation = Quaternion.identity;
            if (model.BarrelPivot != null) model.BarrelPivot.localRotation = Quaternion.identity;
            seats.Clear();
            Health = MaxHealth;
            Speed = 0f;
            nextGunnerShot = Time.time + 1f;
            nextShot = Time.time + 1f;
            SetVisible(true);
        }

        private void AimCannon(float dt)
        {
            if (Driver == null || Driver.ViewCamera == null) return;
            Vector3 target = PrototypeVehicleAim.CameraTarget(
                Driver.ViewCamera, transform, CannonRange);
            model.AimAt(target, dt);
        }

        private void FireCannon()
        {
            if (Time.time < nextShot || !SeatCanFire || PlayerSeat != 0 ||
                model.CannonMuzzle == null) return;
            nextShot = Time.time + CannonInterval;
            Vector3 muzzle = model.CannonMuzzle.position;
            Vector3 direction = model.CannonMuzzle.forward;
            PrototypeBallisticProjectile.LaunchShell(runtime, Team, transform,
                muzzle, direction, CannonVelocity, CannonRange);
            model.FireRecoil();
        }

        private void BuildGunnerWeapon()
        {
            gunnerMount = new GameObject("Independent commander MG").transform;
            gunnerMount.SetParent(model.Turret, false);
            gunnerMount.localPosition = new Vector3(-.67f, 1.62f, -.32f);
            gunnerBarrel = new GameObject("Commander MG elevation").transform;
            gunnerBarrel.SetParent(gunnerMount, false);
            GameObject barrel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            barrel.name = "Commander MG barrel";
            barrel.transform.SetParent(gunnerBarrel, false);
            barrel.transform.localPosition = new Vector3(0f, 0f, .68f);
            barrel.transform.localScale = new Vector3(.14f, .14f, 1.32f);
            Destroy(barrel.GetComponent<Collider>());
            barrel.GetComponent<Renderer>().sharedMaterial = runtime.GunMaterial;
            gunnerMuzzle = new GameObject("Commander MG muzzle").transform;
            gunnerMuzzle.SetParent(gunnerBarrel, false);
            gunnerMuzzle.localPosition = new Vector3(0f, 0f, 1.40f);
        }

        private void FireGunnerWeapon()
        {
            if (Time.time < nextGunnerShot || !SeatCanFire || PlayerSeat != 1 ||
                gunnerMuzzle == null) return;
            nextGunnerShot = Time.time + .12f;
            PrototypeBallisticProjectile.LaunchMountedRound(runtime, Team, transform,
                gunnerMuzzle.position, gunnerMuzzle.forward, 430f, 110f, 18f);
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
