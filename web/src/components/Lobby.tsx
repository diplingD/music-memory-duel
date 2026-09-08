import { useState } from 'react'

interface LobbyProps {
  onCreate: (nick: string) => void
  onJoin: (roomCode: string, nick: string) => void
}

export default function Lobby({ onCreate, onJoin }: LobbyProps) {
  const [nick, setNick] = useState('')
  const [roomCode, setRoomCode] = useState('')

  return (
    <div className="pixel-panel flex w-full max-w-xs flex-col gap-4 p-6">
      <input
        value={nick}
        onChange={(e) => setNick(e.target.value)}
        placeholder="NICKNAME"
        className="pixel-input px-3 py-2 text-lg placeholder:text-ink-soft"
      />
      <button
        type="button"
        onClick={() => onCreate(nick)}
        disabled={!nick}
        className="pixel-button px-4 py-3 text-[0.6rem]"
      >
        CREATE ROOM
      </button>

      <div className="text-center text-[0.55rem] text-ink-soft">— OR —</div>

      <div className="flex flex-col gap-2">
        <input
          value={roomCode}
          onChange={(e) => setRoomCode(e.target.value.toUpperCase())}
          placeholder="ROOM CODE"
          className="pixel-input px-3 py-2 text-lg uppercase placeholder:text-ink-soft"
        />
        <button
          type="button"
          onClick={() => onJoin(roomCode, nick)}
          disabled={!nick || !roomCode}
          className="pixel-button pixel-button--accent px-4 py-3 text-[0.6rem]"
        >
          JOIN ROOM
        </button>
      </div>
    </div>
  )
}
