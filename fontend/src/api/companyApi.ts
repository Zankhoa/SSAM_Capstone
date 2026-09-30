import { api } from '@/lib/apiClient'
import type { CompanyDto, CompanyMatchDto } from '@/types/auth'

export interface CreateCompanyBody {
  name: string
  taxCode: string
  address: string
  emailDomain: string
  adminPhone: string
  workspaceSlug: string
  domainAutoJoinEnabled: boolean
}

export const companyApi = {
  matchByEmail: (email: string) =>
    api.get<CompanyMatchDto>('/companies/match', { params: { email } }).then((r) => r.data),

  create: (body: CreateCompanyBody) =>
    api.post<CompanyDto>('/companies', body).then((r) => r.data),

  joinDirectly: (companyId: number) =>
    api.post<{ companyId: number; status: string }>(`/companies/${companyId}/join`, {}).then((r) => r.data),

  requestToJoin: (companyId: number) =>
    api.post<{ requestId: number; status: string }>(`/companies/${companyId}/join-requests`, {}).then((r) => r.data),

  getJoinRequestStatus: (companyId: number, requestId: number) =>
    api.get<{ status: 'pending' | 'approved' | 'rejected' }>(
      `/companies/${companyId}/join-requests/${requestId}`,
    ).then((r) => r.data),

  invite: (companyId: number, body: { email: string; workspaceId?: number; roleName?: string }) =>
    api.post<{ invitationId: number; token: string; inviteUrl: string }>(
      `/companies/${companyId}/invitations`,
      body,
    ).then((r) => r.data),
}
