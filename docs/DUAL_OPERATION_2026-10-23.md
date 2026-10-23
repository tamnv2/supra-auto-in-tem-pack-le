# V1.8.0 — Hai nghiệp vụ In Tem (23/10/2026)

## Yêu cầu vận hành đã chốt
- Giữ nguyên nghiệp vụ **IN TEM LẺ AUTO**: Tote / số kiện / chọn nhân sự, quy trình packing, lịch sử checkpoint, Excel và thống kê.
- Thêm **IN TEM CHẴN**: nhập một mã SO và Enter. Truy cập trực tiếp `https://wms-supra.winmart.vn/sft3/app/saleorder/details/{SO}?PickType=PickOrder`. Không yêu cầu tìm SO trên màn danh sách.
- Sau khi tải chi tiết: kiểm tra **trạng thái phải đúng `Đã lưu trữ hàng sau đóng gói`**. Nếu chưa đúng hoặc không xác minh được thì dừng và cảnh báo.
- Chọn tab `THÔNG TIN KIỆN HÀNG` bằng role/text; tìm đúng một nút `print` trong hàng của bảng kiện. Nếu có nhiều nút/không nhận diện được thì dừng và cảnh báo.
- Modal `Tùy chọn in nhãn`: đọc `startPackage`, `endPackage`, `Tổng số thùng`; tất cả là số nguyên dương, đầu <= cuối <= tổng, mã SO trong hộp thoại trùng mã yêu cầu. Không cho phép 0, số âm, 1.3, 1.5, 0.6 hay vượt tổng.
- Chỉ khi mọi điều kiện PASS mới click duy nhất `In Nhãn` trong đúng modal. Chờ modal đóng; sau khi WMS đóng modal, hoàn tất vòng giao diện và mở ô SO để in tiếp SO mới. **Không khẳng định máy in đã thực sự ra tem** chỉ dựa trên đóng modal.
- Nghiệp vụ chẵn **không ghi lịch sử nghiệp vụ**; chỉ hiển thị các bước trong phiên và cảnh báo hộp thoại; nút Ẩn/Hiện trình duyệt.
- Khởi động mở hộp chọn hai nghiệp vụ; tab `CÀI ĐẶT` có hai nút đổi nghiệp vụ mà không cần khởi động lại. Chặn chuyển khi có lượt in đang chạy, đang ghi WMS recorder, workflow in lẻ bị kẹt hoặc có lệnh in chẵn cần kiểm tra.
- Cùng WebView2 với ứng dụng chính, không tạo hồ sơ đăng nhập mới, không can thiệp cookies/token mật khẩu.

## Dẫn chứng
- User đã cung cấp ảnh trang chi tiết SO, ảnh modal và ZIP `SUPRA_InTemChan_DOM_20261023_162416_709.zip` (không commit ZIP thật). Trong ZIP có 34 sự kiện/15,3 giây gồm:
  - tab `role=tab` text `THÔNG TIN KIỆN HÀNG`
  - nút `button.app-table-action-button` chứa icon `print`
  - `mat-dialog-container`, title `Tùy chọn in nhãn`
  - `input[name="startPackage"]`, `input[name="endPackage"]`
  - nút `In Nhãn` trong hộp thoại
- Các selector không bấm theo tọa độ, DOM index hoặc một CSS class động `ng-tns-...`. Giới hạn một đối tượng hợp lệ / một hàng để tránh in nhầm.

## Triển khai kỹ thuật và kiểm thử
- `WmsTemChanJs.cs`: xác minh route, trạng thái, tab, modal, số thùng, guard hành động.
- `TemChanPanel.cs`: ô SO và Enter, UI tiến trình, cảnh báo, chặn nhập song song, không tự retry in.
- `WmsBrowser.cs`: điều hướng theo mode và cầu nối JS WebView2, profile cũ.
- `MainForm.cs`: startup choice + tab settings + chuyển hot mode; giữ các tab thao tác/lịch sử in lẻ.
- `tests/v1.8-dual-operation.cjs`: giả lập DOM WMS, trạng thái sai, số thập phân, phạm vi vượt, ambiguous buttons, click một lần.
- .NET Windows CI PASS là code PASS; **P0 thử trên máy mạng Office/WMS/máy in thực tế** gồm đăng nhập, trạng thái sai, endPackage thập phân, SO mới, chuyển nghiệp vụ và giao diện DPI; chưa có field PASS thì không được tuyên bố in thực tế thành công.
- **Không ghi nội dung SO thật trong diagnostic log** và không đưa ZIP log thực tế chứa dữ liệu kinh doanh lên repo.

## Hạn chế cần chờ field evidence
- WMS trang chi tiết có thể thay đổi DOM/bố cục và truy cập khác theo phân quyền; nếu không đọc được trạng thái có độ tin cậy đủ cao thì fail closed.
- Trang có nhiều dòng kiện/nhiều nút in thì phải xác nhận quy tắc chọn kiện trước khi hỗ trợ tự động, không click dòng đầu một cách mù quáng.
- Hộp thoại in của Windows hoặc việc tem vật lý ra máy không thể chứng minh qua DOM.
