import type { ReactNode } from 'react'
import clsx from 'clsx'
import { Link } from 'react-router-dom'
import { BrandHeader } from './BrandHeader'
import { Logo } from '@/components/ui/Logo'

// Khung chung cho mọi trang Auth/Onboarding/Invite. Cùng ngôn ngữ với Landing: nền giấy, hairline,
// mono cho meta; cột trái tối kể lại câu chuyện "bảng truy xuất" để người dùng không quên mình đang
// đăng nhập vào cái gì. Dưới lg chỉ còn header + card, không mất gì về chức năng.
export function AuthCard({
  children,
  headerRight,
  maxWidth = 'max-w-md',
}: {
  children: ReactNode
  headerRight?: ReactNode
  maxWidth?: string
}) {
  return (
    <div className="min-h-screen bg-paper lg:grid lg:grid-cols-[22rem_minmax(0,1fr)] xl:grid-cols-[26rem_minmax(0,1fr)]">
      <AuthSidebar />

      <div className="flex min-h-screen flex-col">
        <div className="lg:hidden">
          <BrandHeader right={headerRight} />
        </div>
        <div className="hidden h-16 items-center justify-between px-8 text-sm text-gray-500 lg:flex">
          <Link to="/" className="inline-flex items-center gap-1.5 rounded-md transition-colors hover:text-gray-900 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-500">
            <span aria-hidden="true">←</span> Về trang chủ
          </Link>
          {headerRight && <span className="font-mono text-[11px] uppercase tracking-[0.14em]">{headerRight}</span>}
        </div>

        <main className="flex flex-1 items-start justify-center px-4 pb-16 pt-6 sm:pt-10 lg:pt-8">
          <div className={clsx('w-full rounded-lg border border-gray-200 bg-white p-6 shadow-sm shadow-gray-900/[0.04] sm:p-8', maxWidth)}>
            {children}
          </div>
        </main>
      </div>
    </div>
  )
}

const SIDEBAR_TRACE = [
  { req: 'REQ-012', ui: 'UI-07', tc: 'TC-031', drift: false },
  { req: 'REQ-013', ui: 'UI-08', tc: 'TC-032', drift: true },
  { req: 'REQ-014', ui: 'UI-09', tc: 'TC-033', drift: false },
]

function AuthSidebar() {
  return (
    <aside className="relative hidden overflow-hidden bg-gray-950 text-white lg:flex lg:flex-col lg:justify-between lg:p-10">
      <div
        aria-hidden="true"
        className="pointer-events-none absolute inset-0 bg-[radial-gradient(70%_45%_at_50%_0%,rgb(18_183_106/0.18),transparent_70%)]"
      />
      <div
        aria-hidden="true"
        className="pointer-events-none absolute inset-0 bg-[linear-gradient(to_right,rgb(255_255_255/0.04)_1px,transparent_1px),linear-gradient(to_bottom,rgb(255_255_255/0.04)_1px,transparent_1px)] bg-[size:40px_40px]"
      />

      <Link to="/" aria-label="SSAM — về trang chủ" className="relative w-fit rounded-md focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-400">
        <Logo inverted />
      </Link>

      <div className="relative">
        <p className="font-mono text-xs uppercase tracking-[0.18em] text-brand-300">// bảng truy xuất</p>
        <p className="mt-4 text-2xl font-semibold leading-snug tracking-tight">
          Một nguồn sự thật cho yêu cầu, thiết kế và kiểm thử.
        </p>
        <ul className="mt-8 space-y-2.5 font-mono text-[12px]">
          {SIDEBAR_TRACE.map((row) => (
            <li key={row.req} className="flex items-center gap-2 text-gray-300">
              <span className="text-white">{row.req}</span>
              <span className={clsx('h-px flex-1', row.drift ? 'border-t border-dashed border-amber-400' : 'bg-brand-500')} aria-hidden="true" />
              <span>{row.ui}</span>
              <span className={clsx('h-px flex-1', row.drift ? 'border-t border-dashed border-amber-400' : 'bg-brand-500')} aria-hidden="true" />
              <span className={row.drift ? 'text-amber-300' : 'text-white'}>{row.tc}</span>
              <span
                className={clsx('h-1.5 w-1.5 rounded-full', row.drift ? 'bg-amber-400' : 'bg-brand-400')}
                aria-label={row.drift ? 'cần cập nhật' : 'đã liên kết'}
              />
            </li>
          ))}
        </ul>
        <p className="mt-6 text-sm leading-relaxed text-gray-400">
          Yêu cầu đổi thì testcase liên quan được gắn cờ. Không còn bộ test &ldquo;xanh&rdquo; nhưng sai yêu cầu.
        </p>
      </div>

      <p className="relative font-mono text-[11px] uppercase tracking-[0.14em] text-gray-500">
        SEP490-G65 · FPT University
      </p>
    </aside>
  )
}

export function AuthCardIcon({ children }: { children: ReactNode }) {
  return (
    <div className="mx-auto mb-4 flex h-12 w-12 items-center justify-center rounded-md border border-brand-100 bg-brand-50 text-brand-700">
      {children}
    </div>
  )
}
