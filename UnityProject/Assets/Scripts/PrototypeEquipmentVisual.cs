using System.Collections.Generic;
using UnityEngine;

namespace Ironfront.UnityPrototype
{
    // Small world-space props share the soldier's primitive low-poly language.
    // They are visual only; gameplay collision stays on the projectile or actor.
    public static class PrototypeEquipmentVisual
    {
        public static GameObject BuildThrowable(Transform parent,
            PrototypeThrowableId kind, PrototypeRuntime game) =>
            PrototypeThrowableVisual.Build(parent, kind, game);

        public static GameObject BuildRocket(Transform parent, PrototypeRuntime game)
        {
            var root = new GameObject("Anti-armor rocket model");
            root.transform.SetParent(parent, false);
            Part("Motor casing", PrimitiveType.Cylinder, root.transform,
                new Vector3(0f, 0f, 0f), new Vector3(.085f, .28f, .085f),
                game.ArmorMaterial).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Part("Warhead", PrimitiveType.Sphere, root.transform,
                new Vector3(0f, 0f, .30f), new Vector3(.19f, .19f, .27f),
                game.GunMaterial);
            Part("Warhead band", PrimitiveType.Cylinder, root.transform,
                new Vector3(0f, 0f, .17f), new Vector3(.098f, .024f, .098f),
                game.RedTrimMaterial).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            for (int i = 0; i < 4; i++)
            {
                float angle = i * 90f;
                Part("Stabilizer fin", PrimitiveType.Cube, root.transform,
                    new Vector3(Mathf.Sin(angle * Mathf.Deg2Rad) * .11f,
                        Mathf.Cos(angle * Mathf.Deg2Rad) * .11f, -.20f),
                    new Vector3(.025f, .20f, .20f), game.BootMaterial)
                    .transform.localRotation = Quaternion.Euler(0f, 0f, -angle);
            }
            return root;
        }

        public static GameObject BuildLauncher(Transform parent, PrototypeRuntime game)
        {
            var root = new GameObject("Anti-armor launcher model");
            root.transform.SetParent(parent, false);
            Part("Launcher tube", PrimitiveType.Cylinder, root.transform,
                new Vector3(0f, 0f, .12f), new Vector3(.145f, .56f, .145f),
                game.ArmorMaterial).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            // The front is hollow: a dark recessed bore surrounded by an actual
            // polygonal collar makes the long shoulder launcher readable.
            Ring("Muzzle collar", root.transform, .695f, .096f, .035f, game.GunMaterial);
            Part("Recessed muzzle bore", PrimitiveType.Cylinder, root.transform,
                new Vector3(0f, 0f, .685f), new Vector3(.125f, .004f, .125f),
                game.BootMaterial).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Ring("Rear vent", root.transform, -.465f, .084f, .06f, game.GunMaterial);
            for (int i = 0; i < 3; i++)
                Ring("Heat shield band", root.transform, -.15f + i * .2f,
                    .079f, .024f, game.BootMaterial);
            Part("Shoulder rest", PrimitiveType.Cube, root.transform,
                new Vector3(0f, -.12f, -.31f), new Vector3(.19f, .10f, .29f), game.BootMaterial);
            Part("Firing grip", PrimitiveType.Cube, root.transform,
                new Vector3(0f, -.20f, .02f), new Vector3(.08f, .28f, .10f), game.BootMaterial);
            Part("Trigger guard", PrimitiveType.Cube, root.transform,
                new Vector3(0f, -.12f, .12f), new Vector3(.06f, .018f, .12f), game.GunMaterial);
            Part("Forward grip", PrimitiveType.Cube, root.transform,
                new Vector3(0f, -.15f, .34f), new Vector3(.085f, .19f, .11f), game.BootMaterial);
            Part("Optical sight", PrimitiveType.Cylinder, root.transform,
                new Vector3(.15f, .19f, .17f), new Vector3(.095f, .18f, .095f),
                game.GunMaterial).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Part("Sight glass", PrimitiveType.Cylinder, root.transform,
                new Vector3(.15f, .19f, .355f), new Vector3(.065f, .005f, .065f),
                game.VisorMaterial).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Part("Sight bracket", PrimitiveType.Cube, root.transform,
                new Vector3(.085f, .13f, .17f), new Vector3(.16f, .075f, .08f), game.GunMaterial);
            Part("Warning stripe", PrimitiveType.Cube, root.transform,
                new Vector3(.073f, 0f, -.28f), new Vector3(.009f, .07f, .10f), game.RedTrimMaterial);
            return root;
        }

        public static GameObject BuildAntiAirLauncher(Transform parent, PrototypeRuntime game)
        {
            GameObject root = BuildLauncher(parent, game);
            root.name = "Engineer infrared anti-air launcher";
            // The inherited cylinder and end glass cap would hide the actual
            // aircraft in ADS. Replace them with an open annular optical tube.
            RetireVisual(root.transform.Find("Optical sight"));
            RetireVisual(root.transform.Find("Sight glass"));
            GameObject optic = HollowTube("Optical sight", root.transform,
                new Vector3(.15f, .19f, .17f), .0475f, .034f, .36f, game.GunMaterial);
            HollowTube("Sight front annular rim", optic.transform, Vector3.forward * .18f,
                .0545f, .034f, .016f, game.GunMaterial);
            HollowTube("Sight rear annular rim", optic.transform, Vector3.back * .18f,
                .0545f, .034f, .016f, game.GunMaterial);
            root.transform.Find("Launcher tube").localScale = new Vector3(.18f, .60f, .18f);
            Part("Infrared seeker electronics", PrimitiveType.Cube, root.transform,
                new Vector3(-.13f, .065f, .21f), new Vector3(.19f, .18f, .44f), game.GunMaterial);
            Part("Infrared sensor lens", PrimitiveType.Sphere, root.transform,
                new Vector3(-.13f, .065f, .444f), new Vector3(.13f, .13f, .045f), game.VisorMaterial);
            Part("Battery cooling pack", PrimitiveType.Cube, root.transform,
                new Vector3(.13f, -.05f, -.18f), new Vector3(.19f, .18f, .34f), game.ArmorMaterial);
            Ring("AA identification band", root.transform, .52f, .102f, .045f, game.LampMaterial);
            Ring("AA identification rear band", root.transform, -.34f, .102f, .035f, game.LampMaterial);
            Part("Raised acquisition sight", PrimitiveType.Cube, root.transform,
                new Vector3(.15f, .31f, .10f), new Vector3(.06f, .20f, .035f), game.GunMaterial);
            var window = new GameObject("Acquisition sight window");
            window.transform.SetParent(root.transform, false);
            window.transform.localPosition = new Vector3(.15f, .39f, .115f);
            foreach (float side in new[] { -1f, 1f })
            {
                Part("Open window vertical frame", PrimitiveType.Cube, window.transform,
                    new Vector3(side * .064f, 0f, 0f), new Vector3(.012f, .12f, .025f), game.GunMaterial);
                Part("Open window horizontal frame", PrimitiveType.Cube, window.transform,
                    new Vector3(0f, side * .054f, 0f), new Vector3(.116f, .012f, .025f), game.GunMaterial);
            }
            return root;
        }

        private static void RetireVisual(Transform old)
        {
            if (old == null) return;
            old.gameObject.name = "Retired solid optic";
            old.gameObject.SetActive(false);
            foreach (Collider collider in old.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            if (Application.isPlaying) Object.Destroy(old.gameObject);
            else Object.DestroyImmediate(old.gameObject);
        }

        // Four annular surfaces form a real through-hole. No front or rear
        // center disc, transparent cap or physics collider is created.
        private static GameObject HollowTube(string name, Transform parent, Vector3 position,
            float outerRadius, float innerRadius, float length, Material material)
        {
            const int segments = 24;
            var vertices = new List<Vector3>(segments * 16);
            var triangles = new List<int>(segments * 24);
            var uv = new List<Vector2>(segments * 16);
            float half = length * .5f;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments, b = (i + 1) * Mathf.PI * 2f / segments;
                Vector3 oa = new Vector3(Mathf.Sin(a) * outerRadius, Mathf.Cos(a) * outerRadius, 0f);
                Vector3 ob = new Vector3(Mathf.Sin(b) * outerRadius, Mathf.Cos(b) * outerRadius, 0f);
                Vector3 ia = new Vector3(Mathf.Sin(a) * innerRadius, Mathf.Cos(a) * innerRadius, 0f);
                Vector3 ib = new Vector3(Mathf.Sin(b) * innerRadius, Mathf.Cos(b) * innerRadius, 0f);
                Vector3 front = Vector3.forward * half, back = -front;
                TubeQuad(vertices, triangles, uv, oa + back, oa + front, ob + front, ob + back);
                TubeQuad(vertices, triangles, uv, ia + back, ib + back, ib + front, ia + front);
                TubeQuad(vertices, triangles, uv, oa + front, ia + front, ib + front, ob + front);
                TubeQuad(vertices, triangles, uv, oa + back, ob + back, ib + back, ia + back);
            }
            var mesh = new Mesh { name = name + " hollow annular mesh" };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.SetUVs(0, uv);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var part = new GameObject(name);
            part.transform.SetParent(parent, false); part.transform.localPosition = position;
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            part.AddComponent<MeshRenderer>().sharedMaterial = material;
            part.AddComponent<PrototypeEquipmentMeshOwner>().OwnedMesh = mesh;
            return part;
        }

        private static void TubeQuad(List<Vector3> vertices, List<int> triangles, List<Vector2> uv,
            Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int first = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
            triangles.Add(first); triangles.Add(first + 1); triangles.Add(first + 2);
            triangles.Add(first); triangles.Add(first + 2); triangles.Add(first + 3);
            uv.Add(Vector2.zero); uv.Add(Vector2.up); uv.Add(Vector2.one); uv.Add(Vector2.right);
        }

        public static GameObject BuildAntiAirMissile(Transform parent, PrototypeRuntime game)
        {
            GameObject root = BuildRocket(parent, game);
            root.name = "Guided infrared anti-air missile body";
            root.transform.localScale = new Vector3(.75f, .75f, 1.5f);
            Part("Infrared nose seeker", PrimitiveType.Sphere, root.transform,
                new Vector3(0f, 0f, .43f), new Vector3(.14f, .14f, .13f), game.VisorMaterial);
            return root;
        }

        public static void AddHeldHands(Transform model, PrototypeHeldEquipment kind,
            PrototypeRuntime game, PrototypeTeam team)
        {
            Material cloth = team == PrototypeTeam.Blue ? game.BlueClothMaterial : game.RedClothMaterial;
            Vector3 right = kind == PrototypeHeldEquipment.Throwable ?
                new Vector3(0f, -.05f, -.02f) : new Vector3(0f, -.23f, .02f);
            Vector3 left = kind == PrototypeHeldEquipment.Throwable ?
                new Vector3(-.23f, -.13f, -.11f) : new Vector3(0f, -.20f, .34f);
            foreach (Vector3 grip in new[] { right, left })
            {
                Part("Held glove", PrimitiveType.Cube, model, grip,
                    new Vector3(.13f, .12f, .16f), game.BootMaterial);
                Part("Held sleeve", PrimitiveType.Cylinder, model,
                    grip + new Vector3(grip == right ? .13f : -.15f, -.18f, -.13f),
                    new Vector3(.16f, .19f, .16f), cloth)
                    .transform.localRotation = Quaternion.Euler(33f, 0f,
                        grip == right ? -27f : 27f);
            }
        }

        private static void Ring(string name, Transform parent, float z,
            float radius, float depth, Material material)
        {
            var ring = new GameObject(name);
            ring.transform.SetParent(parent, false);
            ring.transform.localPosition = Vector3.forward * z;
            for (int i = 0; i < 12; i++)
            {
                float angle = i * Mathf.PI / 6f;
                Part("Collar segment", PrimitiveType.Cube, ring.transform,
                    new Vector3(Mathf.Sin(angle) * radius, Mathf.Cos(angle) * radius, 0f),
                    new Vector3(radius * .55f, .025f, depth), material)
                    .transform.localRotation = Quaternion.Euler(0f, 0f, -angle * Mathf.Rad2Deg);
            }
        }

        public static void BuildClassGear(Transform soldier,
            PrototypeInfantryClass role, PrototypeRuntime game,
            PrototypeTeam team = PrototypeTeam.Blue)
        {
            Material accent = team == PrototypeTeam.Blue ?
                game.BlueTrimMaterial : game.RedTrimMaterial;
            if (role == PrototypeInfantryClass.Engineer)
            {
                GameObject launcher = BuildLauncher(soldier, game);
                launcher.name = "Stowed launcher";
                launcher.transform.localPosition = new Vector3(-.37f, 1.16f, -.42f);
                launcher.transform.localRotation = Quaternion.Euler(-20f, 0f, -18f);
                launcher.transform.localScale = Vector3.one * .55f;
                Part("Repair tool", PrimitiveType.Cube, soldier,
                    new Vector3(.37f, .74f, -.18f), new Vector3(.14f, .38f, .13f),
                    game.LampMaterial);
            }
            else if (role == PrototypeInfantryClass.Medic)
            {
                Part("Medical kit", PrimitiveType.Cube, soldier,
                    new Vector3(-.40f, .77f, -.11f), new Vector3(.29f, .29f, .21f),
                    accent);
                Part("Medical cross vertical", PrimitiveType.Cube, soldier,
                    new Vector3(-.40f, .77f, .001f), new Vector3(.055f, .17f, .012f),
                    game.LampMaterial);
                Part("Medical cross horizontal", PrimitiveType.Cube, soldier,
                    new Vector3(-.40f, .77f, .003f), new Vector3(.17f, .055f, .012f),
                    game.LampMaterial);
            }
            else if (role == PrototypeInfantryClass.Recon)
            {
                Part("Respawn beacon casing", PrimitiveType.Cylinder, soldier,
                    new Vector3(.35f, .88f, -.29f), new Vector3(.12f, .31f, .12f),
                    game.GunMaterial);
                Part("Beacon antenna", PrimitiveType.Cylinder, soldier,
                    new Vector3(.35f, 1.31f, -.29f), new Vector3(.018f, .20f, .018f),
                    accent);
            }
            else
            {
                Part("Ammunition satchel", PrimitiveType.Cube, soldier,
                    new Vector3(.37f, .77f, -.16f), new Vector3(.32f, .27f, .22f),
                    game.ArmorMaterial);
                for (int i = 0; i < 3; i++)
                    Part("Magazine", PrimitiveType.Cube, soldier,
                        new Vector3(.24f + i * .12f, .81f, -.04f),
                        new Vector3(.085f, .18f, .055f), game.GunMaterial);
            }
        }

        private static GameObject Part(string name, PrimitiveType shape,
            Transform parent, Vector3 position, Vector3 scale, Material material) =>
            PrototypeSoldierVisual.Part(name, shape, parent, position, scale, material);
    }
    // Every generated optic mesh belongs to its visual object. Equipping,
    // changing views, death and Play-mode cleanup cannot accumulate meshes.
    public sealed class PrototypeEquipmentMeshOwner : MonoBehaviour
    {
        public Mesh OwnedMesh;
        private void OnDestroy()
        {
            if (OwnedMesh == null) return;
            if (Application.isPlaying) Object.Destroy(OwnedMesh);
            else Object.DestroyImmediate(OwnedMesh);
            OwnedMesh = null;
        }
    }
}
