import { useState, type ReactNode } from 'react'
import { Link, useLocation } from 'react-router-dom'
import clsx from 'clsx'
import { useAuth } from '@/context/AuthContext'
import { Logo } from '@/components/ui/Logo'
import {
  BellIcon,
  ChevronDownIcon,
  DocumentIcon,
  FolderIcon,
  HomeIcon,
  ListChecksIcon,
  LogoutIcon,
  UsersIcon,
} from '@/components/ui/icons'

interface NavItem {
  label: string
  icon: ReactNode
  to?: string
  comingSoon?: boolean
}

const NAV_ITEMS: NavItem[] = [
  { label: 'Tổng quan', icon: <HomeIcon className="h-5 w-5" />, to: '/' },
  { label: 'Dự án', icon: <FolderIcon className="h-5 w-5" />, comingSoon: true },
  { label: 'Tài liệu yêu cầu', icon: <DocumentIcon className="h-5 w-5" />, comingSoon: true },
  { label: 'Testcase', icon: <ListChecksIcon className="h-5 w-5" />, comingSoon: true },
  { label: 'Thành viên & Phân quyền', icon: <UsersIcon className="h-5 w-5" />, to: '/members/invite' },
]

export function DashboardLayout({ children }: { children: ReactNode }) {
  const { user, companies } = useAuth()
  const location = useLocation()
  const [workspaceMenuOpen, setWorkspaceMenuOpen] = useState(false)
  const currentCompany = companies[0]?.companyName ?? 'Không gian cá nhân'

  return (
    <div className="flex min-h-screen bg-gray-50">
      <aside className="flex w-64 shrink-0 flex-col border-r border-gray-200 bg-white">
        <div className="flex h-16 items-center border-b border-gray-200 px-5">
          <Logo />
        </div>

        <nav className="flex-1 space-y-1 p-3">
          {NAV_ITEMS.map((item) => {
            const isActive = item.to !== undefined && item.to === location.pathname
            const itemClassName = clsx(
              'flex w-full items-center gap-3 rounded-lg px-3 py-2 text-sm font-medium transition-colors',
              isActive
                ? 'bg-brand-50 text-brand-700'
                : item.comingSoon
                  ? 'cursor-not-allowed text-gray-300'
                  : 'text-gray-600 hover:bg-gray-50',
            )

            if (item.comingSoon || !item.to) {
              return (
                <button key={item.label} type="button" disabled title="Sắp ra mắt" className={itemClassName}>
                  {item.icon}
                  <span className="flex-1 text-left">{item.label}</span>
                  <span className="rounded bg-gray-100 px-1.5 py-0.5 text-[9px] font-semibold text-gray-400">
                    SỚM
                  </span>
                </button>
              )
            }

            return (
              <Link key={item.label} to={item.to} className={itemClassName}>
                {item.icon}
                <span className="flex-1 text-left">{item.label}</span>
              </Link>
            )
          })}
        </nav>

        <div className="border-t border-gray-200 p-3">
          <div className="flex items-center gap-2 rounded-lg px-2 py-2">
            <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-brand-100 text-sm font-semibold text-brand-700">
              {(user?.fullName ?? '?').slice(0, 1).toUpperCase()}
            </span>
            <div className="min-w-0 flex-1">
              <p className="truncate text-sm font-medium text-gray-900">{user?.fullName}</p>
              <p className="truncate text-xs text-gray-400">{user?.email}</p>
            </div>
          </div>
        </div>
      </aside>

      <div className="flex flex-1 flex-col">
        <header className="flex h-16 items-center justify-between border-b border-gray-200 bg-white px-6">
          <div className="relative">
            <button
              type="button"
              onClick={() => setWorkspaceMenuOpen((v) => !v)}
              className="flex items-center gap-2 rounded-lg px-2 py-1.5 text-sm font-medium text-gray-700 hover:bg-gray-50"
            >
              {currentCompany}
              <ChevronDownIcon className="h-4 w-4 text-gray-400" />
            </button>
            {workspaceMenuOpen && (
              <div className="absolute left-0 top-full mt-1 w-56 rounded-lg border border-gray-200 bg-white p-1 text-sm shadow-lg">
                <p className="px-3 py-2 text-xs text-gray-400">
                  Chuyển đổi không gian làm việc — sắp ra mắt
                </p>
              </div>
            )}
          </div>

          <div className="flex items-center gap-3">
            <button type="button" title="Sắp ra mắt" className="rounded-lg p-2 text-gray-400 hover:bg-gray-50">
              <BellIcon className="h-5 w-5" />
            </button>
            <LogoutButton />
          </div>
        </header>

        <main className="flex-1 p-6">{children}</main>
      </div>
    </div>
  )
}

function LogoutButton() {
  const { logout } = useAuth()
  return (
    <button
      type="button"
      onClick={() => void logout()}
      className="flex items-center gap-2 rounded-lg px-3 py-1.5 text-sm font-medium text-gray-600 hover:bg-gray-50"
    >
      <LogoutIcon className="h-4 w-4" />
      Đăng xuất
    </button>
  )
}

export function DashboardEmptyState({
  icon,
  title,
  description,
}: {
  icon: ReactNode
  title: string
  description: string
}) {
  return (
    <div className="flex flex-col items-center justify-center rounded-xl border border-dashed border-gray-200 bg-white px-6 py-10 text-center">
      <span className="flex h-10 w-10 items-center justify-center rounded-lg bg-gray-100 text-gray-400">{icon}</span>
      <p className="mt-3 text-sm font-semibold text-gray-700">{title}</p>
      <p className="mt-1 max-w-xs text-xs text-gray-400">{description}</p>
    </div>
  )
}

