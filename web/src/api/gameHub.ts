import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr'
import type {
  CreateRoomResult,
  JoinRoomResult,
  NoteEvent,
  PlayerDto,
  RoundEndedDto,
} from '../models/contracts'

const HUB_URL = 'http://localhost:5149/hubs/game'

let connection: HubConnection | null = null
let startPromise: Promise<HubConnection> | null = null

export function getConnection(): Promise<HubConnection> {
  if (connection) return Promise.resolve(connection)
  if (startPromise) return startPromise

  const conn = new HubConnectionBuilder()
    .withUrl(HUB_URL)
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Information)
    .build()

  startPromise = conn.start().then(() => {
    connection = conn
    return conn
  })

  return startPromise
}

export async function ping(): Promise<number> {
  const conn = await getConnection()
  return conn.invoke<number>('Ping')
}

export async function createRoom(nick: string): Promise<CreateRoomResult> {
  const conn = await getConnection()
  return conn.invoke<CreateRoomResult>('CreateRoom', nick)
}

export async function joinRoom(roomCode: string, nick: string): Promise<JoinRoomResult> {
  const conn = await getConnection()
  return conn.invoke<JoinRoomResult>('JoinRoom', roomCode, nick)
}

export async function startMatch(): Promise<void> {
  const conn = await getConnection()
  return conn.invoke<void>('StartMatch')
}

export async function submitSequence(notes: NoteEvent[]): Promise<void> {
  const conn = await getConnection()
  return conn.invoke<void>('SubmitSequence', notes)
}

// sends a single note live, as it's played, so others can hear/see it during Composing
export async function sendNotePlayed(note: NoteEvent): Promise<void> {
  const conn = await getConnection()
  return conn.invoke<void>('PlayNote', note)
}

// submitted during Solving — by the creator (confirmation replay) or a solver (their attempt)
export async function submitAnswer(roundId: string, notes: NoteEvent[]): Promise<void> {
  const conn = await getConnection()
  return conn.invoke<void>('SubmitAnswer', roundId, notes)
}

// --- everything below is triggered BY the backend (EffectExecutor broadcasts), not called by us ---

// triggers when 'PlayerListChanged' is beaing sent from backend - and it calls setPlayers from RoomPage.tsx
export async function onPlayerListChanged(
  callback: (players: PlayerDto[]) => void,       // param callback is function that accepts PlayerDto[] and returns void
): Promise<void> {
  const conn = await getConnection()
  conn.on('PlayerListChanged', callback)    // registred
}

// triggers when 'MatchStarted' is sent from backend (Lobby -> Composing)
export async function onMatchStarted(
  callback: (composeDeadlineUnixMs: number) => void,    // matching: setComposeDeadline(deadline), from RoomPage.tsx;
): Promise<void> {
  const conn = await getConnection()
  conn.on('MatchStarted', callback)
}

// triggers when 'NotePlayed' is sent from backend (another player's note, live, during Composing)
export async function onNotePlayed(
  callback: (note: NoteEvent) => void,
): Promise<void> {
  const conn = await getConnection()
  conn.on('NotePlayed', callback)
}

// triggers when 'SolvingStarted' is sent from backend (Composing -> Solving)
export async function onSolvingStarted(
  callback: (roundId: string, solveDeadlineUnixMs: number) => void,
): Promise<void> {
  const conn = await getConnection()
  conn.on('SolvingStarted', callback)
}

// triggers when 'RoundEnded' is sent from backend (round closed, results + standings)
export async function onRoundEnded(callback: (dto: RoundEndedDto) => void): Promise<void> {
  const conn = await getConnection()
  conn.on('RoundEnded', callback)
}

// triggers when 'ErrorOccurred' is sent from backend (e.g. not enough players to start)
export async function onErrorOccurred(
  callback: (code: string, message: string) => void,
): Promise<void> {
  const conn = await getConnection()
  conn.on('ErrorOccurred', callback)
}
