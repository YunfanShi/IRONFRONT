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
        private Material markerNeutral;
        private Material markerBlue;
        private Material markerRed;
        private Material blueTracer;
        private Material redTracer;
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

        public PrototypeMatch Match { get; private set; }
        public PrototypeNavigation Navigation { get; private set; }
        public PrototypePlayer Player { get; private set; }
        public PrototypeCommander BlueCommander { get; private set; }
        public PrototypeCommander RedCommander { get; private set; }
        public bool MatchStarted { get; private set; }
        public bool IsNetworkReplica => networkModeActive &&
            (!networkHost || lan == null || !lan.IsConnected ||
             (lan.LastRoom != null && lan.LastRoom.paused));
        public readonly List<PrototypeRemotePlayer> RemotePlayers =
            new List<PrototypeRemotePlayer>();
        public PrototypeFrontend Frontend { get; private set; }
        public readonly List<PrototypeBot> Bots = new List<PrototypeBot>();
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
            BlueCommander.Tick(Time.time, Match);
            RedCommander.Tick(Time.time, Match);
            Frontend = new PrototypeFrontend();
            Frontend.Open();
            lan = new PrototypeLanClient();
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

        private void Update()
        {
            if (Match == null || Player == null) return;
            lan?.Poll();
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
            Match.Tick(Mathf.Min(Time.deltaTime, 0.05f), Player, Bots,
                networkModeActive ? RemotePlayers : null);
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
            MatchStarted = true;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
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
                    "  #" + member.id + "  " + (member.team ?? "blue").ToUpperInvariant() +
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
                await lan.CreateRoom(roomMenu.Address);
                networkModeActive = true;
                roomStatus = "Share room code " + lan.Code + " with another Unity player.";
            }
            catch (System.Exception exception) { roomStatus = exception.Message; }
        }

        private async void JoinLanRoom()
        {
            try
            {
                await lan.JoinRoom(roomMenu.Address, roomMenu.JoinCode);
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
                    if (member.id == remote.Id && member.connected &&
                        member.team == (remote.Team == PrototypeTeam.Blue ? "blue" : "red"))
                    { keep = true; break; }
                if (keep) continue;
                RemotePlayers.RemoveAt(i);
                Destroy(remote.gameObject);
            }
            foreach (PrototypeLanRosterMember member in roster)
            {
                if (!member.connected || member.id == lan.LocalId) continue;
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
                    if (remote.Id == peer.id)
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
                    if (remote.Id == request.id)
                    {
                        remote.TryDeploy(request.location);
                        break;
                    }
            }
            while (lan.PeerDownedActions.Count > 0)
            {
                PrototypeLanPeerDowned request = lan.PeerDownedActions.Dequeue();
                foreach (PrototypeRemotePlayer remote in RemotePlayers)
                    if (remote.Id == request.id)
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
            bool active = Cursor.lockState == CursorLockMode.Locked && Player.Alive;
            float pitch = Player.ViewCamera.transform.eulerAngles.x;
            if (pitch > 180f) pitch -= 360f;
            lan.SendInput(new PrototypeLanInput {
                forward = active ? Input.GetAxisRaw("Vertical") : 0f,
                side = active ? Input.GetAxisRaw("Horizontal") : 0f,
                yaw = Player.transform.eulerAngles.y,
                pitch = pitch,
                sprint = active && Input.GetKey(KeyCode.LeftShift),
                fire = active && Input.GetMouseButton(0),
                callRescue = Player.IsDowned && Input.GetKey(KeyCode.H),
                giveUp = Player.IsDowned && Input.GetKey(KeyCode.Space)
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
                blueTickets = Match.BlueTickets, redTickets = Match.RedTickets };
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
            if (state.points != null)
                foreach (PrototypeLanPoint point in state.points)
                    Match.SetNetworkPoint(point.id, point.control,
                        point.owner == "blue" ? 0 : point.owner == "red" ? 1 : -1);
            if (state.players != null)
                foreach (PrototypeLanActor actor in state.players)
                {
                    Vector3 position = new Vector3(actor.x, actor.y, actor.z);
                    if (actor.id == lan.LocalId)
                        Player.ApplyNetworkState(position, actor.yaw, actor.hp, actor.alive,
                            actor.ammo, actor.reserve, actor.respawnRemaining,
                            actor.downedRemaining, actor.rescueCalled);
                    else
                        foreach (PrototypeRemotePlayer remote in RemotePlayers)
                            if (remote.Id == actor.id)
                            { remote.SetNetworkState(position, actor.yaw, actor.hp, actor.alive); break; }
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
                    }
            if (Player.Alive && Frontend.IsOpen) Frontend.Close();
            else if (Player.IsDowned && !Frontend.IsDownedScreen &&
                !Match.Winner.HasValue) Frontend.OpenDowned();
            else if (!Player.Alive && !Player.IsDowned &&
                (!Frontend.IsOpen || Frontend.IsDownedScreen) &&
                !Match.Winner.HasValue) Frontend.Open(true);
        }

        private void OnDestroy() { lan?.Close(); }

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
                    Player.SelectClass(Frontend.SelectedClass);
                    BeginBattle();
                }
                else if (Frontend != null && Frontend.LanRequested)
                    roomMenuOpen = true;
                GUI.matrix = previousMatrix;
                return;
            }
            if (Frontend != null && Frontend.IsDownedScreen && Player.IsDowned &&
                !Match.Winner.HasValue)
            {
                PrototypeDownedAction action = Frontend.DrawDowned(canvasWidth,
                    canvasHeight, Player.DownedRemaining, Player.RescueCalled);
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
                    else if (Player.TryDeploy(location, Frontend.SelectedClass))
                        Frontend.Close();
                }
                GUI.matrix = previousMatrix;
                return;
            }
            var title = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
            title.normal.textColor = Color.white;
            var body = new GUIStyle(GUI.skin.label) { fontSize = 14 };
            body.normal.textColor = Color.white;
            GUI.Box(new Rect(12, 12, 370, networkModeActive ? 225 : 210),
                GUIContent.none);
            GUI.Label(new Rect(25, 20, 340, 30), "IRONFRONT  |  UNITY PROTOTYPE", title);
            GUI.Label(new Rect(25, 55, 340, 23),
                "BLUE " + Mathf.CeilToInt(Match.BlueTickets) + "     RED " + Mathf.CeilToInt(Match.RedTickets), body);
            if (networkModeActive)
                GUI.Label(new Rect(25, 206, 340, 23),
                    "ROOM " + (lan?.Code ?? "------") + "  |  " +
                    (networkHost ? "HOST" : "PLAYER") + "  |  " +
                    (lan != null && lan.IsConnected ?
                        lan.LastRoom != null && lan.LastRoom.paused ? "PAUSED" : "CONNECTED" :
                        "RECONNECTING"), body);
            if (Player.CurrentVehicle != null)
                GUI.Label(new Rect(25, 81, 340, 23),
                    Player.CurrentVehicle.VehicleName + "  ARMOR " +
                    Mathf.CeilToInt(Player.CurrentVehicle.Health) +
                    "  SPEED " + Mathf.RoundToInt(Mathf.Abs(Player.CurrentVehicle.Speed) * 3.6f) +
                    " km/h", body);
            else
                GUI.Label(new Rect(25, 81, 340, 23),
                    "HP " + Mathf.CeilToInt(Player.Health) + "  ARMOR " + Mathf.CeilToInt(Player.Kit.Armor) +
                    "   " + Player.Kit.Weapon.Name + " " + Player.Ammo + " / " + Player.Reserve +
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
            float ordersX = canvasWidth - 282f;
            float ordersY = 12f;
            GUI.Box(new Rect(ordersX, ordersY, 270, 176), GUIContent.none);
            GUI.Label(new Rect(ordersX + 12, ordersY + 6, 245, 25), "SQUAD ORDERS", title);
            DrawOrders(BlueCommander, "BLUE", ordersX + 12, ordersY + 34, body);
            DrawOrders(RedCommander, "RED", ordersX + 12, ordersY + 82, body);
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
            GUI.Label(new Rect(ordersX + 12, ordersY + 128, 245, 20),
                "BLUE  COVER " + blueCover + "  SEARCH " + blueSearch, body);
            GUI.Label(new Rect(ordersX + 12, ordersY + 147, 245, 20),
                "RED   COVER " + redCover + "  SEARCH " + redSearch, body);
            string controls = Player.CurrentVehicle is PrototypeTankVehicle ?
                "T90 BASTION  |  WASD drive  |  Mouse aim  |  LMB cannon  |  E exit  |  Esc cursor" :
                Player.CurrentVehicle is PrototypeTransportVehicle transport ?
                "U8 ROVER  |  AI passengers " + transport.PassengerCount + "/2  |  " +
                (transport.PlayerSeat == 0 ?
                    "WASD drive  |  F2 gunner  |  Driver unarmed" :
                    "Mouse aim  |  LMB machine gun  |  F1 driver") +
                "  |  E exit  |  Esc cursor" :
                Player.CurrentVehicle != null ?
                "R4 SCOUT  |  WASD drive  |  Mouse aim  |  LMB machine gun  |  E exit  |  Esc cursor" :
                "WASD move  |  Mouse aim  |  LMB fire  |  R reload  |  X class ability (" +
                Player.Kit.GadgetCharges + ")  |  E vehicle  |  Esc cursor";
            GUI.Label(new Rect(12, canvasHeight - 35, canvasWidth - 24f, 25), controls, body);
            if (!string.IsNullOrEmpty(Player.SupportStatus))
                GUI.Label(new Rect(12, canvasHeight - 59, 370, 24), Player.SupportStatus, body);
            if (Cursor.lockState == CursorLockMode.Locked && Player.Alive && !Match.Winner.HasValue)
            {
                float cx = canvasWidth / 2f;
                float cy = canvasHeight / 2f;
                GUI.Label(new Rect(cx - 5, cy - 10, 25, 25), "+", title);
            }
            if (!Player.Alive && !Match.Winner.HasValue)
                GUI.Label(new Rect(canvasWidth / 2f - 120, canvasHeight / 2f - 65,
                    300, 40), "DOWN  |  SELECT DEPLOYMENT", title);
            if (Match.Winner.HasValue) GUI.Label(new Rect(canvasWidth / 2f - 145,
                canvasHeight / 2f - 80, 350, 45), Match.Winner.Value + " TEAM WINS", title);
            GUI.matrix = previousMatrix;
        }

        private static void DrawOrders(PrototypeCommander commander, string team,
            float x, float y, GUIStyle style)
        {
            GUI.Label(new Rect(x, y, 245, 20), team + "  " + commander.Squads[0].ObjectiveId + " / " +
                commander.Squads[0].Mission + "  (" + commander.Squads[0].LivingMembers + "/2)", style);
            GUI.Label(new Rect(x, y + 21, 245, 20), "          " + commander.Squads[1].ObjectiveId + " / " +
                commander.Squads[1].Mission + "  (" + commander.Squads[1].LivingMembers + "/2)", style);
        }
    }
}
