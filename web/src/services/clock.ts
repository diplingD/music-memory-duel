import { ping } from '../api/gameHub'

// Measures how far this client's clock is from the server's (SPEC 5.4)
export async function syncClock(): Promise<number> {
  const offsets: number[] = []

  for (let i = 0; i < 5; i++) {
    const t0 = performance.now()
    const serverNowMs = await ping()
    const rtt = performance.now() - t0
    offsets.push(serverNowMs + rtt / 2 - Date.now())
  }

  offsets.sort((a, b) => a - b)
  return offsets[2] // median of 5
}
