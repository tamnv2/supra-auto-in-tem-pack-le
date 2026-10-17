# WMS Packing & Productivity Assistant

## Tài liệu bàn giao cho chat tiếp theo

**Nguồn trạng thái và backlog mới nhất (17/10/2026): [docs/HANDOFF_2026-10-17.md](docs/HANDOFF_2026-10-17.md).** Phải đọc trước khi sửa code; các mục ở các phiên bản trước là lịch sử và có thể đã bị thay thế.


Ứng dụng Windows độc lập **hỗ trợ đóng gói WMS và ghi nhận năng suất local**; không phải phần mềm chính thức do nhà cung cấp WMS phát hành.

## Bốn tab
- **NGHIỆP VỤ**: quét Tote, nhập số kiện, chọn nhân sự, quản lý danh sách, lịch sử và xuất XLSX/ZIP log.
- **TRÌNH DUYỆT WMS**: WebView2 gắn trực tiếp trong cửa sổ, mở đầu tiên để đăng nhập/chọn máy in, có thể trở lại bằng tab hoặc nút Mở trình duyệt.
- **SẢN LƯỢNG HÔM NAY**: thống kê DO duy nhất đã hoàn tất, lượt, điểm khi có dữ liệu, thời gian xử lý theo người; không coi là giờ công và không chấm điểm nếu thiếu dữ liệu.
- **THÔNG TIN PHẦN MỀM**: công khai mục đích, dữ liệu, quyền tác động và bật/tắt tự khởi động cùng Windows.

Ứng dụng **có thực hiện cập nhật số kiện, in nhãn và xác nhận hoàn tất** trong quy trình đóng gói khi người dùng khởi tạo; không sửa các thông số quản trị, phân quyền hoặc danh mục WMS. Không lặp lại thao tác in khi trạng thái không chắc chắn.

## Dữ liệu và bảo mật
- Hồ sơ WebView2 cố định tại `%LOCALAPPDATA%\SupraAutoPackLe\webview2-profile`, riêng theo tài khoản Windows. Browser có thể hỏi lưu mật khẩu, lưu phiên đăng nhập/cookie/token theo cơ chế riêng của WebView2. Ứng dụng **không đọc hoặc xuất** các giá trị này. Khả năng lưu mật khẩu tùy WMS và chính sách máy.
- Lịch sử và nhân sự tiếp tục lưu ở `%LOCALAPPDATA%\SupraAutoPackLe\history.jsonl` và `people.json`; đường dẫn không đổi để giữ dữ liệu v1.3.0.
- Log kỹ thuật local `logs/` chỉ ghi phiên bản, bước, thời lượng, mã trạng thái và số lượng target UI; không lưu Tote, SO/DO, nhân sự, HTML, mật khẩu, token, header hoặc request/response. Xuất log thủ công; không tự gửi dữ liệu ra mạng.
- JavaScript chỉ thao tác DOM packing WMS được xác định; không quét máy hoặc tiến trình khác. Đọc thông tin SO/DO hiện tại, có thể mở rộng trường SKU/số kiện/số điểm trong phiên bản tương lai sau khi xác minh giao diện.

## Tự khởi động và cập nhật
- Checkbox tự khởi động chỉ ghi `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\WmsPackingAssistant` cho tài khoản Windows hiện tại. Không yêu cầu Administrator, không dùng HKLM, service hoặc tác vụ lên lịch. Chính sách IT có thể giới hạn.
- **Không có updater online trong ứng dụng.** Các bản mới được cài thủ công bằng ZIP/EXE theo chính sách công ty.
- v1.4.0 đổi tên EXE thành `WmsPackingAssistant.exe`; gói `WmsPackingAssistant-win-x64.zip`. Khi cài bản mới, thay shortcut trỏ đến file mới và không xóa thư mục dữ liệu local.

## Kiểm thử
GitHub Actions chạy kiểm tra JavaScript, test tương tác dialog, regression kiến trúc, compile Windows x64, smoke XLSX/log/lịch sử và đóng gói checksum SHA256. CI không thay thế kiểm thử trực tiếp trên WMS, mạng Office và máy in. Đặc biệt cần thử chuyển qua tab Nghiệp vụ mà WebView2 vẫn xử lý được và thử browser-native lưu mật khẩu.

Chỉ dùng khi được doanh nghiệp cho phép. Không giả mạo trình duyệt hoặc né hệ thống quản lý thiết bị.


## v1.5.0 — Thiết lập WMS và sản lượng trong ngày

- Sau đăng nhập WMS, công cụ thử trở về màn hình đóng gói PTVC; nếu hiện đúng hộp thoại **NHẬP MÃ QUẦY ĐÓNG GÓI**, điền `SDG.24` và bấm Xác nhận. Sau đó tìm menu tài khoản → Thiết lập máy in; nếu trống, chọn `21-1129-D29 (Online)`, xác nhận; nếu đã có máy in thì đóng hộp. Chỉ click khi các phần tử được nhận diện duy nhất trong đúng ngữ cảnh.
- Nếu giao diện công ty khác với mẫu nhận diện, ứng dụng dừng tự thiết lập và chuyển đến tab WMS; người dùng tự hoàn tất rồi bấm Xác nhận WMS sẵn sàng. Không tự tuyên bố trạng thái khi chưa xác minh.
- Các tab tăng kích thước và nổi bật trạng thái chọn. Form nhập Tote/số kiện và nút bấm được mở rộng; một nút Thêm nhân sự chấp nhận `A,B,C` hoặc `A, B, C`; hộp chọn mặc định tên đầu tiên.
- Lịch sử hiển thị **Thời gian xử lí**, **Số SKU**, **Số kiện**, **Pcs**, **Điểm**; kết quả Excel đồng nhất. Trường mới nullable nên dữ liệu cũ không biến thành 0. Số kiện phiên bản này ghi từ số nhân viên nhập trên phần mềm (chưa xác minh độc lập từ WMS). Số SKU/Pcs/Điểm đợi thống nhất cách đọc WMS.
- Tab **Sản lượng hôm nay** tổng hợp số DO duy nhất hoàn tất, lượt thành công/chưa hoàn tất, tổng thời gian xử lý, trung vị thời gian và số điểm **nếu có** theo người từ lịch sử local. Không coi thời gian xử lý là thời gian ca, không xếp hạng năng suất khi thiếu điểm.

## v1.5.1 — Bố cục dễ đọc và khôi phục trình duyệt (17/10/2026)

- Mở ứng dụng chọn sẵn tab Trình duyệt WMS ngay từ lần hiển thị đầu tiên; các tab còn lại vẫn truy cập bình thường.
- Vẽ tab chỉ một dòng: Sản lượng / Thông tin là nhãn rút gọn trên thanh tab, trang đầy đủ vẫn mang tên Sản lượng hôm nay / Thông tin phần mềm.
- Tab Sản lượng: thay bản tóm tắt đa dòng dễ cắt chữ bằng tám thẻ thông tin, lưới và ô hướng dẫn cuộn; gọi chỉ số median là "Thời gian ở giữa". Công thức thống kê không đổi: giá trị đứng giữa khi sắp xếp lượt, nếu chẵn lấy trung bình hai giá trị giữa. Không chuyển thành số trung bình cộng của cả ngày.
- Tab Thông tin: sáu khung nội dung rõ ràng có thanh cuộn riêng; vị trí điều khiển quản lý tài khoản Windows và xóa trình duyệt ở phía trên, không ẩn cuối trang.
- Nút XÓA DỮ LIỆU TRÌNH DUYỆT chỉ khả dụng khi không có lượt Tote đang chạy; cần hộp thoại xác nhận nêu rõ mật khẩu, cookie, dữ liệu trang sẽ bị xóa. Gọi API WebView2 profile ClearBrowsingDataAsync() (không đọc mật khẩu/cookie/token) rồi mở lại WMS; phải đăng nhập, xác nhận quầy và máy in lần nữa. Nếu thất bại thông báo rõ, không coi như đã xóa. Không được xóa LocalStore/history.jsonl, people.json hoặc logs.
- Không thêm quyền admin, không làm thay đổi bản ghi lịch sử hay quy trình in nhãn. Windows CI + kiểm thử thủ công ở máy công ty mới xác nhận giao diện / việc xóa profile thực tế.
