using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ironfront.UnityPrototype
{
    public enum PrototypeSupportKind { Motorcycle, Ifv, AntiAir, ReconHelicopter, AttackHelicopter }

    public sealed class PrototypeSupportOption
    {
        public readonly PrototypeSupportKind Kind;
        public readonly string ChineseName, Description;
        public readonly int Cost;
        public readonly PrototypeVehicleKind VehicleKind;

        public PrototypeSupportOption(PrototypeSupportKind kind, string name, int cost,
            string description, PrototypeVehicleKind vehicleKind)
        { Kind = kind; ChineseName = name; Cost = cost; Description = description; VehicleKind = vehicleKind; }
    }

    // A request buys one physical vehicle, with the same seats, combat and
    // engine audio as the regular fleet. It never changes the fleet's old IDs.
    public sealed class PrototypeSupportRequisition : IDisposable
    {
        public const float DeliveryDelay = 6f;
        public const float RequestCooldown = 60f;
        public const int ActiveLimitPerTeam = 2;
        private static readonly PrototypeSupportOption[] Options =
        {
            new PrototypeSupportOption(PrototypeSupportKind.Motorcycle, "M2 侦察摩托", 200,
                "2 席 · 快速转移", PrototypeVehicleKind.Motorcycle),
            new PrototypeSupportOption(PrototypeSupportKind.Ifv, "V12 步兵战车", 500,
                "4 席 · 30mm 炮 / 机枪", PrototypeVehicleKind.Ifv),
            new PrototypeSupportOption(PrototypeSupportKind.AntiAir, "A40 防空车", 500,
                "4 席 · 防空炮 / 制导导弹", PrototypeVehicleKind.AntiAir),
            new PrototypeSupportOption(PrototypeSupportKind.ReconHelicopter, "R3 侦察直升机", 600,
                "3 席 · 机枪 / 雷达", PrototypeVehicleKind.ReconHelicopter),
            new PrototypeSupportOption(PrototypeSupportKind.AttackHelicopter, "H8 武装直升机", 900,
                "4 席 · 主炮 / 下射机枪", PrototypeVehicleKind.Helicopter)
        };
        private sealed class DeliveredVehicle
        {
            public PrototypeCombinedArmsVehicle Vehicle;
            public float WreckAge;
        }
        private readonly PrototypeRuntime runtime;
        private readonly PrototypeScoreSystem scores;
        private readonly List<DeliveredVehicle> delivered = new List<DeliveredVehicle>();
        private PrototypeSupportOption pendingOption;
        private PrototypePlayer requester;
        private PrototypeTeam requestTeam;
        private Vector2 reservedPosition;
        private float deliveryRemaining, cooldownRemaining;

        public IReadOnlyList<PrototypeSupportOption> Catalog { get; } = Array.AsReadOnly(Options);
        public bool Pending => pendingOption != null;
        public float DeliveryRemaining => Pending ? deliveryRemaining : 0f;
        public float CooldownReadyIn => Mathf.Max(0f, cooldownRemaining);
        public string Status { get; private set; } = "积分可呼叫载具，6 秒后送达己方基地补给区。";
        public IPrototypeVehicle LastDelivered { get; private set; }
        public Vector2 PendingPosition => reservedPosition;

        public PrototypeSupportRequisition(PrototypeRuntime game, PrototypeScoreSystem scoreSystem)
        { runtime = game; scores = scoreSystem; }

        public int ActiveCount(PrototypeTeam team)
        {
            int count = 0;
            foreach (DeliveredVehicle record in delivered)
                if (record.Vehicle != null && record.Vehicle.Alive && record.Vehicle.Team == team) count++;
            return count;
        }

        public bool TryRequest(PrototypeSupportKind kind, out string reason)
        {
            PrototypeSupportOption option = null;
            foreach (PrototypeSupportOption candidate in Options)
                if (candidate.Kind == kind) { option = candidate; break; }
            if (option == null) return Reject("未知支援项目。", out reason);
            if (!CanOrder(out reason)) return Reject(reason, out reason);
            if (Pending) return Reject("已有载具正在送达。", out reason);
            if (cooldownRemaining > 0f)
                return Reject("支援冷却 " + Mathf.CeilToInt(cooldownRemaining) + " 秒。", out reason);
            PrototypePlayer player = runtime.Player;
            if (ActiveCount(player.Team) >= ActiveLimitPerTeam)
                return Reject("己方已有 2 辆支援载具，请使用现有载具。", out reason);
            if (!TryFindDrop(player.Team, option.VehicleKind, null, out Vector2 position))
                return Reject("基地补给区被占用或受敌方威胁，积分未扣除。", out reason);
            if (scores == null || !scores.TrySpend(player, option.Cost, "呼叫 " + option.ChineseName))
                return Reject("积分不足：需要 " + option.Cost + " 分。", out reason);

            // No yield occurs between debit and reservation. Repeated button
            // events see Pending immediately, so the price is charged once.
            requester = player; requestTeam = player.Team; reservedPosition = position;
            pendingOption = option; deliveryRemaining = DeliveryDelay;
            cooldownRemaining = RequestCooldown;
            reason = "已呼叫 " + option.ChineseName + "，6 秒后抵达己方基地补给区。";
            Status = reason;
            return true;
        }

        private bool Reject(string message, out string reason)
        { Status = reason = message; return false; }

        private bool CanOrder(out string reason)
        {
            reason = "";
            if (runtime == null || !runtime.MatchStarted || runtime.Match == null ||
                runtime.Match.Winner.HasValue)
                reason = "进入战斗后才能呼叫支援。";
            // New paid vehicle IDs need a descriptor in LAN snapshots before
            // guests can reconstruct them. Keep this feature in solo battles
            // until that protocol is available instead of invisible host aid.
            else if (runtime.IsNetworkSession) reason = "当前载具支援仅用于单人战局。";
            else if (runtime.IsNetworkReplica) reason = "请在单人战局呼叫载具支援。";
            else if (runtime.PauseOpen || runtime.Frontend != null && runtime.Frontend.IsOpen)
                reason = "请先返回战场。";
            else if (runtime.Player == null || !runtime.Player.Alive)
                reason = "重新部署后才能呼叫支援。";
            else if (runtime.Player.CurrentVehicle != null)
                reason = "下车后才能呼叫载具支援。";
            return reason.Length == 0;
        }

        public void Tick(float dt)
        {
            if (runtime == null) return;
            if (Pending && (!runtime.MatchStarted || runtime.Match == null ||
                runtime.Match.Winner.HasValue || runtime.IsNetworkSession || runtime.IsNetworkReplica ||
                requester == null || requester.Team != requestTeam))
                CancelPending("支援已取消，积分已返还。");
            if (runtime.PauseOpen || !Finite(dt) || dt <= 0f) return;
            cooldownRemaining = Mathf.Max(0f, cooldownRemaining - dt);
            foreach (DeliveredVehicle record in delivered)
            {
                PrototypeCombinedArmsVehicle vehicle = record.Vehicle;
                if (vehicle == null || vehicle.Alive) continue;
                // Disable immediately rather than letting an autorespawn tick
                // turn a paid wreck into a second free vehicle.
                vehicle.enabled = false;
                record.WreckAge += dt;
                if (record.WreckAge >= 12f) vehicle.gameObject.SetActive(false);
            }
            if (!Pending) return;
            deliveryRemaining = Mathf.Max(0f, deliveryRemaining - dt);
            if (deliveryRemaining > .0001f) return;
            Deliver();
        }

        private void Deliver()
        {
            PrototypeSupportOption option = pendingOption;
            if (ActiveCount(requestTeam) >= ActiveLimitPerTeam ||
                !TryFindDrop(requestTeam, option.VehicleKind, reservedPosition, out Vector2 position))
            { CancelPending("补给区无法安全交付，积分已返还。"); return; }
            GameObject object3D = null;
            try
            {
                object3D = new GameObject(requestTeam + " support " + option.VehicleKind);
                object3D.transform.SetParent(runtime.transform, false);
                var vehicle = object3D.AddComponent<PrototypeCombinedArmsVehicle>();
                vehicle.RespawnEnabled = false;
                vehicle.Initialize(runtime, requestTeam, option.VehicleKind, position, false);
                // Append after Initialize succeeds; never remove an earlier
                // index while a match is in progress, including destroyed aid.
                runtime.Vehicles.Add(vehicle);
                delivered.Add(new DeliveredVehicle { Vehicle = vehicle });
                LastDelivered = vehicle;
                string side = requestTeam == PrototypeTeam.Blue ? "蓝方" : "红方";
                Status = option.ChineseName + " 已抵达" + side + "基地补给区 [" +
                    Mathf.RoundToInt(position.x) + ", " + Mathf.RoundToInt(position.y) +
                    "]，可用 E 上车或阵亡后部署到座位。";
                ClearPending();
            }
            catch (Exception error)
            {
                if (object3D != null) UnityEngine.Object.Destroy(object3D);
                CancelPending("载具交付失败，积分已返还。");
                Debug.LogError("IRONFRONT support delivery failed: " + error);
            }
        }

        public static Vector2 BaseDropPosition(PrototypeTeam team)
        { return team == PrototypeTeam.Blue ? new Vector2(-311f, -265f) : new Vector2(311f, 265f); }

        private bool TryFindDrop(PrototypeTeam team, PrototypeVehicleKind kind,
            Vector2? preferred, out Vector2 position)
        {
            position = Vector2.zero;
            float radius = PrototypeVehicleDefinition.Get(kind).Radius + .8f;
            Physics.SyncTransforms();
            if (preferred.HasValue && DropClear(team, preferred.Value, radius))
            { position = preferred.Value; return true; }
            Vector2 origin = BaseDropPosition(team);
            float sign = team == PrototypeTeam.Blue ? 1f : -1f;
            for (int row = 0; row < 2; row++)
                for (int column = 0; column < 5; column++)
                {
                    Vector2 candidate = origin + new Vector2(sign * column * 18f, sign * row * 18f);
                    if (!DropClear(team, candidate, radius)) continue;
                    position = candidate; return true;
                }
            return false;
        }

        private bool DropClear(PrototypeTeam team, Vector2 position, float radius)
        {
            if (PrototypeLayout.Collides(position, radius)) return false;
            float floor = PrototypeLayout.HeightAt(position.x, position.y);
            foreach (IPrototypeVehicle vehicle in runtime.Vehicles)
                if (vehicle != null && vehicle.Alive &&
                    vehicle.VehicleTransform.position.y < floor + 14f &&
                    Vector2.Distance(position, vehicle.MapPosition) < radius + PrototypeVehicleSpace.Radius(vehicle))
                    return false;
            if (runtime.Player != null && runtime.Player.Alive &&
                Vector2.Distance(position, runtime.Player.MapPosition) < radius + 1f) return false;
            foreach (PrototypeBot bot in runtime.Bots)
                if (bot.Alive && !bot.IsPassenger && Vector2.Distance(position, bot.MapPosition) <
                    (bot.Team == team ? radius + 1f : 65f)) return false;
            foreach (PrototypeRemotePlayer player in runtime.RemotePlayers)
                if (player.Alive && Vector2.Distance(position, player.MapPosition) <
                    (player.Team == team ? radius + 1f : 65f)) return false;
            foreach (IPrototypeVehicle vehicle in runtime.Vehicles)
                if (vehicle != null && vehicle.Alive && vehicle.Team != team &&
                    Vector2.Distance(position, vehicle.MapPosition) < 100f) return false;

            // This also checks runtime-added cover which is absent from the
            // layout table. The overlap starts above the terrain surface.
            Vector3 center = new Vector3(position.x, floor + 3.2f, position.y);
            foreach (Collider collider in Physics.OverlapBox(center,
                new Vector3(radius, 3f, radius), Quaternion.identity, ~(1 << 2),
                QueryTriggerInteraction.Ignore))
            {
                if (!collider.enabled || !collider.gameObject.activeInHierarchy) continue;
                if (collider.gameObject.name == "Terrain 720 m") continue;
                // Expired wrecks and ballistic shells do not block delivery.
                IPrototypeVehicle vehicle = PrototypeVehicleHit.Find(collider);
                if (vehicle != null && !vehicle.Alive) continue;
                if (collider.GetComponentInParent<PrototypeVehicleProjectile>() != null ||
                    collider.GetComponentInParent<PrototypeBallisticProjectile>() != null) continue;
                return false;
            }
            return true;
        }

        private void CancelPending(string message)
        {
            if (pendingOption != null && requester != null)
                scores?.Refund(requester, pendingOption.Cost, "支援未交付");
            ClearPending(); cooldownRemaining = 0f; Status = message;
        }

        private void ClearPending()
        { pendingOption = null; requester = null; deliveryRemaining = 0f; }

        public void Reset()
        {
            CancelPending("积分可呼叫载具，6 秒后送达己方基地补给区。");
            foreach (DeliveredVehicle record in delivered)
                if (record.Vehicle != null) record.Vehicle.RetireSupportVehicle();
            delivered.Clear(); LastDelivered = null;
        }

        public void Dispose() { Reset(); }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
