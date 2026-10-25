using System.Collections.Generic;
using UnityEngine;

namespace Ironfront.UnityPrototype
{
    // U8 ROVER: four shared seats, with AI infantry eligible for rear seats 2/3.
    public sealed class PrototypeTransportVehicle : MonoBehaviour, IPrototypeSeatedVehicle,
        IPrototypeWeaponAim, IPrototypeArmoredVehicle
    {
        private const float MaxHealth = 140f;
        private const float Clearance = 1.85f;
        private const float MaxForward = 25f;
        private const float MaxReverse = 10f;
        private const float Acceleration = 15f;
        private const float TurnRate = 1.9f;
        private const float GunDamage = 18f;
        private const float GunInterval = 0.12f;
        private const float GunRange = 100f;
        private const float GunVelocity = 370f;

        private readonly List<Vector2> route = new List<Vector2>();
        private readonly PrototypeBot[] passengers = new PrototypeBot[2];
        private PrototypeRuntime runtime;
        private bool aiControlled;
        private PrototypeVehicleSeats seats;
        private PrototypePlayer occupant => seats?.Player;
        private int playerSeat => seats?.PlayerSeat ?? -1;
        private int routeIndex;
        private float nextPlan;
        private float nextShot;
        private float blockedUntil;
        private float pickupUntil;
        private Vector2 goal;
        private BoxCollider hullCollider;
        private Transform gunMount;
        private Transform barrelPivot;
        private Transform gunMuzzle;
        private Renderer[] renderers;
        private GameObject artRoot;
        private float wheelSteering;
        public bool HasAuthoredArt => artRoot != null;

        public string VehicleName => "U8 ROVER";
        public Transform VehicleTransform => transform;
        public bool DriverCanFire => false;
        public PrototypeTeam Team { get; private set; }
        public bool Alive => Health > 0f;
        public bool IsActiveThreat => aiControlled || occupant != null || PassengerCount > 0;
        public float Health { get; private set; }
        public float MaximumHealth => MaxHealth;
        public int ArmorRating => 2;
        public float Speed { get; private set; }
        public bool EngineRunning => Alive && (Driver != null || aiControlled);
        public Vector2 MapPosition => new Vector2(transform.position.x, transform.position.z);
        public PrototypePlayer Occupant => occupant;
        public PrototypePlayer Driver => seats?.Driver;
        public PrototypePlayer Gunner => playerSeat == 1 ? occupant : null;
        public bool PlayerControlsWeapon => SeatCanFire;
        public Transform WeaponMuzzle => gunMuzzle;
        public float WeaponRange => GunRange;
        public int PlayerSeat => playerSeat;
        public int SeatCount => seats?.SeatCount ?? 4;
        public PrototypeVehicleSeatRole PlayerSeatRole => seats != null ? seats.PlayerRole :
            PrototypeVehicleSeatRole.Driver;
        public bool SeatCanDrive => Alive && seats != null && seats.CanDrive;
        public bool SeatCanFire => Alive && seats != null && seats.CanFire;
        public MonoBehaviour GetSeatOccupant(int seat) => seats?.GetOccupant(seat);
        public string GetSeatName(int seat) => seats != null ? seats.GetName(seat) : "DRIVER";
        public int PassengerCount => (passengers[0] != null ? 1 : 0) +
            (passengers[1] != null ? 1 : 0);
        public bool HasFreePassengerSeat => Alive && seats != null &&
            (seats.GetOccupant(2) == null || seats.GetOccupant(3) == null);
        public Vector3 SeatPosition => GetSeatPosition(playerSeat);
        public Vector3 CameraPosition => GetSeatCameraPosition(playerSeat);
        public Vector3 GetSeatCameraPosition(int seat) => seat == 1 ?
            gunMount.TransformPoint(HasAuthoredArt ? new Vector3(-.36f, .65f, .68f) : new Vector3(-.36f, .32f, .68f)) :
            seat >= 2 ? transform.TransformPoint(new Vector3(seat == 2 ? -.9f : .9f,
                HasAuthoredArt ? 2.65f : 1.92f, HasAuthoredArt ? -2.4f : -1.35f)) : transform.TransformPoint(HasAuthoredArt ? new Vector3(-.65f, 2.85f, 1.7f) : new Vector3(-.65f, 1.78f, 1.72f));

        public Vector3 GetSeatPosition(int seat)
        {
            Vector3 offset = seat == 1 ? new Vector3(0.65f, 0.45f, 0.1f) :
                seat == 2 ? new Vector3(-0.65f, 0.3f, -1.35f) :
                seat == 3 ? new Vector3(0.65f, 0.3f, -1.35f) :
                new Vector3(-0.65f, 0.22f, 0.65f);
            if (HasAuthoredArt) offset = seat == 1 ? new Vector3(0, 1.9f, .3f) : seat >= 2 ? new Vector3(seat == 2 ? -.8f : .8f, 1.6f, -2.45f) : new Vector3(-.65f, 1.4f, 1f);
            return transform.TransformPoint(offset);
        }

        public Vector3 ExitPosition
        {
            get
            {
                Vector3[] offsets =
                {
                    new Vector3(-3.8f, 0f, -0.3f),
                    new Vector3(3.8f, 0f, -0.3f),
                    new Vector3(0f, 0f, -5.2f),
                    new Vector3(0f, 0f, 5.2f)
                };
                foreach (Vector3 offset in offsets)
                {
                    Vector3 candidate = transform.TransformPoint(offset);
                    Vector2 map = new Vector2(candidate.x, candidate.z);
                    if (!PrototypeLayout.Collides(map, 0.55f))
                        return new Vector3(map.x,
                            PrototypeLayout.HeightAt(map.x, map.y) + 0.3f, map.y);
                }
                Vector3 behind = transform.TransformPoint(new Vector3(0f, 0f, -5.2f));
                return new Vector3(behind.x,
                    PrototypeLayout.HeightAt(behind.x, behind.z) + 0.3f, behind.z);
            }
        }

        public void Initialize(PrototypeRuntime game, PrototypeTeam team,
            Vector2 spawn, bool controlledByAi)
        {
            runtime = game;
            Team = team;
            aiControlled = controlledByAi;
            Health = MaxHealth;
            runtime.Scores?.BeginLife(this);
            goal = spawn;
            transform.position = GroundPosition(spawn);
            transform.rotation = Quaternion.Euler(0f,
                team == PrototypeTeam.Blue ? 0f : 180f, 0f);
            BuildModel();
            seats = new PrototypeVehicleSeats(this, game, 4, false);
            PrototypeVehicleEngineAudio.Attach(gameObject, game, this, PrototypeEngineSound.LightDiesel, MaxForward);
        }

        public void ApplyNetworkState(Vector3 position, float yaw, float health, bool alive)
        {
            if (runtime == null || !runtime.IsNetworkReplica ||
                float.IsNaN(position.x) || float.IsNaN(position.y) || float.IsNaN(position.z) ||
                float.IsNaN(yaw) || float.IsNaN(health) ||
                float.IsInfinity(position.x) || float.IsInfinity(position.y) ||
                float.IsInfinity(position.z) || float.IsInfinity(yaw) ||
                float.IsInfinity(health)) return;
            transform.position = position;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            bool wasAlive = Alive;
            Health = alive ? Mathf.Clamp(health, 0.01f, MaxHealth) : 0f;
            if (!wasAlive && Alive) runtime.Scores?.BeginLife(this);
            Speed = 0f;
            if (hullCollider != null) hullCollider.enabled = alive;
            if (renderers != null)
                foreach (Renderer visual in renderers) visual.enabled = alive;
            if (artRoot != null) artRoot.SetActive(alive);
        }

        public bool TrySetDriver(PrototypePlayer player) => TrySetPlayerSeat(player, 0);
        public bool TrySetGunner(PrototypePlayer player) => TrySetPlayerSeat(player, 1);

        // A human in any seat suspends autonomous driving and fire. Rear AI
        // passengers retain their own occupied slots and can disembark normally.
        public bool TrySetPlayerSeat(PrototypePlayer player, int seat)
        {
            int previousSeat = PlayerSeat;
            if (seats == null || !seats.TrySetPlayerSeat(player, seat)) return false;
            if (previousSeat != seat) Speed = 0f;
            return true;
        }

        public void RemoveDriver(PrototypePlayer player) => RemoveOccupant(player);

        public void RemoveOccupant(PrototypePlayer player)
        {
            if (occupant != player) return;
            seats.RemovePlayer(player);
            Speed = 0f;
            nextPlan = 0f;
            blockedUntil = 0f;
            route.Clear();
            routeIndex = 0;
        }

        public bool TryBoardPassenger(PrototypeBot bot)
        {
            if (runtime == null || runtime.IsNetworkReplica || !runtime.MatchStarted ||
                runtime.Match.Winner.HasValue ||
                !Alive || bot == null || !bot.Alive || bot.IsPassenger || bot.Team != Team ||
                Mathf.Abs(Speed) > 6f || Vector2.Distance(bot.MapPosition, MapPosition) > 7f)
                return false;
            for (int index = 0; index < passengers.Length; index++)
            {
                if (passengers[index] != null || !seats.TryReserve(index + 2, bot)) continue;
                passengers[index] = bot;
                bot.BoardTransport(this, index + 2);
                return true;
            }
            return false;
        }

        public void RemovePassenger(PrototypeBot bot)
        {
            seats.Release(bot);
            for (int index = 0; index < passengers.Length; index++)
                if (passengers[index] == bot) passengers[index] = null;
        }

        public Vector3 FindPassengerExitPosition(int seat, Vector2 fallback)
        {
            Physics.SyncTransforms();
            Vector3 side = seat == 2 ? -transform.right : transform.right;
            Vector3[] directions = { side, -side, -transform.forward, transform.forward };
            foreach (float radius in new[] { 3.8f, 5.2f, 6.5f })
                foreach (Vector3 direction in directions)
                {
                    Vector3 point = transform.position + direction * radius;
                    Vector2 map = new Vector2(point.x, point.z);
                    if (PrototypeLayout.Collides(map, 0.55f)) continue;
                    point.y = PrototypeLayout.HeightAt(point.x, point.z) + 1f;
                    if (!Physics.CheckCapsule(point + Vector3.down * 0.5f,
                        point + Vector3.up * 0.5f, 0.45f, ~(1 << 2),
                        QueryTriggerInteraction.Ignore)) return point;
                }
            return new Vector3(fallback.x,
                PrototypeLayout.HeightAt(fallback.x, fallback.y) + 1f, fallback.y);
        }

        private void UnloadPassengers(bool atVehicleObjective)
        {
            if (Mathf.Abs(Speed) > 2f) return;
            for (int index = 0; index < passengers.Length; index++)
            {
                PrototypeBot passenger = passengers[index];
                if (passenger == null) continue;
                if (atVehicleObjective || Vector2.Distance(MapPosition,
                    passenger.ObjectivePosition) < 25f)
                    passenger.DisembarkTransport();
            }
        }

        public void TakeDamage(float amount)
        {
            if (runtime == null || runtime.IsNetworkReplica || !Alive || !runtime.MatchStarted ||
                runtime.Match.Winner.HasValue || amount <= 0f) return;
            Health = Mathf.Max(0f, Health - amount);
            if (Alive) return;
            runtime.Scores?.ResolveDefeat(this, Team);
            Speed = 0f;
            if (occupant != null) occupant.ForceExitVehicle();
            UnloadPassengers(true);
            seats.Clear();
            PrototypeCombatFeedback.Explosion(runtime,
                transform.position + Vector3.up, 4f,
                PrototypeCombatSound.TankExplosion);
            if (hullCollider != null) hullCollider.enabled = false;
            foreach (Renderer visual in renderers) visual.enabled = false;
            if (artRoot != null) artRoot.SetActive(false);
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
            if (runtime == null || runtime.IsNetworkReplica || !runtime.MatchStarted || !Alive ||
                runtime.Match.Winner.HasValue) return;
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            UnloadPassengers(false);
            if (occupant != null)
            {
                if (!occupant.Alive)
                {
                    occupant.ForceExitVehicle();
                    return;
                }
                if (runtime.ScoreboardOpen)
                {
                    if (SeatCanDrive) Drive(0f, 0f, dt);
                    return;
                }
                if (Cursor.lockState != CursorLockMode.Locked) return;
                ApplyPlayerInput(Input.GetAxisRaw("Vertical"),
                    Input.GetAxisRaw("Horizontal"), Input.GetMouseButton(0), dt);
            }
            else if (aiControlled || PassengerCount > 0) UpdateAi(dt);
            else Speed = Mathf.MoveTowards(Speed, 0f, Acceleration * 1.5f * dt);
        }

        private void ApplyPlayerInput(float throttle, float steering, bool fire, float dt)
        {
            if (runtime.ScoreboardOpen) return;
            if (SeatCanDrive) Drive(throttle, steering, dt);
            else Speed = Mathf.MoveTowards(Speed, 0f, Acceleration * 1.5f * dt);
            if (!SeatCanFire) return;
            if (occupant.ViewCamera != null)
                AimGunAt(PrototypeVehicleAim.CameraTarget(
                    occupant.ViewCamera, transform, GunRange), dt);
            if (fire) FirePlayerGun();
        }

        private void Drive(float throttle, float steering, float dt)
        {
            wheelSteering = Mathf.MoveTowards(wheelSteering, Mathf.Clamp(steering, -1f, 1f), dt * 4f);
            float desired = Mathf.Clamp(throttle, -1f, 1f) *
                (throttle < 0f ? MaxReverse : MaxForward);
            float acceleration = Mathf.Abs(throttle) < 0.05f ?
                Acceleration * 1.5f : Acceleration;
            Speed = Mathf.MoveTowards(Speed, desired, acceleration * dt);
            if (Mathf.Abs(Speed) < 0.05f) Speed = 0f;
            float steerFactor = Mathf.Clamp01(Mathf.Abs(Speed) / 4f);
            if (steerFactor > 0f)
                transform.Rotate(0f, steering * TurnRate * Mathf.Rad2Deg *
                    steerFactor * Mathf.Sign(Speed) * dt, 0f, Space.World);
            Vector2 start = MapPosition;
            Vector3 delta = transform.forward * (Speed * dt);
            Vector2 end = start + new Vector2(delta.x, delta.z);
            if (!ClearSegment(start, end) ||
                PrototypeVehicleSpace.BlockedByAnother(runtime, this, end, Clearance))
            { Speed = 0f; return; }
            transform.position = GroundPosition(end);
        }

        private static bool ClearSegment(Vector2 from, Vector2 to) =>
            !PrototypeLayout.Collides(to, Clearance) &&
            !PrototypeLayout.LineBlocked(from, to, Clearance);

        private static Vector3 GroundPosition(Vector2 point) =>
            new Vector3(point.x, PrototypeLayout.HeightAt(point.x, point.y) + 0.2f, point.y);

        private void FirePlayerGun()
        {
            if (Time.time < nextShot || !SeatCanFire || Gunner == null ||
                Gunner.ViewCamera == null) return;
            nextShot = Time.time + GunInterval;
            Vector3 muzzle = gunMuzzle.position;
            PrototypeBallisticProjectile.LaunchMountedRound(runtime, Team, transform,
                muzzle, gunMuzzle.forward, GunVelocity, GunRange, GunDamage);
        }

        private void UpdateAi(float dt)
        {
            if (pickupUntil <= 0f) pickupUntil = Time.time + 15f;
            Vector2 enemy = FindVisibleEnemy(out PrototypeBot bot,
                out PrototypePlayer player, out IPrototypeVehicle vehicle,
                out PrototypeRemotePlayer remote);
            if ((bot != null || player != null || vehicle != null || remote != null) &&
                Time.time >= nextShot)
            {
                nextShot = Time.time + GunInterval;
                float height = vehicle != null ? 1.5f :
                    player != null || remote != null ? 1.65f : 1.3f;
                Vector3 impact = new Vector3(enemy.x,
                    PrototypeLayout.HeightAt(enemy.x, enemy.y) + height, enemy.y);
                Vector3 muzzle = gunMuzzle.position;
                float flight = Vector3.Distance(muzzle, impact) / GunVelocity;
                impact.y -= Physics.gravity.y * 0.5f * flight * flight;
                if (Random.value >= 0.65f)
                    impact += Vector3.Cross(Vector3.up, impact - muzzle).normalized *
                        (Random.value < .5f ? -8f : 8f);
                AimGunAt(impact, float.PositiveInfinity);
                PrototypeBallisticProjectile.LaunchMountedRound(runtime, Team, transform,
                    gunMuzzle.position, gunMuzzle.forward, GunVelocity, GunRange,
                    GunDamage * .35f);
            }

            if (Time.time >= nextPlan)
            {
                nextPlan = Time.time + 2f;
                goal = SelectObjective();
                route.Clear();
                route.AddRange(PlanVehicleRoute(MapPosition, goal));
                routeIndex = 0;
            }
            // Wait near the infantry start for both rear passengers; a timeout
            // keeps an AI vehicle from waiting forever if one soldier is lost.
            if (PassengerCount < 2 && Time.time < pickupUntil)
            {
                Drive(0f, 0f, dt);
                return;
            }
            if (Vector2.Distance(MapPosition, goal) < 25f ||
                (route.Count == 0 && !ClearSegment(MapPosition, goal)))
            {
                Drive(0f, 0f, dt);
                UnloadPassengers(true);
                return;
            }
            while (routeIndex < route.Count &&
                Vector2.Distance(MapPosition, route[routeIndex]) < 6f) routeIndex++;
            Vector2 destination = routeIndex < route.Count ? route[routeIndex] : goal;
            Vector2 heading = destination - MapPosition;
            float desiredYaw = Mathf.Atan2(heading.x, heading.y) * Mathf.Rad2Deg;
            float angle = Mathf.DeltaAngle(transform.eulerAngles.y, desiredYaw);
            float steer = Mathf.Clamp(angle / 35f, -1f, 1f);
            float throttle = Mathf.Abs(angle) > 65f ? 0.35f : 1f;
            if (Time.time < blockedUntil) { throttle = -0.7f; steer = -steer; }
            Vector2 before = MapPosition;
            Drive(throttle, steer, dt);
            if (throttle > 0f && Vector2.Distance(before, MapPosition) < 0.01f &&
                Mathf.Abs(Speed) < 0.01f)
            {
                blockedUntil = Time.time + 1.1f;
                nextPlan = Mathf.Min(nextPlan, Time.time + 1.2f);
            }
        }

        private Vector2 SelectObjective()
        {
            Vector2 selected = MapPosition;
            float best = float.PositiveInfinity;
            foreach (PrototypeCapturePoint point in runtime.Match.Points)
            {
                if (point.Owner == Team) continue;
                Vector2 destination = point.Definition.Position;
                if (PrototypeLayout.Collides(destination, Clearance)) continue;
                float score = Vector2.Distance(MapPosition, destination);
                if (point.Owner.HasValue) score *= 0.9f;
                if (score >= best) continue;
                best = score;
                selected = destination;
            }
            return selected;
        }

        private Vector2 FindVisibleEnemy(out PrototypeBot selectedBot,
            out PrototypePlayer selectedPlayer, out IPrototypeVehicle selectedVehicle,
            out PrototypeRemotePlayer selectedRemote)
        {
            selectedBot = null;
            selectedPlayer = null;
            selectedVehicle = null;
            selectedRemote = null;
            Vector2 selected = Vector2.zero;
            float nearest = GunRange;
            if (runtime.Player != null && runtime.Player.Team != Team && runtime.Player.Alive)
            {
                IPrototypeVehicle playerVehicle = runtime.Player.CurrentVehicle;
                Vector2 point = playerVehicle != null && playerVehicle.Alive ?
                    playerVehicle.MapPosition : runtime.Player.MapPosition;
                float distance = Vector2.Distance(MapPosition, point);
                if (distance < nearest && !PrototypeLayout.SightBlocked(MapPosition, point, 0.1f))
                {
                    nearest = distance;
                    selected = point;
                    selectedVehicle = playerVehicle;
                    selectedPlayer = playerVehicle == null ? runtime.Player : null;
                }
            }
            foreach (PrototypeRemotePlayer candidate in runtime.RemotePlayers)
            {
                if (candidate == null || !candidate.Alive || candidate.Team == Team) continue;
                Vector2 point = candidate.MapPosition;
                float distance = Vector2.Distance(MapPosition, point);
                if (distance >= nearest ||
                    PrototypeLayout.SightBlocked(MapPosition, point, 0.1f)) continue;
                nearest = distance;
                selected = point;
                selectedBot = null;
                selectedPlayer = null;
                selectedVehicle = null;
                selectedRemote = candidate;
            }
            foreach (PrototypeBot candidate in runtime.Bots)
            {
                if (!candidate.Alive || candidate.IsPassenger || candidate.Team == Team) continue;
                Vector2 point = candidate.MapPosition;
                float distance = Vector2.Distance(MapPosition, point);
                if (distance >= nearest ||
                    PrototypeLayout.SightBlocked(MapPosition, point, 0.1f)) continue;
                nearest = distance;
                selected = point;
                selectedBot = candidate;
                selectedPlayer = null;
                selectedVehicle = null;
                selectedRemote = null;
            }
            foreach (IPrototypeVehicle candidate in runtime.Vehicles)
            {
                if (ReferenceEquals(candidate, this) || !candidate.Alive || candidate.Team == Team ||
                    !candidate.IsActiveThreat) continue;
                Vector2 point = candidate.MapPosition;
                float distance = Vector2.Distance(MapPosition, point);
                if (distance >= nearest ||
                    PrototypeLayout.SightBlocked(MapPosition, point, 0.1f)) continue;
                nearest = distance;
                selected = point;
                selectedBot = null;
                selectedPlayer = null;
                selectedVehicle = candidate;
                selectedRemote = null;
            }
            return selected;
        }

        private void AimGunAt(Vector3 point, float dt)
        {
            PrototypeVehicleAim.Turn(gunMount, barrelPivot, point,
                110f * dt, 80f * dt, -18f, 45f);
        }

        // Each edge and smoothed shortcut uses the same clearance as Drive().
        private static List<Vector2> PlanVehicleRoute(Vector2 start, Vector2 end)
        {
            var result = new List<Vector2>();
            if (ClearSegment(start, end)) { result.Add(end); return result; }
            const int count = 45;
            const int total = count * count;
            var points = new Vector2[total];
            var walkable = new bool[total];
            for (int index = 0; index < total; index++)
            {
                points[index] = new Vector2((index % count + 0.5f) * 16f - 360f,
                    (index / count + 0.5f) * 16f - 360f);
                walkable[index] = !PrototypeLayout.Collides(points[index], Clearance);
            }
            int first = VisibleVehicleNode(start, points, walkable);
            int last = VisibleVehicleNode(end, points, walkable);
            if (first < 0 || last < 0) return result;

            var cost = new float[total];
            var previous = new int[total];
            var closed = new bool[total];
            var queued = new bool[total];
            var open = new List<int> { first };
            for (int i = 0; i < total; i++)
            {
                cost[i] = float.PositiveInfinity;
                previous[i] = -1;
            }
            cost[first] = 0f;
            queued[first] = true;
            int scanned = 0;
            while (open.Count > 0 && scanned++ < total)
            {
                int best = 0;
                float bestScore = float.PositiveInfinity;
                for (int i = 0; i < open.Count; i++)
                {
                    int candidate = open[i];
                    float score = cost[candidate] +
                        Vector2.Distance(points[candidate], points[last]);
                    if (score >= bestScore) continue;
                    best = i;
                    bestScore = score;
                }
                int current = open[best];
                open.RemoveAt(best);
                queued[current] = false;
                if (current == last) break;
                closed[current] = true;
                int cx = current % count;
                int cz = current / count;
                for (int dz = -1; dz <= 1; dz++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dz == 0) continue;
                        int x = cx + dx, z = cz + dz;
                        if (x < 0 || z < 0 || x >= count || z >= count) continue;
                        int next = z * count + x;
                        if (!walkable[next] || closed[next] ||
                            !ClearSegment(points[current], points[next])) continue;
                        float alternate = cost[current] +
                            Vector2.Distance(points[current], points[next]);
                        if (alternate >= cost[next]) continue;
                        cost[next] = alternate;
                        previous[next] = current;
                        if (!queued[next]) { open.Add(next); queued[next] = true; }
                    }
            }
            if (first != last && previous[last] < 0) return result;
            var raw = new List<Vector2> { end };
            for (int at = last; at != first; at = previous[at]) raw.Add(points[at]);
            raw.Add(points[first]);
            raw.Reverse();
            Vector2 cursor = start;
            for (int index = 0; index < raw.Count;)
            {
                int furthest = -1;
                for (int test = raw.Count - 1; test >= index; test--)
                    if (ClearSegment(cursor, raw[test])) { furthest = test; break; }
                if (furthest < 0) return new List<Vector2>();
                cursor = raw[furthest];
                result.Add(cursor);
                index = furthest + 1;
            }
            return result;
        }

        private static int VisibleVehicleNode(Vector2 point,
            Vector2[] nodes, bool[] walkable)
        {
            int selected = -1;
            float nearest = 80f * 80f;
            for (int index = 0; index < nodes.Length; index++)
            {
                if (!walkable[index]) continue;
                float distance = (nodes[index] - point).sqrMagnitude;
                if (distance >= nearest || !ClearSegment(point, nodes[index])) continue;
                nearest = distance;
                selected = index;
            }
            return selected;
        }

        private void BuildModel()
        {
            hullCollider = gameObject.AddComponent<BoxCollider>();
            hullCollider.center = new Vector3(0f, 1f, 0f);
            hullCollider.size = new Vector3(3.4f, 1.9f, 5.2f);
            Material body = Team == PrototypeTeam.Blue ?
                runtime.BlueMaterial : runtime.RedMaterial;
            Material trim = Team == PrototypeTeam.Blue ?
                runtime.BlueTrimMaterial : runtime.RedTrimMaterial;
            AddVisual("Rover chassis", PrimitiveType.Cube,
                new Vector3(0f, 0.78f, 0f), new Vector3(3.2f, 0.55f, 5f),
                body, transform);
            AddVisual("Armored hood", PrimitiveType.Cube,
                new Vector3(0f, 1.22f, 1.83f), new Vector3(2.85f, .30f, 1.32f),
                body, transform);
            AddVisual("Cabin roof", PrimitiveType.Cube,
                new Vector3(0f, 1.93f, 0.7f), new Vector3(2.9f, 0.18f, 1.8f),
                body, transform);
            AddVisual("Roof trim", PrimitiveType.Cube,
                new Vector3(0f, 2.045f, .7f), new Vector3(2.68f, .045f, 1.68f),
                trim, transform);
            AddVisual("Windshield", PrimitiveType.Cube,
                new Vector3(0f, 1.45f, 1.58f), new Vector3(2.6f, 0.75f, 0.1f),
                runtime.GlassMaterial, transform);
            AddVisual("Front grille", PrimitiveType.Cube,
                new Vector3(0f, 1.05f, 2.52f), new Vector3(1.42f, .27f, .07f),
                runtime.GunMaterial, transform);
            AddVisual("Front bumper", PrimitiveType.Cube,
                new Vector3(0f, .56f, 2.52f), new Vector3(3.30f, .17f, .23f),
                runtime.GunMaterial, transform);
            AddVisual("Cargo bed floor", PrimitiveType.Cube,
                new Vector3(0f, 1.12f, -1.22f), new Vector3(2.85f, 0.15f, 2.15f),
                body, transform);
            foreach (int side in new[] { -1, 1 })
            {
                AddVisual("Cargo side rail", PrimitiveType.Cube,
                    new Vector3(side * 1.43f, 1.42f, -1.22f),
                    new Vector3(0.12f, 0.55f, 2.15f), body, transform);
                AddVisual("Side door armor", PrimitiveType.Cube,
                    new Vector3(side * 1.52f, 1.32f, .68f),
                    new Vector3(.09f, .78f, 1.61f), body, transform);
                AddVisual("Door window", PrimitiveType.Cube,
                    new Vector3(side * 1.57f, 1.66f, .66f),
                    new Vector3(.035f, .31f, .72f), runtime.GlassMaterial, transform);
                AddVisual("Door handle", PrimitiveType.Cube,
                    new Vector3(side * 1.59f, 1.22f, .45f),
                    new Vector3(.04f, .05f, .23f), runtime.GunMaterial, transform);
                AddVisual("Side step", PrimitiveType.Cube,
                    new Vector3(side * 1.57f, .51f, .55f),
                    new Vector3(.30f, .10f, 1.52f), runtime.GunMaterial, transform);
                AddVisual("Headlamp", PrimitiveType.Cube,
                    new Vector3(side * 1.15f, 1.12f, 2.55f),
                    new Vector3(.37f, .22f, .07f), runtime.LampMaterial, transform);
                AddVisual("Passenger bench", PrimitiveType.Cube,
                    new Vector3(side * 0.8f, 1.32f, -1.32f),
                    new Vector3(0.6f, 0.16f, 0.9f), runtime.GunMaterial, transform);
                foreach (int axle in new[] { -1, 1 })
                {
                    GameObject wheel = AddVisual("Rover wheel", PrimitiveType.Cylinder,
                        new Vector3(side * 1.55f, 0.48f, axle * 1.55f),
                        new Vector3(0.75f, 0.2f, 0.75f), runtime.GunMaterial,
                        transform);
                    wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    GameObject hub = AddVisual("Rover wheel hub", PrimitiveType.Cylinder,
                        new Vector3(side * 1.68f, .48f, axle * 1.55f),
                        new Vector3(.30f, .04f, .30f), trim, transform);
                    hub.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                }
            }
            AddVisual("Rear tailgate", PrimitiveType.Cube,
                new Vector3(0f, 1.17f, -2.47f), new Vector3(2.80f, .65f, .10f),
                body, transform);
            var mount = new GameObject("Gunner MG mount");
            gunMount = mount.transform;
            gunMount.SetParent(transform, false);
            gunMount.localPosition = new Vector3(0.55f, 2.1f, 0.1f);
            AddVisual("MG ring", PrimitiveType.Cylinder, Vector3.zero,
                new Vector3(0.45f, 0.12f, 0.45f), runtime.GunMaterial, gunMount);
            barrelPivot = new GameObject("Elevating MG").transform;
            barrelPivot.SetParent(gunMount, false);
            barrelPivot.localPosition = new Vector3(0f, .16f, .33f);
            AddVisual("MG shield", PrimitiveType.Cube,
                Vector3.zero, new Vector3(.54f, .33f, .10f),
                trim, barrelPivot);
            AddVisual("MG barrel", PrimitiveType.Cube,
                new Vector3(0f, -.09f, .37f), new Vector3(.16f, .16f, 1.4f),
                runtime.GunMaterial, barrelPivot);
            gunMuzzle = new GameObject("MG bore").transform;
            gunMuzzle.SetParent(barrelPivot, false);
            gunMuzzle.localPosition = new Vector3(0f, -.09f, 1.14f);
            renderers = GetComponentsInChildren<Renderer>();
            TryAttachAuthoredArt();
        }

        private void TryAttachAuthoredArt()
        {
            var prefab = Resources.Load<GameObject>("Vehicles/U8_" + (Team == PrototypeTeam.Blue ? "Blue" : "Red"));
            if (prefab == null) return;
            var instance = Instantiate(prefab, transform, false); instance.name = "U8 authored art";
            var rig = instance.GetComponent<PrototypeVehicleArtRig>();
            if (rig == null || !rig.BindLight(gunMount, barrelPivot, () => wheelSteering)) { Destroy(instance); return; }
            rig.CalibrateWeapon(gunMount, barrelPivot, gunMuzzle);
            hullCollider.center = rig.HullCenter; hullCollider.size = rig.HullSize;
            foreach (var renderer in renderers) renderer.enabled = false;
            artRoot = instance; renderers = instance.GetComponentsInChildren<Renderer>(true);
        }

        private static GameObject AddVisual(string label, PrimitiveType shape,
            Vector3 position, Vector3 scale, Material material, Transform parent)
        {
            GameObject part = GameObject.CreatePrimitive(shape);
            part.name = label;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            Collider collider = part.GetComponent<Collider>();
            if (collider != null) { collider.enabled = false; Destroy(collider); }
            part.GetComponent<Renderer>().sharedMaterial = material;
            return part;
        }
    }
}
