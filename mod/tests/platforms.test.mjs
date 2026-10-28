import { test } from 'node:test';
import assert from 'node:assert/strict';
import { register } from '../hooks/register.ts';

function engine(environment, existing = []) {
  const handlers = new Map();
  const writes = [];
  const commands = [];
  register((name, ...args) => handlers.set(name, args.at(-1)));
  const $ = {
    env: { get: async (name) => environment[name] },
    fs: { exists: async (path) => existing.includes(path), read: async () => '{}', write: async (path, text) => writes.push({ path, text }) },
    session: { id: async () => 'session', cwd: async () => '/work', usage: async () => ({ context: { window: 200000 }, rateLimits: [] }) },
    clock: { now: async () => 1000, every: () => {} }, command: { register: async () => {} },
    process: { run: async (args) => { commands.push(args); return { exitCode: 0 }; } },
  };
  return { $, writes, commands, start: () => handlers.get('session.start')($, {}, async () => {}), open: () => handlers.get('command.run')($) };
}
test('Linux feeds and moved configuration register in XDG data home', async () => {
  const host = engine({ HOME: '/home/test', XDG_DATA_HOME: '/data with spaces', CLAUDE_CONFIG_DIR: '/custom/claude' });
  await host.start();
  assert.ok(host.writes[0].path.startsWith('/data with spaces/MiniInfos/sources/claude-'));
  assert.equal(host.writes[1].path, '/custom/claude/miniinfos/sessions/session.json');
});
test('macOS registrations use Application Support and the command opens the installed bundle', async () => {
  const host = engine({ HOME: '/Users/test' }, ['/Users/test/Library/Application Support', '/Applications/MiniInfos.app']);
  await host.start(); await host.open();
  assert.ok(host.writes[0].path.startsWith('/Users/test/Library/Application Support/MiniInfos/sources/claude-'));
  assert.deepEqual(host.commands, [['open', '/Applications/MiniInfos.app']]);
});
test('Linux launcher paths are passed as arguments, never executable shell text', async () => {
  const launcher = '/home/test/.local/share/miniinfos-application/miniinfos';
  const host = engine({ HOME: '/home/test' }, [launcher]);
  await host.open();
  assert.equal(host.commands[0].at(-1), launcher);
  assert.ok(!host.commands[0][2].includes('/home/test'));
});
