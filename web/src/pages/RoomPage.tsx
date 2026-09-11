import { useEffect, useRef, useState } from 'react'
import {
  createRoom,
  joinRoom,
  onErrorOccurred,
  onMatchEnded,
  onMatchStarted,
  onNotePlayed,
  onPlayerListChanged,
  onRoundEnded,
  onSolvingStarted,
  sendNotePlayed,
  startMatch,
  submitAnswer,
  submitSequence,
} from '../api/gameHub'
import type { NoteEvent, PlayerDto, RoundEndedDto, StandingDto } from '../models/contracts'
import { SequenceCapture } from '../services/capture'
import { syncClock } from '../services/clock'
import { playNote } from '../services/synth'
import Lobby from '../components/Lobby'
import Keyboard from '../components/Keyboard'
import PhaseBanner from '../components/PhaseBanner'
import Countdown from '../components/Countdown'
import PianoRoll from '../components/PianoRoll'
import Scoreboard from '../components/Scoreboard'

type Phase = 'lobby' | 'composing' | 'solving' | 'roundResult' | 'matchOver'

export default function RoomPage() {
  const [roomCode, setRoomCode] = useState<string | null>(null)
  const [playerId, setPlayerId] = useState<string | null>(null)
  const [players, setPlayers] = useState<PlayerDto[]>([])
  const [phase, setPhase] = useState<Phase>('lobby')
  const [composeDeadline, setComposeDeadline] = useState<number | null>(null)
  const [composerId, setComposerId] = useState<string | null>(null)
  const [liveNotes, setLiveNotes] = useState<NoteEvent[]>([])
  const [solveDeadline, setSolveDeadline] = useState<number | null>(null)
  const [roundId, setRoundId] = useState<string | null>(null)
  const [answered, setAnswered] = useState(false)
  const [lastRoundResult, setLastRoundResult] = useState<RoundEndedDto | null>(null)
  const [finalStandings, setFinalStandings] = useState<StandingDto[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [clockOffsetMs, setClockOffsetMs] = useState(0)
  const captureRef = useRef(new SequenceCapture())
  const answerCaptureRef = useRef(new SequenceCapture())

  const isHost = players.some((p) => p.id === playerId && p.isHost)
  const isComposer = composerId !== null && composerId === playerId

  useEffect(() => {   // this is called only once, on first build
    onPlayerListChanged(setPlayers).catch((err) => setError((err as Error).message))
    onMatchStarted((deadline, newComposerId) => {
      captureRef.current.reset()
      setLiveNotes([])
      setComposeDeadline(deadline)
      setComposerId(newComposerId)
      setPhase('composing')
    }).catch((err) => setError((err as Error).message))
    onNotePlayed((note) => {
      playNote(note.pitch).catch((err) => setError((err as Error).message))
      setLiveNotes((prev) => [...prev, note])
    }).catch((err) => setError((err as Error).message))
    onSolvingStarted((newRoundId, deadline) => {
      answerCaptureRef.current.reset()
      setRoundId(newRoundId)
      setSolveDeadline(deadline)
      setAnswered(false)
      setPhase('solving')
    }).catch((err) => setError((err as Error).message))
    onRoundEnded((dto) => {
      setLastRoundResult(dto)
      setPhase('roundResult')
    }).catch((err) => setError((err as Error).message))
    onMatchEnded((standings) => {
      setFinalStandings(standings)
      setPhase('matchOver')
    }).catch((err) => setError((err as Error).message))
    onErrorOccurred((_code, message) => setError(message)).catch((err) =>
      setError((err as Error).message),
    )
  }, [])

  async function handleCreate(nick: string) {
    setError(null)
    try {
      const result = await createRoom(nick)
      setRoomCode(result.roomCode)
      setPlayerId(result.playerId)
      syncClock().then(setClockOffsetMs).catch((err) => setError((err as Error).message))
    } catch (err) {
      setError((err as Error).message)
    }
  }

  async function handleJoin(code: string, nick: string) {
    setError(null)
    try {
      const result = await joinRoom(code, nick)
      setRoomCode(code)
      setPlayerId(result.playerId)
      syncClock().then(setClockOffsetMs).catch((err) => setError((err as Error).message))
    } catch (err) {
      setError((err as Error).message)
    }
  }

  async function handleStartMatch() {
    setError(null)
    try {
      await startMatch()
    } catch (err) {
      setError((err as Error).message)
    }
  }

  function handleKeyPress(pitch: string) {
    playNote(pitch).catch((err) => setError((err as Error).message))
    const note = captureRef.current.record(pitch)
    setLiveNotes((prev) => [...prev, note])
    sendNotePlayed(note).catch((err) => setError((err as Error).message))
  }

  async function handleSubmit() {
    setError(null)
    try {
      await submitSequence(captureRef.current.getEvents())
    } catch (err) {
      setError((err as Error).message)
    }
  }

  function handleAnswerKeyPress(pitch: string) {
    playNote(pitch).catch((err) => setError((err as Error).message))
    answerCaptureRef.current.record(pitch)
  }

  async function handleSubmitAnswer() {
    if (roundId === null) return
    setError(null)
    setAnswered(true) // disable immediately, before the round-trip, so a fast double-click can't submit twice
    try {
      await submitAnswer(roundId, answerCaptureRef.current.getEvents())
    } catch (err) {
      setAnswered(false) // let them retry if the request itself failed
      setError((err as Error).message)
    }
  }

  if (!roomCode) {
    return (
      <div className="flex min-h-screen flex-col items-center justify-center gap-6 px-4">
        <h1 className="text-shadow-signature text-center text-lg font-display tracking-wide sm:text-2xl">
          MUSIC MEMORY DUEL
        </h1>
        <Lobby onCreate={handleCreate} onJoin={handleJoin} />
        {error && <p className="font-display text-[0.6rem] text-note-a">{error}</p>}
      </div>
    )
  }

  return (
    <div className="flex min-h-screen flex-col items-center gap-6 px-4 py-10">
      <div className="w-full max-w-md font-display text-[0.55rem] text-note-b">
        ROOM #{roomCode}
      </div>

      {phase === 'lobby' && (
        <>
          <div className="flex w-full max-w-md flex-wrap justify-center gap-2">
            {players.map((p) => (
              <div key={p.id} className="pixel-panel min-w-[120px] flex-1 px-3 py-2 text-center">
                <div className="font-display text-[0.6rem] text-amber">{p.nick}</div>
                {p.isHost && <div className="mt-1 text-[0.6rem] text-ink-soft">HOST</div>}
              </div>
            ))}
          </div>

          {isHost && (
            <button
              type="button"
              onClick={handleStartMatch}
              disabled={players.length < 2}
              className="pixel-button pixel-button--accent px-4 py-3 text-[0.6rem]"
            >
              START MATCH
            </button>
          )}
        </>
      )}

      {phase === 'composing' && composeDeadline !== null && (
        <div className="flex w-full max-w-2xl flex-col items-center gap-4">
          <PhaseBanner
            text={isComposer ? 'COMPOSE THE MELODY' : 'LISTEN TO THE MELODY'}
          />
          <Countdown deadlineUnixMs={composeDeadline} clockOffsetMs={clockOffsetMs} />
          <PianoRoll notes={liveNotes} />
          {isComposer ? (
            <>
              <Keyboard onPress={handleKeyPress} />
              <button
                type="button"
                onClick={handleSubmit}
                className="pixel-button pixel-button--accent px-4 py-3 text-[0.6rem]"
              >
                SUBMIT
              </button>
            </>
          ) : (
            <div className="font-display text-[0.6rem] text-ink-soft">
              {players.find((p) => p.id === composerId)?.nick ?? 'SOMEONE'} IS COMPOSING...
            </div>
          )}
        </div>
      )}

      {phase === 'solving' && solveDeadline !== null && (
        <div className="flex w-full max-w-2xl flex-col items-center gap-4">
          <PhaseBanner text="YOUR TURN - REPEAT THE MELODY" />
          <Countdown deadlineUnixMs={solveDeadline} clockOffsetMs={clockOffsetMs} />
          <Keyboard onPress={handleAnswerKeyPress} />
          <button
            type="button"
            onClick={handleSubmitAnswer}
            disabled={answered}
            className="pixel-button pixel-button--accent px-4 py-3 text-[0.6rem]"
          >
            {answered ? 'WAITING FOR OTHERS...' : 'SUBMIT'}
          </button>
        </div>
      )}

      {phase === 'roundResult' && lastRoundResult && (
        <div className="flex w-full max-w-2xl flex-col items-center gap-4">
          <PhaseBanner text="NEXT ROUND IN" />
          <Countdown
            deadlineUnixMs={lastRoundResult.resultDisplayDeadlineUnixMs}
            clockOffsetMs={clockOffsetMs}
          />

          {(() => {
            const myResult = lastRoundResult.results.find((r) => r.playerId === playerId)
            if (!myResult) return null
            const color =
              myResult.pointsDelta > 0
                ? 'text-note-b'
                : myResult.pointsDelta < 0
                  ? 'text-note-a'
                  : 'text-ink-soft'
            return (
              <div className={`text-center font-display text-2xl ${color}`}>
                {myResult.pointsDelta >= 0 ? '+' : ''}
                {myResult.pointsDelta}
                <div className="mt-1 text-[0.6rem] text-ink-soft">
                  {myResult.reason.replaceAll('_', ' ')}
                </div>
              </div>
            )
          })()}

          <div className="pixel-panel flex w-full flex-col gap-3 px-4 py-3">
            <div className="font-display text-[0.6rem] text-note-b">
              composer {lastRoundResult.composerConfirmed ? 'confirmed' : 'did not confirm'}
            </div>
            {lastRoundResult.standings.map((s) => (
              <div key={s.playerId} className="flex items-center justify-between text-xl">
                <span>
                  #{s.rank} {s.nick}
                </span>
                <span className="text-amber">{s.score} pts</span>
              </div>
            ))}
          </div>
        </div>
      )}

      {phase === 'matchOver' && finalStandings && (
        <div className="flex w-full max-w-2xl flex-col items-center gap-4">
          <PhaseBanner text="MATCH OVER" />
          <Scoreboard standings={finalStandings} />
        </div>
      )}

      {error && <p className="font-display text-[0.6rem] text-note-a">{error}</p>}
    </div>
  )
}
