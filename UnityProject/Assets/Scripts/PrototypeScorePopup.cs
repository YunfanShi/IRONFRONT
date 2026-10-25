using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ironfront.UnityPrototype
{
    // Award events are collected independently of the bounded history in the
    // wallet. A burst shows the exact credited increments, including kill top-ups.
    public sealed class PrototypeScorePopup : IDisposable
    {
        public const float MergeSeconds = 1.4f;
        public const float VisibleSeconds = 3.8f;
        public readonly struct Row
        {
            public readonly PrototypeScoreNoticeKind Kind;
            public readonly object Victim;
            public readonly string Reason;
            public readonly int Points, SettlementPoints;
            public Row(PrototypeScoreSystem.Notice notice, int points)
            { Kind = notice.Kind; Victim = notice.Victim; Reason = notice.Reason;
                Points = points; SettlementPoints = notice.SettlementPoints; }
        }

        private readonly PrototypeScoreSystem scores;
        private readonly PrototypePlayer player;
        private readonly Func<float> clock;
        private readonly List<Row> rows = new List<Row>();
        private readonly List<PrototypeScoreNoticeKind> icons = new List<PrototypeScoreNoticeKind>();
        private float lastAwardAt, animatedTotal;
        private int lastDrawFrame = -1;
        private long lastSequence;
        private GUIStyle numberStyle, rowStyle;
        public int Total { get; private set; }
        public bool Visible => Total > 0 && clock() - lastAwardAt < VisibleSeconds;
        public IReadOnlyList<Row> Rows => rows;
        public IReadOnlyList<PrototypeScoreNoticeKind> Icons => icons;

        // Mounted score events sit to the right of the two central health bars.
        public static float CenterX(PrototypePlayer recipient, float width) =>
            PrototypePlayerHealthHud.TryReadVehicle(recipient, out _) ? width - 169f : width * .5f;

        public PrototypeScorePopup(PrototypeScoreSystem source, PrototypePlayer recipient,
            Func<float> time = null)
        {
            scores = source; player = recipient; clock = time ?? (() => Time.time);
            if (scores != null) scores.Awarded += OnAwarded;
        }

        public void Reset()
        {
            rows.Clear(); icons.Clear(); Total = 0; animatedTotal = 0f;
            lastAwardAt = 0f; lastSequence = 0; lastDrawFrame = -1;
        }

        private void OnAwarded(PrototypeScoreSystem.Notice notice)
        {
            if (!ReferenceEquals(notice.Actor, player) || notice.Points <= 0 ||
                notice.Kind == PrototypeScoreNoticeKind.Spend || notice.Kind == PrototypeScoreNoticeKind.Refund ||
                notice.Sequence <= lastSequence) return;
            float now = clock();
            if (Total == 0 || now - lastAwardAt > MergeSeconds)
            { rows.Clear(); icons.Clear(); Total = 0; animatedTotal = 0f; }
            lastSequence = notice.Sequence;
            Total = (int)Math.Min(int.MaxValue, (long)Total + notice.Points);
            bool merged = rows.Count > 0 && rows[0].Kind == notice.Kind &&
                ReferenceEquals(rows[0].Victim, notice.Victim);
            if (merged)
                rows[0] = new Row(notice, rows[0].Points + notice.Points);
            else
            {
                rows.Insert(0, new Row(notice, notice.Points));
                if (rows.Count > 4) rows.RemoveAt(rows.Count - 1);
            }
            if (notice.Kind == PrototypeScoreNoticeKind.Kill || notice.Kind == PrototypeScoreNoticeKind.Assist ||
                notice.Kind == PrototypeScoreNoticeKind.VehicleDestroyed || notice.Kind == PrototypeScoreNoticeKind.VehicleAssist)
            {
                icons.Add(notice.Kind);
                if (icons.Count > 6) icons.RemoveAt(0);
            }
            lastAwardAt = now;
        }

        public void Draw(float width, float height)
        {
            if (!Visible) return;
            if (numberStyle == null)
            {
                numberStyle = new GUIStyle(GUI.skin.label)
                    { fontSize = 34, alignment = TextAnchor.MiddleCenter };
                rowStyle = new GUIStyle(GUI.skin.label)
                    { fontSize = 12, alignment = TextAnchor.MiddleCenter };
                numberStyle.normal.textColor = rowStyle.normal.textColor = Color.white;
            }
            if (lastDrawFrame != Time.frameCount)
            {
                animatedTotal = Mathf.MoveTowards(animatedTotal, Total,
                    Mathf.Max(160f, Total * 5f) * Time.deltaTime);
                lastDrawFrame = Time.frameCount;
            }
            float age = clock() - lastAwardAt;
            float alpha = Mathf.Clamp01((VisibleSeconds - age) / .65f);
            float numberY = height * .63f;
            if (player != null && !player.Alive) numberY = height * .40f;
            if (player != null && player.HeldEquipment == PrototypeHeldEquipment.AntiAirLauncher)
                numberY = Mathf.Max(numberY, height * .5f + 150f);
            // Leave the bottom weapon cards clear even on the minimum canvas.
            numberY = Mathf.Min(numberY, height - 176f);
            Color old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            int count = icons.Count == 0 ? 1 : icons.Count;
            float centerX = CenterX(player, width);
            float iconLeft = centerX - count * 17f;
            for (int i = 0; i < count; i++)
            {
                var kind = icons.Count == 0 ? rows[0].Kind : icons[i];
                GUI.DrawTexture(new Rect(iconLeft + i * 34f, numberY - 29f, 30f, 30f),
                    PrototypeScoreIcons.For(kind), ScaleMode.ScaleToFit, true);
            }
            numberStyle.fontSize = 34 + Mathf.RoundToInt(3f * Mathf.Exp(-age * 8f));
            ShadowLabel(new Rect(centerX - 145f, numberY, 290f, 43f),
                Mathf.RoundToInt(animatedTotal).ToString(), numberStyle, alpha);
            for (int i = 0; i < rows.Count; i++)
                ShadowLabel(new Rect(centerX - 155f, numberY + 45f + i * 18f, 310f, 20f),
                    Describe(rows[i]), rowStyle, alpha);
            GUI.color = old;
        }

        private static string Describe(Row row)
        {
            string label;
            switch (row.Kind)
            {
                case PrototypeScoreNoticeKind.Damage: label = row.Victim is IPrototypeVehicle ? "载具伤害" : "命中伤害"; break;
                case PrototypeScoreNoticeKind.Kill: label = "击倒"; break;
                case PrototypeScoreNoticeKind.Assist: label = "助攻击倒"; break;
                case PrototypeScoreNoticeKind.VehicleDestroyed: label = "摧毁载具"; break;
                case PrototypeScoreNoticeKind.VehicleAssist: label = "载具助攻"; break;
                default: label = row.Reason; break;
            }
            string result = label + " +" + row.Points;
            if ((row.Kind == PrototypeScoreNoticeKind.Kill || row.Kind == PrototypeScoreNoticeKind.VehicleDestroyed) &&
                row.SettlementPoints > row.Points)
                result += " · 合计 " + row.SettlementPoints;
            return result;
        }

        private static void ShadowLabel(Rect rect, string text, GUIStyle style, float alpha)
        {
            GUI.color = new Color(0f, 0f, 0f, alpha * .82f);
            rect.position += new Vector2(1.5f, 1.5f); GUI.Label(rect, text, style);
            rect.position -= new Vector2(1.5f, 1.5f);
            GUI.color = new Color(1f, 1f, 1f, alpha); GUI.Label(rect, text, style);
        }

        public void Dispose()
        {
            if (scores != null) scores.Awarded -= OnAwarded;
            Reset();
        }
    }
}
