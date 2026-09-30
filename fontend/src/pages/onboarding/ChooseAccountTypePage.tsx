import { useState } from 'react'
import clsx from 'clsx'
import { useNavigate } from 'react-router-dom'
import { AuthCard } from '@/components/layout/AuthCard'
import { Button } from '@/components/ui/Button'
import { Alert } from '@/components/ui/Alert'
import { useAuth } from '@/context/AuthContext'
import { companyApi } from '@/api/companyApi'
import { CheckIcon, UserIcon, UsersIcon } from '@/components/ui/icons'
import type { ReactNode } from 'react'

type AccountType = 'personal' | 'business'

export function ChooseAccountTypePage() {
  const navigate = useNavigate()
  const { user } = useAuth()
  const [selected, setSelected] = useState<AccountType>('business')
  const [isChecking, setIsChecking] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const handleContinue = async () => {
    if (selected === 'personal') {
      navigate('/onboarding/create-workspace', {
        state: { companyId: null, suggestedName: user ? `Workspace của ${user.fullName}` : undefined },
      })
      return
    }

    if (!user) return
    setError(null)
    setIsChecking(true)
    try {
      const match = await companyApi.matchByEmail(user.email)
      if (match.matched && match.company) {
        navigate('/onboarding/domain-match', { state: { company: match.company } })
      } else {
        navigate('/onboarding/company-setup')
      }
    } catch {
      setError('Không kiểm tra được domain công ty, vui lòng thử lại.')
    } finally {
      setIsChecking(false)
    }
  }

  return (
    <AuthCard maxWidth="max-w-lg">
      <p className="text-center text-xs font-semibold uppercase tracking-wide text-brand-600">
        Bước thiết lập ban đầu
      </p>
      <h1 className="mt-2 text-center text-2xl font-semibold tracking-tight text-gray-950">
        Bạn dự định sử dụng SSAM cho mục đích nào?
      </h1>
      <p className="mt-1 text-center text-sm text-gray-500">
        Bạn có thể chuyển đổi hoặc bổ sung thành viên bất kỳ lúc nào trong cài đặt dự án.
      </p>

      {error && (
        <div className="mt-4">
          <Alert tone="error">{error}</Alert>
        </div>
      )}

      <div className="mt-6 space-y-3">
        <OptionCard
          selected={selected === 'personal'}
          onSelect={() => setSelected('personal')}
          icon={<UserIcon />}
          title="Cho cá nhân"
          badge="CÁ NHÂN"
          description="Soạn thảo tài liệu, theo dõi dự án cá nhân và sử dụng AI độc lập."
          footnote="→ Truy cập trực tiếp Dashboard"
        />
        <OptionCard
          selected={selected === 'business'}
          onSelect={() => setSelected('business')}
          icon={<UsersIcon />}
          title="Cho doanh nghiệp / Tổ chức"
          badge="KHUYẾN DÙNG"
          description="Cộng tác trên toàn bộ vòng đời phát triển phần mềm (SDLC), thiết lập quy trình làm việc động và quản lý phân quyền theo chuẩn doanh nghiệp."
          footnote="☰ Chuyển tiếp đến Thiết lập Doanh nghiệp"
        />
      </div>

      <div className="mt-6">
        <Button onClick={handleContinue} isLoading={isChecking}>
          {selected === 'business' ? 'Tiếp tục thiết lập Đội ngũ →' : 'Truy cập Dashboard →'}
        </Button>
      </div>
      <p className="mt-3 text-center text-xs text-gray-400">
        Chọn &quot;Cho doanh nghiệp&quot; sẽ đưa bạn đến biểu mẫu Thiết lập Tổ chức Doanh nghiệp, hỗ trợ phân quyền và SSO.
      </p>
    </AuthCard>
  )
}

function OptionCard({
  selected,
  onSelect,
  icon,
  title,
  badge,
  description,
  footnote,
}: {
  selected: boolean
  onSelect: () => void
  icon: ReactNode
  title: string
  badge: string
  description: string
  footnote: string
}) {
  return (
    <button
      type="button"
      onClick={onSelect}
      className={clsx(
        'w-full rounded-xl border p-4 text-left transition-colors',
        selected ? 'border-brand-500 bg-brand-50 ring-1 ring-brand-500' : 'border-gray-200 hover:border-gray-300',
      )}
    >
      <div className="flex items-start gap-3">
        <span
          className={clsx(
            'flex h-9 w-9 shrink-0 items-center justify-center rounded-lg',
            selected ? 'bg-brand-600 text-white' : 'bg-gray-100 text-gray-500',
          )}
        >
          {icon}
        </span>
        <div className="min-w-0 flex-1">
          <div className="flex items-center justify-between gap-2">
            <span className="font-semibold text-gray-900">{title}</span>
            <span className="flex items-center gap-1.5">
              <span
                className={clsx(
                  'rounded px-2 py-0.5 text-[10px] font-bold',
                  selected ? 'bg-brand-600 text-white' : 'bg-gray-100 text-gray-500',
                )}
              >
                {badge}
              </span>
              {selected && <CheckIcon className="h-4 w-4 text-brand-600" />}
            </span>
          </div>
          <p className="mt-1 text-sm text-gray-600">{description}</p>
          <p className="mt-2 text-xs font-medium text-brand-700">{footnote}</p>
        </div>
      </div>
    </button>
  )
}
