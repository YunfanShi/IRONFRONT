using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ironfront.UnityPrototype
{
    public sealed class PrototypeAmmoSupplySystem : IDisposable
    {
        public const float ResupplyCooldown = 30f;
        public const float CrateDeployCooldown = 90f;
        public const float InteractionRange = 3f;
        public const int OwnedCrateLimit = 2;
        private readonly PrototypeRuntime runtime;
        private readonly List<PrototypeAmmoSupplyPoint> points = new List<PrototypeAmmoSupplyPoint>();
        private readonly Dictionary<PrototypePlayer, float> nextResupplyAt = new Dictionary<PrototypePlayer, float>();
        private readonly Dictionary<PrototypePlayer, float> nextDeployAt = new Dictionary<PrototypePlayer, float>();
        private float statusUntil;
        public IReadOnlyList<PrototypeAmmoSupplyPoint> Points => points;
        public string Status { get; private set; } = "";

        public PrototypeAmmoSupplySystem(PrototypeRuntime game)
        { runtime = game; BuildFixedPoints(); }

        private void BuildFixedPoints()
        {
            AddFixed(PrototypeLayout.BlueBase, "蓝方基地中立补给点", true);
            AddFixed(PrototypeLayout.RedBase, "红方基地中立补给点", true);
            foreach (PrototypeLayout.Objective objective in PrototypeLayout.Objectives)
                AddFixed(objective.Position, objective.Id + " 区中立补给点", false);
        }
        private void AddFixed(Vector2 anchor, string name, bool isBase)
        {
            float radius = isBase ? 18f : 33f;
            for (int ring = 0; ring < 3; ring++)
                for (int step = 0; step < 16; step++)
                {
                    float angle = step * Mathf.PI / 8f;
                    Vector2 candidate = anchor + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (radius + ring * 8f);
                    if (!SpawnClear(candidate, .9f)) continue;
                    Create(candidate, name, null); return;
                }
            Debug.LogWarning("IRONFRONT fixed ammo supply has no clear bay: " + name);
        }
        private PrototypeAmmoSupplyPoint Create(Vector2 position, string name, PrototypePlayer owner)
        {
            var object3D = new GameObject(name); object3D.transform.SetParent(runtime.transform, false);
            var point = object3D.AddComponent<PrototypeAmmoSupplyPoint>();
            point.Initialize(runtime, position, name, owner); points.Add(point); return point;
        }

        public PrototypeAmmoSupplyPoint NearestInteraction(Vector3 position, float distance = InteractionRange)
        {
            PrototypeAmmoSupplyPoint nearest = null; float best = distance;
            foreach (PrototypeAmmoSupplyPoint point in points)
            {
                if (point == null || !point.Alive) continue;
                float gap = Vector3.Distance(position + Vector3.up * .35f, point.transform.position + Vector3.up * .35f);
                if (gap > best) continue;
                best = gap; nearest = point;
            }
            return nearest;
        }
        public float ResupplyReadyIn(PrototypePlayer player) => player != null && nextResupplyAt.TryGetValue(player, out float time) ?
            Mathf.Max(0f, time - Time.time) : 0f;
        public float DeployReadyIn(PrototypePlayer player) => player != null && nextDeployAt.TryGetValue(player, out float time) ?
            Mathf.Max(0f, time - Time.time) : 0f;
        public int OwnedCount(PrototypePlayer player)
        {
            int count = 0;
            foreach (PrototypeAmmoSupplyPoint point in points)
                if (point != null && point.Alive && point.IsDeployable && point.Owner == player) count++;
            return count;
        }

        // Return whether E was handled, so a full/cooling crate cannot also
        // enter a nearby vehicle in the same input event.
        public bool TryUseNearest(PrototypePlayer player, out string reason)
        {
            reason = "";
            if (player == null || NearestInteraction(player.transform.position) == null) return false;
            TryResupply(player, out reason); return true;
        }
        public bool TryResupply(PrototypePlayer player, out string reason)
        {
            if (!Eligible(player, out reason)) return Reject(reason, out reason);
            PrototypeAmmoSupplyPoint point = NearestInteraction(player.transform.position);
            if (point == null) return Reject("靠近中立弹药箱 3 米内按 E 补给。", out reason);
            float cooldown = ResupplyReadyIn(player);
            if (cooldown > 0f) return Reject("补给冷却 " + Mathf.CeilToInt(cooldown) + " 秒（所有补给点共用）。", out reason);
            if (!player.ResupplyAllFromCrate()) return Reject("弹药与兵种装备已满，无需补给。", out reason);
            nextResupplyAt[player] = Time.time + ResupplyCooldown;
            reason = "已补满弹药与装备 · 补给冷却 30 秒。"; SetStatus(reason);
            PrototypeCombatFeedback.Support(runtime, point.transform.position, PrototypeInfantryClass.Assault);
            return true;
        }
        public bool TryDeployCrate(PrototypePlayer player, out string reason)
        {
            if (!Eligible(player, out reason)) return Reject(reason, out reason);
            if (runtime.IsNetworkSession) return Reject("可部署补给箱当前用于单人战局。", out reason);
            if (player.Class != PrototypeInfantryClass.Assault) return Reject("只有突击兵可以按 6 放置中立补给箱。", out reason);
            float cooldown = DeployReadyIn(player);
            if (cooldown > 0f) return Reject("补给箱部署冷却 " + Mathf.CeilToInt(cooldown) + " 秒。", out reason);
            Vector2 forward = new Vector2(player.transform.forward.x, player.transform.forward.z).normalized;
            Vector2 candidate = player.MapPosition + forward * 2.15f;
            if (!SpawnClear(candidate, .9f) || PrototypeLayout.CirclePathBlocked(player.MapPosition, candidate, .7f) ||
                !PlacementPathClear(player, candidate))
                return Reject("前方受阻，未消耗部署冷却。", out reason);
            PrototypeAmmoSupplyPoint oldest = null;
            if (OwnedCount(player) >= OwnedCrateLimit)
                foreach (PrototypeAmmoSupplyPoint point in points)
                    if (point != null && point.Alive && point.IsDeployable && point.Owner == player)
                    { oldest = point; break; }
            Create(candidate, "中立弹药补给箱", player);
            nextDeployAt[player] = Time.time + CrateDeployCooldown;
            if (oldest != null) { oldest.Retire(); points.Remove(oldest); UnityEngine.Object.Destroy(oldest.gameObject); }
            reason = "中立弹药箱已放置 · 部署冷却 90 秒。"; SetStatus(reason);
            return true;
        }
        private bool Eligible(PrototypePlayer player, out string reason)
        {
            reason = "";
            if (runtime == null || !runtime.MatchStarted || runtime.Match == null || runtime.Match.Winner.HasValue)
                reason = "进入战斗后才能使用补给。";
            else if (runtime.IsNetworkReplica) reason = "补给当前由单机或主机处理。";
            else if (player == null || player != runtime.Player || !player.Alive || player.CurrentVehicle != null)
                reason = "活着并下车后才能使用补给。";
            else if (runtime.PauseOpen || runtime.SupportMenuOpen || runtime.Frontend != null && runtime.Frontend.IsOpen)
                reason = "请先返回战场。";
            return reason.Length == 0;
        }
        private bool Reject(string message, out string reason) { reason = message; SetStatus(message); return false; }
        private void SetStatus(string message) { Status = message; statusUntil = Time.time + 3f; }

        private static bool PlacementPathClear(PrototypePlayer player, Vector2 position)
        {
            Vector3 start = player.transform.position + Vector3.up * .45f;
            Vector3 target = new Vector3(position.x, PrototypeLayout.HeightAt(position.x, position.y) + .4f, position.y);
            Vector3 travel = target - start;
            foreach (RaycastHit hit in Physics.SphereCastAll(start, .15f, travel.normalized, travel.magnitude,
                ~(1 << 2), QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(player.transform) || hit.collider.gameObject.name == "Terrain 720 m") continue;
                return false;
            }
            return true;
        }

        private bool SpawnClear(Vector2 position, float radius)
        {
            if (PrototypeLayout.Collides(position, radius)) return false;
            foreach (IPrototypeVehicle vehicle in runtime.Vehicles)
                if (vehicle.Alive && (!(vehicle is IPrototypeAircraftVehicle aircraft) || !aircraft.IsAircraft || aircraft.Altitude < 3f) &&
                    Vector2.Distance(vehicle.MapPosition, position) < radius + PrototypeVehicleSpace.Radius(vehicle)) return false;
            foreach (PrototypeAmmoSupplyPoint point in points)
                if (point != null && point.Alive && Vector2.Distance(point.MapPosition, position) < radius + .9f) return false;
            if (runtime.Player != null && runtime.Player.Alive && Vector2.Distance(runtime.Player.MapPosition, position) < radius + .5f)
                return false;
            foreach (PrototypeBot bot in runtime.Bots)
                if (bot.Alive && !bot.IsPassenger && Vector2.Distance(bot.MapPosition, position) < radius + .5f) return false;
            foreach (PrototypeRemotePlayer remote in runtime.RemotePlayers)
                if (remote.Alive && Vector2.Distance(remote.MapPosition, position) < radius + .5f) return false;
            Physics.SyncTransforms();
            float floor = PrototypeLayout.HeightAt(position.x, position.y);
            foreach (Collider collider in Physics.OverlapBox(new Vector3(position.x, floor + .42f, position.y),
                new Vector3(radius, .39f, radius), Quaternion.identity, ~(1 << 2), QueryTriggerInteraction.Ignore))
            {
                if (!collider.enabled || collider.gameObject.name == "Terrain 720 m") continue;
                return false;
            }
            return true;
        }
        public void Tick(float dt)
        {
            if (Time.time >= statusUntil) Status = "";
            for (int i = points.Count - 1; i >= 0; i--)
                if (points[i] == null || !points[i].Alive)
                {
                    PrototypeAmmoSupplyPoint point = points[i]; points.RemoveAt(i);
                    if (point != null) UnityEngine.Object.Destroy(point.gameObject);
                }
        }
        public void Reset()
        {
            for (int i = points.Count - 1; i >= 0; i--)
                if (points[i] == null || points[i].IsDeployable)
                {
                    PrototypeAmmoSupplyPoint point = points[i]; points.RemoveAt(i);
                    if (point != null) { point.Retire(); UnityEngine.Object.Destroy(point.gameObject); }
                }
            nextResupplyAt.Clear(); nextDeployAt.Clear();
            Status = ""; statusUntil = 0f;
        }
        public void Dispose()
        {
            foreach (PrototypeAmmoSupplyPoint point in points)
                if (point != null) { point.Retire(); UnityEngine.Object.Destroy(point.gameObject); }
            points.Clear(); nextResupplyAt.Clear(); nextDeployAt.Clear();
        }
    }
}
