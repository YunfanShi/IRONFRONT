using UnityEngine;

namespace Ironfront.UnityPrototype
{
    public static class PrototypePlayerHealthHud
    {
        public struct VehicleReading
        {
            public string VehicleName;
            public float Health, MaximumHealth, Fraction;
            public int ArmorRating;
        }

        public static Rect Panel(float width, float height) =>
            new Rect(width * .375f, height - 103f, width * .25f, 62f);

        public static Rect VehiclePanel(float width, float height) =>
            new Rect(width * .375f, height - 165f, width * .25f, 54f);

        public static Rect VehicleBar(float width, float height, float health, float maximumHealth)
        {
            Rect panel = VehiclePanel(width, height);
            float fraction = !Finite(health) || !Finite(maximumHealth) || maximumHealth <= 0f ?
                0f : Mathf.Clamp01(health / maximumHealth);
            return new Rect(panel.x + 10f, panel.y + 34f, (panel.width - 20f) * fraction, 10f);
        }

        public static bool TryReadVehicle(PrototypePlayer player, out VehicleReading reading)
        {
            reading = default;
            if (player == null || !player.Alive || !(player.CurrentVehicle is IPrototypeArmoredVehicle vehicle)) return false;
            if (vehicle is Object unityVehicle && unityVehicle == null) return false;
            if (!vehicle.Alive || vehicle.VehicleTransform == null || !vehicle.VehicleTransform.gameObject.activeInHierarchy ||
                !Finite(vehicle.Health) || !Finite(vehicle.MaximumHealth) || vehicle.MaximumHealth <= 0f) return false;
            if (vehicle is IPrototypeSeatedVehicle seats && (seats.PlayerSeat < 0 ||
                seats.PlayerSeat >= seats.SeatCount || !ReferenceEquals(seats.GetSeatOccupant(seats.PlayerSeat), player))) return false;
            reading.VehicleName = vehicle.VehicleName;
            reading.Health = Mathf.Clamp(vehicle.Health, 0f, vehicle.MaximumHealth);
            reading.MaximumHealth = vehicle.MaximumHealth;
            reading.Fraction = reading.Health / reading.MaximumHealth;
            reading.ArmorRating = Mathf.Max(0, vehicle.ArmorRating);
            return true;
        }

        public static float Fraction(float health) => float.IsNaN(health) || float.IsInfinity(health) ?
            0f : Mathf.Clamp01(health / 100f);

        public static Rect Bar(float width, float height, float health = 100f)
        {
            Rect panel = Panel(width, height);
            return new Rect(panel.x + 10f, panel.y + 42f,
                (panel.width - 20f) * Fraction(health), 12f);
        }

        // The six engineer cards use two rows at the right edge, leaving
        // the center quarter of the screen free for the player's health.
        public static Rect EquipmentCard(float width, float height, int index) =>
            new Rect(width - 195f + (index % 3) * 62f,
                height - 145f + (index / 3) * 65f, 57f, 59f);

        public static void Draw(PrototypePlayer player, float width, float height)
        {
            if (player == null || !player.Alive) return;
            Rect panel = Panel(width, height);
            Fill(panel, new Color(.018f, .044f, .050f, .90f));
            Color accent = Fraction(player.Health) <= .25f ? new Color(1f, .34f, .28f) :
                Fraction(player.Health) <= .45f ? new Color(1f, .74f, .34f) :
                new Color(.76f, .94f, .90f);
            var name = new GUIStyle(GUI.skin.label) { fontSize = 12,
                alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Bold };
            name.normal.textColor = new Color(.74f, .84f, .83f);
            var number = new GUIStyle(GUI.skin.label) { fontSize = 28,
                alignment = TextAnchor.MiddleRight, fontStyle = FontStyle.Bold };
            number.normal.textColor = Color.white;
            GUI.Label(new Rect(panel.x + 10f, panel.y + 5f, 80f, 30f), "生命 / HP", name);
            GUI.Label(new Rect(panel.x + 90f, panel.y + 3f, panel.width - 100f, 34f),
                Mathf.CeilToInt(Fraction(player.Health) * 100f).ToString(), number);
            Fill(Bar(width, height), new Color(.16f, .23f, .25f));
            Fill(Bar(width, height, player.Health), accent);
            if (TryReadVehicle(player, out VehicleReading vehicle))
                DrawVehicle(vehicle, width, height);
        }

        private static void DrawVehicle(VehicleReading vehicle, float width, float height)
        {
            Rect panel = VehiclePanel(width, height);
            Fill(panel, new Color(.018f, .044f, .060f, .94f));
            var caption = new GUIStyle(GUI.skin.label) { fontSize = 12,
                alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Bold };
            caption.normal.textColor = new Color(.65f, .83f, .96f);
            var value = new GUIStyle(caption) { alignment = TextAnchor.MiddleRight, fontSize = 18 };
            value.normal.textColor = Color.white;
            GUI.Label(new Rect(panel.x + 10f, panel.y + 3f, panel.width - 70f, 27f),
                "载具护甲 " + vehicle.ArmorRating, caption);
            GUI.Label(new Rect(panel.x + panel.width - 65f, panel.y + 3f, 55f, 27f),
                Mathf.CeilToInt(vehicle.Fraction * 100f) + "%", value);
            Fill(VehicleBar(width, height, vehicle.MaximumHealth, vehicle.MaximumHealth), new Color(.13f, .22f, .29f));
            Color armorColor = vehicle.Fraction <= .25f ? new Color(1f, .37f, .28f) :
                vehicle.Fraction <= .45f ? new Color(1f, .75f, .35f) : new Color(.40f, .77f, 1f);
            Fill(VehicleBar(width, height, vehicle.Health, vehicle.MaximumHealth), armorColor);
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static void Fill(Rect rectangle, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color; GUI.DrawTexture(rectangle, Texture2D.whiteTexture); GUI.color = previous;
        }
    }
}
