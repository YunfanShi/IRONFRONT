using System.Collections.Generic;
using UnityEngine;

namespace Ironfront.UnityPrototype
{
    public sealed class PrototypeBot : MonoBehaviour
    {
        private PrototypeRuntime runtime;
        private PrototypeNavigation navigation;
        private Vector2 spawn;
        private Vector2 objective;
        private List<Vector2> route = new List<Vector2>();
        private int waypoint;
        private float repathAt;
        private float nextShot;
        private float respawnAt;
        private Renderer body;
        private Collider bodyCollider;

        public PrototypeTeam Team { get; private set; }
        public bool Alive => Health > 0f;
        public float Health { get; private set; } = 100f;
        public Vector2 MapPosition => new Vector2(transform.position.x, transform.position.z);

        public void Initialize(PrototypeRuntime game, PrototypeNavigation nav,
            PrototypeTeam team, Vector2 spawnPoint, Vector2 assignedObjective)
        {
            runtime = game;
            navigation = nav;
            Team = team;
            spawn = spawnPoint;
            objective = assignedObjective;
            body = GetComponent<Renderer>();
            bodyCollider = GetComponent<Collider>();
            body.sharedMaterial = team == PrototypeTeam.Blue ? game.BlueMaterial : game.RedMaterial;
            Respawn();
        }

        private void Update()
        {
            if (runtime == null || runtime.Match.Winner.HasValue) return;
            if (!Alive)
            {
                if (Time.time >= respawnAt) Respawn();
                return;
            }

            Vector2 enemy = FindVisibleEnemy(out PrototypeBot bot, out PrototypePlayer player);
            bool inCombat = bot != null || player != null;
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

            Vector2 destination = inCombat && enemyDistance > 27f ? enemy : objective;
            if (!inCombat && Vector2.Distance(MapPosition, objective) < 18f) return;
            if (inCombat && enemyDistance <= 27f) return;
            MoveToward(destination);
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

        private void MoveToward(Vector2 destination)
        {
            if (Time.time >= repathAt || route.Count == 0 ||
                Vector2.Distance(route[route.Count - 1], destination) > 8f)
            {
                route = navigation.Find(MapPosition, destination);
                waypoint = 0;
                repathAt = Time.time + 2f;
            }
            if (route.Count == 0) return;
            while (waypoint < route.Count && Vector2.Distance(MapPosition, route[waypoint]) < 1.6f)
                waypoint++;
            if (waypoint >= route.Count) return;

            Vector2 next = Vector2.MoveTowards(MapPosition, route[waypoint], 5.8f * Time.deltaTime);
            if (PrototypeLayout.Collides(next, 0.5f)) { repathAt = 0f; return; }
            Vector3 direction = new Vector3(next.x - transform.position.x, 0f, next.y - transform.position.z);
            transform.position = new Vector3(next.x, PrototypeLayout.HeightAt(next.x, next.y) + 1f, next.y);
            if (direction.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation,
                    Quaternion.LookRotation(direction), 280f * Time.deltaTime);
        }

        public void TakeDamage(float amount)
        {
            if (!Alive) return;
            Health = Mathf.Max(0f, Health - amount);
            if (Alive) return;
            runtime.Match.RecordDeath(Team);
            respawnAt = Time.time + 6f;
            body.enabled = false;
            bodyCollider.enabled = false;
        }

        private void Respawn()
        {
            Vector2 point = spawn;
            transform.position = new Vector3(point.x, PrototypeLayout.HeightAt(point.x, point.y) + 1f, point.y);
            Health = 100f;
            route.Clear();
            waypoint = 0;
            repathAt = 0f;
            if (body != null) body.enabled = true;
            if (bodyCollider != null) bodyCollider.enabled = true;
        }
    }
}
