# DriveSync Portable v0.3 — Nhật ký thay đổi

## Phạm vi

Triển khai giao diện và đo tốc độ truyền dữ liệu thực tế cho ứng dụng độc lập DriveSync Portable. Không sửa nghiệp vụ hoặc EXE WMS. Không thay đổi mapping Google Drive, checkpoint, token đang lưu hoặc cơ chế đăng nhập.

## Tính năng

- **Tải lên / tải xuống:** hai thẻ tốc độ riêng, đơn vị B/s, KB/s, MB/s; số byte thực sự đọc/ghi trong luồng HTTP, cập nhật tối đa khoảng 4 lần/giây. Đây là thông lượng quan sát tại tầng ứng dụng, có thể lệch với lưu lượng mạng/Google báo cáo do buffer.
- **Lịch sử:** bảng ListView toàn chiều rộng có 4 cột Ngày giờ / Thao tác / Tệp, nội dung / Kết quả; cuộn, hiển thị tới 500 bản ghi. Lưu nhật ký dưới `%LOCALAPPDATA%\DriveSyncPortable\activity-history.log`, không tự upload log.
- **Trạng thái:** có loại trạng thái rõ ràng (chưa thiết lập; đang đăng nhập; đang kiểm tra; đang đồng bộ tải lên; đang đồng bộ tải xuống; đã đồng bộ; tạm dừng; chờ mạng/giới hạn; lỗi xác thực; có file cần xử lý; không thấy thư mục; đang thoát; xung đột). Chỉ báo Đã đồng bộ khi đợt quét thành công, không có file lỗi, xung đột hoặc thay đổi còn chờ.
- **Cửa sổ:** nút X và Alt+F4 luôn **ẩn vào khay hệ thống**; engine tiếp tục chạy. Nút Chạy ngầm giữ cùng hành vi.
- **Thoát:** nút Thoát ứng dụng màu đỏ nhạt ở màn hình chính, hai hộp xác nhận Yes/No liên tiếp (mặc định No). Menu khay Thoát đi qua cùng xác nhận; nếu nhấn từ tray, hiện lại cửa sổ trước. Chỉ thoát sau khi yêu cầu hủy và chờ engine dừng. Khi Windows Shutdown không cố chặn shutdown của hệ điều hành.

## Giao diện

Giữ màu xanh dương/xanh lá và công nghệ WinForms của bản đã duyệt; bố cục mới:
1. Trạng thái rõ ràng ở trên cùng;
2. Hai ô tốc độ Upload và Download;
3. Gmail + hai ô chọn thư mục;
4. Một hàng Thiết lập nhanh và Thoát;
5. Lịch sử chiếm toàn bộ chiều ngang và phần lớn chiều cao còn lại.

## Test cần kiểm tra trên Windows

- CI win-x64 self-contained build, UI smoke: các nhãn, thanh tốc độ, 4 cột lịch sử và trạng thái hợp lệ.
- CI sync offline smoke: upload, download, conflict, trash, recovery, restart; không có truy cập Google thật trong môi trường CI.
- Kiểm thử thủ công X -> khay, mở lại; từ màn hình chính và menu tray đều phải qua hai hộp xác nhận; bấm Không ở bất kỳ hộp nào giữ ứng dụng chạy.
- Trong khi tải file lớn, xác nhận tốc độ lên/xuống có giá trị biến thiên, sau khi hoàn thành về 0 B/s.
- Trường hợp thiếu mạng, file đang bị Excel khóa, token hết hạn, thư mục bị rút, hai phía cùng sửa file: kiểm tra trạng thái không báo hoàn tất sai.
- Kiểm tra Windows DPI 100/125/150%, lịch sử cuộn và không cắt chữ ở kích thước cửa sổ tối thiểu.

**Lưu ý:** Build và offline smoke PASS không phải kiểm thử Gmail thật trên laptop công ty. Không bật chế độ xóa hai chiều trên thư mục dữ liệu thật trước khi field PASS.
