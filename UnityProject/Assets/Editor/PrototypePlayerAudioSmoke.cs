using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Ironfront.UnityPrototype.Editor
{
    public static class PrototypePlayerAudioSmoke
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        [MenuItem("IRONFRONT/Verify Player Action Audio", true)]
        private static bool CanVerify() => EditorApplication.isPlaying;
        [MenuItem("IRONFRONT/Verify Player Action Audio")]
        private static void Verify()
        {
            PrototypeRuntime game = null; PrototypePlayer player = null; PrototypePlayerAudio audio = null;
            GameObject wall = null; Vector3 oldPosition = Vector3.zero; Quaternion oldRotation = Quaternion.identity;
            float oldSlideEnd = 0f; float health = 100f; bool pause = false, captured = false;
            var fields = new Dictionary<string, object>();
            try
            {
                game = UnityEngine.Object.FindAnyObjectByType<PrototypeRuntime>();
                Check(game != null && !game.IsNetworkSession, "solo runtime exists");
                if (!game.MatchStarted) game.BeginBattle();
                game.Frontend.Close(); game.SetSupportMenu(false);
                player = game.Player; Check(player.Alive, "verify with living player");
                player.ForceExitVehicle(); oldPosition = player.transform.position; oldRotation = player.transform.rotation;
                health = player.Health; pause = game.PauseOpen;
                typeof(PrototypeRuntime).GetProperty("PauseOpen").SetValue(game, false);
                foreach (string name in new[] { "crouched", "prone", "sprinting" }) fields[name] = Field(player, name);
                oldSlideEnd = player.Slide.EndsAt;
                captured = true;
                Set(player, "crouched", false); Set(player, "prone", false); Set(player, "sprinting", false); player.Slide.EndsAt = 0f;
                audio = player.GetComponent<PrototypePlayerAudio>(); Check(audio != null, "production player audio is attached");
                audio.Reset();
                foreach (string name in new[] { "StepWalk", "StepRun", "StepCrouch", "StepProne", "Crouch", "Stand", "Prone",
                    "Rise", "Board", "Hit", "Kill", "LockTick", "LockReady", "LockLost", "Zoom" })
                {
                    AudioClip clip = Resources.Load<AudioClip>("Audio/PlayerCues/" + name);
                    Check(clip != null && clip.channels == 1 && clip.frequency == 44100 && clip.length > .05f && clip.length < .4f,
                        "original PCM cue imports with expected channels/rate/length: " + name);
                    var data = new float[clip.samples]; Check(clip.GetData(data, 0), "cue samples readable");
                    float peak = 0f; double sum = 0;
                    foreach (float value in data) { peak = Mathf.Max(peak, Mathf.Abs(value)); sum += value * value; }
                    Check(peak < .8f && Math.Sqrt(sum / data.Length) > .025,
                        "cue has audible RMS and no clipped samples: " + name);
                }
                Check(audio.ActionSource.spatialBlend == 0f && audio.HudSource.spatialBlend == 0f,
                    "independent local action/HUD sources are two dimensional");
                Move(player, new Vector2(-130f, -280f), 0f); var controller = player.GetComponent<CharacterController>();
                controller.Move(Vector3.down * 2f); audio.ResetBaseline();
                Check(controller.isGrounded, "real character controller rests on terrain");
                for (int i = 0; i < 30; i++) { controller.Move(new Vector3(.35f, -.15f, 0f)); audio.Tick(.05f); }
                Check(audio.FootstepCount > 0 && audio.LastActionCue == "StepWalk", "actual grounded displacement emits walking cues");
                Set(player, "sprinting", true); audio.Reset();
                for (int i = 0; i < 30; i++) { controller.Move(new Vector3(.55f, -.15f, 0f)); audio.Tick(.05f); }
                Check(audio.FootstepCount > 0 && audio.LastActionCue == "StepRun", "actual fast grounded displacement uses running cue");
                Set(player, "sprinting", false); audio.Reset();
                Set(player, "crouched", true); audio.Tick(.05f); Check(audio.LastActionCue == "Crouch", "crouch transition emits once");
                int count = audio.ActionCueCount; audio.Tick(.05f); Check(audio.ActionCueCount == count, "held stance is silent");
                for (int i = 0; i < 20; i++) { controller.Move(new Vector3(.15f, -.15f, 0f)); audio.Tick(.05f); }
                Check(audio.LastActionCue == "StepCrouch", "actual crouched movement selects the quieter step texture");
                Set(player, "crouched", false); audio.Tick(.05f); Check(audio.LastActionCue == "Stand", "stand transition has distinct cue");
                Set(player, "prone", true); audio.Tick(.05f); Check(audio.LastActionCue == "Prone", "prone transition has cloth cue");
                for (int i = 0; i < 20; i++) { controller.Move(new Vector3(.12f, -.15f, 0f)); audio.Tick(.05f); }
                Check(audio.LastActionCue == "StepProne", "actual prone movement selects cloth movement texture");
                Set(player, "prone", false); audio.Tick(.05f); Check(audio.LastActionCue == "Rise", "prone rise has distinct cue");
                Set(player, "crouched", true); count = audio.ActionCueCount; audio.Tick(.8f);
                Check(audio.ActionCueCount == count + 1 && audio.LastActionCue == "Crouch",
                    "a long active frame still emits the successful stance transition once");
                audio.Tick(.8f); Check(audio.ActionCueCount == count + 1, "a repeated stalled frame never repeats the stance cue");
                Set(player, "crouched", false); audio.Tick(.05f);
                Move(player, new Vector2(-130f, -280f), 0f); controller.Move(Vector3.down * 2f); audio.Reset();
                wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.transform.position = player.transform.position + Vector3.right + Vector3.up;
                wall.transform.localScale = new Vector3(.4f, 3f, 6f); Physics.SyncTransforms();
                for (int i = 0; i < 60; i++) { controller.Move(new Vector3(.2f, -.15f, 0f)); audio.Tick(.05f); }
                Check(audio.FootstepCount == 0, "holding movement into a real wall emits no footstep burst");
                UnityEngine.Object.DestroyImmediate(wall); wall = null;
                controller.enabled = false; player.transform.position += Vector3.up * 10f; controller.enabled = true;
                controller.Move(new Vector3(.3f, 0f, 0f)); audio.Tick(.05f);
                Check(!controller.isGrounded && audio.FootstepCount == 0, "airborne motion has no footsteps");
                Move(player, new Vector2(-180f, -280f), 0f); controller.Move(Vector3.down * 2f); audio.Tick(.05f);
                Check(audio.FootstepCount == 0, "teleport does not create a step");
                typeof(PrototypeRuntime).GetProperty("PauseOpen").SetValue(game, true);
                PrototypePlayerAudio.PlayCue(PrototypePlayerCue.Hit); audio.Tick(.05f);
                Check(audio.HudCueCount == 0 && !audio.HudSource.isPlaying, "pause suppresses feedback and stops sources");
                typeof(PrototypeRuntime).GetProperty("PauseOpen").SetValue(game, false);
                foreach (PrototypePlayerCue cue in Enum.GetValues(typeof(PrototypePlayerCue))) PrototypePlayerAudio.PlayCue(cue);
                Check(audio.HudCueCount == 6 && audio.LastCue == PrototypePlayerCue.Zoom && audio.HudSource.isPlaying,
                    "all six public feedback cues reach the real local audio source");
                IPrototypeVehicle vehicle = null;
                foreach (IPrototypeVehicle candidate in game.Vehicles)
                    if (candidate.Team == player.Team && candidate.Alive && candidate.Occupant == null) { vehicle = candidate; break; }
                Check(vehicle != null && (bool)Invoke(player, "EnterVehicleSeat", vehicle, 0), "real seat boarding succeeds");
                count = audio.ActionCueCount;
                PrototypePlayerAudio.PlayCue(PrototypePlayerCue.Hit);
                audio.Tick(.8f); Check(audio.LastActionCue == "Board" && audio.ActionCueCount == count + 1 && audio.HudSource.isPlaying,
                    "successful real boarding on a stalled frame emits once and does not stop valid HUD audio");
                count = audio.ActionCueCount; int footsteps = audio.FootstepCount;
                audio.Tick(.05f); Check(audio.ActionCueCount == count && audio.FootstepCount == footsteps,
                    "remaining in seat does not repeat boarding or emit delayed long-frame footsteps");
                player.ForceExitVehicle(); audio.ResetBaseline();
                typeof(PrototypePlayer).GetProperty("Health").SetValue(player, 0f); audio.Tick(.05f);
                Check(!audio.ActionSource.isPlaying && !audio.HudSource.isPlaying, "death stops local action and HUD sources");
                Debug.Log("IRONFRONT player audio PASS: 15 original PCM clips validated, independent 2D sources, real terrain walk/run, " +
                    "stance transitions, wall/air/teleport suppression, actual seat boarding once, six feedback cues and pause/death cleanup");
            }
            catch (Exception error) { Debug.LogError("IRONFRONT player audio FAIL: " + error); }
            finally
            {
                if (wall != null) UnityEngine.Object.DestroyImmediate(wall);
                if (player != null && captured)
                {
                    typeof(PrototypePlayer).GetProperty("Health").SetValue(player, health); player.ForceExitVehicle();
                    player.Slide.EndsAt = oldSlideEnd;
                    foreach (KeyValuePair<string, object> field in fields) Set(player, field.Key, field.Value);
                    Move(player, new Vector2(oldPosition.x, oldPosition.z), oldRotation.eulerAngles.y); player.transform.position = oldPosition;
                    typeof(PrototypeRuntime).GetProperty("PauseOpen").SetValue(game, pause); audio?.Reset();
                }
            }
        }
        private static void Move(PrototypePlayer player, Vector2 position, float yaw)
        {
            var controller = player.GetComponent<CharacterController>(); controller.enabled = false;
            player.transform.position = new Vector3(position.x, PrototypeLayout.HeightAt(position.x, position.y) + .3f, position.y);
            player.transform.rotation = Quaternion.Euler(0f, yaw, 0f); controller.enabled = true; Physics.SyncTransforms();
        }
        private static object Field(object target, string name) => target.GetType().GetField(name, Private).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
        private static object Invoke(object target, string name, params object[] values) => target.GetType().GetMethod(name, Private).Invoke(target, values);
        private static void Check(bool value, string label) { if (!value) throw new InvalidOperationException(label); }
    }
}
