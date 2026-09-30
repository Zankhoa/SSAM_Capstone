# SSAM Frontend

React + TypeScript + Vite + Tailwind CSS. Triển khai luồng Authentication & Onboarding
theo đúng `doc/Auth-API-Contract.md` (ở gốc repo `DoAn`) — BE đổi API thì sửa file đó trước.

## Chạy dự án

```bash
npm install
cp .env.example .env   # rồi điền VITE_API_BASE_URL và VITE_GOOGLE_CLIENT_ID
npm run dev
```

`VITE_GOOGLE_CLIENT_ID` lấy từ Google Cloud Console (OAuth 2.0 Client ID, loại "Web application").
Nếu chưa có, nút "Tiếp tục với Google" vẫn hiện nhưng sẽ báo lỗi khi bấm.

Dev server chạy ở `http://127.0.0.1:5174` (ép IPv4 trong `vite.config.ts` để ngrok/tunnel nối được).
Font Be Vietnam Pro + IBM Plex Mono nạp qua Google Fonts trong `index.html`; offline thì rơi về system-ui.

## Cấu trúc

```
src/
  api/          - hàm gọi từng nhóm endpoint (authApi, companyApi, invitationApi)
  context/      - AuthContext: phiên đăng nhập, user hiện tại, companies/workspaces
  lib/          - apiClient (axios + tự refresh token khi 401), storage, passwordStrength
  components/
    ui/         - Button, TextField, Alert, PasswordStrengthMeter... dùng chung
    layout/     - AuthCard (khung card có header SSAM), ProtectedRoute
  pages/
    auth/       - Register, Login, Verify email, Forgot/Reset password
    onboarding/ - Chọn loại tài khoản, Thiết lập Doanh nghiệp, Domain-match, Chờ duyệt
    invite/     - Chấp nhận lời mời qua email
```

## Đã làm / chưa làm

Đã làm: toàn bộ luồng Đăng ký, Đăng nhập (email + Google), quên/đặt lại mật khẩu, xác thực
email, onboarding Cá nhân/Doanh nghiệp, gợi ý tham gia theo domain (kèm nhánh chờ duyệt),
chấp nhận lời mời qua email.

Chưa làm (theo đúng phạm vi đã thống nhất — xem `doc/Auth-DB-UI-Notes.md`):
- Đăng nhập GitHub / SAML SSO — nút hiện nhưng chưa gọi API thật.
- Dashboard thật, tạo Workspace cho nhánh cá nhân — thuộc module khác.
- Màn hình Admin (Role Tags, Permission Matrix, duyệt Join-request) — làm đợt sau.
