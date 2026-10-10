using System.Collections.Generic;
using UnityEngine;

namespace Ironfront.UnityPrototype
{
    // A parked T90 BASTION model preview. It is deliberately separate from
    // IPrototypeVehicle until cannon combat and tank seats are ported.
    public sealed class PrototypeTankVisual : MonoBehaviour
    {
        public PrototypeTeam Team { get; private set; }
        public Vector2 MapPosition => new Vector2(transform.position.x, transform.position.z);

        public void Initialize(PrototypeRuntime game, PrototypeTeam team,
            Vector2 position, float yaw)
        {
            Team = team;
            transform.position = new Vector3(position.x,
                PrototypeLayout.HeightAt(position.x, position.y) + .05f, position.y);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            Material armor = team == PrototypeTeam.Blue ? game.BlueMaterial : game.RedMaterial;
            Material trim = team == PrototypeTeam.Blue ?
                game.BlueTrimMaterial : game.RedTrimMaterial;
            Material dark = game.ArmorMaterial;
            Material steel = game.GunMaterial;
            Material rubber = game.BootMaterial;

            var hull = new GameObject("Welded sloped hull").transform;
            hull.SetParent(transform, false);
            Wedge("Lower armored tub", hull, 4.15f, 4.48f,
                -3.53f, 3.44f, -3.40f, 3.10f, .43f, 1.34f, dark);
            Wedge("Angled upper glacis", hull, 4.25f, 3.76f,
                -3.17f, 3.32f, -2.83f, 1.92f, 1.13f, 2.09f, armor);
            Box("Deck armor", hull, new Vector3(0f, 2.05f, -1.32f),
                new Vector3(3.66f, .11f, 3.18f), armor);
            Box("Glacis ridge", hull, new Vector3(0f, 1.80f, 2.38f),
                new Vector3(3.65f, .09f, .16f), trim).transform.localRotation =
                Quaternion.Euler(-22f, 0f, 0f);
            Box("Front lower armor", hull, new Vector3(0f, .93f, 3.44f),
                new Vector3(4.16f, .45f, .16f), dark);
            Box("Rear armor", hull, new Vector3(0f, 1.16f, -3.54f),
                new Vector3(4.37f, .70f, .17f), armor);

            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * 2.42f;
                Box("Track bed", hull, new Vector3(x, .77f, 0f),
                    new Vector3(.75f, .83f, 7.25f), rubber);
                Box("Upper track run", hull, new Vector3(x, 1.31f, 0f),
                    new Vector3(.81f, .18f, 7.47f), rubber);
                Box("Lower track run", hull, new Vector3(x, .22f, 0f),
                    new Vector3(.81f, .18f, 7.47f), rubber);
                for (int wheel = 0; wheel < 7; wheel++)
                {
                    float z = -2.97f + wheel * .99f;
                    Cylinder("Road wheel", hull,
                        new Vector3(side * 2.80f, .77f, z), .87f, .24f,
                        dark).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    Cylinder("Wheel hub", hull,
                        new Vector3(side * 2.94f, .77f, z), .37f, .05f,
                        trim).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                }
                for (int tread = 0; tread < 23; tread++)
                {
                    float z = -3.42f + tread * .31f;
                    Box("Top track shoe", hull,
                        new Vector3(x, 1.43f, z),
                        new Vector3(.85f, .09f, .23f), dark);
                    Box("Lower track shoe", hull,
                        new Vector3(x, .10f, z),
                        new Vector3(.85f, .08f, .23f), dark);
                }
                for (int panel = 0; panel < 5; panel++)
                {
                    float z = -2.62f + panel * 1.31f;
                    Box("Separated side skirt", hull,
                        new Vector3(side * 2.32f, 1.65f, z),
                        new Vector3(.17f, .62f, 1.23f), armor);
                    Box("Skirt edge", hull,
                        new Vector3(side * 2.43f, 1.33f, z),
                        new Vector3(.08f, .07f, 1.14f), trim);
                }
                Box("Side marker", hull,
                    new Vector3(side * 2.43f, 1.86f, 2.39f),
                    new Vector3(.08f, .15f, .52f), trim);
                Box("Headlamp guard", hull,
                    new Vector3(side * 1.55f, 1.44f, 3.29f),
                    new Vector3(.56f, .36f, .18f), steel);
                Box("Headlamp", hull,
                    new Vector3(side * 1.55f, 1.44f, 3.40f),
                    new Vector3(.36f, .23f, .055f), game.LampMaterial);
                Box("Tow lug", hull,
                    new Vector3(side * 1.48f, .67f, 3.58f),
                    new Vector3(.27f, .25f, .36f), steel);
            }
            for (int grille = 0; grille < 7; grille++)
                Box("Engine vent", hull,
                    new Vector3(0f, 2.125f, -1.17f - grille * .24f),
                    new Vector3(2.72f, .025f, .105f), steel);
            for (int side = -1; side <= 1; side += 2)
                Cylinder("Rear fuel drum", hull,
                    new Vector3(side * 1.18f, 1.46f, -3.65f),
                    .62f, 1.22f, dark, true);

            var turret = new GameObject("Rotatable armored turret").transform;
            turret.SetParent(transform, false);
            turret.localPosition = new Vector3(0f, 2.06f, -.55f);
            Cylinder("Turret race", turret, new Vector3(0f, .04f, 0f),
                2.85f, .16f, steel);
            Wedge("Cast turret armor", turret, 3.35f, 2.65f,
                -1.62f, 1.53f, -1.25f, .93f, .10f, 1.18f, armor);
            Box("Turret front cheek", turret,
                new Vector3(-1.01f, .60f, 1.15f),
                new Vector3(.84f, .62f, .67f), trim).transform.localRotation =
                Quaternion.Euler(0f, -17f, -10f);
            Box("Turret front cheek", turret,
                new Vector3(1.01f, .60f, 1.15f),
                new Vector3(.84f, .62f, .67f), trim).transform.localRotation =
                Quaternion.Euler(0f, 17f, 10f);
            Box("Gun mantlet", turret, new Vector3(0f, .65f, 1.24f),
                new Vector3(.94f, .81f, .51f), dark);
            Cylinder("Main cannon", turret,
                new Vector3(0f, .68f, 3.26f), .31f, 4.00f, steel, true);
            Cylinder("Thermal sleeve", turret,
                new Vector3(0f, .68f, 2.49f), .43f, 1.42f, armor, true);
            Cylinder("Muzzle brake", turret,
                new Vector3(0f, .68f, 5.30f), .48f, .45f, dark, true);
            Box("Muzzle port left", turret, new Vector3(-.24f, .68f, 5.30f),
                new Vector3(.035f, .15f, .23f), rubber);
            Box("Muzzle port right", turret, new Vector3(.24f, .68f, 5.30f),
                new Vector3(.035f, .15f, .23f), rubber);
            Cylinder("Coaxial machine gun", turret,
                new Vector3(.57f, .61f, 2.15f), .095f, 1.70f, steel, true);
            Box("Turret bustle", turret,
                new Vector3(0f, .61f, -1.55f),
                new Vector3(2.68f, .65f, .68f), dark);
            Box("Bustle rail", turret,
                new Vector3(0f, 1.00f, -1.60f),
                new Vector3(2.71f, .06f, .73f), trim);
            Cylinder("Commander cupola", turret,
                new Vector3(-.67f, 1.30f, -.32f), .90f, .30f, dark);
            Cylinder("Commander hatch", turret,
                new Vector3(-.67f, 1.50f, -.32f), .77f, .07f, trim);
            Box("Gunner optic", turret,
                new Vector3(.73f, 1.22f, .43f),
                new Vector3(.55f, .35f, .62f), dark);
            Box("Gunner optic glass", turret,
                new Vector3(.73f, 1.22f, .76f),
                new Vector3(.38f, .22f, .055f), game.GlassMaterial);
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < 3; i++)
                    Cylinder("Smoke launcher", turret,
                        new Vector3(side * (1.29f + i * .10f),
                            .54f + i * .14f, .43f - i * .23f),
                        .16f, .52f, steel, true).transform.localRotation =
                        Quaternion.Euler(-15f, side * 36f, 0f);
                Cylinder("Radio antenna", turret,
                    new Vector3(side * 1.10f, 2.02f, -1.01f),
                    .035f, 1.77f, steel);
            }
            Box("Team ID bar", turret,
                new Vector3(0f, 1.19f, -.74f),
                new Vector3(.96f, .045f, .29f), trim);

            BoxCollider collider = gameObject.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 1.15f, 0f);
            collider.size = new Vector3(5.50f, 2.30f, 7.55f);
        }

        private static GameObject Box(string name, Transform parent,
            Vector3 position, Vector3 scale, Material material)
        {
            return Part(name, PrimitiveType.Cube, parent, position, scale, material);
        }

        private static GameObject Cylinder(string name, Transform parent,
            Vector3 position, float diameter, float length, Material material,
            bool horizontal = false)
        {
            GameObject part = Part(name, PrimitiveType.Cylinder, parent, position,
                new Vector3(diameter, length * .5f, diameter), material);
            if (horizontal) part.transform.localRotation =
                Quaternion.Euler(90f, 0f, 0f);
            return part;
        }

        private static GameObject Part(string name, PrimitiveType shape,
            Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(shape);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            Collider primitiveCollider = part.GetComponent<Collider>();
            if (primitiveCollider != null) Destroy(primitiveCollider);
            part.GetComponent<Renderer>().sharedMaterial = material;
            return part;
        }

        private static void Wedge(string name, Transform parent, float lowerWidth,
            float upperWidth, float lowerRear, float lowerFront,
            float upperRear, float upperFront, float lowerY, float upperY,
            Material material)
        {
            float lo = lowerWidth * .5f;
            float hi = upperWidth * .5f;
            var p = new[]
            {
                new Vector3(-lo, lowerY, lowerRear),
                new Vector3( lo, lowerY, lowerRear),
                new Vector3( lo, lowerY, lowerFront),
                new Vector3(-lo, lowerY, lowerFront),
                new Vector3(-hi, upperY, upperRear),
                new Vector3( hi, upperY, upperRear),
                new Vector3( hi, upperY, upperFront),
                new Vector3(-hi, upperY, upperFront)
            };
            var vertices = new List<Vector3>(24);
            var triangles = new List<int>(36);
            Face(vertices, triangles, p[0], p[1], p[2], p[3]);
            Face(vertices, triangles, p[4], p[7], p[6], p[5]);
            Face(vertices, triangles, p[1], p[0], p[4], p[5]);
            Face(vertices, triangles, p[3], p[2], p[6], p[7]);
            Face(vertices, triangles, p[0], p[3], p[7], p[4]);
            Face(vertices, triangles, p[2], p[1], p[5], p[6]);
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            var part = new GameObject(name);
            part.transform.SetParent(parent, false);
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            part.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static void Face(List<Vector3> vertices, List<int> triangles,
            Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int start = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            vertices.Add(d);
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
            triangles.Add(start);
            triangles.Add(start + 2);
            triangles.Add(start + 3);
        }
    }
}
