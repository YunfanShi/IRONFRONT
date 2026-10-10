const assert=require('node:assert/strict');
const {spawn}=require('node:child_process');
const {WebSocket}=require('ws');

const port=18788;
const base=`http://127.0.0.1:${port}`;
const host=spawn(process.execPath,['server/lan.cjs'],{env:{...process.env,PORT:String(port)},stdio:['ignore','pipe','pipe']});
const wait=ms=>new Promise(resolve=>setTimeout(resolve,ms));
function awaitEvent(client,predicate,timeout=1500) {
    const existing=client.events.find(predicate);
    if (existing) return Promise.resolve(existing);
    return new Promise((resolve,reject)=>{
        const timer=setTimeout(()=>{client.waiters.delete(onEvent);reject(new Error('Timed out waiting for Unity message'));},timeout);
        const onEvent=event=>{if(!predicate(event))return;clearTimeout(timer);client.waiters.delete(onEvent);resolve(event);};
        client.waiters.add(onEvent);
    });
}
function connect(code,token='',resume='') {
    return new Promise((resolve,reject)=>{
        const ws=new WebSocket(`ws://127.0.0.1:${port}/unity?room=${code}&token=${token}&resume=${resume}`);
        const client={ws,events:[],waiters:new Set(),welcome:null};
        const timer=setTimeout(()=>reject(new Error('Unity WebSocket timeout')),3000);
        ws.on('message',raw=>{
            const event=JSON.parse(raw);
            client.events.push(event);
            for(const waiter of client.waiters)waiter(event);
            if(event.type==='welcome') {client.welcome=event;clearTimeout(timer);resolve(client);}
        });
        ws.on('error',reject);
        ws.on('close',code=>{if(!client.welcome){clearTimeout(timer);reject(new Error(`Unity WebSocket closed ${code}`));}});
    });
}
async function createRoom() {
    const response=await fetch(`${base}/api/unity/rooms`,{method:'POST',headers:{'Content-Type':'application/json'},body:'{}'});
    assert.equal(response.status,200);
    return response.json();
}
async function status(code) { return (await fetch(`${base}/api/unity/rooms/${code}`)).json(); }
async function rejectSocket(url) {
    const ws=new WebSocket(url);
    return new Promise((resolve,reject)=>{ws.on('close',resolve);ws.on('error',reject);});
}
async function run() {
    const output=[];
    host.stderr.on('data',chunk=>output.push(chunk.toString()));
    await new Promise((resolve,reject)=>{
        const timer=setTimeout(()=>reject(new Error('LAN server startup timeout: '+output.join(''))),4000);
        host.stdout.once('data',()=>{clearTimeout(timer);resolve();});
        host.once('exit',code=>reject(new Error(`LAN server exited ${code}: ${output.join('')}`)));
    });
    const room=await createRoom();
    assert.equal(room.protocol,'unity-2');
    assert.match(room.code,/^[A-F0-9]{6}$/);
    assert.equal((await status(room.code)).phase,'preparation');
    const guest=await connect(room.code);
    assert.equal(guest.welcome.host,false);
    assert(Number.isInteger(guest.welcome.id));
    guest.ws.send(JSON.stringify({type:'start',host:true}));
    await awaitEvent(guest,e=>e.type==='error'&&e.code==='host_only');
    assert.equal((await status(room.code)).phase,'preparation');
    const captain=await connect(room.code,room.hostToken);
    assert.equal(captain.welcome.host,true);
    assert.notEqual(captain.welcome.id,guest.welcome.id);
    assert.equal(await rejectSocket(`ws://127.0.0.1:${port}/unity?room=${room.code}&token=${room.hostToken}`),1008);

    const guestErrors=()=>guest.events.filter(e=>e.type==='error'&&e.code==='host_only').length;
    const denied=async message=>{
        const before=guestErrors();
        guest.ws.send(JSON.stringify(message));
        await awaitEvent(guest,e=>e.type==='error'&&e.code==='host_only'&&guestErrors()>before);
    };
    await denied({type:'settings',tickets:100,botCount:8,host:true});
    await denied({type:'team',targetId:captain.welcome.id,team:'red',host:true});
    await denied({type:'kick',targetId:captain.welcome.id,host:true});
    await denied({type:'snapshot',tick:0,players:[],bots:[],vehicles:[],points:[],blueTickets:0,redTickets:0,host:true});
    assert.equal((await status(room.code)).settings.tickets,100);
    assert.equal((await status(room.code)).roster.find(c=>c.id===captain.welcome.id).team,'blue');
    guest.ws.send(JSON.stringify({type:'team',targetId:-1,team:'red'}));
    await awaitEvent(guest,e=>e.type==='room'&&e.roster.find(c=>c.id===guest.welcome.id)?.team==='red');
    captain.ws.send(JSON.stringify({type:'settings',tickets:300,botCount:2}));
    await awaitEvent(captain,e=>e.type==='error'&&e.code==='unsupported_settings');
    captain.ws.send(JSON.stringify({type:'settings',tickets:300,botCount:8}));
    await awaitEvent(captain,e=>e.type==='room'&&e.settings.tickets===300&&e.settings.botCount===8);
    assert.equal((await status(room.code)).settings.tickets,300);
    captain.ws.send(JSON.stringify({type:'ready',ready:true}));
    guest.ws.send(JSON.stringify({type:'ready',ready:true}));
    await awaitEvent(captain,e=>e.type==='room'&&e.roster.every(c=>c.ready));
    await denied({type:'start',host:true});
    assert.equal((await status(room.code)).phase,'preparation');
    captain.ws.send(JSON.stringify({type:'start'}));
    await awaitEvent(guest,e=>e.type==='room'&&e.phase==='countdown');
    await awaitEvent(guest,e=>e.type==='room'&&e.phase==='battle',4000);

    const falseRoomCount=guest.events.filter(e=>e.type==='room'&&e.phase==='preparation').length;
    guest.ws.send(JSON.stringify({type:'room',phase:'preparation',host:true}));
    guest.ws.send(JSON.stringify({type:'peerInput',id:captain.welcome.id,seq:99}));
    await denied({type:'snapshot',tick:1,players:[],bots:[],vehicles:[],points:[],blueTickets:0,redTickets:0,host:true});
    await wait(80);
    assert.equal(guest.events.filter(e=>e.type==='room'&&e.phase==='preparation').length,falseRoomCount);
    const snapshot={type:'snapshot',tick:1,players:[{id:captain.welcome.id,x:1,y:2,z:3,yaw:90,hp:100,alive:true}],bots:[{id:1,x:0}],vehicles:[{id:1,x:0}],points:[{id:1,owner:'blue'}],blueTickets:299,redTickets:300,id:guest.welcome.id,host:false};
    captain.ws.send(JSON.stringify(snapshot));
    const replicated=await awaitEvent(guest,e=>e.type==='snapshot'&&e.tick===1);
    assert.equal(replicated.players[0].id,captain.welcome.id);
    assert.equal(replicated.id,undefined);
    assert.equal(replicated.host,undefined);
    guest.ws.send(JSON.stringify({type:'input',id:captain.welcome.id,host:true,seq:1,forward:9,side:-8,yaw:9999,pitch:999,sprint:true,fire:true,callRescue:true,giveUp:true}));
    const input=await awaitEvent(captain,e=>e.type==='peerInput'&&e.input.seq===1);
    assert.deepEqual({id:input.id,forward:input.input.forward,side:input.input.side,yaw:input.input.yaw,pitch:input.input.pitch,sprint:input.input.sprint,fire:input.input.fire},
        {id:guest.welcome.id,forward:1,side:-1,yaw:3600,pitch:89,sprint:true,fire:true});
    assert.equal(input.input.callRescue,true);
    assert.equal(input.input.giveUp,true);
    guest.ws.send(JSON.stringify({type:'input',seq:1,forward:0,side:0,yaw:0,pitch:0}));
    guest.ws.send(JSON.stringify({type:'input',seq:2,forward:0,side:0,yaw:0,pitch:0}));
    await wait(60);
    assert.equal(captain.events.filter(e=>e.type==='peerInput'&&e.input.seq===1).length,1,'replayed sequence must be ignored');
    assert.equal(captain.events.filter(e=>e.type==='peerInput').length,1,'rapid extra input must be rate limited');
    guest.ws.send(JSON.stringify({type:'input',seq:2,forward:0,side:0,yaw:0,pitch:0}));
    await awaitEvent(captain,e=>e.type==='peerInput'&&e.input.seq===2);

    guest.ws.send(JSON.stringify({type:'peerDeploy',id:captain.welcome.id,location:'BASE'}));
    guest.ws.send(JSON.stringify({type:'deploy',id:captain.welcome.id,team:'blue',location:'VEHICLE-0'}));
    await wait(60);
    assert.equal(captain.events.filter(e=>e.type==='peerDeploy').length,0,
        'guest cannot forge host deployment events or request unsupported seats');
    guest.ws.send(JSON.stringify({type:'deploy',id:captain.welcome.id,team:'blue',location:'BASE'}));
    const deploy=await awaitEvent(captain,e=>e.type==='peerDeploy');
    assert.deepEqual({id:deploy.id,location:deploy.location},
        {id:guest.welcome.id,location:'BASE'},
        'server binds deployment to the sender, never the claimed player or team');
    guest.ws.send(JSON.stringify({type:'deploy',location:'A'}));
    await wait(60);
    assert.equal(captain.events.filter(e=>e.type==='peerDeploy').length,1,
        'deployment requests are rate limited');
    guest.ws.send(JSON.stringify({type:'peerDowned',id:captain.welcome.id,action:'giveUp'}));
    guest.ws.send(JSON.stringify({type:'downed',id:captain.welcome.id,action:'invalid'}));
    await wait(60);
    assert.equal(captain.events.filter(e=>e.type==='peerDowned').length,0);
    guest.ws.send(JSON.stringify({type:'downed',id:captain.welcome.id,action:'rescue'}));
    const rescue=await awaitEvent(captain,e=>e.type==='peerDowned');
    assert.deepEqual({id:rescue.id,action:rescue.action},
        {id:guest.welcome.id,action:'rescue'});

    captain.ws.terminate();
    await awaitEvent(guest,e=>e.type==='room'&&e.paused===true);
    assert.equal((await status(room.code)).paused,true);
    assert.equal((await status(room.code)).roster.find(c=>c.id===guest.welcome.id).host,false,'guest must not inherit host authority');
    const resumed=await connect(room.code,'',captain.welcome.resumeToken);
    assert.equal(resumed.welcome.host,true);
    assert.equal(resumed.welcome.id,captain.welcome.id);
    assert.equal(resumed.welcome.resumed,true);
    await awaitEvent(guest,e=>e.type==='room'&&e.paused===false);
    resumed.ws.send(JSON.stringify({...snapshot,tick:2}));
    await awaitEvent(guest,e=>e.type==='snapshot'&&e.tick===2);
    resumed.ws.send(JSON.stringify({type:'kick',targetId:guest.welcome.id}));
    assert.equal(await new Promise(resolve=>guest.ws.once('close',resolve)),4003);
    assert.equal(await rejectSocket(`ws://127.0.0.1:${port}/unity?room=${room.code}&resume=${guest.welcome.resumeToken}`),1008);
    resumed.ws.send(JSON.stringify({type:'leave'}));
    await new Promise(resolve=>resumed.ws.once('close',resolve));
    assert.equal((await fetch(`${base}/api/unity/rooms/${room.code}`)).status,404,'host voluntary leave must close room');
    console.log('Unity room PASS: host-only controls and snapshots, server-bound movement, deployment and downed actions, rate limits, host resume without promotion, kick, and voluntary host leave.');
}
run().catch(error=>{console.error(error);process.exitCode=1;}).finally(()=>host.kill());
