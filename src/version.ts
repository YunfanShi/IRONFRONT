import { version } from '../package.json';

export const VERSION = version;
export const LAN_VERSION_PREFIX = version.split('.').slice(0, 2).join('.') + '.';
