import { useEffect, useState } from 'react'
import { PITCHES, SHORTCUTS } from '../utils/pitches'

// Pitch name (Tone.js format, e.g. "C4") paired with its physical keyboard shortcut.
const KEYS = PITCHES.map((pitch, i) => ({ pitch, shortcut: SHORTCUTS[i] }))

const IDLE_GRADIENT = 'linear-gradient(180deg, #FBF9F4, #E9E6DE 78%, #D6D2C8)'
const PRESSED_GRADIENT = 'linear-gradient(180deg, #FFD9E5, #FF8FB4 78%, var(--color-note-a))'

interface KeyboardProps {
  onPress?: (pitch: string) => void
}

export default function Keyboard({ onPress }: KeyboardProps) {
  const [activePitch, setActivePitch] = useState<string | null>(null)

  // enters initialy on first render, and if RoomPage changes something else that needs new Keyboard re-render
  useEffect(() => {
    function handleKeyDown(e: KeyboardEvent) {
      const key = KEYS.find((k) => k.shortcut === e.key.toLowerCase())
      if (!key) return
      setActivePitch(key.pitch)
      onPress?.(key.pitch)
    }
    function handleKeyUp(e: KeyboardEvent) {
      const key = KEYS.find((k) => k.shortcut === e.key.toLowerCase())
      if (!key) return
      setActivePitch((current) => (current === key.pitch ? null : current))
    }

    window.addEventListener('keydown', handleKeyDown)
    window.addEventListener('keyup', handleKeyUp)

    return () => {
      window.removeEventListener('keydown', handleKeyDown)
      window.removeEventListener('keyup', handleKeyUp)
    }
  }, [onPress])

  function handlePointerDown(pitch: string) {
    setActivePitch(pitch)
    onPress?.(pitch)
  }

  function release(pitch: string) {
    setActivePitch((current) => (current === pitch ? null : current))
  }

  return (
    <div className="flex w-full flex-col items-center gap-4">
      <p className="text-center font-display text-[0.55rem] text-ink-soft">
        CLICK OR PRESS Z X C V B N M ,
      </p>

      <div className="grid w-full grid-cols-8 gap-1.5">
        {KEYS.map(({ pitch, shortcut }, i) => {
          const pressed = activePitch === pitch
          return (
            <button
              key={pitch}
              type="button"
              onPointerDown={() => handlePointerDown(pitch)}
              onPointerUp={() => release(pitch)}
              onPointerLeave={() => release(pitch)}
              style={{ background: pressed ? PRESSED_GRADIENT : IDLE_GRADIENT }}
              className={`relative flex h-[120px] flex-col items-center justify-end gap-1 border-[3px] border-outline pb-2.5 shadow-[3px_3px_0_var(--color-outline)] transition-transform duration-75 ${
                pressed ? 'translate-x-[3px] translate-y-[3px] shadow-none' : ''
              }`}
            >
              <span
                className="absolute inset-x-0 top-0 h-1.5"
                style={{
                  background: pressed
                    ? 'var(--color-amber)'
                    : i % 2 === 0
                      ? 'var(--color-note-b)'
                      : 'var(--color-note-a)',
                }}
              />
              <span className={`font-display text-[0.78rem] ${pressed ? 'text-outline' : 'text-bg-3'}`}>
                {pitch[0]}
              </span>
              <span className={`font-mono text-[1.1rem] uppercase ${pressed ? 'text-[#FFF0F5]' : 'text-ink-soft'}`}>
                {shortcut}
              </span>
            </button>
          )
        })}
      </div>
    </div>
  )
}
