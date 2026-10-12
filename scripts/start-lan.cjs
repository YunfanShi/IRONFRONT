// One launcher for a headless server or a host who also plays in a browser.
const fs = require('node:fs');
const path = require('node:path');
const { spawnSync, spawn } = require('node:child_process');
const root = path.resolve(__dirname, '..');
process.chdir(root);

function options(args) {
    let mode = 'dedicated', port = process.env.PORT || '7878', build = true, open = true;
    for (let i = 0; i < args.length; i++) {
        const arg = args[i];
        if (arg === '--mode') mode = args[++i];
        else if (arg === '--port') port = args[++i];
        else if (arg === '--skip-build') build = false;
        else if (arg === '--no-open') open = false;
        else if (arg === '--help') return { help: true };
        else throw new Error(`Unknown option: ${arg}`);
    }
    if (!['dedicated', 'host'].includes(mode)) throw new Error('--mode must be dedicated or host');
    if (!/^\d+$/.test(String(port)) || Number(port) < 1 || Number(port) > 65535) throw new Error('--port must be an integer from 1 to 65535');
    return { mode, port: Number(port), build, open };
}

function run(file, args = []) {
    const result = spawnSync(process.execPath, [file, ...args], { cwd: root, stdio: 'inherit' });
    if (result.error) throw result.error;
    if (result.status !== 0) throw new Error(`Build failed: ${path.basename(file)} (${result.status})`);
}

function openBrowser(url) {
    const command = process.platform === 'win32' ? 'explorer.exe' : process.platform === 'darwin' ? 'open' : 'xdg-open';
    const child = spawn(command, [url], { detached: true, stdio: 'ignore' });
    child.on('error', error => console.warn(`Open ${url} in your browser (${error.message})`));
    child.unref();
}

async function main() {
    const config = options(process.argv.slice(2));
    if (config.help) {
        console.log('node scripts/start-lan.cjs [--mode dedicated|host] [--port 7878] [--skip-build] [--no-open]');
        return;
    }
    for (const dependency of ['typescript', 'vite', 'ws']) {
        if (!fs.existsSync(path.join(root, 'node_modules', dependency))) throw new Error('Install Node.js 24 and run npm ci in this folder first.');
    }
    if (config.build) {
        run('node_modules/typescript/bin/tsc', ['-p', 'tsconfig.logic.json']);
        run('scripts/prepare-logic.cjs');
        run('node_modules/typescript/bin/tsc', ['--noEmit']);
        run('node_modules/vite/bin/vite.js', ['build']);
    }
    for (const file of ['dist/index.html', '.logic-build/core/Battle.js']) {
        if (!fs.existsSync(file)) throw new Error(`Missing ${file}; start again without --skip-build.`);
    }
    process.env.PORT = String(config.port);
    process.env.IRONFRONT_HOST_MODE = config.mode;
    console.log(config.mode === 'host' ? 'HOST AND PLAY: this computer simulates; its browser can play.' : 'DEDICATED SERVER: this computer simulates; players connect from their browsers.');
    console.log('Keep this terminal open. Ctrl+C stops the server and all its rooms.');
    // Browser opening follows a successful listen, never an occupied-port failure.
    const { server } = require('../server/lan.cjs');
    if (config.mode === 'host' && config.open) server.once('listening', () => openBrowser(`http://localhost:${config.port}`));
}

if (require.main === module) main().catch(error => { console.error(error.message); process.exitCode = 1; });
module.exports = { options };
