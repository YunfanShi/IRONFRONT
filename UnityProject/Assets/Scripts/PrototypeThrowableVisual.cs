using UnityEngine;

namespace Ironfront.UnityPrototype
{
    // Shared world/hand model. These props never add physics to an actor or a
    // projectile; the throwing component owns its own small collider instead.
    public static class PrototypeThrowableVisual
    {
        private static Mesh smokeBody;
        private static Mesh fragBody;
        private static Mesh pullRing;

        public static GameObject Build(Transform parent, PrototypeThrowableId kind,
            PrototypeRuntime runtime)
        {
            GameObject authored = PrototypeEquipmentArt.Create(parent,
                kind == PrototypeThrowableId.Smoke ? "SmokeGrenade" : "FragGrenade");
            if (authored != null)
            {
                authored.name = kind == PrototypeThrowableId.Smoke ? "Smoke grenade model" : "Fragmentation grenade model";
                return authored;
            }
            var root = new GameObject(kind == PrototypeThrowableId.Smoke ?
                "Smoke grenade model" : "Fragmentation grenade model");
            root.transform.SetParent(parent, false);
            bool smoke = kind == PrototypeThrowableId.Smoke;
            Material body = smoke ? runtime.BlueTrimMaterial : runtime.BlueHelmetMaterial;
            if (smokeBody == null)
                smokeBody = Lathe("Smoke canister mesh", new[] {
                    new Vector2(.047f, -.145f), new Vector2(.058f, -.133f),
                    new Vector2(.058f, .102f), new Vector2(.052f, .118f),
                    new Vector2(.030f, .128f) });
            if (fragBody == null)
                fragBody = Lathe("Fragmentation body mesh", new[] {
                    new Vector2(.038f, -.124f), new Vector2(.066f, -.107f),
                    new Vector2(.078f, -.070f), new Vector2(.079f, .045f),
                    new Vector2(.065f, .080f), new Vector2(.033f, .103f) });
            MeshPart("Canister", root.transform, smoke ? smokeBody : fragBody, body);
            float capY = smoke ? .135f : .112f;
            Cylinder("Fuse cap", root.transform, new Vector3(0f, capY, 0f),
                .028f, .017f, runtime.GunMaterial);
            Cylinder("Fuse collar", root.transform, new Vector3(0f, capY - .015f, 0f),
                .034f, .005f, runtime.ArmorMaterial);
            if (smoke)
            {
                Cylinder("Identification band", root.transform, new Vector3(0f, -.073f, 0f),
                    .0588f, .020f, runtime.LampMaterial);
                Cylinder("Rolled base seam", root.transform, new Vector3(0f, -.135f, 0f),
                    .059f, .006f, runtime.GunMaterial);
                Cylinder("Rolled top seam", root.transform, new Vector3(0f, .102f, 0f),
                    .0588f, .005f, runtime.ArmorMaterial);
                for (int i = 0; i < 8; i++)
                {
                    float angle = i * 45f;
                    Vector3 p = Quaternion.Euler(0f, angle, 0f) * new Vector3(0f, .082f, .059f);
                    GameObject vent = Box("Smoke vent", root.transform, p,
                        new Vector3(.014f, .007f, .004f), runtime.BootMaterial);
                    vent.transform.localRotation = Quaternion.Euler(0f, angle, 0f);
                }
                Box("SMOKE identification stripe", root.transform,
                    new Vector3(0f, -.015f, .0595f), new Vector3(.026f, .065f, .002f),
                    runtime.LampMaterial);
            }
            else
            {
                for (int i = 0; i < 8; i++)
                {
                    float angle = i * 45f;
                    GameObject rib = Box("Fragment rib", root.transform,
                        Quaternion.Euler(0f, angle, 0f) * new Vector3(0f, -.015f, .080f),
                        new Vector3(.012f, .153f, .008f), runtime.ArmorMaterial);
                    rib.transform.localRotation = Quaternion.Euler(0f, angle, 0f);
                }
                for (int i = -1; i <= 1; i++)
                    Cylinder("Fragment grip band", root.transform,
                        new Vector3(0f, i * .047f, 0f), .080f, .004f, runtime.BootMaterial);
            }
            // A bent metal spoon follows the body; the safety pin crosses the
            // fuse, and the torus has an actual visible hole instead of a ball.
            Box("Safety lever", root.transform, new Vector3(.029f, capY + .020f, 0f),
                new Vector3(.082f, .009f, .022f), runtime.ArmorMaterial);
            GameObject bend = Box("Safety lever bend", root.transform,
                new Vector3(.065f, capY - .010f, 0f), new Vector3(.012f, .067f, .022f),
                runtime.ArmorMaterial);
            bend.transform.localRotation = Quaternion.Euler(0f, 0f, 18f);
            GameObject spoon = Box("Safety lever grip", root.transform,
                new Vector3(.077f, capY - .083f, 0f), new Vector3(.011f, .098f, .023f),
                runtime.ArmorMaterial);
            spoon.transform.localRotation = Quaternion.Euler(0f, 0f, 3f);
            GameObject pin = Cylinder("Safety pin", root.transform,
                new Vector3(-.015f, capY + .004f, 0f), .0033f, .041f, runtime.LampMaterial);
            pin.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            if (pullRing == null) pullRing = Torus();
            GameObject ring = MeshPart("Pull ring", root.transform, pullRing, runtime.ArmorMaterial);
            ring.transform.localPosition = new Vector3(-.059f, capY + .008f, 0f);
            ring.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            return root;
        }

        public static void RemoveSafety(GameObject model)
        {
            if (model == null) return;
            foreach (Transform part in model.transform)
                if (part.name.StartsWith("Safety") || part.name == "Pull ring")
                    part.gameObject.SetActive(false);
        }

        private static GameObject MeshPart(string name, Transform parent, Mesh mesh, Material material)
        {
            var part = new GameObject(name);
            part.transform.SetParent(parent, false);
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            part.AddComponent<MeshRenderer>().sharedMaterial = material;
            return part;
        }

        private static GameObject Box(string name, Transform parent, Vector3 position,
            Vector3 scale, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            Collider collider = part.GetComponent<Collider>();
            collider.enabled = false;
            Object.Destroy(collider);
            part.GetComponent<Renderer>().sharedMaterial = material;
            return part;
        }

        private static GameObject Cylinder(string name, Transform parent, Vector3 position,
            float radius, float halfHeight, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = new Vector3(radius * 2f, halfHeight, radius * 2f);
            Collider collider = part.GetComponent<Collider>();
            collider.enabled = false;
            Object.Destroy(collider);
            part.GetComponent<Renderer>().sharedMaterial = material;
            return part;
        }

        private static Mesh Lathe(string name, Vector2[] profile)
        {
            const int segments = 20;
            var vertices = new Vector3[profile.Length * (segments + 1) + 2];
            var triangles = new int[((profile.Length - 1) * segments * 2 + segments * 2) * 3];
            for (int y = 0; y < profile.Length; y++)
                for (int i = 0; i <= segments; i++)
                {
                    float a = i * Mathf.PI * 2f / segments;
                    vertices[y * (segments + 1) + i] = new Vector3(
                        Mathf.Sin(a) * profile[y].x, profile[y].y, Mathf.Cos(a) * profile[y].x);
                }
            int t = 0;
            for (int y = 0; y < profile.Length - 1; y++)
                for (int i = 0; i < segments; i++)
                {
                    int a = y * (segments + 1) + i;
                    int b = a + segments + 1;
                    triangles[t++] = a; triangles[t++] = a + 1; triangles[t++] = b;
                    triangles[t++] = a + 1; triangles[t++] = b + 1; triangles[t++] = b;
                }
            int bottom = vertices.Length - 2;
            int top = vertices.Length - 1;
            vertices[bottom] = Vector3.up * profile[0].y;
            vertices[top] = Vector3.up * profile[profile.Length - 1].y;
            int lastRing = (profile.Length - 1) * (segments + 1);
            for (int i = 0; i < segments; i++)
            {
                triangles[t++] = bottom; triangles[t++] = i + 1; triangles[t++] = i;
                triangles[t++] = top; triangles[t++] = lastRing + i; triangles[t++] = lastRing + i + 1;
            }
            var mesh = new Mesh { name = name, vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh Torus()
        {
            const int majorSegments = 24;
            const int minorSegments = 6;
            var vertices = new Vector3[(majorSegments + 1) * (minorSegments + 1)];
            var triangles = new int[majorSegments * minorSegments * 6];
            for (int a = 0; a <= majorSegments; a++)
                for (int b = 0; b <= minorSegments; b++)
                {
                    float u = a * Mathf.PI * 2f / majorSegments;
                    float v = b * Mathf.PI * 2f / minorSegments;
                    float radius = .021f + Mathf.Cos(v) * .003f;
                    vertices[a * (minorSegments + 1) + b] = new Vector3(
                        Mathf.Cos(u) * radius, Mathf.Sin(u) * radius, Mathf.Sin(v) * .003f);
                }
            int t = 0;
            for (int a = 0; a < majorSegments; a++)
                for (int b = 0; b < minorSegments; b++)
                {
                    int i = a * (minorSegments + 1) + b;
                    int j = i + minorSegments + 1;
                    triangles[t++] = i; triangles[t++] = j; triangles[t++] = i + 1;
                    triangles[t++] = i + 1; triangles[t++] = j; triangles[t++] = j + 1;
                }
            var mesh = new Mesh { name = "Hollow safety pull ring", vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }
    }
}
