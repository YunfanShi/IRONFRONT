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

        public PrototypeTeam Team { get; private set; }
        public bool Alive => Health > 0f;
        public float Health { get; private set; } = 100f;
        public Vector2 MapPosition => new Vector2(transform.position.x, transform.position.z);
        public Vector2 SpawnPosition => spawn;
        public string AssignedObjectiveId { get; private set; } = "-";
        public PrototypeTacticalState TacticalState { get; private set; } = PrototypeTacticalState.Advance;
        public Vector2 ReservedCover => coverUntil > Time.time ? coverPoint : MapPosition;

        public void Initialize(PrototypeRuntime game, PrototypeNavigation nav,
            PrototypeTeam team, Vector2 spawnPoint)
        {
            runtime = game;
            navigation = nav;
            Team = team;
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
            if (runtime == null || runtime.Match.Winner.HasValue) return;
            if (!Alive)
            {
                TacticalState = PrototypeTacticalState.Down;
                if (Time.time >= respawnAt) Respawn();
                return;
            }

            Vector2 enemy = FindVisibleEnemy(out PrototypeBot bot, out PrototypePlayer player);
            bool inCombat = bot != null || player != null;
            if (inCombat && squad != null) squad.ReportContact(enemy, Time.time);
            if (inCombat && commander != null && Time.time - lastReportAt >= 1f)
            {
                commander.ReportEnemy(enemy, Time.time);
                lastReportAt = Time.time;
            }
            float enemyDistance = inCombat ? Vector2.Distance(MapPosition, enemy) : float.PositiveInfinity;
            if (inCombat && enemyDistance < 80f && Time.time >= nextShot)
            {
                nextShot = Time.time + Random.Range(0.38f, 0.62f);
                Vector3 origin = transform.position + Vector3.up * 0.55f;
                float targetHeight = player != null ? 1.25f : 0.45f;
                Vector3 impact = new Vector3(enemy.x,
                    PrototypeLayout.HeightAt(enemy.x, enemy.y) + 1f + targetHeight, enemy.y);
                runtime.ShowTracer(origin, impact, Team);
                if (Random.value < Mathf.Lerp(0.78f, 0.32f, enemyDistance / 80f))
                {
                    if (player != null) player.TakeDamage(9f);
                    else bot.TakeDamage(9f);
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

        private Vector2 FindVisibleEnemy(out PrototypeBot selectedBot, out PrototypePlayer selectedPlayer)
        {
            selectedBot = null;
            selectedPlayer = null;
            Vector2 position = MapPosition;
            Vector2 selectedPosition = Vector2.zero;
            float bestDistance = 85f;

            if (Team == PrototypeTeam.Red && runtime.Player != null && runtime.Player.Alive)
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
                if (candidate == this || !candidate.Alive || candidate.Team == Team) continue;
                Vector2 point = candidate.MapPosition;
                float distance = Vector2.Distance(position, point);
                if (distance >= bestDistance || PrototypeLayout.LineBlocked(position, point, 0.1f)) continue;
                bestDistance = distance;
                selectedPosition = point;
                selectedBot = candidate;
                selectedPlayer = null;
            }
            return selectedPosition;
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
            if (!Alive) return;
            Health = Mathf.Max(0f, Health - amount);
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
