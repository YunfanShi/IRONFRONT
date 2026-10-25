using UnityEngine;

namespace Ironfront.UnityPrototype
{
    public enum PrototypeMusicState { Silent, Lobby, NearVictory, NearDefeat }

    // Two local, non-spatial music voices. The existing AudioListener volume
    // remains the master control for music and all battlefield sound effects.
    public sealed class PrototypeMusicDirector : MonoBehaviour
    {
        public const string VolumeKey = "ironfront.unity.musicVolume";
        public const float DefaultVolume = .55f;
        public const float ConfirmationSeconds = 3f;
        public const float FinalCaptureConfirmationSeconds = .5f;
        public const float CrossfadeSeconds = 2.8f;
        private const float EnterFraction = .25f;
        private const float ExitFraction = .32f;
        private readonly AudioSource[] sources = new AudioSource[2];
        private readonly AudioClip[] clips = new AudioClip[4];
        private readonly float[] gains = new float[2];
        private readonly float[] from = new float[2];
        private readonly float[] to = new float[2];
        private bool initialized;
        private bool disposed;
        private bool fading;
        private float fadeElapsed;
        private PrototypeMusicState candidate = PrototypeMusicState.Silent;
        private float candidateElapsed;
        private PrototypeMusicState? queued;
#if UNITY_EDITOR
        private PrototypeMusicState? preview;
#endif

        public float MusicVolume { get; private set; } = DefaultVolume;
        public PrototypeMusicState CurrentState { get; private set; } = PrototypeMusicState.Silent;
        public bool IsFading => fading;

        public void Initialize()
        {
            if (initialized || disposed) return;
            initialized = true;
            MusicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, DefaultVolume));
            for (int i = 0; i < sources.Length; i++)
            {
                AudioSource source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = true;
                source.spatialBlend = 0f;
                source.ignoreListenerPause = true;
                source.priority = 128;
                source.volume = 0f;
                sources[i] = source;
            }
            LoadClip(PrototypeMusicState.Lobby, "lobby");
            LoadClip(PrototypeMusicState.NearVictory, "victory-near");
            LoadClip(PrototypeMusicState.NearDefeat, "defeat-near");
        }

        private void LoadClip(PrototypeMusicState state, string filename)
        {
            AudioClip clip = Resources.Load<AudioClip>("Audio/Music/" + filename);
            clips[(int)state] = clip;
            if (clip != null) clip.LoadAudioData();
            else Debug.LogWarning("IRONFRONT music clip is missing: " + filename +
                ". This music state will be silent.", this);
        }

        public void SetMusicVolume(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return;
            MusicVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(VolumeKey, MusicVolume);
            PlayerPrefs.Save();
            ApplyVolumes();
        }

        // No player alive, frontend-open, or pause input belongs here: those
        // screens still belong to the same battle, including redeployment.
        public void Tick(PrototypeMatch match, PrototypeTeam playerTeam, bool battleStarted,
            float unscaledDeltaTime, float ticketLimit = 0f)
        {
            if (disposed || !isActiveAndEnabled) return;
            Initialize();
            float dt = Mathf.Max(0f, unscaledDeltaTime);
            if (float.IsNaN(dt) || float.IsInfinity(dt)) return;
            PrototypeMusicState desired = EvaluateState(match, playerTeam, battleStarted,
                CurrentState, ticketLimit);
#if UNITY_EDITOR
            if (preview.HasValue) desired = preview.Value;
#endif
            bool immediate = !battleStarted || (match != null && match.Winner.HasValue) ||
                (CurrentState == PrototypeMusicState.Lobby && desired == PrototypeMusicState.Silent);
#if UNITY_EDITOR
            if (preview.HasValue) immediate = true;
#endif
            if (desired != candidate)
            {
                candidate = desired;
                candidateElapsed = 0f;
            }
            else candidateElapsed += dt;
            float confirmation = ConfirmationSeconds;
            // Several attackers can finish the final 50 control points in
            // less than two seconds. Their unopposed capture already provides
            // strong evidence, so let that music arrive before the result.
            if (match != null && match.Mode == PrototypeGameMode.Breakthrough &&
                match.SectorIndex == match.Points.Count - 1 &&
                desired == (playerTeam == PrototypeTeam.Blue ? PrototypeMusicState.NearVictory :
                    PrototypeMusicState.NearDefeat) && match.ActiveObjective != null &&
                match.ActiveObjective.BlueCount > 0 && match.ActiveObjective.RedCount == 0 &&
                !match.ActiveObjective.Contested)
                confirmation = FinalCaptureConfirmationSeconds;
            if (desired != CurrentState && (immediate || candidateElapsed >= confirmation))
                RequestState(desired);
            AdvanceFade(dt);
        }

        // Pure rules: safe to test without audio playback or a scene. Control
        // goes from -100 (red) to +100 (blue), so +50 is 75% of a full assault.
        public static PrototypeMusicState EvaluateState(PrototypeMatch match,
            PrototypeTeam playerTeam, bool battleStarted,
            PrototypeMusicState currentState = PrototypeMusicState.Silent, float ticketLimit = 0f)
        {
            if (!battleStarted || match == null) return PrototypeMusicState.Lobby;
            if (match.Winner.HasValue)
                return match.Winner.Value == playerTeam ? PrototypeMusicState.NearVictory :
                    PrototypeMusicState.NearDefeat;
            float limit = Mathf.Max(1f, ticketLimit > 0f ? ticketLimit : match.TicketLimit);
            if (match.Mode == PrototypeGameMode.Breakthrough)
            {
                bool wasAttackerLosing = playerTeam == PrototypeTeam.Blue ?
                    currentState == PrototypeMusicState.NearDefeat :
                    currentState == PrototypeMusicState.NearVictory;
                bool wasAttackerWinning = playerTeam == PrototypeTeam.Blue ?
                    currentState == PrototypeMusicState.NearVictory :
                    currentState == PrototypeMusicState.NearDefeat;
                bool lowAttackerTickets = match.BlueTickets <= limit *
                    (wasAttackerLosing ? ExitFraction : EnterFraction);
                PrototypeCapturePoint point = match.ActiveObjective;
                bool finalCapture = match.SectorIndex == match.Points.Count - 1 && point != null &&
                    point.Control >= (wasAttackerWinning ? 30f : 50f) &&
                    !point.Contested && point.BlueCount > 0 && point.RedCount == 0;
                // Attacker survival wins a simultaneous warning: red tickets
                // are infinite in this mode and must never signal red defeat.
                if (lowAttackerTickets)
                    return playerTeam == PrototypeTeam.Blue ? PrototypeMusicState.NearDefeat :
                        PrototypeMusicState.NearVictory;
                if (finalCapture)
                    return playerTeam == PrototypeTeam.Blue ? PrototypeMusicState.NearVictory :
                        PrototypeMusicState.NearDefeat;
                return PrototypeMusicState.Silent;
            }

            float ours = playerTeam == PrototypeTeam.Blue ? match.BlueTickets : match.RedTickets;
            float theirs = playerTeam == PrototypeTeam.Blue ? match.RedTickets : match.BlueTickets;
            bool oursLow = ours <= limit * (currentState == PrototypeMusicState.NearDefeat ?
                ExitFraction : EnterFraction);
            bool theirsLow = theirs <= limit * (currentState == PrototypeMusicState.NearVictory ?
                ExitFraction : EnterFraction);
            if (oursLow && !theirsLow) return PrototypeMusicState.NearDefeat;
            if (theirsLow && !oursLow) return PrototypeMusicState.NearVictory;
            if (!oursLow && !theirsLow) return PrototypeMusicState.Silent;
            float margin = limit * .05f;
            if (ours + margin < theirs) return PrototypeMusicState.NearDefeat;
            if (theirs + margin < ours) return PrototypeMusicState.NearVictory;
            int oursOwned = 0;
            int theirsOwned = 0;
            foreach (PrototypeCapturePoint point in match.Points)
            {
                if (!point.Owner.HasValue) continue;
                if (point.Owner.Value == playerTeam) oursOwned++;
                else theirsOwned++;
            }
            if (oursOwned < theirsOwned) return PrototypeMusicState.NearDefeat;
            if (oursOwned > theirsOwned) return PrototypeMusicState.NearVictory;
            // Both sides are close to zero with no clear advantage. Preserve
            // an existing cue rather than swap it for a one-ticket difference.
            return currentState == PrototypeMusicState.NearVictory ||
                currentState == PrototypeMusicState.NearDefeat ? currentState :
                PrototypeMusicState.Silent;
        }

        private void RequestState(PrototypeMusicState state)
        {
            CurrentState = state;
            AudioClip clip = clips[(int)state];
            int index = -1;
            if (clip != null)
                for (int i = 0; i < sources.Length; i++)
                    if (sources[i].clip == clip && sources[i].isPlaying) index = i;
            if (clip != null && index < 0)
            {
                for (int i = 0; i < sources.Length; i++)
                    if (gains[i] < .0001f && !sources[i].isPlaying) index = i;
                // A third cue requested halfway through a fade waits for a
                // free voice. Neither audible track is stopped abruptly.
                if (index < 0)
                {
                    queued = state;
                    return;
                }
                sources[index].clip = clip;
                sources[index].volume = 0f;
                sources[index].Play();
            }
            queued = null;
            fadeElapsed = 0f;
            fading = true;
            for (int i = 0; i < sources.Length; i++)
            {
                from[i] = gains[i];
                to[i] = i == index ? 1f : 0f;
            }
        }

        private void AdvanceFade(float dt)
        {
            if (!fading) return;
            fadeElapsed += dt;
            float t = Mathf.Clamp01(fadeElapsed / CrossfadeSeconds);
            for (int i = 0; i < sources.Length; i++)
                gains[i] = Mathf.Sqrt(Mathf.Lerp(from[i] * from[i], to[i] * to[i], t));
            ApplyVolumes();
            if (t < 1f) return;
            fading = false;
            for (int i = 0; i < sources.Length; i++)
                if (to[i] == 0f)
                {
                    sources[i].Stop();
                    sources[i].clip = null;
                }
            if (queued.HasValue)
            {
                PrototypeMusicState next = queued.Value;
                queued = null;
                RequestState(next);
            }
        }

        private void ApplyVolumes()
        {
            for (int i = 0; i < sources.Length; i++)
                if (sources[i] != null) sources[i].volume = gains[i] * MusicVolume;
        }

#if UNITY_EDITOR
        public void SetEditorPreview(PrototypeMusicState? state)
        {
            preview = state;
            candidateElapsed = 0f;
        }
#endif

        public void Dispose()
        {
            if (disposed) return;
            StopVoices();
            disposed = true;
        }

        private void StopVoices()
        {
            for (int i = 0; i < sources.Length; i++)
            {
                if (sources[i] != null)
                {
                    sources[i].Stop();
                    sources[i].clip = null;
                    sources[i].volume = 0f;
                }
                gains[i] = 0f;
            }
            fading = false;
            queued = null;
            candidateElapsed = 0f;
            candidate = PrototypeMusicState.Silent;
            CurrentState = PrototypeMusicState.Silent;
        }

        private void OnDisable() { StopVoices(); }
        private void OnDestroy() { Dispose(); }
    }
}
