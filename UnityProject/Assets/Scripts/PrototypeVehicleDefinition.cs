namespace Ironfront.UnityPrototype
{
    // Append new values: LAN snapshots and existing saved/editor references use
    // the original five integer IDs.
    public enum PrototypeVehicleKind { Ifv = 0, AntiAir = 1, Helicopter = 2, Jet = 3, Motorcycle = 4, ReconHelicopter = 5 }

    // Gameplay numbers from main/src/vehicles/Vehicle.ts and Flight.ts. All five
    // additions share lifecycle and seats, but keep their own movement and ammo.
    public sealed class PrototypeVehicleDefinition
    {
        public readonly PrototypeVehicleKind Kind;
        public readonly string Name;
        public readonly float Health, Radius, Forward, Reverse, Acceleration,
            Braking, TurnRate, Damage, Interval, Range, RespawnDelay,
            ProjectileSpeed, Gravity, BlastRadius, Armor;
        public readonly int Seats;
        public int ArmorRating => Kind == PrototypeVehicleKind.Ifv || Kind == PrototypeVehicleKind.AntiAir ? 4 :
            Kind == PrototypeVehicleKind.Helicopter ? 3 : Kind == PrototypeVehicleKind.Motorcycle ? 1 : 2;
        public bool Aircraft => Kind == PrototypeVehicleKind.Helicopter ||
            Kind == PrototypeVehicleKind.ReconHelicopter || Kind == PrototypeVehicleKind.Jet;
        public bool Armed => Kind != PrototypeVehicleKind.Motorcycle;
        public bool Guided => Kind == PrototypeVehicleKind.AntiAir || Kind == PrototypeVehicleKind.Jet;

        private PrototypeVehicleDefinition(PrototypeVehicleKind kind, string name,
            float health, float radius, float forward, float reverse, float acceleration,
            float braking, float turn, float damage, float interval, float range,
            float respawn, int seats, float speed, float gravity, float blast, float armor)
        {
            Kind = kind; Name = name; Health = health; Radius = radius;
            Forward = forward; Reverse = reverse; Acceleration = acceleration;
            Braking = braking; TurnRate = turn; Damage = damage; Interval = interval;
            Range = range; RespawnDelay = respawn; Seats = seats;
            ProjectileSpeed = speed; Gravity = gravity; BlastRadius = blast; Armor = armor;
        }

        public static PrototypeVehicleDefinition Get(PrototypeVehicleKind kind)
        {
            switch (kind)
            {
                case PrototypeVehicleKind.Ifv: return new PrototypeVehicleDefinition(kind,
                    "V12 LANCER", 420f, 3.15f, 13.2f, 5.8f, 8.4f, 10.5f, 1.08f,
                    74f, 1.15f, 175f, 90f, 4, 180f, 4f, 4f, .7f);
                case PrototypeVehicleKind.AntiAir: return new PrototypeVehicleDefinition(kind,
                    "A40 WATCHTOWER", 320f, 3.15f, 16f, 5.8f, 8.4f, 10.5f, 1.08f,
                    30f, .24f, 360f, 90f, 4, 280f, 1.5f, 2.2f, .45f);
                case PrototypeVehicleKind.Helicopter: return new PrototypeVehicleDefinition(kind,
                    "H8 KESTREL ATTACK", 260f, 3f, 38f, 5.8f, 8.4f, 10.5f, 1.4f,
                    45f, .45f, 250f, 90f, 4, 115f, 3f, 6f, .85f);
                case PrototypeVehicleKind.Jet: return new PrototypeVehicleDefinition(kind,
                    "J20 STRATUS", 220f, 4f, 65f, 5.8f, 8.4f, 10.5f, .8f,
                    32f, .25f, 400f, 90f, 1, 330f, 1f, 2f, .65f);
                case PrototypeVehicleKind.ReconHelicopter: return new PrototypeVehicleDefinition(kind,
                    "R3 FIREFLY RECON", 190f, 2.7f, 43f, 8f, 12f, 12f, 1.65f,
                    20f, .12f, 150f, 75f, 3, 700f, 0f, 0f, .12f);
                default: return new PrototypeVehicleDefinition(kind,
                    "M2 COURIER", 85f, .85f, 33f, 5f, 19f, 18f, 1.05f,
                    0f, 1.15f, 0f, 70f, 2, 0f, 0f, 0f, 0f);
            }
        }
    }
}
