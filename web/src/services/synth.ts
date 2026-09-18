import * as Tone from 'tone'

let synth: Tone.PolySynth | null = null

function getSynth(): Tone.PolySynth {
  synth ??= new Tone.PolySynth(Tone.Synth).toDestination()
  return synth
}

// Tone.start() must run from a user gesture (autoplay policy). Like turning on a keyboard to be played
async function ensureAudioStarted(): Promise<void> {
  if (Tone.getContext().state === 'running') return
  await Tone.start()
}

export async function playNote(pitch: string): Promise<void> {
  await ensureAudioStarted()
  getSynth().triggerAttackRelease(pitch, '8n')
}
