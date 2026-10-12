const os = require('node:os');

function lanAddresses() {
    return [...new Set(Object.entries(os.networkInterfaces())
        .filter(([name]) => !/^(utun|tun|docker|veth|br-|bridge|tailscale|vmnet)/i.test(name))
        .flatMap(([, list]) => list || [])
        .filter(n => n.family === 'IPv4' && !n.internal)
        .map(n => n.address))];
}

module.exports = { lanAddresses };
