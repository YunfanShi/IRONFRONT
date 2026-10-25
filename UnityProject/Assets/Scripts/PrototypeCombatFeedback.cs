using System;
using UnityEngine;

namespace Ironfront.UnityPrototype
{
    public enum PrototypeCombatSound
    {
        MountedGun, TankCannon, TankExplosion, GrenadeThrow, FragExplosion, SmokeRelease,
        RocketLaunch, RocketExplosion, AmmoPack, Medkit, ArmorTool, Beacon
    }

    // The browser game's weapon and explosion clips are reused in Unity. Support
    // sounds without a browser counterpart are synthesized once and cached.
    public static class PrototypeCombatFeedback
    {
        private const int SampleRate = 22050;
        private static readonly AudioClip[] WeaponClips = new AudioClip[7];
        private static readonly AudioClip[] EffectClips = new AudioClip[12];

        public static void PlayWeapon(Transform source, PrototypeWeaponId weapon)
        {
            int index = (int)weapon;
            if (index < 0 || index >= WeaponClips.Length) return;
            if (WeaponClips[index] == null)
            {
                string clipName = weapon == PrototypeWeaponId.BattleRifle ?
                    "battleRifle" : weapon.ToString().ToLowerInvariant();
                WeaponClips[index] = Resources.Load<AudioClip>("Audio/" + clipName);
            }
            if (WeaponClips[index] == null)
            {
                float length = weapon == PrototypeWeaponId.Sniper ? .48f :
                    weapon == PrototypeWeaponId.Lmg ? .27f :
                    weapon == PrototypeWeaponId.Smg ? .13f :
                    weapon == PrototypeWeaponId.Pistol ? .17f : .22f;
                float bass = weapon == PrototypeWeaponId.Sniper ? 67f :
                    weapon == PrototypeWeaponId.Lmg ? 89f :
                    weapon == PrototypeWeaponId.Smg ? 155f :
                    weapon == PrototypeWeaponId.Pistol ? 180f : 120f;
                WeaponClips[index] = Synthesize("IRONFRONT " + weapon + " shot",
                    length, bass, .72f, 101 + index * 17);
            }
            Play(source, WeaponClips[index], weapon == PrototypeWeaponId.Sniper ? .85f : .56f);
        }

        public static void Play(Transform source, PrototypeCombatSound sound)
        {
            if (source == null) return;
            AudioClip clip = Clip(sound);
            Play(source, clip, sound == PrototypeCombatSound.TankCannon ? 1f : .72f);
        }

        public static void PlayAt(Vector3 position, PrototypeCombatSound sound)
        {
            var anchor = new GameObject(sound + " audio");
            anchor.transform.position = position;
            Play(anchor.transform, sound);
            UnityEngine.Object.Destroy(anchor, Clip(sound).length + .15f);
        }

        private static AudioClip Clip(PrototypeCombatSound sound)
        {
            int index = (int)sound;
            if (EffectClips[index] != null) return EffectClips[index];
            if (sound == PrototypeCombatSound.TankCannon)
                EffectClips[index] = Resources.Load<AudioClip>("Audio/cannon");
            else if (sound == PrototypeCombatSound.TankExplosion ||
                sound == PrototypeCombatSound.FragExplosion ||
                sound == PrototypeCombatSound.RocketExplosion)
                EffectClips[index] = Resources.Load<AudioClip>("Audio/explosion");
            if (EffectClips[index] != null) return EffectClips[index];
            float length;
            float bass;
            float crack;
            switch (sound)
            {
                case PrototypeCombatSound.MountedGun:
                    length = .18f; bass = 105f; crack = .8f; break;
                case PrototypeCombatSound.TankCannon:
                    length = 1.05f; bass = 49f; crack = 1f; break;
                case PrototypeCombatSound.TankExplosion:
                    length = 1.25f; bass = 44f; crack = .8f; break;
                case PrototypeCombatSound.GrenadeThrow:
                    length = .15f; bass = 260f; crack = .25f; break;
                case PrototypeCombatSound.FragExplosion:
                    length = .9f; bass = 57f; crack = .9f; break;
                case PrototypeCombatSound.SmokeRelease:
                    length = .8f; bass = 125f; crack = .35f; break;
                case PrototypeCombatSound.RocketLaunch:
                    length = .55f; bass = 95f; crack = .65f; break;
                case PrototypeCombatSound.RocketExplosion:
                    length = 1f; bass = 51f; crack = .9f; break;
                case PrototypeCombatSound.AmmoPack:
                    length = .24f; bass = 190f; crack = .38f; break;
                case PrototypeCombatSound.Medkit:
                    length = .32f; bass = 460f; crack = .12f; break;
                case PrototypeCombatSound.ArmorTool:
                    length = .38f; bass = 280f; crack = .5f; break;
                case PrototypeCombatSound.Beacon:
                    length = .46f; bass = 590f; crack = .1f; break;
                default:
                    length = .3f; bass = 320f; crack = .3f; break;
            }
            EffectClips[index] = Synthesize("IRONFRONT " + sound,
                length, bass, crack, 701 + index * 37);
            return EffectClips[index];
        }

        private static AudioClip Synthesize(string name, float seconds,
            float bass, float crack, int seed)
        {
            int count = Mathf.CeilToInt(seconds * SampleRate);
            var data = new float[count];
            var random = new System.Random(seed);
            float filtered = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float age = t / seconds;
                float noise = (float)(random.NextDouble() * 2.0 - 1.0);
                filtered += (noise - filtered) * .16f;
                float body = Mathf.Sin(2f * Mathf.PI *
                    (bass * t - bass * .15f * t * t / seconds));
                float click = noise * Mathf.Exp(-t * 90f) * crack;
                float tail = (filtered * .48f + body * .52f) *
                    Mathf.Pow(1f - age, 2.6f);
                data[i] = Mathf.Clamp((click + tail) * .7f, -.95f, .95f);
            }
            AudioClip clip = AudioClip.Create(name, count, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static void Play(Transform source, AudioClip clip, float volume)
        {
            if (source == null || clip == null) return;
            AudioSource audio = source.GetComponent<AudioSource>();
            if (audio == null) audio = source.gameObject.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = source.GetComponent<PrototypePlayer>() != null ? .15f : 1f;
            audio.rolloffMode = AudioRolloffMode.Logarithmic;
            audio.minDistance = 4f;
            audio.maxDistance = 155f;
            audio.dopplerLevel = 0f;
            audio.PlayOneShot(clip, volume);
        }

        public static void Muzzle(PrototypeRuntime game, Vector3 point,
            Vector3 direction, float size)
        {
            SpawnPulse("Muzzle flash", point + direction.normalized * .16f,
                game.LampMaterial, size * .18f, size, .11f, direction.normalized * 2f);
        }

        public static void Impact(PrototypeRuntime game, Vector3 point)
        {
            SpawnPulse("Impact sparks", point, game.LampMaterial,
                .06f, .36f, .16f, Vector3.up * .8f);
        }

        public static bool VehicleImpact(PrototypeRuntime game, IPrototypeVehicle vehicle,
            Vector3 point, PrototypeDamageClass damageClass) =>
            PrototypeVehicleImpactAudio.Play(game, vehicle, point, damageClass);

        public static void Explosion(PrototypeRuntime game, Vector3 center,
            float radius, PrototypeCombatSound sound)
        {
            PlayAt(center, sound);
            SpawnPulse("Blast core", center, game.LampMaterial,
                .3f, radius * .62f, .34f, Vector3.zero);
            for (int i = 0; i < 12; i++)
            {
                float angle = i * Mathf.PI / 6f;
                Vector3 outward = new Vector3(Mathf.Cos(angle), .16f + (i % 3) * .15f,
                    Mathf.Sin(angle));
                SpawnPulse("Blast debris", center + Vector3.up * .2f,
                    i % 3 == 0 ? game.GunMaterial : game.LampMaterial,
                    .09f, i % 3 == 0 ? .65f : .28f,
                    i % 3 == 0 ? 1.15f : .52f, outward * radius * 1.35f);
            }
            var lightObject = new GameObject("Blast light");
            lightObject.transform.position = center;
            Light light = lightObject.AddComponent<Light>();
            light.color = new Color(1f, .56f, .22f);
            light.range = radius * 2f;
            light.intensity = 4f;
            lightObject.AddComponent<PrototypePulseVisual>().Initialize(
                Vector3.one, Vector3.one, .35f, Vector3.zero, light);
        }

        public static void Smoke(PrototypeRuntime game, Vector3 center)
        {
            PlayAt(center, PrototypeCombatSound.SmokeRelease);
            PrototypeSmokeCloud.Spawn(game, center);
        }

        public static void Support(PrototypeRuntime game, Vector3 point,
            PrototypeInfantryClass role)
        {
            PrototypeCombatSound sound = role == PrototypeInfantryClass.Medic ?
                PrototypeCombatSound.Medkit : role == PrototypeInfantryClass.Engineer ?
                PrototypeCombatSound.ArmorTool : role == PrototypeInfantryClass.Recon ?
                PrototypeCombatSound.Beacon : PrototypeCombatSound.AmmoPack;
            PlayAt(point, sound);
            SpawnPulse("Support cue", point + Vector3.up * .8f,
                role == PrototypeInfantryClass.Medic ? game.LampMaterial :
                    game.BlueTrimMaterial, .12f, .85f, .36f, Vector3.up);
        }

        private static void SpawnPulse(string name, Vector3 point, Material material,
            float initial, float peak, float lifetime, Vector3 velocity)
        {
            GameObject pulse = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pulse.name = name;
            pulse.transform.position = point;
            Collider collider = pulse.GetComponent<Collider>();
            collider.enabled = false;
            UnityEngine.Object.Destroy(collider);
            pulse.GetComponent<Renderer>().sharedMaterial = material;
            pulse.AddComponent<PrototypePulseVisual>().Initialize(
                Vector3.one * initial, Vector3.one * peak, lifetime, velocity);
        }
    }

    public sealed class PrototypePulseVisual : MonoBehaviour
    {
        private Vector3 initial;
        private Vector3 peak;
        private Vector3 velocity;
        private float life;
        private float started;
        private Light effectLight;

        public void Initialize(Vector3 start, Vector3 end, float seconds,
            Vector3 movement, Light light = null)
        {
            initial = start;
            peak = end;
            life = seconds;
            velocity = movement;
            effectLight = light;
            started = Time.time;
            transform.localScale = start;
        }

        private void Update()
        {
            float progress = (Time.time - started) / Mathf.Max(.01f, life);
            if (progress >= 1f) { Destroy(gameObject); return; }
            transform.localScale = Vector3.Lerp(initial, peak,
                Mathf.Sin(progress * Mathf.PI));
            transform.position += velocity * Time.deltaTime;
            if (effectLight != null) effectLight.intensity = 4f * (1f - progress);
        }
    }
}
