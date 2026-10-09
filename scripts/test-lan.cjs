const assert = require('node:assert/strict'), { spawn } = require('node:child_process'), { WebSocket } = require('ws');
const lanIP=Object.values(require('node:os').networkInterfaces()).flat().find(n=>n&&n.family==='IPv4'&&!n.internal)?.address||'127.0.0.1';
const port = 18787, host = spawn(process.execPath, ['server/lan.cjs'], { env: { ...process.env, PORT: String(port) }, stdio: ['ignore', 'pipe', 'inherit'] });
const wait = ms => new Promise(r => setTimeout(r, ms));
const connect = (code, token = '', resume = '') => new Promise((resolve, reject) => { const ws = new WebSocket(`ws://${lanIP}:${port}/play?room=${code}&token=${token}&resume=${resume}`); const c = { ws, id: null, states: [], events: [], resumeToken:'', isHost:false }; ws.on('message', data => { const m = JSON.parse(data); if (m.type === 'welcome'){
    c.id = m.id;c.resumeToken=m.resumeToken;c.isHost=m.isHost;
}else if(m.type==='role')c.isHost=m.isHost;
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
        assert.match(code, /^[A-F0-9]{6}$/);const status=await (await fetch(`http://${lanIP}:${port}/api/status`)).json();assert.equal(status.port,port);assert(status.addresses.includes(`http://${lanIP}:${port}`));assert(status.rooms.every(r=>!('code' in r)),'status must not expose invite codes');assert.equal((await fetch(`http://${lanIP}:${port}/api/rooms/${code}`)).status,200);assert.equal((await fetch(`http://${lanIP}:${port}/api/rooms/ZZZZZZ`)).status,404);
        const freshRoom=await (await fetch(`http://${lanIP}:${port}/api/rooms`,{method:'POST'})).json(),earlyGuest=await connect(freshRoom.code);assert.equal(earlyGuest.isHost,false,'a guest must not claim a room before its founder connects');const founder=await connect(freshRoom.code,freshRoom.hostToken);assert.equal(founder.isHost,true);founder.ws.close(4000,'leave');await wait(120);assert.equal(earlyGuest.isHost,true);const replay=await connect(freshRoom.code,freshRoom.hostToken);assert.equal(replay.isHost,false,'a used host token must not grant another host role');replay.ws.close(4000,'leave');earlyGuest.ws.close(4000,'leave');
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
        const oldId=a.id,oldToken=a.resumeToken;a.ws.terminate();await wait(160);
        assert(b.states.at(-1).roster.some(c=>c.id===oldId&&!c.connected),'dropped player should keep a visible reserved seat');
        const resumed=await connect(code,'',oldToken);assert.equal(resumed.id,oldId);assert.equal(resumed.isHost,true);assert.equal(resumed.states.at(-1).soldiers[oldId].player,true);
        resumed.ws.close(4000,'leave');
        await wait(150);
        assert.equal(b.states.at(-1).soldiers[a.id].player, false);
        assert.equal(b.isHost,true,'remaining player should inherit the host role');b.ws.close(4000,'leave');
        const countdownRoom=await (await fetch(`http://${lanIP}:${port}/api/rooms`,{method:'POST'})).json();
        const captain=await connect(countdownRoom.code,countdownRoom.hostToken),mate=await connect(countdownRoom.code);
        captain.ws.send(JSON.stringify({type:'action',action:'ready'}));mate.ws.send(JSON.stringify({type:'action',action:'ready'}));await wait(100);captain.ws.send(JSON.stringify({type:'action',action:'begin'}));await wait(100);assert.equal(captain.states.at(-1).phase,'countdown');assert.equal((await fetch(`http://${lanIP}:${port}/api/rooms/${countdownRoom.code}`)).status,409);
        mate.ws.terminate();await wait(150);assert.equal(captain.states.at(-1).phase,'preparation','a disconnect must cancel countdown');
        const mateBack=await connect(countdownRoom.code,'',mate.resumeToken);assert.equal(mateBack.id,mate.id);assert.equal(mateBack.states.at(-1).roster.length,2);captain.ws.close(4000,'leave');mateBack.ws.close(4000,'leave');
        const config = await (await fetch(`http://${lanIP}:${port}/api/rooms`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ size: 8, difficulty: 'hard', tickets: 300, killTicketPenalty: 2, aiEnabled: false, joinTeam: 'red' }) })).json();
        let h = await connect(config.code, config.hostToken);const e = await connect(config.code);h.ws.send(JSON.stringify({type:'action',action:'ready'}));e.ws.send(JSON.stringify({type:'action',action:'ready'}));await wait(150);h.ws.send(JSON.stringify({type:'action',action:'begin'}));await wait(3200);
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
        for(let i=0;i<12;i++){h.ws.send(JSON.stringify({type:'input',yaw:-Math.PI,pitch:.3,forward:1,side:1,boost:true,airbrake:false}));await wait(50);}const flight=h.states.at(-1).vehicles[7];assert(flight.roll>.3&&flight.speed>3,'LAN authority did not apply jet bank/throttle');
        const flightId=h.id,flightToken=h.resumeToken;h.ws.terminate();await wait(150);h=await connect(config.code,'',flightToken);assert.equal(h.id,flightId);assert.equal(h.states.at(-1).soldiers[flightId].vehicleId,null);assert.equal(h.states.at(-1).player.playerVehicleId,null,'rejoin after AI-free seat release must clear stale private vehicle state');assert.equal(h.states.at(-1).soldiers[flightId].alive,true);
        const more = [];
        for (let i = 0; i < 6; i++)
            more.push(await connect(config.code));
        const ninth = new WebSocket(`ws://${lanIP}:${port}/play?room=${config.code}`);
        assert.equal(await new Promise(r => ninth.on('close', r)), 1008);
        await wait(100);
        assert.equal(h.states.at(-1).soldiers.filter(s => s.player).length, 8);
        const detached=more.pop();detached.ws.terminate();await wait(120);assert.equal((await fetch(`http://${lanIP}:${port}/api/rooms/${config.code}`)).status,409,'a disconnected reserved seat still counts toward capacity');const returned=await connect(config.code,'',detached.resumeToken);assert.equal(returned.id,detached.id);more.push(returned);
        for (const c of more)
            c.ws.close();
        e.ws.close();
        await wait(150);
        assert.equal(h.states.at(-1).soldiers.filter(s => s.alive).length, 1);
        h.ws.close();
        console.log('LAN_IP_TEST',lanIP);console.log('LAN PASS: two real clients, authority and events, stable-seat reconnect, host handoff, countdown cancellation, AI takeover, opposing teams, host permissions, jet controls and eight-player capacity.');
    }
    finally {
        host.kill();
    }
})().catch(e => { console.error(e); process.exitCode = 1; });
