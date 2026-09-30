import { BrowserRouter, Routes, Route } from 'react-router-dom'
import { GoogleOAuthProvider } from '@react-oauth/google'
import { AuthProvider, useAuth } from '@/context/AuthContext'
import { ProtectedRoute } from '@/components/layout/ProtectedRoute'
import { GuestRoute } from '@/components/layout/GuestRoute'
import { FullPageSpinner } from '@/components/ui/Spinner'
import { LandingPage } from '@/pages/LandingPage'
import { RegisterPage } from '@/pages/auth/RegisterPage'
import { LoginPage } from '@/pages/auth/LoginPage'
import { VerifyEmailPendingPage } from '@/pages/auth/VerifyEmailPendingPage'
import { VerifyEmailPage } from '@/pages/auth/VerifyEmailPage'
import { ForgotPasswordPage } from '@/pages/auth/ForgotPasswordPage'
import { ForgotPasswordSentPage } from '@/pages/auth/ForgotPasswordSentPage'
import { ResetPasswordPage } from '@/pages/auth/ResetPasswordPage'
import { ChooseAccountTypePage } from '@/pages/onboarding/ChooseAccountTypePage'
import { CompanySetupPage } from '@/pages/onboarding/CompanySetupPage'
import { CreateWorkspacePage } from '@/pages/onboarding/CreateWorkspacePage'
import { DomainMatchPage } from '@/pages/onboarding/DomainMatchPage'
import { PendingApprovalPage } from '@/pages/onboarding/PendingApprovalPage'
import { AcceptInvitePage } from '@/pages/invite/AcceptInvitePage'
import { DashboardHomePage } from '@/pages/dashboard/DashboardHomePage'
import { InviteMemberPage } from '@/pages/dashboard/InviteMemberPage'
import { NotFoundPage } from '@/pages/NotFoundPage'

function RootRoute() {
  const { isAuthenticated, isBootstrapping } = useAuth()
  if (isBootstrapping) return <FullPageSpinner />
  return isAuthenticated ? (
    <ProtectedRoute>
      <DashboardHomePage />
    </ProtectedRoute>
  ) : (
    <LandingPage />
  )
}

function AppRoutes() {
  return (
    <Routes>
      <Route path="/" element={<RootRoute />} />

      <Route
        path="/register"
        element={
          <GuestRoute>
            <RegisterPage />
          </GuestRoute>
        }
      />
      <Route
        path="/login"
        element={
          <GuestRoute>
            <LoginPage />
          </GuestRoute>
        }
      />
      <Route path="/verify-email" element={<VerifyEmailPage />} />
      <Route path="/verify-email/pending" element={<VerifyEmailPendingPage />} />
      <Route path="/forgot-password" element={<ForgotPasswordPage />} />
      <Route path="/forgot-password/sent" element={<ForgotPasswordSentPage />} />
      <Route path="/reset-password" element={<ResetPasswordPage />} />
      <Route path="/invite/:token" element={<AcceptInvitePage />} />

      <Route
        path="/onboarding/account-type"
        element={
          <ProtectedRoute>
            <ChooseAccountTypePage />
          </ProtectedRoute>
        }
      />
      <Route
        path="/onboarding/company-setup"
        element={
          <ProtectedRoute>
            <CompanySetupPage />
          </ProtectedRoute>
        }
      />
      <Route
        path="/onboarding/domain-match"
        element={
          <ProtectedRoute>
            <DomainMatchPage />
          </ProtectedRoute>
        }
      />
      <Route
        path="/onboarding/pending-approval"
        element={
          <ProtectedRoute>
            <PendingApprovalPage />
          </ProtectedRoute>
        }
      />
      <Route
        path="/onboarding/create-workspace"
        element={
          <ProtectedRoute>
            <CreateWorkspacePage />
          </ProtectedRoute>
        }
      />
      <Route
        path="/members/invite"
        element={
          <ProtectedRoute>
            <InviteMemberPage />
          </ProtectedRoute>
        }
      />

      <Route path="*" element={<NotFoundPage />} />
    </Routes>
  )
}

export function App() {
  return (
    <GoogleOAuthProvider clientId={import.meta.env.VITE_GOOGLE_CLIENT_ID}>
      <BrowserRouter>
        <AuthProvider>
          <AppRoutes />
        </AuthProvider>
      </BrowserRouter>
    </GoogleOAuthProvider>
  )
}
