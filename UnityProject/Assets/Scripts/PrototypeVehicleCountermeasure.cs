using UnityEngine;

namespace Ironfront.UnityPrototype
{
    public sealed class PrototypeVehicleCountermeasure : MonoBehaviour
    {
        private bool flares;
        private float createdAt;
        private Transform[] particles;
        private Material material;

        public static void Deploy(PrototypeRuntime runtime, Vector3 origin, bool aircraft)
        {
            var effect = new GameObject(aircraft ? "Aircraft thermal flares" : "Vehicle defensive smoke")
                .AddComponent<PrototypeVehicleCountermeasure>();
            effect.transform.position = origin; effect.createdAt = Time.time; effect.flares = aircraft;
            effect.particles = new Transform[aircraft ? 8 : 4];
            effect.material = new Material(aircraft ? runtime.LampMaterial : runtime.GunMaterial);
            if (aircraft && effect.material.HasProperty("_EmissionColor"))
            { effect.material.EnableKeyword("_EMISSION"); effect.material.SetColor("_EmissionColor", new Color(3f, 1.8f, .6f)); }
            for (int i = 0; i < effect.particles.Length; i++)
            {
                GameObject part = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                part.name = aircraft ? "Burning decoy" : "Smoke cloud";
                Collider collider = part.GetComponent<Collider>();
                collider.enabled = false; Object.Destroy(collider);
                part.transform.SetParent(effect.transform, false);
                part.GetComponent<Renderer>().sharedMaterial = effect.material;
                effect.particles[i] = part.transform;
            }
        }
        private void Update()
        {
            float age = Time.time - createdAt;
            if (age >= 5f) { Destroy(gameObject); return; }
            for (int i = 0; i < particles.Length; i++)
            {
                float angle = i * Mathf.PI * 2f / particles.Length;
                float distance = flares ? 1f + age * 5f : 2.5f + age;
                particles[i].localPosition = new Vector3(Mathf.Cos(angle) * distance,
                    flares ? 1f - age * 2f : 1.5f + age * .45f, Mathf.Sin(angle) * distance);
                particles[i].localScale = Vector3.one * (flares ? .25f : 3.5f + age * .9f);
            }
        }
        private void OnDestroy() { if (material != null) Destroy(material); }
    }
}
