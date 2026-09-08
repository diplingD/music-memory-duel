import { useEffect, useState } from 'react'

interface CountdownProps {
  deadlineUnixMs: number
}

// Local countdown from an absolute deadline. No clock-offset sync yet (that's 5.4 in SPEC.md) —
// this trusts the browser's own clock for now.
export default function Countdown({ deadlineUnixMs }: CountdownProps) {
  const [remainingMs, setRemainingMs] = useState(() => Math.max(0, deadlineUnixMs - Date.now()))

  useEffect(() => {
    const interval = setInterval(() => {
      setRemainingMs(Math.max(0, deadlineUnixMs - Date.now()))
    }, 100)
    return () => clearInterval(interval)
  }, [deadlineUnixMs])

  const seconds = Math.ceil(remainingMs / 1000)

  return <div className="font-display text-[0.7rem] text-amber">{seconds}s</div>
}
