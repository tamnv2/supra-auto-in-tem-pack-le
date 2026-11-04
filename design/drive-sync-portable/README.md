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


## v0.4.0 - Quota & đồng bộ an toàn
- Thẻ quota: dung lượng thực tế từ `about.get` (tối đa 1 lần/30 phút hoặc bấm làm mới) và API Units ước tính riêng EXE theo request từ lúc áp dụng v0.4.0; **không phải số liệu billing/quota của toàn Google Cloud Project**. Ngưỡng 400 triệu units/ngày chỉ là tham chiếu dự án thuộc chính sách từ 01/05/2026; dự án cũ có thể khác. Giao diện có thể chưa cập nhật ngay khi mới mở.
- Tìm kiếm file Drive theo tên chỉ khi người dùng yêu cầu, tối đa 100 kết quả mỗi lượt.
- Giảm quét toàn bộ Drive: lọc Google Change Feed theo File ID và parent ID thuộc mapping; đối soát toàn cây mỗi 4 giờ, hoặc khi cần phục hồi checkpoint. Vẫn giữ quét định kỳ bảo vệ.
- Đổi tên/di chuyển local hoặc remote chỉ cập nhật metadata/đường dẫn khi chứng minh nội dung không đổi; giữ nguyên File ID và lịch sử Drive. Nếu có xung đột, fallback logic giữ dữ liệu.
- Đồng bộ tạo thư mục rỗng từ máy lên Google Drive; không tự xóa remote empty folder.
- Download giữ file tạm sau sự cố mạng và thử `Range` để tải tiếp. Nếu server không hỗ trợ hoặc checksum sai, tải lại từ đầu.
- Retry GET có giới hạn, có jitter; không retry ghi mù để tránh tạo bản sao.
- **Chưa PASS OAuth/Drive thật trên máy người dùng.** CI chỉ kiểm tra mã, UI và mô phỏng offline. Chưa triển khai đủ 33 hạng mục được đề xuất.

- Tài liệu Google Docs/Sheets/Slides/Drawings trong thư mục được đồng bộ thành shortcut có hậu tố `.gdrive.url`; không phải bản sao chỉnh sửa offline. Không tự động xóa shortcut cũ khi tài liệu bị xóa từ Drive để tránh mất dữ liệu.
- Tại cửa sổ **Tìm kiếm Drive** có thể xuất **bản sao Office/PDF** theo yêu cầu người dùng, tuân thủ giới hạn export của Google (ví dụ tệp vượt 10 MB có thể không xuất được qua endpoint thông thường). Không tự xuất hàng loạt.

## v0.4.1 — Chế độ cá nhân (1 tài khoản / 1 cặp thư mục)

- EXE Portable, không yêu cầu bộ cài hay quyền quản trị. Chỉ một Gmail đăng nhập và một cặp thư mục Windows ↔ Drive hoạt động tại một thời điểm.
- **Không cho chọn toàn bộ My Drive hoặc toàn bộ ổ đĩa Windows.** Không đồng bộ thư mục chứa token/cấu hình DriveSync.
- Tự động từ chối truy cập đường dẫn ngoài phạm vi, đường dẫn `..`, symlink, junction hoặc đường dẫn đã được chuyển ra ngoài thư mục đã thiết lập.
- Kiểm tra Drive File ID và chuỗi thư mục cha bằng API trước khi tạo, cập nhật, di chuyển, xóa file. Nếu file đã rời khỏi vùng được chọn thì dừng thay vì sửa/xóa ngoài phạm vi.
- Tìm kiếm chỉ duyệt trong thư mục Drive được chọn, không tìm kiếm toàn bộ Drive. Xuất thủ công Office/PDF cũng chỉ cho phép trong thư mục Windows đã thiết lập.
- Các thao tác đồng bộ chỉ ảnh hưởng file nghiệp vụ bên trong cặp thư mục đã thiết lập; không tự sửa/xóa file nghiệp vụ ngoài hai thư mục.
- Dữ liệu **nội bộ ứng dụng** (OAuth token mã hóa, cấu hình, lịch sử và Recovery khi bật đồng bộ xóa) vẫn lưu tại `%LOCALAPPDATA%\DriveSyncPortable`. Đây là dữ liệu hệ thống của ứng dụng, không phải đồng bộ file nghiệp vụ ngoài phạm vi.
- Offline smoke: kiểm tra chặn đường dẫn vượt thư mục, chặn chọn cả My Drive và không xóa file bị chuyển ra ngoài Drive khi cache cũ.
- Lưu ý: API metadata xác minh trước khi ghi giúp giảm rủi ro, nhưng Google có thể có thay đổi đồng thời trong khoảng kiểm tra–ghi. Không khẳng định bảo đảm tuyệt đối 100% khi có tiến trình khác liên tục di chuyển file.
- Chỉ phát hành bản ZIP test. Production PASS đòi hỏi kiểm tra trên Windows/Google Drive thật; không merge vào WMS `main`.

## v0.4.2 — Hoàn thiện chức năng trong phạm vi cá nhân

- Tiếp tục Upload sau đứt mạng/khởi động lại bằng phiên upload mã hóa DPAPI và chunk 8 MiB; dùng trạng thái HTTP 308 của Google, không tự gửi lại từ đầu nếu session còn hiệu lực. Chưa field test Google thật.
- Đổi tên/di chuyển thư mục con trên máy hoặc trên Google Drive: giữ cùng ID thư mục khi nhận diện được thay đổi, tránh tải toàn bộ dữ liệu lại. Rename trên laptop khi EXE đã tắt chưa có bảo đảm nhận diện chính xác nếu có nhiều thư mục giống nhau.
- Hiển thị số file chờ xử lý, ưu tiên file nhỏ, bỏ qua file không thay đổi (giảm thao tác local và API khi không cần).
- Quản lý Viewer/Editor cho file nằm trong thư mục đã chọn; toàn bộ thay đổi quyền là thao tác chủ động có xác nhận, không cấp quyền tự động.
- Xem 100 phiên bản gần nhất do Drive API cung cấp đối với file dạng binary; tải bản sao cũ vào chính thư mục local đã chọn, không ghi đè bản hiện tại. Tài liệu Google-native mở website để xem lịch sử.
- Tùy chọn proxy HTTP/HTTPS (không cho nhập mật khẩu trong URL), giữ nguyên proxy hệ thống khi để trống.
- Phím tắt Windows Ctrl+Alt+D mở lại cửa sổ từ tray. Không cài shell extension, không yêu cầu Admin.
- Upload session URL mã hóa DPAPI, tự xóa phiên khi thay tài khoản, thay cặp thư mục hoặc Logout.
- CI kiểm tra build portable Windows x64, giao diện, bố cục và đồng bộ offline; chưa xác nhận OAuth/Google Drive thật. Không merge vào WMS main.

## v0.4.3 — Theo dõi và xử lý xóa thư mục rỗng
- Khi bật đồng bộ xóa, phần mềm chỉ đưa thư mục trên Drive vào Thùng rác nếu thư mục từng đồng bộ thành công, đã xóa ở Windows, và Google API xác nhận không còn bất kỳ file/thư mục con nào. Không xóa vĩnh viễn.
- Nếu xuất hiện file mới trên Drive trong khi thư mục bị xóa trên Windows, file mới sẽ được tải về và thư mục được giữ lại. Không tự xóa thư mục chưa có checkpoint trước đó.
- Kiểm thử bổ sung: xóa thư mục rỗng, bảo vệ thư mục có file mới trên Drive, không tái tạo thư mục đã xóa.
- Phạm vi giữ nguyên một Gmail, một cặp thư mục; build Portable riêng biệt với WMS. Chưa xác nhận bằng dữ liệu Google thật.
