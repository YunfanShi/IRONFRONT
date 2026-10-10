using System.Collections.Generic;
using UnityEngine;

namespace Ironfront.UnityPrototype
{
    public enum PrototypeTeam { Blue, Red }

    public sealed class PrototypeCapturePoint
    {
        public readonly PrototypeLayout.Objective Definition;
        public float Control;
        public PrototypeTeam? Owner;
        public bool Contested;
        public int BlueCount;
        public int RedCount;

        public PrototypeCapturePoint(PrototypeLayout.Objective definition) { Definition = definition; }
    }

    // Capture radius, progress speed and ticket bleed are ported from Conquest.ts.
    public sealed class PrototypeMatch
    {
        public readonly List<PrototypeCapturePoint> Points = new List<PrototypeCapturePoint>();
        public float BlueTickets { get; private set; } = 100f;
        public float RedTickets { get; private set; } = 100f;
        public PrototypeTeam? Winner { get; private set; }

        public PrototypeMatch()
        {
            foreach (PrototypeLayout.Objective objective in PrototypeLayout.Objectives)
                Points.Add(new PrototypeCapturePoint(objective));
        }

        public void RecordDeath(PrototypeTeam team)
        {
            if (Winner.HasValue) return;
            if (team == PrototypeTeam.Blue) BlueTickets = Mathf.Max(0f, BlueTickets - 1f);
            else RedTickets = Mathf.Max(0f, RedTickets - 1f);
            CheckWinner();
        }

        public void RecordVehicleLoss(PrototypeTeam team)
        {
            if (Winner.HasValue) return;
            if (team == PrototypeTeam.Blue) BlueTickets = Mathf.Max(0f, BlueTickets - 0.5f);
            else RedTickets = Mathf.Max(0f, RedTickets - 0.5f);
            CheckWinner();
        }

        public void Tick(float dt, PrototypePlayer player, IReadOnlyList<PrototypeBot> bots,
            IReadOnlyList<PrototypeRemotePlayer> remotePlayers = null)
        {
            if (Winner.HasValue) return;
            foreach (PrototypeCapturePoint point in Points)
            {
                int blue = 0;
                int red = 0;
                Vector2 position = point.Definition.Position;
                if (player != null && player.Alive &&
                    Vector2.Distance(player.MapPosition, position) < 25f)
                {
                    if (player.Team == PrototypeTeam.Blue) blue++;
                    else red++;
                }
                if (remotePlayers != null)
                    foreach (PrototypeRemotePlayer remote in remotePlayers)
                    {
                        if (remote == null || !remote.Alive ||
                            Vector2.Distance(remote.MapPosition, position) >= 25f) continue;
                        if (remote.Team == PrototypeTeam.Blue) blue++;
                        else red++;
                    }
                foreach (PrototypeBot bot in bots)
                {
                    if (!bot.Alive || bot.IsPassenger ||
                        Vector2.Distance(bot.MapPosition, position) >= 25f) continue;
                    if (bot.Team == PrototypeTeam.Blue) blue++;
                    else red++;
                }

                point.BlueCount = blue;
                point.RedCount = red;
                point.Contested = blue > 0 && red > 0;
                if (point.Contested || blue + red == 0) continue;

                int units = blue + red;
                float speed = 13f * (1f + Mathf.Min(1.3f, (units - 1) * 0.22f));
                point.Control = Mathf.Clamp(point.Control + dt * speed * (blue > 0 ? 1f : -1f), -100f, 100f);
                if (point.Owner == PrototypeTeam.Red && point.Control >= 0f) point.Owner = null;
                if (point.Owner == PrototypeTeam.Blue && point.Control <= 0f) point.Owner = null;
                if (point.Control >= 99.999f) point.Owner = PrototypeTeam.Blue;
                if (point.Control <= -99.999f) point.Owner = PrototypeTeam.Red;
            }

            int blueOwned = 0;
            int redOwned = 0;
            foreach (PrototypeCapturePoint point in Points)
            {
                if (point.Owner == PrototypeTeam.Blue) blueOwned++;
                if (point.Owner == PrototypeTeam.Red) redOwned++;
            }
            if (redOwned > blueOwned) BlueTickets = Mathf.Max(0f, BlueTickets - (redOwned - blueOwned) * 0.85f * dt);
            if (blueOwned > redOwned) RedTickets = Mathf.Max(0f, RedTickets - (blueOwned - redOwned) * 0.85f * dt);
            CheckWinner();
        }

        public void SetNetworkTickets(float blue, float red)
        {
            if (float.IsNaN(blue) || float.IsInfinity(blue) ||
                float.IsNaN(red) || float.IsInfinity(red)) return;
            BlueTickets = Mathf.Clamp(blue, 0f, 800f);
            RedTickets = Mathf.Clamp(red, 0f, 800f);
            Winner = null;
            CheckWinner();
        }

        public void SetNetworkPoint(string id, float control, int owner)
        {
            if (string.IsNullOrEmpty(id) || float.IsNaN(control) ||
                float.IsInfinity(control)) return;
            foreach (PrototypeCapturePoint point in Points)
            {
                if (point.Definition.Id != id) continue;
                point.Control = Mathf.Clamp(control, -100f, 100f);
                point.Owner = owner == 0 ? PrototypeTeam.Blue :
                    owner == 1 ? PrototypeTeam.Red : (PrototypeTeam?)null;
                point.Contested = false;
                point.BlueCount = 0;
                point.RedCount = 0;
                return;
            }
        }

        private void CheckWinner()
        {
            if (BlueTickets <= 0f) Winner = PrototypeTeam.Red;
            else if (RedTickets <= 0f) Winner = PrototypeTeam.Blue;
        }
    }
}
