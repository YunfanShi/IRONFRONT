using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Ironfront.UnityPrototype.Editor
{
    public static class PrototypeEquipmentSmoke
    {
        [MenuItem("IRONFRONT/Verify Equipment and Effects", true)]
        private static bool CanVerify() => EditorApplication.isPlaying;

        [MenuItem("IRONFRONT/Verify Equipment and Effects")]
        private static void Verify()
        {
            try
            {
                PrototypeRuntime game = UnityEngine.Object.FindAnyObjectByType<PrototypeRuntime>();
                Check(game != null && game.Player != null, "Play mode has a player");
                if (!game.MatchStarted)
                {
                    game.ConfigureSoloTeams(8);
                    game.Match.ConfigureSolo(PrototypeGameMode.Conquest);
                    game.BeginBattle();
                    game.Frontend.Close();
                }
                var temporary = new GameObject("Equipment smoke props");
                try
                {
                    foreach (PrototypeWeaponId id in Enum.GetValues(typeof(PrototypeWeaponId)))
                    {
                        GameObject model = PrototypeWeaponVisual.Build(
                            temporary.transform, id, game);
                        Check(model.GetComponentsInChildren<Renderer>().Length >= 7,
                            id + " has a detailed 3D model");
                        PrototypeCombatFeedback.PlayWeapon(game.Player.transform, id);
                        string clipName = id == PrototypeWeaponId.BattleRifle ?
                            "battleRifle" : id.ToString().ToLowerInvariant();
                        AudioClip expected = Resources.Load<AudioClip>("Audio/" + clipName);
                        Check(expected != null, id + " browser shot is included in Unity");
                        FieldInfo weapons = typeof(PrototypeCombatFeedback).GetField(
                            "WeaponClips", BindingFlags.Static | BindingFlags.NonPublic);
                        Check(weapons != null &&
                            ((AudioClip[])weapons.GetValue(null))[(int)id] == expected,
                            id + " plays its browser shot clip");
                        UnityEngine.Object.Destroy(model);
                    }
                    Check(temporary.GetComponent<AudioSource>() == null,
                        "weapon sounds attach to the shooter, not a prop");
                    Check(game.Player.GetComponent<AudioSource>() != null,
                        "all seven firearms create a playable shot source");
                    foreach (PrototypeThrowableId kind in Enum.GetValues(typeof(PrototypeThrowableId)))
                    {
                        GameObject grenade = PrototypeEquipmentVisual.BuildThrowable(
                            temporary.transform, kind, game);
                        Check(grenade.transform.Find("Canister") != null &&
                            grenade.transform.Find("Safety lever") != null &&
                            grenade.transform.Find(kind == PrototypeThrowableId.Frag ?
                                "Fragment rib" : "Smoke vent") != null,
                            kind + " has a distinct canister, fuse and body detail");
                        UnityEngine.Object.Destroy(grenade);
                    }
                    GameObject rocket = PrototypeEquipmentVisual.BuildRocket(
                        temporary.transform, game);
                    GameObject launcher = PrototypeEquipmentVisual.BuildLauncher(
                        temporary.transform, game);
                    Check(rocket.transform.Find("Warhead") != null &&
                        launcher.transform.Find("Launcher tube") != null,
                        "rocket and launcher each have a 3D model");
                    foreach (PrototypeInfantryClass role in
                        Enum.GetValues(typeof(PrototypeInfantryClass)))
                    {
                        GameObject gear = new GameObject(role + " gear");
                        gear.transform.SetParent(temporary.transform, false);
                        PrototypeEquipmentVisual.BuildClassGear(gear.transform, role, game);
                        Check(gear.GetComponentsInChildren<Renderer>().Length > 0,
                            role + " has visible class equipment");
                    }
                    PrototypeBot near = null;
                    PrototypeBot far = null;
                    PrototypeBot friendly = null;
                    foreach (PrototypeBot bot in game.Bots)
                    {
                        if (!bot.Alive || bot.IsPassenger) continue;
                        if (bot.Team == game.Player.Team)
                        { if (friendly == null) friendly = bot; }
                        else if (near == null) near = bot;
                        else if (far == null) far = bot;
                    }
                    Check(near != null && far != null && friendly != null,
                        "two opponents and one teammate are available");
                    Vector3 center = game.Player.transform.position + Vector3.up;
                    near.transform.position = center + Vector3.right * 1.5f;
                    far.transform.position = center + Vector3.right * 12f;
                    friendly.transform.position = center + Vector3.forward * 1.5f;
                    float nearBefore = near.Health;
                    float farBefore = far.Health;
                    float friendlyBefore = friendly.Health;
                    MethodInfo frag = typeof(PrototypePlayer).GetMethod("ApplyFragDamage",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                    Check(frag != null, "frag blast damage method exists");
                    frag.Invoke(game.Player, new object[] { center });
                    Check(near.Health < nearBefore, "frag hurts a target inside 5.5 meters");
                    Check(Mathf.Approximately(far.Health, farBefore),
                        "frag does not hurt a target outside 5.5 meters");
                    Check(Mathf.Approximately(friendly.Health, friendlyBefore),
                        "frag respects friendly fire rules");
                    int pulseCount = UnityEngine.Object.FindObjectsByType<PrototypePulseVisual>().Length;
                    PrototypeCombatFeedback.Explosion(game, center, 5.5f,
                        PrototypeCombatSound.FragExplosion);
                    Check(UnityEngine.Object.FindObjectsByType<PrototypePulseVisual>().Length >
                        pulseCount + 10, "frag explosion creates a blast and debris");
                    MethodInfo clipFactory = typeof(PrototypeCombatFeedback).GetMethod("Clip",
                        BindingFlags.Static | BindingFlags.NonPublic);
                    Check(clipFactory != null, "equipment sound library exists");
                    foreach (PrototypeCombatSound sound in
                        Enum.GetValues(typeof(PrototypeCombatSound)))
                        Check(clipFactory.Invoke(null, new object[] { sound }) is AudioClip,
                            sound + " has a playable audio clip");
                    PrototypeTankVehicle tank = null;
                    foreach (IPrototypeVehicle vehicle in game.Vehicles)
                        if (vehicle is PrototypeTankVehicle candidate &&
                            candidate.Team == game.Player.Team && candidate.Alive)
                        { tank = candidate; break; }
                    Check(tank != null && tank.TrySetDriver(game.Player),
                        "a friendly tank accepts the player");
                    int before = UnityEngine.Object.FindObjectsByType<PrototypeBallisticProjectile>().Length;
                    MethodInfo cannon = typeof(PrototypeTankVehicle).GetMethod("FireCannon",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                    Check(cannon != null, "tank cannon fire method exists");
                    cannon.Invoke(tank, null);
                    Check(UnityEngine.Object.FindObjectsByType<PrototypeBallisticProjectile>().Length ==
                        before + 1, "tank fires a physical shell");
                    Check(tank.GetComponent<AudioSource>() != null &&
                        tank.GetComponent<AudioSource>().isPlaying,
                        "tank cannon produces audible sound");
                    FieldInfo effects = typeof(PrototypeCombatFeedback).GetField(
                        "EffectClips", BindingFlags.Static | BindingFlags.NonPublic);
                    AudioClip cannonClip = Resources.Load<AudioClip>("Audio/cannon");
                    Check(cannonClip != null && effects != null &&
                        ((AudioClip[])effects.GetValue(null))[(int)PrototypeCombatSound.TankCannon]
                            == cannonClip,
                        "tank cannon plays the browser cannon clip");
                    Check(UnityEngine.Object.FindAnyObjectByType<PrototypePulseVisual>() != null,
                        "weapon discharge creates visible muzzle feedback");
                    Debug.Log("IRONFRONT equipment PASS: seven guns, four class kits, two grenades, " +
                        "rocket model, 5.5 m blast boundary, tank firing audio and effects");
                }
                finally { UnityEngine.Object.Destroy(temporary); }
            }
            catch (Exception error)
            {
                Debug.LogError("IRONFRONT equipment FAIL: " + error);
            }
        }

        private static void Check(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException(description);
        }
    }
}
