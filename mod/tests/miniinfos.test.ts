import { expect, mock, test } from 'claude-code/testing'

const NOW = Date.parse('2026-10-24T12:00:00Z')

const USAGE = {
  startedAt: NOW - 3_600_000,
  context: { tokens: 88_000, window: 200_000, percent: 44 },
  rateLimits: [
    { kind: 'five_hour', percentUsed: 42.4, resetsAt: '2026-10-24T14:13:00Z' },
    { kind: 'seven_day', percentUsed: 18, resetsAt: '2026-10-27T16:00:00Z' },
  ],
  cost: { usd: 3.41 },
}

test('each chat publishes its limits, context and cost to its own feed file', async ($, on) => {
  const written: { path: string; text: string }[] = []
  mock.clock(on, { now: NOW })
  mock.env(on, { USERPROFILE: 'C:\\Users\\usuario' })
  on('session.id', () => ({ value: 'abc-123' }))
  on('session.cwd', () => ({ value: 'C:\\Users\\usuario\\Work' }))
  on('session.usage', () => ({ value: USAGE }))
  on('session.start', (_, e) => ({ cwd: e.cwd }))
  on('session.measure', (_, e) => ({ changed: [...e.changed] }))
  on('command.register', () => ({ value: { command: 'miniinfos' } }))
  on('fs.exists', () => ({ value: false }))
  on('fs.write', (_, e) => {
    written.push({ path: e.path, text: e.text })

    return { value: undefined }
  })

  await $.session.start({ cwd: '/', surface: 'desktop', isInteractive: true })
  await $.session.measure({ ...USAGE, rateLimits: [{ kind: 'five_hour', percentUsed: 50 }], changed: ['rateLimits'] })

  expect(written.length).toBe(2)
  expect(written[0]?.path).toBe('C:\\Users\\usuario\\.claude\\miniinfos\\sessions\\abc-123.json')

  const first = JSON.parse(written[0]?.text ?? '{}')
  expect(first.updatedAt).toBe(NOW)
  expect(first.cwd).toBe('C:\\Users\\usuario\\Work')
  expect(first.rateLimits).toEqual(USAGE.rateLimits)
  expect(first.context).toEqual({ tokens: 88_000, window: 200_000, percent: 44 })
  expect(first.costUsd).toBe(3.41)

  const second = JSON.parse(written[1]?.text ?? '{}')
  expect(second.rateLimits).toEqual([{ kind: 'five_hour', percentUsed: 50 }])
})

test('a chat keeps when it last answered across reloads, and stamps each new answer', async ($, on) => {
  const written: string[] = []
  const clock = mock.clock(on, { now: NOW })
  mock.env(on, { USERPROFILE: 'C:\\Users\\usuario' })
  on('session.id', () => ({ value: 'abc-123' }))
  on('session.cwd', () => ({ value: 'C:\\Users\\usuario\\Work' }))
  on('session.usage', () => ({ value: USAGE }))
  on('session.start', (_, e) => ({ cwd: e.cwd }))
  on('command.register', () => ({ value: { command: 'miniinfos' } }))
  on('fs.exists', () => ({ value: true }))
  on('fs.read',() => ({ value: JSON.stringify({ answeredAt: NOW - 60_000 }) }))
  on('fs.write', (_, e) => {
    written.push(e.text)

    return { value: undefined }
  })
  on('turn.step', async function* (_, e) {
    return {
      turnId: e.turnId,
      index: e.index,
      answer: 'ok',
      toolUses: [],
      stopReason: 'end_turn' as const,
      usage: { model: 'claude-opus-5-5', input_tokens: 1, output_tokens: 1, cache_read_input_tokens: 0, cache_creation_input_tokens: 0 },
    }
  })

  await $.session.start({ cwd: '/', surface: 'desktop', isInteractive: true })
  expect(JSON.parse(written[0] ?? '{}').answeredAt).toBe(NOW - 60_000)

  await clock.advance(5_000)
  for await (const _ of $.turn.step({ turnId: 't1', index: 0, model: 'claude-opus-5-5', messageCount: 1 })) {
    // only the result matters here
  }
  expect(JSON.parse(written[1] ?? '{}').answeredAt).toBe(NOW + 5_000)
})

test('/miniinfos opens the installed app apart from the chat', async ($, on) => {
  const ran: (readonly string[])[] = []
  mock.clock(on, { now: NOW })
  mock.env(on, { USERPROFILE: 'C:\\Users\\usuario', LOCALAPPDATA: 'C:\\Users\\usuario\\AppData\\Local' })
  on('session.id', () => ({ value: 'abc-123' }))
  on('session.cwd', () => ({ value: 'C:\\Users\\usuario\\Work' }))
  on('session.usage', () => ({ value: USAGE }))
  on('session.start', (_, e) => ({ cwd: e.cwd }))
  on('command.register', () => ({ value: { command: 'miniinfos' } }))
  on('fs.exists', (_, e) => ({ value: e.path.endsWith('MiniInfos.exe') }))
  on('fs.write', () => ({ value: undefined }))
  on('process.run', (_, e) => {
    ran.push(e.argv)

    return { value: { exitCode: 1, stdout: '', stderr: '', isStdoutTruncated: false, isStderrTruncated: false } }
  })

  await $.session.start({ cwd: '/', surface: 'desktop', isInteractive: true })
  const result = await $.command.run({
    command: 'miniinfos',
    args: '',
    origin: { kind: 'composer' },
    presentation: { isFullscreen: false, columns: 80 },
  })

  expect(ran).toEqual([['explorer.exe', 'C:\\Users\\usuario\\AppData\\Local\\Programs\\MiniInfos\\MiniInfos.exe']])
  expect(result).toEqual(expect.objectContaining({ text: 'MiniInfos aberto.' }))
})

test('a chat registers its private config location and publishes heartbeats there', async ($, on) => {
  const written: { path: string; text: string }[] = []
  const clock = mock.clock(on, { now: NOW })
  mock.env(on, { USERPROFILE: 'C:\\Users\\usuario', CLAUDE_CONFIG_DIR: 'D:\\claude-config', LOCALAPPDATA: 'C:\\Users\\usuario\\AppData\\Local' })
  on('session.id', () => ({ value: 'abc-123' }))
  on('session.cwd', () => ({ value: 'C:\\Users\\usuario\\Work' }))
  on('session.usage', () => ({ value: USAGE }))
  on('session.start', (_, e) => ({ cwd: e.cwd }))
  on('session.end', (_, e) => ({ sessionId: e.sessionId }))
  on('command.register', () => ({ value: { command: 'miniinfos' } }))
  on('fs.exists', () => ({ value: false }))
  on('fs.write', (_, e) => {
    written.push({ path: e.path, text: e.text })

    return { value: undefined }
  })

  await $.session.start({ cwd: '/', surface: 'terminal', isInteractive: true })
  expect(written[0]?.path.startsWith('C:\\Users\\usuario\\AppData\\Local\\MiniInfos\\sources\\claude-')).toBe(true)
  expect(JSON.parse(written[0]?.text ?? '{}')).toEqual({ Provider: 'claude', Root: 'D:\\claude-config' })
  expect(written[1]?.path).toBe('D:\\claude-config\\miniinfos\\sessions\\abc-123.json')
  expect(JSON.parse(written[1]?.text ?? '{}').aliveAt).toBe(NOW)

  await clock.advance(30_000)
  const alive = JSON.parse(written.at(-1)?.text ?? '{}')
  expect(alive.aliveAt).toBe(NOW + 30_000)
  expect(alive.updatedAt).toBe(NOW)
  expect(alive.rateLimits).toEqual(USAGE.rateLimits)

  await $.session.end({ reason: 'prompt_input_exit', sessionId: 'abc-123', resume: { id: 'abc-123' } })
  expect(JSON.parse(written.at(-1)?.text ?? '{}').endedAt).toBe(NOW + 30_000)
})
