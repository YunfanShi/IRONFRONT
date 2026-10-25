using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Ironfront.UnityPrototype.Editor
{
    // Exercise the real import, save, player slot and deployment entry points.
    // The independent player fixture leaves the user's inventory untouched.
    public static class PrototypeLoadoutPolicySmoke
    {
        private const string SaveKey = "ironfront.unity.loadout";
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly PrototypeWeaponId[][] Matrix =
        {
            new[] { PrototypeWeaponId.Carbine, PrototypeWeaponId.BattleRifle },
            new[] { PrototypeWeaponId.Smg, PrototypeWeaponId.Carbine },
            new[] { PrototypeWeaponId.Marksman, PrototypeWeaponId.Sniper },
            new[] { PrototypeWeaponId.BattleRifle, PrototypeWeaponId.Lmg }
        };

        [MenuItem("IRONFRONT/Verify Class Loadout Policy", true)]
        private static bool CanVerify() => EditorApplication.isPlaying;

        [MenuItem("IRONFRONT/Verify Class Loadout Policy")]
        public static void Verify()
        {
            bool hadSavedLoadout = PlayerPrefs.HasKey(SaveKey);
            string previousJson = PlayerPrefs.GetString(SaveKey, "");
            PrototypeRuntime game = null;
            PrototypeScoreSystem previousScores = null;
            bool scoresReplaced = false;
            GameObject fixture = null;
            try
            {
                VerifyPolicyMatrix();
                VerifySavedLoadouts();
                game = UnityEngine.Object.FindAnyObjectByType<PrototypeRuntime>();
                Check(game != null && game.Player != null && game.MatchStarted &&
                    !game.IsNetworkReplica && !game.Match.Winner.HasValue,
                    "a live authoritative match is available");
                previousScores = game.Scores;
                Set(game, "<Scores>k__BackingField", new PrototypeScoreSystem(game));
                scoresReplaced = true;
                fixture = new GameObject("Class loadout policy player fixture");
                fixture.transform.SetParent(game.transform, false);
                fixture.AddComponent<CharacterController>();
                PrototypePlayer player = fixture.AddComponent<PrototypePlayer>();
                player.Initialize(game);
                player.enabled = false;
                foreach (Camera camera in fixture.GetComponentsInChildren<Camera>()) camera.enabled = false;
                foreach (AudioListener listener in fixture.GetComponentsInChildren<AudioListener>()) listener.enabled = false;

                VerifyPlayerSlots(player);
                VerifyRoleChanges(player);
                VerifyDeployment(game, player);
                VerifyNetworkRequestSchema();
                Debug.Log("IRONFRONT loadout policy PASS: all four class weapon matrices, legacy and malformed " +
                    "saved loadouts, real player primary/pistol inventories and visuals, class changes, " +
                    "deployment validation, replica rejection and existing LAN request boundary");
            }
            catch (Exception exception)
            {
                Debug.LogError("IRONFRONT loadout policy FAIL: " + exception.GetBaseException().Message);
            }
            finally
            {
                if (fixture != null) UnityEngine.Object.DestroyImmediate(fixture);
                if (game != null && scoresReplaced)
                    Set(game, "<Scores>k__BackingField", previousScores);
                if (hadSavedLoadout) PlayerPrefs.SetString(SaveKey, previousJson);
                else PlayerPrefs.DeleteKey(SaveKey);
                PlayerPrefs.Save();
            }
        }

        private static void VerifyPolicyMatrix()
        {
            foreach (PrototypeInfantryClass role in Enum.GetValues(typeof(PrototypeInfantryClass)))
            {
                IReadOnlyList<PrototypeWeaponId> allowed = PrototypeInfantryRoles.AllowedPrimaryWeapons(role);
                Check(allowed.Count == 2 && allowed[0] == Matrix[(int)role][0] &&
                    allowed[1] == Matrix[(int)role][1], role + " exposes exactly its two primary choices");
                Check(allowed is ICollection<PrototypeWeaponId> collection && collection.IsReadOnly,
                    "callers cannot edit the shared class matrix");
                Check(PrototypeInfantryRoles.IsPrimaryWeaponAllowed(role, PrototypeInfantryRoles.DefaultWeapon(role)),
                    "each existing class default remains permitted");
                foreach (PrototypeWeaponId weapon in Enum.GetValues(typeof(PrototypeWeaponId)))
                {
                    bool permitted = weapon == Matrix[(int)role][0] || weapon == Matrix[(int)role][1];
                    Check(PrototypeInfantryRoles.IsPrimaryWeaponAllowed(role, weapon) == permitted,
                        role + " class permission for " + weapon);
                    var selection = new PrototypeLoadout { role = role, primary = weapon,
                        secondary = PrototypeWeaponId.Marksman, throwable = PrototypeThrowableId.Smoke };
                    selection.Validate();
                    Check(selection.primary == (permitted ? weapon : PrototypeInfantryRoles.DefaultWeapon(role)) &&
                        selection.secondary == PrototypeWeaponId.Pistol && selection.throwable == PrototypeThrowableId.Smoke,
                        "validation repairs forbidden weapons without changing a permitted throwable");
                }
            }
            Check(!PrototypeInfantryRoles.IsPrimaryWeaponAllowed((PrototypeInfantryClass)999, PrototypeWeaponId.Carbine) &&
                !PrototypeInfantryRoles.IsPrimaryWeaponAllowed(PrototypeInfantryClass.Medic, (PrototypeWeaponId)(-1)) &&
                PrototypeInfantryRoles.AllowedPrimaryWeapons((PrototypeInfantryClass)(-1)).Count == 0,
                "invalid enums do not acquire weapon permissions");
            foreach (PrototypeWeaponId weapon in Enum.GetValues(typeof(PrototypeWeaponId)))
                Check(PrototypeInfantryRoles.IsSecondaryWeaponAllowed(weapon) == (weapon == PrototypeWeaponId.Pistol),
                    "only a pistol may occupy the secondary slot");
            var invalid = new PrototypeLoadout { role = (PrototypeInfantryClass)999,
                primary = (PrototypeWeaponId)999, secondary = (PrototypeWeaponId)(-1),
                throwable = (PrototypeThrowableId)999 };
            invalid.Validate();
            Check(invalid.role == PrototypeInfantryClass.Assault && invalid.primary == PrototypeWeaponId.Carbine &&
                invalid.secondary == PrototypeWeaponId.Pistol && invalid.throwable == PrototypeThrowableId.Frag,
                "every malformed slot is repaired before creating an inventory");
        }

        private static void VerifySavedLoadouts()
        {
            PlayerPrefs.SetString(SaveKey, "{\"role\":1,\"primary\":5,\"secondary\":1,\"throwable\":1}");
            PrototypeLoadout medic = PrototypeLoadout.Load();
            Check(medic.role == PrototypeInfantryClass.Medic && medic.primary == PrototypeWeaponId.Smg &&
                medic.secondary == PrototypeWeaponId.Pistol && medic.throwable == PrototypeThrowableId.Smoke,
                "an older medic/sniper/marksman saved choice cannot bypass the current slots");
            PlayerPrefs.SetString(SaveKey, "{\"role\":3,\"primary\":4,\"secondary\":1,\"throwable\":1}");
            PrototypeLoadout engineer = PrototypeLoadout.Load();
            Check(engineer.primary == PrototypeWeaponId.BattleRifle && engineer.secondary == PrototypeWeaponId.Pistol &&
                engineer.throwable == PrototypeThrowableId.Smoke,
                "migration preserves a legal primary and repairs the historical marksman secondary");
            PlayerPrefs.SetString(SaveKey, "{\"role\":999,\"primary\":999,\"secondary\":999,\"throwable\":999}");
            CheckDefault(PrototypeLoadout.Load(), "malformed saved enums");
            PlayerPrefs.SetString(SaveKey, "{ invalid json");
            CheckDefault(PrototypeLoadout.Load(), "corrupt saved JSON");
            PlayerPrefs.DeleteKey(SaveKey);
            CheckDefault(PrototypeLoadout.Load(), "missing saved loadout");

            var forbidden = new PrototypeLoadout { role = PrototypeInfantryClass.Medic,
                primary = PrototypeWeaponId.Sniper, secondary = PrototypeWeaponId.Sniper,
                throwable = PrototypeThrowableId.Smoke };
            forbidden.Save();
            PrototypeLoadout roundtrip = PrototypeLoadout.Load();
            Check(roundtrip.primary == PrototypeWeaponId.Smg && roundtrip.secondary == PrototypeWeaponId.Pistol &&
                roundtrip.throwable == PrototypeThrowableId.Smoke && forbidden.primary == roundtrip.primary,
                "Save validates before serialization and Load repeats the same policy");
        }

        private static void VerifyPlayerSlots(PrototypePlayer player)
        {
            foreach (PrototypeInfantryClass role in Enum.GetValues(typeof(PrototypeInfantryClass)))
                foreach (PrototypeWeaponId primary in Matrix[(int)role])
                {
                    player.ApplyLoadout(new PrototypeLoadout { role = role, primary = primary,
                        secondary = PrototypeWeaponId.Marksman });
                    Check(player.Class == role && player.ActiveWeapon.Id == primary && !player.SecondaryEquipped,
                        "ApplyLoadout installs an allowed primary in the real player");
                    CheckModel(player, primary);
                    int primaryAmmo = player.Ammo;
                    player.EquipFirearm(true);
                    Check(player.SecondaryEquipped && player.ActiveWeapon.Id == PrototypeWeaponId.Pistol &&
                        player.Loadout.secondary == PrototypeWeaponId.Pistol,
                        "the real secondary inventory is a pistol for every class");
                    CheckModel(player, PrototypeWeaponId.Pistol);
                    Check(player.ActiveKit.TryFire(Time.time) &&
                        player.Ammo == PrototypeInfantryRoles.GetWeapon(PrototypeWeaponId.Pistol).Magazine - 1,
                        "the secondary slot has an independent usable pistol magazine");
                    player.EquipFirearm(false);
                    Check(player.ActiveWeapon.Id == primary && player.Ammo == primaryAmmo,
                        "switching back preserves the independent primary magazine");
                    player.EquipFirearm(true);
                    Check(player.Ammo == PrototypeInfantryRoles.GetWeapon(PrototypeWeaponId.Pistol).Magazine - 1,
                        "switching weapons does not refill the used pistol");
                }
            var forbidden = new PrototypeLoadout { role = PrototypeInfantryClass.Medic,
                primary = PrototypeWeaponId.Sniper, secondary = PrototypeWeaponId.Sniper };
            player.ApplyLoadout(forbidden);
            Check(player.ActiveWeapon.Id == PrototypeWeaponId.Smg && player.Class == PrototypeInfantryClass.Medic &&
                forbidden.primary == PrototypeWeaponId.Sniper,
                "direct ApplyLoadout repairs a copy instead of mutating caller data");
            player.EquipFirearm(true);
            Check(player.ActiveWeapon.Id == PrototypeWeaponId.Pistol,
                "an illegal secondary cannot reappear after a direct runtime slot switch");
        }

        private static void VerifyRoleChanges(PrototypePlayer player)
        {
            player.ApplyLoadout(new PrototypeLoadout { role = PrototypeInfantryClass.Assault,
                primary = PrototypeWeaponId.Carbine, throwable = PrototypeThrowableId.Smoke });
            player.SelectClass(PrototypeInfantryClass.Medic);
            Check(player.Class == PrototypeInfantryClass.Medic && player.Loadout.primary == PrototypeWeaponId.Carbine &&
                player.Loadout.throwable == PrototypeThrowableId.Smoke,
                "a class switch retains a primary shared by the two classes");
            player.SelectClass(PrototypeInfantryClass.Recon);
            Check(player.Loadout.primary == PrototypeWeaponId.Marksman,
                "a class switch replaces a forbidden primary with the unchanged class default");
            player.ApplyLoadout(new PrototypeLoadout { role = PrototypeInfantryClass.Assault,
                primary = PrototypeWeaponId.BattleRifle });
            player.SelectClass(PrototypeInfantryClass.Engineer);
            Check(player.Loadout.primary == PrototypeWeaponId.BattleRifle,
                "the assault/engineer shared primary is retained");
            player.SelectClass((PrototypeInfantryClass)999);
            Check(player.Class == PrototypeInfantryClass.Assault &&
                player.Loadout.primary == PrototypeWeaponId.BattleRifle &&
                player.Loadout.secondary == PrototypeWeaponId.Pistol,
                "invalid class requests are repaired before consulting a default weapon");
        }

        private static void VerifyDeployment(PrototypeRuntime game, PrototypePlayer player)
        {
            Set(player, "<Health>k__BackingField", 0f);
            Set(player, "downedUntil", 0f);
            Set(player, "respawnAt", -1f);
            Check(player.TryDeploy("BASE", new PrototypeLoadout { role = PrototypeInfantryClass.Medic,
                primary = PrototypeWeaponId.Sniper, secondary = PrototypeWeaponId.Marksman }),
                "an eligible real player can deploy using a repaired loadout");
            Check(player.Alive && player.Class == PrototypeInfantryClass.Medic &&
                player.ActiveWeapon.Id == PrototypeWeaponId.Smg && player.Loadout.secondary == PrototypeWeaponId.Pistol,
                "deployment cannot install forbidden primary or historical secondary weapons");
            player.EquipFirearm(true);
            Check(player.ActiveWeapon.Id == PrototypeWeaponId.Pistol,
                "the deployed secondary kit was rebuilt under the same policy");

            player.ApplyLoadout(new PrototypeLoadout { role = PrototypeInfantryClass.Medic,
                primary = PrototypeWeaponId.Carbine });
            Set(player, "<Health>k__BackingField", 0f);
            Set(player, "downedUntil", 0f);
            Set(player, "respawnAt", -1f);
            Check(player.TryDeploy("BASE", PrototypeInfantryClass.Assault) &&
                player.Class == PrototypeInfantryClass.Assault && player.Loadout.primary == PrototypeWeaponId.Carbine,
                "the class-only deployment overload retains a legal shared primary");

            bool previousNetwork = (bool)Field(game, "networkModeActive");
            bool previousHost = (bool)Field(game, "networkHost");
            try
            {
                Set(game, "networkModeActive", true);
                Set(game, "networkHost", false);
                Set(player, "<Health>k__BackingField", 0f);
                Set(player, "downedUntil", 0f);
                Set(player, "respawnAt", -1f);
                Check(!player.TryDeploy("BASE", new PrototypeLoadout { role = PrototypeInfantryClass.Recon,
                    primary = PrototypeWeaponId.Sniper }) && player.Class == PrototypeInfantryClass.Assault,
                    "a network replica cannot locally install a deployment loadout");
            }
            finally
            {
                Set(game, "networkModeActive", previousNetwork);
                Set(game, "networkHost", previousHost);
            }
        }

        private static void VerifyNetworkRequestSchema()
        {
            PrototypeLanPeerDeploy request = JsonUtility.FromJson<PrototypeLanPeerDeploy>(
                "{\"type\":\"deploy\",\"id\":7,\"location\":\"BASE\",\"role\":1,\"primary\":5,\"secondary\":5}");
            Check(request.id == 7 && request.location == "BASE" &&
                typeof(PrototypeLanPeerDeploy).GetField("primary") == null &&
                typeof(PrototypeLanPeerDeploy).GetField("secondary") == null &&
                typeof(PrototypeLanPeerDeploy).GetField("role") == null,
                "the existing LAN deployment schema never accepts a guest's claimed class or weapon slots");
        }

        private static void CheckModel(PrototypePlayer player, PrototypeWeaponId expected)
        {
            GameObject weapon = player.GetComponent<PrototypeSoldierVisual>().HeldWeapon;
            GameObject firstPerson = (GameObject)Field(player, "firstPersonWeapon");
            Check(ModelMatches(weapon, expected, false) && ModelMatches(firstPerson, expected, true),
                "both actual weapon models match the validated active inventory: " + expected + " / " +
                (weapon != null ? weapon.name : "missing") + " / " + (firstPerson != null ? firstPerson.name : "missing"));
        }

        private static bool ModelMatches(GameObject model, PrototypeWeaponId expected, bool firstPerson)
        {
            if (model == null) return false;
            PrototypeWeaponArtRig art = model.GetComponent<PrototypeWeaponArtRig>();
            if (art == null) return model.name == expected + " weapon model";
            GameObject source = Resources.Load<GameObject>("Weapons/IF27" + (firstPerson ? "View" : "World"));
            MeshFilter actualMesh = model.GetComponentInChildren<MeshFilter>();
            MeshFilter sourceMesh = source != null ? source.GetComponentInChildren<MeshFilter>() : null;
            return expected == PrototypeWeaponId.Carbine && art.Muzzle != null && art.RightGrip != null &&
                actualMesh != null && sourceMesh != null && actualMesh.sharedMesh == sourceMesh.sharedMesh;
        }

        private static void CheckDefault(PrototypeLoadout selection, string source) =>
            Check(selection.role == PrototypeInfantryClass.Assault && selection.primary == PrototypeWeaponId.Carbine &&
                selection.secondary == PrototypeWeaponId.Pistol && selection.throwable == PrototypeThrowableId.Frag,
                source + " uses a valid default loadout");

        private static object Field(object target, string name) =>
            target.GetType().GetField(name, Private).GetValue(target);
        private static void Set(object target, string name, object value) =>
            target.GetType().GetField(name, Private).SetValue(target, value);
        private static void Check(bool value, string detail)
        {
            if (!value) throw new InvalidOperationException(detail);
        }
    }
}
