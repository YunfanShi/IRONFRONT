// Unity rooms intentionally have their own protocol and never enter the browser Battle.
const crypto = require('node:crypto');
const { WebSocketServer, WebSocket } = require('ws');

const PROTOCOL = 'unity-1';
const MAX_PLAYERS = 8;
const RESUME_MS = 30000;
const ROOM_IDLE_MS = 600000;
const SEND_BUFFER_LIMIT = 1024 * 1024;
const allowedTickets = new Set([100,300,500,800]);
const finite = value => typeof value === 'number' && Number.isFinite(value);
const clamp = (value, low, high) => Math.max(low, Math.min(high, value));

function createUnityRooms(server) {
    const rooms = new Map();
    const wss = new WebSocketServer({ noServer: true, maxPayload: 65536 });

    function send(ws, message) {
        if (ws.readyState === WebSocket.OPEN && ws.bufferedAmount < SEND_BUFFER_LIMIT)
            ws.send(JSON.stringify(message));
    }
    function members(room) {
        return [...room.clients.values(), ...room.reservations.values()];
    }
    function roster(room) {
        return members(room).map(c => ({ id:c.id, host:c.host, ready:c.ready, team:c.team, connected:room.clients.has(c.ws) }));
    }
    function state(room) {
        return { type:'room', code:room.code, protocol:PROTOCOL, phase:room.phase,
            paused:room.paused, countdown:room.phase === 'countdown' && !room.paused ? Math.max(0, (room.countdownAt-Date.now())/1000) : 0,
            roster:roster(room), settings:room.settings };
    }
    function broadcast(room, message) {
        for (const ws of room.clients.keys()) send(ws, message);
    }
    function broadcastState(room) { broadcast(room, state(room)); }
    function closeRoom(room, reason) {
        rooms.delete(room.code);
        room.closed=true;
        for (const [ws,c] of room.clients) { c.leaving=true; ws.close(1001,reason); }
    }
    function json(res, status, value) {
        res.writeHead(status, { 'Content-Type':'application/json; charset=utf-8' });
        res.end(JSON.stringify(value));
    }
    function handleHttp(req, res) {
        const url = new URL(req.url, 'http://localhost');
        const match = /^\/api\/unity\/rooms(?:\/([A-Fa-f0-9]{6}))?\/?$/.exec(url.pathname);
        if (!match) return false;
        if (!match[1] && req.method === 'POST') {
            let size = 0, body = '';
            req.on('data', chunk => {
                size += chunk.length;
                if (size > 8192) { json(res, 413, { error:'Configuration too large' }); req.destroy(); }
                else body += chunk;
            });
            req.on('end', () => {
                if (res.writableEnded) return;
                let options;
                try { options = body ? JSON.parse(body) : {}; }
                catch { return json(res, 400, { error:'Invalid configuration' }); }
                if (!options || typeof options !== 'object' || Array.isArray(options))
                    return json(res, 400, { error:'Invalid configuration' });
                if (options.tickets !== undefined && !allowedTickets.has(options.tickets) ||
                    options.botCount !== undefined && options.botCount !== 8)
                    return json(res, 400, { error:'Unsupported Unity match settings' });
                if (rooms.size >= 12) return json(res, 503, { error:'Room limit' });
                let code;
                do { code = crypto.randomBytes(3).toString('hex').toUpperCase(); } while (rooms.has(code));
                const room = { code, hostToken:crypto.randomBytes(24).toString('hex'), hostEverJoined:false,
                    phase:'preparation', paused:false, countdownAt:0, lastTick:-1, lastSnapshotAt:0, clients:new Map(), reservations:new Map(),
                    settings:{ tickets:options.tickets ?? 100, botCount:8 },
                    created:Date.now(), emptyAt:0 };
                rooms.set(code, room);
                return json(res, 200, { code, hostToken:room.hostToken, protocol:PROTOCOL, maxPlayers:MAX_PLAYERS });
            });
            return true;
        }
        if (match[1] && req.method === 'GET') {
            const room = rooms.get(match[1].toUpperCase());
            if (!room) return json(res, 404, { error:'Room not found' }), true;
            if (room.phase === 'countdown') return json(res, 409, { error:'Room is counting down' }), true;
            return json(res, 200, { code:room.code, protocol:PROTOCOL, phase:room.phase,
                paused:room.paused, players:room.clients.size, maxPlayers:MAX_PLAYERS,
                roster:roster(room), settings:room.settings }), true;
        }
        json(res, 405, { error:'Method not allowed' });
        return true;
    }
    function handleUpgrade(req, socket, head) {
        const url = new URL(req.url, 'http://localhost');
        if (url.pathname !== '/unity') return false;
        wss.handleUpgrade(req, socket, head, ws => wss.emit('connection', ws, req));
        return true;
    }
    function reject(ws, reason) { ws.close(1008, reason); }
    function hostOnly(c, ws) {
        if (c.host) return true;
        send(ws, { type:'error', code:'host_only' });
        return false;
    }
    function onMessage(room, c, ws, data) {
        const now = Date.now();
        if (now-c.windowAt >= 1000) { c.windowAt=now; c.messages=0; }
        if (++c.messages > 90) return ws.close(1008, 'Message rate exceeded');
        let m;
        try { m=JSON.parse(data); } catch { return; }
        if (!m || typeof m !== 'object' || Array.isArray(m)) return;
        switch (m.type) {
            case 'leave':
                c.leaving=true;
                if (c.host) closeRoom(room,'Host left');
                else ws.close(4000,'Left room');
                return;
            case 'ready':
                if (room.phase !== 'preparation' || room.paused || typeof m.ready !== 'boolean') return;
                c.ready=m.ready; broadcastState(room); return;
            case 'team': {
                if (room.phase !== 'preparation' || room.paused || !['blue','red'].includes(m.team)) return;
                const targetId=m.targetId===undefined || m.targetId===-1 ? c.id : m.targetId;
                if (targetId !== c.id && !hostOnly(c, ws)) return;
                const target=members(room).find(o=>o.id===targetId);
                if (!target) return;
                target.team=m.team; target.ready=false; broadcastState(room); return;
            }
            case 'settings':
                if (!hostOnly(c, ws) || room.phase !== 'preparation' || room.paused) return;
                if (m.tickets !== undefined && !allowedTickets.has(m.tickets) || m.botCount !== undefined && m.botCount !== 8) {
                    send(ws,{type:'error',code:'unsupported_settings'}); return;
                }
                if (m.tickets !== undefined && m.tickets !== room.settings.tickets) {
                    room.settings.tickets=m.tickets;
                    for (const member of members(room)) member.ready=false;
                }
                broadcastState(room); return;
            case 'start':
                if (!hostOnly(c, ws) || room.phase !== 'preparation' || room.paused) return;
                if (members(room).length === 0 || members(room).some(member=>!member.ready || !room.clients.has(member.ws))) {
                    send(ws, { type:'error', code:'not_ready' }); return;
                }
                room.phase='countdown'; room.countdownAt=now+3000; broadcastState(room); return;
            case 'kick': {
                if (!hostOnly(c, ws) || !Number.isInteger(m.targetId) || m.targetId===c.id) return;
                const target=members(room).find(member=>member.id===m.targetId);
                if (!target) return;
                target.kicked=true;
                room.reservations.delete(target.resumeToken);
                if (target.ws && room.clients.has(target.ws)) {
                    room.clients.delete(target.ws);
                    target.ws.close(4003, 'Kicked by host');
                }
                if (room.phase === 'countdown') { room.phase='preparation'; room.countdownAt=0; }
                broadcastState(room); return;
            }
            case 'input': {
                if (room.phase !== 'battle' || room.paused || c.host || !Number.isSafeInteger(m.seq) || m.seq <= c.lastSeq) return;
                // The Unity host applies each accepted input for 0.05 s. At most 20/s
                // prevents a guest from accelerating its simulation with packet spam.
                if (now-c.lastInputAt < 50 || !finite(m.forward) || !finite(m.side) || !finite(m.yaw) || !finite(m.pitch)) return;
                c.lastSeq=m.seq; c.lastInputAt=now;
                const host=[...room.clients.values()].find(member=>member.host);
                if (host) send(host.ws, { type:'peerInput', id:c.id, input:{ seq:c.lastSeq,
                    forward:clamp(m.forward,-1,1), side:clamp(m.side,-1,1), yaw:clamp(m.yaw,-3600,3600),
                    pitch:clamp(m.pitch,-89,89), sprint:m.sprint===true,
                    fire:m.fire===true } });
                return;
            }
            case 'snapshot': {
                if (!hostOnly(c, ws) || room.phase !== 'battle' || room.paused) return;
                if (now-room.lastSnapshotAt < 20 || !Number.isSafeInteger(m.tick) || m.tick<=room.lastTick ||
                    !Array.isArray(m.players) || m.players.length>MAX_PLAYERS ||
                    !Array.isArray(m.bots) || m.bots.length>128 ||
                    !Array.isArray(m.vehicles) || m.vehicles.length>64 ||
                    !Array.isArray(m.points) || m.points.length>64 ||
                    !finite(m.blueTickets) || !finite(m.redTickets)) return;
                room.lastSnapshotAt=now; room.lastTick=m.tick;
                const snapshot={type:'snapshot',tick:m.tick,players:m.players,bots:m.bots,vehicles:m.vehicles,
                    points:m.points,blueTickets:clamp(m.blueTickets,0,100000),redTickets:clamp(m.redTickets,0,100000)};
                for (const [peerWs, peer] of room.clients) if (!peer.host) send(peerWs,snapshot);
                return;
            }
            default: return; // In particular, client supplied roles, room state, and peerInput are ignored.
        }
    }
    wss.on('connection', (ws, req) => {
        const url=new URL(req.url,'http://localhost');
        const room=rooms.get((url.searchParams.get('room')||'').toUpperCase());
        if (!room) return reject(ws,'Room unavailable');
        const resume=url.searchParams.get('resume')||'';
        const token=url.searchParams.get('token')||'';
        let c=resume ? room.reservations.get(resume) : null;
        if (resume && (!c || c.expires <= Date.now())) return reject(ws,'Session expired');
        const claimHost=!!token && !room.hostEverJoined && token===room.hostToken;
        if (token && resume || token && !claimHost || !c && (room.phase==='countdown' ||
            members(room).length >= (room.hostEverJoined || claimHost ? MAX_PLAYERS : MAX_PLAYERS-1)))
            return reject(ws,'Room unavailable or full');
        if (c) { room.reservations.delete(resume); c.ws=ws; c.expires=0; }
        else {
            const players=members(room);
            const id=Array.from({length:MAX_PLAYERS},(_,index)=>index+1).find(candidate=>!players.some(p=>p.id===candidate));
            c={ id, host:claimHost, team:claimHost?'blue':players.filter(p=>p.team==='blue').length<=players.filter(p=>p.team==='red').length?'blue':'red',
                ready:false, ws, resumeToken:crypto.randomBytes(24).toString('hex'), lastSeq:-1,
                lastInputAt:0, lastSnapshotAt:0, windowAt:Date.now(), messages:0, kicked:false };
        }
        if (claimHost) { room.hostEverJoined=true; room.hostToken=null; }
        room.clients.set(ws,c);
        room.emptyAt=0;
        if (c.host) room.paused=false;
        send(ws,{type:'welcome',id:c.id,host:c.host,team:c.team,code:room.code,protocol:PROTOCOL,
            resumeToken:c.resumeToken,resumed:!!resume});
        broadcastState(room);
        ws.on('message', data=>onMessage(room,c,ws,data));
        ws.on('close',code=>{
            if (!room.clients.has(ws)) return;
            room.clients.delete(ws);
            if (room.closed) return;
            if (c.host && (c.leaving || code===4000)) { closeRoom(room,'Host left'); return; }
            if (!c.kicked && !c.leaving && code!==4000) { c.expires=Date.now()+RESUME_MS; room.reservations.set(c.resumeToken,c); }
            if (c.host) room.paused=true;
            if (room.phase==='countdown') { room.phase='preparation'; room.countdownAt=0; }
            if (!room.clients.size) room.emptyAt=Date.now();
            broadcastState(room);
        });
    });
    setInterval(()=>{
        const now=Date.now();
        for (const [code,room] of rooms) {
            let changed=false,hostExpired=false;
            for (const [token,c] of room.reservations) if (c.expires<=now) {
                room.reservations.delete(token); changed=true; if (c.host) hostExpired=true;
            }
            if (hostExpired) {
                for (const ws of room.clients.keys()) ws.close(1011,'Host session expired');
                rooms.delete(code); continue;
            }
            if (!room.paused && room.phase==='countdown' && now>=room.countdownAt) {
                room.phase='battle'; room.countdownAt=0; changed=true;
            }
            if (!room.clients.size && now-(room.emptyAt||room.created)>ROOM_IDLE_MS) {
                rooms.delete(code); continue;
            }
            if (changed) broadcastState(room);
        }
    },100).unref();
    return { handleHttp, handleUpgrade };
}

module.exports={ createUnityRooms };
