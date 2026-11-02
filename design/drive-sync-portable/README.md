# DriveSync Portable v0.2 — Google Drive đồng bộ hai chiều (Beta)

**Đây là ứng dụng Windows WinForms có engine Google Drive thực**, không phải HTML mock. Ở thời điểm phát hành Beta, CI Windows xác minh build và UI smoke, còn kiểm thử OAuth/đồng bộ tài khoản Google thật **bắt buộc trên máy người dùng** để kết luận Production PASS.

## Tải và chạy
1. Vào GitHub Actions của nhánh `design/drive-sync-portable-ui`, chọn workflow **Build DriveSync Portable Beta** có kết quả SUCCESS.
2. Tải artifact `DriveSyncPortable-beta-win-x64`, giải nén; nếu bên trong còn ZIP thì giải nén lần nữa.
3. Chạy trực tiếp `DriveSyncPortable.exe` trên Windows x64. Ứng dụng là .NET 8 single-file self-contained, manifest asInvoker (không tự xin Admin).
4. Ứng dụng chỉ chạy được nếu chính sách doanh nghiệp cho phép thực thi và cho phép tải dữ liệu lên Google Drive cá nhân. Không có bypass bảo mật.

## Đăng nhập Google — cấu hình bắt buộc một lần
Google yêu cầu OAuth Desktop Client ID riêng (Rclone shared ID không còn phù hợp dùng lâu dài). DriveSync **không lưu sẵn OAuth ID của người khác**.

1. Đăng nhập Gmail cá nhân tại https://console.cloud.google.com; tạo một project mới.
2. Enable **Google Drive API**.
3. Google Auth Platform: tạo OAuth consent screen, Audience External, thêm tài khoản Gmail làm Test User nếu đang Testing; dùng phạm vi Drive phù hợp.
4. Tạo OAuth Client loại **Desktop app** tại https://console.cloud.google.com/auth/clients; tải file JSON.
5. Trong EXE: **Đăng nhập Google** → dán nội dung OAuth Desktop JSON (không dán service account) → Lưu & tiếp tục → trình duyệt Google mở ra.
6. Xác nhận quyền trên trang Google. Ứng dụng không thu thập mật khẩu. Để thay OAuth Client, giữ **Shift** khi bấm **Đổi tài khoản**.

**Lưu ý:** Testing có thể khiến refresh token hết hạn sau 7 ngày. Thiết lập Production cho ứng dụng OAuth cá nhân hợp lệ để tránh giới hạn này, tuân thủ các yêu cầu xác minh/cảnh báo của Google. Scope `drive` là quyền rộng vì cần đọc/sửa file có sẵn. Chỉ chọn thư mục cụ thể trong EXE để hạn chế phạm vi xử lý thực tế.

## Cách sử dụng hằng ngày
1. Bấm **Chọn thư mục** trên laptop.
2. Bấm **Chọn thư mục** ở phía Google Drive; chọn thư mục hiện tại hoặc tạo thư mục mới; không nên chọn toàn bộ My Drive.
3. Lần đầu sẽ xuất hiện xác nhận. **CÓ** = đồng bộ xóa hai chiều (Drive vào Trash, local chuyển Recovery). **KHÔNG** = chỉ thêm/sửa file, không tự đồng bộ thao tác xóa. **HỦY** = chưa bắt đầu.
4. Sau khi chọn xong, tự đồng bộ. Dùng Excel/File Explorer bình thường; nhấn Ctrl+S để lưu.
5. Bật **Tự khởi động cùng Windows**, bấm **Chạy ngầm**. Từ biểu tượng khay hệ thống chọn Mở/Tạm dừng/Tiếp tục/Thoát.

**Đồng bộ local:** FileSystemWatcher theo dõi sự kiện, chờ ổn định trước upload. **Đồng bộ Drive:** `changes.list` mỗi khoảng 60 giây và chỉ quét lại thư mục khi có thay đổi hoặc đối soát sau khoảng một giờ. Không chạy server riêng; vẫn có request upload/download/kiểm tra changes/token.

## Bảo vệ dữ liệu
- File trùng tên nhưng nội dung khác trong lần đầu: giữ bản laptop với hậu tố `(xung-dot-laptop-...)`, tải bản Drive vào đường dẫn chính.
- Khi cả laptop và Drive cùng chỉnh file sau đồng bộ: giữ bản laptop xung đột, bản Drive về đường dẫn chính. Bản xung đột được tải lên trong lần kế tiếp.
- Xóa trên Drive trong chế độ mirror-deletes: chuyển file local vào `%LOCALAPPDATA%\DriveSyncPortable\Recovery\...`.
- Xóa trên laptop trong chế độ mirror-deletes: chuyển file trên Drive vào thùng rác Google (không xóa vĩnh viễn).
- Kiểm tra MD5 của bản tải về; không ghi đè nếu local vừa thay đổi trong khi tải.
- Hàng đợi bền vững bằng checkpoints JSON trong `%LOCALAPPDATA%\DriveSyncPortable\settings.json`. Khi app mở lại, đối chiếu dữ liệu.
- Mật khẩu Google không đi qua EXE. Refresh/access token và Client Secret được DPAPI mã hóa theo user Windows. Không gửi log, token, hay nội dung file sang server bên thứ ba.
- Chỉ chạy một phiên đồng bộ mỗi user Windows.

## Giới hạn bản Beta
- **Chưa kiểm thử thực tế bằng Gmail và file Excel thật của chủ sở hữu.** CI PASS không thay thế được field PASS.
- Google Docs, Sheets, Slides gốc (dạng web) chưa chuyển đổi thành tệp Windows; bản Beta bỏ qua và ghi trạng thái. Hãy dùng các tệp XLSX/DOCX/PDF hoặc mở Google Docs trực tuyến.
- Bản Beta dùng upload một lượt trong phiên resumable; chưa có tiếp tục tải từ byte giữa chừng khi file quá lớn hoặc đứt mạng (lần sau gửi lại).
- Chưa tối ưu thao tác đổi tên file bằng Move API; hiện xử lý như xóa cũ / tạo mới, vì vậy phải thử trên dữ liệu giả trước khi bật mirror-deletes.
- Tệp Windows mang tên không hợp lệ hoặc tên bị trùng không phân biệt hoa-thường sẽ được bỏ qua và ghi lỗi; không tự xóa.
- Google có thể yêu cầu xác thực lại; Windows/AppLocker/IT có thể chặn chương trình.
- Google Drive API được công bố không tính thêm phí ở mức sử dụng tiêu chuẩn; phải theo dõi quota và không bật billing trả phí nếu không muốn rủi ro phí.

## Kiểm thử chấp nhận trước khi đưa vào tài liệu thật
- Tạo thư mục **DriveSync_Test** riêng bên Google Drive và laptop; đặt vào 3 file mẫu XLSX, PDF, TXT.
- Chỉnh XLSX nhiều lần nhanh, nhấn Ctrl+S; xác minh file Drive thực sự cập nhật nội dung, không chỉ nhìn nhãn UI.
- Copy thêm file, tạo subfolder, đổi tên file, di chuyển file và thử xóa (chỉ trên dữ liệu mẫu). Xác minh hai chiều.
- Thử sửa cùng file ở Drive và laptop rồi kiểm tra bản xung đột.
- Ngắt mạng, chỉnh file, bật mạng lại; đảm bảo không mất và không nhân đôi dữ liệu.
- Đóng/mở lại ứng dụng và restart Windows; thử khay hệ thống và HKCU autostart.
- Chỉ chốt Production sau khi đạt tất cả các bài kiểm tra trên máy thực tế.

## Phạm vi repo
Ứng dụng DriveSync nằm hoàn toàn tại `design/drive-sync-portable/src/DriveSyncPortable`, workflow riêng. Không sửa mã nghiệp vụ hoặc phát hành WMS đang chạy. PR #30 không merge main cho tới khi tách ứng dụng sang repo độc lập.
