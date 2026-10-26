#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Ironfront.UnityPrototype
{
    // Opt-in integration capture; inactive during ordinary play.
    public sealed class PrototypeEquipmentArtProbe : MonoBehaviour
    {
        private string output;
        private int errors;
        private readonly List<string> notes = new List<string>();
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
                if (args[i] == "-ironfrontEquipmentArtProbe")
                    new GameObject("Equipment integration capture").AddComponent<PrototypeEquipmentArtProbe>().output = Path.GetFullPath(args[i + 1]);
        }

        private IEnumerator Start()
        {
            Directory.CreateDirectory(output); Application.runInBackground = true;
            Application.logMessageReceived += Observe;
            IEnumerator work = Verify(); Exception failure = null;
            while (true)
            {
                bool more = false; object current = null;
                try { more = work.MoveNext(); if (more) current = work.Current; }
                catch (Exception e) { failure = e; }
                if (!more || failure != null) break;
                yield return current;
            }
            bool pass = failure == null && errors == 0;
            File.WriteAllText(Path.Combine(output, "results.txt"), (pass ? "PASS" : "FAIL") +
                "\nVersion=" + Application.version + "\nRuntimeErrors=" + errors + "\n" + string.Join("\n", notes) +
                (failure == null ? "" : "\n" + failure));
            Application.logMessageReceived -= Observe;
            Debug.Log("EQUIPMENT_RUNTIME_" + (pass ? "PASS" : "FAIL"));
            Application.Quit(pass ? 0 : 1);
        }

        private IEnumerator Verify()
        {
            PrototypeRuntime game = null;
            for (int i = 0; i < 600; i++)
            { game = FindAnyObjectByType<PrototypeRuntime>(); if (game != null && game.Player != null) break; yield return null; }
            Check(game != null && game.Player != null, "Runtime starts");
            game.BeginBattle(); game.Frontend.Close(); game.SetSupportMenu(false);
            foreach (var bot in game.Bots) { bot.enabled = false; bot.gameObject.SetActive(false); }
            foreach (var vehicle in game.Vehicles.OfType<MonoBehaviour>()) vehicle.gameObject.SetActive(false);
            var player = game.Player; player.enabled = false;
            Vector3 position = new Vector3(-325f, PrototypeLayout.HeightAt(-325f, -320f) + .3f, -320f);
            player.transform.SetPositionAndRotation(position, Quaternion.identity);
            var camera = player.ViewCamera; camera.enabled = false;

            using (var preview = new PrototypeMenuPreview(game))
            {
                foreach (PrototypeInfantryClass role in Enum.GetValues(typeof(PrototypeInfantryClass)))
                {
                    preview.SetOperator(role, PrototypeInfantryRoles.DefaultWeapon(role));
                    string key = PrototypeEquipmentArt.ForRole(role);
                    var gear = preview.Operator.GetComponentsInChildren<PrototypeEquipmentArt>().Single(a => a.AssetKey == key);
                    CheckArt(gear, key);
                    File.WriteAllBytes(Path.Combine(output, "card-" + key + ".png"), preview.GadgetThumbnail(role).EncodeToPNG());
                    preview.Render(); SaveTexture((RenderTexture)preview.Texture, "operator-" + key + "-front");
                    preview.Operator.transform.Rotate(0f, 180f, 0f);
                    preview.Render(); SaveTexture((RenderTexture)preview.Texture, "operator-" + key + "-back");
                }
                foreach (PrototypeThrowableId kind in new[] { PrototypeThrowableId.Frag, PrototypeThrowableId.Smoke })
                    File.WriteAllBytes(Path.Combine(output, "card-" + kind + ".png"), preview.ThrowableThumbnail(kind).EncodeToPNG());
            }

            foreach (PrototypeThrowableId kind in new[] { PrototypeThrowableId.Frag, PrototypeThrowableId.Smoke })
            {
                player.ApplyLoadout(new PrototypeLoadout { role = PrototypeInfantryClass.Assault,
                    primary = PrototypeWeaponId.Carbine, secondary = PrototypeWeaponId.Pistol, throwable = kind });
                if (!player.IsFirstPerson) player.TogglePerspective();
                camera.transform.SetPositionAndRotation(position + Vector3.up * 1.62f, Quaternion.identity);
                Check(player.TryEquipThrowable(), "Equip " + kind);
                player.RefreshHeldPresentation();
                CheckArt(player.HeldEquipmentVisual.GetComponent<PrototypeEquipmentArt>(), kind == PrototypeThrowableId.Frag ? "FragGrenade" : "SmokeGrenade");
                yield return null;
                Capture(camera, "held-" + kind);
                int count = player.ThrowableCount;
                typeof(PrototypePlayer).GetField("nextEquipmentUseAt", Hidden).SetValue(player, 0f);
                Check(player.TryUseHeldEquipment() && player.ThrowableCount == count - 1, "Throw consumes one " + kind);
                var projectile = GameObject.Find(kind == PrototypeThrowableId.Smoke ? "Smoke grenade" : "Fragmentation grenade");
                Check(projectile != null && projectile.GetComponent<Rigidbody>() != null && projectile.GetComponent<SphereCollider>() != null &&
                    projectile.GetComponentInChildren<PrototypeEquipmentArt>() != null, "Imported projectile keeps physics " + kind);
            }
            typeof(PrototypePlayer).GetMethod("PlaceBeacon", Hidden).Invoke(player, new object[] { new Vector2(-323f, -320f) });
            var beacon = (GameObject)typeof(PrototypePlayer).GetField("beaconMarker", Hidden).GetValue(player);
            CheckArt(beacon.GetComponent<PrototypeEquipmentArt>(), "Beacon");
            Check(beacon.transform.position.y > PrototypeWorldVisual.InfantrySurfaceAt(-323f, -320f), "Beacon sits above rendered base pad");
            CaptureObject(camera, beacon, "deployed-beacon");
            Physics.SyncTransforms();
            Check(game.Supplies.TryDeployCrate(player, out string reason), "Deploy supply: " + reason);
            var supply = game.Supplies.Points.Last(p => p.IsDeployable);
            CheckArt(supply.GetComponentInChildren<PrototypeEquipmentArt>(), "SupplyBag");
            Check(supply.transform.position.y > PrototypeWorldVisual.InfantrySurfaceAt(supply.transform.position.x, supply.transform.position.z),
                "Supply sits above rendered base pad");
            Check(supply.GetComponent<BoxCollider>().size.x < .8f && supply.Health == PrototypeAmmoSupplyPoint.MaxHealth,
                "Supply collision fits new bag; health retained");
            CaptureObject(camera, supply.gameObject, "deployed-supply");
            yield return null;
        }

        private void CheckArt(PrototypeEquipmentArt art, string key)
        {
            Check(art != null && art.AssetKey == key, key + " uses imported prefab");
            Check(art.GetComponent<LODGroup>().GetLODs().Length == 3, key + " has three LODs");
            Material material = art.GetComponentInChildren<Renderer>().sharedMaterial;
            Check(material.GetTexture("_BaseMap") != null && material.GetTexture("_BumpMap") != null && material.GetTexture("_MetallicGlossMap") != null,
                key + " retains three PBR textures");
        }
        private void CaptureObject(Camera camera, GameObject obj, string name)
        {
            camera.transform.position = obj.transform.position + new Vector3(.8f, .65f, 1.05f);
            camera.transform.LookAt(obj.transform.position + Vector3.up * .2f); Capture(camera, name);
        }
        private void Capture(Camera camera, string name)
        {
            var target = new RenderTexture(1280, 800, 24); target.Create();
            var previous = camera.targetTexture;
            try { camera.targetTexture = target; camera.aspect = 1.6f; camera.Render(); SaveTexture(target, name); }
            finally { camera.targetTexture = previous; target.Release(); Destroy(target); }
        }
        private void SaveTexture(RenderTexture target, string name)
        {
            var previous = RenderTexture.active; var image = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            try { RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply();
                File.WriteAllBytes(Path.Combine(output, name + ".png"), image.EncodeToPNG()); }
            finally { RenderTexture.active = previous; Destroy(image); }
        }
        private void Check(bool success, string message) { if (!success) throw new InvalidOperationException(message); notes.Add(message); }
        private void Observe(string message, string trace, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; }
    }
}
#endif
