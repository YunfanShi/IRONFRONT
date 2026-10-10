using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ironfront.UnityPrototype
{
    // Decorative geometry follows the browser map. PrototypeLayout remains the
    // single source of collision, line-of-sight and navigation footprints.
    public sealed class PrototypeWorldVisual : MonoBehaviour
    {
        private sealed class MeshBatch
        {
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<int> Triangles = new List<int>();

            public void Triangle(Vector3 a, Vector3 b, Vector3 c)
            {
                int start = Vertices.Count;
                Vertices.Add(a); Vertices.Add(b); Vertices.Add(c);
                Triangles.Add(start); Triangles.Add(start + 1); Triangles.Add(start + 2);
            }

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                int start = Vertices.Count;
                Vertices.Add(a); Vertices.Add(b); Vertices.Add(c); Vertices.Add(d);
                Triangles.Add(start); Triangles.Add(start + 1); Triangles.Add(start + 2);
                Triangles.Add(start); Triangles.Add(start + 2); Triangles.Add(start + 3);
            }

            public void Box(Vector3 center, Vector3 size, float angle = 0f) =>
                Box(center, size, Quaternion.Euler(0f, angle, 0f));

            public void Box(Vector3 center, Vector3 size, Quaternion turn)
            {
                Vector3 half = size * .5f;
                Vector3 Point(float x, float y, float z) =>
                    center + turn * new Vector3(x * half.x, y * half.y, z * half.z);
                Vector3 a = Point(-1f, -1f, -1f), b = Point(1f, -1f, -1f);
                Vector3 c = Point(1f, -1f, 1f), d = Point(-1f, -1f, 1f);
                Vector3 e = Point(-1f, 1f, -1f), f = Point(1f, 1f, -1f);
                Vector3 g = Point(1f, 1f, 1f), h = Point(-1f, 1f, 1f);
                Quad(a, b, c, d); Quad(e, h, g, f);
                Quad(b, a, e, f); Quad(d, c, g, h);
                Quad(a, d, h, e); Quad(c, b, f, g);
            }

            public void Pine(Vector3 basePosition, float radius, float height)
            {
                const int sides = 7;
                Vector3 tip = basePosition + Vector3.up * height;
                for (int i = 0; i < sides; i++)
                {
                    float a = i * Mathf.PI * 2f / sides;
                    float b = (i + 1) * Mathf.PI * 2f / sides;
                    Vector3 p = basePosition + new Vector3(Mathf.Cos(a) * radius,
                        0f, Mathf.Sin(a) * radius);
                    Vector3 q = basePosition + new Vector3(Mathf.Cos(b) * radius,
                        0f, Mathf.Sin(b) * radius);
                    Triangle(p, tip, q);
                }
            }
        }

        private static readonly Vector2[][] RoadLines =
        {
            new[] { new Vector2(-320,-296), new Vector2(-185,-210),
                new Vector2(-58,-180), new Vector2(37,-62),
                new Vector2(140,65), new Vector2(301,296) },
            new[] { new Vector2(-291,160), new Vector2(-171,116),
                new Vector2(-26,80), new Vector2(140,113),
                new Vector2(302,154) },
            new[] { new Vector2(-210,-144), new Vector2(-15,-92),
                new Vector2(17,80), new Vector2(-186,185) }
        };

        private readonly Dictionary<Material, MeshBatch> batches =
            new Dictionary<Material, MeshBatch>();
        private Material concrete, plaster, olive, roof, roofLight, trim;
        private Material glass, metal, paleMetal, timber, ballast, road, dirt;
        private Material trunk, needles, needlesLight, stone, lamp, bluePad, redPad;

        public void Initialize()
        {
            concrete = MakeMaterial(new Color(.43f, .46f, .43f));
            plaster = MakeMaterial(new Color(.68f, .61f, .50f));
            olive = MakeMaterial(new Color(.34f, .40f, .31f));
            roof = MakeMaterial(new Color(.19f, .25f, .26f));
            roofLight = MakeMaterial(new Color(.34f, .41f, .43f));
            trim = MakeMaterial(new Color(.29f, .31f, .29f));
            glass = MakeMaterial(new Color(.11f, .22f, .25f));
            metal = MakeMaterial(new Color(.29f, .36f, .38f));
            paleMetal = MakeMaterial(new Color(.56f, .55f, .49f));
            timber = MakeMaterial(new Color(.52f, .43f, .30f));
            ballast = MakeMaterial(new Color(.43f, .42f, .38f));
            road = MakeMaterial(new Color(.47f, .45f, .40f));
            dirt = MakeMaterial(new Color(.53f, .49f, .40f));
            trunk = MakeMaterial(new Color(.31f, .27f, .20f));
            needles = MakeMaterial(new Color(.20f, .36f, .28f));
            needlesLight = MakeMaterial(new Color(.27f, .42f, .32f));
            stone = MakeMaterial(new Color(.42f, .43f, .39f));
            lamp = MakeMaterial(new Color(.89f, .72f, .48f));
            bluePad = MakeMaterial(new Color(.22f, .43f, .51f));
            redPad = MakeMaterial(new Color(.50f, .32f, .28f));

            BuildRoads();
            BuildRailway();
            BuildBuildings();
            BuildLandmarks();
            BuildFoliage();
            Flush();
        }

        private static Material MakeMaterial(Color color)
        {
            Material template = Resources.Load<Material>("Materials/PrototypeLit");
            Shader shader = template == null ?
                Shader.Find("Universal Render Pipeline/Lit") : null;
            if (template == null && shader == null) shader = Shader.Find("Standard");
            if (template == null && shader == null)
                throw new InvalidOperationException("Map material shader is unavailable.");
            var material = template == null ? new Material(shader) : new Material(template);
            material.name = "Map " + ColorUtility.ToHtmlStringRGB(color);
            material.color = color;
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            return material;
        }

        private MeshBatch Batch(Material material)
        {
            if (!batches.TryGetValue(material, out MeshBatch batch))
            {
                batch = new MeshBatch();
                batches.Add(material, batch);
            }
            return batch;
        }

        private void Box(Material material, float x, float y, float z,
            float width, float height, float depth, float yaw = 0f)
        {
            Batch(material).Box(new Vector3(x, y, z),
                new Vector3(width, height, depth), yaw);
        }

        private static float Floor(float x, float z) => PrototypeLayout.HeightAt(x, z);

        private void Ribbon(Vector2 from, Vector2 to, float width,
            float lift, Material material)
        {
            Vector2 direction = to - from;
            float length = direction.magnitude;
            if (length < .01f) return;
            Vector2 side = new Vector2(-direction.y, direction.x) / length * (width / 2f);
            int steps = Mathf.CeilToInt(length / 4f);
            for (int i = 0; i < steps; i++)
            {
                Vector2 a = Vector2.Lerp(from, to, (float)i / steps);
                Vector2 b = Vector2.Lerp(from, to, (float)(i + 1) / steps);
                Vector2 al = a + side, ar = a - side;
                Vector2 bl = b + side, br = b - side;
                Batch(material).Quad(
                    new Vector3(al.x, Floor(al.x, al.y) + lift, al.y),
                    new Vector3(bl.x, Floor(bl.x, bl.y) + lift, bl.y),
                    new Vector3(br.x, Floor(br.x, br.y) + lift, br.y),
                    new Vector3(ar.x, Floor(ar.x, ar.y) + lift, ar.y));
            }
        }

        private void BuildRoads()
        {
            foreach (Vector2[] line in RoadLines)
                for (int i = 0; i < line.Length - 1; i++)
                {
                    Ribbon(line[i], line[i + 1], 14f, .09f, dirt);
                    Ribbon(line[i], line[i + 1], 11f, .12f, road);
                }
            // The main road and its branches use the same coordinates as main.
            Vector2 blue = PrototypeLayout.BlueBase;
            Vector2 red = PrototypeLayout.RedBase;
            Box(bluePad, blue.x, Floor(blue.x, blue.y) + .19f,
                blue.y - 21f, 40f, .36f, 16f);
            Box(redPad, red.x, Floor(red.x, red.y) + .19f,
                red.y - 21f, 40f, .36f, 16f);
        }

        private void BuildRailway()
        {
            foreach (float z in new[] { 47f, 132f })
            {
                Ribbon(new Vector2(-286f, z), new Vector2(294f, z),
                    10.4f, .13f, ballast);
                foreach (float side in new[] { -2.6f, 2.6f })
                    Ribbon(new Vector2(-286f, z + side),
                        new Vector2(294f, z + side), .28f, .35f, paleMetal);
                for (float x = -285f; x < 295f; x += 7.6f)
                    Box(timber, x, Floor(x, z) + .25f, z,
                        1.1f, .22f, 9.4f);
            }
        }

        private void BuildBuildings()
        {
            foreach (PrototypeLayout.Block block in PrototypeLayout.Blocks)
            {
                float x = block.Position.x, z = block.Position.y;
                float y = Floor(x, z), w = block.Width, d = block.Depth;
                float h = block.Height;
                switch (block.Kind)
                {
                    case "house": House(x, y, z, w, h, d); break;
                    case "factory": Factory(x, y, z, w, h, d); break;
                    case "barracks": Barracks(x, y, z, w, h, d); break;
                    case "container": Container(x, y, z, w, h, d); break;
                    case "railcar": Railcar(x, y, z, w, h, d); break;
                    case "crate": Crate(x, y, z, w, h, d); break;
                    case "wall":
                        Box(concrete, x, y + h / 2f, z, w, h, d);
                        Box(paleMetal, x, y + h + .13f, z,
                            w + .18f, .26f, d + .18f);
                        break;
                    case "shedpost":
                        Box(metal, x, y + h / 2f, z, w, h, d);
                        Box(trim, x, y + .30f, z, w + .3f, .6f, d + .3f);
                        break;
                }
            }
            foreach (Vector2 center in new[] { new Vector2(-7f,-16f),
                         new Vector2(-40f,146f), new Vector2(116f,-160f) })
            {
                float width = center.x == 116f ? 32f : 29f;
                float depth = center.x == -7f ? 24f : center.x == -40f ? 23f : 32f;
                float height = center.x == -7f ? 7.6f : center.x == -40f ? 7.3f : 7f;
                float y = Floor(center.x, center.y);
                Box(roof, center.x, y + height + .4f, center.y,
                    width + 1f, .8f, depth + 1f);
                Box(concrete, center.x, y + .10f, center.y,
                    width - .8f, .2f, depth - .8f);
            }
            foreach (Vector2 center in new[] { new Vector2(-29f,189f),
                         new Vector2(96f,-201f) })
                Box(roofLight, center.x, Floor(center.x, center.y) + 7.55f,
                    center.y, 31f, 1.1f, 24f);
        }

        private void Door(float x, float y, float front)
        {
            Box(metal, x, y + 1.55f, front + .14f, 2.2f, 3.1f, .22f);
            Box(paleMetal, x, y + 3.15f, front + .23f, 2.7f, .18f, .30f);
            Box(lamp, x + .71f, y + 1.45f, front + .29f, .10f, .12f, .08f);
        }

        private void Windows(float x, float y, float z, float w,
            float h, float d, int count)
        {
            float centerY = y + Mathf.Min(5f, h - 2f);
            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < count; i++)
                {
                    float wx = x + (i - (count - 1) * .5f) * w / (count + .3f);
                    float wz = z + side * (d / 2f + .08f);
                    Box(glass, wx, centerY, wz, 2.65f, 1.9f, .14f);
                    Box(paleMetal, wx, centerY - 1.06f, wz + side * .10f,
                        2.96f, .17f, .24f);
                    Box(trim, wx, centerY, wz + side * .13f,
                        .11f, 1.9f, .07f);
                }
        }

        private void House(float x, float y, float z, float w, float h, float d)
        {
            Box(plaster, x, y + h / 2f, z, w, h, d);
            float rise = 2.6f, slope = Mathf.Atan2(rise, w / 2f) * Mathf.Rad2Deg;
            float length = Mathf.Sqrt(w * w / 4f + rise * rise) + 1.1f;
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 center = new Vector3(x + side * w / 4f,
                    y + h + rise / 2f, z);
                Batch(roof).Box(center, new Vector3(length, .32f, d + 1.3f),
                    Quaternion.Euler(0f, 0f, -side * slope));
            }
            for (int end = -1; end <= 1; end += 2)
            {
                float facade = z + end * d / 2f;
                Batch(plaster).Triangle(
                    new Vector3(x - w / 2f, y + h, facade),
                    new Vector3(x, y + h + rise, facade),
                    new Vector3(x + w / 2f, y + h, facade));
                Batch(plaster).Triangle(
                    new Vector3(x + w / 2f, y + h, facade),
                    new Vector3(x, y + h + rise, facade),
                    new Vector3(x - w / 2f, y + h, facade));
            }
            Windows(x, y, z, w, h, d, 3);
            Door(x, y, z + d / 2f);
            Box(trim, x - w / 2f, y + h / 2f,
                z + d / 2f, .32f, h, .34f);
            Box(trim, x + w / 2f, y + h / 2f,
                z + d / 2f, .32f, h, .34f);
            Box(stone, x + w * .28f, y + h + rise * .65f,
                z - d * .22f, 1.45f, rise * 1.6f, 1.3f);
        }

        private void Factory(float x, float y, float z, float w, float h, float d)
        {
            Box(concrete, x, y + h / 2f, z, w, h, d);
            Box(roof, x, y + h + .30f, z, w + 1.5f, .6f, d + 1.5f);
            Windows(x, y, z, w, h, d, w > 35f ? 4 : 3);
            Door(x, y, z + d / 2f);
            for (int i = 0; i < 3; i++)
            {
                float vx = x - w * .29f + i * w * .29f;
                Box(metal, vx, y + h + 1.5f, z - d * .25f,
                    1.8f, 2.4f, 1.8f);
                Box(trim, vx, y + h + 2.8f, z - d * .25f,
                    2.1f, .25f, 2.1f);
            }
            for (int side = -1; side <= 1; side += 2)
                Box(trim, x, y + h + .65f, z + side * d / 2f,
                    w + 1.2f, .25f, .46f);
        }

        private void Barracks(float x, float y, float z, float w, float h, float d)
        {
            Box(olive, x, y + h / 2f, z, w, h, d);
            Box(roofLight, x, y + h + .24f, z,
                w + 1.4f, .48f, d + 1.4f);
            Windows(x, y, z, w, h, d, w > 35f ? 4 : 3);
            Door(x, y, z + d / 2f);
            for (int i = -1; i <= 1; i++)
                Box(metal, x + i * w / 4f, y + h + .84f,
                    z, 2.4f, .72f, 2.0f);
        }

        private void Container(float x, float y, float z, float w, float h, float d)
        {
            Box(metal, x, y + h / 2f, z, w, h, d);
            Box(trim, x, y + h - .12f, z, w + .12f, .22f, d + .12f);
            for (int i = 0; i < 6; i++)
                for (int side = -1; side <= 1; side += 2)
                    Box(paleMetal, x + (i - 2.5f) * w / 6f,
                        y + h / 2f, z + side * (d / 2f + .04f),
                        .12f, h - .3f, .09f);
            Box(trim, x + w / 2f + .05f, y + h / 2f,
                z, .12f, h - .2f, d - .2f);
        }

        private void Railcar(float x, float y, float z, float w, float h, float d)
        {
            Box(metal, x, y + h / 2f, z, w, h, d);
            Box(roofLight, x, y + h + .2f, z,
                w + .5f, .4f, d + .5f);
            for (int side = -1; side <= 1; side += 2)
                for (int i = -1; i <= 1; i++)
                    Box(glass, x + i * 4.2f, y + h * .70f,
                        z + side * (d / 2f + .08f), 2.2f, 1.2f, .14f);
            for (int i = -1; i <= 1; i += 2)
                Box(trim, x + i * 5f, y + .56f, z,
                    2.3f, 1.12f, d + .35f);
        }

        private void Crate(float x, float y, float z, float w, float h, float d)
        {
            Box(timber, x, y + h / 2f, z, w, h, d);
            Box(trim, x, y + h * .72f, z + d / 2f + .07f,
                w + .06f, .17f, .16f);
            Box(trim, x, y + h * .28f, z - d / 2f - .07f,
                w + .06f, .17f, .16f);
            for (int side = -1; side <= 1; side += 2)
                Box(paleMetal, x + side * (w / 2f - .20f), y + h / 2f,
                    z + d / 2f + .08f, .16f, h - .16f, .12f);
        }

        private void BuildLandmarks()
        {
            float x = -185f, z = 244f, y = Floor(x, z);
            Box(metal, x, y + 34.5f, z, .90f, 69f, .90f);
            for (int h = 10; h < 70; h += 13)
            {
                Box(trim, x, y + h, z, 18f, .45f, .55f);
                Box(trim, x, y + h, z, .55f, .45f, 18f);
            }
            Box(lamp, x, y + 69.5f, z, 1.4f, 1f, 1.4f);
            foreach (Vector2 tower in new[] { new Vector2(-177f,246f),
                         new Vector2(289f,-235f), new Vector2(155f,253f) })
            {
                float baseY = Floor(tower.x, tower.y);
                Box(metal, tower.x, baseY + 20f, tower.y,
                    .68f, 40f, .68f);
                for (int h = 4; h < 40; h += 4)
                {
                    Box(h % 8 == 0 ? timber : trim,
                        tower.x, baseY + h, tower.y, 7f, .20f, .35f);
                    if (h % 8 == 0)
                        for (int side = -1; side <= 1; side += 2)
                            Box(metal, tower.x + side * 2.3f,
                                baseY + h - 2f, tower.y,
                                .18f, 4.2f, .18f);
                }
            }
        }

        private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 d = b - a;
            float lengthSquared = d.sqrMagnitude;
            float t = lengthSquared < .01f ? 0f :
                Mathf.Clamp01(Vector2.Dot(point - a, d) / lengthSquared);
            return Vector2.Distance(point, a + d * t);
        }

        private static bool NearRoad(Vector2 position)
        {
            foreach (Vector2[] line in RoadLines)
                for (int i = 0; i < line.Length - 1; i++)
                    if (DistanceToSegment(position, line[i], line[i + 1]) < 9f)
                        return true;
            return false;
        }

        private void BuildFoliage()
        {
            var random = new System.Random(22305);
            int planted = 0;
            for (int attempt = 0; attempt < 600 && planted < 165; attempt++)
            {
                float x = -345f + (float)random.NextDouble() * 690f;
                float z = -345f + (float)random.NextDouble() * 690f;
                var position = new Vector2(x, z);
                if (PrototypeLayout.Collides(position, 5f) || NearRoad(position) ||
                    Mathf.Abs(z - 47f) < 12f || Mathf.Abs(z - 132f) < 12f ||
                    Vector2.Distance(position, PrototypeLayout.BlueBase) < 36f ||
                    Vector2.Distance(position, PrototypeLayout.RedBase) < 36f)
                    continue;
                bool nearObjective = false;
                foreach (PrototypeLayout.Objective objective in PrototypeLayout.Objectives)
                    if (Vector2.Distance(position, objective.Position) < 34f)
                        nearObjective = true;
                if (nearObjective) continue;
                float scale = .72f + (float)random.NextDouble() * .9f;
                float y = Floor(x, z);
                Box(trunk, x, y + 2.0f * scale, z,
                    .70f * scale, 4f * scale, .70f * scale);
                Batch(needles).Pine(new Vector3(x, y + 3.3f * scale, z),
                    4.3f * scale, 9f * scale);
                Batch(needlesLight).Pine(new Vector3(x, y + 7.0f * scale, z),
                    3.1f * scale, 6.5f * scale);
                planted++;
            }
            for (int i = 0; i < 110; i++)
            {
                float x = -330f + (float)random.NextDouble() * 660f;
                float z = -330f + (float)random.NextDouble() * 660f;
                if (PrototypeLayout.Collides(new Vector2(x, z), 2f) ||
                    NearRoad(new Vector2(x, z))) continue;
                float s = .55f + (float)random.NextDouble() * 1.3f;
                Box(stone, x, Floor(x, z) + .22f * s, z,
                    1.5f * s, .45f * s, 1.1f * s,
                    (float)random.NextDouble() * 180f);
            }
        }

        private void Flush()
        {
            foreach (KeyValuePair<Material, MeshBatch> entry in batches)
            {
                if (entry.Value.Vertices.Count == 0) continue;
                var mesh = new Mesh { name = "IRONFRONT " + entry.Key.name };
                mesh.indexFormat = IndexFormat.UInt32;
                mesh.SetVertices(entry.Value.Vertices);
                mesh.SetTriangles(entry.Value.Triangles, 0);
                mesh.RecalculateNormals();
                var layer = new GameObject("Map detail " + entry.Key.name);
                layer.transform.SetParent(transform, false);
                layer.AddComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer renderer = layer.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = entry.Key;
                renderer.shadowCastingMode = entry.Key == road ||
                    entry.Key == dirt || entry.Key == ballast ||
                    entry.Key == needles || entry.Key == needlesLight ?
                    ShadowCastingMode.Off : ShadowCastingMode.On;
            }
            batches.Clear();
        }
    }
}
