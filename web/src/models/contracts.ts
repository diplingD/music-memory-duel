export interface PlayerDto {
  id: string
  nick: string
  isHost: boolean
  isConnected: boolean
}

export interface CreateRoomResult {
  roomCode: string
  playerId: string
  playerToken: string
}

export interface JoinRoomResult {
  playerId: string
  playerToken: string
}

export type Phase = 'Lobby' | 'Composing' | 'Solving' | 'RoundResult' | 'MatchOver'

export interface RoomSnapshotDto {
  phase: Phase
  players: PlayerDto[]
  composerId: string | null
  roundId: string | null
  phaseDeadlineUnixMs: number | null
  standings: StandingDto[]
}

export interface NoteEvent {
  pitch: string
  tMs: number
}

export interface PlayerRoundResultDto {
  playerId: string
  submitted: boolean
  correct: boolean
  prefixRatio: number
  pointsDelta: number
  reason: string
}

export interface StandingDto {
  playerId: string
  nick: string
  score: number
  rank: number
}

export interface RoundEndedDto {
  roundId: string
  composerId: string
  composerConfirmed: boolean
  results: PlayerRoundResultDto[]
  standings: StandingDto[]
  resultDisplayDeadlineUnixMs: number
}
