using System.Collections.Generic;
using UnityEngine;

namespace Ironfront.UnityPrototype
{
    public enum PrototypePlayerCue { Hit, Kill, LockTick, LockReady, LockLost, Zoom }

    public sealed class PrototypePlayerAudio : MonoBehaviour
    {
        private static PrototypePlayerAudio current;
        private static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        private PrototypeRuntime runtime;
        private PrototypePlayer player;
        private CharacterController controller;
        private AudioSource actionSource, hudSource;
        private Vector3 previousPosition;
        private IPrototypeVehicle previousVehicle;
        private bool previousCrouched, previousProne;
        private float stride, sinceStep;
        private bool disposed;
        public int ActionCueCount { get; private set; }
        public int HudCueCount { get; private set; }
        public int FootstepCount { get; private set; }
        public string LastActionCue { get; private set; }
        public PrototypePlayerCue LastCue { get; private set; }
        public AudioSource HudSource => hudSource;
        public AudioSource ActionSource => actionSource;

        public void Initialize(PrototypeRuntime game, PrototypePlayer localPlayer)
        {
            disposed = false; runtime = game; player = localPlayer;
            controller = player.GetComponent<CharacterController>(); current = this;
            if (actionSource == null) actionSource = Source("Local movement and equipment audio", .55f);
            if (hudSource == null) hudSource = Source("Local combat and seeker feedback audio", .45f);
            foreach (string name in new[] { "StepWalk", "StepRun", "StepCrouch", "StepProne", "Crouch", "Stand",
                "Prone", "Rise", "Board", "Hit", "Kill", "LockTick", "LockReady", "LockLost", "Zoom" })
                if (!clips.ContainsKey(name)) clips[name] = Resources.Load<AudioClip>("Audio/PlayerCues/" + name);
            ResetBaseline();
        }
        private AudioSource Source(string name, float volume)
        {
            var anchor = new GameObject(name); anchor.transform.SetParent(transform, false);
            var source = anchor.AddComponent<AudioSource>(); source.playOnAwake = false;
            source.spatialBlend = 0f; source.volume = volume; source.loop = false;
            source.ignoreListenerPause = false; return source;
        }
        private bool Active => !disposed && runtime != null && player != null && runtime.MatchStarted && player.Alive &&
            !runtime.PauseOpen && !runtime.SupportMenuOpen && !runtime.Match.Winner.HasValue &&
            (runtime.Frontend == null || !runtime.Frontend.IsOpen);
        public static void PlayCue(PrototypePlayerCue cue)
        {
            if (current == null || !current.Active || !current.isActiveAndEnabled) return;
            string name = cue.ToString();
            if (!clips.TryGetValue(name, out AudioClip clip) || clip == null) return;
            current.hudSource.PlayOneShot(clip); current.LastCue = cue; current.HudCueCount++;
        }
        private void LateUpdate() => Tick(Time.deltaTime);
        public void Tick(float dt)
        {
            if (player == null) return;
            if (!Active || float.IsNaN(dt) || float.IsInfinity(dt) || dt < 0f)
            {
                if (actionSource != null) actionSource.Stop();
                if (hudSource != null) hudSource.Stop(); ResetBaseline(); return;
            }
            Vector3 position = player.transform.position;
            IPrototypeVehicle vehicle = player.CurrentVehicle;
            if (previousVehicle == null && vehicle != null) Action("Board");
            if (vehicle == null && previousVehicle == null)
            {
                if (player.IsProne != previousProne) Action(player.IsProne ? "Prone" : "Rise");
                else if (player.IsCrouched != previousCrouched) Action(player.IsCrouched ? "Crouch" : "Stand");
            }
            // State changes are discrete successful actions, even on a stalled
            // frame. Skip only movement accumulation so a hitch cannot create
            // delayed footsteps or cut off valid combat/seeker feedback.
            if (dt == 0f || dt > .2f) { ResetBaseline(); return; }
            Vector3 displacement = position - previousPosition; displacement.y = 0f;
            float distance = displacement.magnitude;
            sinceStep += dt;
            bool moved = vehicle == null && previousVehicle == null && controller != null && controller.enabled &&
                controller.isGrounded && !player.IsSliding && distance > .008f && distance <= Mathf.Max(.75f, dt * 24f);
            if (!moved) stride = 0f;
            else
            {
                stride += distance;
                float length = player.IsProne ? 1.05f : player.IsCrouched ? 1.7f : player.IsSprinting ? 3.55f : 3.4f;
                float minimum = player.IsProne ? .40f : player.IsCrouched ? .34f : player.IsSprinting ? .20f : .28f;
                if (stride >= length && sinceStep >= minimum)
                {
                    Action(player.IsProne ? "StepProne" : player.IsCrouched ? "StepCrouch" : player.IsSprinting ? "StepRun" : "StepWalk");
                    FootstepCount++; stride = 0f; sinceStep = 0f;
                }
            }
            previousPosition = position; previousVehicle = vehicle;
            previousCrouched = player.IsCrouched; previousProne = player.IsProne;
        }
        private void Action(string name)
        {
            if (!clips.TryGetValue(name, out AudioClip clip) || clip == null) return;
            actionSource.pitch = 1f + ((ActionCueCount % 3) - 1) * .025f;
            actionSource.PlayOneShot(clip, name == "StepRun" ? 1f : name == "StepCrouch" ? .65f :
                name == "StepProne" ? .45f : .85f); ActionCueCount++; LastActionCue = name;
        }
        public void ResetBaseline()
        {
            if (player == null) return;
            previousPosition = player.transform.position; previousVehicle = player.CurrentVehicle;
            previousCrouched = player.IsCrouched; previousProne = player.IsProne;
            stride = sinceStep = 0f;
        }
        private void OnDisable()
        {
            actionSource?.Stop(); hudSource?.Stop(); ResetBaseline();
        }
        private void OnDestroy()
        {
            Dispose();
        }
        public void Reset()
        {
            actionSource?.Stop(); hudSource?.Stop(); ResetBaseline();
            ActionCueCount = HudCueCount = FootstepCount = 0; LastActionCue = "";
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (current == this) current = null;
            actionSource?.Stop(); hudSource?.Stop();
            if (actionSource != null) Destroy(actionSource.gameObject);
            if (hudSource != null) Destroy(hudSource.gameObject);
            actionSource = hudSource = null;
        }
    }
}
