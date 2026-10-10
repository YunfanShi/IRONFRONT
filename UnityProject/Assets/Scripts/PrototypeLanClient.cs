using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Ironfront.UnityPrototype
{
    // Unity's scene objects are only touched by Poll(), which must run on the main thread.
    // The server assigns host authority; a local room button never grants it.
    public sealed class PrototypeLanClient : IDisposable
    {
        private const string Protocol = "unity-3";
        private const int MaxMessageBytes = 65536;
        private readonly ConcurrentQueue<Incoming> incoming = new ConcurrentQueue<Incoming>();
        private readonly SemaphoreSlim sendGate = new SemaphoreSlim(1, 1);
        private CancellationTokenSource cancellation;
        private ClientWebSocket socket;
        private Uri baseAddress;
        private string hostToken = "";
        private string resumeToken = "";
        private string playerName = "Player";
        private int generation;
        private int nextSequence;
        private float lastInputSendAt = -1f;
        private float lastSnapshotSendAt = -1f;
        private float lastDeploySendAt = -1f;
        private float lastDownedSendAt = -1f;

        public bool IsConnected { get; private set; }
        public bool ReconnectFailed { get; private set; }
        public bool IsHost { get; private set; }
        public int LocalId { get; private set; } = -1;
        public string Code { get; private set; } = "";
        public string Phase { get; private set; } = "preparation";
        public string Error { get; private set; } = "";
        public PrototypeLanRoom LastRoom { get; private set; }
        public PrototypeLanSnapshot LastSnapshot { get; private set; }
        public readonly System.Collections.Generic.Queue<PrototypeLanPeerInput> PeerInputs =
            new System.Collections.Generic.Queue<PrototypeLanPeerInput>();
        public readonly System.Collections.Generic.Queue<PrototypeLanPeerDeploy> PeerDeployments =
            new System.Collections.Generic.Queue<PrototypeLanPeerDeploy>();
        public readonly System.Collections.Generic.Queue<PrototypeLanPeerDowned> PeerDownedActions =
            new System.Collections.Generic.Queue<PrototypeLanPeerDowned>();

        private struct Incoming
        {
            public int generation;
            public string json;
            public string error;
            public bool disconnected;
            public bool reconnectFailed;
        }

        private void Push(Incoming value)
        {
            incoming.Enqueue(value);
            while (incoming.Count > 512) incoming.TryDequeue(out _);
        }

        public async Task CreateRoom(string address, string name)
        {
            Close();
            int current = generation;
            cancellation = new CancellationTokenSource();
            playerName = name;
            try
            {
                baseAddress = ParseAddress(address);
                string json = await HttpJson(new Uri(baseAddress, "/api/unity/rooms"), true,
                    cancellation.Token);
                var created = JsonUtility.FromJson<RoomCreated>(json);
                if (created == null || string.IsNullOrEmpty(created.code) ||
                    string.IsNullOrEmpty(created.hostToken) || created.protocol != Protocol)
                    throw new InvalidOperationException("Unity room protocol mismatch");
                if (current != generation) return;
                Code = created.code;
                hostToken = created.hostToken;
                await ConnectSocket(false, current, cancellation.Token);
            }
            catch (Exception exception)
            {
                if (current == generation) Error = exception.Message;
                throw;
            }
        }

        public async Task JoinRoom(string address, string code, string name)
        {
            Close();
            int current = generation;
            cancellation = new CancellationTokenSource();
            playerName = name;
            try
            {
                baseAddress = ParseAddress(address);
                code = (code ?? "").Trim().ToUpperInvariant();
                if (code.Length != 6)
                    throw new ArgumentException("Enter a six-character room code", nameof(code));
                foreach (char value in code)
                    if (!(value >= 'A' && value <= 'Z') && !(value >= '0' && value <= '9'))
                        throw new ArgumentException("Invalid room code", nameof(code));
                string json = await HttpJson(new Uri(baseAddress, "/api/unity/rooms/" + code),
                    false, cancellation.Token);
                var info = JsonUtility.FromJson<RoomInfo>(json);
                if (info == null || info.code != code || info.protocol != Protocol)
                    throw new InvalidOperationException("Unity room protocol mismatch");
                if (current != generation) return;
                Code = code;
                await ConnectSocket(false, current, cancellation.Token);
            }
            catch (Exception exception)
            {
                if (current == generation) Error = exception.Message;
                throw;
            }
        }

        public void Poll()
        {
            while (incoming.TryDequeue(out Incoming item))
            {
                if (item.generation != generation) continue;
                if (item.disconnected)
                {
                    IsConnected = false;
                    IsHost = false;
                    ReconnectFailed = false;
                    PeerInputs.Clear();
                    PeerDeployments.Clear();
                    PeerDownedActions.Clear();
                }
                if (item.reconnectFailed) ReconnectFailed = true;
                if (!string.IsNullOrEmpty(item.error)) Error = item.error;
                if (string.IsNullOrEmpty(item.json)) continue;
                try
                {
                    var envelope = JsonUtility.FromJson<Envelope>(item.json);
                    if (envelope == null) continue;
                    switch (envelope.type)
                    {
                        case "welcome":
                            var welcome = JsonUtility.FromJson<Welcome>(item.json);
                            if (welcome.protocol != Protocol ||
                                (LocalId >= 0 && welcome.id != LocalId))
                            {
                                Error = "Unity room session or protocol mismatch";
                                Close();
                                return;
                            }
                            LocalId = welcome.id;
                            Code = welcome.code;
                            resumeToken = welcome.resumeToken ?? "";
                            IsHost = welcome.host || welcome.isHost;
                            IsConnected = true;
                            ReconnectFailed = false;
                            Error = "";
                            break;
                        case "role":
                            var role = JsonUtility.FromJson<Role>(item.json);
                            IsHost = IsConnected && (role.host || role.isHost);
                            break;
                        case "room":
                            LastRoom = JsonUtility.FromJson<PrototypeLanRoom>(item.json);
                            Phase = LastRoom.phase ?? Phase;
                            if (LastRoom.roster != null)
                                foreach (var member in LastRoom.roster)
                                    if (member.id == LocalId)
                                        IsHost = IsConnected && (member.host || member.isHost);
                            break;
                        case "snapshot":
                            LastSnapshot = JsonUtility.FromJson<PrototypeLanSnapshot>(item.json);
                            break;
                        case "peerInput":
                            var peer = JsonUtility.FromJson<PrototypeLanPeerInput>(item.json);
                            if (IsHost && peer.id != LocalId && PeerInputs.Count < 256)
                            {
                                if (peer.input == null)
                                    peer.input = new PrototypeLanInput {
                                        seq = peer.seq, forward = peer.forward, side = peer.side,
                                        yaw = peer.yaw, pitch = peer.pitch, sprint = peer.sprint,
                                        fire = peer.fire, callRescue = peer.callRescue,
                                        giveUp = peer.giveUp
                                    };
                                PeerInputs.Enqueue(peer);
                            }
                            break;
                        case "peerDeploy":
                            var deployment = JsonUtility.FromJson<PrototypeLanPeerDeploy>(item.json);
                            if (IsHost && deployment.id != LocalId &&
                                PeerDeployments.Count < 64)
                                PeerDeployments.Enqueue(deployment);
                            break;
                        case "peerDowned":
                            var downed = JsonUtility.FromJson<PrototypeLanPeerDowned>(item.json);
                            if (IsHost && downed.id != LocalId &&
                                PeerDownedActions.Count < 64)
                                PeerDownedActions.Enqueue(downed);
                            break;
                        case "error":
                            var failure = JsonUtility.FromJson<Failure>(item.json);
                            Error = failure.error ?? failure.message ?? failure.code ??
                                "Server rejected request";
                            break;
                    }
                }
                catch (Exception exception)
                {
                    Error = "Invalid Unity room message: " + exception.Message;
                }
            }
        }

        public void SendReady(bool ready)
        {
            SendJson(JsonUtility.ToJson(new Ready { ready = ready }));
        }

        public void SendTeam(string team, int targetId = -1)
        {
            if (team != "blue" && team != "red") return;
            if (targetId >= 0 && targetId != LocalId && !IsHost) return;
            SendJson(JsonUtility.ToJson(new Team { team = team, targetId = targetId }));
        }

        public void SendStart()
        {
            if (IsHost) SendJson("{\"type\":\"start\"}");
        }

        public void SendSettings(int tickets, int botCount)
        {
            if (!IsHost || Phase != "preparation" ||
                (tickets != 100 && tickets != 300 && tickets != 500 && tickets != 800) ||
                botCount < 0 || botCount > 32) return;
            SendJson(JsonUtility.ToJson(new Settings { tickets = tickets, botCount = botCount }));
        }

        public void SendKick(int id)
        {
            if (IsHost && id != LocalId)
                SendJson(JsonUtility.ToJson(new Kick { targetId = id }));
        }

        public void SendInput(PrototypeLanInput input)
        {
            if (!IsConnected || IsHost || Phase != "battle" || input == null) return;
            float now = Time.realtimeSinceStartup;
            if (lastInputSendAt >= 0f && now - lastInputSendAt < 0.045f) return;
            lastInputSendAt = now;
            input.type = "input";
            input.seq = ++nextSequence;
            SendJson(JsonUtility.ToJson(input));
        }

        public void SendDeploy(string location)
        {
            if (!IsConnected || IsHost || Phase != "battle" ||
                string.IsNullOrEmpty(location)) return;
            float now = Time.realtimeSinceStartup;
            if (lastDeploySendAt >= 0f && now - lastDeploySendAt < 0.2f) return;
            lastDeploySendAt = now;
            SendJson(JsonUtility.ToJson(new Deploy { location = location }));
        }

        public void SendDownedAction(string action)
        {
            if (!IsConnected || IsHost || Phase != "battle" ||
                (action != "rescue" && action != "giveUp")) return;
            float now = Time.realtimeSinceStartup;
            if (lastDownedSendAt >= 0f && now - lastDownedSendAt < 0.2f) return;
            lastDownedSendAt = now;
            SendJson(JsonUtility.ToJson(new Downed { action = action }));
        }

        public void SendSnapshot(PrototypeLanSnapshot snapshot)
        {
            if (!IsConnected || !IsHost || Phase != "battle" || snapshot == null) return;
            float now = Time.realtimeSinceStartup;
            if (lastSnapshotSendAt >= 0f && now - lastSnapshotSendAt < 0.045f) return;
            lastSnapshotSendAt = now;
            snapshot.type = "snapshot";
            SendJson(JsonUtility.ToJson(snapshot));
        }

        public void Close()
        {
            generation++;
            var oldCancellation = cancellation;
            cancellation = null;
            var oldSocket = socket;
            socket = null;
            if (oldSocket != null && oldSocket.State == WebSocketState.Open)
                _ = GracefulLeave(oldSocket, oldCancellation);
            else
            {
                oldCancellation?.Cancel();
                oldCancellation?.Dispose();
                try { oldSocket?.Abort(); } catch { }
                try { oldSocket?.Dispose(); } catch { }
            }
            while (incoming.TryDequeue(out _)) { }
            PeerInputs.Clear();
            PeerDeployments.Clear();
            PeerDownedActions.Clear();
            IsConnected = false;
            ReconnectFailed = false;
            IsHost = false;
            LocalId = -1;
            Code = "";
            Phase = "preparation";
            Error = "";
            hostToken = "";
            resumeToken = "";
            LastRoom = null;
            LastSnapshot = null;
            nextSequence = 0;
            lastInputSendAt = -1f;
            lastDeploySendAt = -1f;
            lastDownedSendAt = -1f;
            lastSnapshotSendAt = -1f;
        }

        public void Dispose() { Close(); }

        private async Task GracefulLeave(ClientWebSocket oldSocket,
            CancellationTokenSource oldCancellation)
        {
            using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1)))
            {
                bool gateHeld = false;
                try
                {
                    await sendGate.WaitAsync(timeout.Token);
                    gateHeld = true;
                    if (oldSocket.State == WebSocketState.Open)
                    {
                        byte[] leave = Encoding.UTF8.GetBytes("{\"type\":\"leave\"}");
                        await oldSocket.SendAsync(new ArraySegment<byte>(leave),
                            WebSocketMessageType.Text, true, timeout.Token);
                        await oldSocket.CloseOutputAsync((WebSocketCloseStatus)4000,
                            "leave", timeout.Token);
                    }
                }
                catch { /* A broken socket will be released by the server timeout. */ }
                finally
                {
                    if (gateHeld) sendGate.Release();
                    oldCancellation?.Cancel();
                    oldCancellation?.Dispose();
                    try { oldSocket.Abort(); } catch { }
                    oldSocket.Dispose();
                }
            }
        }

        private static Uri ParseAddress(string address)
        {
            address = (address ?? "").Trim();
            if (!address.Contains("://")) address = "http://" + address;
            if (!Uri.TryCreate(address, UriKind.Absolute, out Uri uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                throw new ArgumentException("Enter an HTTP or HTTPS room server address", nameof(address));
            return uri;
        }

        private static async Task<string> HttpJson(Uri uri, bool create, CancellationToken token)
        {
            using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) })
            using (var response = create
                ? await client.PostAsync(uri, new StringContent("{}", Encoding.UTF8, "application/json"), token)
                : await client.GetAsync(uri, token))
            {
                string body = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    string detail = body.Length > 200 ? body.Substring(0, 200) : body;
                    throw new InvalidOperationException("Room server HTTP " +
                        (int)response.StatusCode + ": " + detail);
                }
                if (body.Length > MaxMessageBytes)
                    throw new InvalidOperationException("Room response exceeds size limit");
                return body;
            }
        }

        private async Task ConnectSocket(bool resume, int current, CancellationToken token)
        {
            var uri = new UriBuilder(baseAddress) {
                Scheme = baseAddress.Scheme == "https" ? "wss" : "ws",
                Path = "/unity",
                Query = "room=" + Uri.EscapeDataString(Code) +
                    (resume ? "&resume=" + Uri.EscapeDataString(resumeToken) :
                    string.IsNullOrEmpty(hostToken) ? "" :
                    "&token=" + Uri.EscapeDataString(hostToken)) +
                    (resume ? "" : "&name=" + Uri.EscapeDataString(playerName ?? "Player"))
            }.Uri;
            var connected = new ClientWebSocket();
            try
            {
                await connected.ConnectAsync(uri, token);
                if (current != generation || token.IsCancellationRequested)
                {
                    connected.Dispose();
                    return;
                }
                socket = connected;
                _ = Task.Run(() => ReceiveAndReconnect(connected, current, token));
            }
            catch
            {
                connected.Dispose();
                throw;
            }
        }

        private async Task ReceiveAndReconnect(ClientWebSocket connected, int current,
            CancellationToken token)
        {
            while (!token.IsCancellationRequested && current == generation)
            {
                string reason = "Connection closed";
                WebSocketCloseStatus? closeStatus = null;
                try { reason = await ReceiveMessages(connected, current, token); }
                catch (OperationCanceledException) { break; }
                catch (Exception exception) { reason = exception.Message; }
                finally
                {
                    closeStatus = connected.CloseStatus;
                    connected.Dispose();
                }
                if (token.IsCancellationRequested || current != generation) break;
                Push(new Incoming { generation = current, disconnected = true, error = reason });
                if (closeStatus == WebSocketCloseStatus.PolicyViolation ||
                    closeStatus == (WebSocketCloseStatus)4000 ||
                    closeStatus == (WebSocketCloseStatus)4003 ||
                    closeStatus == WebSocketCloseStatus.EndpointUnavailable) break;
                // A resume credential exists only after the server's welcome was processed.
                if (string.IsNullOrEmpty(resumeToken)) break;
                DateTime deadline = DateTime.UtcNow.AddSeconds(30);
                bool restored = false;
                int[] delays = { 250, 500, 1000, 1500, 2500, 3500, 4500 };
                int attempt = 0;
                while (DateTime.UtcNow < deadline)
                {
                    try
                    {
                        int delay = delays[Math.Min(attempt++, delays.Length - 1)];
                        await Task.Delay(Math.Min(delay, Math.Max(1,
                            (int)(deadline - DateTime.UtcNow).TotalMilliseconds)), token);
                        var next = new ClientWebSocket();
                        try
                        {
                            var uri = new UriBuilder(baseAddress) {
                                Scheme = baseAddress.Scheme == "https" ? "wss" : "ws",
                                Path = "/unity",
                                Query = "room=" + Uri.EscapeDataString(Code) +
                                    "&resume=" + Uri.EscapeDataString(resumeToken)
                            }.Uri;
                            using (var attemptCancellation =
                                CancellationTokenSource.CreateLinkedTokenSource(token))
                            {
                                attemptCancellation.CancelAfter(TimeSpan.FromMilliseconds(
                                    Math.Min(5000, Math.Max(1,
                                        (deadline - DateTime.UtcNow).TotalMilliseconds))));
                                await next.ConnectAsync(uri, attemptCancellation.Token);
                            }
                            if (current != generation || token.IsCancellationRequested)
                            {
                                next.Dispose();
                                return;
                            }
                            socket = connected = next;
                            restored = true;
                            break;
                        }
                        catch { next.Dispose(); throw; }
                    }
                    catch (OperationCanceledException)
                    {
                        if (token.IsCancellationRequested) return;
                    }
                    catch { }
                }
                if (!restored) break;
            }
            if (!token.IsCancellationRequested && current == generation)
                Push(new Incoming { generation = current, disconnected = true,
                    reconnectFailed = true });
        }

        private async Task<string> ReceiveMessages(ClientWebSocket connected, int current,
            CancellationToken token)
        {
            var buffer = new byte[8192];
            using (var message = new MemoryStream())
            {
                while (connected.State == WebSocketState.Open && !token.IsCancellationRequested)
                {
                    message.SetLength(0);
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await connected.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                        if (result.MessageType == WebSocketMessageType.Close)
                            return connected.CloseStatusDescription ?? "Connection closed";
                        if (message.Length + result.Count > MaxMessageBytes)
                            return "Unity room message exceeds size limit";
                        message.Write(buffer, 0, result.Count);
                    } while (!result.EndOfMessage);
                    if (result.MessageType == WebSocketMessageType.Text)
                        Push(new Incoming { generation = current,
                            json = Encoding.UTF8.GetString(message.ToArray()) });
                }
            }
            return "Connection closed";
        }

        private async void SendJson(string json)
        {
            var connected = socket;
            var source = cancellation;
            int current = generation;
            if (!IsConnected || connected == null || source == null ||
                connected.State != WebSocketState.Open || json.Length > MaxMessageBytes) return;
            try
            {
                await sendGate.WaitAsync(source.Token);
                try
                {
                    if (current != generation || connected.State != WebSocketState.Open) return;
                    byte[] bytes = Encoding.UTF8.GetBytes(json);
                    if (bytes.Length > MaxMessageBytes) return;
                    await connected.SendAsync(new ArraySegment<byte>(bytes),
                        WebSocketMessageType.Text, true, source.Token);
                }
                finally { sendGate.Release(); }
            }
            catch (OperationCanceledException) { }
            catch (Exception exception)
            {
                Push(new Incoming { generation = current, error = exception.Message });
            }
        }

        [Serializable] private sealed class Envelope { public string type; }
        [Serializable] private sealed class RoomCreated
        { public string code; public string hostToken; public string protocol; }
        [Serializable] private sealed class RoomInfo { public string code; public string protocol; }
        [Serializable] private sealed class Welcome
        { public string type; public int id; public bool host; public bool isHost; public string code;
          public string protocol; public string resumeToken; public string team; }
        [Serializable] private sealed class Role { public bool host; public bool isHost; }
        [Serializable] private sealed class Failure
        { public string error; public string message; public string code; }
        [Serializable] private sealed class Ready { public string type = "ready"; public bool ready; }
        [Serializable] private sealed class Deploy
        { public string type = "deploy"; public string location; }
        [Serializable] private sealed class Downed
        { public string type = "downed"; public string action; }
        [Serializable] private sealed class Team
        { public string type = "team"; public string team; public int targetId; }
        [Serializable] private sealed class Kick { public string type = "kick"; public int targetId; }
        [Serializable] private sealed class Settings
        { public string type = "settings"; public int tickets; public int botCount; }
    }

    [Serializable]
    public sealed class PrototypeLanRoom
    {
        public string type;
        public string phase;
        public float countdown;
        public bool paused;
        public PrototypeLanRosterMember[] roster;
        public PrototypeLanSettings settings;
    }

    [Serializable]
    public sealed class PrototypeLanSettings
    {
        public int tickets;
        public int botCount;
    }

    [Serializable]
    public sealed class PrototypeLanRosterMember
    {
        public int id;
        public string name;
        public bool host;
        public bool isHost;
        public bool ready;
        public bool connected;
        public string team;
    }

    [Serializable]
    public sealed class PrototypeLanInput
    {
        public string type = "input";
        public int seq;
        public float forward;
        public float side;
        public float yaw;
        public float pitch;
        public bool sprint;
        public bool fire;
        public bool callRescue;
        public bool giveUp;
    }

    [Serializable]
    public sealed class PrototypeLanPeerInput
    {
        public string type;
        public int id;
        public PrototypeLanInput input;
        public int seq;
        public float forward;
        public float side;
        public float yaw;
        public float pitch;
        public bool sprint;
        public bool fire;
        public bool callRescue;
        public bool giveUp;
    }

    [Serializable]
    public sealed class PrototypeLanPeerDeploy
    {
        public string type;
        public int id;
        public string location;
    }

    [Serializable]
    public sealed class PrototypeLanPeerDowned
    {
        public string type;
        public int id;
        public string action;
    }

    [Serializable]
    public sealed class PrototypeLanSnapshot
    {
        public string type = "snapshot";
        public int tick;
        public PrototypeLanActor[] players;
        public PrototypeLanActor[] bots;
        public PrototypeLanActor[] vehicles;
        public PrototypeLanPoint[] points;
        public float blueTickets;
        public float redTickets;
    }

    [Serializable]
    public sealed class PrototypeLanActor
    {
        public int id;
        public float x;
        public float y;
        public float z;
        public float yaw;
        public float hp;
        public bool alive;
        public string team;
        public int ammo;
        public int reserve;
        public float respawnRemaining;
        public float downedRemaining;
        public bool rescueCalled;
    }

    [Serializable]
    public sealed class PrototypeLanPoint
    {
        public string id;
        public float control;
        public string owner;
    }
}
