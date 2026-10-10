using System.Collections.Generic;
using UnityEngine;

namespace Ironfront.UnityPrototype
{
    // Deployment choices follow main: base, held objectives, an available
    // squadmate, and an empty friendly vehicle. The host re-evaluates them.
    public static class PrototypeDeployment
    {
        public readonly struct Location
        {
            public readonly string Id;
            public readonly string Name;
            public readonly Vector2 Center;
            public readonly bool Available;
            public readonly bool Hazard;
            public readonly string Reason;
            public readonly string Kind;

            public Location(string id, string name, Vector2 center, bool available,
                bool hazard, string reason, string kind)
            {
                Id = id;
                Name = name;
                Center = center;
                Available = available;
                Hazard = hazard;
                Reason = reason;
                Kind = kind;
            }
        }

        public static List<Location> GetLocations(PrototypeRuntime game,
            PrototypeTeam team, bool includeVehicles)
        {
            var locations = new List<Location>();
            Vector2 home = team == PrototypeTeam.Blue ?
                PrototypeLayout.BlueBase : PrototypeLayout.RedBase;
            locations.Add(new Location("BASE", "主基地 / MAIN BASE", home,
                true, false, "安全部署", "base"));
            if (game.Player != null && game.Player.Team == team && game.Player.BeaconAvailable)
            {
                Vector2 beacon = game.Player.BeaconPosition;
                bool threatened = ThreatNear(game, team, beacon);
                locations.Add(new Location("BEACON", "侦察兵复活信标", beacon,
                    !threatened, threatened, threatened ? "附近有敌军" : "一次性部署", "beacon"));
            }
            foreach (PrototypeCapturePoint point in game.Match.Points)
            {
                Vector2 center = point.Definition.Position;
                bool owned = point.Owner == team;
                bool hazard = point.Contested || ThreatNear(game, team, center);
                locations.Add(new Location(point.Definition.Id,
                    point.Definition.Id + " · " + point.Definition.Name,
                    center, owned, hazard,
                    !owned ? "未被己方控制" :
                    hazard ? "交战据点 · 远距离部署" : "安全部署", "point"));
            }
            PrototypeCommander commander = team == PrototypeTeam.Blue ?
                game.BlueCommander : game.RedCommander;
            if (commander.Squads.Count > 0)
                foreach (PrototypeBot member in commander.Squads[0].Members)
                {
                    int index = game.Bots.IndexOf(member);
                    bool available = member.Alive && !member.IsPassenger &&
                        member.TacticalState != PrototypeTacticalState.Engage &&
                        member.TacticalState != PrototypeTacticalState.SeekCover &&
                        member.TacticalState != PrototypeTacticalState.InCover &&
                        member.TacticalState != PrototypeTacticalState.Search &&
                        !ThreatNear(game, team, member.MapPosition);
                    locations.Add(new Location("SQUAD-" + index,
                        "队友 " + (index + 1), member.MapPosition, available,
                        !available, !member.Alive ? "队友阵亡" :
                        member.IsPassenger ? "队友在载具中" :
                        available ? "安全部署" : "队友正在交战", "squad"));
                }
            if (includeVehicles)
                for (int i = 0; i < game.Vehicles.Count; i++)
                {
                    IPrototypeVehicle vehicle = game.Vehicles[i];
                    if (vehicle.Team != team) continue;
                    bool threatened = vehicle.Alive &&
                        ThreatNear(game, team, vehicle.MapPosition);
                    bool available = vehicle.Alive && vehicle.Occupant == null &&
                        !vehicle.IsActiveThreat && !threatened;
                    locations.Add(new Location("VEHICLE-" + i,
                        vehicle.VehicleName + " · 驾驶位", vehicle.MapPosition,
                        available, threatened, !vehicle.Alive ? "载具已被击毁" :
                        threatened ? "载具正在交战" :
                        available ? "安全座位" : "驾驶位已占用", "vehicle"));
                }
            return locations;
        }

        // Returns the same checked location that was used to choose a safe
        // position. Call this only on the authoritative Unity host.
        public static bool TryResolve(PrototypeRuntime game, PrototypeTeam team,
            string id, bool includeVehicles, out Vector2 position,
            out IPrototypeVehicle vehicle)
        {
            position = Vector2.zero;
            vehicle = null;
            Location? selected = null;
            foreach (Location location in GetLocations(game, team, includeVehicles))
                if (location.Id == id) { selected = location; break; }
            if (!selected.HasValue || !selected.Value.Available ||
                game.Match.Winner.HasValue) return false;
            Location choice = selected.Value;
            if (choice.Kind == "vehicle")
            {
                if (!int.TryParse(id.Substring("VEHICLE-".Length), out int index) ||
                    index < 0 || index >= game.Vehicles.Count) return false;
                vehicle = game.Vehicles[index];
                position = choice.Center;
                return vehicle.Alive && vehicle.Occupant == null && vehicle.Team == team;
            }
            int attempts = choice.Kind == "base" ? 25 : 72;
            for (int i = 0; i < attempts; i++)
            {
                float angle = i * Mathf.PI / 12f;
                float radius = choice.Kind == "base" ?
                    (i == 0 ? 0f : 4f + (i - 1) / 8 * 3f) :
                    choice.Kind == "squad" || choice.Kind == "beacon" ? 3f + i / 24 * 2f :
                    choice.Hazard ? 42f + i / 24 * 20f : 6f + i / 24 * 12f;
                Vector2 candidate = choice.Kind == "base" && i == 0 ? choice.Center :
                    choice.Center + new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * radius;
                if (PrototypeLayout.Collides(candidate, 1.3f) ||
                    Occupied(game, candidate) ||
                    (choice.Kind != "base" && choice.Kind != "squad" &&
                     ThreatNear(game, team, candidate))) continue;
                position = candidate;
                return true;
            }
            return false;
        }

        private static bool Occupied(PrototypeRuntime game, Vector2 position)
        {
            foreach (IPrototypeVehicle vehicle in game.Vehicles)
                if (vehicle.Alive && Vector2.Distance(vehicle.MapPosition, position) <
                    (vehicle is PrototypeTankVehicle ? 3.5f : 2.1f) + 1f)
                    return true;
            foreach (PrototypeBot bot in game.Bots)
                if (bot.Alive && !bot.IsPassenger &&
                    Vector2.Distance(bot.MapPosition, position) < 1.5f) return true;
            foreach (PrototypeRemotePlayer remote in game.RemotePlayers)
                if (remote.Alive && Vector2.Distance(remote.MapPosition, position) < 1.5f)
                    return true;
            if (game.Player != null && game.Player.Alive &&
                Vector2.Distance(game.Player.MapPosition, position) < 1.5f) return true;
            return false;
        }

        private static bool ThreatNear(PrototypeRuntime game, PrototypeTeam team,
            Vector2 position)
        {
            bool SoldierThreat(Vector2 enemy) =>
                Vector2.Distance(enemy, position) < 65f &&
                (Vector2.Distance(enemy, position) < 30f ||
                 !PrototypeLayout.LineBlocked(enemy, position));
            foreach (PrototypeBot bot in game.Bots)
                if (bot.Alive && bot.Team != team && SoldierThreat(bot.MapPosition))
                    return true;
            foreach (PrototypeRemotePlayer remote in game.RemotePlayers)
                if (remote.Alive && remote.Team != team && SoldierThreat(remote.MapPosition))
                    return true;
            if (game.Player != null && game.Player.Alive && game.Player.Team != team &&
                SoldierThreat(game.Player.MapPosition)) return true;
            foreach (IPrototypeVehicle vehicle in game.Vehicles)
                if (vehicle.Alive && vehicle.Team != team &&
                    Vector2.Distance(vehicle.MapPosition, position) < 100f &&
                    !PrototypeLayout.LineBlocked(vehicle.MapPosition, position))
                    return true;
            return false;
        }
    }
}
