import type { StandingDto } from '../models/contracts'

export default function Scoreboard({ standings }: { standings: StandingDto[] }) {
  return (
    <div className="pixel-panel flex w-full flex-col gap-3 px-4 py-3">
      {standings.map((s) => (
        <div key={s.playerId} className="flex items-center justify-between text-xl">
          <span>
            #{s.rank} {s.nick}
          </span>
          <span className="text-amber">{s.score} pts</span>
        </div>
      ))}
    </div>
  )
}
