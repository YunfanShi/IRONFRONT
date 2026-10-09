const assert = require('node:assert/strict'), { spawn } = require('node:child_process'), { WebSocket } = require('ws');
const lanIP=Object.values(require('node:os').networkInterfaces()).flat().find(n=>n&&n.family==='IPv4'&&!n.internal)?.address||'127.0.0.1';
const port = 18787, host = spawn(process.execPath, ['server/lan.cjs'], { env: { ...process.env, PORT: String(port) }, stdio: ['ignore', 'pipe', 'inherit'] });
const wait = ms => new Promise(r => setTimeout(r, ms));
const connect = (code, token = '') => new Promise((resolve, reject) => { const ws = new WebSocket(`ws://${lanIP}:${port}/play?room=${code}&token=${token}`); const c = { ws, id: null, states: [], events: [] }; ws.on('message', data => { const m = JSON.parse(data); if (m.type === 'welcome')
    c.id = m.id;
else if (m.type === 'state') {
    c.states.push(m);
    c.events.push(...m.events);
    if (c.states.length > 40)
        c.states.shift();
    resolve(c);
} }); ws.on('error', reject); setTimeout(() => reject(new Error('snapshot timeout')), 4000).unref(); });
(async () => {
    try {
        await new Promise((resolve, reject) => { const timeout = setTimeout(() => reject(new Error('LAN service did not start')), 5000); host.stdout.once('data', () => { clearTimeout(timeout); resolve(); }); host.once('exit', code => { clearTimeout(timeout); reject(new Error('LAN server exited ' + code)); }); });
        const response = await fetch(`http://${lanIP}:${port}/api/rooms`, { method: 'POST' }), { code, hostToken } = await response.json();
        assert.match(code, /^[A-F0-9]{6}$/);const status=await (await fetch(`http://${lanIP}:${port}/api/status`)).json();assert.equal(status.port,port);assert(status.addresses.includes(`http://${lanIP}:${port}`));assert.equal((await fetch(`http://${lanIP}:${port}/api/rooms/${code}`)).status,200);assert.equal((await fetch(`http://${lanIP}:${port}/api/rooms/ZZZZZZ`)).status,404);
        const a = await connect(code, hostToken), b = await connect(code);
        assert.equal(a.states.at(-1).phase,'preparation');assert.equal(a.states.at(-1).elapsed,0);
        a.ws.send(JSON.stringify({type:'action',action:'ready'}));a.ws.send(JSON.stringify({type:'action',action:'begin'}));await wait(150);assert.equal(a.states.at(-1).phase,'preparation');
        b.ws.send(JSON.stringify({type:'action',action:'ready'}));await wait(100);a.ws.send(JSON.stringify({type:'action',action:'begin'}));await wait(3200);assert.equal(a.states.at(-1).phase,'battle');
        assert.notEqual(a.id, b.id);
        const start = a.states.at(-1).soldiers[a.id].pos;
        for (let i = 0; i < 12; i++) {
            a.ws.send(JSON.stringify({ type: 'input', yaw: 0, pitch: 0, side: 1, forward: 0 }));
            await wait(50);
        }
        await wait(100);
        const own = a.states.at(-1).soldiers[a.id], seen = b.states.at(-1).soldiers[a.id];
        assert(own.pos.x > start.x + 2);
        assert(Math.abs(seen.pos.x - own.pos.x) < 1);
        assert.equal(own.player, true);
        assert.equal(b.states.at(-1).soldiers[b.id].player, true);
        assert(Math.abs(a.states.at(-1).elapsed - b.states.at(-1).elapsed) <= .051);
        const ammo = b.states.at(-1).player.ammo.carbine;
        a.ws.send(JSON.stringify({ type: 'input', yaw: 0, pitch: .5, forward: 0, side: 0, fire: true,muzzleOffset:{x:.35,y:1.4,z:-.65} }));
        await wait(150);
        assert(a.states.at(-1).player.ammo.carbine < ammo);
        assert.equal(b.states.at(-1).player.ammo.carbine, ammo);
        const shot=a.events.find(e=>e.type==='shot'&&e.player);assert(shot);assert(Math.abs(shot.muzzle.x-shot.from.x-.35)<.0001);assert(Math.abs(shot.muzzle.z-shot.from.z+.65)<.0001);
        assert(b.events.some(e => e.type === 'shot'));
        await wait(450);
        const stopped = a.states.at(-1).soldiers[a.id].pos;
        await wait(200);
        assert.deepEqual(a.states.at(-1).soldiers[a.id].pos, stopped);
        const reject = new WebSocket(`ws://${lanIP}:${port}/play?room=NOPE`);
        const close = await new Promise(r => reject.on('close', code => r(code)));
        assert.equal(close, 1008);
        a.ws.close();
        await wait(150);
        assert.equal(b.states.at(-1).soldiers[a.id].player, false);
        b.ws.close();
        const config = await (await fetch(`http://${lanIP}:${port}/api/rooms`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ size: 8, difficulty: 'hard', tickets: 300, killTicketPenalty: 2, aiEnabled: false, joinTeam: 'red' }) })).json();
        const h = await connect(config.code, config.hostToken), e = await connect(config.code);h.ws.send(JSON.stringify({type:'action',action:'ready'}));e.ws.send(JSON.stringify({type:'action',action:'ready'}));await wait(150);h.ws.send(JSON.stringify({type:'action',action:'begin'}));await wait(3200);
        await wait(100);
        let snapshot = h.states.at(-1);
        assert.equal(snapshot.soldiers[h.id].team, 'blue');
        assert.equal(snapshot.soldiers[e.id].team, 'red');
        assert.equal(snapshot.soldiers.filter(s => s.alive).length, 2);
        assert.equal(snapshot.soldiers.filter(s => s.player).length, 2);
        e.ws.send(JSON.stringify({ type: 'action', action: 'team', value: h.id, team: 'red' }));
        await wait(100);
        assert.equal(h.states.at(-1).soldiers[h.id].team, 'blue');
        h.ws.send(JSON.stringify({ type: 'action', action: 'team', value: e.id, team: 'blue' }));
        await wait(100);
        assert.equal(e.states.at(-1).soldiers[e.id].team, 'blue');
        assert(h.states.at(-1).soldiers.filter(s => s.team === 'blue' && s.squad === 0).length <= 4);
        assert(snapshot.recommendation);h.ws.send(JSON.stringify({type:'action',action:'recommendation',value:false}));await wait(150);assert.equal(h.states.at(-1).recommendation.status,'dismissed');
        h.ws.send(JSON.stringify({type:'action',action:'unstuck'}));await wait(5100);h.ws.send(JSON.stringify({type:'action',action:'respawn',value:'VEHICLE-7-0'}));await wait(200);assert.equal(h.states.at(-1).soldiers[h.id].vehicleId,7);
        for(let i=0;i<12;i++){h.ws.send(JSON.stringify({type:'input',yaw:-Math.PI,pitch:.3,forward:1,side:1,boost:true,airbrake:false}));await wait(50);}const flight=h.states.at(-1).vehicles[7];assert(flight.roll>.3&&flight.speed>3,'LAN authority did not apply jet bank/throttle');h.ws.send(JSON.stringify({type:'action',action:'enter'}));await wait(100);
        const more = [];
        for (let i = 0; i < 6; i++)
            more.push(await connect(config.code));
        const ninth = new WebSocket(`ws://${lanIP}:${port}/play?room=${config.code}`);
        assert.equal(await new Promise(r => ninth.on('close', r)), 1008);
        await wait(100);
        assert.equal(h.states.at(-1).soldiers.filter(s => s.player).length, 8);
        for (const c of more)
            c.ws.close();
        e.ws.close();
        await wait(150);
        assert.equal(h.states.at(-1).soldiers.filter(s => s.alive).length, 1);
        h.ws.close();
        console.log('LAN_IP_TEST',lanIP);console.log('LAN PASS: two real clients, shared authoritative movement/time/events, separate ammo, stale input stop, unknown room rejection, AI takeover on disconnect; configured AI-free opposing teams, host-only reassignment, commander dismissal and authoritative jet bank/boost, four-member squads, eight-player cap and ninth-player rejection.');
    }
    finally {
        host.kill();
    }
})().catch(e => { console.error(e); process.exitCode = 1; });
