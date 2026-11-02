# DriveSync Portable — giao diện để duyệt

**Trạng thái:** Bản mô phỏng HTML, chưa đăng nhập Google, chưa đồng bộ file, chưa có ứng dụng EXE thật.

## Mục đích
Ứng dụng Windows riêng biệt để đồng bộ một thư mục local ↔ một thư mục Google Drive cá nhân, dùng tài khoản Windows thường, không cài driver hay đòi Admin. Không sửa nghiệp vụ in tem hoặc dùng tài khoản/credential WMS. Tên repo hiện tại chỉ là nơi tham khảo công nghệ C#/.NET 8 WinForms và lưu bản duyệt.

## Hành trình tối giản
1. Mở EXE, bấm Đăng nhập Google → mở trang OAuth chính thức trên trình duyệt mặc định, không yêu cầu nhập mật khẩu vào EXE.
2. Chọn thư mục trên laptop và thư mục tương ứng trên Google Drive, xem trước trước khi đồng bộ lần đầu.
3. Bật Tự khởi động cùng Windows (chỉ HKCU Run nếu được phép).
4. Bấm Chạy ngầm → thu nhỏ vào system tray; biểu tượng tray có Mở, Tạm dừng, Tiếp tục, Thoát.
5. File local thay đổi: tự upload sau khi file ghi xong; file trên Drive thay đổi: tự download trong chu kỳ kiểm tra thay đổi.
6. Trạng thái và lịch sử ngắn gọn; không có màn hình setup kỹ thuật dài dòng.

## Đồng bộ
- Local: FileSystemWatcher + debounce/check file ổn định + nhật ký thay đổi bền vững + đối soát sau khởi động để không bỏ sót thông báo.
- Drive: sử dụng changes.getStartPageToken / changes.list theo page token, không quét toàn bộ My Drive liên tục; mặc định kiểm tra 60 giây/lần (có thể điều chỉnh sau đo thực tế). Remote không thể hoàn toàn miễn request.
- Các request đăng nhập, upload, download, kiểm tra changes và refresh token vẫn là điều kiện bắt buộc của đồng bộ; không cần thuê server trung gian.
- Đồng bộ hai chiều không có nghĩa ghi đè tùy tiện: sửa cùng file ở hai nơi → giữ cả hai bản để người dùng xử lý.
- Cấp quyền xóa hai chiều chỉ khi người dùng chấp thuận rõ; mặc định dùng thùng rác/bản sao khôi phục để tránh xóa mất dữ liệu.
- Không xóa hàng loạt khi thư mục local tạm mất kết nối hoặc khi Drive API trả lỗi.
- Bỏ qua file tạm của Office như ~$ và chỉ upload khi tệp đã ghi ổn định.
- Google Docs/Sheets/Slides trực tuyến không phải XLSX/DOCX gốc: bản đầu sẽ tạo shortcut tới tài liệu Google hoặc cho xuất định dạng, không hứa đồng bộ chỉnh sửa hai chiều của Google-native files như Office files.

## Bảo mật
- Dùng OAuth Desktop + PKCE, trình duyệt hệ thống, không yêu cầu Google password và không tự đọc cookies.
- Cần app maintainer thiết lập OAuth Client ID riêng; Rclone shared client ID bị loại dần trong 2026.
- Google Drive full-scope là quyền hạn chế; chỉ yêu cầu phạm vi thật sự cần, giải thích trước khi consent. Ứng dụng cá nhân có thể gặp cảnh báo app chưa xác minh; không bỏ qua chính sách Google.
- Token lưu mã hóa theo Windows user bằng DPAPI/Windows Credential Manager; không để plaintext trong repo, file cấu hình, log.
- Không ghi token, mật khẩu, nội dung file, dữ liệu riêng tư vào nhật ký; không tự gửi log.
- Máy có chính sách cấm EXE hoặc cấm đưa dữ liệu công ty vào Drive cá nhân thì không tự vượt qua hạn chế, cần tuân thủ quy định.

## Tiêu chí trước khi phát hành
- Windows x64 / tài khoản thường; kiểm tra thực tế EXE portable, tray, HKCU startup.
- Kiểm thử tạo/sửa/đổi tên/xóa/di chuyển Excel, file nhiều GB, Office lock file.
- Kiểm thử mất mạng, retry, reboot giữa lúc tải, phiên bản xung đột, OAuth expiry.
- Đo usage/latency khi ít thay đổi, bảo đảm không polling dày và không quét My Drive mỗi vòng.
- Windows build PASS không đồng nghĩa Google OAuth & sync PASS trên laptop thực tế.

## Tách phạm vi
Repo này hiện là ứng dụng in tem WMS. Nhánh design/drive-sync-portable-ui chỉ thêm HTML/Markdown thiết kế trong thư mục design/drive-sync-portable. Không sửa source, config hay workflow WMS. Khi được duyệt, ứng dụng đồng bộ cần repo độc lập để tách CI/release/credentials.

## Bản dựng Windows v0.1 — sau khi chốt UX

Mã nguồn WinForms đã tạo tại src/DriveSyncPortable/ và workflow build riêng tại .github/workflows/build-drivesync-ui.yml.

- MainForm.cs: bố cục đã duyệt, tài khoản, 2 ô thư mục, toggle tự khởi động, chạy ngầm, tạm dừng và nhật ký minh họa.
- VisualControls.cs: RoundedPanel, RoundedButton, ToggleSwitch theo palette đã duyệt.
- Program.cs: entry point và UI smoke test.
- Build win-x64 net8.0-windows self-contained single-file, manifest asInvoker.
- Nút chọn thư mục laptop đã hoạt động ở cấp UI; không đọc/upload file.
- Tự khởi động chỉ viết/xóa HKCU Run/DriveSyncPortableUI khi người dùng chủ động bật; có thể bị policy chặn.
- Chạy ngầm thu nhỏ vào khay hệ thống, menu mở/tạm dừng mô phỏng/thoát; chưa có engine đồng bộ.
- Đăng nhập Google và chọn thư mục Google Drive là mục CHƯA TRIỂN KHAI, không yêu cầu mật khẩu.
- Tạm dừng chỉ thay đổi trạng thái UI vì chưa có sync thực. Nhật ký được dán nhãn dữ liệu mẫu.
- Source và bản phát hành WMS không bị chỉnh sửa.

### Tải và chạy thử giao diện

Vào GitHub Actions → Build DriveSync Portable UI → run PASS → artifact DriveSyncPortable-UI-Preview-win-x64. Giải nén hai lớp ZIP nếu cần, chạy DriveSyncPortable.exe nếu Windows policy cho phép.
