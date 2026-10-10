// One authoritative Battle per co-op room. Clients send controls, never health/positions.
const http = require('node:http'), fs = require('node:fs'), path = require('node:path'), crypto = require('node:crypto'), os = require('node:os');
const { WebSocketServer, WebSocket } = require('ws');
const {heightAt}=require('../.logic-build/core/math.js');
const { Battle } = require('../.logic-build/core/Battle.js');
const port = Number(process.env.PORT) || 7878, rooms = new Map(), dist = path.resolve(__dirname, '../dist');
const addresses=()=>{const nets=Object.entries(os.networkInterfaces()).filter(([name])=>! /^(utun|tun|docker|veth|br-|bridge|tailscale|vmnet)/i.test(name)).flatMap(([,list])=>list||[]);return [...new Set(nets.filter(n=>n.family==='IPv4'&&!n.internal).map(n=>`http://${n.address}:${port}`))];};
const server = http.createServer((req, res) => {
    res.setHeader('Access-Control-Allow-Origin', '*');
    res.setHeader('Access-Control-Allow-Headers', 'Content-Type');
    res.setHeader('Access-Control-Allow-Methods','GET, POST, OPTIONS');
    res.setHeader('Access-Control-Allow-Private-Network','true');
    res.setHeader('Cache-Control','no-store');
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
            const config = { size: [8, 16, 32, 64].includes(options.size) ? options.size : 32, difficulty: ['easy', 'normal', 'hard'].includes(options.difficulty) ? options.difficulty : 'easy', aiProfile: options.aiProfile === 'elite' ? 'elite' : 'regular', tickets: [100, 300, 500, 800].includes(options.tickets) ? options.tickets : 500, mode: options.mode === 'breakthrough' ? 'breakthrough' : 'conquest', killTicketPenalty: [0, 1, 2].includes(options.killTicketPenalty) ? options.killTicketPenalty : 1, aiEnabled: options.aiEnabled !== false, seed: 505 };
            let code;do{code=crypto.randomBytes(3).toString('hex').toUpperCase();}while(rooms.has(code));
            const hostToken = crypto.randomBytes(24).toString('hex'), battle = new Battle(config), room = { phase:'preparation',countdownAt:0,code, hostToken, hostEverJoined:false, battle, clients: new Map(), reservations:new Map(), created: Date.now(), joinTeam: options.joinTeam === 'red' ? 'red' : options.joinTeam === 'alternate' ? 'alternate' : 'blue' };
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
    if(req.url.startsWith('/api/rooms/')){const room=rooms.get(req.url.slice('/api/rooms/'.length).toUpperCase());res.setHeader('Content-Type','application/json');if(!room){res.writeHead(404);return res.end(JSON.stringify({error:'房间码不存在，请确认加入的是同一台房主服务器'}));}if(room.phase==='countdown'){res.writeHead(409);return res.end(JSON.stringify({error:'房间正在倒计时，请等待战斗开始后加入'}));}if(room.clients.size+room.reservations.size>=8){res.writeHead(409);return res.end(JSON.stringify({error:'房间已满（最多八人，含短暂掉线的保留席位）'}));}return res.end(JSON.stringify({code:room.code,players:room.clients.size,maxPlayers:8,phase:room.phase,port}));}
    if (req.url === '/api/status') {
        res.setHeader('Content-Type', 'application/json');
        return res.end(JSON.stringify({ version: '0.24.0', port, addresses: addresses(), rooms: [...rooms.values()].map(r => ({ players: r.clients.size, phase:r.phase })) }));
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
const ensureHost = room => {if(!room.hostEverJoined||[...room.clients.values()].some(c=>c.isHost)||[...room.reservations.values()].some(c=>c.isHost))return;const next=room.clients.entries().next().value;if(next){const [ws,c]=next;c.isHost=true;if(ws.readyState===WebSocket.OPEN)ws.send(JSON.stringify({type:'role',isHost:true}));}};
wss.on('connection', (ws, req) => {
    const url = new URL(req.url, 'http://localhost'), room = rooms.get((url.searchParams.get('room') || '').toUpperCase());
    const resumeToken=url.searchParams.get('resume')||'';
    let reservation=room?.reservations.get(resumeToken);
    if(reservation&&reservation.expires<=Date.now()){room.reservations.delete(resumeToken);reservation=null;}
    if (!room || resumeToken&&!reservation || room.phase==='countdown'&&!reservation || room.clients.size >= 8 || !reservation&&room.clients.size+room.reservations.size>=8) {
        ws.close(1008, resumeToken&&!reservation?'Session expired':'Room unavailable or full');
        return;
    }
    if(reservation)room.reservations.delete(resumeToken);
    const isHost = reservation?reservation.isHost:!!room.hostToken&&url.searchParams.get('token') === room.hostToken, b = room.battle, team = reservation?b.soldiers[reservation.id].team:isHost ? 'blue' : room.joinTeam === 'alternate' ? (room.clients.size % 2 ? 'red' : 'blue') : room.joinTeam, s = reservation?b.soldiers[reservation.id]:b.soldiers.find(s => s.team === team && ![...room.clients.values()].some(c => c.id === s.id)&&![...room.reservations.values()].some(c=>c.id===s.id));
    if (!s) {
        ws.close(1008, 'Faction is full');
        return;
    }
    const id = s.id;
    if(isHost&&!reservation){room.hostEverJoined=true;room.hostToken=null;}
    if (!reservation && s.vehicleId !== null) {
        const v = b.vehicles.find(v => v.id === s.vehicleId);
        if (v) {
            v.occupants[v.occupants.indexOf(id)] = null;
            v.speed = 0;
        }
    }
    s.vehicleId = null;
    s.player = true;
    if(!reservation){s.squad = Math.floor(b.soldiers.filter(o => o.team === team && o.id < s.id).length / 4);s.alive = room.phase!=='battle';s.hp = 100;s.pos = { ...require('../.logic-build/world/Layout.js').BASES[team] };s.spawnGraceUntil = b.elapsed + 3600;}
    else if(b.settings.aiEnabled===false){s.alive=reservation.alive;s.hp=reservation.hp;}
    const previous = b.exportPlayerState(), old = b.controlledPlayerId;
    b.controlledPlayerId = id;
    b.importPlayerState(reservation?reservation.state:new Battle({ size: 8, difficulty: 'normal', tickets: 500 }).exportPlayerState());
    const c = { ready:reservation?.ready??false,id, isHost, resumeToken:reservation?.resumeToken??crypto.randomBytes(24).toString('hex'), state: b.exportPlayerState(), input: {}, lastInput: 0, window: Date.now(), messages: 0 };
    if(reservation){const vehicle=b.vehicles.find(v=>v.id===s.vehicleId&&v.alive&&v.occupants.includes(id));if(!vehicle){s.vehicleId=null;c.state.playerVehicleId=null;c.state.playerSeat=0;}else if(vehicle.occupants[0]===id)vehicle.driver='player';}
    b.controlledPlayerId = old;b.importPlayerState(previous);
    room.clients.set(ws, c);
    ensureHost(room);
    ws.send(JSON.stringify({ type: 'welcome', id, code: room.code, settings: b.settings, isHost:c.isHost, resumeToken:c.resumeToken, resumed:!!reservation }));
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
        if(m.type==='action'&&room.phase!=='battle'&&!['unready','ready','begin','loadout','team'].includes(m.action))return;
        const finite = v => typeof v === 'number' && Number.isFinite(v);
        if (m.type === 'input') {
            if (!finite(m.yaw) || !finite(m.pitch))
                return;
            const muzzleOffset=m.muzzleOffset&&['x','y','z'].every(k=>finite(m.muzzleOffset[k]))&&Math.hypot(m.muzzleOffset.x,m.muzzleOffset.y-1.78,m.muzzleOffset.z)<3?m.muzzleOffset:null;
            c.input = { equipment:m.equipment==='aa'?'aa':null,muzzleOffset,yaw: Math.max(-1000, Math.min(1000, m.yaw)), pitch: Math.max(-1.47, Math.min(1.47, m.pitch)), forward: Math.sign(m.forward) || 0, side: Math.sign(m.side) || 0, up: Math.sign(m.up) || 0, fire: m.fire === true, ads: m.ads === true, sprint: m.sprint === true, boost:m.boost===true,airbrake:m.airbrake===true, giveUp:m.giveUp===true,crouch: m.crouch === true };
            c.lastInput = now;
            return;
        }
        if (m.type === 'action')
            withClient(room, c, b => { const p = c.input, d = { x: -Math.sin(p.yaw || 0) * Math.cos(p.pitch || 0), y: Math.sin(p.pitch || 0), z: -Math.cos(p.yaw || 0) * Math.cos(p.pitch || 0) }; const offset=m.muzzleOffset;const muzzle=offset&&['x','y','z'].every(k=>typeof offset[k]==='number'&&Number.isFinite(offset[k]))&&Math.hypot(offset.x,offset.y-1.65,offset.z)<3?{x:b.player.pos.x+offset.x,y:heightAt(b.player.pos.x,b.player.pos.z)+offset.y,z:b.player.pos.z+offset.z}:undefined;const direction=m.direction;if(direction&&['x','y','z'].every(k=>typeof direction[k]==='number'&&Number.isFinite(direction[k]))){const len=Math.hypot(direction.x,direction.y,direction.z);if(len>.01){d.x=direction.x/len;d.y=direction.y/len;d.z=direction.z/len;}}switch (m.action) {
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
                            soldier.team = m.team;if(room.phase==='preparation')target.ready=false;
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
                case 'unready':
                    if(room.phase==='preparation')c.ready=false;break;
                case 'ready':
                    if(c.ready&&room.phase!=='preparation')break;
                    if(room.phase==='preparation'){const elapsed=b.elapsed;b.elapsed=0;b.setLoadout(m.loadout);b.elapsed=elapsed;c.ready=true;}else {b.setLoadout(m.loadout);c.ready=true;b.player.alive=true;b.player.hp=100;b.player.spawnGraceUntil=b.elapsed+3;}break;
                case 'begin':
                    if(c.isHost&&room.phase==='preparation'&&room.clients.size>0&&room.reservations.size===0&&[...room.clients.values()].every(o=>o.ready)){room.phase='countdown';room.countdownAt=Date.now()+3000;}break;
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
                case 'recommendation':if(typeof m.value==='boolean')b.respondRecommendation(m.value);break;case 'rescue':b.callRescue();break;case 'veh-weapon':b.selectVehicleWeapon(m.value===1?1:0);break;case 'veh-ammo':if(['armor','he'].includes(m.value))b.setVehicleAmmo(m.value);break;case 'flares':b.deployFlares();break;
                case 'grenade':
                    b.throwGrenade(d);
                    break;
                case 'gadget':
                    b.useGadget();
                    break;
                case 'rocket':
                    b.fireRocket(d,muzzle);
                    break;
                case 'aa':
                    b.fireAA(d,muzzle);
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
                    if (room.phase==='preparation'||!c.loadoutSet) {
                        const elapsed = b.elapsed;
                        b.elapsed = 0;
                        b.setLoadout(m.loadout);
                        b.elapsed = elapsed;
                        c.loadoutSet = true;
                    }
                    break;
            } });
    });
    ws.on('close', code => { room.clients.delete(ws);if(code!==4000&&code!==1008)room.reservations.set(c.resumeToken,{id:c.id,isHost:c.isHost,resumeToken:c.resumeToken,state:c.state,ready:c.ready,alive:s.alive,hp:s.hp,expires:Date.now()+30000});if(room.phase==='countdown'){room.phase='preparation';room.countdownAt=0;}s.player = false; if (b.settings.aiEnabled === false) {
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
    } if (!room.clients.size)room.emptyAt = Date.now();ensureHost(room); });
});
setInterval(() => {
    for (const [code, r] of rooms) {
        for(const [token,c] of r.reservations)if(c.expires<=Date.now())r.reservations.delete(token);
        ensureHost(r);
        if (!r.clients.size) {
            if (Date.now() - (r.emptyAt || r.created) > 600000)
                rooms.delete(code);
            continue;
        }
        const b = r.battle;
        if(r.phase==='countdown'&&Date.now()>=r.countdownAt){r.phase='battle';for(const c of r.clients.values())b.soldiers[c.id].spawnGraceUntil=b.elapsed+3;}
        for (const c of r.clients.values())
            withClient(r, c, b => { if(b.player.downedUntil>b.elapsed&&c.input.giveUp&&Date.now()-c.lastInput<250)b.giveUp(.05);if (r.phase!=='battle'||!c.ready||Date.now() - c.lastInput > 250 || !b.player.alive || b.finished)
                return; const i = c.input, y = i.yaw, p = i.pitch, d = { x: -Math.sin(y) * Math.cos(p), y: Math.sin(p), z: -Math.cos(y) * Math.cos(p) }; b.player.yaw = y + Math.PI; b.playerAiming = i.ads;if(i.equipment==='aa')b.warnAATarget(d); if (b.inVehicle) {
                b.drivePlayerVehicle(i.forward,i.side,.05,y,b.playerVehicle?.kind==='jet'?i.airbrake:false,p,i.boost);
                b.aimPlayerVehicle(y, p, .05);
                b.changeAltitude(i.up, .05);
                if (i.fire)
                    b.shootPlayerVehicle(d);
            }
            else {
                const len = Math.hypot(i.forward, i.side) || 1, speed = i.crouch ? 3.8 : i.sprint && !i.ads ? 13.7 : i.ads ? 5.8 : 8.6;
                const desiredX=(Math.cos(y)*i.side-Math.sin(y)*i.forward)/len*speed,desiredZ=(-Math.sin(y)*i.side-Math.cos(y)*i.forward)/len*speed;
                const weight=1-Math.exp(-.05*(i.forward||i.side?13:17));
                const velocity=b.player.velocity||{x:0,z:0};velocity.x+=(desiredX-velocity.x)*weight;velocity.z+=(desiredZ-velocity.z)*weight;
                const old={...b.player.pos};b.movePlayer(velocity.x*.05,velocity.z*.05);
                if(Math.hypot(b.player.pos.x-old.x,b.player.pos.z-old.z)<Math.hypot(velocity.x,velocity.z)*.05*.22){velocity.x*=.65;velocity.z*=.65;}b.player.velocity=velocity;
                if (i.fire && (b.activeWeapon.automatic || !c.fireHeld))
                    b.shootPlayer(d,i.crouch?1.07:1.78,i.muzzleOffset?{x:b.player.pos.x+i.muzzleOffset.x,y:heightAt(b.player.pos.x,b.player.pos.z)+i.muzzleOffset.y,z:b.player.pos.z+i.muzzleOffset.z}:undefined);
                c.fireHeld = i.fire;
            } });
        const first = [...r.clients.values()][0];
        if(r.phase==='battle')withClient(r, first, b => b.tick(.05));
        const events = b.events();
        for (const event of events)
            if (event.type === 'capture' && event.owner !== null)
                for (const c of r.clients.values())
                    if (c !== first && b.soldiers[c.id].team === event.owner && b.soldiers[c.id].alive && Math.hypot(b.soldiers[c.id].pos.x - b.points.find(p => p.id === event.id).x, b.soldiers[c.id].pos.z - b.points.find(p => p.id === event.id).z) < 25)
                        c.state.requisitionPoints = Math.min(9999, c.state.requisitionPoints + 120);
        for (const [ws, c] of r.clients) {
            withClient(r, c, b => b.advancePlayerTimers());
            const filtered = events.filter(e => (e.type !== 'hitConfirmed'||e.owner===c.id) && (e.type !== 'rp' || e.owner === c.id) && (e.type !== 'playerHit' || e.victim === c.id)).map(e => (e.type === 'shot' || e.type === 'vehicleShot') ? { ...e, player: e.owner === c.id } : e);
            const state = { type: 'state',phase:r.phase,countdown:Math.max(0,(r.countdownAt-Date.now())/1000),roster:[...[...r.clients.values()].map(c=>({id:c.id,ready:c.ready,team:b.soldiers[c.id].team,isHost:c.isHost,connected:true})),...[...r.reservations.values()].map(c=>({id:c.id,ready:c.ready,team:b.soldiers[c.id].team,isHost:c.isHost,connected:false}))], elapsed: b.elapsed, finished: b.finished, winner: b.winner, sectorIndex: b.sectorIndex, tickets: b.tickets, soldiers: b.soldiers, vehicles: b.vehicles, points: b.points, supports: b.supports, projectiles: b.projectiles, grenades:b.grenades,ammoBoxes:b.ammoBoxes, recommendation:b.commanders[b.soldiers[c.id].team].recommendations.get(b.soldiers[c.id].squad)??null,player: c.state, events: filtered };
            if (ws.readyState === WebSocket.OPEN && ws.bufferedAmount < 1024 * 1024)
                ws.send(JSON.stringify(state));
        }
    }
}, 50).unref();
server.on('error',error=>{console.error(error.code==='EADDRINUSE'?`端口 ${port} 已占用。关闭旧服务或使用 PORT=其他端口 npm run lan。`:`LAN 启动失败：${error.message}`);process.exitCode=1;});
server.listen(port,'0.0.0.0',()=>{console.log(`IRONFRONT LAN 本机：http://localhost:${port} · 最多 8 人`);for(const url of addresses())console.log(`朋友访问：${url}（同一局域网）`);});
