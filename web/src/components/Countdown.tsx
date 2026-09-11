import { useEffect, useState } from 'react'

interface CountdownProps {
  deadlineUnixMs: number
  clockOffsetMs?: number // this client's clock offset from the server (SPEC 5.4) - default 0 until synced
}

export default function Countdown({ deadlineUnixMs, clockOffsetMs = 0 }: CountdownProps) {
  const estimatedServerNow = () => Date.now() + clockOffsetMs
  const [remainingMs, setRemainingMs] = useState(() => Math.max(0, deadlineUnixMs - estimatedServerNow()))

  useEffect(() => {
    const interval = setInterval(() => {    // setInterval will run setRemainingMs every 100ms, until new useEffect is being triggered again
      setRemainingMs(Math.max(0, deadlineUnixMs - estimatedServerNow()))
    }, 100)
    return () => clearInterval(interval)    // React runs this before this useEffect is executed again
  }, [deadlineUnixMs, clockOffsetMs])

  const seconds = Math.ceil(remainingMs / 1000)

  return <div className="font-display text-[0.7rem] text-amber">{seconds}s</div>
}
