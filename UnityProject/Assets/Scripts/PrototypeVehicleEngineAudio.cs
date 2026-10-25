using UnityEngine;

namespace Ironfront.UnityPrototype
{
    public enum PrototypeEngineSound { HeavyDiesel, LightDiesel, Motorcycle, Rotor, Jet }

    // Two persistent mono 3D beds blend intake/exhaust load. Nothing is allocated
    // per frame; combat one-shots and the independent music volume stay separate.
    public sealed class PrototypeVehicleEngineAudio : MonoBehaviour
    {
        private PrototypeRuntime runtime;
        private IPrototypeVehicle vehicle;
        private AudioSource idle, load;
        private GameObject audioRoot;
        private float topSpeed, power, gain;
        private Vector3 previousPosition;
        private float replicaLastMotion, replicaMotionUntil, replicaSpeed;
        private bool paused;
        public PrototypeEngineSound Profile { get; private set; }
        public float EngineLoad => power;
        public AudioSource IdleSource => idle;
        public AudioSource LoadSource => load;

        public static PrototypeVehicleEngineAudio Attach(GameObject root, PrototypeRuntime game,
            IPrototypeVehicle owner, PrototypeEngineSound profile, float maxSpeed)
        {
            var audio = root.GetComponent<PrototypeVehicleEngineAudio>();
            if (audio == null) audio = root.AddComponent<PrototypeVehicleEngineAudio>();
            audio.Initialize(game, owner, profile, maxSpeed);
            return audio;
        }

        public void Initialize(PrototypeRuntime game, IPrototypeVehicle owner,
            PrototypeEngineSound profile, float maxSpeed)
        {
            Stop();
            runtime = game; vehicle = owner; Profile = profile; topSpeed = Mathf.Max(1f, maxSpeed);
            previousPosition = transform.position;
            replicaLastMotion = Time.time; replicaMotionUntil = replicaSpeed = 0f;
            string name = profile == PrototypeEngineSound.HeavyDiesel ? "heavy-diesel" :
                profile == PrototypeEngineSound.LightDiesel ? "light-diesel" :
                profile == PrototypeEngineSound.Motorcycle ? "motorcycle" :
                profile == PrototypeEngineSound.Rotor ? "rotor" : "jet";
            if (idle == null) idle = CreateSource();
            if (load == null) load = CreateSource();
            idle.clip = Resources.Load<AudioClip>("Audio/Engines/" + name + "-idle");
            load.clip = Resources.Load<AudioClip>("Audio/Engines/" + name + "-load");
            ConfigureRange(idle); ConfigureRange(load);
            if (idle.clip == null || load.clip == null)
                Debug.LogWarning("IRONFRONT missing engine loops: " + name);
        }

        private AudioSource CreateSource()
        {
            // CombatFeedback reuses the hull's first AudioSource for shots.
            // Engine beds have a dedicated child so shooting cannot retune or
            // stop an engine voice, and a paused engine cannot swallow a shot.
            if (audioRoot == null)
            {
                audioRoot = new GameObject("Dedicated vehicle engine loops");
                audioRoot.transform.SetParent(transform, false);
                audioRoot.transform.localPosition = Vector3.up;
            }
            var source = audioRoot.AddComponent<AudioSource>();
            source.playOnAwake = false; source.loop = true; source.spatialBlend = 1f;
            ConfigureRange(source);
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.dopplerLevel = .24f; source.spread = 18f;
            source.priority = 155; source.volume = 0f;
            return source;
        }
        private void ConfigureRange(AudioSource source)
        {
            source.minDistance = Profile == PrototypeEngineSound.Rotor || Profile == PrototypeEngineSound.Jet ? 12f : 5f;
            source.maxDistance = Profile == PrototypeEngineSound.Rotor || Profile == PrototypeEngineSound.Jet ? 230f : 95f;
        }

        private void LateUpdate()
        {
            if (runtime == null || vehicle == null || !runtime.MatchStarted ||
                runtime.Match.Winner.HasValue || !vehicle.Alive)
            { Stop(); return; }
            if (Time.timeScale <= 0f)
            {
                if (!paused) { idle?.Pause(); load?.Pause(); paused = true; }
                return;
            }
            if (paused) { idle?.UnPause(); load?.UnPause(); paused = false; }
            bool running = vehicle is PrototypeCombinedArmsVehicle combined ? combined.EngineRunning :
                vehicle is PrototypeTankVehicle tank ? tank.EngineRunning :
                vehicle is PrototypeScoutVehicle scout ? scout.EngineRunning :
                vehicle is PrototypeTransportVehicle transport && transport.EngineRunning;
            float requested = Mathf.Clamp01(Mathf.Abs(vehicle.Speed) / topSpeed);
            if (runtime.IsNetworkReplica)
            {
                // Replica snapshots intentionally zero gameplay Speed. Infer
                // audible motion without granting client simulation authority.
                float distance = Vector3.Distance(previousPosition,transform.position);
                if (distance > .003f)
                {
                    if (distance < 25f)
                    {
                        replicaSpeed = Mathf.Min(topSpeed * 1.5f,distance / Mathf.Max(.016f,Time.time-replicaLastMotion));
                        replicaMotionUntil = Time.time + .4f;
                    }
                    replicaLastMotion = Time.time;
                }
                if (Time.time < replicaMotionUntil)
                { requested = Mathf.Max(requested, Mathf.Clamp01(replicaSpeed/topSpeed)); running = true; }
            }
            previousPosition = transform.position;
            if (vehicle.Driver != null && Cursor.lockState == CursorLockMode.Locked)
            {
                requested = Mathf.Max(requested, Mathf.Abs(Input.GetAxisRaw("Vertical")) * .85f);
                if (Profile == PrototypeEngineSound.Rotor && (Input.GetKey(KeyCode.Space) ||
                    Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))
                    requested = Mathf.Max(requested, .7f);
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) requested = 1f;
            }
            Step(running, requested, Mathf.Min(Time.deltaTime, .1f));
        }

        // Explicit engine step lets smoke checks validate throttle, release and
        // source reuse through the actual mixer path, without fake AudioSource state.
        public void Step(bool running, float throttleLoad, float dt)
        {
            if (idle == null || load == null || idle.clip == null || load.clip == null) return;
            dt = Mathf.Clamp(dt, 0f, .1f);
            if (paused) { idle.UnPause(); load.UnPause(); paused = false; }
            power = Mathf.Lerp(power, Mathf.Clamp01(throttleLoad), 1f - Mathf.Exp(-dt * 4f));
            gain = Mathf.MoveTowards(gain, running ? 1f : 0f, dt * (running ? 2f : 3f));
            if (running && !idle.isPlaying)
            {
                idle.Play(); load.Play();
                // Deterministic phase offset avoids two nearby vehicles beating
                // with identical samples; their two layers retain the same time.
                idle.time = load.time = Mathf.Repeat(Mathf.Abs(transform.position.x * .071f +
                    transform.position.z * .019f), Mathf.Max(.1f, idle.clip.length - .05f));
            }
            float volume = Profile == PrototypeEngineSound.Rotor ? .37f :
                Profile == PrototypeEngineSound.Jet ? .34f : Profile == PrototypeEngineSound.HeavyDiesel ? .3f : .26f;
            idle.volume = volume * gain * Mathf.Sqrt(1f - power * .82f);
            load.volume = volume * gain * Mathf.Sqrt(power) * .82f;
            float pitch = Profile == PrototypeEngineSound.Rotor ? Mathf.Lerp(.96f, 1.08f, power) :
                Profile == PrototypeEngineSound.Jet ? Mathf.Lerp(.86f, 1.24f, power) :
                Profile == PrototypeEngineSound.Motorcycle ? Mathf.Lerp(.75f, 1.9f, power) :
                Mathf.Lerp(.78f, 1.52f, power);
            idle.pitch = load.pitch = pitch;
            if (!running && gain <= 0f) Stop();
        }

        public void Stop()
        {
            idle?.Stop(); load?.Stop();
            if (idle != null) idle.volume = 0f;
            if (load != null) load.volume = 0f;
            power = gain = 0f; paused = false;
            previousPosition = transform.position;
        }
        private void OnDisable() => Stop();
        private void OnDestroy()
        {
            Stop();
            if (audioRoot != null) Destroy(audioRoot);
        }
    }
}
