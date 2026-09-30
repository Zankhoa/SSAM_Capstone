import { useEffect, useRef, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { AuthCard, AuthCardIcon } from '@/components/layout/AuthCard'
import { buttonClasses } from '@/components/ui/Button'
import { Alert } from '@/components/ui/Alert'
import { authApi } from '@/api/authApi'
import { ApiError } from '@/lib/apiClient'
import { CheckIcon } from '@/components/ui/icons'

type Status = 'verifying' | 'success' | 'error'

export function VerifyEmailPage() {
  const [searchParams] = useSearchParams()
  const token = searchParams.get('token')
  const [status, setStatus] = useState<Status>('verifying')
  const [errorMessage, setErrorMessage] = useState('')
  // Token xác thực là dùng-một-lần. StrictMode (dev) chạy effect 2 lần → lần 2 sẽ bị TOKEN_INVALID
  // dù lần 1 đã thành công. Ghi nhớ token đã gửi để không gọi trùng.
  const submittedTokenRef = useRef<string | null>(null)

  useEffect(() => {
    if (!token) {
      setStatus('error')
      setErrorMessage('Link xác thực không hợp lệ.')
      return
    }
    if (submittedTokenRef.current === token) return
    submittedTokenRef.current = token

    let cancelled = false
    authApi
      .verifyEmail(token)
      .then(() => {
        if (!cancelled) setStatus('success')
      })
      .catch((err) => {
        if (cancelled) return
        setStatus('error')
        setErrorMessage(
          err instanceof ApiError && err.code === 'TOKEN_EXPIRED'
            ? 'Link xác thực đã hết hạn.'
            : 'Link xác thực không hợp lệ hoặc đã được sử dụng.',
        )
      })
    return () => {
      cancelled = true
    }
  }, [token])

  return (
    <AuthCard>
      <AuthCardIcon>
        <CheckIcon />
      </AuthCardIcon>

      {status === 'verifying' && (
        <p className="text-center text-sm text-gray-500">Đang xác thực email...</p>
      )}

      {status === 'success' && (
        <>
          <h1 className="text-center text-2xl font-semibold tracking-tight text-gray-950">Xác thực email thành công</h1>
          <p className="mt-2 text-center text-sm text-gray-500">Bạn có thể đăng nhập ngay bây giờ.</p>
          <div className="mt-6">
            <Link to="/login" className={buttonClasses()}>
              Đăng nhập →
            </Link>
          </div>
        </>
      )}

      {status === 'error' && (
        <>
          <Alert tone="error">{errorMessage}</Alert>
          <div className="mt-6">
            <Link to="/verify-email/pending" className={buttonClasses({ variant: 'secondary' })}>
              Gửi lại email xác thực
            </Link>
          </div>
        </>
      )}
    </AuthCard>
  )
}
