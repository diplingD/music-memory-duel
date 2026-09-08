import type { NoteEvent } from '../models/contracts'

// The first recorded note anchors t0; every subsequent tMs is relative to it,
// so the first note is always tMs = 0.
export class SequenceCapture {
  private events: NoteEvent[] = []
  private t0: number | null = null

  record(pitch: string): NoteEvent {
    const now = performance.now()
    this.t0 ??= now

    const event: NoteEvent = { pitch, tMs: Math.round(now - this.t0) }
    this.events.push(event)
    return event
  }

  getEvents(): NoteEvent[] {
    return this.events
  }

  reset(): void {
    this.events = []
    this.t0 = null
  }
}
