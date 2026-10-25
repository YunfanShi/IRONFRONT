using UnityEngine;

namespace Ironfront.UnityPrototype
{
    public enum PrototypeVehicleImpactKind { Ricochet, MachineGun, HeavyImpact }

    // Two dedicated, reusable voices per hull. An automatic weapon replaces
    // its previous metallic tail instead of stacking dozens of one-shots.
    public sealed class PrototypeVehicleImpactAudio : MonoBehaviour
    {
        private static readonly AudioClip[] Clips = new AudioClip[3];
        private PrototypeRuntime runtime;
        private IPrototypeVehicle vehicle;
        private AudioSource bulletSource, blastSource;
        private float nextBulletAt, nextBlastAt;
        private float bulletGain, blastGain;
        public AudioSource BulletSource => bulletSource;
        public AudioSource BlastSource => blastSource;
        public int PlayedCount { get; private set; }
        public int SuppressedCount { get; private set; }
        public PrototypeVehicleImpactKind LastKind { get; private set; }
        public PrototypeDamageClass LastDamageClass { get; private set; }
        public Vector3 LastPoint { get; private set; }

        public static AudioClip Clip(PrototypeVehicleImpactKind kind)
        {
            int index = (int)kind;
            if (index < 0 || index >= Clips.Length) return null;
            if (Clips[index] == null)
                Clips[index] = Resources.Load<AudioClip>("Audio/VehicleImpacts/" + kind);
            return Clips[index];
        }

        public static bool Play(PrototypeRuntime game, IPrototypeVehicle target,
            Vector3 point, PrototypeDamageClass damageClass)
        {
            if (!CanPlay(game, target) || !Finite(point.x) || !Finite(point.y) ||
                !Finite(point.z) || (int)damageClass < 0 ||
                damageClass > PrototypeDamageClass.Frag) return false;
            var feedback = target.VehicleTransform.GetComponent<PrototypeVehicleImpactAudio>();
            if (feedback == null)
                feedback = target.VehicleTransform.gameObject.AddComponent<PrototypeVehicleImpactAudio>();
            feedback.runtime = game; feedback.vehicle = target;
            return feedback.Impact(point, damageClass);
        }

        private static bool CanPlay(PrototypeRuntime game, IPrototypeVehicle target) =>
            game != null && game.isActiveAndEnabled && game.MatchStarted &&
            !game.IsNetworkReplica && !game.Match.Winner.HasValue && Time.timeScale > 0f &&
            (!game.PauseOpen || game.IsNetworkSession) && target != null && target.Alive &&
            target.VehicleTransform != null && target.VehicleTransform.gameObject.activeInHierarchy;

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private bool Impact(Vector3 point, PrototypeDamageClass damageClass)
        {
            PrototypeVehicleImpactKind kind = damageClass == PrototypeDamageClass.InfantryBullet ?
                PrototypeVehicleImpactKind.Ricochet : damageClass == PrototypeDamageClass.MountedMachineGun ?
                PrototypeVehicleImpactKind.MachineGun : PrototypeVehicleImpactKind.HeavyImpact;
            bool heavy = kind == PrototypeVehicleImpactKind.HeavyImpact;
            if (Time.time < (heavy ? nextBlastAt : nextBulletAt))
            { SuppressedCount++; return false; }
            AudioClip clip = Clip(kind);
            if (clip == null) return false;
            if (heavy && blastSource == null) blastSource = CreateSource("Heavy hull impact audio");
            else if (!heavy && bulletSource == null) bulletSource = CreateSource("Metallic hull bullet audio");
            AudioSource source = heavy ? blastSource : bulletSource;
            source.transform.position = point;
            bool inside = runtime.Player != null &&
                ReferenceEquals(runtime.Player.CurrentVehicle, vehicle);
            // Hull position remains the real impact point. A passenger gets a
            // little non-spatial body so engine beds cannot mask cabin strikes.
            source.minDistance = heavy ? 8f : 5f;
            source.maxDistance = heavy ? 170f : 105f;
            float gain = kind == PrototypeVehicleImpactKind.Ricochet ? .28f :
                heavy ? (damageClass == PrototypeDamageClass.Frag ? .66f : .92f) : .66f;
            if (heavy) blastGain = gain; else bulletGain = gain;
            CabinMix(source, gain, inside);
            source.pitch = heavy ? (damageClass == PrototypeDamageClass.Frag ? 1.12f : .96f) :
                1f + ((PlayedCount % 5) - 2) * .025f;
            source.Stop(); source.clip = clip; source.Play();
            if (heavy) nextBlastAt = Time.time + .10f;
            else nextBulletAt = Time.time + (kind == PrototypeVehicleImpactKind.MachineGun ? .07f : .11f);
            LastKind = kind; LastDamageClass = damageClass; LastPoint = point; PlayedCount++;
            return true;
        }

        private AudioSource CreateSource(string name)
        {
            var anchor = new GameObject(name);
            anchor.transform.SetParent(transform, false);
            var source = anchor.AddComponent<AudioSource>();
            source.playOnAwake = false; source.loop = false;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.dopplerLevel = 0f; source.spread = 35f; source.priority = 70;
            source.ignoreListenerPause = false;
            return source;
        }

        private void LateUpdate()
        {
            // A lethal strike may finish its short ringing tail. New hits on a
            // dead hull are rejected by CanPlay, rather than cutting that strike.
            if (runtime == null || !runtime.MatchStarted || runtime.IsNetworkReplica ||
                runtime.Match.Winner.HasValue || Time.timeScale <= 0f ||
                runtime.PauseOpen && !runtime.IsNetworkSession) { Stop(); return; }
            bool inside = runtime.Player != null && ReferenceEquals(runtime.Player.CurrentVehicle, vehicle);
            CabinMix(bulletSource, bulletGain, inside); CabinMix(blastSource, blastGain, inside);
        }
        private static void CabinMix(AudioSource source, float gain, bool inside)
        {
            if (source == null) return;
            source.spatialBlend = inside ? .2f : 1f;
            source.volume = inside ? Mathf.Min(1f, gain * 1.15f) : gain;
        }
        public void Stop() { bulletSource?.Stop(); blastSource?.Stop(); }
        private void OnDisable() => Stop();
    }
}
