import { api } from '@/lib/apiClient'
import type { WorkspaceSummaryDto } from '@/types/auth'

export const workspaceApi = {
  create: (body: { name: string; description?: string; companyId: number | null }) =>
    api.post<WorkspaceSummaryDto>('/workspaces', body).then((r) => r.data),
}
