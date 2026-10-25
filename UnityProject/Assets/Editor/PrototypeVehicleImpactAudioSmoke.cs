using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Ironfront.UnityPrototype.Editor
{
    public static class PrototypeVehicleImpactAudioSmoke
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly Vector3 Origin = new Vector3(1250f, 240f, 900f);

        [MenuItem("IRONFRONT/Verify Vehicle Impact Audio", true)]
        private static bool CanVerify() => EditorApplication.isPlaying;
        [MenuItem("IRONFRONT/Verify Vehicle Impact Audio")]
        private static void Verify()
        {
            PrototypeRuntime game = UnityEngine.Object.FindAnyObjectByType<PrototypeRuntime>();
            var fixtures = new List<GameObject>();
            var oldPulses = new HashSet<PrototypePulseVisual>(UnityEngine.Object.FindObjectsByType<PrototypePulseVisual>());
            var oldAudio = new HashSet<AudioSource>(UnityEngine.Object.FindObjectsByType<AudioSource>());
            PrototypeScoreSystem originalScores = null;
            float blue = 0f, red = 0f, originalScale = Time.timeScale;
            bool network = false, host = false, matchStarted = false, pause = false, restore = false;
            Vector3 playerPosition = Vector3.zero; Quaternion playerRotation = Quaternion.identity;
            try
            {
                Check(game != null && game.Player != null && game.MatchStarted && !game.IsNetworkReplica &&
                    game.Player.Alive && game.Player.CurrentVehicle == null && !game.Match.Winner.HasValue && !game.PauseOpen,
                    "verify requires a live authoritative match with the local player on foot");
                originalScores = game.Scores; blue = game.Match.BlueTickets; red = game.Match.RedTickets;
                network = (bool)Field(game, "networkModeActive"); host = (bool)Field(game, "networkHost");
                matchStarted = game.MatchStarted; pause = game.PauseOpen;
                playerPosition = game.Player.transform.position; playerRotation = game.Player.transform.rotation; restore = true;
                Set(game, "networkModeActive", false); Time.timeScale = 1f;
                var scores = new PrototypeScoreSystem(game);
                Property(game, "Scores", scores); game.Match.SetNetworkTickets(100f, 100f);
                VerifyClips();
                PrototypeTeam team = game.Player.Team, enemy = team == PrototypeTeam.Blue ? PrototypeTeam.Red : PrototypeTeam.Blue;
                var shooter = Bot(game, team, Origin + Vector3.left * 35f, fixtures);
                var opposingShooter = Bot(game, enemy, Origin + Vector3.back * 35f, fixtures);
                var hulls = new List<IPrototypeVehicle>();
                for (int i = 0; i < 9; i++) hulls.Add(Hull(game, enemy, i, fixtures));
                foreach (IPrototypeVehicle hull in hulls)
                {
                    float before = hull.Health;
                    HitBullet(game, shooter.transform, team, hull, false, fixtures);
                    var audio = Feedback(hull);
                    Check(hull.Health == before && audio.PlayedCount == 1 && audio.LastKind == PrototypeVehicleImpactKind.Ricochet &&
                        audio.BulletSource.clip == PrototypeVehicleImpactAudio.Clip(PrototypeVehicleImpactKind.Ricochet) &&
                        audio.BulletSource.spatialBlend == 1f && audio.BulletSource.minDistance == 5f &&
                        audio.BulletSource.maxDistance == 105f && audio.BulletSource.rolloffMode == AudioRolloffMode.Logarithmic &&
                        OnHull(hull, audio.LastPoint),
                        "real swept rifle hits " + hull.VehicleName + " with a spatial ricochet but no hull damage; hp=" +
                        hull.Health + "/" + before + " point=" + audio.LastPoint);
                }
                Check(scores.Get(game.Player).Total == 0 && scores.Get(shooter).Total == 0,
                    "armor-blocked rifle impacts do not introduce damage credits");

                IPrototypeVehicle tank = hulls[0];
                PrototypeVehicleImpactAudio tankAudio = Feedback(tank);
                Ready(tankAudio); float tankHealth = tank.Health;
                var engine = tank.VehicleTransform.GetComponent<PrototypeVehicleEngineAudio>();
                AudioSource idle = engine.IdleSource, load = engine.LoadSource;
                AudioClip idleClip = idle.clip, loadClip = load.clip;
                HitBullet(game, shooter.transform, team, tank, true, fixtures);
                AudioSource reused = tankAudio.BulletSource; int played = tankAudio.PlayedCount;
                for (int i = 0; i < 20; i++) HitBullet(game, shooter.transform, team, tank, true, fixtures);
                Check(tank.Health == tankHealth && tankAudio.LastKind == PrototypeVehicleImpactKind.MachineGun &&
                    tankAudio.BulletSource.clip == PrototypeVehicleImpactAudio.Clip(PrototypeVehicleImpactKind.MachineGun) &&
                    tankAudio.PlayedCount == played && tankAudio.SuppressedCount >= 20 &&
                    ReferenceEquals(reused, tankAudio.BulletSource) && engine.IdleSource == idle && engine.LoadSource == load &&
                    idle.clip == idleClip && load.clip == loadClip,
                    "actual armor-blocked MG burst sounds metallic once in its 70ms window, reuses one voice and leaves engine loops unchanged; " +
                    "played=" + tankAudio.PlayedCount + " suppressed=" + tankAudio.SuppressedCount + " hp=" + tank.Health);
                Set(tankAudio, "nextBulletAt", Time.time - .001f);
                HitBullet(game, shooter.transform, team, tank, true, fixtures);
                Check(tankAudio.PlayedCount == played + 1 && tankAudio.BulletSource == reused,
                    "a subsequent real MG strike restarts the same bounded voice when the cooldown expires");

                IPrototypeVehicle bike = hulls[7]; float bikeBefore = bike.Health;
                Ready(Feedback(bike)); HitBullet(game, shooter.transform, team, bike, true, fixtures);
                Check(Mathf.Abs(bikeBefore - bike.Health - PrototypeVehicleArmor.Resolve(bike, 40f,
                    PrototypeDamageClass.MountedMachineGun)) < .02f && Feedback(bike).LastKind == PrototypeVehicleImpactKind.MachineGun &&
                    scores.Get(game.Player).Total == 0 && scores.Get(shooter).DamagePoints > 0,
                    "actual light-hull MG penetration keeps the existing armor coefficient and bot-only score attribution");

                Ready(tankAudio); tankHealth = tank.Health;
                HitExplosive(game, shooter.transform, team, tank, false, fixtures);
                Check(Mathf.Abs(tankHealth - tank.Health - PrototypeVehicleArmor.Resolve(tank, 145f,
                    PrototypeDamageClass.Cannon)) < .03f && tankAudio.LastDamageClass == PrototypeDamageClass.Cannon &&
                    tankAudio.LastKind == PrototypeVehicleImpactKind.HeavyImpact && tankAudio.BlastSource.clip ==
                    PrototypeVehicleImpactAudio.Clip(PrototypeVehicleImpactKind.HeavyImpact) && OnHull(tank, tankAudio.LastPoint) &&
                    tankAudio.BlastSource != tankAudio.BulletSource && tankAudio.BlastSource.minDistance == 8f &&
                    tankAudio.BlastSource.maxDistance == 170f && tankAudio.BlastSource.spatialBlend == 1f,
                    "real swept cannon collision produces its own heavy hull voice with unchanged damage; hp=" + tank.Health);
                IPrototypeVehicle ifv = hulls[3]; Ready(Feedback(ifv)); float ifvBefore = ifv.Health;
                HitExplosive(game, shooter.transform, team, ifv, true, fixtures);
                Check(Mathf.Abs(ifvBefore - ifv.Health - PrototypeVehicleArmor.Resolve(ifv, 210f,
                    PrototypeDamageClass.AntiArmorRocket)) < .03f && Feedback(ifv).LastDamageClass == PrototypeDamageClass.AntiArmorRocket,
                    "real swept anti-armor rocket blast reaches the actual hull and uses heavy metal impact; hp=" + ifv.Health);

                IPrototypeVehicle scout = hulls[1]; Ready(Feedback(scout)); float scoutBefore = scout.Health;
                HitVehicleRound(game, shooter.transform, team, scout, false, fixtures);
                Check(Mathf.Abs(scoutBefore - scout.Health - PrototypeVehicleArmor.Resolve(scout, 36f,
                    PrototypeDamageClass.Cannon)) < .03f && Feedback(scout).LastDamageClass == PrototypeDamageClass.Cannon,
                    "combined-arms cannon collision and overlapping blast de-duplicate hull feedback and retain damage");
                IPrototypeVehicle aircraft = hulls[5]; Ready(Feedback(aircraft)); float aircraftBefore = aircraft.Health;
                HitVehicleRound(game, shooter.transform, team, aircraft, true, fixtures);
                Check(Mathf.Abs(aircraftBefore - aircraft.Health - PrototypeVehicleArmor.Resolve(aircraft, 36f,
                    PrototypeDamageClass.AntiAirMissile)) < .03f && Feedback(aircraft).LastDamageClass == PrototypeDamageClass.AntiAirMissile,
                    "actual guided missile Step and collider hit produce a heavy strike on the aircraft, not MG audio");

                Ready(tankAudio); tankHealth = tank.Health; played = tankAudio.PlayedCount;
                Vector3 fragPoint = tank.VehicleTransform.GetComponent<Collider>().bounds.center;
                Invoke(game.Player, "ApplyFragDamageForTeam", fragPoint, team);
                Check(tank.Health == tankHealth && tankAudio.PlayedCount == played + 1 &&
                    tankAudio.LastDamageClass == PrototypeDamageClass.Frag && tankAudio.LastKind == PrototypeVehicleImpactKind.HeavyImpact,
                    "production grenade overlap still rings heavy armor even when frag damage resolves to zero");

                var cabin = (PrototypeCombinedArmsVehicle)Hull(game, team, 3, fixtures, 11);
                Check((bool)Invoke(game.Player, "EnterVehicleSeat", cabin, 0) && ReferenceEquals(game.Player.CurrentVehicle, cabin),
                    "local player enters an actual friendly driver seat for the cabin mix check");
                HitBullet(game, opposingShooter.transform, enemy, cabin, true, fixtures);
                var cabinAudio = Feedback(cabin);
                Check(cabinAudio.LastKind == PrototypeVehicleImpactKind.MachineGun && cabinAudio.BulletSource.spatialBlend == .2f &&
                    cabinAudio.BulletSource.volume > reused.volume && OnHull(cabin, cabinAudio.BulletSource.transform.position),
                    "an actual occupied hull keeps the strike at its collision point while adding clear cabin presence; blend=" +
                    cabinAudio.BulletSource.spatialBlend + " volume=" + cabinAudio.BulletSource.volume);
                game.Player.ForceExitVehicle();
                Invoke(cabinAudio, "LateUpdate");
                Check(cabinAudio.BulletSource.spatialBlend == 1f && Mathf.Abs(cabinAudio.BulletSource.volume - reused.volume) < .001f,
                    "leaving the actual seat returns the remaining metallic tail to full distance-based 3D sound");
                VerifyGates(game, tank, tankAudio);
                Check(scores.Get(game.Player).Total == 0, "vehicle impact audio never reattributes bot damage or awards player credits");
                Debug.Log("IRONFRONT vehicle impact audio PASS: three imported original PCM clips, actual rifle collisions on nine hulls, " +
                    "armor-blocked MG metal burst, two bounded independent voices, cadence limit/source reuse, untouched engines, " +
                    "real MG/cannon/rocket/guided missile damage and bot attribution, frag zero-damage metal strike, actual cabin seat presence, " +
                    "spatial hit points and pause/replica/lobby/winner/dead/inactive/invalid gating");
            }
            catch (Exception error) { Debug.LogError("IRONFRONT vehicle impact audio FAIL: " + error); }
            finally
            {
                if (restore && game != null)
                {
                    Set(game, "networkModeActive", false); Property(game.Match, "Winner", null);
                    Property(game, "MatchStarted", matchStarted); Property(game, "PauseOpen", pause); Time.timeScale = 1f;
                    if (game.Player.CurrentVehicle != null) game.Player.ForceExitVehicle();
                    game.Player.transform.SetPositionAndRotation(playerPosition, playerRotation);
                    Property(game, "Scores", originalScores); game.Match.SetNetworkTickets(blue, red);
                    Set(game, "networkModeActive", network); Set(game, "networkHost", host);
                }
                Time.timeScale = originalScale;
                foreach (GameObject fixture in fixtures) if (fixture != null) UnityEngine.Object.DestroyImmediate(fixture);
                foreach (PrototypePulseVisual pulse in UnityEngine.Object.FindObjectsByType<PrototypePulseVisual>())
                    if (!oldPulses.Contains(pulse)) UnityEngine.Object.DestroyImmediate(pulse.gameObject);
                foreach (AudioSource audio in UnityEngine.Object.FindObjectsByType<AudioSource>())
                    if (!oldAudio.Contains(audio) && audio.transform.parent == null && audio.name.EndsWith(" audio"))
                        UnityEngine.Object.DestroyImmediate(audio.gameObject);
                Physics.SyncTransforms();
            }
        }

        private static void VerifyClips()
        {
            foreach (PrototypeVehicleImpactKind kind in Enum.GetValues(typeof(PrototypeVehicleImpactKind)))
            {
                AudioClip clip = PrototypeVehicleImpactAudio.Clip(kind);
                Check(clip != null && clip.channels == 1 && clip.frequency == 44100 && clip.loadState == AudioDataLoadState.Loaded,
                    "actual imported mono PCM loaded for " + kind);
                var samples = new float[clip.samples]; Check(clip.GetData(samples, 0), "read imported samples for " + kind);
                float peak = 0f; double power = 0.0;
                foreach (float value in samples) { peak = Mathf.Max(peak, Mathf.Abs(value)); power += value * value; }
                Check(peak > .5f && peak < .8f && Math.Sqrt(power / samples.Length) > .025 &&
                    Mathf.Abs(samples[0]) < .001f && Mathf.Abs(samples[samples.Length - 1]) < .001f,
                    kind + " imported waveform has audible RMS, unclipped .78 peak and silent edges; peak=" + peak);
            }
            Check(PrototypeVehicleImpactAudio.Clip(PrototypeVehicleImpactKind.HeavyImpact).length > .8f &&
                PrototypeVehicleImpactAudio.Clip(PrototypeVehicleImpactKind.MachineGun).length < .25f &&
                PrototypeVehicleImpactAudio.Clip(PrototypeVehicleImpactKind.Ricochet).length < .2f,
                "metal machine-gun strike is short and the separate cannon/panel ring has a longer low body");
        }

        private static void VerifyGates(PrototypeRuntime game, IPrototypeVehicle hull, PrototypeVehicleImpactAudio audio)
        {
            int before = audio.PlayedCount; Vector3 point = hull.VehicleTransform.GetComponent<Collider>().bounds.center;
            Time.timeScale = 0f; Ready(audio);
            Check(!PrototypeCombatFeedback.VehicleImpact(game, hull, point, PrototypeDamageClass.MountedMachineGun), "solo time-scale pause rejects impact");
            Invoke(audio, "LateUpdate"); Check(!audio.BulletSource.isPlaying && !audio.BlastSource.isPlaying, "pause stops pending impact tails");
            Time.timeScale = 1f; Property(game, "PauseOpen", true);
            Check(!PrototypeCombatFeedback.VehicleImpact(game, hull, point, PrototypeDamageClass.Cannon), "solo paused menu rejects impact");
            Property(game, "PauseOpen", false); Set(game, "networkModeActive", true); Set(game, "networkHost", false);
            Check(game.IsNetworkReplica && !PrototypeCombatFeedback.VehicleImpact(game, hull, point, PrototypeDamageClass.Cannon), "replica cannot invent authoritative hits");
            Set(game, "networkModeActive", false); Property(game, "MatchStarted", false);
            Check(!PrototypeCombatFeedback.VehicleImpact(game, hull, point, PrototypeDamageClass.Cannon), "lobby has no hull-hit feedback");
            Property(game, "MatchStarted", true); Property(game.Match, "Winner", game.Player.Team);
            Check(!PrototypeCombatFeedback.VehicleImpact(game, hull, point, PrototypeDamageClass.Cannon), "completed battle rejects impact");
            Property(game.Match, "Winner", null); hull.VehicleTransform.gameObject.SetActive(false);
            Check(!PrototypeCombatFeedback.VehicleImpact(game, hull, point, PrototypeDamageClass.Cannon), "inactive hull rejects impact");
            hull.VehicleTransform.gameObject.SetActive(true); float hp = hull.Health; Property(hull, "Health", 0f);
            Check(!PrototypeCombatFeedback.VehicleImpact(game, hull, point, PrototypeDamageClass.Cannon), "dead hull rejects repeat impact");
            Property(hull, "Health", hp);
            Check(!PrototypeCombatFeedback.VehicleImpact(game, hull, new Vector3(float.NaN, 0f, 0f), PrototypeDamageClass.Cannon) &&
                !PrototypeCombatFeedback.VehicleImpact(game, hull, point, (PrototypeDamageClass)99) && audio.PlayedCount == before,
                "invalid positions/classes and all gated calls allocate no extra voices or accepted impacts");
        }

        private static void HitBullet(PrototypeRuntime game, Transform source, PrototypeTeam team,
            IPrototypeVehicle target, bool mounted, List<GameObject> fixtures)
        {
            Vector3 start = target.VehicleTransform.GetComponent<Collider>().bounds.center - Vector3.forward * 16f;
            var round = mounted ? PrototypeBallisticProjectile.LaunchMountedRound(game, team, source, start, Vector3.forward,
                500f, 100f, 40f) : PrototypeBallisticProjectile.LaunchBullet(game, team, source, start, Vector3.forward,
                PrototypeInfantryRoles.GetWeapon(PrototypeWeaponId.Carbine));
            fixtures.Add(round.gameObject);
            for (int i = 0; i < 15 && round.enabled; i++) Invoke(round, "Step", .02f);
            Check(!round.enabled, "swept " + (mounted ? "MG" : "rifle") + " actually collides with " + target.VehicleName);
        }
        private static void HitExplosive(PrototypeRuntime game, Transform source, PrototypeTeam team,
            IPrototypeVehicle target, bool rocket, List<GameObject> fixtures)
        {
            Vector3 start = target.VehicleTransform.GetComponent<Collider>().bounds.center - Vector3.forward * 16f;
            var round = rocket ? PrototypeBallisticProjectile.LaunchRocket(game, team, source, start, Vector3.forward) :
                PrototypeBallisticProjectile.LaunchShell(game, team, source, start, Vector3.forward, 170f, 100f);
            fixtures.Add(round.gameObject);
            for (int i = 0; i < 25 && round.enabled; i++) Invoke(round, "Step", .02f);
            Check(!round.enabled, "swept " + (rocket ? "rocket" : "cannon") + " actually collides with " + target.VehicleName);
        }
        private static void HitVehicleRound(PrototypeRuntime game, Transform source, PrototypeTeam team,
            IPrototypeVehicle target, bool guided, List<GameObject> fixtures)
        {
            Vector3 start = target.VehicleTransform.GetComponent<Collider>().bounds.center - Vector3.forward * 16f;
            var round = PrototypeVehicleProjectile.Launch(game, team, source, start, Vector3.forward, 160f, 100f,
                45f, 0f, 3f, .8f, guided ? target : null,
                guided ? PrototypeDamageClass.AntiAirMissile : PrototypeDamageClass.Cannon);
            Check(round != null, "production vehicle round launches"); fixtures.Add(round.gameObject);
            int before = Feedback(target).PlayedCount;
            for (int i = 0; i < 35 && round.enabled; i++) Invoke(round, "Step", .02f);
            Check(!round.enabled && round.HitCollider != null && ReferenceEquals(PrototypeVehicleHit.Find(round.HitCollider), target) &&
                Feedback(target).PlayedCount == before + 1,
                "actual vehicle round hits the intended collider and direct/blast overlap creates one metallic cue");
        }
        private static IPrototypeVehicle Hull(PrototypeRuntime game, PrototypeTeam team, int index,
            List<GameObject> fixtures, int? positionIndex = null)
        {
            var root = new GameObject("Impact Audio Verification Hull " + index); fixtures.Add(root); root.transform.SetParent(game.transform);
            Vector3 position = Origin + Vector3.right * ((positionIndex ?? index) * 40f);
            Vector2 map = new Vector2(position.x, position.z); IPrototypeVehicle hull;
            if (index == 0) { var v = root.AddComponent<PrototypeTankVehicle>(); v.Initialize(game, team, map, 0f); hull = v; }
            else if (index == 1) { var v = root.AddComponent<PrototypeScoutVehicle>(); v.Initialize(game, team, map, false); hull = v; }
            else if (index == 2) { var v = root.AddComponent<PrototypeTransportVehicle>(); v.Initialize(game, team, map, false); hull = v; }
            else { var v = root.AddComponent<PrototypeCombinedArmsVehicle>(); v.Initialize(game, team, (PrototypeVehicleKind)(index - 3), map, false); v.RespawnEnabled = false; hull = v; }
            ((MonoBehaviour)hull).enabled = false; root.transform.position = position; Physics.SyncTransforms(); return hull;
        }
        private static PrototypeBot Bot(PrototypeRuntime game, PrototypeTeam team, Vector3 point, List<GameObject> fixtures)
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Capsule); fixtures.Add(root); root.transform.SetParent(game.transform);
            var bot = root.AddComponent<PrototypeBot>(); bot.Initialize(game, game.Navigation, team,
                new Vector2(point.x, point.z), PrototypeInfantryClass.Assault); bot.enabled = false; root.transform.position = point; return bot;
        }
        private static bool OnHull(IPrototypeVehicle hull, Vector3 point) =>
            Vector3.Distance(hull.VehicleTransform.GetComponent<Collider>().ClosestPoint(point), point) < .015f;
        private static PrototypeVehicleImpactAudio Feedback(IPrototypeVehicle hull)
        { var audio = hull.VehicleTransform.GetComponent<PrototypeVehicleImpactAudio>(); Check(audio != null, "real hull collision initialized dedicated impact feedback for " + hull.VehicleName); return audio; }
        private static void Ready(PrototypeVehicleImpactAudio audio)
        { Set(audio, "nextBulletAt", Time.time - .001f); Set(audio, "nextBlastAt", Time.time - .001f); }
        private static object Field(object target, string name) => target.GetType().GetField(name, Private).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
        private static void Property(object target, string name, object value) => target.GetType().GetProperty(name).SetValue(target, value);
        private static object Invoke(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
        private static void Check(bool value, string description) { if (!value) throw new InvalidOperationException(description); }
    }
}
