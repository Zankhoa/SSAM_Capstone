import { useState } from 'react'
import { Navigate, useLocation, useNavigate } from 'react-router-dom'
import { AuthCard } from '@/components/layout/AuthCard'
import { Button } from '@/components/ui/Button'
import { Alert } from '@/components/ui/Alert'
import { companyApi } from '@/api/companyApi'
import { ApiError } from '@/lib/apiClient'
import { useAuth } from '@/context/AuthContext'

interface MatchedCompany {
  id: number
  name: string
  domain: string
  logoUrl: string | null
  autoJoinEnabled: boolean
}

export function DomainMatchPage() {
  const location = useLocation()
  const navigate = useNavigate()
  const { refreshMe } = useAuth()
  const company = (location.state as { company?: MatchedCompany } | null)?.company
  const [error, setError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  if (!company) return <Navigate to="/onboarding/account-type" replace />

  const goToPendingApproval = async () => {
    const { requestId } = await companyApi.requestToJoin(company.id)
    navigate('/onboarding/pending-approval', {
      state: { companyId: company.id, requestId, companyName: company.name },
    })
  }

  const handleJoin = async () => {
    setError(null)
    setIsSubmitting(true)
    try {
      if (company.autoJoinEnabled) {
        await companyApi.joinDirectly(company.id)
        // Nạp lại companies để Dashboard hiển thị đúng công ty vừa tham gia (không thì vẫn thấy "cá nhân").
        await refreshMe()
        navigate('/')
      } else {
        await goToPendingApproval()
      }
    } catch (err) {
      if (err instanceof ApiError && err.code === 'AUTO_JOIN_NOT_ALLOWED') {
        // Trạng thái auto-join đổi giữa lúc match và lúc bấm join — thử lại bằng đường join-request.
        try {
          await goToPendingApproval()
          return
        } catch {
          // rơi xuống thông báo lỗi chung bên dưới
        }
      }
      setError('Có lỗi xảy ra, vui lòng thử lại.')
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <AuthCard>
      <div className="rounded-xl bg-gray-50 p-6 text-center">
        <span className="inline-block rounded-full bg-brand-100 px-3 py-1 text-xs font-semibold text-brand-700">
          ĐÃ PHÁT HIỆN TÊN MIỀN: @{company.domain}
        </span>

        <h1 className="mt-4 text-xl font-semibold tracking-tight text-gray-950">Có vẻ như đội ngũ của bạn đã ở đây!</h1>
        <p className="mt-2 text-sm text-gray-600">
          Email của bạn khớp với không gian làm việc doanh nghiệp đã đăng ký của{' '}
          <span className="font-semibold">{company.name}</span>.
        </p>

        <div className="mt-4 flex items-center gap-3 rounded-lg border border-gray-200 bg-white p-3 text-left">
          {company.logoUrl ? (
            <img src={company.logoUrl} alt="" className="h-8 w-8 rounded" />
          ) : (
            <span className="flex h-8 w-8 items-center justify-center rounded bg-brand-600 text-xs font-bold text-white">
              {company.name.slice(0, 2).toUpperCase()}
            </span>
          )}
          <div>
            <p className="text-sm font-semibold text-gray-900">{company.name}</p>
            <p className="text-xs text-gray-400">{company.domain}</p>
          </div>
        </div>
      </div>

      {error && (
        <div className="mt-4">
          <Alert tone="error">{error}</Alert>
        </div>
      )}

      <div className="mt-6">
        <Button onClick={handleJoin} isLoading={isSubmitting}>
          {company.autoJoinEnabled ? `Tham gia ${company.name} →` : 'Gửi yêu cầu tham gia →'}
        </Button>
      </div>
      <button
        type="button"
        onClick={() => navigate('/onboarding/company-setup')}
        className="mt-3 w-full text-center text-sm text-gray-400 hover:text-gray-600"
      >
        Không phải công ty của bạn? Tạo công ty mới
      </button>
    </AuthCard>
  )
}
