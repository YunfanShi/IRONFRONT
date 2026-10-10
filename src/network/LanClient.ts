import { Battle, type BattleEvent, type BattleSettings } from '../core/Battle';
import {lanAddress,lanJSON} from './LanAddress';
export class LanClient {
    private socket: WebSocket | null = null;
    private pending: BattleEvent[] = [];
    private lastSend = 0;
    phase:'preparation'|'countdown'|'battle'='preparation';countdown=0;roster:{id:number;ready:boolean;team:'blue'|'red';isHost:boolean;connected:boolean}[]=[];
    id = 0;
    code = '';
    connected = false;
    isHost = false;
    address = '';
    shareUrls: string[] = [];
    private correction = {x: 0, z: 0};
    onRelocate: () => void = () => {};
    onConnectionState:(state:'connected'|'reconnecting'|'disconnected')=>void=()=>{};
    onRoleChange:(isHost:boolean)=>void=()=>{};
    private receivedState = false;
    private roomUrl:URL|null=null;
    private battle:Battle|null=null;
    private resumeToken='';
    private desiredLoadout:unknown;
    private closedByUser=false;
    private reconnecting=false;
    private cancelOpen:()=>void=()=>{};
    onDisconnect: () => void = () => { };
    async connect(address: string, code: string, host: boolean, options: Record<string, unknown> = {}): Promise<Battle> {
        const url = lanAddress(address);this.address=url.host;
        const info=await lanJSON(new URL('/api/status',url));if(!String(info.version).startsWith('0.29.'))throw new Error(`房主服务版本 ${info.version} 与当前网页不匹配，请房主更新到0.29并停止旧进程、重新运行 npm run lan`);this.shareUrls=Array.isArray(info.addresses)?info.addresses:[];
        if(location.protocol==='https:'&&url.protocol==='http:')throw new Error('HTTPS 页面无法连接 HTTP 主机，请打开房主分享的 HTTP 网页再加入');
        let hostToken='';
        if(host){const room=await lanJSON(new URL('/api/rooms',url),{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(options)});code=room.code;hostToken=room.hostToken;}
        else {code=(code||url.searchParams.get('room')||'').trim().toUpperCase();if(!/^[A-Z0-9]{6}$/.test(code))throw new Error('请输入六位房间码或完整分享链接');await lanJSON(new URL('/api/rooms/'+code,url));}
        this.code = code.trim().toUpperCase();this.roomUrl=new URL('/play',url);this.roomUrl.protocol=url.protocol==='https:'?'wss:':'ws:';this.roomUrl.searchParams.set('room',this.code);if(hostToken)this.roomUrl.searchParams.set('token',hostToken);this.desiredLoadout=options.loadout;
        return this.openSocket();
    }
    private openSocket():Promise<Battle>{
        const wsURL=new URL(this.roomUrl!);if(this.resumeToken){wsURL.searchParams.delete('token');wsURL.searchParams.set('resume',this.resumeToken);}
        const resuming=!!this.battle;
        return new Promise((resolve, reject) => {
            let welcomed=false,settled=false;
            const ws=this.socket=new WebSocket(wsURL),timeout=window.setTimeout(()=>{ws.close();if(!settled){settled=true;reject(new Error('连接超时，请确认房主运行 npm run lan'));}},7000);
            this.cancelOpen=()=>{if(!settled){settled=true;clearTimeout(timeout);reject(new Error('连接已取消'));}};
            ws.onmessage=event=>{let m:any;try{m=JSON.parse(event.data)}catch{return}if(m.type==='welcome'){
                if(resuming&&(m.id!==this.id||m.resumed!==true)){ws.close();return;}
                this.id=m.id;this.isHost=m.isHost===true;this.resumeToken=m.resumeToken??'';
                if(!this.battle){this.battle=new Battle(m.settings as BattleSettings);this.battle.controlledPlayerId=this.id;if(this.desiredLoadout)this.action('loadout',undefined,{loadout:this.desiredLoadout});}
            }
            else if(m.type==='role'){this.isHost=m.isHost===true;this.onRoleChange(this.isHost);}
            else if (m.type === 'state' && this.battle) {
                this.apply(this.battle,m);
                if (!welcomed && (resuming||!this.desiredLoadout||Object.entries(this.desiredLoadout as Record<string,unknown>).every(([key,value])=>m.player.loadout[key]===value))) {
                    welcomed=true;settled=true;this.connected=true;clearTimeout(timeout);this.cancelOpen=()=>{};this.onConnectionState('connected');resolve(this.battle);
                }
                if(Array.isArray(m.events))this.pending.push(...m.events);
            } };
            ws.onerror=()=>{};
            ws.onclose=event=>{clearTimeout(timeout);if(this.socket!==ws||this.closedByUser)return;const was=this.connected;this.connected=false;if(!settled){settled=true;reject(new Error('房间连接中断：'+(event.reason||'服务不可用、房间已满或会话已过期')));}if(was)this.reconnect();};
        });
    }
    private async reconnect(){if(this.reconnecting||this.closedByUser)return;this.reconnecting=true;this.receivedState=false;this.correction={x:0,z:0};this.onConnectionState('reconnecting');for(const delay of [250,500,1000,1500,2500,3500,4500]){await new Promise(resolve=>window.setTimeout(resolve,delay));if(this.closedByUser)return;try{await this.openSocket();this.reconnecting=false;this.onRelocate();return;}catch(error){if(String(error).includes('Session expired'))break;}}this.reconnecting=false;this.onConnectionState('disconnected');this.onDisconnect();}
    private apply(b: Battle, m: any) {
        b.remoteRecommendation=m.recommendation??null;this.phase=m.phase??'battle';this.countdown=m.countdown??0;this.roster=m.roster??[];
        const previous = { ...b.player.pos }, alive = b.player.alive, vehicle = b.player.vehicleId, team = b.player.team;
 for (const key of ['elapsed', 'finished', 'winner', 'sectorIndex'] as const)
        b[key] = m[key] as never; Object.assign(b.tickets, m.tickets); for (const key of ['soldiers', 'vehicles', 'points', 'supports', 'projectiles','grenades','ammoBoxes'] as const) {
        if(!Array.isArray(m[key]))continue;const list = b[key] as any[];
        for (let i = 0; i < m[key].length; i++) {
            if (list[i])
                Object.assign(list[i], m[key][i]);
            else
                list.push(m[key][i]);
        }
        list.length = m[key].length;
    } b.importPlayerState(m.player);
        const player = b.player, authoritative = {...player.pos};
        if(this.receivedState && alive && player.alive && vehicle === null && player.vehicleId === null && team === player.team && Math.hypot(authoritative.x-previous.x,authoritative.z-previous.z)<3){
            player.pos = previous;
            // Small authority errors are distributed over rendering frames, never a 20Hz camera jump.
            this.correction = {x:authoritative.x+(player.velocity?.x??0)*.035-previous.x,z:authoritative.z+(player.velocity?.z??0)*.035-previous.z};
        } else {this.correction={x:0,z:0};if(this.receivedState)this.onRelocate();}
        this.receivedState=true;
    }
    reconcile(b: Battle, dt: number) {
        if(!b.player.alive || b.inVehicle)return;
        const weight=1-Math.exp(-dt*10), x=this.correction.x*weight,z=this.correction.z*weight;
        b.movePlayer(x,z);this.correction.x-=x;this.correction.z-=z;
    }
    input(value: Record<string, unknown>) { const now = performance.now(); if (now - this.lastSend < 35)
        return; this.lastSend = now; this.send({ type: 'input', ...value }); }
    action(action: string, value?: unknown, extra: Record<string, unknown> = {}) { this.send({ type: 'action', action, value, ...extra }); }
    private send(value: unknown) { if (this.socket?.readyState === WebSocket.OPEN)
        this.socket.send(JSON.stringify(value)); }
    events() { const events = this.pending; this.pending = []; return events; }
    close() { this.closedByUser=true;this.onDisconnect=()=>{};this.cancelOpen();const ws=this.socket;if(ws?.readyState===WebSocket.CONNECTING)ws.addEventListener('open',()=>ws.close(4000,'leave'),{once:true});else try{ws?.close(4000,'leave')}catch{}this.socket=null;this.connected=false; }
}
