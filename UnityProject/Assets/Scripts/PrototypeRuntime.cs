using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ironfront.UnityPrototype
{
    // A deliberately small playable slice. The browser game remains the parity reference.
    public sealed class PrototypeRuntime : MonoBehaviour
    {
        private readonly Dictionary<PrototypeCapturePoint, Renderer> markers =
            new Dictionary<PrototypeCapturePoint, Renderer>();
        private Material groundMaterial;
        private Material markerNeutral;
        private Material markerBlue;
        private Material markerRed;
        private Material blueTracer;
        private Material redTracer;
        private PrototypeTracerPool tracerPool;
        private PrototypeLanClient lan;
        private readonly PrototypeRoomMenu roomMenu = new PrototypeRoomMenu();
        private readonly Dictionary<int, float> lastRemoteInputAt = new Dictionary<int, float>();
        private bool roomMenuOpen;
        private float nextLanSend;
        private int snapshotTick;
        private int lastAppliedTick = -1;
        private bool networkModeActive;
        private bool networkHost;
        private string roomStatus = "";
        private const string SensitivityKey = "ironfront.unity.mouseSensitivity";
        private const string CrosshairSizeKey = "ironfront.unity.crosshairSize";

        public PrototypeMatch Match { get; private set; }
        public PrototypeNavigation Navigation { get; private set; }
        public PrototypePlayer Player { get; private set; }
        public PrototypeCommander BlueCommander { get; private set; }
        public PrototypeCommander RedCommander { get; private set; }
        public bool MatchStarted { get; private set; }
        public int SoloTeamSize { get; private set; }
        public bool PauseOpen { get; private set; }
        public bool SupportMenuOpen { get; private set; }
        public bool ScoreboardOpen { get; private set; }
        public PrototypeScoreboard Scoreboard { get; private set; }
        public bool IsNetworkSession => networkModeActive;
        public bool IsNetworkHost => networkModeActive && networkHost;
        public PrototypeScoreSystem Scores { get; private set; }
        public PrototypeSupportRequisition Support { get; private set; }
        public PrototypeAmmoSupplySystem Supplies { get; private set; }
        public PrototypeScorePopup ScorePopup { get; private set; }
        public PrototypePlayerAudio PlayerAudio { get; private set; }
        public PrototypeHitFeedback HitFeedback { get; private set; }
        public PrototypeDamageFeedback DamageFeedback { get; private set; }
        public float MouseSensitivity { get; private set; } = 2.2f;
        public float CrosshairSize { get; private set; } = 1f;
        public bool IsNetworkReplica => networkModeActive &&
            (!networkHost || lan == null || !lan.IsConnected ||
             (lan.LastRoom != null && lan.LastRoom.paused));
        public readonly List<PrototypeRemotePlayer> RemotePlayers =
            new List<PrototypeRemotePlayer>();
        public PrototypeFrontend Frontend { get; private set; }
        public PrototypeMusicDirector Music { get; private set; }
        public float MusicTicketLimit => networkModeActive && lan?.LastRoom?.settings != null ?
            Mathf.Clamp(lan.LastRoom.settings.tickets, 100, 800) : Match?.TicketLimit ?? 100f;
        public readonly List<PrototypeBot> Bots = new List<PrototypeBot>();
        public PrototypeBotDownFeed BotDownFeed { get; } = new PrototypeBotDownFeed();
        public readonly List<IPrototypeVehicle> Vehicles = new List<IPrototypeVehicle>();
        public Material BlueMaterial { get; private set; }
        public Material RedMaterial { get; private set; }
        public Material GunMaterial { get; private set; }
        public Material BlueClothMaterial { get; private set; }
        public Material RedClothMaterial { get; private set; }
        public Material BlueHelmetMaterial { get; private set; }
        public Material RedHelmetMaterial { get; private set; }
        public Material BlueTrimMaterial { get; private set; }
        public Material RedTrimMaterial { get; private set; }
        public Material ArmorMaterial { get; private set; }
        public Material SkinMaterial { get; private set; }
        public Material VisorMaterial { get; private set; }
        public Material BootMaterial { get; private set; }
        public Material GlassMaterial { get; private set; }
        public Material LampMaterial { get; private set; }
        public Material TracerMaterial(PrototypeTeam side) =>
            side == PrototypeTeam.Blue ? blueTracer : redTracer;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterBoot()
        {
            SceneManager.sceneLoaded -= Boot;
            SceneManager.sceneLoaded += Boot;
        }

        private static void Boot(Scene scene, LoadSceneMode mode)
        {
            if (GameObject.Find("IRONFRONT Unity Prototype") == null)
                new GameObject("IRONFRONT Unity Prototype").AddComponent<PrototypeRuntime>();
        }

        private void Awake()
        {
            MouseSensitivity = Mathf.Clamp(PlayerPrefs.GetFloat(SensitivityKey, 2.2f), .5f, 5f);
            LoadCrosshairSize();
            foreach (Camera existing in Camera.allCameras)
            {
                existing.enabled = false;
                AudioListener listener = existing.GetComponent<AudioListener>();
                if (listener != null) listener.enabled = false;
            }
            Match = new PrototypeMatch();
            Scores = new PrototypeScoreSystem(this);
            Scoreboard = new PrototypeScoreboard(this);
            Match.ScoreSystem = Scores;
            Navigation = new PrototypeNavigation();
            BlueCommander = new PrototypeCommander(PrototypeTeam.Blue);
            RedCommander = new PrototypeCommander(PrototypeTeam.Red);
            groundMaterial = MakeMaterial(new Color(0.35f, 0.43f, 0.31f));
            BlueMaterial = MakeMaterial(new Color(0.31f, 0.42f, 0.44f));
            RedMaterial = MakeMaterial(new Color(0.46f, 0.35f, 0.32f));
            GunMaterial = MakeMaterial(new Color(0.12f, 0.15f, 0.16f));
            BlueClothMaterial = MakeMaterial(new Color(0.25f, 0.31f, 0.35f));
            RedClothMaterial = MakeMaterial(new Color(0.36f, 0.31f, 0.29f));
            BlueHelmetMaterial = MakeMaterial(new Color(0.20f, 0.28f, 0.32f));
            RedHelmetMaterial = MakeMaterial(new Color(0.32f, 0.28f, 0.27f));
            BlueTrimMaterial = MakeMaterial(new Color(0.42f, 0.58f, 0.60f));
            RedTrimMaterial = MakeMaterial(new Color(0.61f, 0.43f, 0.38f));
            ArmorMaterial = MakeMaterial(new Color(0.22f, 0.27f, 0.28f));
            SkinMaterial = MakeMaterial(new Color(0.67f, 0.52f, 0.40f));
            VisorMaterial = MakeMaterial(new Color(0.08f, 0.15f, 0.18f));
            BootMaterial = MakeMaterial(new Color(0.10f, 0.12f, 0.13f));
            GlassMaterial = MakeMaterial(new Color(0.10f, 0.20f, 0.23f));
            LampMaterial = MakeMaterial(new Color(0.88f, 0.78f, 0.57f));
            markerNeutral = MakeMaterial(new Color(0.9f, 0.83f, 0.58f));
            markerBlue = MakeMaterial(new Color(0.28f, 0.72f, 0.95f));
            markerRed = MakeMaterial(new Color(0.95f, 0.4f, 0.33f));
            blueTracer = MakeMaterial(new Color(0.62f, 0.88f, 1f), true);
            redTracer = MakeMaterial(new Color(1f, 0.64f, 0.48f), true);

            tracerPool = gameObject.AddComponent<PrototypeTracerPool>();

            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.58f, 0.69f, 0.72f);
            RenderSettings.fogDensity = 0.0027f;
            BuildGround();
            BuildStructures();
            BuildWorldVisual();
            BuildTanks();
            BuildMarkers();
            BuildPlayer();
            BuildBots();
            BuildScouts();
            BuildTransports();
            BuildCombinedArmsVehicles();
            Support = new PrototypeSupportRequisition(this, Scores);
            Supplies = new PrototypeAmmoSupplySystem(this);
            ScorePopup = new PrototypeScorePopup(Scores, Player);
            PlayerAudio = Player.gameObject.AddComponent<PrototypePlayerAudio>();
            PlayerAudio.Initialize(this, Player);
            HitFeedback = gameObject.AddComponent<PrototypeHitFeedback>();
            HitFeedback.Initialize(Scores, Player);
            DamageFeedback = gameObject.AddComponent<PrototypeDamageFeedback>();
            DamageFeedback.Initialize(this, Player);
            BlueCommander.Tick(Time.time, Match);
            RedCommander.Tick(Time.time, Match);
            Music = gameObject.AddComponent<PrototypeMusicDirector>();
            Music.Initialize();
            Frontend = new PrototypeFrontend(this);
            Frontend.Open();
            lan = new PrototypeLanClient();
            Music.Tick(Match, Player.Team, MatchStarted, 0f);
        }

        private static Material MakeMaterial(Color color, bool unlit = false)
        {
            // Resources material assets keep the URP shaders in standalone builds.
            // Shader.Find alone works in the Editor but may be stripped by BuildPipeline.
            Material template = Resources.Load<Material>(unlit ?
                "Materials/PrototypeUnlit" : "Materials/PrototypeLit");
            Material material;
            if (template != null) material = new Material(template);
            else
            {
                Shader shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" :
                    "Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find(unlit ? "Unlit/Color" : "Standard");
                if (shader == null) throw new System.InvalidOperationException(
                    "Prototype URP material asset is missing and no fallback shader was found.");
                material = new Material(shader);
            }
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
                object3D.name = block.Kind + " collision";
                object3D.transform.SetParent(transform);
                object3D.transform.position = new Vector3(block.Position.x,
                    PrototypeLayout.HeightAt(block.Position.x, block.Position.y) + block.Height / 2f,
                    block.Position.y);
                object3D.transform.localScale = new Vector3(block.Width, block.Height, block.Depth);
                object3D.GetComponent<Renderer>().enabled = false;
            }
        }

        private void BuildWorldVisual()
        {
            var details = new GameObject("Browser-map architecture and roads");
            details.transform.SetParent(transform, false);
            details.AddComponent<PrototypeWorldVisual>().Initialize();
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
            // Preserve the existing eight-bot roster for LAN snapshots. Solo
            // adds its seven extra bots only when the player starts a match.
            BlueCommander.Register(
                AddBot(PrototypeTeam.Blue, -80f, -120f),
                AddBot(PrototypeTeam.Blue, -76f, -103f));
            BlueCommander.Register(
                AddBot(PrototypeTeam.Blue, -30f, 70f),
                AddBot(PrototypeTeam.Blue, -41f, 78f));
            RedCommander.Register(
                AddBot(PrototypeTeam.Red, 31f, -111f),
                AddBot(PrototypeTeam.Red, 44f, -115f));
            RedCommander.Register(
                AddBot(PrototypeTeam.Red, 66f, 115f),
                AddBot(PrototypeTeam.Red, 70f, 95f));
        }

        public void ConfigureSoloTeams(int playersPerTeam)
        {
            if (networkModeActive || MatchStarted)
                throw new System.InvalidOperationException("Solo teams must be set before deployment.");
            if (playersPerTeam != 8)
                throw new System.ArgumentOutOfRangeException(nameof(playersPerTeam),
                    "Only the tested 8v8 solo size is available.");
            if (SoloTeamSize == playersPerTeam) return;
            if (Bots.Count != 8)
                throw new System.InvalidOperationException("Unexpected initial AI roster.");

            BlueCommander.Register(
                AddBot(PrototypeTeam.Blue, -86f, -119f),
                AddBot(PrototypeTeam.Blue, -70f, -101f));
            BlueCommander.Register(AddBot(PrototypeTeam.Blue, -36f, 70f));
            RedCommander.Register(
                AddBot(PrototypeTeam.Red, 37f, -105f),
                AddBot(PrototypeTeam.Red, 48f, -103f));
            RedCommander.Register(
                AddBot(PrototypeTeam.Red, 64f, 104f),
                AddBot(PrototypeTeam.Red, 75f, 113f));
            SoloTeamSize = playersPerTeam;
            BlueCommander.Replan();
            RedCommander.Replan();
        }

        private PrototypeBot AddBot(PrototypeTeam team, float x, float z)
        {
            GameObject object3D = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            object3D.name = team + " Soldier";
            object3D.transform.SetParent(transform);
            PrototypeBot bot = object3D.AddComponent<PrototypeBot>();
            PrototypeInfantryClass role = (PrototypeInfantryClass)(Bots.Count % 4);
            Bots.Add(bot);
            bot.Initialize(this, Navigation, team, new Vector2(x, z), role);
            return bot;
        }

        private void BuildScouts()
        {
            // Place the first drivable scout beside the infantry start so the
            // vehicle loop can be exercised immediately in this short match.
            AddScout(PrototypeTeam.Blue, new Vector2(-60f, -111f), false);
            AddScout(PrototypeTeam.Red, new Vector2(55f, -95f), true);
        }

        private void AddScout(PrototypeTeam team, Vector2 position, bool aiControlled)
        {
            var object3D = new GameObject(team + " R4 SCOUT");
            object3D.transform.SetParent(transform);
            PrototypeScoutVehicle vehicle = object3D.AddComponent<PrototypeScoutVehicle>();
            Vehicles.Add(vehicle);
            vehicle.Initialize(this, team, position, aiControlled);
        }

        private void BuildTransports()
        {
            AddTransport(PrototypeTeam.Blue, new Vector2(-52f, -111f), false);
            AddTransport(PrototypeTeam.Red, new Vector2(67f, -95f), true);
        }

        private void BuildTanks()
        {
            AddTank(PrototypeTeam.Blue, PrototypeLayout.BlueTankSpawn, 0f);
            AddTank(PrototypeTeam.Red, PrototypeLayout.RedTankSpawn, 180f);
        }

        private void AddTank(PrototypeTeam team, Vector2 position, float yaw)
        {
            var object3D = new GameObject(team + " T90 BASTION");
            object3D.transform.SetParent(transform);
            PrototypeTankVehicle tank = object3D.AddComponent<PrototypeTankVehicle>();
            Vehicles.Add(tank);
            tank.Initialize(this, team, position, yaw);
        }

        private void AddTransport(PrototypeTeam team, Vector2 position, bool aiControlled)
        {
            var object3D = new GameObject(team + " U8 ROVER");
            object3D.transform.SetParent(transform);
            PrototypeTransportVehicle vehicle =
                object3D.AddComponent<PrototypeTransportVehicle>();
            Vehicles.Add(vehicle);
            vehicle.Initialize(this, team, position, aiControlled);
        }

        private void BuildCombinedArmsVehicles()
        {
            // Keep the existing six IDs stable for LAN snapshots and historical
            // deployment checks. New vehicles use main's separate base bays.
            foreach (PrototypeTeam team in new[] { PrototypeTeam.Blue, PrototypeTeam.Red })
            {
                float side = team == PrototypeTeam.Blue ? 1f : -1f;
                Vector2 origin = team == PrototypeTeam.Blue ?
                    new Vector2(-304f, -302f) : new Vector2(304f, 302f);
                AddCombinedArms(team, PrototypeVehicleKind.Ifv, origin);
                AddCombinedArms(team, PrototypeVehicleKind.AntiAir,
                    origin + Vector2.right * (side * 48f));
                AddCombinedArms(team, PrototypeVehicleKind.Helicopter,
                    origin + Vector2.right * (side * 60f));
                AddCombinedArms(team, PrototypeVehicleKind.Jet,
                    origin + Vector2.right * (side * 76f));
                AddCombinedArms(team, PrototypeVehicleKind.Motorcycle,
                    origin + Vector2.right * (side * 94f));
            }
            // Append the new aircraft after all 0.46 vehicles so every existing
            // vehicle ID remains stable in deployment and LAN snapshots.
            AddCombinedArms(PrototypeTeam.Blue, PrototypeVehicleKind.ReconHelicopter,
                new Vector2(-194f, -302f));
            AddCombinedArms(PrototypeTeam.Red, PrototypeVehicleKind.ReconHelicopter,
                new Vector2(194f, 302f));
        }

        private void AddCombinedArms(PrototypeTeam team, PrototypeVehicleKind kind,
            Vector2 position)
        {
            var object3D = new GameObject(team + " " + kind);
            object3D.transform.SetParent(transform);
            PrototypeCombinedArmsVehicle vehicle =
                object3D.AddComponent<PrototypeCombinedArmsVehicle>();
            Vehicles.Add(vehicle);
            // Eight infantry per side cannot crew five additional combat
            // vehicles at once. They remain available for player deployment.
            vehicle.Initialize(this, team, kind, position, false);
        }

        private void Update()
        {
            BotDownFeed.Expire(Time.time);
            Music?.Tick(Match, Player == null ? PrototypeTeam.Blue : Player.Team,
                MatchStarted, Time.unscaledDeltaTime, MusicTicketLimit);
            if (Match == null || Player == null) return;
            lan?.Poll();
            if (SupportMenuOpen && (!Player.Alive || Match.Winner.HasValue ||
                Player.CurrentVehicle != null || Frontend.IsOpen)) SetSupportMenu(false);
            if (!MatchStarted && ScoreboardOpen) SetScoreboard(false);
            if (MatchStarted && Input.GetKeyDown(KeyCode.Escape))
            {
                if (ScoreboardOpen) SetScoreboard(false);
                else if (SupportMenuOpen) SetSupportMenu(false);
                else SetPause(!PauseOpen);
            }
            else if (MatchStarted && !PauseOpen && Input.GetKeyDown(KeyCode.Y))
                SetScoreboard(!ScoreboardOpen);
            else if (MatchStarted && !PauseOpen && Input.GetKeyDown(KeyCode.B))
                SetSupportMenu(!SupportMenuOpen);
            if (PauseOpen && Input.GetKeyDown(KeyCode.L))
            {
                ReturnToLobby();
                return;
            }
            if (PauseOpen && !networkModeActive) return;
            if (networkModeActive && lan != null && lan.IsConnected)
            {
                if (lan.IsHost) networkHost = true;
                PrototypeLanRosterMember[] members = lan.LastRoom?.roster;
                if (members != null)
                    foreach (PrototypeLanRosterMember member in members)
                        if (member.id == lan.LocalId)
                        {
                            Player.SetTeam(member.team == "red" ? PrototypeTeam.Red :
                                PrototypeTeam.Blue);
                            break;
                        }
                SyncRemoteRoster();
                if (!MatchStarted && lan.Phase == "battle")
                {
                    if (networkHost && lan.LastRoom?.settings != null)
                    {
                        int tickets = lan.LastRoom.settings.tickets;
                        Match.SetNetworkTickets(tickets, tickets);
                    }
                    Player.SelectClass(PrototypeInfantryClass.Assault);
                    Frontend.Close();
                    roomMenuOpen = false;
                    BeginBattle();
                }
            }
            if (!MatchStarted) return;
            Support?.Tick(Time.deltaTime);
            Supplies?.Tick(Time.deltaTime);
            if (networkModeActive && IsNetworkReplica)
            {
                if (lan != null && lan.IsConnected)
                {
                    ApplyLatestSnapshot();
                    SendLocalInput();
                }
                UpdateMarkerMaterials();
                return;
            }
            if (networkModeActive) ProcessPeerInputs();
            int previousSector = Match.SectorIndex;
            Match.Tick(Mathf.Min(Time.deltaTime, 0.05f), Player, Bots,
                networkModeActive ? RemotePlayers : null);
            if (Match.SectorIndex != previousSector)
            {
                BlueCommander.Replan();
                RedCommander.Replan();
            }
            BlueCommander.Tick(Time.time, Match);
            RedCommander.Tick(Time.time, Match);
            UpdateMarkerMaterials();
            if (networkModeActive && lan != null && lan.IsConnected &&
                Time.unscaledTime >= nextLanSend)
            {
                nextLanSend = Time.unscaledTime + 0.05f;
                lan.SendSnapshot(CaptureSnapshot());
            }
        }

        private void UpdateMarkerMaterials()
        {
            foreach (KeyValuePair<PrototypeCapturePoint, Renderer> entry in markers)
            {
                PrototypeCapturePoint point = entry.Key;
                entry.Value.sharedMaterial = point.Owner == PrototypeTeam.Blue ? markerBlue :
                    point.Owner == PrototypeTeam.Red ? markerRed : markerNeutral;
            }
        }

        public void BeginBattle()
        {
            if (MatchStarted || Match == null || Player == null) return;
            Support?.Reset();
            Scores?.Reset();
            Scoreboard?.Reset();
            ScoreboardOpen = false;
            Supplies?.Reset();
            ScorePopup?.Reset();
            PlayerAudio?.Reset();
            HitFeedback?.Reset();
            DamageFeedback?.Clear();
            MatchStarted = true;
            // One existing hostile aircraft gives solo engineers a real SAM
            // target. Keep the lobby parked and the LAN fleet unchanged.
            if (!networkModeActive)
                foreach (IPrototypeVehicle vehicle in Vehicles)
                    if (vehicle is PrototypeCombinedArmsVehicle aircraft &&
                        aircraft.Team == PrototypeTeam.Red && aircraft.Kind == PrototypeVehicleKind.Helicopter)
                    { aircraft.SetAiControlled(true); break; }
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public void SetMouseSensitivity(float value)
        {
            MouseSensitivity = Mathf.Clamp(value, .5f, 5f);
            PlayerPrefs.SetFloat(SensitivityKey, MouseSensitivity);
            PlayerPrefs.Save();
        }

        public static float ClampCrosshairSize(float value) =>
            float.IsNaN(value) || float.IsInfinity(value) ? 1f : Mathf.Clamp(value, .5f, 2f);

        private void LoadCrosshairSize() =>
            CrosshairSize = ClampCrosshairSize(PlayerPrefs.GetFloat(CrosshairSizeKey, 1f));

        public void SetCrosshairSize(float value)
        {
            CrosshairSize = ClampCrosshairSize(value);
            PlayerPrefs.SetFloat(CrosshairSizeKey, CrosshairSize);
            PlayerPrefs.Save();
        }

        private void SetPause(bool open)
        {
            if (open && ScoreboardOpen) SetScoreboard(false);
            if (open && SupportMenuOpen) SetSupportMenu(false);
            PauseOpen = open;
            if (!networkModeActive) Time.timeScale = open ? 0f : 1f;
            Cursor.lockState = open || ScoreboardOpen || !Player.Alive || Frontend.IsOpen ||
                Match.Winner.HasValue ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = Cursor.lockState != CursorLockMode.Locked;
        }

        public bool SetSupportMenu(bool open)
        {
            if (open && (!MatchStarted || PauseOpen || !Player.Alive ||
                Match.Winner.HasValue || Player.CurrentVehicle != null || Frontend.IsOpen)) return false;
            if (open && ScoreboardOpen) SetScoreboard(false);
            SupportMenuOpen = open;
            if (open) Player.ClearHeldEquipment();
            Cursor.lockState = open || PauseOpen || !Player.Alive || Frontend.IsOpen ||
                Match.Winner.HasValue ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = Cursor.lockState != CursorLockMode.Locked;
            return true;
        }

        public bool SetScoreboard(bool open)
        {
            if (open && (!MatchStarted || PauseOpen || Scoreboard == null)) return false;
            if (open && SupportMenuOpen) SetSupportMenu(false);
            ScoreboardOpen = open;
            // Viewing standings never pauses a battle, including solo mode.
            Cursor.lockState = open || PauseOpen || !Player.Alive || Frontend.IsOpen ||
                Match.Winner.HasValue ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = Cursor.lockState != CursorLockMode.Locked;
            return true;
        }

        private void ReturnToLobby()
        {
            SetScoreboard(false);
            SetPause(false);
            Time.timeScale = 1f;
            lan?.Close();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void DrawRoomMenu(float canvasWidth, float canvasHeight)
        {
            PrototypeLanRoom room = lan?.LastRoom;
            PrototypeLanRosterMember[] members = room?.roster;
            int count = members == null ? 0 : members.Length;
            var lines = new string[count];
            var ids = new int[count];
            var blue = new bool[count];
            bool localReady = false;
            for (int i = 0; i < count; i++)
            {
                PrototypeLanRosterMember member = members[i];
                ids[i] = member.id;
                blue[i] = member.team == "blue";
                lines[i] = (member.host || member.isHost ? "HOST" : "PLAYER") +
                    "  " + (string.IsNullOrEmpty(member.name) ? "#" + member.id : member.name) +
                    "  " + (member.team ?? "blue").ToUpperInvariant() +
                    (member.ready ? "  READY" : "  WAITING") +
                    (member.connected ? "" : "  DISCONNECTED");
                if (member.id == lan.LocalId)
                {
                    localReady = member.ready;
                    Player.SetTeam(member.team == "red" ? PrototypeTeam.Red : PrototypeTeam.Blue);
                }
            }
            string notice = lan != null && !string.IsNullOrEmpty(lan.Error) ? lan.Error : roomStatus;
            if (room != null && room.paused) notice = "Host disconnected. Room paused for host reconnect.";
            PrototypeRoomAction action = roomMenu.Draw(canvasWidth, canvasHeight,
                lan != null && lan.IsConnected, lan != null && lan.IsHost,
                localReady, lan?.Phase ?? "preparation", lan?.Code ?? "", notice,
                lan?.LocalId ?? -1, lines, ids, blue, room?.settings?.tickets ?? 100);
            switch (action.Kind)
            {
                case PrototypeRoomActionKind.Create:
                    roomStatus = "Creating room...";
                    CreateLanRoom();
                    break;
                case PrototypeRoomActionKind.Join:
                    roomStatus = "Joining room...";
                    JoinLanRoom();
                    break;
                case PrototypeRoomActionKind.Leave:
                    lan?.Close();
                    networkModeActive = false;
                    networkHost = false;
                    roomMenuOpen = false;
                    roomStatus = "";
                    Frontend.Open();
                    break;
                case PrototypeRoomActionKind.Ready: lan?.SendReady(true); break;
                case PrototypeRoomActionKind.Unready: lan?.SendReady(false); break;
                case PrototypeRoomActionKind.Start: lan?.SendStart(); break;
                case PrototypeRoomActionKind.Blue: lan?.SendTeam("blue"); break;
                case PrototypeRoomActionKind.Red: lan?.SendTeam("red"); break;
                case PrototypeRoomActionKind.Kick: lan?.SendKick(action.TargetId); break;
                case PrototypeRoomActionKind.MoveBlue:
                    lan?.SendTeam("blue", action.TargetId); break;
                case PrototypeRoomActionKind.MoveRed:
                    lan?.SendTeam("red", action.TargetId); break;
                case PrototypeRoomActionKind.SetTickets:
                    lan?.SendSettings(action.TargetId, 8); break;
            }
        }

        private async void CreateLanRoom()
        {
            try
            {
                await lan.CreateRoom(roomMenu.Address, Frontend.PlayerName);
                networkModeActive = true;
                roomStatus = "Share room code " + lan.Code + " with another Unity player.";
            }
            catch (System.Exception exception) { roomStatus = exception.Message; }
        }

        private async void JoinLanRoom()
        {
            try
            {
                await lan.JoinRoom(roomMenu.Address, roomMenu.JoinCode, Frontend.PlayerName);
                networkModeActive = true;
                roomStatus = "Connected. Choose your team and press READY.";
            }
            catch (System.Exception exception) { roomStatus = exception.Message; }
        }

        private void SyncRemoteRoster()
        {
            PrototypeLanRosterMember[] roster = lan.LastRoom?.roster;
            if (roster == null) return;
            for (int i = RemotePlayers.Count - 1; i >= 0; i--)
            {
                PrototypeRemotePlayer remote = RemotePlayers[i];
                bool keep = false;
                foreach (PrototypeLanRosterMember member in roster)
                    if (member.id == remote.Id &&
                        member.team == (remote.Team == PrototypeTeam.Blue ? "blue" : "red"))
                    {
                        keep = true;
                        if (remote.IsConnected != member.connected)
                        {
                            remote.SetConnected(member.connected);
                            lastRemoteInputAt.Remove(remote.Id);
                        }
                        break;
                    }
                if (keep) continue;
                RemotePlayers.RemoveAt(i);
                lastRemoteInputAt.Remove(remote.Id);
                Destroy(remote.gameObject);
            }
            foreach (PrototypeLanRosterMember member in roster)
            {
                if (!member.connected || member.id == lan.LocalId ||
                    (member.team != "blue" && member.team != "red")) continue;
                bool exists = false;
                foreach (PrototypeRemotePlayer remote in RemotePlayers)
                    if (remote.Id == member.id) { exists = true; break; }
                if (exists) continue;
                var actor = new GameObject("Remote Player " + member.id);
                actor.transform.SetParent(transform);
                PrototypeRemotePlayer component = actor.AddComponent<PrototypeRemotePlayer>();
                component.Initialize(this, member.id,
                    member.team == "red" ? PrototypeTeam.Red : PrototypeTeam.Blue);
                RemotePlayers.Add(component);
            }
        }

        private void ProcessPeerInputs()
        {
            while (lan.PeerInputs.Count > 0)
            {
                PrototypeLanPeerInput peer = lan.PeerInputs.Dequeue();
                PrototypeLanInput input = peer.input;
                if (input == null) continue;
                if (lastRemoteInputAt.TryGetValue(peer.id, out float previous) &&
                    Time.unscaledTime - previous < 0.045f) continue;
                foreach (PrototypeRemotePlayer remote in RemotePlayers)
                    if (remote.Id == peer.id && remote.IsConnected)
                    {
                        float dt = lastRemoteInputAt.TryGetValue(peer.id, out float prior) ?
                            Mathf.Clamp(Time.unscaledTime - prior, 0f, 0.1f) : 0.05f;
                        lastRemoteInputAt[peer.id] = Time.unscaledTime;
                        if (remote.IsDowned)
                        {
                            if (input.callRescue) remote.RequestRescue();
                            if (input.giveUp) remote.GiveUp(dt);
                        }
                        remote.ApplyInput(input.forward, input.side, input.yaw,
                            input.pitch, input.sprint, input.fire, dt);
                        break;
                    }
            }
            while (lan.PeerDeployments.Count > 0)
            {
                PrototypeLanPeerDeploy request = lan.PeerDeployments.Dequeue();
                if (string.IsNullOrEmpty(request.location)) continue;
                foreach (PrototypeRemotePlayer remote in RemotePlayers)
                    if (remote.Id == request.id && remote.IsConnected)
                    {
                        remote.TryDeploy(request.location);
                        break;
                    }
            }
            while (lan.PeerDownedActions.Count > 0)
            {
                PrototypeLanPeerDowned request = lan.PeerDownedActions.Dequeue();
                foreach (PrototypeRemotePlayer remote in RemotePlayers)
                    if (remote.Id == request.id && remote.IsConnected)
                    {
                        if (request.action == "rescue") remote.RequestRescue();
                        else if (request.action == "giveUp") remote.GiveUpImmediately();
                        break;
                    }
            }
        }

        private void SendLocalInput()
        {
            if (Time.unscaledTime < nextLanSend) return;
            nextLanSend = Time.unscaledTime + 0.05f;
            bool active = Cursor.lockState == CursorLockMode.Locked && Player.Alive && !ScoreboardOpen;
            float pitch = Player.ViewCamera.transform.eulerAngles.x;
            if (pitch > 180f) pitch -= 360f;
            lan.SendInput(new PrototypeLanInput {
                forward = active ? Input.GetAxisRaw("Vertical") : 0f,
                side = active ? Input.GetAxisRaw("Horizontal") : 0f,
                yaw = Player.transform.eulerAngles.y,
                pitch = pitch,
                sprint = active && Input.GetKey(KeyCode.LeftShift) && Player.SprintRequested &&
                    !Input.GetMouseButton(0) && !Input.GetMouseButton(1),
                fire = active && Player.HeldEquipment == PrototypeHeldEquipment.Firearm &&
                    Input.GetMouseButton(0),
                callRescue = !ScoreboardOpen && !PauseOpen && !SupportMenuOpen &&
                    Player.IsDowned && Input.GetKey(KeyCode.H),
                giveUp = !ScoreboardOpen && !PauseOpen && !SupportMenuOpen &&
                    Player.IsDowned && Input.GetKey(KeyCode.Space)
            });
        }

        private PrototypeLanSnapshot CaptureSnapshot()
        {
            var players = new PrototypeLanActor[RemotePlayers.Count + 1];
            players[0] = CaptureActor(lan.LocalId, Player.transform,
                Player.Health, Player.Alive, Player.Team);
            players[0].ammo = Player.Ammo;
            players[0].reserve = Player.Reserve;
            players[0].respawnRemaining = Player.RespawnRemaining;
            players[0].downedRemaining = Player.DownedRemaining;
            players[0].rescueCalled = Player.RescueCalled;
            players[0].revivePaused = Player.IsBeingRevived;
            players[0].reviveProgress = Player.ReviveProgress;
            players[0].incomingDamage = Player.LastIncomingDamage?.Snapshot();
            for (int i = 0; i < RemotePlayers.Count; i++)
            {
                PrototypeRemotePlayer remote = RemotePlayers[i];
                players[i + 1] = CaptureActor(remote.Id, remote.transform,
                    remote.Health, remote.Alive, remote.Team);
                players[i + 1].ammo = remote.Ammo;
                players[i + 1].reserve = remote.Reserve;
                players[i + 1].respawnRemaining = remote.RespawnRemaining;
                players[i + 1].downedRemaining = remote.DownedRemaining;
                players[i + 1].rescueCalled = remote.RescueCalled;
                players[i + 1].revivePaused = remote.IsBeingRevived;
                players[i + 1].reviveProgress = remote.ReviveProgress;
                players[i + 1].incomingDamage = remote.LastIncomingDamage?.Snapshot();
            }
            var bots = new PrototypeLanActor[Bots.Count];
            for (int i = 0; i < Bots.Count; i++)
                bots[i] = CaptureActor(i, Bots[i].transform, Bots[i].Health,
                    Bots[i].Alive, Bots[i].Team);
            var vehicles = new PrototypeLanActor[Vehicles.Count];
            for (int i = 0; i < Vehicles.Count; i++)
                vehicles[i] = CaptureActor(i, Vehicles[i].VehicleTransform,
                    Vehicles[i].Health, Vehicles[i].Alive, Vehicles[i].Team);
            var points = new PrototypeLanPoint[Match.Points.Count];
            for (int i = 0; i < Match.Points.Count; i++)
            {
                PrototypeCapturePoint point = Match.Points[i];
                points[i] = new PrototypeLanPoint { id = point.Definition.Id,
                    control = point.Control,
                    owner = point.Owner == PrototypeTeam.Blue ? "blue" :
                        point.Owner == PrototypeTeam.Red ? "red" : "" };
            }
            return new PrototypeLanSnapshot { tick = ++snapshotTick, players = players,
                bots = bots, vehicles = vehicles, points = points,
                blueTickets = Match.BlueTickets, redTickets = Match.RedTickets,
                scoreboard = Scoreboard?.BuildSnapshot(lan.LastRoom?.roster, lan.LocalId) };
        }

        private static PrototypeLanActor CaptureActor(int id, Transform actor,
            float health, bool alive, PrototypeTeam team)
        {
            Vector3 position = actor.position;
            return new PrototypeLanActor { id = id, x = position.x, y = position.y,
                z = position.z, yaw = actor.eulerAngles.y, hp = health, alive = alive,
                team = team == PrototypeTeam.Blue ? "blue" : "red" };
        }

        private void ApplyLatestSnapshot()
        {
            PrototypeLanSnapshot state = lan.LastSnapshot;
            if (state == null || state.tick <= lastAppliedTick) return;
            lastAppliedTick = state.tick;
            Match.SetNetworkTickets(state.blueTickets, state.redTickets);
            Scoreboard?.ApplyReadOnlySnapshot(state.scoreboard);
            if (state.points != null)
                foreach (PrototypeLanPoint point in state.points)
                    Match.SetNetworkPoint(point.id, point.control,
                        point.owner == "blue" ? 0 : point.owner == "red" ? 1 : -1);
            if (state.players != null)
                foreach (PrototypeLanActor actor in state.players)
                {
                    Vector3 position = new Vector3(actor.x, actor.y, actor.z);
                    if (actor.id == lan.LocalId)
                    {
                        float previousHealth = Player.Health;
                        Player.ApplyNetworkState(position, actor.yaw, actor.hp, actor.alive,
                            actor.ammo, actor.reserve, actor.respawnRemaining,
                            actor.downedRemaining, actor.rescueCalled,
                            actor.revivePaused, actor.reviveProgress);
                        DamageFeedback?.ReceiveSnapshot(actor.incomingDamage, previousHealth, Player.Health);
                    }
                    else
                        foreach (PrototypeRemotePlayer remote in RemotePlayers)
                            if (remote.Id == actor.id)
                            {
                                remote.SetNetworkState(position, actor.yaw, actor.hp,
                                    actor.alive, actor.downedRemaining > 0f);
                                break;
                            }
                }
            if (state.bots != null)
                foreach (PrototypeLanActor actor in state.bots)
                    if (actor.id >= 0 && actor.id < Bots.Count)
                        Bots[actor.id].ApplyNetworkState(new Vector3(actor.x, actor.y, actor.z),
                            actor.yaw, actor.hp, actor.alive);
            if (state.vehicles != null)
                foreach (PrototypeLanActor actor in state.vehicles)
                    if (actor.id >= 0 && actor.id < Vehicles.Count)
                    {
                        Vector3 position = new Vector3(actor.x, actor.y, actor.z);
                        if (Vehicles[actor.id] is PrototypeScoutVehicle scout)
                            scout.ApplyNetworkState(position, actor.yaw, actor.hp, actor.alive);
                        else if (Vehicles[actor.id] is PrototypeTransportVehicle transport)
                            transport.ApplyNetworkState(position, actor.yaw, actor.hp, actor.alive);
                        else if (Vehicles[actor.id] is PrototypeTankVehicle tank)
                            tank.ApplyNetworkState(position, actor.yaw, actor.hp, actor.alive);
                        else if (Vehicles[actor.id] is PrototypeCombinedArmsVehicle combined)
                            combined.ApplyNetworkState(position, actor.yaw, actor.hp, actor.alive);
                    }
            if (Player.Alive && Frontend.IsOpen) Frontend.Close();
            else if (Player.IsDowned && !Frontend.IsDownedScreen &&
                !Match.Winner.HasValue) Frontend.OpenDowned();
            else if (!Player.Alive && !Player.IsDowned &&
                (!Frontend.IsOpen || Frontend.IsDownedScreen) &&
                !Match.Winner.HasValue) Frontend.Open(true);
        }

        private void OnDestroy()
        {
            if (PauseOpen) Time.timeScale = 1f;
            Frontend?.Dispose();
            lan?.Close();
            Music?.Dispose();
            Support?.Dispose();
            Supplies?.Dispose();
            ScorePopup?.Dispose();
            HitFeedback?.Dispose();
            DamageFeedback?.Dispose();
            PlayerAudio?.Dispose();
        }

        public void ShowTracer(Vector3 from, Vector3 to, PrototypeTeam team)
        {
            tracerPool.Show(from, to, TracerMaterial(team));
        }

        private void OnGUI()
        {
            if (Match == null || Player == null) return;
            // Keep the same 960x600 logical layout on small Game views, and
            // enlarge it on desktop displays so the menu and HUD remain legible.
            float scale = Mathf.Min(2f, Screen.width / 960f, Screen.height / 600f);
            float canvasWidth = Screen.width / scale;
            float canvasHeight = Screen.height / scale;
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            if (!MatchStarted)
            {
                if (roomMenuOpen)
                    DrawRoomMenu(canvasWidth, canvasHeight);
                else if (Frontend != null && Frontend.Draw(canvasWidth, canvasHeight))
                {
                    ConfigureSoloTeams(Frontend.SelectedTeamSize);
                    Match.ConfigureSolo(Frontend.SelectedMode);
                    BlueCommander.Replan();
                    RedCommander.Replan();
                    UpdateMarkerMaterials();
                    Player.ApplyLoadout(Frontend.SelectedLoadout);
                    BeginBattle();
                }
                else if (Frontend != null && Frontend.LanRequested)
                    roomMenuOpen = true;
                GUI.matrix = previousMatrix;
                return;
            }
            if (PauseOpen)
            {
                PrototypePauseAction action = Frontend.DrawPause(canvasWidth, canvasHeight,
                    networkModeActive, MouseSensitivity);
                if (action == PrototypePauseAction.Resume) SetPause(false);
                else if (action == PrototypePauseAction.MainMenu) ReturnToLobby();
                DrawBotDownFeed(7f, 1, false);
                GUI.matrix = previousMatrix;
                return;
            }
            if (ScoreboardOpen)
            {
                Scoreboard.Draw(canvasWidth, canvasHeight, lan?.LastRoom?.roster, lan?.LocalId ?? -1);
                GUI.matrix = previousMatrix;
                return;
            }
            if (Match.Winner.HasValue && !networkModeActive)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                if (DrawSoloResult(canvasWidth, canvasHeight)) ReturnToLobby();
                DrawBotDownFeed(7f, 1, false);
                GUI.matrix = previousMatrix;
                return;
            }
            if (networkModeActive && lan != null && !lan.IsConnected)
            {
                string connectionNotice = lan.ReconnectFailed ?
                    "Connection lost. Session could not be restored." :
                    "Reconnecting to room...";
                float noticeX = canvasWidth / 2f - 190f;
                GUI.Box(new Rect(noticeX, 12f, 380f, 35f), GUIContent.none);
                GUI.Label(new Rect(noticeX + 12f, 19f, 356f, 22f), connectionNotice);
            }
            if (Frontend != null && Frontend.IsDownedScreen && Player.IsDowned &&
                !Match.Winner.HasValue)
            {
                PrototypeDownedAction action = Frontend.DrawDowned(canvasWidth,
                    canvasHeight, Player.DownedRemaining, Player.RescueCalled,
                    Player.ReviveProgress, Player.IsBeingRevived);
                if (action == PrototypeDownedAction.Rescue)
                {
                    if (IsNetworkReplica) lan?.SendDownedAction("rescue");
                    else Player.RequestRescue();
                }
                else if (action == PrototypeDownedAction.GiveUp)
                {
                    if (IsNetworkReplica) lan?.SendDownedAction("giveUp");
                    else Player.GiveUpImmediately();
                }
                DrawBotDownFeed(12f);
                ScorePopup?.Draw(canvasWidth, canvasHeight);
                GUI.matrix = previousMatrix;
                return;
            }
            if (Frontend != null && Frontend.IsOpen && !Player.Alive &&
                !Match.Winner.HasValue)
            {
                string location = Frontend.DrawDeployment(canvasWidth, canvasHeight,
                    this, !IsNetworkReplica, Player.RespawnRemaining);
                if (location != null)
                {
                    if (IsNetworkReplica)
                        lan?.SendDeploy(location);
                    else if (Player.TryDeploy(location, Frontend.SelectedLoadout))
                        Frontend.Close();
                }
                DrawBotDownFeed(7f, 1, false);
                GUI.matrix = previousMatrix;
                return;
            }
            if (SupportMenuOpen)
            {
                DrawSupportPanel(canvasWidth, canvasHeight);
                GUI.matrix = previousMatrix;
                return;
            }
            DrawTicketBanner(canvasWidth);
            DrawBotDownFeed(networkModeActive ? 222f : 199f);
            DrawBotNames(canvasWidth, canvasHeight);
            var title = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
            title.normal.textColor = Color.white;
            var body = new GUIStyle(GUI.skin.label) { fontSize = 12 };
            body.normal.textColor = Color.white;
            bool tankSight = Player.IsFirstPerson && Player.CurrentVehicle is PrototypeTankVehicle opticTank &&
                opticTank.PlayerSeat == 0;
            if (!tankSight)
            {
            DrawHudRect(new Rect(12, 12, 270, networkModeActive ? 202 : 179),
                new Color(.012f, .045f, .060f, .82f));
            DrawHudRect(new Rect(12, 12, 270, 2), new Color(.46f, .76f, .82f));
            GUI.Label(new Rect(25, 20, 244, 30), "IRONFRONT  |  " + Frontend.PlayerName, title);
            if (networkModeActive)
                GUI.Label(new Rect(25, 178, 244, 23),
                    "ROOM " + (lan?.Code ?? "------") + "  |  " +
                    (networkHost ? "HOST" : "PLAYER") + "  |  " +
                    (lan != null && lan.IsConnected ?
                        lan.LastRoom != null && lan.LastRoom.paused ? "PAUSED" : "CONNECTED" :
                        lan != null && lan.ReconnectFailed ? "SESSION ENDED" : "RECONNECTING"), body);
            if (Player.CurrentVehicle != null)
                GUI.Label(new Rect(25, 51, 244, 31),
                    Player.CurrentVehicle.VehicleName + "  HP " +
                    Mathf.CeilToInt(Player.CurrentVehicle.Health) +
                    "\nARMOR " + PrototypeVehicleArmor.Rating(Player.CurrentVehicle) +
                    "  |  " + Mathf.RoundToInt(Mathf.Abs(Player.CurrentVehicle.Speed) * 3.6f) +
                    " km/h", body);
            else
                GUI.Label(new Rect(25, 51, 244, 23),
                    Player.IsParachuting ? "AUTO PARACHUTE · 5 m/s" :
                    Player.IsSliding ? "SLIDING" : Player.IsProne ? "PRONE" :
                    Player.IsCrouched ? "CROUCHED" : Player.IsSprinting ? "SPRINTING" :
                    Player.IsAiming ? "AIMING" : "READY", body);
            for (int i = 0; i < Match.Points.Count; i++)
            {
                PrototypeCapturePoint p = Match.Points[i];
                string owner = p.Owner == PrototypeTeam.Blue ? "BLU" :
                    p.Owner == PrototypeTeam.Red ? "RED" : "---";
                GUI.Label(new Rect(25, 78 + i * 19, 244, 20),
                    p.Definition.Id + "  " + owner + "  " + Mathf.RoundToInt(p.Control) + "%" +
                    (p.Contested ? "  CONTESTED" : ""), body);
            }
            float ordersX = canvasWidth - 282f;
            float ordersY = 12f;
            float blueOrdersY = ordersY + 34f;
            float redOrdersY = blueOrdersY + BlueCommander.Squads.Count * 19f + 8f;
            float summaryY = redOrdersY + RedCommander.Squads.Count * 19f + 7f;
            DrawHudRect(new Rect(ordersX, ordersY, 270, summaryY - ordersY + 43f),
                new Color(.012f, .045f, .060f, .82f));
            DrawHudRect(new Rect(ordersX, ordersY, 270, 2), new Color(.46f, .76f, .82f));
            GUI.Label(new Rect(ordersX + 12, ordersY + 6, 245, 25), "SQUAD ORDERS", title);
            DrawOrders(BlueCommander, "BLUE", ordersX + 12, blueOrdersY, body);
            DrawOrders(RedCommander, "RED", ordersX + 12, redOrdersY, body);
            int blueCover = 0, redCover = 0, blueSearch = 0, redSearch = 0;
            foreach (PrototypeBot bot in Bots)
            {
                bool covering = bot.TacticalState == PrototypeTacticalState.SeekCover ||
                    bot.TacticalState == PrototypeTacticalState.InCover;
                if (bot.Team == PrototypeTeam.Blue)
                {
                    if (covering) blueCover++;
                    if (bot.TacticalState == PrototypeTacticalState.Search) blueSearch++;
                }
                else
                {
                    if (covering) redCover++;
                    if (bot.TacticalState == PrototypeTacticalState.Search) redSearch++;
                }
            }
            GUI.Label(new Rect(ordersX + 12, summaryY, 245, 20),
                "BLUE  COVER " + blueCover + "  SEARCH " + blueSearch, body);
            GUI.Label(new Rect(ordersX + 12, summaryY + 19f, 245, 20),
                "RED   COVER " + redCover + "  SEARCH " + redSearch, body);
            }
            string controls = Player.CurrentVehicle != null ?
                VehicleControls(Player.CurrentVehicle) :
                Player.Class == PrototypeInfantryClass.Engineer ?
                "WASD | 1/2 gun | 3 grenade | 4 AT | 5 AA | P fire | E supply | B support" :
                Player.Class == PrototypeInfantryClass.Assault ?
                "WASD | 1/2 gun | 3 grenade | X ammo | 6 box | E supply | B support" :
                "WASD | 1/2 gun | 3 grenade | X gadget | E supply | B support";
            controls = "V view " + (Player.IsFirstPerson ? "1P" : "3P") +
                "  |  " + controls + " | Y scores";
            GUI.Label(new Rect(12, canvasHeight - 35, Mathf.Max(250f,
                canvasWidth - (Player.Class == PrototypeInfantryClass.Engineer ? 395f : 285f)), 25), controls, body);
            if (Player.CurrentVehicle is IPrototypeSeatedVehicle currentSeats)
                DrawVehicleSeats(currentSeats, canvasHeight, body);
            if (!tankSight && Player.CurrentVehicle is PrototypeTankVehicle tank)
                GUI.Label(new Rect(canvasWidth / 2f - 76f, canvasHeight / 2f + 42f,
                    152f, 23f), tank.PlayerSeat >= 2 ? "PASSENGER" :
                    tank.PlayerSeat == 1 ? "CREW MG" : tank.CannonReadyIn > 0f ?
                    "RELOAD " + tank.CannonReadyIn.ToString("0.0") + " s" :
                    "CANNON READY", body);
            if (Player.CurrentVehicle is PrototypeCombinedArmsVehicle activeVehicle)
            {
                activeVehicle.ReconRadar?.DrawHud(Player.ViewCamera, canvasWidth, canvasHeight);
                string countermeasure = activeVehicle.IsAircraft ? "FLARES" : "SMOKE";
                string defenseStatus = activeVehicle.Kind == PrototypeVehicleKind.Motorcycle ? "" :
                    "\n" + countermeasure + (activeVehicle.CountermeasureActive ?
                    activeVehicle.IsAircraft ? " LOCK IMMUNE " + activeVehicle.LockImmunityRemaining.ToString("0.0") + " s" : " ACTIVE" :
                    activeVehicle.CountermeasureReadyIn > 0f ? " " +
                    activeVehicle.CountermeasureReadyIn.ToString("0") + " s" : " READY") +
                    (activeVehicle.PlayerSeat == 0 ? " · X" : " · F1 TO USE");
                GUI.Label(new Rect(canvasWidth / 2f - 155f, canvasHeight / 2f + 42f,
                    310f, 85f), activeVehicle.WeaponStatus +
                    (activeVehicle.IsAircraft ? "  |  ALT " +
                        Mathf.RoundToInt(activeVehicle.Altitude) + " m" : "") + defenseStatus +
                    (activeVehicle.IncomingUntil > Time.time ? "\nMISSILE INCOMING" :
                     activeVehicle.WarningUntil > Time.time ? "\nTARGET LOCK WARNING" : ""), body);
            }
            if (Player.CurrentVehicle == null && Player.Alive)
                DrawCombatHud(canvasWidth, canvasHeight);
            PrototypePlayerHealthHud.Draw(Player, canvasWidth, canvasHeight);
            PrototypeTankSightHud.Draw(this, canvasWidth, canvasHeight);
            DrawScoreHud(canvasWidth, canvasHeight);
            ScorePopup?.Draw(canvasWidth, canvasHeight);
            DrawAmmoSupplyHud(canvasWidth, canvasHeight);
            DrawSupportDeliveryMarker(canvasWidth, canvasHeight);
            PrototypeAntiAirHud.Draw(Player, canvasWidth, canvasHeight);
            DrawAircraftThreatHud(canvasWidth);
            if (!string.IsNullOrEmpty(Player.SupportStatus))
            {
                float supportY = Player.CurrentVehicle is IPrototypeSeatedVehicle seats ?
                    canvasHeight - 224f - seats.SeatCount * 19f : canvasHeight - 202f;
                GUI.Label(new Rect(12, supportY, 370, 24), Player.SupportStatus, body);
            }
            if (Cursor.lockState == CursorLockMode.Locked && Player.Alive && !Match.Winner.HasValue)
            {
                if (!tankSight)
                {
                    DrawCrosshair(canvasWidth, canvasHeight);
                    DrawVehicleBoreReticle(canvasWidth, canvasHeight, scale);
                }
                HitFeedback?.Draw(canvasWidth, canvasHeight);
            }
            if (!Player.Alive && !Match.Winner.HasValue)
                GUI.Label(new Rect(canvasWidth / 2f - 120, canvasHeight / 2f - 65,
                    300, 40), "DOWN  |  SELECT DEPLOYMENT", title);
            if (Match.Winner.HasValue) GUI.Label(new Rect(canvasWidth / 2f - 145,
                canvasHeight / 2f - 80, 350, 45), Match.Winner.Value + " TEAM WINS", title);
            GUI.matrix = previousMatrix;
        }

        public void NotifyBotDowned(PrototypeBot bot)
        {
            if (bot == null || bot.Alive || !MatchStarted) return;
            BotDownFeed.Add(bot.DisplayName, bot.Team, Time.time);
        }

        private void DrawBotDownFeed(float y, int visibleCount = 4, bool showHeading = true)
        {
            IReadOnlyList<PrototypeBotDownNotice> notices = BotDownFeed.Entries;
            if (notices.Count == 0) return;
            int count = Mathf.Min(notices.Count, visibleCount);
            float headingHeight = showHeading ? 24f : 0f;
            DrawHudRect(new Rect(12f, y, 270f, headingHeight + count * 25f),
                new Color(.012f, .045f, .060f, .86f));
            if (showHeading)
            {
                var heading = new GUIStyle(GUI.skin.label) { fontSize = 11 };
                heading.normal.textColor = new Color(.72f, .84f, .86f);
                GUI.Label(new Rect(23f, y + 4f, 248f, 20f), "战场播报 / DOWNED", heading);
            }
            var row = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold };
            for (int i = 0; i < count; i++)
            {
                PrototypeBotDownNotice notice = notices[i];
                float alpha = Mathf.Clamp01(notice.ExpiresAt - Time.time);
                row.normal.textColor = notice.Team == Player.Team ?
                    new Color(.60f, .85f, 1f, alpha) : new Color(1f, .62f, .51f, alpha);
                GUI.Label(new Rect(23f, y + headingHeight + i * 25f, 248f, 23f),
                    (notice.Team == PrototypeTeam.Blue ? "[BLUE] " : "[RED] ") +
                    notice.Name + "  被击倒", row);
            }
        }

        private void DrawBotNames(float width, float height)
        {
            if (!Player.Alive || Player.ViewCamera == null || Player.CurrentVehicle != null) return;
            var label = new GUIStyle(GUI.skin.label) { fontSize = 11,
                fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            foreach (PrototypeBot bot in Bots)
            {
                if (bot == null || !bot.Alive || bot.IsPassenger ||
                    Vector2.Distance(Player.MapPosition, bot.MapPosition) > 65f ||
                    PrototypeLayout.SightBlocked(Player.MapPosition, bot.MapPosition, .1f)) continue;
                Vector3 projected = Player.ViewCamera.WorldToViewportPoint(
                    bot.transform.position + Vector3.up * 1.05f);
                if (projected.z <= 0f || projected.x < 0f || projected.x > 1f ||
                    projected.y < 0f || projected.y > 1f) continue;
                float x = projected.x * width, y = (1f - projected.y) * height;
                bool friendly = bot.Team == Player.Team;
                if (!friendly && (Mathf.Abs(x - width * .5f) > 35f ||
                    Mathf.Abs(y - height * .5f) > 60f)) continue;
                float left = Mathf.Clamp(x - 65f, 0f, width - 130f);
                DrawHudRect(new Rect(left, y - 21f, 130f, 20f),
                    new Color(.015f, .035f, .043f, .74f));
                label.normal.textColor = friendly ? new Color(.65f, .89f, 1f) :
                    new Color(1f, .67f, .53f);
                GUI.Label(new Rect(left, y - 21f, 130f, 20f), bot.DisplayName, label);
            }
        }

        private static string VehicleControls(IPrototypeVehicle vehicle)
        {
            IPrototypeSeatedVehicle seats = vehicle as IPrototypeSeatedVehicle;
            string drive = seats == null || seats.SeatCanDrive ? "WASD drive  |  " : "";
            if (vehicle is PrototypeCombinedArmsVehicle combined &&
                combined.IsAircraft && seats.SeatCanDrive)
                drive = "WASD fly  |  Space climb  |  Ctrl descend  |  Shift boost  |  ";
            string weapon = seats != null && seats.SeatCanFire ?
                "Mouse aim  |  LMB fire  |  " : "";
            if (vehicle is PrototypeCombinedArmsVehicle armed && seats.SeatCanFire &&
                seats.PlayerSeat == 0 &&
                (armed.Kind == PrototypeVehicleKind.AntiAir ||
                 armed.Kind == PrototypeVehicleKind.Jet))
                weapon = "1/2 weapon  |  LMB fire  |  RMB missile  |  X countermeasure  |  ";
            else if (vehicle is PrototypeCombinedArmsVehicle helicopter &&
                helicopter.Kind == PrototypeVehicleKind.Helicopter && seats.SeatCanFire)
                weapon = (seats.PlayerSeat == 0 ? "1 cannon / 2 belly MG  |  " : "BELLY MG  |  ") +
                    "LMB fire  |  " + (seats.PlayerSeat == 0 ? "X flares  |  " : "");
            else if (vehicle is PrototypeCombinedArmsVehicle recon &&
                recon.Kind == PrototypeVehicleKind.ReconHelicopter)
                weapon = (seats.SeatCanFire ? "MG  |  LMB fire  |  " : "") +
                    (seats.PlayerSeat == 0 ? "Q radar  |  X flares  |  " : "");
            else if (vehicle is PrototypeCombinedArmsVehicle ifv &&
                ifv.Kind == PrototypeVehicleKind.Ifv && seats.SeatCanFire && seats.PlayerSeat == 0)
                weapon = "1/2 weapon  |  Mouse aim  |  LMB fire  |  X smoke  |  ";
            return drive + weapon + "E exit  |  Esc menu";
        }

        private void DrawVehicleSeats(IPrototypeSeatedVehicle vehicle,
            float canvasHeight, GUIStyle style)
        {
            float y = canvasHeight - 180f - vehicle.SeatCount * 19f;
            DrawHudRect(new Rect(12f, y, 270f, 60f + vehicle.SeatCount * 19f),
                new Color(.012f, .045f, .060f, .82f));
            GUI.Label(new Rect(24f, y + 8f, 248f, 21f),
                "SEATS  /  " + Player.CurrentSeatName, style);
            for (int seat = 0; seat < vehicle.SeatCount; seat++)
            {
                MonoBehaviour occupant = vehicle.GetSeatOccupant(seat);
                string state = seat == vehicle.PlayerSeat ? "YOU" :
                    occupant == null ? "FREE" : occupant is PrototypeBot bot ? bot.DisplayName : "OCCUPIED";
                GUI.Label(new Rect(24f, y + 31f + seat * 19f, 248f, 20f),
                    "F" + (seat + 1) + "  " + vehicle.GetSeatName(seat) + "  ·  " + state, style);
            }
            if (!string.IsNullOrEmpty(Player.VehicleSeatStatus))
                GUI.Label(new Rect(12f, y - 26f, 520f, 24f),
                    Player.VehicleSeatStatus, style);
        }

        private bool DrawSoloResult(float canvasWidth, float canvasHeight)
        {
            DrawHudRect(new Rect(0f, 0f, canvasWidth, canvasHeight),
                new Color(.008f, .025f, .032f, .84f));
            float x = (canvasWidth - 600f) * .5f;
            float y = (canvasHeight - 420f) * .5f;
            DrawHudRect(new Rect(x, y, 600f, 420f),
                new Color(.035f, .085f, .105f, .98f));
            DrawHudRect(new Rect(x, y, 600f, 4f),
                Match.Winner == PrototypeTeam.Blue ?
                    new Color(.42f, .80f, .96f) : new Color(.95f, .47f, .39f));
            var heading = new GUIStyle(GUI.skin.label)
            {
                fontSize = 30, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            heading.normal.textColor = Color.white;
            var detail = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15, alignment = TextAnchor.MiddleCenter
            };
            detail.normal.textColor = new Color(.78f, .88f, .91f);
            var button = new GUIStyle(GUI.skin.button)
            {
                fontSize = 18, fontStyle = FontStyle.Bold
            };
            string result = Match.Winner == Player.Team ? "胜利 / VICTORY" :
                "战败 / DEFEAT";
            GUI.Label(new Rect(x + 30f, y + 35f, 540f, 48f), result, heading);
            GUI.Label(new Rect(x + 30f, y + 92f, 540f, 30f),
                (Match.Mode == PrototypeGameMode.Breakthrough ? "攻防" : "征服") +
                " · " + (Match.Winner == PrototypeTeam.Blue ? "蓝方" : "红方") +
                "赢得战局", detail);
            GUI.Label(new Rect(x + 30f, y + 135f, 540f, 30f),
                "BLUE " + Mathf.CeilToInt(Match.BlueTickets) + "   VS   " +
                (Match.Mode == PrototypeGameMode.Breakthrough ? "∞" :
                    Mathf.CeilToInt(Match.RedTickets).ToString()) + " RED", detail);
            string objectives = "";
            foreach (PrototypeCapturePoint point in Match.Points)
                objectives += point.Definition.Id + " " +
                    (point.Owner == PrototypeTeam.Blue ? "BLU" :
                        point.Owner == PrototypeTeam.Red ? "RED" : "---") + "    ";
            GUI.Label(new Rect(x + 30f, y + 186f, 540f, 30f), objectives, detail);
            var personal = Scores.Get(Player);
            GUI.Label(new Rect(x + 30f, y + 228f, 540f, 30f),
                "本局战绩 " + personal.Total + "  ·  剩余支援积分 " + personal.Credits, detail);
            GUI.Label(new Rect(x + 30f, y + 265f, 540f, 26f),
                "击杀 " + personal.Kills + "  助攻 " + personal.Assists +
                "  占领 " + personal.Captures + "  毁车 " + personal.VehiclesDestroyed, detail);
            return GUI.Button(new Rect(x + 70f, y + 317f, 460f, 56f),
                "返回大厅 / MAIN MENU", button);
        }

        private void DrawTicketBanner(float width)
        {
            float x = width * .5f - 168f;
            DrawHudRect(new Rect(x, 9f, 336f, 48f), new Color(.012f, .045f, .065f, .83f));
            DrawHudRect(new Rect(x, 9f, 4f, 48f), new Color(.38f, .72f, .92f));
            DrawHudRect(new Rect(x + 332f, 9f, 4f, 48f), new Color(.89f, .38f, .31f));
            var blue = new GUIStyle(GUI.skin.label) { fontSize = 18,
                fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            blue.normal.textColor = new Color(.58f, .83f, 1f);
            var red = new GUIStyle(blue);
            red.normal.textColor = new Color(1f, .59f, .52f);
            var separator = new GUIStyle(blue) { fontSize = 11 };
            separator.normal.textColor = new Color(.72f, .79f, .80f);
            GUI.Label(new Rect(x + 8f, 13f, 141f, 39f),
                "◆ BLUE " + Mathf.CeilToInt(Match.BlueTickets), blue);
            GUI.Label(new Rect(x + 149f, 13f, 38f, 39f), "VS", separator);
            GUI.Label(new Rect(x + 187f, 13f, 141f, 39f),
                (Match.Mode == PrototypeGameMode.Breakthrough ? "∞" :
                    Mathf.CeilToInt(Match.RedTickets).ToString()) + " RED ◆", red);
            float start = width * .5f - Match.Points.Count * 18f;
            var pointStyle = new GUIStyle(GUI.skin.label) { fontSize = 12,
                fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            for (int i = 0; i < Match.Points.Count; i++)
            {
                PrototypeCapturePoint point = Match.Points[i];
                Color color = point.Owner == PrototypeTeam.Blue ?
                    new Color(.20f, .45f, .60f, .9f) : point.Owner == PrototypeTeam.Red ?
                    new Color(.58f, .22f, .20f, .9f) : new Color(.09f, .16f, .20f, .87f);
                float px = start + i * 36f;
                if (Match.Mode == PrototypeGameMode.Breakthrough &&
                    Match.IsPointActive(point))
                    DrawHudRect(new Rect(px - 2f, 59f, 34f, 29f),
                        new Color(1f, .78f, .32f, .95f));
                DrawHudRect(new Rect(px, 61f, 30f, 25f), color);
                pointStyle.normal.textColor = point.Contested ?
                    new Color(1f, .79f, .47f) : Color.white;
                GUI.Label(new Rect(px, 61f, 30f, 25f), point.Definition.Id, pointStyle);
            }
            if (!networkModeActive)
            {
                int blueTotal = 1, blueAlive = Player.Alive ? 1 : 0;
                int redTotal = 0, redAlive = 0;
                foreach (PrototypeBot bot in Bots)
                    if (bot.Team == PrototypeTeam.Blue)
                    { blueTotal++; if (bot.Alive) blueAlive++; }
                    else
                    { redTotal++; if (bot.Alive) redAlive++; }
                var rosterStyle = new GUIStyle(GUI.skin.label) { fontSize = 11,
                    alignment = TextAnchor.MiddleCenter };
                rosterStyle.normal.textColor = Color.white;
                GUI.Label(new Rect(x, 89f, 336f, 19f),
                    "BLUE " + blueAlive + "/" + blueTotal +
                    "  ·  RED " + redAlive + "/" + redTotal + "  ALIVE", rosterStyle);
            }
        }

        private void DrawCrosshair(float width, float height)
        {
            float cx = width * .5f;
            float cy = height * .5f;
            float gap = Player.CurrentVehicle != null ? 5f : Player.CrosshairSpread;
            float arm = Player.IsAiming ? 4f : 7f;
            DrawCrosshairMark(new Vector2(cx, cy), gap, arm, Player.IsAiming, CrosshairSize);
            if (Player.CurrentVehicle is IPrototypeWeaponAim weapon &&
                weapon.PlayerControlsWeapon)
            {
                var label = new GUIStyle(GUI.skin.label) { fontSize = 10,
                    fontStyle = FontStyle.Bold };
                label.normal.textColor = new Color(.63f, .94f, .96f);
                GUI.Label(new Rect(cx + 13f, cy + 8f, 62f, 17f), "AIM", label);
            }
        }

        public static void DrawCrosshairMark(Vector2 center, float gap, float arm, bool aiming, float size)
        {
            size = ClampCrosshairSize(size);
            gap *= size; arm *= size;
            float cx = center.x, cy = center.y, thickness = 2f * size, half = size;
            Color edge = new Color(.005f, .035f, .04f, .85f);
            Color mark = aiming ? new Color(.60f, .95f, .95f, .96f) :
                new Color(.96f, .98f, .95f, .92f);
            DrawHudRect(new Rect(cx - gap - arm - size, cy - thickness, arm + thickness, thickness * 2f), edge);
            DrawHudRect(new Rect(cx + gap - size, cy - thickness, arm + thickness, thickness * 2f), edge);
            DrawHudRect(new Rect(cx - thickness, cy - gap - arm - size, thickness * 2f, arm + thickness), edge);
            DrawHudRect(new Rect(cx - thickness, cy + gap - size, thickness * 2f, arm + thickness), edge);
            DrawHudRect(new Rect(cx - gap - arm, cy - half, arm, thickness), mark);
            DrawHudRect(new Rect(cx + gap, cy - half, arm, thickness), mark);
            DrawHudRect(new Rect(cx - half, cy - gap - arm, thickness, arm), mark);
            DrawHudRect(new Rect(cx - half, cy + gap, thickness, arm), mark);
            DrawHudRect(new Rect(cx - half, cy - half, thickness, thickness), mark);
        }

        private void DrawVehicleBoreReticle(float width, float height, float scale)
        {
            if (!(Player.CurrentVehicle is IPrototypeWeaponAim weapon) ||
                !weapon.PlayerControlsWeapon || weapon.WeaponMuzzle == null ||
                Player.ViewCamera == null) return;
            Vector3 point = PrototypeVehicleAim.BorePoint(weapon.WeaponMuzzle,
                Player.CurrentVehicle.VehicleTransform, weapon.WeaponRange);
            Vector3 screen = Player.ViewCamera.WorldToScreenPoint(point);
            if (screen.z <= 0f)
            {
                Vector3 local = Player.ViewCamera.transform.InverseTransformDirection(
                    weapon.WeaponMuzzle.forward);
                screen.x = Screen.width * (.5f + Mathf.Sign(local.x) * .5f);
                screen.y = Screen.height * (.5f + Mathf.Sign(local.y) * .5f);
            }
            float x = Mathf.Clamp(screen.x / scale, 24f, width - 24f);
            float y = Mathf.Clamp((Screen.height - screen.y) / scale, 24f, height - 38f);
            Color outline = new Color(.025f, .035f, .035f, .9f);
            Color bore = new Color(1f, .72f, .30f, .97f);
            float size = CrosshairSize;
            DrawHudRect(new Rect(x - 10f * size, y - 10f * size, 7f * size, 2f * size), outline);
            DrawHudRect(new Rect(x + 3f * size, y - 10f * size, 7f * size, 2f * size), outline);
            DrawHudRect(new Rect(x - 10f * size, y + 8f * size, 7f * size, 2f * size), outline);
            DrawHudRect(new Rect(x + 3f * size, y + 8f * size, 7f * size, 2f * size), outline);
            DrawHudRect(new Rect(x - 10f * size, y - 10f * size, 2f * size, 7f * size), bore);
            DrawHudRect(new Rect(x + 8f * size, y - 10f * size, 2f * size, 7f * size), bore);
            DrawHudRect(new Rect(x - 10f * size, y + 3f * size, 2f * size, 7f * size), bore);
            DrawHudRect(new Rect(x + 8f * size, y + 3f * size, 2f * size, 7f * size), bore);
            DrawHudRect(new Rect(x - size, y - size, 2f * size, 2f * size), bore);
            var label = new GUIStyle(GUI.skin.label) { fontSize = 10,
                fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            label.normal.textColor = bore;
            GUI.Label(new Rect(x - 30f, y - 29f, 60f, 16f), "BARREL", label);
        }

        private void DrawCombatHud(float width, float height)
        {
            bool firearm = Player.HeldEquipment == PrototypeHeldEquipment.Firearm;
            bool throwable = Player.HeldEquipment == PrototypeHeldEquipment.Throwable;
            bool antiAir = Player.HeldEquipment == PrototypeHeldEquipment.AntiAirLauncher;
            float right = width - 14f;
            float top = height - 229f;
            var small = new GUIStyle(GUI.skin.label) { fontSize = 10, alignment = TextAnchor.MiddleLeft };
            small.normal.textColor = new Color(.79f, .89f, .91f);
            var number = new GUIStyle(GUI.skin.label) { fontSize = 27,
                fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
            number.normal.textColor = Color.white;
            var key = new GUIStyle(GUI.skin.label) { fontSize = 11,
                fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperLeft };
            key.normal.textColor = Color.white;
            var panel = new Color(.025f, .075f, .09f, .82f);
            var accent = new Color(.49f, .79f, .83f);
            Rect info = new Rect(right - 200f, top, 200f, 77f);
            DrawHudRect(info, panel);
            GUI.Label(new Rect(info.x + 10f, info.y + 5f, 180f, 16f), "弹药 / AMMUNITION", small);
            GUI.Label(new Rect(info.x + 9f, info.y + 28f, 105f, 18f),
                firearm ? Player.ActiveWeapon.Name : throwable ?
                    Player.Loadout.throwable == PrototypeThrowableId.Smoke ? "SMOKE GRENADE" : "FRAG GRENADE" :
                    antiAir ? "ANTI-AIR LAUNCHER" : "ANTI-ARMOR LAUNCHER", small);
            GUI.Label(new Rect(info.x + 8f, info.y + 46f, 53f, 17f),
                firearm ? Player.ActiveWeapon.Automatic ? "AUTO" : "SEMI" :
                    throwable ? "THROW" : antiAir ? "SEEKER" : "ROCKET", small);
            GUI.Label(new Rect(info.x + 66f, info.y + 35f, 75f, 39f),
                firearm ? Player.Reloading ? "…" : Player.Ammo.ToString() :
                    throwable ? Player.ThrowableCount.ToString() : antiAir ?
                    Player.AntiAirMissileCount.ToString() : Player.RocketCount.ToString(), number);
            GUI.Label(new Rect(info.x + 145f, info.y + 52f, 49f, 18f),
                firearm ? "/ " + Player.Reserve : "LEFT", small);

            bool engineer = Player.Class == PrototypeInfantryClass.Engineer;
            bool ammoSupport = Player.Class == PrototypeInfantryClass.Assault;
            int cardCount = engineer ? 6 : ammoSupport ? 5 : 4;
            Texture2D[] icons =
            {
                PrototypeEquipmentIcons.Weapon(Player.Loadout.primary),
                PrototypeEquipmentIcons.Weapon(Player.Loadout.secondary),
                PrototypeEquipmentIcons.Gadget(Player.Class),
                PrototypeEquipmentIcons.Throwable(Player.Loadout.throwable),
                ammoSupport ? PrototypeEquipmentIcons.AmmoCrate() : PrototypeEquipmentIcons.Rocket(),
                PrototypeEquipmentIcons.AntiAir()
            };
            string[] names =
            {
                ShortWeaponName(Player.Loadout.primary),
                ShortWeaponName(Player.Loadout.secondary),
                PrototypeStartMenuView.GadgetName(Player.Class),
                Player.Loadout.throwable == PrototypeThrowableId.Frag ? "FRAG" : "SMOKE",
                ammoSupport ? "AMMO BOX" : "ROCKET", "AA"
            };
            string[] keys = { "1", "2", "X", "3", ammoSupport ? "6" : "4", "5" };
            string[] counts = { "", "", Player.Kit.GadgetCharges.ToString(),
                Player.ThrowableCount.ToString(), ammoSupport ? (Supplies?.OwnedCount(Player) ?? 0) + "/2" :
                Player.RocketCount.ToString(), Player.AntiAirMissileCount.ToString() };
            for (int i = 0; i < cardCount; i++)
            {
                Rect card = PrototypePlayerHealthHud.EquipmentCard(width, height, i);
                float x = card.x, cardTop = card.y;
                bool selected = firearm && (i == 0 && !Player.SecondaryEquipped ||
                    i == 1 && Player.SecondaryEquipped) ||
                    i == 3 && throwable || i == 4 && Player.HeldEquipment == PrototypeHeldEquipment.Launcher ||
                    i == 5 && antiAir;
                DrawHudRect(new Rect(x, cardTop, 57f, 59f), selected ?
                    new Color(.11f, .29f, .32f, .93f) : panel);
                if (selected) DrawHudRect(new Rect(x, cardTop, 57f, 2f), accent);
                GUI.DrawTexture(new Rect(x + 7f, cardTop + 10f, 43f, 18f), icons[i], ScaleMode.ScaleToFit, true);
                GUI.Label(new Rect(x + 4f, cardTop + 2f, 18f, 16f), keys[i], key);
                GUI.Label(new Rect(x + 4f, cardTop + 32f, 51f, 14f), names[i], small);
                if (counts[i].Length > 0)
                    GUI.Label(new Rect(x + 39f, cardTop + 42f, 18f, 15f), counts[i], key);
            }
        }

        private void DrawScoreHud(float width, float height)
        {
            if (Scores == null) return;
            var score = Scores.Get(Player);
            float y = height - 109f;
            DrawHudRect(new Rect(12f, y, 230f, 61f), new Color(.012f, .045f, .060f, .86f));
            var main = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
            main.normal.textColor = new Color(.72f, .91f, .89f);
            GUI.Label(new Rect(23f, y + 4f, 210f, 22f),
                IsNetworkReplica ? "[ Y ] 双方积分榜 · 主机同步" :
                "可用积分 " + score.Credits + "   [ B ]", main);
            var small = new GUIStyle(GUI.skin.label) { fontSize = 11 };
            small.normal.textColor = new Color(.79f, .87f, .90f);
            GUI.Label(new Rect(23f, y + 26f, 210f, 18f),
                IsNetworkReplica ? "本局成绩由房主记录" :
                "累计总分 " + score.Total + "   [ Y ] 积分榜", small);
            foreach (var notice in Scores.Notices)
            {
                if (!ReferenceEquals(notice.Actor, Player) || Time.time - notice.Time > 5f) continue;
                GUI.Label(new Rect(23f, y + 43f, 210f, 16f),
                    (notice.Points >= 0 ? "+" : "") + notice.Points + "  " + notice.Reason, small);
                break;
            }
            if (Support != null && !string.IsNullOrEmpty(Support.Status) &&
                (Player.CurrentVehicle == null || Support.Pending))
            {
                float supportY = Player.CurrentVehicle is IPrototypeSeatedVehicle seats ?
                    height - 270f - seats.SeatCount * 19f : height - 184f;
                GUI.Label(new Rect(12f, supportY, 400f, 42f),
                    Support.Status + (Support.Pending ? " · " + Support.DeliveryRemaining.ToString("0.0") + " s" : ""), small);
            }
        }

        private void DrawAmmoSupplyHud(float width, float height)
        {
            if (Supplies == null || !Player.Alive || Player.CurrentVehicle != null) return;
            var text = new GUIStyle(GUI.skin.label) { fontSize = 12 };
            text.normal.textColor = new Color(.83f, .94f, .90f);
            PrototypeAmmoSupplyPoint point = Supplies.NearestInteraction(Player.transform.position);
            if (point != null)
            {
                float y = height - 267f;
                DrawHudRect(new Rect(12f, y, 365f, 52f), new Color(.012f, .045f, .060f, .86f));
                float wait = Supplies.ResupplyReadyIn(Player);
                GUI.Label(new Rect(23f, y + 4f, 343f, 22f), "[ E ] " + point.DisplayName +
                    (point.IsDeployable ? " · " + Mathf.CeilToInt(point.Health) + " HP" : " · 中立物资"), text);
                text.fontSize = 11;
                GUI.Label(new Rect(23f, y + 28f, 343f, 20f), !string.IsNullOrEmpty(Supplies.Status) ? Supplies.Status : wait > 0f ?
                    "个人补给冷却 " + wait.ToString("0.0") + " s" : "补满主副武器、手雷与导弹 · 领取冷却 30 秒", text);
            }
            if (Player.Class == PrototypeInfantryClass.Assault)
            {
                text.fontSize = 11;
                float wait = Supplies.DeployReadyIn(Player);
                GUI.Label(new Rect(12f, height - 213f, 365f, 22f), IsNetworkSession ?
                    "X 弹药包 · 6 补给箱仅用于单人战局" :
                    "X 弹药包 · 6 布置补给箱 " + Supplies.OwnedCount(Player) + "/2" +
                    (wait > 0f ? " · " + wait.ToString("0") + " s" : " · 可部署"), text);
            }
            if (point == null && !string.IsNullOrEmpty(Supplies.Status))
                GUI.Label(new Rect(12f, height - 239f, 365f, 24f), Supplies.Status, text);
        }

        private void DrawSupportPanel(float width, float height)
        {
            DrawHudRect(new Rect(0f, 0f, width, height), new Color(.005f, .015f, .025f, .76f));
            float x = width * .5f - 350f, y = height * .5f - 241f;
            DrawHudRect(new Rect(x, y, 700f, 482f), new Color(.025f, .065f, .080f, .97f));
            DrawHudRect(new Rect(x, y, 700f, 3f), new Color(.48f, .79f, .83f));
            var title = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold };
            title.normal.textColor = Color.white;
            var text = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true };
            text.normal.textColor = new Color(.80f, .90f, .92f);
            var entry = Scores.Get(Player);
            GUI.Label(new Rect(x + 24f, y + 16f, 640f, 36f), "战场支援 / REQUISITION", title);
            GUI.Label(new Rect(x + 24f, y + 55f, 640f, 26f),
                "可用积分 " + entry.Credits + "   ·   本局累计 " + entry.Total +
                "   ·   冷却 " + Support.CooldownReadyIn.ToString("0") + " s", text);
            GUI.Label(new Rect(x + 24f, y + 83f, 650f, 30f),
                IsNetworkSession ? "载具支援当前用于单人战局，LAN 新载具同步尚未接入。" :
                "击杀 100 / 助攻 50 / 占领 150 · 6 秒交付至己方基地增援位 · 战局继续运行", text);
            int i = 0;
            foreach (PrototypeSupportOption option in Support.Catalog)
            {
                float row = y + 116f + i++ * 56f;
                DrawHudRect(new Rect(x + 24f, row, 652f, 50f), new Color(.055f, .13f, .16f, .96f));
                GUI.Label(new Rect(x + 36f, row + 3f, 450f, 23f), option.ChineseName + "  ·  " + option.Cost + " 积分", text);
                var description = new GUIStyle(text) { fontSize = 11 };
                GUI.Label(new Rect(x + 36f, row + 26f, 450f, 20f), option.Description, description);
                bool previousEnabled = GUI.enabled;
                GUI.enabled = previousEnabled && !IsNetworkSession && !IsNetworkReplica &&
                    !Support.Pending && Support.CooldownReadyIn <= 0f && entry.Credits >= option.Cost;
                if (GUI.Button(new Rect(x + 518f, row + 8f, 146f, 34f), "呼叫 / REQUEST"))
                    Support.TryRequest(option.Kind, out _);
                GUI.enabled = previousEnabled;
            }
            GUI.Label(new Rect(x + 24f, y + 404f, 650f, 36f),
                Support.Pending ? Support.Status + "  ·  " + Support.DeliveryRemaining.ToString("0.0") + " 秒" :
                string.IsNullOrEmpty(Support.Status) ? "每队最多两辆支援载具；失败交付返还积分。" : Support.Status, text);
            if (GUI.Button(new Rect(x + 24f, y + 443f, 652f, 29f), "关闭 / B 或 Esc")) SetSupportMenu(false);
        }

        private void DrawSupportDeliveryMarker(float width, float height)
        {
            IPrototypeVehicle delivered = Support?.LastDelivered;
            if (delivered == null || !delivered.Alive || Player.CurrentVehicle == delivered || Player.ViewCamera == null) return;
            Vector3 position = delivered.VehicleTransform.position + Vector3.up * 3f;
            Vector3 screen = Player.ViewCamera.WorldToScreenPoint(position);
            float scale = Screen.width / width;
            float x = screen.x / scale, y = (Screen.height - screen.y) / scale;
            if (screen.z <= 0f)
            {
                Vector3 local = Player.ViewCamera.transform.InverseTransformDirection(position - Player.ViewCamera.transform.position);
                x = local.x >= 0f ? width - 115f : 115f;
                y = height * .5f;
            }
            x = Mathf.Clamp(x, 115f, width - 115f); y = Mathf.Clamp(y, 110f, height - 215f);
            var label = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter };
            label.normal.textColor = new Color(1f, .82f, .38f);
            DrawHudRect(new Rect(x - 105f, y, 210f, 43f), new Color(.04f, .055f, .065f, .85f));
            GUI.Label(new Rect(x - 102f, y + 2f, 204f, 38f),
                "基地支援 · " + delivered.VehicleName + "\n" +
                Mathf.RoundToInt(Vector2.Distance(Player.MapPosition, delivered.MapPosition)) + " m · E 上车", label);
        }

        private void DrawAircraftThreatHud(float width)
        {
            if (!(Player.CurrentVehicle is PrototypeCombinedArmsVehicle aircraft) || !aircraft.IsAircraft ||
                (!aircraft.BeingLocked && !aircraft.IncomingMissile) || aircraft.CountermeasureActive) return;
            float x = width * .5f - 190f;
            Color warning = aircraft.IncomingMissile ? new Color(1f, .35f, .22f) : new Color(1f, .76f, .27f);
            DrawHudRect(new Rect(x, 106f, 380f, 66f), new Color(.11f, .025f, .015f, .92f));
            DrawHudRect(new Rect(x, 106f, 380f, 3f), warning);
            var label = new GUIStyle(GUI.skin.label) { fontSize = 19, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter };
            label.normal.textColor = warning;
            GUI.Label(new Rect(x + 5f, 113f, 370f, 27f), aircraft.IncomingMissile ?
                "导弹来袭 / MISSILE INCOMING" : "正在被锁定 / TARGET LOCK", label);
            label.fontSize = 12;
            GUI.Label(new Rect(x + 5f, 143f, 370f, 23f), aircraft.PlayerSeat == 0 ?
                "X 热诱弹 · 打断追踪并免疫锁定 10 秒" : "通知驾驶员使用 X 热诱弹", label);
        }

        private static string ShortWeaponName(PrototypeWeaponId id)
        {
            switch (id)
            {
                case PrototypeWeaponId.Carbine: return "IF-27";
                case PrototypeWeaponId.Marksman: return "M89";
                case PrototypeWeaponId.Smg: return "VX-9";
                case PrototypeWeaponId.Lmg: return "H60";
                case PrototypeWeaponId.BattleRifle: return "BR-44";
                case PrototypeWeaponId.Sniper: return "S12";
                default: return "P8";
            }
        }

        private static void DrawHudRect(Rect rect, Color color)
        {
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = old;
        }

        private static void DrawOrders(PrototypeCommander commander, string team,
            float x, float y, GUIStyle style)
        {
            for (int i = 0; i < commander.Squads.Count; i++)
            {
                PrototypeSquad squad = commander.Squads[i];
                GUI.Label(new Rect(x, y + i * 19f, 245, 20),
                    team + " " + (i + 1) + "  " + squad.ObjectiveId + " / " + squad.Mission +
                    "  (" + squad.LivingMembers + "/" + squad.Members.Length + ")", style);
            }
        }
    }
}
