# DriveSync Portable v0.3.1 — Sửa giao diện Windows

## Lỗi xác nhận qua ảnh chụp người dùng
- Góc của các thẻ bị cụt, có mảng trắng đè lên nhau.
- Thẻ ở cùng hàng có đoạn viền bị thiếu; nhãn/nút có cảm giác "hở" hoặc phủ nền sai màu.
- Khung nội dung dàn quá rộng trên màn hình lớn; lịch sử có nhiều vùng trống.
- Dòng thời gian lịch sử bị cắt, đường dẫn file dài khó đọc.
- Cửa sổ nhỏ (~825 px) có khả năng nút Chạy ngầm đè lên toggle tự khởi động.

## Nguyên nhân trong mã
`RoundedPanel.OnPaintBackground` đã xóa toàn bộ nền với màu của cha rồi mới tô hình bo góc. Các `Label.BackColor = Transparent` yêu cầu WinForms mô phỏng trong suốt bằng cách vẽ lại nền của RoundedPanel bên dưới; việc này có thể dịch gốc vẽ, tạo ra mảng trắng, góc bị cụt và viền nối sai. `RoundedButton` cũng tô góc theo `Parent.BackColor` trong khi `FillColor` card không được phản ánh vào thuộc tính `BackColor`. Đó là lỗi painting, không phải dữ liệu.

## Đã thay đổi
- `RoundedPanel`: sử dụng `Region` bo góc thật để cắt đúng viền, nền card được tô kín; `BackColor` luôn cùng giá trị với `FillColor`.
- Các label bên trong card dùng màu nền đặc, đồng nhất với card, không cho phép pseudo-transparency gây lỗi khi vẽ lại.
- Bo góc và đường viền 1 px, anti-alias, invalidation đúng khi resize/thay đổi trạng thái.
- Giới hạn chiều rộng vùng chính 1328 px, căn giữa; các hàng card cùng lề và gap 12 px.
- Card lịch sử co theo số dòng và chiều cao cửa sổ, không mở một khối trắng quá lớn khi chỉ có vài bản ghi.
- Cột Thời gian 158 px để thấy `dd/MM/yyyy HH:mm:ss`, tên file được hiển thị ellipsis và có tooltip; bố cục bốn cột giữ nguyên.
- Header dùng icon PNG từ icon của chính EXE, không dùng ký tự mũi tên có thể bị lỗi font.
- Tại màn hình hẹp, nhóm 3 nút hành động xuống dòng riêng tránh đè lên toggle.
- Status icon nhỏ hơn, không tự thêm dấu ba chấm.
- Loại một dòng footer kỹ thuật bị lặp.
- Giữ nguyên Google OAuth/DPAPI, cơ chế tự đồng bộ, cấu hình mapping, nhịp kiểm tra, bảo vệ file xung đột, lịch sử lưu cục bộ và nút X/Thoát hai lần.

## Xác minh
- CI Windows build self-contained, UI smoke, SyncEngine offline smoke.
- Thêm **LayoutAudit** kiểm tra 8 rounded cards, `BackColor` của nhãn, vùng clipping, không để chồng card, không chồng toggle và button, không vượt max-width; chạy tại ba kích cỡ cửa sổ.
- Xuất ảnh kiểm tra của WinForms từ runner để soi cấu trúc; bản này không thay thế ảnh kiểm chứng trên laptop người dùng (DPI, font, display khác nhau).
- Không thể tuyên bố kiểm thử Google Drive thực tế với tài khoản cá nhân chỉ từ CI offline.

## Cập nhật từ v0.3
1. Thoát v0.3 bằng nút **Thoát ứng dụng** và xác nhận hai lần.
2. Giải nén v0.3.1 sang cùng vị trí sử dụng trước đó; thay thế EXE sau khi chương trình đã đóng.
3. **Không xóa** `%LOCALAPPDATA%\DriveSyncPortable` vì chứa token, mapping và lịch sử.
4. Mở v0.3.1; xác minh giao diện và thực hiện thử đồng bộ một file nhỏ trước khi làm việc với dữ liệu quan trọng.

Không được merge PR chứa app này vào main WMS khi chưa có quyết định tách repo độc lập.
