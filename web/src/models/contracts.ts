export interface PlayerDto {
  id: string
  nick: string
  isHost: boolean
}

export interface CreateRoomResult {
  roomCode: string
  playerId: string
}

export interface JoinRoomResult {
  playerId: string
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
  creatorId: string
  creatorConfirmed: boolean
  results: PlayerRoundResultDto[]
  standings: StandingDto[]
  resultDisplayDeadlineUnixMs: number
}
