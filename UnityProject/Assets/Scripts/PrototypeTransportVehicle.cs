using System.Collections.Generic;
using UnityEngine;

namespace Ironfront.UnityPrototype
{
    // U8 ROVER: the driver moves the vehicle but cannot fire. Seat 1 operates
    // the machine gun; seats 2 and 3 are reserved for future squad passengers.
    public sealed class PrototypeTransportVehicle : MonoBehaviour, IPrototypeVehicle
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

        private readonly List<Vector2> route = new List<Vector2>();
        private PrototypeRuntime runtime;
        private bool aiControlled;
        private PrototypePlayer occupant;
        private int playerSeat = -1;
        private int routeIndex;
        private float nextPlan;
        private float nextShot;
        private float blockedUntil;
        private Vector2 goal;
        private BoxCollider hullCollider;
        private Transform gunMount;
        private Renderer[] renderers;

        public string VehicleName => "U8 ROVER";
        public Transform VehicleTransform => transform;
        public bool DriverCanFire => false;
        public PrototypeTeam Team { get; private set; }
        public bool Alive => Health > 0f;
        public bool IsActiveThreat => aiControlled || occupant != null;
        public float Health { get; private set; }
        public float Speed { get; private set; }
        public Vector2 MapPosition => new Vector2(transform.position.x, transform.position.z);
        public PrototypePlayer Occupant => occupant;
        public PrototypePlayer Driver => playerSeat == 0 ? occupant : null;
        public PrototypePlayer Gunner => playerSeat == 1 ? occupant : null;
        public int PlayerSeat => playerSeat;
        public Vector3 SeatPosition => GetSeatPosition(playerSeat);

        public Vector3 GetSeatPosition(int seat)
        {
            Vector3 offset = seat == 1 ? new Vector3(0.65f, 0.45f, 0.1f) :
                seat == 2 ? new Vector3(-0.65f, 0.3f, -1.35f) :
                seat == 3 ? new Vector3(0.65f, 0.3f, -1.35f) :
                new Vector3(-0.65f, 0.22f, 0.65f);
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
            goal = spawn;
            transform.position = GroundPosition(spawn);
            transform.rotation = Quaternion.Euler(0f,
                team == PrototypeTeam.Blue ? 0f : 180f, 0f);
            BuildModel();
        }

        public bool TrySetDriver(PrototypePlayer player) => TrySetPlayerSeat(player, 0);
        public bool TrySetGunner(PrototypePlayer player) => TrySetPlayerSeat(player, 1);

        // A player can enter either front seat or switch between them. There is
        // deliberately no gun trigger in the driver branch of Update().
        public bool TrySetPlayerSeat(PrototypePlayer player, int seat)
        {
            if (runtime == null || !Alive || Team != PrototypeTeam.Blue || aiControlled ||
                player == null || player != runtime.Player || !player.Alive ||
                (occupant != null && occupant != player) ||
                (seat != 0 && seat != 1) || !runtime.MatchStarted ||
                runtime.Match.Winner.HasValue) return false;
            occupant = player;
            playerSeat = seat;
            Speed = 0f;
            return true;
        }

        public void RemoveDriver(PrototypePlayer player) => RemoveOccupant(player);

        public void RemoveOccupant(PrototypePlayer player)
        {
            if (occupant != player) return;
            occupant = null;
            playerSeat = -1;
            Speed = 0f;
        }

        public void TakeDamage(float amount)
        {
            if (runtime == null || !Alive || !runtime.MatchStarted ||
                runtime.Match.Winner.HasValue || amount <= 0f) return;
            Health = Mathf.Max(0f, Health - amount);
            if (Alive) return;
            Speed = 0f;
            if (occupant != null) occupant.ForceExitVehicle();
            occupant = null;
            playerSeat = -1;
            if (hullCollider != null) hullCollider.enabled = false;
            foreach (Renderer visual in renderers) visual.enabled = false;
            runtime.Match.RecordVehicleLoss(Team);
        }

        private void Update()
        {
            if (runtime == null || !runtime.MatchStarted || !Alive ||
                runtime.Match.Winner.HasValue) return;
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            if (occupant != null)
            {
                if (!occupant.Alive)
                {
                    occupant.ForceExitVehicle();
                    return;
                }
                if (Cursor.lockState != CursorLockMode.Locked) return;
                if (playerSeat == 0)
                    Drive(Input.GetAxisRaw("Vertical"),
                        Input.GetAxisRaw("Horizontal"), dt);
                else
                {
                    Speed = Mathf.MoveTowards(Speed, 0f, Acceleration * 1.5f * dt);
                    if (occupant.ViewCamera != null)
                    {
                        Vector3 aim = occupant.ViewCamera.transform.position +
                            occupant.ViewCamera.transform.forward * GunRange;
                        AimGunAt(aim);
                        if (Input.GetMouseButton(0)) FirePlayerGun();
                    }
                }
            }
            else if (aiControlled) UpdateAi(dt);
            else Speed = Mathf.MoveTowards(Speed, 0f, Acceleration * 1.5f * dt);
        }

        private void Drive(float throttle, float steering, float dt)
        {
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
            if (!ClearSegment(start, end)) { Speed = 0f; return; }
            transform.position = GroundPosition(end);
        }

        private static bool ClearSegment(Vector2 from, Vector2 to) =>
            !PrototypeLayout.Collides(to, Clearance) &&
            !PrototypeLayout.LineBlocked(from, to, Clearance);

        private static Vector3 GroundPosition(Vector2 point) =>
            new Vector3(point.x, PrototypeLayout.HeightAt(point.x, point.y) + 0.2f, point.y);

        private void FirePlayerGun()
        {
            if (Time.time < nextShot || Gunner == null || Gunner.ViewCamera == null) return;
            nextShot = Time.time + GunInterval;
            Camera camera = Gunner.ViewCamera;
            Vector3 origin = camera.transform.position;
            Vector3 direction = camera.transform.forward;
            AimGunAt(origin + direction * GunRange);
            Vector3 target = origin + direction * GunRange;
            float nearest = GunRange;
            Collider chosen = null;
            Physics.SyncTransforms();
            foreach (RaycastHit hit in Physics.RaycastAll(origin, direction, GunRange, ~(1 << 2)))
            {
                if (hit.collider == hullCollider ||
                    hit.collider.transform.IsChildOf(transform) ||
                    hit.distance >= nearest) continue;
                nearest = hit.distance;
                chosen = hit.collider;
                target = hit.point;
            }
            runtime.ShowTracer(GunMuzzle, target, Team);
            if (chosen == null) return;
            PrototypeBot bot = chosen.GetComponent<PrototypeBot>();
            if (bot != null && bot.Team != Team) { bot.TakeDamage(GunDamage); return; }
            IPrototypeVehicle vehicle = PrototypeVehicleHit.Find(chosen);
            if (vehicle != null && vehicle.Team != Team)
                vehicle.TakeDamage(GunDamage * 0.55f);
        }

        private Vector3 GunMuzzle => gunMount != null ?
            gunMount.TransformPoint(new Vector3(0f, 0.05f, 1.15f)) :
            transform.TransformPoint(new Vector3(0.55f, 2.05f, 1f));

        private void UpdateAi(float dt)
        {
            Vector2 enemy = FindVisibleEnemy(out PrototypeBot bot,
                out PrototypePlayer player, out IPrototypeVehicle vehicle);
            if ((bot != null || player != null || vehicle != null) &&
                Time.time >= nextShot)
            {
                nextShot = Time.time + GunInterval;
                float height = vehicle != null ? 1.5f :
                    player != null ? 1.65f : 1.3f;
                Vector3 impact = new Vector3(enemy.x,
                    PrototypeLayout.HeightAt(enemy.x, enemy.y) + height, enemy.y);
                AimGunAt(impact);
                runtime.ShowTracer(GunMuzzle, impact, Team);
                if (Random.value < 0.65f)
                {
                    float damage = GunDamage * 0.35f;
                    if (vehicle != null) vehicle.TakeDamage(damage * 0.55f);
                    else if (player != null) player.TakeDamage(damage);
                    else bot.TakeDamage(damage);
                }
            }

            if (Time.time >= nextPlan)
            {
                nextPlan = Time.time + 2f;
                goal = SelectObjective();
                route.Clear();
                route.AddRange(PlanVehicleRoute(MapPosition, goal));
                routeIndex = 0;
            }
            // A transport delivers its passengers near the objective. Actual
            // squad boarding and disembarkation will be wired in separately.
            if (Vector2.Distance(MapPosition, goal) < 25f ||
                (route.Count == 0 && !ClearSegment(MapPosition, goal)))
            {
                Drive(0f, 0f, dt);
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
            out PrototypePlayer selectedPlayer, out IPrototypeVehicle selectedVehicle)
        {
            selectedBot = null;
            selectedPlayer = null;
            selectedVehicle = null;
            Vector2 selected = Vector2.zero;
            float nearest = GunRange;
            if (Team == PrototypeTeam.Red && runtime.Player != null && runtime.Player.Alive)
            {
                IPrototypeVehicle playerVehicle = runtime.Player.CurrentVehicle;
                Vector2 point = playerVehicle != null && playerVehicle.Alive ?
                    playerVehicle.MapPosition : runtime.Player.MapPosition;
                float distance = Vector2.Distance(MapPosition, point);
                if (distance < nearest && !PrototypeLayout.LineBlocked(MapPosition, point, 0.1f))
                {
                    nearest = distance;
                    selected = point;
                    selectedVehicle = playerVehicle;
                    selectedPlayer = playerVehicle == null ? runtime.Player : null;
                }
            }
            foreach (PrototypeBot candidate in runtime.Bots)
            {
                if (!candidate.Alive || candidate.Team == Team) continue;
                Vector2 point = candidate.MapPosition;
                float distance = Vector2.Distance(MapPosition, point);
                if (distance >= nearest ||
                    PrototypeLayout.LineBlocked(MapPosition, point, 0.1f)) continue;
                nearest = distance;
                selected = point;
                selectedBot = candidate;
                selectedPlayer = null;
                selectedVehicle = null;
            }
            foreach (IPrototypeVehicle candidate in runtime.Vehicles)
            {
                if (ReferenceEquals(candidate, this) || !candidate.Alive || candidate.Team == Team ||
                    !candidate.IsActiveThreat) continue;
                Vector2 point = candidate.MapPosition;
                float distance = Vector2.Distance(MapPosition, point);
                if (distance >= nearest ||
                    PrototypeLayout.LineBlocked(MapPosition, point, 0.1f)) continue;
                nearest = distance;
                selected = point;
                selectedBot = null;
                selectedPlayer = null;
                selectedVehicle = candidate;
            }
            return selected;
        }

        private void AimGunAt(Vector3 point)
        {
            if (gunMount == null) return;
            Vector3 flat = point - gunMount.position;
            flat.y = 0f;
            if (flat.sqrMagnitude > 0.01f)
                gunMount.rotation = Quaternion.LookRotation(flat, Vector3.up);
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
            AddVisual("Rover chassis", PrimitiveType.Cube,
                new Vector3(0f, 0.78f, 0f), new Vector3(3.2f, 0.55f, 5f),
                body, transform);
            AddVisual("Cabin roof", PrimitiveType.Cube,
                new Vector3(0f, 1.93f, 0.7f), new Vector3(2.9f, 0.18f, 1.8f),
                body, transform);
            AddVisual("Windshield", PrimitiveType.Cube,
                new Vector3(0f, 1.45f, 1.58f), new Vector3(2.6f, 0.75f, 0.1f),
                runtime.GunMaterial, transform);
            AddVisual("Cargo bed floor", PrimitiveType.Cube,
                new Vector3(0f, 1.12f, -1.22f), new Vector3(2.85f, 0.15f, 2.15f),
                body, transform);
            foreach (int side in new[] { -1, 1 })
            {
                AddVisual("Cargo side rail", PrimitiveType.Cube,
                    new Vector3(side * 1.43f, 1.42f, -1.22f),
                    new Vector3(0.12f, 0.55f, 2.15f), body, transform);
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
                }
            }
            var mount = new GameObject("Gunner MG mount");
            gunMount = mount.transform;
            gunMount.SetParent(transform, false);
            gunMount.localPosition = new Vector3(0.55f, 2.1f, 0.1f);
            AddVisual("MG ring", PrimitiveType.Cylinder, Vector3.zero,
                new Vector3(0.45f, 0.12f, 0.45f), runtime.GunMaterial, gunMount);
            AddVisual("MG barrel", PrimitiveType.Cube,
                new Vector3(0f, 0.07f, 0.7f), new Vector3(0.16f, 0.16f, 1.4f),
                runtime.GunMaterial, gunMount);
            renderers = GetComponentsInChildren<Renderer>();
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
            if (collider != null) Destroy(collider);
            part.GetComponent<Renderer>().sharedMaterial = material;
            return part;
        }
    }
}
