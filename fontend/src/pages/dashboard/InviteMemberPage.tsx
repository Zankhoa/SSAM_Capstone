import { useEffect, useRef, useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { DashboardLayout } from '@/components/layout/DashboardLayout'
import { TextField } from '@/components/ui/TextField'
import { Button } from '@/components/ui/Button'
import { Alert } from '@/components/ui/Alert'
import { MailIcon, UsersIcon } from '@/components/ui/icons'
import { useAuth } from '@/context/AuthContext'
import { companyApi } from '@/api/companyApi'

const inviteSchema = z.object({
  email: z.string().min(1, 'Vui lòng nhập email').email('Email không hợp lệ'),
  roleName: z.string().optional(),
})
type FormValues = z.infer<typeof inviteSchema>

export function InviteMemberPage() {
  const { companies } = useAuth()
  const companyId = companies[0]?.companyId
  const [serverError, setServerError] = useState<string | null>(null)
  const [inviteUrl, setInviteUrl] = useState<string | null>(null)
  const [copied, setCopied] = useState(false)
  const [copyError, setCopyError] = useState(false)
  const copiedTimerRef = useRef<ReturnType<typeof setTimeout>>()

  // Dọn timer "Đã copy" khi rời trang, tránh setState trên component đã unmount.
  useEffect(() => () => clearTimeout(copiedTimerRef.current), [])

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({ resolver: zodResolver(inviteSchema) })

  const onSubmit = async (values: FormValues) => {
    if (!companyId) return
    setServerError(null)
    setInviteUrl(null)
    try {
      const result = await companyApi.invite(companyId, { email: values.email, roleName: values.roleName })
      setInviteUrl(result.inviteUrl)
      reset()
    } catch {
      setServerError('Có lỗi xảy ra, vui lòng thử lại.')
    }
  }

  const handleCopy = async () => {
    if (!inviteUrl) return
    setCopyError(false)
    try {
      // Clipboard API có thể không tồn tại (HTTP không phải localhost) hoặc bị từ chối quyền → reject.
      await navigator.clipboard.writeText(inviteUrl)
      setCopied(true)
      clearTimeout(copiedTimerRef.current)
      copiedTimerRef.current = setTimeout(() => setCopied(false), 2000)
    } catch {
      setCopyError(true)
    }
  }

  return (
    <DashboardLayout>
      <h1 className="text-2xl font-semibold tracking-tight text-gray-950">Mời thành viên</h1>
      <p className="mt-1 text-sm text-gray-500">Gửi lời mời tham gia công ty qua email.</p>

      {!companyId ? (
        <div className="mt-6 max-w-md">
          <Alert tone="warning">
            Workspace cá nhân không thuộc công ty nào nên không có tính năng mời thành viên. Tạo/tham gia 1 công ty
            trước.
          </Alert>
        </div>
      ) : (
        <div className="mt-6 max-w-md">
          {serverError && (
            <div className="mb-4">
              <Alert tone="error">{serverError}</Alert>
            </div>
          )}

          {inviteUrl && (
            <div className="mb-4 rounded-lg border border-brand-200 bg-brand-50 p-3">
              <p className="text-xs font-semibold text-brand-700">Đã tạo lời mời — copy link gửi cho người nhận:</p>
              <div className="mt-2 flex gap-2">
                <input
                  readOnly
                  value={inviteUrl}
                  className="w-full truncate rounded border border-brand-200 bg-white px-2 py-1.5 text-xs text-gray-700"
                />
                <Button variant="secondary" className="!w-auto shrink-0 px-3 text-xs" onClick={handleCopy}>
                  {copied ? 'Đã copy' : 'Copy'}
                </Button>
              </div>
              {copyError && (
                <p className="mt-2 text-[11px] text-red-600">Trình duyệt không cho phép copy tự động — hãy chọn và copy link thủ công.</p>
              )}
              <p className="mt-2 text-[11px] text-brand-600">
                Bản demo: chưa gửi email thật, dán link này vào tab khác để test màn chấp nhận lời mời.
              </p>
            </div>
          )}

          <form onSubmit={handleSubmit(onSubmit)} noValidate className="space-y-4">
            <TextField
              label="Email người được mời"
              icon={<MailIcon className="h-4 w-4" />}
              type="email"
              placeholder="teammate@company.com"
              error={errors.email?.message}
              {...register('email')}
            />
            <TextField
              label="Vai trò (không bắt buộc)"
              icon={<UsersIcon className="h-4 w-4" />}
              placeholder="Vd: BA, QA, Dev"
              {...register('roleName')}
            />
            <Button type="submit" isLoading={isSubmitting} className="!w-auto px-6">
              Gửi lời mời →
            </Button>
          </form>
        </div>
      )}
    </DashboardLayout>
  )
}
