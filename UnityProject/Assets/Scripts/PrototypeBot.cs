using System.Collections.Generic;
using UnityEngine;

namespace Ironfront.UnityPrototype
{
    public enum PrototypeTacticalState { Advance, Hold, Engage, Search, SeekCover, InCover, Down }

    public sealed class PrototypeBot : MonoBehaviour
    {
        private PrototypeRuntime runtime;
        private PrototypeNavigation navigation;
        private PrototypeCommander commander;
        private PrototypeSquad squad;
        private int memberIndex;
        private Vector2 spawn;
        private Vector2 objective;
        private List<Vector2> route = new List<Vector2>();
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
        private Renderer body;
        private Collider bodyCollider;
        private PrototypeInfantryKit kit;
        private PrototypeTransportVehicle transport;
        private PrototypeTransportVehicle boardingTarget;
        private int transportSeat = -1;
        private float boardAgainAt;
        private float boardingDeadline;

        public PrototypeTeam Team { get; private set; }
        public PrototypeInfantryClass Class => kit.Role;
        public PrototypeInfantryKit Kit => kit;
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
            kit = new PrototypeInfantryKit(role);
            spawn = spawnPoint;
            objective = spawnPoint;
            body = GetComponent<Renderer>();
            bodyCollider = GetComponent<Collider>();
            body.sharedMaterial = team == PrototypeTeam.Blue ? game.BlueMaterial : game.RedMaterial;
            Respawn();
        }

        public void JoinSquad(PrototypeCommander assignedCommander, PrototypeSquad assignedSquad, int member)
        {
            commander = assignedCommander;
            squad = assignedSquad;
            memberIndex = member;
            name = Team + " Squad " + squad.Id + " / " + (member + 1);
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

        private void Update()
        {
            if (runtime == null || !runtime.MatchStarted || runtime.Match.Winner.HasValue) return;
            if (!Alive)
            {
                TacticalState = PrototypeTacticalState.Down;
                if (Time.time >= respawnAt) Respawn();
                return;
            }
            if (transport != null)
            {
                if (!transport.Alive) DisembarkTransport();
                else { TacticalState = PrototypeTacticalState.Hold; return; }
            }
            kit.Tick(Time.time);
            TrySupportNearby();

            // A nearby transport is the squad's route to a distant objective.
            // Keep this commitment ahead of target selection while approaching it.
            if (TryBoardNearbyTransport()) return;

            Vector2 enemy = FindVisibleEnemy(out PrototypeBot bot,
                out PrototypePlayer player, out IPrototypeVehicle vehicle);
            bool inCombat = bot != null || player != null || vehicle != null;
            if (inCombat && squad != null) squad.ReportContact(enemy, Time.time);
            if (inCombat && commander != null && Time.time - lastReportAt >= 1f)
            {
                commander.ReportEnemy(enemy, Time.time);
                lastReportAt = Time.time;
            }
            float enemyDistance = inCombat ? Vector2.Distance(MapPosition, enemy) : float.PositiveInfinity;
            if (inCombat && enemyDistance < kit.Weapon.MaxRange && Time.time >= nextShot &&
                kit.TryFire(Time.time))
            {
                nextShot = Time.time + Mathf.Max(kit.Weapon.FireInterval,
                    Random.Range(0.38f, 0.62f));
                Vector3 origin = transform.position + Vector3.up * 0.55f;
                float targetHeight = player != null ? 1.25f : vehicle != null ? 1.1f : 0.45f;
                Vector3 impact = new Vector3(enemy.x,
                    PrototypeLayout.HeightAt(enemy.x, enemy.y) + 1f + targetHeight, enemy.y);
                runtime.ShowTracer(origin, impact, Team);
                if (Random.value < Mathf.Lerp(0.78f, 0.32f, enemyDistance / kit.DetectionRange))
                {
                    float damage = kit.Weapon.DamageAtRange(enemyDistance) * 0.29f;
                    if (player != null) player.TakeDamage(damage);
                    else if (bot != null) bot.TakeDamage(damage);
                    else vehicle.TakeDamage(damage * 0.55f);
                }
            }

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
                Vector2 mate = squad.Members[1 - memberIndex].ReservedCover;
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
                    MoveToward(coverPoint, false);
                }
                else
                {
                    TacticalState = PrototypeTacticalState.InCover;
                    lastProgressAt = Time.time;
                    lastProgressPosition = MapPosition;
                }
                return;
            }

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
                float preferredRange = memberIndex == 0 ? 24f : 38f;
                if (enemyDistance > preferredRange) MoveToward(enemy, false);
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

        private Vector2 FindVisibleEnemy(out PrototypeBot selectedBot,
            out PrototypePlayer selectedPlayer, out IPrototypeVehicle selectedVehicle)
        {
            selectedBot = null;
            selectedPlayer = null;
            selectedVehicle = null;
            Vector2 position = MapPosition;
            Vector2 selectedPosition = Vector2.zero;
            float bestDistance = kit.DetectionRange;

            if (Team == PrototypeTeam.Red && runtime.Player != null && runtime.Player.Alive &&
                runtime.Player.CurrentVehicle == null)
            {
                Vector2 point = runtime.Player.MapPosition;
                float distance = Vector2.Distance(position, point);
                if (distance < bestDistance && !PrototypeLayout.LineBlocked(position, point, 0.1f))
                {
                    bestDistance = distance;
                    selectedPosition = point;
                    selectedPlayer = runtime.Player;
                }
            }
            foreach (PrototypeBot candidate in runtime.Bots)
            {
                if (candidate == this || !candidate.Alive || candidate.IsPassenger ||
                    candidate.Team == Team) continue;
                Vector2 point = candidate.MapPosition;
                float distance = Vector2.Distance(position, point);
                if (distance >= bestDistance || PrototypeLayout.LineBlocked(position, point, 0.1f)) continue;
                bestDistance = distance;
                selectedPosition = point;
                selectedBot = candidate;
                selectedPlayer = null;
            }
            foreach (IPrototypeVehicle candidate in runtime.Vehicles)
            {
                if (!candidate.Alive || candidate.Team == Team || !candidate.IsActiveThreat) continue;
                Vector2 point = candidate.MapPosition;
                float distance = Vector2.Distance(position, point);
                if (distance >= bestDistance ||
                    PrototypeLayout.LineBlocked(position, point, 0.1f)) continue;
                bestDistance = distance;
                selectedPosition = point;
                selectedBot = null;
                selectedPlayer = null;
                selectedVehicle = candidate;
            }
            return selectedPosition;
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
            transport = vehicle;
            boardingTarget = null;
            transportSeat = seat;
            route.Clear();
            waypoint = 0;
            repathAt = 0f;
            coverUntil = 0f;
            body.enabled = false;
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
            body.enabled = true;
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
                if (Team == PrototypeTeam.Blue && runtime.Player != null && runtime.Player.Alive &&
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
                }
            }
            else if (kit.Role == PrototypeInfantryClass.Assault)
            {
                if (kit.TryResupply(now, kit, 0f)) return;
                if (Team == PrototypeTeam.Blue && runtime.Player != null && runtime.Player.Alive &&
                    kit.TryResupply(now, runtime.Player.Kit,
                        Vector2.Distance(MapPosition, runtime.Player.MapPosition))) return;
                foreach (PrototypeBot ally in runtime.Bots)
                {
                    if (ally == this || !ally.Alive || ally.IsPassenger || ally.Team != Team) continue;
                    if (kit.TryResupply(now, ally.Kit,
                        Vector2.Distance(MapPosition, ally.MapPosition))) return;
                }
            }
            else if (kit.Role == PrototypeInfantryClass.Engineer)
            {
                if (kit.TryApplyArmor(now, kit, 0f)) return;
                if (Team == PrototypeTeam.Blue && runtime.Player != null && runtime.Player.Alive &&
                    kit.TryApplyArmor(now, runtime.Player.Kit,
                        Vector2.Distance(MapPosition, runtime.Player.MapPosition))) return;
                foreach (PrototypeBot ally in runtime.Bots)
                {
                    if (ally == this || !ally.Alive || ally.IsPassenger || ally.Team != Team) continue;
                    if (kit.TryApplyArmor(now, ally.Kit,
                        Vector2.Distance(MapPosition, ally.MapPosition))) return;
                }
            }
        }

        public void HealTo(float health)
        {
            if (Alive) Health = Mathf.Clamp(health, Health, 100f);
        }

        private void MoveToward(Vector2 destination, bool onMission)
        {
            if (Time.time >= repathAt || (route.Count > 0 &&
                Vector2.Distance(route[route.Count - 1], destination) > 8f))
            {
                route = navigation.Find(MapPosition, destination);
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

            Vector2 next = Vector2.MoveTowards(MapPosition, route[waypoint], 5.8f * Time.deltaTime);
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
            if (direction.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation,
                    Quaternion.LookRotation(direction), 280f * Time.deltaTime);
        }

        private void ReportRouteFailure()
        {
            if (commander == null || Time.time - lastRouteFailureAt < 5f) return;
            lastRouteFailureAt = Time.time;
            commander.ReportRouteFailure(squad, Time.time);
        }

        public void TakeDamage(float amount)
        {
            if (!Alive || IsPassenger) return;
            Health = Mathf.Max(0f, Health - kit.AbsorbDamage(amount));
            underFireUntil = Time.time + 4f;
            if (Alive) return;
            runtime.Match.RecordDeath(Team);
            respawnAt = Time.time + 6f;
            body.enabled = false;
            bodyCollider.enabled = false;
            TacticalState = PrototypeTacticalState.Down;
        }

        private void Respawn()
        {
            if (transport != null) DisembarkTransport();
            boardingTarget = null;
            kit.ResetForSpawn();
            Vector2 point = spawn;
            transform.position = new Vector3(point.x, PrototypeLayout.HeightAt(point.x, point.y) + 1f, point.y);
            Health = 100f;
            route.Clear();
            waypoint = 0;
            repathAt = 0f;
            lastProgressAt = Time.time;
            lastProgressPosition = point;
            lastRouteFailureAt = -10f;
            underFireUntil = 0f;
            nextCoverSearch = 0f;
            coverUntil = 0f;
            TacticalState = PrototypeTacticalState.Advance;
            if (body != null) body.enabled = true;
            if (bodyCollider != null) bodyCollider.enabled = true;
        }
    }
}
