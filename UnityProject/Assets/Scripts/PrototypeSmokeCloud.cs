using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ironfront.UnityPrototype
{
    // Visual particles and optical extinction share one timed volume. Smoke is
    // never a physics collider: walking, navigation and bullets still work.
    public sealed class PrototypeSmokeCloud : MonoBehaviour
    {
        public const int MaxActiveClouds = 8;
        public const float GrenadeDuration = 24f;
        public const float GrenadeRadius = 5.4f;
        private static readonly List<PrototypeSmokeCloud> Active = new List<PrototypeSmokeCloud>();
        private static Material smokeMaterial;
        private static Texture2D smokeTexture;
        private PrototypeRuntime runtime;
        private ParticleSystem body;
        private ParticleSystem jet;
        private MaterialPropertyBlock fadeProperties;
        private float bornAt;
        private float duration;
        private float fullRadius;
        private float buildSeconds;
        private float emissionEnds;
        private Vector3 origin;
        private Transform movingCanister;
        private GameObject spentCanister;
        private bool initialized;

        private void Awake() => fadeProperties = new MaterialPropertyBlock();

        public float Age => Mathf.Max(0f, Time.time - bornAt);
        public float Duration => duration;
        public float Radius => RadiusAtAge(Age);
        public float Density => DensityAtAge(Age);
        public int ParticleLimit => 288;
        public Vector3 VolumeCenter => CenterAtAge(Age);
        public static int ActiveCloudCount => Active.Count;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry() => Active.Clear();

        public static PrototypeSmokeCloud Spawn(PrototypeRuntime game, Vector3 point,
            bool vehicleScreen = false)
        {
            while (Active.Count >= MaxActiveClouds)
            {
                PrototypeSmokeCloud oldest = Active[0];
                if (oldest == null) { Active.RemoveAt(0); continue; }
                oldest.enabled = false;
                Object.Destroy(oldest.gameObject);
            }
            var holder = new GameObject(vehicleScreen ? "Vehicle smoke screen" : "Grenade smoke screen");
            if (game != null) holder.transform.SetParent(game.transform, false);
            holder.transform.position = point;
            PrototypeSmokeCloud cloud = holder.AddComponent<PrototypeSmokeCloud>();
            cloud.runtime = game;
            cloud.origin = point;
            cloud.bornAt = Time.time;
            cloud.duration = vehicleScreen ? 19f : GrenadeDuration;
            cloud.fullRadius = vehicleScreen ? 7.2f : GrenadeRadius;
            cloud.buildSeconds = vehicleScreen ? 1.8f : 3.2f;
            cloud.emissionEnds = cloud.duration - 7f;
            cloud.initialized = true;
            Active.Add(cloud);
            if (!vehicleScreen && game != null)
            {
                GameObject canister = PrototypeThrowableVisual.Build(holder.transform,
                    PrototypeThrowableId.Smoke, game);
                cloud.spentCanister = canister;
                // The released can rests on its side and stays at the impact
                // point; smoke drifts separately rather than moving the prop.
                canister.transform.rotation = Quaternion.Euler(0f, 27f, 90f);
                canister.transform.position = CanisterRestPosition(point);
                PrototypeThrowableVisual.RemoveSafety(canister);
            }
            cloud.BuildParticles(vehicleScreen);
            return cloud;
        }

        // A wall-hit grenade can begin emitting while still falling. Follow its
        // physical source, keeping already-emitted particles in world space;
        // the normal stationary spent canister appears when it reaches support.
        public void FollowCanister(Transform canister)
        {
            movingCanister = canister;
            if (spentCanister != null) spentCanister.SetActive(false);
            if (canister != null) MoveEmitter(canister.position);
        }

        public bool IsFollowingCanister(Transform canister) => movingCanister != null && movingCanister == canister;

        public void StopFollowingCanister(Vector3 point)
        {
            movingCanister = null;
            MoveEmitter(point);
            if (spentCanister == null) return;
            spentCanister.transform.position = CanisterRestPosition(point);
            spentCanister.SetActive(true);
        }

        private void MoveEmitter(Vector3 point)
        {
            origin = point;
            transform.position = point;
        }

        private static Vector3 CanisterRestPosition(Vector3 point)
        {
            // Detonation provides the physics sphere's center, not its contact
            // point. Find the support under it without snapping a rooftop can
            // to terrain, or selecting the just-destroyed projectile's sphere.
            Physics.SyncTransforms();
            Vector3 start = point + Vector3.up * .35f;
            float nearest = float.PositiveInfinity;
            float surface = point.y - .13f;
            foreach (RaycastHit hit in Physics.RaycastAll(start, Vector3.down, 1.1f,
                ~(1 << 2), QueryTriggerInteraction.Ignore))
            {
                if (hit.distance >= nearest || hit.normal.y < .45f || hit.rigidbody != null ||
                    hit.collider.GetComponentInParent<PrototypePlayer>() != null ||
                    hit.collider.GetComponentInParent<PrototypeBot>() != null ||
                    hit.collider.GetComponentInParent<PrototypeRemotePlayer>() != null) continue;
                nearest = hit.distance;
                surface = hit.point.y;
            }
            // The horizontal canister's widest seam has radius .059 m.
            return new Vector3(point.x, surface + .061f, point.z);
        }

        private void BuildParticles(bool vehicleScreen)
        {
            Material material = SharedMaterial();
            body = CreateSystem("Rolling smoke body", 224, material);
            var main = body.main;
            main.duration = emissionEnds;
            main.startLifetime = new ParticleSystem.MinMaxCurve(7f, 10f);
            main.startSize = new ParticleSystem.MinMaxCurve(vehicleScreen ? 3.4f : 2.8f,
                vehicleScreen ? 4.8f : 4.2f);
            main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
            var emission = body.emission;
            emission.rateOverTime = vehicleScreen ? 23f : 21f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)(vehicleScreen ? 18 : 5)) });
            var shape = body.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = vehicleScreen ? 1.7f : .32f;
            shape.radiusThickness = 1f;
            shape.scale = new Vector3(1f, .33f, 1f);
            var velocity = body.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-.45f, .49f);
            velocity.y = new ParticleSystem.MinMaxCurve(.07f, .22f);
            velocity.z = new ParticleSystem.MinMaxCurve(-.46f, .46f);
            var size = body.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, .40f), new Keyframe(.20f, .82f),
                new Keyframe(.7f, 1.2f), new Keyframe(1f, 1.6f)));
            ConfigureColor(body, .84f, new Color(.88f, .90f, .90f));
            var rotation = body.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-.16f, .16f);
            var noise = body.noise;
            noise.enabled = true;
            noise.strength = .26f;
            noise.frequency = .32f;
            noise.scrollSpeed = .15f;
            noise.quality = ParticleSystemNoiseQuality.Low;

            jet = CreateSystem("Canister release wisps", 64, material);
            main = jet.main;
            main.duration = emissionEnds;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.2f, 4.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(.35f, .65f);
            main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
            emission = jet.emission;
            emission.rateOverTime = vehicleScreen ? 10f : 12f;
            shape = jet.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 22f;
            shape.radius = vehicleScreen ? .75f : .025f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(.8f, 1.2f);
            jet.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            size = jet.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, .45f, 1f, 3.5f));
            ConfigureColor(jet, .52f, new Color(.90f, .90f, .87f));
            SetBodyEmissionVolume(0f);
            body.Play(); jet.Play();
        }

        private ParticleSystem CreateSystem(string name, int maxParticles, Material material)
        {
            var holder = new GameObject(name);
            holder.transform.SetParent(transform, false);
            holder.transform.localPosition = Vector3.up * .18f;
            ParticleSystem particles = holder.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.loop = false;
            main.playOnAwake = false;
            main.startSpeed = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = maxParticles;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            var renderer = holder.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.maxParticleSize = 1f;
            renderer.minParticleSize = 0f;
            return particles;
        }

        private static void ConfigureColor(ParticleSystem particles, float alpha, Color tint)
        {
            var color = particles.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] {
                new GradientColorKey(tint * .78f, 0f),
                new GradientColorKey(tint, .45f),
                new GradientColorKey(tint, 1f) }, new[] {
                new GradientAlphaKey(0f, 0f), new GradientAlphaKey(alpha, .13f),
                new GradientAlphaKey(alpha * .90f, .60f), new GradientAlphaKey(0f, 1f) });
            color.color = new ParticleSystem.MinMaxGradient(gradient);
        }

        private static Material SharedMaterial()
        {
            if (smokeMaterial != null) return smokeMaterial;
            Shader shader = Resources.Load<Shader>("Effects/PrototypeSmoke");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
            smokeMaterial = new Material(shader) { name = "IRONFRONT soft smoke" };
            smokeTexture = BuildTexture();
            smokeMaterial.mainTexture = smokeTexture;
            if (smokeMaterial.HasProperty("_BaseMap")) smokeMaterial.SetTexture("_BaseMap", smokeTexture);
            if (smokeMaterial.HasProperty("_BaseColor")) smokeMaterial.SetColor("_BaseColor", Color.white);
            return smokeMaterial;
        }

        private static Texture2D BuildTexture()
        {
            const int size = 128;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + .5f) / size;
                    float v = (y + .5f) / size;
                    float dx = (u - .5f) * 2f;
                    float dy = (v - .5f) * 2f;
                    float radius = Mathf.Sqrt(dx * dx + dy * dy);
                    float noise = Mathf.PerlinNoise(13.7f + u * 3.4f, 9.2f + v * 3.4f) * .65f +
                        Mathf.PerlinNoise(7.3f + u * 8f, 19.1f + v * 8f) * .25f +
                        Mathf.PerlinNoise(31.7f + u * 15f, 1.4f + v * 15f) * .10f;
                    float edge = Mathf.Clamp01((1f - radius) / .44f);
                    edge = edge * edge * (3f - 2f * edge);
                    float alpha = edge * Mathf.Clamp01(.52f + noise * .58f);
                    float shade = .76f + noise * .22f + v * .04f;
                    pixels[y * size + x] = new Color(shade, shade, shade * .98f, alpha);
                }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true, false)
            { name = "Procedural billowing smoke mask", wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear };
            texture.SetPixels32(pixels); texture.Apply(true, true);
            return texture;
        }

        public float RadiusAtAge(float age)
        {
            float growth = Mathf.Clamp01(age / buildSeconds);
            return Mathf.Lerp(.65f, fullRadius, Mathf.SmoothStep(0f, 1f, growth));
        }

        public float DensityAtAge(float age)
        {
            if (age < 0f || age >= duration || !initialized) return 0f;
            float rise = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((age - .25f) / buildSeconds));
            float fade = 1f - Mathf.SmoothStep(0f, 1f,
                Mathf.Clamp01((age - emissionEnds) / (duration - emissionEnds)));
            return rise * fade;
        }

        private Vector3 CenterAtAge(float age) => origin +
            new Vector3(age * .026f, Mathf.Lerp(.55f, 2.25f, Mathf.Clamp01(age / buildSeconds)), age * .012f);

        private float PointDensityAt(Vector3 point, float age)
        {
            Vector3 offset = point - CenterAtAge(age);
            float radius = RadiusAtAge(age);
            float halfHeight = Mathf.Lerp(.7f, 2.9f, Mathf.Clamp01(age / buildSeconds));
            float ellipsoid = Mathf.Sqrt((offset.x * offset.x + offset.z * offset.z) /
                (radius * radius) + offset.y * offset.y / (halfHeight * halfHeight));
            float edge = Mathf.Clamp01((1f - ellipsoid) / .38f);
            return DensityAtAge(age) * edge * edge * (3f - 2f * edge);
        }

        public static float DensityAt(Vector3 point)
        {
            float transmission = 1f;
            for (int i = 0; i < Active.Count; i++)
                if (Active[i] != null && Active[i].enabled)
                    transmission *= 1f - Active[i].PointDensityAt(point, Active[i].Age);
            return 1f - transmission;
        }

        public static bool SegmentBlocked(Vector3 from, Vector3 to)
        {
            float depth = 0f;
            for (int i = 0; i < Active.Count; i++)
            {
                PrototypeSmokeCloud cloud = Active[i];
                if (cloud == null || !cloud.enabled) continue;
                depth += cloud.SegmentOpticalDepth(from, to, cloud.Age);
                if (depth >= .85f) return true;
            }
            return false;
        }

        public float SegmentOpticalDepth(Vector3 from, Vector3 to, float age)
        {
            if (DensityAtAge(age) < .01f) return 0f;
            Vector3 delta = to - from;
            float distance = delta.magnitude;
            if (distance < .001f) return 0f;
            float r = RadiusAtAge(age);
            float h = Mathf.Lerp(.7f, 2.9f, Mathf.Clamp01(age / buildSeconds));
            Vector3 offset = from - CenterAtAge(age);
            Vector3 a = new Vector3(offset.x / r, offset.y / h, offset.z / r);
            Vector3 d = new Vector3(delta.x / r, delta.y / h, delta.z / r);
            float aa = Vector3.Dot(d, d);
            float bb = 2f * Vector3.Dot(a, d);
            float cc = Vector3.Dot(a, a) - 1f;
            float discriminant = bb * bb - 4f * aa * cc;
            if (discriminant <= 0f) return 0f;
            float root = Mathf.Sqrt(discriminant);
            float enter = Mathf.Max(0f, (-bb - root) / (2f * aa));
            float exit = Mathf.Min(1f, (-bb + root) / (2f * aa));
            if (exit <= enter) return 0f;
            float total = 0f;
            const int samples = 6;
            for (int i = 0; i < samples; i++)
                total += PointDensityAt(from + delta * Mathf.Lerp(enter, exit,
                    (i + .5f) / samples), age);
            return total / samples * (exit - enter) * distance * .62f;
        }

        private void Update()
        {
            if (!initialized) return;
            if (Age >= duration || runtime != null &&
                (!runtime.MatchStarted || runtime.Match.Winner.HasValue))
            { Destroy(gameObject); return; }
            if (movingCanister != null) MoveEmitter(movingCanister.position);
            // Fade the whole population at the end rather than deleting visible
            // last-born particles when the optical volume expires.
            float fade = 1f - Mathf.SmoothStep(0f, 1f,
                Mathf.Clamp01((Age - emissionEnds) / (duration - emissionEnds)));
            fadeProperties.SetFloat("_CloudFade", fade);
            body.GetComponent<ParticleSystemRenderer>().SetPropertyBlock(fadeProperties);
            jet.GetComponent<ParticleSystemRenderer>().SetPropertyBlock(fadeProperties);
            SetBodyEmissionVolume(Age);
        }

        private void SetBodyEmissionVolume(float age)
        {
            // The core rolls above the surface rather than emitting half its
            // population underground. A compressed source plus gentle rise
            // centers mature visible smoke on the shared optical volume.
            float growth = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(age / buildSeconds));
            body.transform.localPosition = Vector3.up * Mathf.Lerp(.28f, 1.5f, growth);
            var shape = body.shape;
            shape.radius = Mathf.Lerp(.32f, fullRadius * .34f, growth);
            shape.scale = new Vector3(1f, .33f, 1f);
        }

        private void OnDisable() => Active.Remove(this);

        private void OnGUI()
        {
            // Billboards alone can reveal distant silhouettes from inside their
            // near plane. A density veil represents fog at the viewer's eyes;
            // it is behind the HUD and only appears for a camera inside smoke.
            if (Active.Count == 0 || Active[0] != this ||
                runtime == null || !runtime.MatchStarted || Event.current.type != EventType.Repaint) return;
            Camera camera = Camera.main;
            if (camera == null || !camera.enabled) return;
            float density = DensityAt(camera.transform.position);
            if (density <= .01f) return;
            Color previous = GUI.color;
            int depth = GUI.depth;
            Matrix4x4 matrix = GUI.matrix;
            GUI.depth = 100;
            GUI.matrix = Matrix4x4.identity;
            GUI.color = new Color(.69f, .70f, .68f, 1f - Mathf.Exp(-3.4f * density));
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previous; GUI.depth = depth; GUI.matrix = matrix;
        }

#if UNITY_EDITOR
        public void PreviewAtAge(float age)
        {
            body.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            jet.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            bornAt = Time.time - Mathf.Clamp(age, 0f, duration);
            SetBodyEmissionVolume(Age);
            float fade = 1f - Mathf.SmoothStep(0f, 1f,
                Mathf.Clamp01((Age - emissionEnds) / (duration - emissionEnds)));
            fadeProperties.SetFloat("_CloudFade", fade);
            body.GetComponent<ParticleSystemRenderer>().SetPropertyBlock(fadeProperties);
            jet.GetComponent<ParticleSystemRenderer>().SetPropertyBlock(fadeProperties);
            body.useAutoRandomSeed = false; body.randomSeed = 4402;
            jet.useAutoRandomSeed = false; jet.randomSeed = 4411;
            body.Simulate(Age, true, true, true);
            jet.Simulate(Age, true, true, true);
            body.Play(); jet.Play();
        }
#endif
    }
}
