import type { EngineInterface, Register } from 'claude-code'

const FEED_FOLDER = 'miniinfos/sessions'
const APP_PATH = 'Programs/MiniInfos/MiniInfos.exe'
const COMMAND = 'miniinfos'
const ALIVE_EVERY = 30_000

type Measured = {
  context: { tokens?: number; window: number; percent?: number }
  rateLimits: readonly { kind: string; percentUsed: number; resetsAt?: string }[]
  cost?: { usd: number }
}

let feed: { path: string; sessionId: string; cwd: string } | undefined
let answeredAt: number | undefined
let latest: Record<string, unknown> | undefined

// Where Claude Code keeps its own files: ~/.claude, unless CLAUDE_CONFIG_DIR moved it.
async function configRoot($: EngineInterface) {
  const moved = await $.env.get('CLAUDE_CONFIG_DIR')
  if (moved !== undefined) return moved

  const home = (await $.env.get('USERPROFILE')) ?? (await $.env.get('HOME'))
  return home === undefined ? undefined : `${home}/.claude`
}

async function locateFeed($: EngineInterface) {
  const root = await configRoot($)
  if (root === undefined) return

  const sessionId = await $.session.id()
  const path = `${root}/${FEED_FOLDER}/${sessionId}.json`
  feed = { path, sessionId, cwd: await $.session.cwd() }
  answeredAt = await lastAnswer($, path)
  await registerSource($, root)
}

// A chat can use a private CLAUDE_CONFIG_DIR that the widget did not inherit. Publish the location only.
async function registerSource($: EngineInterface, root: string) {
  const local = await $.env.get('LOCALAPPDATA')
  if (local === undefined) return
  let hash = 0xcbf29ce484222325n
  for (const char of root) hash = BigInt.asUintN(64, (hash ^ BigInt(char.codePointAt(0) ?? 0)) * 0x100000001b3n)
  try {
    await $.fs.write(`${local}/MiniInfos/sources/claude-${hash.toString(16)}.json`,
      JSON.stringify({ Provider: 'claude', Root: root }))
  } catch {
    // A blocked registration folder must not stop the chat or its existing feed.
  }
}

// A reload starts the module over: the chat's last answer is kept in its own feed file.
async function lastAnswer($: EngineInterface, path: string) {
  if (!(await $.fs.exists(path))) return undefined

  try {
    const previous = JSON.parse(await $.fs.read(path)) as { answeredAt?: unknown }
    return typeof previous.answeredAt === 'number' ? previous.answeredAt : undefined
  } catch {
    return undefined
  }
}

async function publish($: EngineInterface, measured?: Measured) {
  if (feed === undefined) return

  const usage = measured ?? (await $.session.usage())
  latest = {
    sessionId: feed.sessionId,
    cwd: feed.cwd,
    updatedAt: await $.clock.now(),
    answeredAt,
    rateLimits: usage.rateLimits.map(({ kind, percentUsed, resetsAt }) => ({ kind, percentUsed, resetsAt })),
    context: { tokens: usage.context.tokens, window: usage.context.window, percent: usage.context.percent },
    costUsd: usage.cost?.usd,
  }
  await writeFeed($)
}

// The feed also says whether the chat is still open, wherever it runs (the app, a terminal,
// an editor): `aliveAt` is rewritten every ALIVE_EVERY, and `endedAt` is stamped on the way out.
async function writeFeed($: EngineInterface, endedAt?: number) {
  if (feed === undefined || latest === undefined) return

  await $.fs.write(feed.path, JSON.stringify({ ...latest, aliveAt: await $.clock.now(), endedAt }))
}

async function ended($: EngineInterface) {
  await writeFeed($, await $.clock.now())
}

async function answered($: EngineInterface) {
  answeredAt = await $.clock.now()
  await publish($)
}

async function openApp($: EngineInterface) {
  const localAppData = await $.env.get('LOCALAPPDATA')
  const app = localAppData === undefined ? undefined : `${localAppData}/${APP_PATH}`
  if (app === undefined || !(await $.fs.exists(app))) {
    return { text: 'O MiniInfos não está instalado em %LOCALAPPDATA%\\Programs\\MiniInfos.' }
  }

  // Explorer starts it apart from this chat, so it stays open after the command ends;
  // if it is open already, the app brings that window back instead.
  await $.process.run(['explorer.exe', app.replaceAll('/', '\\')])

  return { text: 'MiniInfos aberto.' }
}

export const register: Register = on => {
  on('session.start', async ($, e, next) => {
    await $.command.register({
      name: COMMAND,
      description: 'Abre o MiniInfos, o quadradinho com os limites de 5h e semanal e os tokens',
    })
    await locateFeed($)
    await publish($)
    $.clock.every(ALIVE_EVERY, () => void writeFeed($))

    return next(e)
  })

  on('session.end', async ($, e, next) => {
    await ended($)

    return next(e)
  })

  on('command.run', { command: COMMAND }, async $ => openApp($))

  on('session.measure', async ($, e, next) => {
    await publish($, e)

    return next(e)
  })

  on('turn.step', async function* ($, e, next) {
    const response = yield* next(e)
    if (response.usage !== null) await answered($)

    return response
  })
}
