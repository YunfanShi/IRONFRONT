import { Battle, type BattleEvent, type BattleSettings } from '../core/Battle';
import {lanAddress,lanJSON} from './LanAddress';
export class LanClient {
    private socket: WebSocket | null = null;
    private pending: BattleEvent[] = [];
    private lastSend = 0;
    phase:'preparation'|'countdown'|'battle'='preparation';countdown=0;roster:{id:number;ready:boolean}[]=[];
    id = 0;
    code = '';
    connected = false;
    isHost = false;
    address = '';
    shareUrls: string[] = [];
    private correction = {x: 0, z: 0};
    onRelocate: () => void = () => {};
    private receivedState = false;
    onDisconnect: () => void = () => { };
    async connect(address: string, code: string, host: boolean, options: Record<string, unknown> = {}): Promise<Battle> {
        const url = lanAddress(address);this.address=url.host;
        const info=await lanJSON(new URL('/api/status',url));if(!String(info.version).startsWith('0.16.'))throw new Error(`房主服务版本 ${info.version} 与当前网页不匹配，请房主更新到0.16并停止旧进程、重新运行 npm run lan`);this.shareUrls=Array.isArray(info.addresses)?info.addresses:[];
        if(location.protocol==='https:'&&url.protocol==='http:')throw new Error('HTTPS 页面无法连接 HTTP 主机，请打开房主分享的 HTTP 网页再加入');
        let hostToken='';
        if(host){const room=await lanJSON(new URL('/api/rooms',url),{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(options)});code=room.code;hostToken=room.hostToken;}
        else {code=(code||url.searchParams.get('room')||'').trim().toUpperCase();if(!/^[A-Z0-9]{6}$/.test(code))throw new Error('请输入六位房间码或完整分享链接');await lanJSON(new URL('/api/rooms/'+code,url));}
        this.code = code.trim().toUpperCase();
        const wsURL = new URL('/play', url);
        wsURL.protocol = url.protocol === 'https:' ? 'wss:' : 'ws:';
        wsURL.searchParams.set('room', this.code);
        if (hostToken)
            wsURL.searchParams.set('token', hostToken);
        return new Promise((resolve, reject) => {
            let battle: Battle | null = null;
            let welcomed = false;
            const ws = this.socket = new WebSocket(wsURL), timeout = window.setTimeout(() => { ws.close(); reject(new Error('连接超时，请确认房主运行 npm run lan')); }, 7000);
            ws.onmessage = event => { const m = JSON.parse(event.data); if (m.type === 'welcome') {
                this.id = m.id;
                this.isHost = m.isHost === true;
                battle = new Battle(m.settings as BattleSettings);
                battle.controlledPlayerId = this.id;
                // Install the chosen loadout before allowing gameplay controls.
                if(options.loadout)this.action('loadout',undefined,{loadout:options.loadout});
            }
            else if (m.type === 'state' && battle) {
                this.apply(battle, m);
                if (!welcomed && (!options.loadout || Object.entries(options.loadout as Record<string,unknown>).every(([key,value])=>m.player.loadout[key]===value))) {
                    welcomed = true;
                    this.connected=true;
                    clearTimeout(timeout);
                    resolve(battle);
                }
                this.pending.push(...m.events);
            } };
            ws.onerror = () => {clearTimeout(timeout);reject(new Error(`房主网页可访问，但 WebSocket ${url.host}/play 连接失败。检查浏览器本地网络权限或代理。`));};
            ws.onclose = () => { clearTimeout(timeout); const was = this.connected; this.connected = false; if (was)
                this.onDisconnect();
            else
                reject(new Error('房间拒绝连接：'+(ws.readyState===WebSocket.CLOSED?'可能已满或房间被关闭':'服务不可用'))); };
        });
    }
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
    close() { this.onDisconnect = () => { }; this.socket?.close(); this.socket = null; this.connected = false; }
}
