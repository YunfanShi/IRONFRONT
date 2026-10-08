import { Battle, type BattleEvent, type BattleSettings } from '../core/Battle';
export class LanClient {
    private socket: WebSocket | null = null;
    private pending: BattleEvent[] = [];
    private lastSend = 0;
    id = 0;
    code = '';
    connected = false;
    isHost = false;
    onDisconnect: () => void = () => { };
    async connect(address: string, code: string, host: boolean, options: Record<string, unknown> = {}): Promise<Battle> {
        const url = new URL(address.includes('://') ? address : `http://${address}`);
        if (!url.port)
            url.port = '8787';
        if (!['http:', 'https:'].includes(url.protocol))
            throw new Error('请输入主机 IP:端口');
        let hostToken = '';
        if (host) {
            const response = await fetch(new URL('/api/rooms', url), { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(options) });
            if (!response.ok)
                throw new Error('房间创建失败');
            const room = await response.json();
            code = room.code;
            hostToken = room.hostToken;
        }
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
                this.connected = true;
            }
            else if (m.type === 'state' && battle) {
                this.apply(battle, m);
                if (!welcomed) {
                    welcomed = true;
                    clearTimeout(timeout);
                    resolve(battle);
                }
                this.pending.push(...m.events);
            } };
            ws.onerror = () => reject(new Error('无法连接。房主先运行 npm run lan，并允许本机防火墙端口 8787。'));
            ws.onclose = () => { clearTimeout(timeout); const was = this.connected; this.connected = false; if (was)
                this.onDisconnect();
            else
                reject(new Error('房间不存在、已满或服务不可用')); };
        });
    }
    private apply(b: Battle, m: any) { for (const key of ['elapsed', 'finished', 'winner', 'sectorIndex'] as const)
        b[key] = m[key] as never; Object.assign(b.tickets, m.tickets); for (const key of ['soldiers', 'vehicles', 'points', 'supports', 'projectiles'] as const) {
        const list = b[key] as any[];
        for (let i = 0; i < m[key].length; i++) {
            if (list[i])
                Object.assign(list[i], m[key][i]);
            else
                list.push(m[key][i]);
        }
        list.length = m[key].length;
    } b.importPlayerState(m.player); }
    input(value: Record<string, unknown>) { const now = performance.now(); if (now - this.lastSend < 35)
        return; this.lastSend = now; this.send({ type: 'input', ...value }); }
    action(action: string, value?: unknown, extra: Record<string, unknown> = {}) { this.send({ type: 'action', action, value, ...extra }); }
    private send(value: unknown) { if (this.socket?.readyState === WebSocket.OPEN)
        this.socket.send(JSON.stringify(value)); }
    events() { const events = this.pending; this.pending = []; return events; }
    close() { this.onDisconnect = () => { }; this.socket?.close(); this.socket = null; this.connected = false; }
}
