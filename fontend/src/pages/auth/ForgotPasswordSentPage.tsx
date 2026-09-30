import { Link, useLocation } from 'react-router-dom'
import { AuthCard, AuthCardIcon } from '@/components/layout/AuthCard'
import { EnvelopeOpenIcon } from '@/components/ui/icons'

export function ForgotPasswordSentPage() {
  const location = useLocation()
  const email = (location.state as { email?: string } | null)?.email ?? 'email của bạn'

  return (
    <AuthCard>
      <AuthCardIcon>
        <EnvelopeOpenIcon />
      </AuthCardIcon>
      <h1 className="text-center text-2xl font-semibold tracking-tight text-gray-950">Kiểm tra hộp thư của bạn</h1>
      <p className="mt-2 text-center text-sm text-gray-500">
        Nếu <span className="font-medium text-gray-800">{email}</span> có trong hệ thống, bạn sẽ nhận được link đặt
        lại mật khẩu trong ít phút.
      </p>
      <p className="mt-6 text-center text-sm text-gray-500">
        <Link to="/login" className="font-medium text-brand-700">
          Quay lại đăng nhập
        </Link>
      </p>
    </AuthCard>
  )
}
