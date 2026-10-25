using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Ironfront.UnityPrototype.Editor
{
    // Real default-scene Rigidbody collisions and scaled game frames drive this
    // check; no fuse timestamps are fabricated to manufacture an emission.
    public static class PrototypeSmokeCollisionFuseSmoke
    {
        private sealed class Probe
        {
            public GameObject Canister;
            public PrototypeSmokeGrenadeFuse Fuse;
        }
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static PrototypeRuntime game;
        private static readonly List<GameObject> fixtures = new List<GameObject>();
        private static readonly Dictionary<Behaviour, bool> disabled = new Dictionary<Behaviour, bool>();
        private static Probe ground, crate, wall, roof, emptyAir, pulse, countermeasure, ownBody;
        private static PrototypeCombinedArmsVehicle roofVehicle;
        private static GameObject fragment;
        private static bool oldRuntimeEnabled, oldPause, oldSupport, didPause, pauseActive, sawWallFollow, checkedAir, roofMoved, restore;
        private static float startedAt, oldScale, pausedClock, pausedRemaining, fragmentGoneAt;
        private static double wallStarted, pauseStarted;

        [MenuItem("IRONFRONT/Verify Smoke Collision Fuse", true)]
        private static bool CanVerify() => EditorApplication.isPlaying && !EditorApplication.isPaused && game == null;
        [MenuItem("IRONFRONT/Verify Smoke Collision Fuse")]
        private static void Verify()
        {
            try
            {
                game = UnityEngine.Object.FindAnyObjectByType<PrototypeRuntime>();
                Check(game != null && game.Player != null && !game.IsNetworkReplica, "authoritative runtime exists");
                if (!game.MatchStarted) game.BeginBattle();
                Check(!game.Match.Winner.HasValue, "active battle has no result");
                oldRuntimeEnabled = game.enabled; oldPause = game.PauseOpen;
                oldSupport = game.SupportMenuOpen; oldScale = Time.timeScale; restore = true;
                game.SetSupportMenu(false); Invoke(game, "SetPause", false); game.Frontend.Close();
                game.enabled = false; Disable(game.Player);
                foreach (PrototypeBot bot in game.Bots) Disable(bot);
                foreach (PrototypeRemotePlayer actor in game.RemotePlayers) Disable(actor);
                foreach (IPrototypeVehicle vehicle in game.Vehicles)
                    if (vehicle is Behaviour behaviour) Disable(behaviour);
                foreach (PrototypeBallisticProjectile round in UnityEngine.Object.FindObjectsByType<PrototypeBallisticProjectile>()) Disable(round);
                foreach (PrototypeVehicleProjectile round in UnityEngine.Object.FindObjectsByType<PrototypeVehicleProjectile>()) Disable(round);
                Box("Fuse flat floor", new Vector3(900f, 0f, 900f), new Vector3(20f, 1f, 20f));
                Box("Fuse box-top support", new Vector3(935f, 1.5f, 900f), new Vector3(4f, 3f, 4f));
                Box("Fuse wall landing floor", new Vector3(970f, 0f, 900f), new Vector3(20f, 1f, 20f));
                Box("Fuse high static wall", new Vector3(970f, 6f, 903f), new Vector3(10f, 12f, .3f));
                ground = Canister(new Vector3(900f, 3f, 900f), Vector3.zero, game.Player.transform);
                crate = Canister(new Vector3(935f, 5f, 900f), Vector3.zero, game.Player.transform);
                wall = Canister(new Vector3(970f, 6f, 900f), new Vector3(0f, 8f, 17f), game.Player.transform);
                Box("Fuse vehicle landing floor", new Vector3(1100f, 0f, 900f), new Vector3(20f, 1f, 20f));
                GameObject vehicleRoot = new GameObject("Actual vehicle roof fuse fixture"); fixtures.Add(vehicleRoot);
                roofVehicle = vehicleRoot.AddComponent<PrototypeCombinedArmsVehicle>();
                roofVehicle.Initialize(game, game.Player.Team, PrototypeVehicleKind.Ifv, new Vector2(1100f, 900f), false);
                roofVehicle.RespawnEnabled = false; roofVehicle.enabled = false;
                roofVehicle.transform.position = new Vector3(1100f, .5f, 900f);
                game.Vehicles.Add(roofVehicle);
                roof = Canister(new Vector3(1100f, 5.5f, 900f), Vector3.zero, game.Player.transform);
                emptyAir = Canister(new Vector3(1500f, 1500f, 1500f), Vector3.zero, game.Player.transform);
                GameObject pulseBox = Box("Ignored muzzle pulse support", new Vector3(1010f, 2f, 900f), new Vector3(4f, 1f, 4f));
                pulseBox.AddComponent<PrototypePulseVisual>().enabled = false;
                pulse = Canister(new Vector3(1010f, 4f, 900f), Vector3.zero, game.Player.transform);
                GameObject counterBox = Box("Ignored flare support", new Vector3(1070f, 2f, 900f), new Vector3(4f, 1f, 4f));
                counterBox.AddComponent<PrototypeVehicleCountermeasure>().enabled = false;
                countermeasure = Canister(new Vector3(1070f, 4f, 900f), Vector3.zero, game.Player.transform);
                GameObject ownBox = Box("Ignored owner support", new Vector3(1040f, 2f, 900f), new Vector3(4f, 1f, 4f));
                ownBody = Canister(new Vector3(1040f, 4f, 900f), Vector3.zero, ownBox.transform);
                fragment = new GameObject("Actual original frag-fuse probe"); fixtures.Add(fragment);
                fragment.transform.position = new Vector3(1500f, 1300f, 1450f);
                PrototypeEquipmentVisual.BuildThrowable(fragment.transform, PrototypeThrowableId.Frag, game);
                IEnumerator detonation = (IEnumerator)typeof(PrototypePlayer).GetMethod("Detonate", Private)
                    .Invoke(game.Player, new object[] { fragment, PrototypeThrowableId.Frag, game.Player.Team });
                game.Player.StartCoroutine(detonation);
                startedAt = Time.time; wallStarted = EditorApplication.timeSinceStartup;
                didPause = pauseActive = sawWallFollow = checkedAir = roofMoved = false; fragmentGoneAt = -1f;
                EditorApplication.update -= Tick; EditorApplication.update += Tick;
                Debug.Log("IRONFRONT smoke fuse verification running: real contacts, .35-second delay, falling wall canister, pause and 14-second no-contact expiry");
            }
            catch (Exception error) { Finish(error); }
        }

        private static void Tick()
        {
            try
            {
                Check(EditorApplication.isPlaying && game != null, "Play remains active");
                Check(EditorApplication.timeSinceStartup - wallStarted < 24d, "verification completes without pausing the Editor");
                if (pauseActive)
                {
                    Check(Mathf.Abs(Time.time - pausedClock) < .001f &&
                        Mathf.Abs(wall.Fuse.FuseRemaining - pausedRemaining) < .001f && !wall.Fuse.HasEmitted,
                        "scaled fuse time and emission remain frozen while the game is paused");
                    if (EditorApplication.timeSinceStartup - pauseStarted < .3d) return;
                    Time.timeScale = 1f; pauseActive = false;
                }
                if (!didPause && wall.Fuse.HasContact && !wall.Fuse.HasEmitted)
                {
                    pausedClock = Time.time; pausedRemaining = wall.Fuse.FuseRemaining;
                    pauseStarted = EditorApplication.timeSinceStartup;
                    Time.timeScale = 0f; didPause = pauseActive = true; return;
                }
                CheckTiming(ground); CheckTiming(crate); CheckTiming(wall); CheckTiming(roof);
                if (!roofMoved && roof.Fuse.HasEmitted && Time.time - roof.Fuse.EmissionTime > .2f)
                {
                    Check(roof.Canister != null && roof.Fuse.Cloud != null &&
                        ReferenceEquals(PrototypeVehicleHit.Find(roof.Fuse.ContactCollider), roofVehicle) &&
                        roof.Fuse.Cloud.IsFollowingCanister(roof.Canister.transform),
                        "actual vehicle-roof contact lights the fuse but keeps the source physical on its movable support");
                    roofVehicle.transform.position += Vector3.right * 10f;
                    Physics.SyncTransforms(); roofMoved = true;
                }
                if (wall.Fuse.HasEmitted && wall.Canister != null && wall.Fuse.Cloud != null &&
                    wall.Canister.transform.position.y > 1f && wall.Fuse.Cloud.IsFollowingCanister(wall.Canister.transform))
                {
                    float frameTravel = wall.Canister.GetComponent<Rigidbody>().linearVelocity.magnitude *
                        Mathf.Max(Time.deltaTime, Time.fixedDeltaTime);
                    Check(Vector3.Distance(wall.Fuse.Cloud.transform.position, wall.Canister.transform.position) < frameTravel + .25f &&
                        !wall.Fuse.Cloud.transform.Find("Smoke grenade model").gameObject.activeSelf,
                        "a high-wall emission follows the physical falling canister without a hovering spent model");
                    foreach (ParticleSystem particles in wall.Fuse.Cloud.GetComponentsInChildren<ParticleSystem>())
                        Check(particles.main.simulationSpace == ParticleSystemSimulationSpace.World,
                            "already-emitted smoke remains in world space while its source falls");
                    sawWallFollow = true;
                }
                float elapsed = Time.time - startedAt;
                if (elapsed > PrototypeThrowableTrajectory.FuseSeconds + .1f && !checkedAir)
                {
                    Check(emptyAir.Canister != null && !emptyAir.Fuse.HasContact && !emptyAir.Fuse.HasEmitted && emptyAir.Fuse.Cloud == null,
                        "an airborne smoke canister does not emit at the old frag fuse time");
                    Check(!pulse.Fuse.HasContact && !countermeasure.Fuse.HasContact && !ownBody.Fuse.HasContact,
                        "muzzle effects, flares and the thrower's own collision cannot light the smoke fuse");
                    checkedAir = true;
                }
                if (fragment == null && fragmentGoneAt < 0f) fragmentGoneAt = elapsed;
                if (elapsed < PrototypeSmokeGrenadeFuse.FlightTimeout + .2f) return;
                Check(didPause && checkedAir && sawWallFollow && roofMoved, "pause, airborne hold, wall and vehicle-roof emission were observed");
                Check(ground.Fuse.HasEmitted && crate.Fuse.HasEmitted && wall.Fuse.HasEmitted && roof.Fuse.HasEmitted &&
                    ground.Canister == null && crate.Canister == null && wall.Canister == null && roof.Canister == null,
                    "ground, box, wall and moved-away vehicle support let physical canisters reach stationary ground");
                CheckSpent(ground, .5f); CheckSpent(crate, 3f); CheckSpent(wall, .5f); CheckSpent(roof, .5f);
                Check(emptyAir.Canister == null && !emptyAir.Fuse.HasEmitted && emptyAir.Fuse.Cloud == null &&
                    pulse.Canister == null && countermeasure.Canister == null && ownBody.Canister == null,
                    "14-second flights without valid contact expire with no airborne cloud");
                Check(fragmentGoneAt >= PrototypeThrowableTrajectory.FuseSeconds - .01f &&
                    fragmentGoneAt < PrototypeThrowableTrajectory.FuseSeconds + Time.maximumDeltaTime + .15f,
                    "fragmentation grenade keeps its original timed fuse");
                Finish(null);
            }
            catch (Exception error) { Finish(error); }
        }

        private static void CheckTiming(Probe probe)
        {
            if (!probe.Fuse.HasEmitted) return;
            float delay = probe.Fuse.EmissionTime - probe.Fuse.ContactTime;
            Check(probe.Fuse.HasContact && delay >= PrototypeSmokeGrenadeFuse.ContactDelay - .002f &&
                delay <= PrototypeSmokeGrenadeFuse.ContactDelay + Time.maximumDeltaTime + .1f,
                "real collision starts the short fuse and emission never precedes the .35-second contact delay");
        }
        private static void CheckSpent(Probe probe, float surfaceY)
        {
            PrototypeSmokeCloud cloud = probe.Fuse.Cloud;
            Check(cloud != null, "emitted smoke outlives its physical canister");
            Transform model = cloud.transform.Find("Smoke grenade model");
            float minimum = float.PositiveInfinity;
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>()) minimum = Mathf.Min(minimum, renderer.bounds.min.y);
            Check(model.gameObject.activeSelf && Mathf.Abs(minimum - surfaceY) < .03f,
                "original spent smoke model is supported by the actual floor/box rather than hovering");
        }
        private static Probe Canister(Vector3 at, Vector3 velocity, Transform owner)
        {
            var can = new GameObject("Actual collision-fuse smoke canister"); fixtures.Add(can);
            can.transform.position = at;
            can.AddComponent<SphereCollider>().radius = PrototypeThrowableTrajectory.CollisionRadius;
            Rigidbody body = can.AddComponent<Rigidbody>();
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.linearDamping = 0f; body.linearVelocity = velocity;
            GameObject model = PrototypeEquipmentVisual.BuildThrowable(can.transform, PrototypeThrowableId.Smoke, game);
            PrototypeThrowableVisual.RemoveSafety(model);
            var fuse = can.AddComponent<PrototypeSmokeGrenadeFuse>(); fuse.Initialize(game, owner);
            return new Probe { Canister = can, Fuse = fuse };
        }
        private static GameObject Box(string name, Vector3 at, Vector3 size)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube); fixtures.Add(box);
            box.name = name; box.transform.position = at; box.transform.localScale = size; return box;
        }
        private static void Disable(Behaviour behaviour)
        {
            if (behaviour == null || disabled.ContainsKey(behaviour)) return;
            disabled.Add(behaviour, behaviour.enabled); behaviour.enabled = false;
        }
        private static void Finish(Exception error)
        {
            EditorApplication.update -= Tick;
            if (restore) Time.timeScale = oldScale > 0f ? oldScale : 1f;
            foreach (Probe probe in new[] { ground, crate, wall, roof, emptyAir, pulse, countermeasure, ownBody })
                if (probe != null && probe.Fuse.Cloud != null) UnityEngine.Object.DestroyImmediate(probe.Fuse.Cloud.gameObject);
            if (game != null && roofVehicle != null) game.Vehicles.Remove(roofVehicle);
            foreach (GameObject item in fixtures) if (item != null) UnityEngine.Object.DestroyImmediate(item);
            fixtures.Clear();
            foreach (KeyValuePair<Behaviour, bool> state in disabled) if (state.Key != null) state.Key.enabled = state.Value;
            disabled.Clear();
            if (restore && game != null)
            {
                game.enabled = oldRuntimeEnabled;
                Invoke(game, "SetPause", oldPause);
                if (oldSupport) game.SetSupportMenu(true);
                Time.timeScale = oldScale;
            }
            restore = false;
            game = null; ground = crate = wall = roof = emptyAir = pulse = countermeasure = ownBody = null;
            fragment = null; roofVehicle = null;
            if (error != null) Debug.LogError("IRONFRONT smoke collision fuse FAIL: " + error);
            else Debug.Log("IRONFRONT smoke collision fuse PASS: actual ground/box/wall/vehicle-roof contact, .35-second delay, " +
                "no old airborne burst, scaled pause, physical falling emitter/world-space particles, supported original spent model, " +
                "vehicle move-away fall without hovering, owner/pulse/flare rejection, 14-second no-contact expiry and unchanged fragmentation fuse");
        }
        private static object Invoke(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
