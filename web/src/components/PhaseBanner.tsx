interface PhaseBannerProps {
  text: string
}

export default function PhaseBanner({ text }: PhaseBannerProps) {
  return (
    <div className="pixel-panel w-full py-2 text-center font-display text-[0.6rem] text-note-b">
      {text}
    </div>
  )
}
