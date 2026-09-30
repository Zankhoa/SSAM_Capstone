// Khớp 1-1 với các DTO trong doc/Auth-API-Contract.md — sửa API thì sửa cả 2 chỗ.

export interface UserDto {
  id: number
  fullName: string
  email: string
  avatarUrl: string | null
  emailVerified: boolean
}

export interface CompanyDto {
  id: number
  name: string
  code: string
  emailDomain: string | null
  domainAutoJoinEnabled: boolean
  taxCode: string
  address: string
  logoUrl: string | null
}

export interface CompanyMembershipDto {
  companyId: number
  companyName: string
  roleName: string
}

export interface WorkspaceSummaryDto {
  workspaceId: number
  workspaceName: string
  companyId: number | null
}

export interface AuthSessionDto {
  accessToken: string
  refreshToken: string
  expiresIn: number
  user: UserDto
}

export interface MeResponseDto {
  user: UserDto
  companies: CompanyMembershipDto[]
  workspaces: WorkspaceSummaryDto[]
}

export interface CompanyMatchDto {
  matched: boolean
  company?: {
    id: number
    name: string
    domain: string
    logoUrl: string | null
    autoJoinEnabled: boolean
  }
}

export interface InvitationPreviewDto {
  valid: boolean
  reason?: 'EXPIRED' | 'ALREADY_USED'
  email?: string
  companyName?: string
  workspaceName?: string | null
  roleName?: string
  expiresAt?: string
}

export interface ApiErrorBody {
  code: string
  message: string
  errors?: Record<string, string[]>
}
