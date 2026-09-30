import clsx from 'clsx'
import { checkPassword, passwordScore, PASSWORD_STRENGTH_LABEL } from '@/lib/passwordStrength'

const barColors = ['bg-gray-200', 'bg-red-400', 'bg-orange-400', 'bg-yellow-400', 'bg-brand-500']

export function PasswordStrengthMeter({ password }: { password: string }) {
  const score = passwordScore(password)
  const checklist = checkPassword(password)

  const items: Array<[boolean, string]> = [
    [checklist.minLength, 'Tối thiểu 8 ký tự'],
    [checklist.hasDigit, 'Có chữ số'],
    [checklist.hasUpperLower, 'Bao gồm chữ hoa & thường'],
    [checklist.hasSpecialChar, 'Ký tự đặc biệt'],
  ]

  return (
    <div className="mt-2">
      <div className="mb-1 flex items-center justify-between text-xs text-gray-500">
        <span>Độ mạnh:</span>
        <span className="font-medium">{password ? PASSWORD_STRENGTH_LABEL[score] : ''}</span>
      </div>
      <div className="grid grid-cols-4 gap-1">
        {[0, 1, 2, 3].map((i) => (
          <span
            key={i}
            className={clsx('h-1.5 rounded-full', i < score ? barColors[score] : 'bg-gray-200')}
          />
        ))}
      </div>
      <ul className="mt-2 grid grid-cols-2 gap-x-3 gap-y-1">
        {items.map(([ok, label]) => (
          <li key={label} className={clsx('flex items-center gap-1 text-xs', ok ? 'text-brand-600' : 'text-gray-400')}>
            <span>{ok ? '✓' : '○'}</span>
            {label}
          </li>
        ))}
      </ul>
    </div>
  )
}
