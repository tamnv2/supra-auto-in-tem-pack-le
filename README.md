# SUPRA Auto In Tem Pack Lẻ

Ứng dụng Windows độc lập: quét Tote → nhập số kiện → chọn nhân sự → đóng gói hàng chẵn → in nhãn → hoàn tất → trở lại ô quét PTVC.

## Vận hành
- Trình duyệt WMS WebView2 **cửa sổ riêng**, đăng nhập/chọn máy in bằng tay rồi ẩn. Mỗi lần mở app phải đăng nhập lại (InPrivate).
- Quét Tote + Enter chuyển focus số kiện; số kiện + Enter tương đương OK; hiển thị đầy đủ tên và hỏi xác nhận.
- Quản lý nhân sự local: thêm/sửa/xóa, nhập nhiều tên ngăn cách dấu phẩy, xóa tất cả có xác nhận.
- Một thời điểm một Tote; dừng khi DOM mơ hồ, thời gian chờ hết, phiên sai hoặc lỗi in; không tự in lại.
- Lưu lịch sử local gồm ngày, giờ đầu/cuối, thời lượng, người thực hiện, SO, DO; xuất XLSX riêng.
- Log JSONL local với tên bước, mã lỗi, thời gian và thống kê nhận diện nút; xuất ZIP log riêng, lọc theo một ngày/khoảng ngày/tất cả.
- Nút Cập nhật kiểm tra GitHub Release khi được bấm, xác minh SHA-256 trước khi cài bản mới lúc không xử lý.

## Phát hành
GitHub Actions chạy Windows runner để build \`win-x64\` self-contained; tải ZIP từ Artifacts hoặc Releases. EXE yêu cầu WebView2 Runtime trên máy dùng. Cập nhật qua mạng Office có thể không thành công nếu GitHub bị chặn; trường hợp đó tải ZIP thủ công để cài đè.

## An toàn
Không lưu, đọc hoặc log cookie, token, mật khẩu, HTTP headers, request/response WMS, query URL. Không commit lịch sử, nhân sự hoặc log thật vào repo public. EXE tự động thao tác UI WMS, **không chứng minh máy in đã in vật lý**, chỉ xác nhận khi trang PTVC hiện lại.
