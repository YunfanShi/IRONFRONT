const { test } = require('node:test');
const assert = require('node:assert/strict');
const { spawn } = require('node:child_process');
const path = require('node:path');
const fs = require('node:fs');
const os = require('node:os');
const { once } = require('node:events');
const { version } = require('../package.json');

for (const [index, mode] of ['dedicated', 'host'].entries()) {
    test(`${mode} starts from another working directory and serves the game`, async () => {
        const port = 18790 + index;
        const child = spawn(process.execPath, [path.join(__dirname, 'start-lan.cjs'), '--mode', mode, '--port', String(port), '--skip-build', '--no-open'], { cwd: os.tmpdir(), stdio: ['ignore', 'pipe', 'pipe'] });
        let output = '';
        child.stdout.on('data', data => { output += data; });
        child.stderr.on('data', data => { output += data; });
        try {
            let status;
            for (let i = 0; i < 80; i++) {
                if (child.exitCode !== null) throw new Error(output);
                try { status = await (await fetch(`http://127.0.0.1:${port}/api/status`)).json(); break; } catch { await new Promise(resolve => setTimeout(resolve, 100)); }
            }
            assert(status, output);
            assert.equal(status.version, version);
            assert.equal(status.hostMode, mode);
            assert.equal(status.simulation, 'server-authoritative');
            assert.match(await (await fetch(`http://127.0.0.1:${port}/`)).text(), /<div id="app">/);
            const local = path.join(__dirname, '../public/audio/music/main-theme.mp3');
            if (fs.existsSync(local)) {
                const response = await fetch(`http://127.0.0.1:${port}/audio/music/main-theme.mp3`);
                assert.equal(response.headers.get('content-type'), 'audio/mpeg');
                assert.deepEqual(Buffer.from(await response.arrayBuffer()), fs.readFileSync(local));
            }
            const room = await (await fetch(`http://127.0.0.1:${port}/api/rooms`, { method: 'POST' })).json();
            assert.match(room.code, /^[A-F0-9]{6}$/);
        } finally {
            if (child.exitCode === null) { const exited = once(child, 'exit'); child.kill(); await exited; }
        }
    });
}
