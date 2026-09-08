import type { NoteEvent } from '../models/contracts'
import { PITCHES } from '../utils/pitches'

interface PianoRollProps {
  notes: NoteEvent[]
}

const PX_PER_MS = 0.08
const NOTE_WIDTH = 40
const LANE_HEIGHT = 100 / PITCHES.length

export default function PianoRoll({ notes }: PianoRollProps) {
  const maxTMs = notes.length > 0 ? Math.max(...notes.map((n) => n.tMs)) : 0
  const timelineWidth = Math.max(320, maxTMs * PX_PER_MS + NOTE_WIDTH + 20)

  return (
    <div className="w-full overflow-x-auto border-[3px] border-outline bg-screen">
      <div className="relative h-[200px]" style={{ width: timelineWidth }}>
        {PITCHES.map((_, i) => (
          <div
            key={i}
            className="absolute inset-x-0 border-t border-dashed border-white/10"
            style={{ top: `${i * LANE_HEIGHT}%` }}
          />
        ))}

        {notes.map((note, i) => {
          const laneIndex = PITCHES.indexOf(note.pitch)
          if (laneIndex === -1) return null

          return (
            <div
              key={i}
              className={`absolute h-[9%] shadow-[3px_3px_0_var(--color-outline)] ${
                i % 2 === 0 ? 'bg-note-a' : 'bg-note-b'
              }`}
              style={{
                left: note.tMs * PX_PER_MS,
                top: `${laneIndex * LANE_HEIGHT + LANE_HEIGHT * 0.2}%`,
                width: NOTE_WIDTH,
              }}
            />
          )
        })}
      </div>
    </div>
  )
}
