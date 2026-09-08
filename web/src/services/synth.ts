import * as Tone from 'tone'

let synth: Tone.PolySynth | null = null
let started = false

function getSynth(): Tone.PolySynth {
  synth ??= new Tone.PolySynth(Tone.Synth).toDestination()
  return synth
}

// Tone.start() must run from a user gesture (autoplay policy) — safe to call on every
// key press, it's a no-op after the first successful call.
async function ensureAudioStarted(): Promise<void> {
  if (started) return
  await Tone.start()
  started = true
}

export async function playNote(pitch: string): Promise<void> {
  await ensureAudioStarted()
  getSynth().triggerAttackRelease(pitch, '8n')
}
