using System.Collections.Generic;
using UnityEngine;

namespace Ironfront.UnityPrototype
{
    public sealed class PrototypeReconRadar : MonoBehaviour
    {
        public const float VehicleRange = 180f, InfantryRange = 120f, MarkDuration = 6f, Cooldown = 12f;
        public readonly struct Contact
        {
            public readonly Transform Target;
            public readonly Vector3 Position;
            public readonly string Label;
            public readonly bool Vehicle;
            public Contact(Transform target, Vector3 position, string label, bool vehicle)
            { Target = target; Position = position; Label = label; Vehicle = vehicle; }
        }
        private PrototypeRuntime runtime;
        private PrototypeCombinedArmsVehicle owner;
        private readonly List<Contact> contacts = new List<Contact>();
        private float expires, nextScan;
        public float ReadyIn => Mathf.Max(0f, nextScan - Time.time);
        public float MarkTimeRemaining => Mathf.Max(0f, expires - Time.time);
        public IReadOnlyList<Contact> Contacts => contacts;
        public void Initialize(PrototypeRuntime game, PrototypeCombinedArmsVehicle vehicle)
        { runtime = game; owner = vehicle; Clear(); }

        public bool TryScan()
        {
            if (runtime == null || runtime.IsNetworkReplica || !runtime.MatchStarted ||
                runtime.Match.Winner.HasValue || Time.timeScale <= 0f || owner == null ||
                owner.Kind != PrototypeVehicleKind.ReconHelicopter || !owner.SeatCanDrive ||
                !owner.Alive || Time.time < nextScan) return false;
            contacts.Clear();
            Vector3 origin = owner.transform.position + Vector3.up * 1.8f;
            foreach (IPrototypeVehicle vehicle in runtime.Vehicles)
            {
                if (ReferenceEquals(owner, vehicle) || !vehicle.Alive || vehicle.Team == owner.Team) continue;
                Vector3 position = vehicle.VehicleTransform.position + Vector3.up * 1.8f;
                if (!VehicleInRange(origin, position)) continue;
                contacts.Add(new Contact(vehicle.VehicleTransform, position, vehicle.VehicleName, true));
            }
            foreach (PrototypeBot bot in runtime.Bots)
            {
                if (!bot.Alive || bot.IsPassenger || bot.Team == owner.Team) continue;
                AddInfantry(origin, bot.transform, bot.transform.position + Vector3.up * 1.1f, bot.DisplayName);
            }
            if (runtime.Player != null && runtime.Player.Alive && runtime.Player.Team != owner.Team &&
                runtime.Player.CurrentVehicle == null)
                AddInfantry(origin, runtime.Player.transform, runtime.Player.transform.position + Vector3.up * 1.1f, "ENEMY PLAYER");
            foreach (PrototypeRemotePlayer remote in runtime.RemotePlayers)
                if (remote != null && remote.Alive && remote.Team != owner.Team)
                    AddInfantry(origin, remote.transform, remote.transform.position + Vector3.up * 1.1f, "ENEMY PLAYER");
            expires = Time.time + MarkDuration; nextScan = Time.time + Cooldown;
            return true;
        }
        public static bool VehicleInRange(Vector3 origin, Vector3 position) =>
            (position - origin).sqrMagnitude <= VehicleRange * VehicleRange;
        public static bool InfantryBelow(Vector3 origin, Vector3 position)
        {
            Vector3 delta = position - origin;
            // The optical downward cone cannot highlight someone beside a
            // landed helicopter or on a rooftop above it.
            return delta.y < -5f && delta.sqrMagnitude <= InfantryRange * InfantryRange &&
                Vector3.Dot(delta.normalized, Vector3.down) >= .42f;
        }
        private void AddInfantry(Vector3 origin, Transform target, Vector3 position, string label)
        {
            if (!InfantryBelow(origin, position) || PrototypeSmokeCloud.SegmentBlocked(origin, position)) return;
            Physics.SyncTransforms();
            Vector3 delta = position - origin;
            foreach (RaycastHit hit in Physics.RaycastAll(origin, delta.normalized, delta.magnitude,
                ~(1 << 2), QueryTriggerInteraction.Ignore))
                if (!hit.collider.transform.IsChildOf(owner.transform) && !hit.collider.transform.IsChildOf(target)) return;
            // Snapshot positions deliberately do not follow a hidden target
            // throughout the six seconds. A new pulse is needed to update it.
            contacts.Add(new Contact(target, position, label, false));
        }
        private void Update()
        {
            if (runtime == null || !runtime.MatchStarted || runtime.Match.Winner.HasValue ||
                owner == null || !owner.Alive || Time.time >= expires) contacts.Clear();
        }
        public void Clear() { contacts.Clear(); expires = nextScan = 0f; }
        private void OnDisable() => Clear();

        public void DrawHud(Camera camera, float width, float height)
        {
            if (camera == null || owner == null || owner.Occupant == null || runtime == null ||
                runtime.Player == null || !ReferenceEquals(runtime.Player.CurrentVehicle, owner)) return;
            var status = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter };
            status.normal.textColor = new Color(.5f, .92f, .85f);
            GUI.Label(new Rect(width * .5f - 175f, height - 114f, 350f, 25f),
                ReadyIn > 0f ? "RECON RADAR  " + Mathf.CeilToInt(ReadyIn) + "s  /  " +
                    contacts.Count + " CONTACTS" : "Q  RADAR SCAN  /  180m VEHICLES + INFANTRY BELOW", status);
            if (Time.time >= expires) return;
            var label = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperCenter };
            Color prior = GUI.color;
            foreach (Contact contact in contacts)
            {
                if (contact.Target == null || !contact.Target.gameObject.activeInHierarchy) continue;
                Vector3 view = camera.WorldToViewportPoint(contact.Position);
                if (view.z <= 0f || view.x < 0f || view.x > 1f || view.y < 0f || view.y > 1f) continue;
                float x = view.x * width, y = (1f - view.y) * height;
                Color color = contact.Vehicle ? new Color(1f, .68f, .22f) : new Color(1f, .3f, .25f);
                color.a = Mathf.Clamp01(MarkTimeRemaining / .6f);
                GUI.color = color;
                float size = contact.Vehicle ? 26f : 18f;
                Rect box = new Rect(x-size*.5f, y-size*.5f, size, size);
                GUI.DrawTexture(new Rect(box.x,box.y,box.width,2f), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(box.x,box.yMax-2f,box.width,2f), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(box.x,box.y,2f,box.height), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(box.xMax-2f,box.y,2f,box.height), Texture2D.whiteTexture);
                label.normal.textColor = color;
                GUI.color = prior;
                GUI.Label(new Rect(x-100f,y+size*.5f+3f,200f,35f), contact.Label + "\n" +
                    Mathf.RoundToInt(Vector3.Distance(owner.transform.position,contact.Position)) + "m", label);
            }
            GUI.color = prior;
        }
    }
}
