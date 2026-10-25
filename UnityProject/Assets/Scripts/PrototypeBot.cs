using System.Collections.Generic;
using UnityEngine;

namespace Ironfront.UnityPrototype
{
    public enum PrototypeTacticalState { Advance, Hold, Engage, Search, SeekCover, InCover, Down, Revive }

    public sealed class PrototypeBot : MonoBehaviour
    {
        private const float SightHalfAngle = 70f;
        private const float FireHalfAngle = 12f;
        private const float CombatTurnRate = 210f;
        // These rockets are the engineer's existing anti-armor equipment, not
        // the armor/repair gadget charges. Other classes do not receive any.
        private const int EngineerRocketCapacity = 2;
        private const float RocketCooldown = 10f;
        private const float LauncherRaiseSeconds = .65f;
        private const float RocketSafetyDistance = 10f;
        private const float RocketVelocity = 110f;
        private const float RocketFireHalfAngle = 1f;
        private const float LauncherPitchLimit = 55f;
        private PrototypeRuntime runtime;
        private PrototypeNavigation navigation;
        private PrototypeCommander commander;
        private PrototypeSquad squad;
        private int memberIndex;
        private Vector2 spawn;
        private Vector2 objective;
        private readonly List<Vector2> route = new List<Vector2>(16);
        private int waypoint;
        private float repathAt;
        private float nextShot;
        private float respawnAt;
        private float lastReportAt;
        private float lastRouteFailureAt;
        private float lastProgressAt;
        private Vector2 lastProgressPosition;
        private float underFireUntil;
        private float nextCoverSearch;
        private float coverUntil;
        private Vector2 coverPoint;
        private Vector2 lastThreatPosition;
        private float investigateUntil;
        private float nextScanAt;
        private Quaternion scanFacing;
        private Collider bodyCollider;
        private PrototypeSoldierVisual visual;
        private PrototypeInfantryKit kit;
        private PrototypeTransportVehicle transport;
        private PrototypeTransportVehicle boardingTarget;
        private int transportSeat = -1;
        private float boardAgainAt;
        private float boardingDeadline;
        private bool receivedNetworkState;
        private int rocketCount;
        private float nextRocketAt;
        private float launcherReadyAt;
        private GameObject heldLauncher;
        private IPrototypeVehicle heldRocketTarget;

        public PrototypeTeam Team { get; private set; }
        public string DisplayName { get; private set; } = "Soldier";
        public PrototypeInfantryClass Class => kit.Role;
        public PrototypeInfantryKit Kit => kit;
        public int RocketCount => rocketCount;
        public float RocketReadyAt => Mathf.Max(nextRocketAt, launcherReadyAt);
        public bool IsUsingLauncher => heldLauncher != null && heldLauncher.activeSelf;
        public float LauncherAimPitch { get; private set; }
        public bool Alive => Health > 0f;
        public bool IsPassenger => transport != null;
        public float Health { get; private set; } = 100f;
        public Vector2 MapPosition => transport != null ? transport.MapPosition :
            new Vector2(transform.position.x, transform.position.z);
        public Vector2 SpawnPosition => spawn;
        public Vector2 ObjectivePosition => objective;
        public string AssignedObjectiveId { get; private set; } = "-";
        public PrototypeTacticalState TacticalState { get; private set; } = PrototypeTacticalState.Advance;
        public Vector2 ReservedCover => coverUntil > Time.time ? coverPoint : MapPosition;

        public void Initialize(PrototypeRuntime game, PrototypeNavigation nav,
            PrototypeTeam team, Vector2 spawnPoint, PrototypeInfantryClass role)
        {
            runtime = game;
            navigation = nav;
            Team = team;
            int nameOrdinal = 0;
            foreach (PrototypeBot peer in game.Bots)
                if (peer != this && peer.Team == team) nameOrdinal++;
            DisplayName = PrototypeBotRoster.NameFor(team, nameOrdinal);
            name = Team + " " + DisplayName;
            kit = new PrototypeInfantryKit(role);
            spawn = spawnPoint;
            objective = spawnPoint;
            bodyCollider = GetComponent<Collider>();
            GetComponent<Renderer>().enabled = false;
            visual = gameObject.AddComponent<PrototypeSoldierVisual>();
            visual.Initialize(game, team, role, -1f);
            Respawn();
        }

        public void JoinSquad(PrototypeCommander assignedCommander, PrototypeSquad assignedSquad, int member)
        {
            commander = assignedCommander;
            squad = assignedSquad;
            memberIndex = member;
            name = Team + " " + DisplayName + " / Squad " + squad.Id;
        }

        public void SetObjective(Vector2 destination, string id)
        {
            if (AssignedObjectiveId == id && Vector2.Distance(objective, destination) < 1f) return;
            objective = destination;
            AssignedObjectiveId = id;
            route.Clear();
            waypoint = 0;
            repathAt = 0f;
            lastProgressAt = Time.time;
            lastProgressPosition = MapPosition;
        }

        public void ApplyNetworkState(Vector3 position, float yaw, float health, bool alive)
        {
            if (runtime == null || !runtime.IsNetworkReplica ||
                float.IsNaN(position.x) || float.IsNaN(position.y) || float.IsNaN(position.z) ||
                float.IsNaN(yaw) || float.IsNaN(health) ||
                float.IsInfinity(position.x) || float.IsInfinity(position.y) ||
                float.IsInfinity(position.z) || float.IsInfinity(yaw) ||
                float.IsInfinity(health)) return;
            bool wasAlive = Alive;
            transform.position = position;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            Health = alive ? Mathf.Clamp(health, 0.01f, 100f) : 0f;
            if (visual != null) visual.SetVisible(alive);
            if (bodyCollider != null) bodyCollider.enabled = alive;
            TacticalState = alive ? PrototypeTacticalState.Advance : PrototypeTacticalState.Down;
            if (receivedNetworkState && wasAlive && !Alive)
                runtime.NotifyBotDowned(this);
            receivedNetworkState = true;
        }

        private void Update()
        {
            if (runtime == null || runtime.IsNetworkReplica || !runtime.MatchStarted ||
                runtime.Match.Winner.HasValue || Time.timeScale <= 0f ||
                runtime.PauseOpen && !runtime.IsNetworkSession) return;
            if (!Alive)
            {
                TacticalState = PrototypeTacticalState.Down;
                if (Time.time >= respawnAt) Respawn();
                return;
            }
            if (transport != null)
            {
                if (!transport.Alive) DisembarkTransport();
                else { HideLauncher(); TacticalState = PrototypeTacticalState.Hold; return; }
            }
            kit.Tick(Time.time);
            TrySupportNearby();
            if (TryRescueAlly()) return;

            if (Time.time < investigateUntil)
                TurnToward(lastThreatPosition, CombatTurnRate);
            else if (TacticalState == PrototypeTacticalState.Hold ||
                TacticalState == PrototypeTacticalState.InCover)
                ScanSurroundings();

            Vector2 enemy = FindVisibleEnemy(out PrototypeBot bot,
                out PrototypePlayer player, out IPrototypeVehicle vehicle,
                out PrototypeRemotePlayer remote);
            bool inCombat = bot != null || player != null || vehicle != null || remote != null;
            if (inCombat)
            {
                investigateUntil = 0f;
                Vector2 facing = vehicle != null ? RocketAimMapPosition(vehicle) : enemy;
                TurnToward(facing, CombatTurnRate);
            }
            if (vehicle != null) ShowLauncher(vehicle);
            else HideLauncher();
            if (inCombat && squad != null) squad.ReportContact(enemy, Time.time);
            if (inCombat && commander != null && Time.time - lastReportAt >= 1f)
            {
                commander.ReportEnemy(enemy, Time.time);
                lastReportAt = Time.time;
            }
            float enemyDistance = inCombat ? Vector2.Distance(MapPosition, enemy) : float.PositiveInfinity;
            if (vehicle != null) TryFireVehicleRocket(vehicle);
            else if (inCombat && IsAimedAtPosition(enemy) &&
                enemyDistance < kit.Weapon.MaxRange && Time.time >= nextShot &&
                kit.TryFire(Time.time))
            {
                nextShot = Time.time + Mathf.Max(kit.Weapon.FireInterval,
                    Random.Range(0.38f, 0.62f));
                Vector3 origin = transform.position + Vector3.up * 0.55f;
                Vector3 aim = player != null ? player.transform.position + Vector3.up * 1.1f :
                    remote != null ? remote.transform.position + Vector3.up * 1.1f :
                    bot.transform.position;
                float flight = Vector3.Distance(origin, aim) / kit.Weapon.MuzzleVelocity;
                aim.y -= Physics.gravity.y * 0.5f * flight * flight;
                float accuracy = Mathf.Lerp(0.78f, 0.32f,
                    enemyDistance / kit.DetectionRange);
                if (Random.value > accuracy)
                {
                    Vector3 sideways = Vector3.Cross(Vector3.up, aim - origin).normalized;
                    aim += sideways * (Random.value < .5f ? -8f : 8f);
                }
                PrototypeBallisticProjectile.LaunchBullet(runtime, Team, transform,
                    origin, aim - origin, kit.Weapon, .29f);
            }

            // A spent launcher is no reason to run into a nearby enemy hull.
            // Keep observation separate from attack capability, so a vehicle
            // inside the blast safety distance never becomes a rifle target.
            if (vehicle == null && TryAvoidNearbyVehicle()) return;

            Vector2 threat = enemy;
            bool recentContact = !inCombat && squad != null &&
                squad.TryRecentContact(Time.time, out threat);
            if (inCombat && coverUntil > Time.time &&
                !PrototypeLayout.LineBlocked(coverPoint, enemy, 0.1f))
            {
                coverUntil = 0f;
                route.Clear();
                repathAt = 0f;
            }
            if ((inCombat || recentContact) &&
                (Health < 65f || Time.time < underFireUntil) &&
                Time.time >= nextCoverSearch && coverUntil <= Time.time)
            {
                nextCoverSearch = Time.time + 3.5f;
                Vector2 mate = squad != null && squad.Members.Length > 1 ?
                    squad.Members[1 - memberIndex].ReservedCover : MapPosition;
                if (PrototypeTactics.TryFindCover(MapPosition, threat, objective, mate,
                    navigation, out Vector2 chosen))
                {
                    coverPoint = chosen;
                    coverUntil = Time.time + 5f;
                    route.Clear();
                    repathAt = 0f;
                }
            }
            if (coverUntil > Time.time)
            {
                if (Vector2.Distance(MapPosition, coverPoint) > 1.8f)
                {
                    TacticalState = PrototypeTacticalState.SeekCover;
                    MoveToward(coverPoint, false, null, vehicle);
                }
                else
                {
                    TacticalState = PrototypeTacticalState.InCover;
                    lastProgressAt = Time.time;
                    lastProgressPosition = MapPosition;
                }
                return;
            }

            // A hit gives a direction to investigate, never an enemy target.
            // The soldier still needs to turn, see an unobstructed target and aim.
            if (!inCombat && Time.time < investigateUntil)
            {
                TacticalState = PrototypeTacticalState.Search;
                return;
            }

            // Check the surroundings before committing to a nearby transport.
            if (!inCombat && TryBoardNearbyTransport()) return;

            if (!inCombat && recentContact && memberIndex == 0 &&
                Vector2.Distance(threat, objective) < 70f &&
                Vector2.Distance(MapPosition, threat) < 55f)
            {
                if (Vector2.Distance(MapPosition, threat) > 5f)
                {
                    TacticalState = PrototypeTacticalState.Search;
                    MoveToward(threat, false);
                    return;
                }
                squad.ClearContact();
            }

            if (inCombat)
            {
                TacticalState = PrototypeTacticalState.Engage;
                float preferredRange = vehicle != null ? 45f : memberIndex == 0 ? 24f : 38f;
                Vector2 engageFacing = vehicle != null ? RocketAimMapPosition(vehicle) : enemy;
                // An engineer waits for a reload at a useful stand-off range;
                // capture orders must not walk them into the enemy hull.
                if (vehicle != null && enemyDistance < 25f)
                {
                    Vector2 away = (MapPosition - enemy).normalized;
                    if (away.sqrMagnitude < .5f) away = -new Vector2(transform.forward.x, transform.forward.z);
                    MoveToward(MapPosition + away * 18f, false, engageFacing, vehicle);
                }
                // An attacker that stops at firing range can remain outside the
                // 25 m capture circle forever, especially on the long C-D route.
                // Keep shooting while pressing the assigned objective.
                else if (squad != null && squad.Mission == PrototypeMission.Capture &&
                    Vector2.Distance(MapPosition, objective) > 20f)
                    MoveToward(objective, true, engageFacing, vehicle);
                else if (enemyDistance > preferredRange) MoveToward(enemy, false, engageFacing, vehicle);
                else { lastProgressAt = Time.time; lastProgressPosition = MapPosition; }
                return;
            }

            if (TacticalState != PrototypeTacticalState.Advance &&
                TacticalState != PrototypeTacticalState.Hold)
            {
                lastProgressAt = Time.time;
                lastProgressPosition = MapPosition;
            }
            if (Vector2.Distance(MapPosition, objective) < 18f)
            {
                TacticalState = PrototypeTacticalState.Hold;
                return;
            }
            TacticalState = PrototypeTacticalState.Advance;
            MoveToward(objective, true);
        }

        private bool TryRescueAlly()
        {
            if (kit.Role != PrototypeInfantryClass.Medic) return false;
            PrototypePlayer player = runtime.Player;
            PrototypeRemotePlayer remoteTarget = null;
            Vector2 target = Vector2.zero;
            float nearest = float.PositiveInfinity;
            if (player != null && player.Team == Team && player.IsDowned)
            {
                nearest = Vector2.Distance(MapPosition, player.MapPosition);
                target = player.MapPosition;
            }
            foreach (PrototypeRemotePlayer remote in runtime.RemotePlayers)
            {
                if (remote.Team != Team || !remote.IsDowned) continue;
                float distance = Vector2.Distance(MapPosition, remote.MapPosition);
                if (distance >= nearest) continue;
                nearest = distance;
                target = remote.MapPosition;
                remoteTarget = remote;
            }
            bool called = remoteTarget != null ? remoteTarget.RescueCalled :
                player != null && player.RescueCalled;
            if (nearest > (called ? 100f : 60f)) return false;
            if (nearest <= 3.5f)
            {
                TacticalState = PrototypeTacticalState.Revive;
                TurnToward(target, CombatTurnRate);
                bool revived = remoteTarget != null ? remoteTarget.TryRevive(this) :
                    player.TryRevive(this);
                if (revived) PrototypeCombatFeedback.Support(runtime, transform.position,
                    PrototypeInfantryClass.Medic);
                lastProgressAt = Time.time;
                lastProgressPosition = MapPosition;
                return true;
            }
            TacticalState = PrototypeTacticalState.Advance;
            MoveToward(target, false);
            return true;
        }

        private Vector2 FindVisibleEnemy(out PrototypeBot selectedBot,
            out PrototypePlayer selectedPlayer, out IPrototypeVehicle selectedVehicle,
            out PrototypeRemotePlayer selectedRemote)
        {
            selectedBot = null;
            selectedPlayer = null;
            selectedVehicle = null;
            selectedRemote = null;
            Vector2 position = MapPosition;
            Vector2 selectedPosition = Vector2.zero;
            float bestDistance = kit.DetectionRange;

            if (runtime.Player != null && runtime.Player.Team != Team && runtime.Player.Alive &&
                runtime.Player.CurrentVehicle == null)
            {
                Vector2 point = runtime.Player.MapPosition;
                float distance = Vector2.Distance(position, point);
                if (distance < bestDistance && CanSeePosition(point))
                {
                    bestDistance = distance;
                    selectedPosition = point;
                    selectedPlayer = runtime.Player;
                }
            }
            foreach (PrototypeRemotePlayer candidate in runtime.RemotePlayers)
            {
                if (candidate == null || !candidate.Alive || candidate.Team == Team) continue;
                Vector2 point = candidate.MapPosition;
                float distance = Vector2.Distance(position, point);
                if (distance >= bestDistance || !CanSeePosition(point)) continue;
                bestDistance = distance;
                selectedPosition = point;
                selectedBot = null;
                selectedPlayer = null;
                selectedVehicle = null;
                selectedRemote = candidate;
            }
            foreach (PrototypeBot candidate in runtime.Bots)
            {
                if (candidate == this || !candidate.Alive || candidate.IsPassenger ||
                    candidate.Team == Team) continue;
                Vector2 point = candidate.MapPosition;
                float distance = Vector2.Distance(position, point);
                if (distance >= bestDistance || !CanSeePosition(point)) continue;
                bestDistance = distance;
                selectedPosition = point;
                selectedBot = candidate;
                selectedPlayer = null;
                selectedVehicle = null;
                selectedRemote = null;
            }
            // Anti-armor carriers give an active, visible vehicle priority.
            // A soldier with no effective explosive keeps the infantry target
            // above instead of repeatedly wasting rifle rounds on the hull.
            float nearestVehicle = kit.DetectionRange;
            foreach (IPrototypeVehicle candidate in runtime.Vehicles)
            {
                if (!CanAttackVehicle(candidate)) continue;
                Vector2 point = candidate.MapPosition;
                float distance = Vector3.Distance(RocketEyePosition, VehicleAimPoint(candidate));
                if (distance >= nearestVehicle) continue;
                nearestVehicle = distance;
                selectedPosition = point;
                selectedBot = null;
                selectedPlayer = null;
                selectedVehicle = candidate;
                selectedRemote = null;
            }
            return selectedPosition;
        }

        private Vector3 RocketEyePosition => transform.position + Vector3.up * .55f;

        private static Collider VehicleHull(IPrototypeVehicle vehicle)
        {
            if (vehicle == null || vehicle.VehicleTransform == null ||
                !vehicle.VehicleTransform.gameObject.activeInHierarchy) return null;
            Collider hull = vehicle.VehicleTransform.GetComponent<Collider>();
            if (hull != null && hull.enabled && !hull.isTrigger) return hull;
            foreach (Collider candidate in vehicle.VehicleTransform.GetComponentsInChildren<Collider>())
                if (candidate.enabled && !candidate.isTrigger) return candidate;
            return null;
        }

        private static Vector3 VehicleAimPoint(IPrototypeVehicle vehicle)
        {
            Collider hull = VehicleHull(vehicle);
            return hull != null ? hull.bounds.center : vehicle.VehicleTransform.position;
        }

        // This is also the selection policy: cooldown does not invalidate an
        // otherwise useful target, but exhausted inventory and ineffective
        // damage types do. Aircraft are checked at their actual altitude.
        public bool CanAttackVehicle(IPrototypeVehicle vehicle)
        {
            if (kit == null || kit.Role != PrototypeInfantryClass.Engineer ||
                rocketCount <= 0 ||
                PrototypeVehicleArmor.Resolve(vehicle, 210f,
                    PrototypeDamageClass.AntiArmorRocket) <= 0f ||
                !CanObserveVehicle(vehicle, out Collider hull)) return false;
            Vector3 origin = RocketEyePosition;
            Vector3 delta = hull.bounds.center - origin;
            float horizontal = new Vector2(delta.x, delta.z).magnitude;
            return Vector3.Distance(origin, hull.ClosestPoint(origin)) >= RocketSafetyDistance &&
                Mathf.Abs(Mathf.Atan2(delta.y, Mathf.Max(.01f, horizontal)) * Mathf.Rad2Deg) <=
                    LauncherPitchLimit;
        }

        private bool CanObserveVehicle(IPrototypeVehicle vehicle, out Collider hull)
        {
            hull = null;
            if (!Alive || IsPassenger || vehicle == null || !vehicle.Alive ||
                vehicle.Team == Team || !vehicle.IsActiveThreat || vehicle.VehicleTransform == null)
                return false;
            Physics.SyncTransforms();
            hull = VehicleHull(vehicle);
            if (hull == null) return false;
            Vector3 origin = RocketEyePosition;
            Vector3 point = hull.bounds.center;
            if (!Finite(origin) || !Finite(point)) return false;
            Vector3 delta = point - origin;
            if (delta.magnitude >= kit.DetectionRange ||
                !IsFacing(vehicle.MapPosition, SightHalfAngle)) return false;
            return RocketSightClear(origin, point, vehicle.VehicleTransform, false);
        }

        private bool TryAvoidNearbyVehicle()
        {
            if (kit.Role != PrototypeInfantryClass.Engineer) return false;
            IPrototypeVehicle nearest = null;
            float clearance = RocketSafetyDistance + 4f;
            foreach (IPrototypeVehicle candidate in runtime.Vehicles)
            {
                if (!CanObserveVehicle(candidate, out Collider hull)) continue;
                float distance = Vector3.Distance(RocketEyePosition, hull.ClosestPoint(RocketEyePosition));
                if (distance >= clearance) continue;
                clearance = distance;
                nearest = candidate;
            }
            if (nearest == null) return false;
            Vector2 away = (MapPosition - nearest.MapPosition).normalized;
            if (away.sqrMagnitude < .5f) away = -new Vector2(transform.forward.x, transform.forward.z);
            TacticalState = PrototypeTacticalState.SeekCover;
            MoveToward(MapPosition + away * 18f, false, nearest.MapPosition, nearest);
            return true;
        }

        private static bool Finite(Vector3 value) =>
            !float.IsNaN(value.x) && !float.IsNaN(value.y) && !float.IsNaN(value.z) &&
            !float.IsInfinity(value.x) && !float.IsInfinity(value.y) && !float.IsInfinity(value.z);

        private bool RocketSightClear(Vector3 origin, Vector3 point, Transform target, bool swept)
        {
            if (PrototypeSmokeCloud.SegmentBlocked(origin, point)) return false;
            Vector3 delta = point - origin;
            if (delta.sqrMagnitude < .0001f) return false;
            Physics.SyncTransforms();
            RaycastHit[] hits = swept ? Physics.SphereCastAll(origin, .09f, delta.normalized,
                delta.magnitude, ~0, QueryTriggerInteraction.Ignore) :
                Physics.RaycastAll(origin, delta.normalized, delta.magnitude,
                    ~0, QueryTriggerInteraction.Ignore);
            foreach (RaycastHit hit in hits)
                if (!hit.collider.transform.IsChildOf(transform) &&
                    (target == null || !hit.collider.transform.IsChildOf(target))) return false;
            return true;
        }

        private Vector3 RocketAimPoint(IPrototypeVehicle vehicle, Vector3 origin)
        {
            Vector3 point = VehicleAimPoint(vehicle);
            float speed = vehicle.Speed;
            Vector3 velocity = !float.IsNaN(speed) && !float.IsInfinity(speed) ?
                vehicle.VehicleTransform.forward * speed : Vector3.zero;
            float flight = Vector3.Distance(point, origin) / RocketVelocity;
            // A second estimate includes the target's own travel time. This is
            // an unguided rocket, not a new instant-lock anti-air weapon.
            for (int step = 0; step < 2; step++)
                flight = Vector3.Distance(point + velocity * flight, origin) / RocketVelocity;
            point += velocity * flight;
            point.y -= Physics.gravity.y * .075f * flight * flight;
            return point;
        }

        private Vector2 RocketAimMapPosition(IPrototypeVehicle vehicle)
        {
            Vector3 point = RocketAimPoint(vehicle, RocketEyePosition);
            return new Vector2(point.x, point.z);
        }

        private void ShowLauncher(IPrototypeVehicle target)
        {
            if (heldLauncher == null)
                heldLauncher = PrototypeEquipmentVisual.BuildLauncher(visual.WeaponMount, runtime);
            bool newlyRaised = !IsUsingLauncher || heldRocketTarget == null;
            heldRocketTarget = target;
            heldLauncher.SetActive(true);
            if (visual.HeldWeapon != null) visual.HeldWeapon.SetActive(false);
            if (newlyRaised)
            {
                launcherReadyAt = Time.time + LauncherRaiseSeconds;
                visual.SetHeldEquipment(heldLauncher, PrototypeHeldEquipment.Launcher);
            }
            AlignLauncher(target);
        }

        private void AlignLauncher(IPrototypeVehicle target)
        {
            // Recompute after posing: the actual muzzle moves when the shoulder
            // launcher is tilted, and both hands follow that same equipment.
            for (int step = 0; step < 2; step++)
            {
                Vector3 origin = heldLauncher.transform.TransformPoint(new Vector3(0f, 0f, .76f));
                Vector3 delta = RocketAimPoint(target, origin) - origin;
                LauncherAimPitch = -Mathf.Clamp(Mathf.Atan2(delta.y,
                    Mathf.Max(.01f, new Vector2(delta.x, delta.z).magnitude)) * Mathf.Rad2Deg,
                    -LauncherPitchLimit, LauncherPitchLimit);
                visual.RefreshPose(true);
            }
        }

        private void HideLauncher()
        {
            heldRocketTarget = null;
            LauncherAimPitch = 0f;
            if (heldLauncher == null || !heldLauncher.activeSelf) return;
            heldLauncher.SetActive(false);
            visual.SetHeldEquipment(null, PrototypeHeldEquipment.Firearm);
            if (visual.HeldWeapon != null) visual.HeldWeapon.SetActive(true);
        }

        private bool TryFireVehicleRocket(IPrototypeVehicle target)
        {
            if (runtime == null || runtime.IsNetworkReplica || !runtime.MatchStarted ||
                runtime.Match.Winner.HasValue || Time.timeScale <= 0f ||
                runtime.PauseOpen && !runtime.IsNetworkSession || !IsUsingLauncher ||
                Time.time < RocketReadyAt || !CanAttackVehicle(target)) return false;
            AlignLauncher(target);
            Vector3 origin = heldLauncher.transform.TransformPoint(new Vector3(0f, 0f, .76f));
            Vector3 direction = heldLauncher.transform.forward;
            Vector3 point = RocketAimPoint(target, origin);
            Collider hull = VehicleHull(target);
            if (!Finite(origin) || !Finite(point) || Vector3.Distance(origin, point) >= 160f ||
                hull == null || Vector3.Distance(origin, hull.ClosestPoint(origin)) < RocketSafetyDistance ||
                Vector3.Angle(direction, point - origin) > RocketFireHalfAngle ||
                !RocketSightClear(RocketEyePosition, origin, null, true) ||
                !RocketSightClear(origin, point, target.VehicleTransform, true) ||
                !RocketFlightClear(origin, direction, Vector3.Distance(origin, point),
                    target.VehicleTransform)) return false;
            foreach (Collider nearby in Physics.OverlapSphere(origin, .1f, ~0,
                QueryTriggerInteraction.Ignore))
                if (!nearby.transform.IsChildOf(transform)) return false;
            if (PrototypeBallisticProjectile.LaunchRocket(runtime, Team, transform,
                origin, direction) == null) return false;
            rocketCount--;
            nextRocketAt = Time.time + RocketCooldown;
            return true;
        }

        private bool RocketFlightClear(Vector3 origin, Vector3 direction, float distance, Transform target)
        {
            float flight = distance / RocketVelocity;
            int steps = Mathf.Max(1, Mathf.CeilToInt(flight / .06f));
            Vector3 previous = origin;
            for (int step = 1; step <= steps; step++)
            {
                float time = flight * step / steps;
                // Match the existing rocket's reduced-gravity flight, checking
                // the curved corridor as well as the straight optical sight.
                Vector3 point = origin + direction * (RocketVelocity * time) +
                    Physics.gravity * (.075f * time * time);
                if (!RocketSightClear(previous, point, target, true)) return false;
                previous = point;
            }
            return true;
        }

        // Horizontal body direction is the soldier's view. Distance and cover
        // alone must not reveal an enemy standing behind them.
        public bool CanSeePosition(Vector2 point)
        {
            if (Vector2.Distance(MapPosition, point) >= kit.DetectionRange ||
                !IsFacing(point, SightHalfAngle)) return false;
            return !PrototypeLayout.SightBlocked(MapPosition, point, 0.1f);
        }

        public bool IsAimedAtPosition(Vector2 point) => IsFacing(point, FireHalfAngle);

        private bool IsFacing(Vector2 point, float halfAngle)
        {
            Vector2 direction = point - MapPosition;
            if (direction.sqrMagnitude < 0.0001f) return true;
            Vector3 forward = transform.forward;
            return Vector2.Dot(new Vector2(forward.x, forward.z).normalized,
                direction.normalized) >= Mathf.Cos(halfAngle * Mathf.Deg2Rad);
        }

        private void TurnToward(Vector2 point, float degreesPerSecond)
        {
            Vector2 direction = point - MapPosition;
            if (direction.sqrMagnitude < 0.0001f) return;
            Quaternion facing = Quaternion.LookRotation(new Vector3(direction.x, 0f, direction.y));
            transform.rotation = Quaternion.RotateTowards(transform.rotation,
                facing, degreesPerSecond * Time.deltaTime);
        }

        private void ScanSurroundings()
        {
            if (Time.time >= nextScanAt)
            {
                nextScanAt = Time.time + Random.Range(1.5f, 2.4f);
                float turn = Random.Range(80f, 150f) * (Random.value < 0.5f ? -1f : 1f);
                scanFacing = Quaternion.Euler(0f, transform.eulerAngles.y + turn, 0f);
            }
            transform.rotation = Quaternion.RotateTowards(transform.rotation,
                scanFacing, 95f * Time.deltaTime);
        }

        private bool TryBoardNearbyTransport()
        {
            if (Time.time < boardAgainAt) return false;
            if (boardingTarget != null && (Time.time >= boardingDeadline ||
                !boardingTarget.HasFreePassengerSeat || boardingTarget.Team != Team ||
                Mathf.Abs(boardingTarget.Speed) > 6f ||
                Vector2.Distance(MapPosition, boardingTarget.MapPosition) > 55f))
            {
                boardingTarget = null;
                boardAgainAt = Time.time + 12f;
                return false;
            }
            if (boardingTarget == null)
            {
                if (Vector2.Distance(MapPosition, objective) < 45f) return false;
                float nearest = 45f;
                foreach (IPrototypeVehicle candidate in runtime.Vehicles)
                {
                    PrototypeTransportVehicle rover = candidate as PrototypeTransportVehicle;
                    if (rover == null || !rover.HasFreePassengerSeat || rover.Team != Team ||
                        Mathf.Abs(rover.Speed) > 6f) continue;
                    float distance = Vector2.Distance(MapPosition, rover.MapPosition);
                    if (distance >= nearest) continue;
                    boardingTarget = rover;
                    nearest = distance;
                }
                if (boardingTarget == null) return false;
                boardingDeadline = Time.time + 18f;
            }
            if (Vector2.Distance(MapPosition, boardingTarget.MapPosition) <= 7f &&
                boardingTarget.TryBoardPassenger(this)) return true;
            TacticalState = PrototypeTacticalState.Advance;
            MoveToward(boardingTarget.MapPosition, false);
            return true;
        }

        internal void BoardTransport(PrototypeTransportVehicle vehicle, int seat)
        {
            HideLauncher();
            transport = vehicle;
            boardingTarget = null;
            transportSeat = seat;
            route.Clear();
            waypoint = 0;
            repathAt = 0f;
            coverUntil = 0f;
            visual.SetVisible(false);
            bodyCollider.enabled = false;
            transform.SetParent(vehicle.transform, true);
            transform.position = vehicle.GetSeatPosition(seat);
            TacticalState = PrototypeTacticalState.Hold;
        }

        public void DisembarkTransport()
        {
            if (transport == null) return;
            PrototypeTransportVehicle vehicle = transport;
            int seat = transportSeat;
            Vector3 exit = vehicle.FindPassengerExitPosition(seat, spawn);
            vehicle.RemovePassenger(this);
            transport = null;
            transportSeat = -1;
            transform.SetParent(runtime.transform, true);
            transform.position = exit;
            transform.rotation = Quaternion.Euler(0f, vehicle.transform.eulerAngles.y, 0f);
            visual.SetVisible(true);
            bodyCollider.enabled = true;
            boardAgainAt = Time.time + 25f;
            boardingTarget = null;
            route.Clear();
            waypoint = 0;
            repathAt = 0f;
            lastProgressAt = Time.time;
            lastProgressPosition = MapPosition;
            TacticalState = PrototypeTacticalState.Advance;
        }

        private void TrySupportNearby()
        {
            if (kit.GadgetCharges <= 0 || Time.time < kit.GadgetReadyAt) return;
            float now = Time.time;
            if (kit.Role == PrototypeInfantryClass.Medic)
            {
                PrototypeBot woundedBot = null;
                float lowestHealth = Health;
                bool helpPlayer = false;
                if (runtime.Player != null && runtime.Player.Team == Team && runtime.Player.Alive &&
                    Vector2.Distance(MapPosition, runtime.Player.MapPosition) <= 8f &&
                    runtime.Player.Health < lowestHealth)
                {
                    lowestHealth = runtime.Player.Health;
                    helpPlayer = true;
                }
                foreach (PrototypeBot ally in runtime.Bots)
                {
                    if (ally == this || !ally.Alive || ally.IsPassenger || ally.Team != Team ||
                        Vector2.Distance(MapPosition, ally.MapPosition) > 8f ||
                        ally.Health >= lowestHealth) continue;
                    woundedBot = ally;
                    helpPlayer = false;
                    lowestHealth = ally.Health;
                }
                if (kit.TryHeal(now, lowestHealth, 0f, out float healed))
                {
                    if (woundedBot != null) woundedBot.HealTo(healed);
                    else if (helpPlayer) runtime.Player.HealTo(healed);
                    else HealTo(healed);
                    PrototypeCombatFeedback.Support(runtime, transform.position, kit.Role);
                }
            }
            else if (kit.Role == PrototypeInfantryClass.Assault)
            {
                if (kit.TryResupply(now, kit, 0f))
                { PrototypeCombatFeedback.Support(runtime, transform.position, kit.Role); return; }
                if (runtime.Player != null && runtime.Player.Team == Team && runtime.Player.Alive &&
                    kit.TryResupply(now, runtime.Player.Kit,
                        Vector2.Distance(MapPosition, runtime.Player.MapPosition)))
                { PrototypeCombatFeedback.Support(runtime, transform.position, kit.Role); return; }
                foreach (PrototypeBot ally in runtime.Bots)
                {
                    if (ally == this || !ally.Alive || ally.IsPassenger || ally.Team != Team) continue;
                    if (kit.TryResupply(now, ally.Kit,
                        Vector2.Distance(MapPosition, ally.MapPosition)))
                    { PrototypeCombatFeedback.Support(runtime, transform.position, kit.Role); return; }
                }
            }
            else if (kit.Role == PrototypeInfantryClass.Engineer)
            {
                if (kit.TryApplyArmor(now, kit, 0f))
                { PrototypeCombatFeedback.Support(runtime, transform.position, kit.Role); return; }
                if (runtime.Player != null && runtime.Player.Team == Team && runtime.Player.Alive &&
                    kit.TryApplyArmor(now, runtime.Player.Kit,
                        Vector2.Distance(MapPosition, runtime.Player.MapPosition)))
                { PrototypeCombatFeedback.Support(runtime, transform.position, kit.Role); return; }
                foreach (PrototypeBot ally in runtime.Bots)
                {
                    if (ally == this || !ally.Alive || ally.IsPassenger || ally.Team != Team) continue;
                    if (kit.TryApplyArmor(now, ally.Kit,
                        Vector2.Distance(MapPosition, ally.MapPosition)))
                    { PrototypeCombatFeedback.Support(runtime, transform.position, kit.Role); return; }
                }
            }
        }

        public void HealTo(float health)
        {
            if (Alive) Health = Mathf.Clamp(health, Health, 100f);
        }

        private void MoveToward(Vector2 destination, bool onMission, Vector2? lookAt = null,
            IPrototypeVehicle keepClearOf = null)
        {
            if (Time.time >= repathAt || (route.Count > 0 &&
                Vector2.Distance(route[route.Count - 1], destination) > 8f))
            {
                navigation.Find(MapPosition, destination, route);
                waypoint = 0;
                repathAt = Time.time + 2f;
            }
            if (route.Count == 0)
            {
                if (onMission) ReportRouteFailure();
                return;
            }
            while (waypoint < route.Count && Vector2.Distance(MapPosition, route[waypoint]) < 1.6f)
                waypoint++;
            if (waypoint >= route.Count) return;

            float pace = TacticalState == PrototypeTacticalState.Revive || TacticalState == PrototypeTacticalState.InCover ? 2f : 5.8f;
            Vector2 next = Vector2.MoveTowards(MapPosition, route[waypoint], pace * Time.deltaTime);
            if (keepClearOf != null)
            {
                Collider hull = VehicleHull(keepClearOf);
                Vector3 nextEye = new Vector3(next.x,
                    PrototypeLayout.HeightAt(next.x, next.y) + 1.55f, next.y);
                float nowClearance = hull != null ? Vector3.Distance(RocketEyePosition,
                    hull.ClosestPoint(RocketEyePosition)) : float.PositiveInfinity;
                float nextClearance = hull != null ? Vector3.Distance(nextEye,
                    hull.ClosestPoint(nextEye)) : float.PositiveInfinity;
                if (nextClearance < RocketSafetyDistance + 2f && nextClearance < nowClearance) return;
            }
            if (PrototypeLayout.Collides(next, 0.5f))
            {
                repathAt = 0f;
                if (onMission) ReportRouteFailure();
                return;
            }
            Vector3 direction = new Vector3(next.x - transform.position.x, 0f, next.y - transform.position.z);
            transform.position = new Vector3(next.x, PrototypeLayout.HeightAt(next.x, next.y) + 1f, next.y);
            if (Vector2.Distance(MapPosition, lastProgressPosition) > 2f)
            {
                lastProgressPosition = MapPosition;
                lastProgressAt = Time.time;
            }
            else if (onMission && Time.time - lastProgressAt > 4f) ReportRouteFailure();
            if (lookAt.HasValue)
                TurnToward(lookAt.Value, CombatTurnRate);
            else if (direction.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation,
                    Quaternion.LookRotation(direction), 280f * Time.deltaTime);
        }

        private void ReportRouteFailure()
        {
            if (commander == null || Time.time - lastRouteFailureAt < 5f) return;
            lastRouteFailureAt = Time.time;
            commander.ReportRouteFailure(squad, Time.time);
        }

        public void TakeDamage(float amount, Vector2? sourcePosition = null)
        {
            if (runtime == null || runtime.IsNetworkReplica || !Alive || IsPassenger ||
                float.IsNaN(amount) || float.IsInfinity(amount) || amount <= 0f) return;
            Health = Mathf.Max(0f, Health - kit.AbsorbDamage(amount));
            underFireUntil = Time.time + 4f;
            if (sourcePosition.HasValue && Health > 0f)
            {
                lastThreatPosition = sourcePosition.Value;
                investigateUntil = Time.time + 1.25f;
            }
            if (Alive) return;
            HideLauncher();
            runtime.Scores?.ResolveDefeat(this, Team);
            runtime.NotifyBotDowned(this);
            runtime.Match.RecordDeath(Team);
            respawnAt = Time.time + 6f;
            visual.SetVisible(false);
            bodyCollider.enabled = false;
            TacticalState = PrototypeTacticalState.Down;
        }

        private void Respawn()
        {
            if (transport != null) DisembarkTransport();
            HideLauncher();
            boardingTarget = null;
            kit.ResetForSpawn();
            rocketCount = kit.Role == PrototypeInfantryClass.Engineer ? EngineerRocketCapacity : 0;
            nextRocketAt = launcherReadyAt = 0f;
            Vector2 point = spawn;
            transform.position = new Vector3(point.x, PrototypeLayout.HeightAt(point.x, point.y) + 1f, point.y);
            Health = 100f;
            runtime.Scores?.BeginLife(this);
            route.Clear();
            waypoint = 0;
            repathAt = 0f;
            lastProgressAt = Time.time;
            lastProgressPosition = point;
            lastRouteFailureAt = -10f;
            underFireUntil = 0f;
            nextCoverSearch = 0f;
            coverUntil = 0f;
            investigateUntil = 0f;
            nextScanAt = 0f;
            TacticalState = PrototypeTacticalState.Advance;
            if (visual != null) visual.SetVisible(true);
            if (bodyCollider != null) bodyCollider.enabled = true;
        }
    }
}
