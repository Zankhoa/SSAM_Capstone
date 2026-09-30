import { DashboardEmptyState, DashboardLayout } from '@/components/layout/DashboardLayout'
import { useAuth } from '@/context/AuthContext'
import { DocumentIcon, FolderIcon, ListChecksIcon } from '@/components/ui/icons'

// Khung sườn Dashboard — chưa có API Project/Document/Testcase nên chỉ hiển thị empty
// state cho từng khu vực, không dựng dữ liệu giả. Nội dung thật thuộc module sau.
export function DashboardHomePage() {
  const { user, companies, workspaces } = useAuth()

  return (
    <DashboardLayout>
      <h1 className="text-2xl font-semibold tracking-tight text-gray-950">Chào {user?.fullName}!</h1>
      <p className="mt-1 text-sm text-gray-500">
        {companies[0]
          ? `Đây là không gian làm việc của ${companies[0].companyName}.`
          : 'Đây là không gian làm việc cá nhân của bạn.'}
      </p>

      <div className="mt-6 grid grid-cols-1 gap-4 sm:grid-cols-3">
        <DashboardEmptyState
          icon={<FolderIcon className="h-5 w-5" />}
          title="Chưa có dự án"
          description="Tạo dự án đầu tiên để bắt đầu theo dõi công việc."
        />
        <DashboardEmptyState
          icon={<DocumentIcon className="h-5 w-5" />}
          title="Chưa có tài liệu yêu cầu"
          description="Module AI sinh URD từ ghi chú thô đang được phát triển."
        />
        <DashboardEmptyState
          icon={<ListChecksIcon className="h-5 w-5" />}
          title="Chưa có testcase"
          description="Module AI sinh testcase & truy xuất nguồn gốc đang được phát triển."
        />
      </div>

      {(companies.length > 0 || workspaces.length > 0) && (
        <div className="mt-6 rounded-xl border border-gray-200 bg-white p-4 text-sm">
          <p className="font-semibold text-gray-700">Thông tin tài khoản</p>
          <dl className="mt-2 space-y-1 text-gray-500">
            {companies.map((c) => (
              <div key={c.companyId} className="flex justify-between">
                <dt>Công ty</dt>
                <dd className="font-medium text-gray-700">
                  {c.companyName} · {c.roleName}
                </dd>
              </div>
            ))}
            {workspaces.map((w) => (
              <div key={w.workspaceId} className="flex justify-between">
                <dt>Workspace</dt>
                <dd className="font-medium text-gray-700">{w.workspaceName}</dd>
              </div>
            ))}
          </dl>
        </div>
      )}
    </DashboardLayout>
  )
}
