import { useEffect, useState } from 'react'
import { Link, useLocation } from 'react-router-dom'
import { AuthCard, AuthCardIcon } from '@/components/layout/AuthCard'
import { Button } from '@/components/ui/Button'
import { Alert } from '@/components/ui/Alert'
import { authApi } from '@/api/authApi'
import { EnvelopeOpenIcon } from '@/components/ui/icons'

const RESEND_COOLDOWN_SECONDS = 30

export function VerifyEmailPendingPage() {
  const location = useLocation()
  const email = (location.state as { email?: string } | null)?.email ?? ''
  const [cooldown, setCooldown] = useState(0)
  const [isSending, setIsSending] = useState(false)
  const [sent, setSent] = useState(false)
  const [sendError, setSendError] = useState<string | null>(null)

  // Đếm ngược bằng effect để timer được dọn khi unmount (tránh setState trên component đã gỡ).
  useEffect(() => {
    if (cooldown <= 0) return
    const timer = setTimeout(() => setCooldown((c) => c - 1), 1000)
    return () => clearTimeout(timer)
  }, [cooldown])

  const handleResend = async () => {
    if (!email || cooldown > 0 || isSending) return
    setIsSending(true)
    setSendError(null)
    try {
      await authApi.resendVerification(email)
      setSent(true)
      setCooldown(RESEND_COOLDOWN_SECONDS)
    } catch {
      setSendError('Không gửi lại được email, vui lòng thử lại.')
    } finally {
      setIsSending(false)
    }
  }

  return (
    <AuthCard>
      <AuthCardIcon>
        <EnvelopeOpenIcon />
      </AuthCardIcon>
      <h1 className="text-center text-2xl font-semibold tracking-tight text-gray-950">Kiểm tra hộp thư của bạn</h1>
      <p className="mt-2 text-center text-sm text-gray-500">
        Chúng tôi đã gửi link xác thực tới{' '}
        <span className="font-medium text-gray-800">{email || 'email của bạn'}</span>. Bấm vào link trong email để
        kích hoạt tài khoản.
      </p>

      {sent && !sendError && (
        <div className="mt-4">
          <Alert tone="success">Đã gửi lại email xác thực.</Alert>
        </div>
      )}
      {sendError && (
        <div className="mt-4">
          <Alert tone="error">{sendError}</Alert>
        </div>
      )}

      <div className="mt-6">
        <Button variant="secondary" onClick={handleResend} isLoading={isSending} disabled={!email || cooldown > 0}>
          {cooldown > 0 ? `Gửi lại sau ${cooldown}s` : 'Không nhận được email? Gửi lại'}
        </Button>
      </div>

      <p className="mt-6 text-center text-sm text-gray-500">
        <Link to="/login" className="font-medium text-brand-700">
          Quay lại đăng nhập
        </Link>
      </p>
    </AuthCard>
  )
}
