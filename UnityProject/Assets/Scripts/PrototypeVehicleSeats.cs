using UnityEngine;

namespace Ironfront.UnityPrototype
{
    public enum PrototypeVehicleSeatRole { Driver, Pilot, Gunner, Passenger }

    public interface IPrototypeAircraftVehicle : IPrototypeVehicle
    {
        bool IsAircraft { get; }
        float Altitude { get; }
    }

    public static class PrototypeVehicleSpace
    {
        public static float Radius(IPrototypeVehicle vehicle) =>
            vehicle is PrototypeTankVehicle ? 3.5f :
            vehicle is PrototypeScoutVehicle ? 2.1f :
            vehicle is PrototypeTransportVehicle ? 1.85f :
            vehicle is PrototypeCombinedArmsVehicle combined ? combined.Clearance : 2.1f;

        public static bool BlockedByAnother(PrototypeRuntime game, IPrototypeVehicle owner,
            Vector2 point, float radius)
        {
            foreach (IPrototypeVehicle other in game.Vehicles)
            {
                if (ReferenceEquals(other, owner) || !other.Alive) continue;
                if (other is IPrototypeAircraftVehicle aircraft && aircraft.IsAircraft &&
                    aircraft.Altitude > 3f) continue;
                if (Vector2.Distance(point, other.MapPosition) < radius + Radius(other)) return true;
            }
            return false;
        }
    }

    public interface IPrototypeSeatedVehicle : IPrototypeVehicle
    {
        int SeatCount { get; }
        int PlayerSeat { get; }
        PrototypeVehicleSeatRole PlayerSeatRole { get; }
        bool SeatCanDrive { get; }
        bool SeatCanFire { get; }
        MonoBehaviour GetSeatOccupant(int seat);
        string GetSeatName(int seat);
        Vector3 GetSeatPosition(int seat);
        Vector3 GetSeatCameraPosition(int seat);
        bool TrySetPlayerSeat(PrototypePlayer player, int seat);
    }

    // A local seat ledger shared by all eight vehicle kinds. A failed transfer
    // never removes the old seat, and human/AI occupants share the same slots.
    public sealed class PrototypeVehicleSeats
    {
        private readonly IPrototypeVehicle owner;
        private readonly PrototypeRuntime runtime;
        private readonly MonoBehaviour[] occupants;
        private readonly bool driverCanFire;
        private readonly bool gunnerCanFire;
        private readonly bool pilot;

        public PrototypeVehicleSeats(IPrototypeVehicle vehicle, PrototypeRuntime game,
            int count, bool armedDriver, bool armedGunner = true, bool pilotSeat = false)
        {
            owner = vehicle;
            runtime = game;
            occupants = new MonoBehaviour[Mathf.Clamp(count, 1, 4)];
            driverCanFire = armedDriver;
            gunnerCanFire = armedGunner;
            pilot = pilotSeat;
        }

        public int SeatCount => occupants.Length;
        public int PlayerSeat { get; private set; } = -1;
        public PrototypePlayer Player => PlayerSeat >= 0 ?
            occupants[PlayerSeat] as PrototypePlayer : null;
        public PrototypePlayer Driver => occupants[0] as PrototypePlayer;
        public PrototypeVehicleSeatRole PlayerRole => GetRole(PlayerSeat);
        public bool CanDrive => Player != null && PlayerSeat == 0;
        public bool CanFire => Player != null &&
            (PlayerSeat == 0 ? driverCanFire : PlayerSeat == 1 && gunnerCanFire);

        public MonoBehaviour GetOccupant(int seat) =>
            Valid(seat) ? occupants[seat] : null;

        public PrototypeVehicleSeatRole GetRole(int seat) => seat == 0 ?
            (pilot ? PrototypeVehicleSeatRole.Pilot : PrototypeVehicleSeatRole.Driver) :
            seat == 1 && gunnerCanFire ? PrototypeVehicleSeatRole.Gunner :
            PrototypeVehicleSeatRole.Passenger;

        public string GetName(int seat)
        {
            if (!Valid(seat)) return "NO SEAT";
            if (seat == 0) return pilot ? "PILOT" : "DRIVER";
            if (seat == 1 && gunnerCanFire) return "GUNNER";
            return "PASSENGER " + (gunnerCanFire ? seat - 1 : seat);
        }

        public bool TrySetPlayerSeat(PrototypePlayer player, int seat)
        {
            if (!Valid(seat) || runtime == null || runtime.IsNetworkReplica ||
                !runtime.MatchStarted || runtime.Match.Winner.HasValue ||
                !owner.Alive || player == null || !player.Alive ||
                player != runtime.Player || player.Team != owner.Team ||
                (player.CurrentVehicle != null &&
                    !ReferenceEquals(player.CurrentVehicle, owner)) ||
                (Player != null && Player != player) ||
                (occupants[seat] != null && occupants[seat] != player)) return false;
            if (PlayerSeat == seat) return true;
            // Destination has been validated before the source is released.
            Release(player);
            occupants[seat] = player;
            PlayerSeat = seat;
            return true;
        }

        public void RemovePlayer(PrototypePlayer player)
        {
            if (player != null) Release(player);
        }

        public bool TryReserve(int seat, MonoBehaviour occupant)
        {
            if (!Valid(seat) || occupant == null || !owner.Alive ||
                occupants[seat] != null) return false;
            foreach (MonoBehaviour existing in occupants)
                if (existing == occupant) return false;
            occupants[seat] = occupant;
            return true;
        }

        public void Release(MonoBehaviour occupant)
        {
            for (int seat = 0; seat < occupants.Length; seat++)
                if (occupants[seat] == occupant)
                {
                    occupants[seat] = null;
                    if (PlayerSeat == seat) PlayerSeat = -1;
                }
        }

        public void Clear()
        {
            for (int seat = 0; seat < occupants.Length; seat++) occupants[seat] = null;
            PlayerSeat = -1;
        }

        private bool Valid(int seat) => seat >= 0 && seat < occupants.Length;
    }
}
