using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Ironfront.UnityPrototype.Editor
{
    public static class PrototypeMusicSmoke
    {
        [MenuItem("IRONFRONT/Verify Adaptive Music", true)]
        private static bool CanVerify() => EditorApplication.isPlaying;

        [MenuItem("IRONFRONT/Verify Adaptive Music")]
        public static void Verify()
        {
            GameObject holder = null;
            bool hadVolume = PlayerPrefs.HasKey(PrototypeMusicDirector.VolumeKey);
            float savedVolume = PlayerPrefs.GetFloat(PrototypeMusicDirector.VolumeKey,
                PrototypeMusicDirector.DefaultVolume);
            float masterVolume = AudioListener.volume;
            bool listenerPause = AudioListener.pause;
            float timeScale = Time.timeScale;
            try
            {
                VerifyRules();
                holder = new GameObject("Temporary adaptive music smoke");
                PrototypeMusicDirector director = holder.AddComponent<PrototypeMusicDirector>();
                director.Initialize();
                director.Initialize();
                AudioSource[] voices = holder.GetComponents<AudioSource>();
                Check(voices.Length == 2, "repeated initialization creates only two music voices");
                director.SetMusicVolume(.55f);
                foreach (string name in new[] { "lobby", "victory-near", "defeat-near" })
                {
                    AudioClip clip = Resources.Load<AudioClip>("Audio/Music/" + name);
                    Check(clip != null && clip.length >= 15f && clip.channels == 2 &&
                        clip.frequency == 44100 && clip.samples > 0 &&
                        clip.loadState != AudioDataLoadState.Failed,
                        name + " imports as a full 44.1-kHz stereo music loop");
                    // Streaming Vorbis assets intentionally do not expose
                    // PCM through GetData. Original WAV energy, clipping and
                    // loop boundaries are checked by the composition manifest.
                }

                var match = new PrototypeMatch();
                match.ConfigureSolo(PrototypeGameMode.Conquest);
                director.Tick(match, PrototypeTeam.Blue, false, 0f);
                director.Tick(match, PrototypeTeam.Blue, false, 3f);
                AudioSource lobby = Playing(voices, "lobby");
                Check(director.CurrentState == PrototypeMusicState.Lobby && lobby != null &&
                    Mathf.Abs(lobby.volume - .55f) < .001f, "lobby starts its looping 2D source");
                Check(lobby.loop && lobby.spatialBlend == 0f && lobby.ignoreListenerPause,
                    "music is non-spatial and continues through a paused listener");
                lobby.timeSamples = 1200;
                director.SetMusicVolume(0f);
                director.Tick(match, PrototypeTeam.Blue, false, .5f);
                Check(lobby.isPlaying && lobby.timeSamples >= 1200 && AllMuted(voices),
                    "muting changes gain without stopping or restarting the loop");
                director.SetMusicVolume(.55f);
                Check(lobby.isPlaying && lobby.timeSamples >= 1200 && lobby.volume > .5f,
                    "unmuting resumes the same playback position");
                Check(Mathf.Approximately(AudioListener.volume, masterVolume),
                    "music volume preserves the master listener volume");

                director.Tick(match, PrototypeTeam.Blue, true, 0f);
                Time.timeScale = 0f;
                AudioListener.pause = true;
                director.Tick(match, PrototypeTeam.Blue, true, 1.4f);
                Check(director.IsFading && lobby.volume > .1f && lobby.volume < .55f,
                    "crossfade progresses with unscaled time while the battle is paused");
                director.Tick(match, PrototypeTeam.Blue, true, 1.5f);
                Check(PlayingCount(voices) == 0 && director.CurrentState == PrototypeMusicState.Silent,
                    "ordinary battle fades the lobby out and adds no fourth track");
                AudioListener.pause = listenerPause;
                Time.timeScale = timeScale;

                var assault = new PrototypeMatch();
                assault.ConfigureSolo(PrototypeGameMode.Breakthrough);
                typeof(PrototypeMatch).GetField("<SectorIndex>k__BackingField",
                    BindingFlags.Instance | BindingFlags.NonPublic).SetValue(assault,
                        assault.Points.Count - 1);
                assault.ActiveObjective.Control = 50f;
                assault.ActiveObjective.BlueCount = 4;
                director.Tick(assault, PrototypeTeam.Blue, true, 0f);
                director.Tick(assault, PrototypeTeam.Blue, true, .4f);
                Check(director.CurrentState == PrototypeMusicState.Silent,
                    "a final capture still needs a short stable confirmation");
                director.Tick(assault, PrototypeTeam.Blue, true, .2f);
                Check(director.CurrentState == PrototypeMusicState.NearVictory,
                    "unopposed final capture music begins before a multi-attacker capture completes");
                director.Tick(match, PrototypeTeam.Blue, true, 0f);
                director.Tick(match, PrototypeTeam.Blue, true, 3.1f);
                director.Tick(match, PrototypeTeam.Blue, true, 3f);
                Check(PlayingCount(voices) == 0, "returning to ordinary battle releases the final capture cue");

                match.SetNetworkTickets(80f, 20f);
                director.Tick(match, PrototypeTeam.Blue, true, 0f);
                director.Tick(match, PrototypeTeam.Blue, true, 2.9f);
                Check(director.CurrentState == PrototypeMusicState.Silent,
                    "a near-victory cue requires a stable three-second advantage");
                match.SetNetworkTickets(80f, 60f);
                director.Tick(match, PrototypeTeam.Blue, true, .2f);
                Check(director.CurrentState == PrototypeMusicState.Silent,
                    "a recovered brief warning is cancelled before playback");
                match.SetNetworkTickets(80f, 20f);
                director.Tick(match, PrototypeTeam.Blue, true, 0f);
                director.Tick(match, PrototypeTeam.Blue, true, 3.1f);
                director.Tick(match, PrototypeTeam.Blue, true, 3f);
                Check(Playing(voices, "victory-near") != null,
                    "confirmed advantage plays the victory music");
                director.SetMusicVolume(0f);
                match.SetNetworkTickets(20f, 80f);
                director.Tick(match, PrototypeTeam.Blue, true, 0f);
                director.Tick(match, PrototypeTeam.Blue, true, 3.1f);
                director.Tick(match, PrototypeTeam.Blue, true, 1f);
                Check(AllMuted(voices), "all fading voices remain silent at music volume zero");
                director.SetMusicVolume(.55f);
                director.Tick(match, PrototypeTeam.Blue, true, 3f);
                Check(Playing(voices, "defeat-near") != null,
                    "confirmed danger crossfades to the defeat music");

                // Rapid menu/preview requests exercise an interrupted two-voice
                // fade and a third cue queued until a voice becomes silent.
                director.SetEditorPreview(PrototypeMusicState.Lobby);
                director.Tick(match, PrototypeTeam.Blue, true, 0f);
                director.Tick(match, PrototypeTeam.Blue, true, .7f);
                float before0 = voices[0].volume, before1 = voices[1].volume;
                director.SetEditorPreview(PrototypeMusicState.NearVictory);
                director.Tick(match, PrototypeTeam.Blue, true, 0f);
                Check(PlayingCount(voices) == 2 &&
                    Mathf.Approximately(before0, voices[0].volume) &&
                    Mathf.Approximately(before1, voices[1].volume),
                    "a third cue does not cut either audible source or jump its gain");
                director.Tick(match, PrototypeTeam.Blue, true, 2.2f);
                director.Tick(match, PrototypeTeam.Blue, true, 3f);
                Check(PlayingCount(voices) == 1 && Playing(voices, "victory-near") != null,
                    "a queued third cue completes with exactly two reusable voices");
                director.SetEditorPreview(null);
                director.enabled = false;
                Check(PlayingCount(voices) == 0 && AllMuted(voices),
                    "disabling the director releases all looping audio");
                director.enabled = true;
                director.Tick(match, PrototypeTeam.Blue, false, 3f);
                Check(holder.GetComponents<AudioSource>().Length == 2 &&
                    Playing(voices, "lobby") != null, "re-enable recovers without duplicate voices");
                director.enabled = false;
                AudioClip[] loaded = (AudioClip[])typeof(PrototypeMusicDirector).GetField("clips",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(director);
                loaded[(int)PrototypeMusicState.Lobby] = null;
                director.enabled = true;
                director.Tick(match, PrototypeTeam.Blue, false, 3f);
                Check(director.CurrentState == PrototypeMusicState.Lobby && PlayingCount(voices) == 0,
                    "an unavailable clip falls back to silence without a null playback failure");
                director.Dispose();
                director.Dispose();
                Check(PlayingCount(voices) == 0, "repeated disposal is safe and stops audio");
                Debug.Log("IRONFRONT adaptive music PASS: three stereo loops, both-team " +
                    "Conquest/Breakthrough and 800-ticket rules, result/lobby, hysteresis, " +
                    "three-second ticket and half-second final capture confirmation, paused crossfade, " +
                    "mute continuity, rapid three-cue " +
                    "queue and two-source lifecycle");
            }
            catch (Exception error) { Debug.LogError("IRONFRONT adaptive music FAIL: " + error); }
            finally
            {
                AudioListener.volume = masterVolume;
                AudioListener.pause = listenerPause;
                Time.timeScale = timeScale;
                if (holder != null) UnityEngine.Object.DestroyImmediate(holder);
                if (hadVolume) PlayerPrefs.SetFloat(PrototypeMusicDirector.VolumeKey, savedVolume);
                else PlayerPrefs.DeleteKey(PrototypeMusicDirector.VolumeKey);
                PlayerPrefs.Save();
            }
        }

        private static void VerifyRules()
        {
            var match = new PrototypeMatch();
            match.ConfigureSolo(PrototypeGameMode.Conquest);
            Check(State(match, PrototypeTeam.Blue) == PrototypeMusicState.Silent &&
                State(match, PrototypeTeam.Red) == PrototypeMusicState.Silent,
                "ordinary Conquest starts without an imminent-result cue");
            match.SetNetworkTickets(80f, 25f);
            Check(State(match, PrototypeTeam.Blue) == PrototypeMusicState.NearVictory &&
                State(match, PrototypeTeam.Red) == PrototypeMusicState.NearDefeat,
                "Conquest advantage follows the actual player team");
            match.SetNetworkTickets(25f, 80f);
            Check(State(match, PrototypeTeam.Blue) == PrototypeMusicState.NearDefeat &&
                State(match, PrototypeTeam.Red) == PrototypeMusicState.NearVictory,
                "low blue tickets reverse for a red player");
            match.SetNetworkTickets(80f, 30f);
            Check(State(match, PrototypeTeam.Blue, PrototypeMusicState.NearVictory) ==
                PrototypeMusicState.NearVictory && State(match, PrototypeTeam.Blue) ==
                PrototypeMusicState.Silent, "25-to-32-percent ticket hysteresis prevents edge chatter");
            match.SetNetworkTickets(80f, 33f);
            Check(State(match, PrototypeTeam.Blue, PrototypeMusicState.NearVictory) ==
                PrototypeMusicState.Silent, "recovered tickets release a victory warning");
            match.SetNetworkTickets(20f, 21f);
            Check(State(match, PrototypeTeam.Red, PrototypeMusicState.NearDefeat) ==
                PrototypeMusicState.NearDefeat && State(match, PrototypeTeam.Blue,
                    PrototypeMusicState.NearVictory) == PrototypeMusicState.NearVictory,
                "one-ticket double-critical differences preserve the current cue");
            match.Points[0].Owner = PrototypeTeam.Red;
            Check(State(match, PrototypeTeam.Blue) == PrototypeMusicState.NearDefeat &&
                State(match, PrototypeTeam.Red) == PrototypeMusicState.NearVictory,
                "double-critical tied tickets use the actual objective bleed direction");
            match.Points[0].Owner = null;
            match.SetNetworkTickets(700f, 180f);
            Check(PrototypeMusicDirector.EvaluateState(match, PrototypeTeam.Blue, true,
                PrototypeMusicState.Silent, 800f) == PrototypeMusicState.NearVictory,
                "LAN room ticket capacity overrides the local default of 100");
            match.SetNetworkTickets(10f, 0f);
            Check(State(match, PrototypeTeam.Blue) == PrototypeMusicState.NearVictory &&
                State(match, PrototypeTeam.Red) == PrototypeMusicState.NearDefeat &&
                PrototypeMusicDirector.EvaluateState(match, PrototypeTeam.Blue, false) ==
                PrototypeMusicState.Lobby, "winner overrides danger and lobby overrides stale results");

            match.ConfigureSolo(PrototypeGameMode.Breakthrough);
            Check(State(match, PrototypeTeam.Blue) == PrototypeMusicState.Silent &&
                State(match, PrototypeTeam.Red) == PrototypeMusicState.Silent,
                "normal Breakthrough does not misread infinite defender tickets");
            match.SetNetworkTickets(25f, 100f);
            Check(State(match, PrototypeTeam.Blue) == PrototypeMusicState.NearDefeat &&
                State(match, PrototypeTeam.Red) == PrototypeMusicState.NearVictory,
                "attacker shortage reverses correctly for the defender");
            match.SetNetworkTickets(100f, 100f);
            typeof(PrototypeMatch).GetField("<SectorIndex>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic).SetValue(match, match.Points.Count - 1);
            PrototypeCapturePoint final = match.ActiveObjective;
            final.Control = 49f;
            final.BlueCount = 1;
            Check(State(match, PrototypeTeam.Blue) == PrototypeMusicState.Silent,
                "entering the final sector alone is not near victory");
            final.Control = 50f;
            final.BlueCount = 0;
            Check(State(match, PrototypeTeam.Blue) == PrototypeMusicState.Silent,
                "abandoned capture progress does not announce imminent victory");
            final.BlueCount = 1;
            final.RedCount = 1;
            final.Contested = true;
            Check(State(match, PrototypeTeam.Blue) == PrototypeMusicState.Silent,
                "a contested final objective is not imminently captured");
            final.RedCount = 0;
            final.Contested = false;
            Check(State(match, PrototypeTeam.Blue) == PrototypeMusicState.NearVictory &&
                State(match, PrototypeTeam.Red) == PrototypeMusicState.NearDefeat,
                "actively capturing the final objective triggers opposite team cues");
            final.Control = 35f;
            Check(State(match, PrototypeTeam.Blue, PrototypeMusicState.NearVictory) ==
                PrototypeMusicState.NearVictory, "final capture progress has a separate release threshold");
            final.Control = 80f;
            match.SetNetworkTickets(20f, 100f);
            Check(State(match, PrototypeTeam.Blue) == PrototypeMusicState.NearDefeat &&
                State(match, PrototypeTeam.Red) == PrototypeMusicState.NearVictory,
                "attacker ticket crisis has a defined priority over simultaneous final capture");
            final.Owner = PrototypeTeam.Blue;
            match.Tick(0f, null, Array.Empty<PrototypeBot>());
            Check(match.Winner == PrototypeTeam.Blue && State(match, PrototypeTeam.Blue) ==
                PrototypeMusicState.NearVictory && State(match, PrototypeTeam.Red) ==
                PrototypeMusicState.NearDefeat, "final capture result overrides the prior ticket warning");
        }

        private static PrototypeMusicState State(PrototypeMatch match, PrototypeTeam team,
            PrototypeMusicState previous = PrototypeMusicState.Silent) =>
            PrototypeMusicDirector.EvaluateState(match, team, true, previous);

        [MenuItem("IRONFRONT/Music Preview/Lobby", true)]
        [MenuItem("IRONFRONT/Music Preview/Near Victory", true)]
        [MenuItem("IRONFRONT/Music Preview/Near Defeat", true)]
        [MenuItem("IRONFRONT/Music Preview/Resume Adaptive Music", true)]
        private static bool CanPreview() => EditorApplication.isPlaying;

        [MenuItem("IRONFRONT/Music Preview/Lobby")]
        public static void PreviewLobby() => Preview(PrototypeMusicState.Lobby);
        [MenuItem("IRONFRONT/Music Preview/Near Victory")]
        public static void PreviewVictory() => Preview(PrototypeMusicState.NearVictory);
        [MenuItem("IRONFRONT/Music Preview/Near Defeat")]
        public static void PreviewDefeat() => Preview(PrototypeMusicState.NearDefeat);
        [MenuItem("IRONFRONT/Music Preview/Resume Adaptive Music")]
        public static void ResumeAdaptive() => Preview(null);

        private static void Preview(PrototypeMusicState? state)
        {
            PrototypeRuntime game = UnityEngine.Object.FindAnyObjectByType<PrototypeRuntime>();
            if (game?.Music == null) return;
            game.Music.SetEditorPreview(state);
            Debug.Log("IRONFRONT music preview: " + (state.HasValue ? state.Value.ToString() : "adaptive"));
        }

        private static AudioSource Playing(AudioSource[] voices, string clipName)
        {
            foreach (AudioSource voice in voices)
                if (voice.isPlaying && voice.clip != null && voice.clip.name == clipName) return voice;
            return null;
        }
        private static int PlayingCount(AudioSource[] voices)
        {
            int count = 0;
            foreach (AudioSource voice in voices) if (voice.isPlaying) count++;
            return count;
        }
        private static bool AllMuted(AudioSource[] voices)
        {
            foreach (AudioSource voice in voices) if (voice.volume > .0001f) return false;
            return true;
        }
        private static void Check(bool value, string reason)
        {
            if (!value) throw new InvalidOperationException(reason);
        }
    }
}
