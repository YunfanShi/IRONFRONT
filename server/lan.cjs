// One authoritative Battle per co-op room. Clients send controls, never health/positions.
const http = require('node:http'), fs = require('node:fs'), path = require('node:path'), crypto = require('node:crypto');
const { WebSocketServer, WebSocket } = require('ws');
const { Battle } = require('../.logic-build/core/Battle.js');
const port = Number(process.env.PORT) || 7878, rooms = new Map(), dist = path.resolve(__dirname, '../dist');
const server = http.createServer((req, res) => {
    res.setHeader('Access-Control-Allow-Origin', '*');
    res.setHeader('Access-Control-Allow-Headers', 'Content-Type');
    if (req.method === 'OPTIONS') {
        res.writeHead(204);
        return res.end();
    }
    if (req.url === '/api/rooms' && req.method === 'POST') {
        let body = '';
        req.on('data', chunk => { body += chunk; if (body.length > 8192) {
            res.writeHead(413);
            res.end('Configuration too large');
            req.destroy();
        } });
        req.on('end', () => {
            if (res.writableEnded)
                return;
            let options;
            try {
                options = body ? JSON.parse(body) : {};
            }
            catch {
                res.writeHead(400);
                return res.end('Invalid configuration');
            }
            if (rooms.size >= 12) {
                res.writeHead(503);
                return res.end('Room limit');
            }
            const config = { size: [8, 16, 32, 64].includes(options.size) ? options.size : 32, difficulty: ['easy', 'normal', 'hard'].includes(options.difficulty) ? options.difficulty : 'normal', tickets: [100, 300, 500, 800].includes(options.tickets) ? options.tickets : 500, mode: options.mode === 'breakthrough' ? 'breakthrough' : 'conquest', killTicketPenalty: [0, 1, 2].includes(options.killTicketPenalty) ? options.killTicketPenalty : 1, aiEnabled: options.aiEnabled !== false, seed: 505 };
            const code = crypto.randomBytes(3).toString('hex').toUpperCase(), hostToken = crypto.randomBytes(24).toString('hex'), battle = new Battle(config), room = { code, hostToken, battle, clients: new Map(), created: Date.now(), joinTeam: options.joinTeam === 'red' ? 'red' : options.joinTeam === 'alternate' ? 'alternate' : 'blue' };
            if (!config.aiEnabled)
                for (const s of battle.soldiers) {
                    s.alive = false;
                    s.player = false;
                    s.respawnAt = Infinity;
                }
            battle.onAward = (id, amount) => { const client = [...room.clients.values()].find(c => c.id === id); if (client) {
                client.state.requisitionPoints = Math.min(9999, client.state.requisitionPoints + amount);
                if (battle.controlledPlayerId === id)
                    battle.requisitionPoints = client.state.requisitionPoints;
            } };
            rooms.set(code, room);
            res.setHeader('Content-Type', 'application/json');
            res.end(JSON.stringify({ code, hostToken, maxPlayers: 8, mode: config.mode, port }));
        });
        return;
    }
    if (req.url === '/api/status') {
        res.setHeader('Content-Type', 'application/json');
        return res.end(JSON.stringify({ version: '0.10.1', rooms: [...rooms.values()].map(r => ({ code: r.code, players: r.clients.size })) }));
    }
    let pathname;
    try {
        pathname = decodeURIComponent(new URL(req.url, 'http://localhost').pathname);
    }
    catch {
        res.writeHead(400);
        return res.end('Invalid path');
    }
    let file = path.resolve(dist, '.' + (pathname === '/' ? '/index.html' : pathname));
    if (!file.startsWith(dist + path.sep)) {
        res.writeHead(403);
        return res.end();
    }
    if (!fs.existsSync(file) || !fs.statSync(file).isFile()) {
        res.writeHead(404);
        return res.end('Build first: npm run lan');
    }
    const ext = path.extname(file);
    res.setHeader('Content-Type', ({ '.html': 'text/html', '.js': 'text/javascript', '.css': 'text/css', '.mp3': 'audio/mpeg', '.ogg': 'audio/ogg', '.json': 'application/json' })[ext] || 'application/octet-stream');
    fs.createReadStream(file).pipe(res);
});
const wss = new WebSocketServer({ server, path: '/play', maxPayload: 8192 });
const withClient = (r, c, fn) => { const b = r.battle; b.controlledPlayerId = c.id; b.importPlayerState(c.state); fn(b); c.state = b.exportPlayerState(); };
wss.on('connection', (ws, req) => {
    const url = new URL(req.url, 'http://localhost'), room = rooms.get((url.searchParams.get('room') || '').toUpperCase());
    if (!room || room.clients.size >= 8) {
        ws.close(1008, 'Room unavailable or full');
        return;
    }
    const isHost = url.searchParams.get('token') === room.hostToken, b = room.battle, team = isHost ? 'blue' : room.joinTeam === 'alternate' ? (room.clients.size % 2 ? 'red' : 'blue') : room.joinTeam, s = b.soldiers.find(s => s.team === team && ![...room.clients.values()].some(c => c.id === s.id));
    if (!s) {
        ws.close(1008, 'Faction is full');
        return;
    }
    const id = s.id;
    if (s.vehicleId !== null) {
        const v = b.vehicles.find(v => v.id === s.vehicleId);
        if (v) {
            v.occupants[v.occupants.indexOf(id)] = null;
            v.speed = 0;
        }
    }
    s.vehicleId = null;
    s.player = true;
    s.squad = Math.floor(b.soldiers.filter(o => o.team === team && o.id < s.id).length / 4);
    s.alive = true;
    s.hp = 100;
    s.pos = { ...require('../.logic-build/world/Layout.js').BASES[team] };
    s.spawnGraceUntil = b.elapsed + 1.2;
    const previous = b.exportPlayerState(), old = b.controlledPlayerId;
    b.controlledPlayerId = id;
    b.importPlayerState(new Battle({ size: 8, difficulty: 'normal', tickets: 500 }).exportPlayerState());
    const c = { id, isHost, state: b.exportPlayerState(), input: {}, lastInput: 0, window: Date.now(), messages: 0 };
    b.controlledPlayerId = old;
    b.importPlayerState(previous);
    room.clients.set(ws, c);
    ws.send(JSON.stringify({ type: 'welcome', id, code: room.code, settings: b.settings, isHost }));
    ws.on('message', data => {
        const now = Date.now();
        if (now - c.window > 1000) {
            c.window = now;
            c.messages = 0;
        }
        if (++c.messages > 80) {
            ws.close(1008, 'Input rate exceeded');
            return;
        }
        let m;
        try {
            m = JSON.parse(data);
        }
        catch {
            return;
        }
        if (!m || typeof m !== 'object')
            return;
        const finite = v => typeof v === 'number' && Number.isFinite(v);
        if (m.type === 'input') {
            if (!finite(m.yaw) || !finite(m.pitch))
                return;
            c.input = { yaw: Math.max(-1000, Math.min(1000, m.yaw)), pitch: Math.max(-1.47, Math.min(1.47, m.pitch)), forward: Math.sign(m.forward) || 0, side: Math.sign(m.side) || 0, up: Math.sign(m.up) || 0, fire: m.fire === true, ads: m.ads === true, sprint: m.sprint === true, crouch: m.crouch === true };
            c.lastInput = now;
            return;
        }
        if (m.type === 'action')
            withClient(room, c, b => { const p = c.input, d = { x: -Math.sin(p.yaw || 0) * Math.cos(p.pitch || 0), y: Math.sin(p.pitch || 0), z: -Math.cos(p.yaw || 0) * Math.cos(p.pitch || 0) }; switch (m.action) {
                case 'team':
                    if (c.isHost && ['blue', 'red'].includes(m.team)) {
                        const target = [...room.clients.values()].find(o => o.id === m.value);
                        if (target) {
                            const soldier = b.soldiers[target.id];
                            if (soldier.vehicleId !== null) {
                                const v = b.vehicles.find(v => v.id === soldier.vehicleId);
                                if (v) {
                                    v.occupants = v.occupants.map(id => id === soldier.id ? null : id);
                                    if (v.occupants[0] === null) {
                                        v.driver = null;
                                        v.speed = 0;
                                    }
                                }
                                soldier.vehicleId = null;
                                target.state.playerVehicleId = null;
                                target.state.playerSeat = 0;
                            }
                            soldier.team = m.team;
                            let squad = 0;
                            while (b.soldiers.filter(o => o !== soldier && o.team === m.team && o.squad === squad && o.player).length >= 4)
                                squad++;
                            soldier.squad = squad;
                            const mates = b.soldiers.filter(o => o !== soldier && o.team === m.team && o.squad === squad && !o.player);
                            for (const ai of mates.slice(Math.max(0, 3 - b.soldiers.filter(o => o !== soldier && o.team === m.team && o.squad === squad && o.player).length)))
                                ai.squad = Math.floor(b.soldiers.length / 4) + ai.id;
                            soldier.pos = { ...require('../.logic-build/world/Layout.js').BASES[m.team] };
                            soldier.combatUntil = 0;
                        }
                    }
                    break;
                case 'enter':
                    b.togglePlayerVehicle();
                    break;
                case 'seat':
                    b.switchVehicleSeat(m.value);
                    break;
                case 'reload':
                    b.startReload();
                    break;
                case 'weapon':
                    b.switchPlayerWeapon(m.value);
                    break;
                case 'grenade':
                    b.throwGrenade(d);
                    break;
                case 'gadget':
                    b.useGadget();
                    break;
                case 'rocket':
                    b.fireRocket(d);
                    break;
                case 'aa':
                    b.fireAA(d);
                    break;
                case 'unstuck':
                    b.unstuckPlayer();
                    break;
                case 'support':
                    b.requestSupport(m.value, m.at);
                    break;
                case 'respawn':
                    b.setLoadout(m.loadout);
                    b.respawnPlayer(m.value);
                    break;
                case 'loadout':
                    if (!c.loadoutSet) {
                        const elapsed = b.elapsed;
                        b.elapsed = 0;
                        b.setLoadout(m.loadout);
                        b.elapsed = elapsed;
                        c.loadoutSet = true;
                    }
                    break;
            } });
    });
    ws.on('close', () => { room.clients.delete(ws); s.player = false; if (b.settings.aiEnabled === false) {
        s.alive = false;
        s.respawnAt = Infinity;
        if (s.vehicleId !== null) {
            const vehicle = b.vehicles.find(v => v.id === s.vehicleId);
            if (vehicle) {
                vehicle.occupants = vehicle.occupants.map(id => id === s.id ? null : id);
                if (vehicle.occupants[0] === null) {
                    vehicle.driver = null;
                    vehicle.speed = 0;
                }
            }
            s.vehicleId = null;
        }
    } s.boardingUntil = b.elapsed + 5; s.nextAiAt = b.elapsed + .2; if (s.vehicleId !== null) {
        const v = b.vehicles.find(v => v.id === s.vehicleId);
        if (v)
            v.driver = 'ai';
    } if (!room.clients.size)
        room.emptyAt = Date.now(); });
});
setInterval(() => {
    for (const [code, r] of rooms) {
        if (!r.clients.size) {
            if (Date.now() - (r.emptyAt || r.created) > 600000)
                rooms.delete(code);
            continue;
        }
        const b = r.battle;
        for (const c of r.clients.values())
            withClient(r, c, b => { if (Date.now() - c.lastInput > 250 || !b.player.alive || b.finished)
                return; const i = c.input, y = i.yaw, p = i.pitch, d = { x: -Math.sin(y) * Math.cos(p), y: Math.sin(p), z: -Math.cos(y) * Math.cos(p) }; b.player.yaw = y + Math.PI; b.playerAiming = i.ads; if (b.inVehicle) {
                b.drivePlayerVehicle(i.forward, i.side, .05, y);
                b.aimPlayerVehicle(y, p, .05);
                b.changeAltitude(i.up, .05);
                if (i.fire)
                    b.shootPlayerVehicle(d);
            }
            else {
                const len = Math.hypot(i.forward, i.side) || 1, speed = i.crouch ? 3.8 : i.sprint && !i.ads ? 13.7 : i.ads ? 5.8 : 8.6;
                b.movePlayer((Math.cos(y) * i.side - Math.sin(y) * i.forward) / len * speed * .05, (-Math.sin(y) * i.side - Math.cos(y) * i.forward) / len * speed * .05);
                if (i.fire && (b.activeWeapon.automatic || !c.fireHeld))
                    b.shootPlayer(d);
                c.fireHeld = i.fire;
            } });
        const first = [...r.clients.values()][0];
        withClient(r, first, b => b.tick(.05));
        const events = b.events();
        for (const event of events)
            if (event.type === 'capture' && event.owner !== null)
                for (const c of r.clients.values())
                    if (c !== first && b.soldiers[c.id].team === event.owner && b.soldiers[c.id].alive && Math.hypot(b.soldiers[c.id].pos.x - b.points.find(p => p.id === event.id).x, b.soldiers[c.id].pos.z - b.points.find(p => p.id === event.id).z) < 25)
                        c.state.requisitionPoints = Math.min(9999, c.state.requisitionPoints + 120);
        for (const [ws, c] of r.clients) {
            withClient(r, c, b => b.advancePlayerTimers());
            const filtered = events.filter(e => (e.type !== 'rp' || e.owner === c.id) && (e.type !== 'playerHit' || e.victim === c.id)).map(e => (e.type === 'shot' || e.type === 'vehicleShot') ? { ...e, player: e.owner === c.id } : e);
            const state = { type: 'state', elapsed: b.elapsed, finished: b.finished, winner: b.winner, sectorIndex: b.sectorIndex, tickets: b.tickets, soldiers: b.soldiers, vehicles: b.vehicles, points: b.points, supports: b.supports, projectiles: b.projectiles, player: c.state, events: filtered };
            if (ws.readyState === WebSocket.OPEN && ws.bufferedAmount < 1024 * 1024)
                ws.send(JSON.stringify(state));
        }
    }
}, 50).unref();
server.listen(port, '0.0.0.0', () => console.log(`IRONFRONT LAN http://0.0.0.0:${port} · configurable faction rooms, 8 players max`));
