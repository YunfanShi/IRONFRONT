using System.Collections.Generic;
using UnityEngine;

namespace Ironfront.UnityPrototype
{
    // A deliberately small playable slice. The browser game remains the parity reference.
    public sealed class PrototypeRuntime : MonoBehaviour
    {
        private readonly Dictionary<PrototypeCapturePoint, Renderer> markers =
            new Dictionary<PrototypeCapturePoint, Renderer>();
        private Material groundMaterial;
        private Material buildingMaterial;
        private Material coverMaterial;
        private Material markerNeutral;
        private Material markerBlue;
        private Material markerRed;
        private Material blueTracer;
        private Material redTracer;

        public PrototypeMatch Match { get; private set; }
        public PrototypeNavigation Navigation { get; private set; }
        public PrototypePlayer Player { get; private set; }
        public readonly List<PrototypeBot> Bots = new List<PrototypeBot>();
        public Material BlueMaterial { get; private set; }
        public Material RedMaterial { get; private set; }
        public Material GunMaterial { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (GameObject.Find("IRONFRONT Unity Prototype") == null)
                new GameObject("IRONFRONT Unity Prototype").AddComponent<PrototypeRuntime>();
        }

        private void Awake()
        {
            foreach (Camera existing in Camera.allCameras)
            {
                existing.enabled = false;
                AudioListener listener = existing.GetComponent<AudioListener>();
                if (listener != null) listener.enabled = false;
            }
            Match = new PrototypeMatch();
            Navigation = new PrototypeNavigation();
            groundMaterial = MakeMaterial(new Color(0.35f, 0.43f, 0.31f));
            buildingMaterial = MakeMaterial(new Color(0.43f, 0.42f, 0.37f));
            coverMaterial = MakeMaterial(new Color(0.34f, 0.31f, 0.27f));
            BlueMaterial = MakeMaterial(new Color(0.22f, 0.48f, 0.7f));
            RedMaterial = MakeMaterial(new Color(0.72f, 0.28f, 0.23f));
            GunMaterial = MakeMaterial(new Color(0.12f, 0.15f, 0.16f));
            markerNeutral = MakeMaterial(new Color(0.9f, 0.83f, 0.58f));
            markerBlue = MakeMaterial(new Color(0.28f, 0.72f, 0.95f));
            markerRed = MakeMaterial(new Color(0.95f, 0.4f, 0.33f));
            blueTracer = MakeMaterial(new Color(0.62f, 0.88f, 1f), true);
            redTracer = MakeMaterial(new Color(1f, 0.64f, 0.48f), true);

            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.58f, 0.69f, 0.72f);
            RenderSettings.fogDensity = 0.0027f;
            BuildGround();
            BuildStructures();
            BuildMarkers();
            BuildPlayer();
            BuildBots();
        }

        private static Material MakeMaterial(Color color, bool unlit = false)
        {
            Shader shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" :
                "Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find(unlit ? "Unlit/Color" : "Standard");
            var material = new Material(shader);
            material.color = color;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            return material;
        }

        private void BuildGround()
        {
            const int segments = 90;
            const float size = PrototypeLayout.MapSize;
            var vertices = new Vector3[(segments + 1) * (segments + 1)];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[segments * segments * 6];
            for (int z = 0; z <= segments; z++)
                for (int x = 0; x <= segments; x++)
                {
                    float worldX = x * size / segments - size / 2f;
                    float worldZ = z * size / segments - size / 2f;
                    int index = z * (segments + 1) + x;
                    vertices[index] = new Vector3(worldX, PrototypeLayout.HeightAt(worldX, worldZ), worldZ);
                    uv[index] = new Vector2(x / 8f, z / 8f);
                }
            int triangle = 0;
            for (int z = 0; z < segments; z++)
                for (int x = 0; x < segments; x++)
                {
                    int a = z * (segments + 1) + x;
                    int b = a + 1;
                    int c = a + segments + 1;
                    int d = c + 1;
                    triangles[triangle++] = a; triangles[triangle++] = c; triangles[triangle++] = b;
                    triangles[triangle++] = b; triangles[triangle++] = c; triangles[triangle++] = d;
                }
            var mesh = new Mesh { name = "IRONFRONT Terrain" };
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            var terrain = new GameObject("Terrain 720 m");
            terrain.transform.SetParent(transform);
            terrain.AddComponent<MeshFilter>().sharedMesh = mesh;
            terrain.AddComponent<MeshRenderer>().sharedMaterial = groundMaterial;
            terrain.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        private void BuildStructures()
        {
            foreach (PrototypeLayout.Block block in PrototypeLayout.Blocks)
            {
                GameObject object3D = GameObject.CreatePrimitive(PrimitiveType.Cube);
                object3D.name = block.Kind;
                object3D.transform.SetParent(transform);
                object3D.transform.position = new Vector3(block.Position.x,
                    PrototypeLayout.HeightAt(block.Position.x, block.Position.y) + block.Height / 2f,
                    block.Position.y);
                object3D.transform.localScale = new Vector3(block.Width, block.Height, block.Depth);
                object3D.GetComponent<Renderer>().sharedMaterial = block.Kind == "crate" ||
                    block.Kind == "container" ? coverMaterial : buildingMaterial;
            }
        }

        private void BuildMarkers()
        {
            foreach (PrototypeCapturePoint point in Match.Points)
            {
                var ring = new GameObject("Objective " + point.Definition.Id);
                ring.transform.SetParent(transform);
                var mesh = new Mesh { name = "Capture Ring" };
                const int segments = 64;
                var vertices = new Vector3[segments * 2];
                var triangles = new int[segments * 6];
                for (int i = 0; i < segments; i++)
                {
                    float angle = i * Mathf.PI * 2f / segments;
                    float cos = Mathf.Cos(angle);
                    float sin = Mathf.Sin(angle);
                    for (int side = 0; side < 2; side++)
                    {
                        float radius = side == 0 ? 24f : 25f;
                        float x = point.Definition.Position.x + cos * radius;
                        float z = point.Definition.Position.y + sin * radius;
                        vertices[i * 2 + side] = new Vector3(x,
                            PrototypeLayout.HeightAt(x, z) + 0.35f, z);
                    }
                    int a = i * 2;
                    int b = ((i + 1) % segments) * 2;
                    int t = i * 6;
                    triangles[t] = a; triangles[t + 1] = b; triangles[t + 2] = a + 1;
                    triangles[t + 3] = a + 1; triangles[t + 4] = b; triangles[t + 5] = b + 1;
                }
                mesh.vertices = vertices;
                mesh.triangles = triangles;
                mesh.RecalculateNormals();
                ring.AddComponent<MeshFilter>().sharedMesh = mesh;
                Renderer renderer = ring.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = markerNeutral;
                markers[point] = renderer;

                GameObject flag = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                flag.name = "Flag " + point.Definition.Id;
                flag.transform.SetParent(ring.transform);
                Vector2 p = point.Definition.Position;
                flag.transform.position = new Vector3(p.x, PrototypeLayout.HeightAt(p.x, p.y) + 3f, p.y);
                flag.transform.localScale = new Vector3(0.25f, 3f, 0.25f);
                flag.GetComponent<Renderer>().sharedMaterial = markerNeutral;
                Destroy(flag.GetComponent<Collider>());
            }
        }

        private void BuildPlayer()
        {
            var character = new GameObject("Blue Player");
            character.transform.SetParent(transform);
            character.AddComponent<CharacterController>();
            Player = character.AddComponent<PrototypePlayer>();
            Player.Initialize(this);
        }

        private void BuildBots()
        {
            AddBot(PrototypeTeam.Blue, -80f, -120f, "B");
            AddBot(PrototypeTeam.Blue, -76f, -103f, "B");
            AddBot(PrototypeTeam.Blue, -30f, 70f, "C");
            AddBot(PrototypeTeam.Blue, -41f, 78f, "C");
            AddBot(PrototypeTeam.Red, 31f, -111f, "B");
            AddBot(PrototypeTeam.Red, 44f, -115f, "B");
            AddBot(PrototypeTeam.Red, 66f, 115f, "C");
            AddBot(PrototypeTeam.Red, 70f, 95f, "C");
        }

        private void AddBot(PrototypeTeam team, float x, float z, string objectiveId)
        {
            GameObject object3D = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            object3D.name = team + " Squad - " + objectiveId;
            object3D.transform.SetParent(transform);
            Vector2 goal = PrototypeLayout.Objectives[objectiveId == "B" ? 1 : 2].Position;
            PrototypeBot bot = object3D.AddComponent<PrototypeBot>();
            Bots.Add(bot);
            bot.Initialize(this, Navigation, team, new Vector2(x, z), goal);
        }

        private void Update()
        {
            if (Match == null || Player == null) return;
            Match.Tick(Mathf.Min(Time.deltaTime, 0.05f), Player, Bots);
            foreach (KeyValuePair<PrototypeCapturePoint, Renderer> entry in markers)
            {
                PrototypeCapturePoint point = entry.Key;
                entry.Value.sharedMaterial = point.Owner == PrototypeTeam.Blue ? markerBlue :
                    point.Owner == PrototypeTeam.Red ? markerRed : markerNeutral;
            }
        }

        public void ShowTracer(Vector3 from, Vector3 to, PrototypeTeam team)
        {
            var object3D = new GameObject("Tracer");
            LineRenderer line = object3D.AddComponent<LineRenderer>();
            line.sharedMaterial = team == PrototypeTeam.Blue ? blueTracer : redTracer;
            line.positionCount = 2;
            line.SetPosition(0, from);
            line.SetPosition(1, to);
            line.startWidth = 0.035f;
            line.endWidth = 0.012f;
            line.numCapVertices = 2;
            Destroy(object3D, 0.09f);
        }

        private void OnGUI()
        {
            if (Match == null || Player == null) return;
            var title = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
            title.normal.textColor = Color.white;
            var body = new GUIStyle(GUI.skin.label) { fontSize = 14 };
            body.normal.textColor = Color.white;
            GUI.Box(new Rect(12, 12, 370, 210), GUIContent.none);
            GUI.Label(new Rect(25, 20, 340, 30), "IRONFRONT  |  UNITY PROTOTYPE", title);
            GUI.Label(new Rect(25, 55, 340, 23),
                "BLUE " + Mathf.CeilToInt(Match.BlueTickets) + "     RED " + Mathf.CeilToInt(Match.RedTickets), body);
            GUI.Label(new Rect(25, 81, 340, 23),
                "HP " + Mathf.CeilToInt(Player.Health) + "     IF-27 " + Player.Ammo + " / " + Player.Reserve +
                (Player.Reloading ? "  RELOADING" : ""), body);
            for (int i = 0; i < Match.Points.Count; i++)
            {
                PrototypeCapturePoint p = Match.Points[i];
                string owner = p.Owner == PrototypeTeam.Blue ? "BLU" :
                    p.Owner == PrototypeTeam.Red ? "RED" : "---";
                GUI.Label(new Rect(25, 108 + i * 19, 340, 20),
                    p.Definition.Id + "  " + owner + "  " + Mathf.RoundToInt(p.Control) + "%" +
                    (p.Contested ? "  CONTESTED" : ""), body);
            }
            GUI.Label(new Rect(12, Screen.height - 35, 750, 25),
                "WASD move  |  Mouse aim  |  Left click fire  |  R reload  |  Shift sprint  |  Space jump  |  Esc cursor", body);
            if (Cursor.lockState == CursorLockMode.Locked && Player.Alive && !Match.Winner.HasValue)
            {
                float cx = Screen.width / 2f;
                float cy = Screen.height / 2f;
                GUI.Label(new Rect(cx - 5, cy - 10, 25, 25), "+", title);
            }
            if (!Player.Alive) GUI.Label(new Rect(Screen.width / 2f - 120, Screen.height / 2f - 65,
                300, 40), "DOWN  |  Respawning...", title);
            if (Match.Winner.HasValue) GUI.Label(new Rect(Screen.width / 2f - 145,
                Screen.height / 2f - 80, 350, 45), Match.Winner.Value + " TEAM WINS", title);
        }
    }
}
