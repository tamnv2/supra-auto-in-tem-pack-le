# SUPRA WMS Recorder V5 — nhánh kiểm thử

Ứng dụng độc lập Windows x64 sử dụng **.NET 8 WinForms + Microsoft WebView2** như app in tem trong cùng repository. KHÔNG dùng bộ điều khiển Win32 tự viết bằng Go. Không thay đổi app đóng gói đang vận hành.

## Giao diện

- WebView2 chiếm toàn bộ phần nội dung. Thanh điều khiển là hàng riêng của WinForms, **không chèn vào WMS, không che menu máy in**.
- Nút BẮT ĐẦU GHI và KẾT THÚC & XUẤT LOG ở thanh trên.
- Tự mở trang WMS packing. Người dùng đăng nhập WMS bằng giao diện thật của trang và thử nhập quầy / chọn máy in.

## Ghi và xuất

Khi nhấn bắt đầu, ghi sự kiện DOM: click, pointerdown, Enter, Tab, Escape, chọn trường, tiêu đề/caption nút, selector, thay đổi thành phần trạng thái/hộp thoại, tín hiệu trước/sau in của trang. Chỉ thu mã quầy khớp mẫu SDG.x khi nhìn thấy, không thu nội dung trường nhập tự do, Tote, SO, DO, mật khẩu, cookie, token, nội dung mạng, ảnh màn hình. Các biểu thức lọc tránh email, số điện thoại, mã dài; không bảo đảm mọi chuỗi văn bản nghiệp vụ là phi nhạy cảm nên xem lại log trước khi chia sẻ.

Khi nhấn kết thúc: xuất **SUPRA_WMS_Trace_V5_*.zip** vào Desktop của tài khoản Windows (hỗ trợ OneDrive redirect từ Windows). ZIP chứa **events.jsonl** và **session.json**. Nếu xuất không thành công, file JSONL vẫn giữ tại %LOCALAPPDATA%\SupraWmsRecorderV5\Pending.

WebView2 dùng phiên **InPrivate** để không cố tình giữ thông tin đăng nhập giữa các lượt. Recorder không đọc/copy profile hoặc credential. Chỉ hoạt động cục bộ; không gửi log lên server. Phiên InPrivate có thể chịu chính sách doanh nghiệp hoặc Edge WebView2 Runtime.

## Yêu cầu cài đặt

Windows 10/11 x64, **Microsoft Edge WebView2 Runtime**, quyền sử dụng WMS; truy cập mạng WMS tương ứng. Không cần quyền Administrator cho chương trình. Không dùng khi chưa được phép ghi chép thao tác WMS.

## Kiểm tra

Github Actions kiểm tra build Windows và thử chạy GUI ở môi trường CI với trang HTML thử nghiệm: click 'Thiết lập máy in' phải xuất hiện trong events.jsonl. **Kiểm tra CI không thay thế thử thực tế WMS/máy in ở mạng Office.**

Để kiểm tra thực tế, mở EXE -> login WMS -> về packing -> bắt đầu ghi -> nhập SDG.22 -> chọn/kiểm tra máy in -> kết thúc & xuất ZIP. Gửi lại ZIP đã rà soát để phân tích. Các thay đổi cột và tự động thao tác WMS **chưa** thực hiện ở app chính.
