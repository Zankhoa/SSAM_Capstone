import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { Logo } from '@/components/ui/Logo'

export function BrandHeader({ right }: { right?: ReactNode }) {
  return (
    <header className="flex h-16 items-center justify-between border-b border-gray-200 bg-paper/85 px-5 backdrop-blur sm:px-8">
      <Link to="/" aria-label="SSAM — về trang chủ" className="rounded-md focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-500">
        <Logo />
      </Link>
      {right && <div className="text-sm text-gray-500">{right}</div>}
    </header>
  )
}
