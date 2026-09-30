# Mockoon mock cho SSAM Auth API

`ssam-auth.mockoon.json` — sinh trực tiếp từ `doc/Auth-API-Contract.md`, đủ 19 endpoint (gồm cả
`POST /workspaces` và `POST /companies/:companyId/invitations` thêm sau khi test luồng Tạo
Doanh nghiệp/Workspace/Mời thành viên). Cổng `5000` + prefix `api/v1` đã khớp sẵn với
`VITE_API_BASE_URL` trong `fontend/.env.example` — không cần chỉnh gì ở FE.

## Cách dùng

1. Cài [Mockoon](https://mockoon.com/download/) (desktop app, miễn phí).
2. File → Open environment → chọn `ssam-auth.mockoon.json`.
3. Bấm nút ▶ (play) ở góc trên để chạy server tại `http://localhost:5000`.
4. Chạy FE bình thường (`npm run dev` trong `fontend/`) — mọi request sẽ trúng mock này.

> Nếu Mockoon đang chạy sẵn từ trước khi file này được cập nhật (thêm route mới), bấm nút ⟳
> reload environment (hoặc tắt/mở lại) để nó đọc file mới nhất — Mockoon không tự động theo dõi
> thay đổi file trên đĩa.

## Đổi kịch bản test (thành công / lỗi)

Mỗi route có sẵn nhiều response (vd `auth/login` có 4: Thành công, Sai mật khẩu, Email chưa xác
thực, Tài khoản bị khóa). Route đang trả về response nào được đánh dấu **Default**. Muốn đổi
kịch bản: chọn route ở sidebar trái → chọn tab response tương ứng ở thanh ngang trên → bấm icon
dấu chấm than/"Set as default" (hoặc bật **Random** / **Sequential** ở góc trên response list nếu
muốn nó tự xoay vòng qua các kịch bản).

Lưu ý 2 route đã cố tình để **mặc định là trạng thái "chưa hoàn tất"** để bạn test đúng luồng mới
xây (Onboarding, Domain-match):
- `GET auth/me` → mặc định "Chưa onboard" (companies/workspaces rỗng) để sau khi login/verify sẽ
  tự điều hướng sang `ChooseAccountTypePage`. Đổi sang "Đã onboard" để test thẳng vào Dashboard.
- `GET companies/match` → mặc định "Không khớp domain" (đi thẳng vào `CompanySetupPage`). Đổi
  sang "Có khớp domain (FPT)" để test màn hình `DomainMatchPage`.

## Giới hạn của bản mock này

- Không có state thật giữa các request (vd đăng ký xong rồi login lại vẫn dùng data giả cố định,
  không lưu gì cả) — chỉ để test giao diện/luồng điều hướng, không test được nghiệp vụ thật.
- `auth/logout` trả 204 luôn thành công, không thật sự revoke gì.
- Khi BE thật xong, chỉ cần đổi `VITE_API_BASE_URL` trỏ sang BE thật, tắt Mockoon đi.
