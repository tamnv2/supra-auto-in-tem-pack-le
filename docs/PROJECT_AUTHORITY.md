# SUPRA Auto In Tem Pack Lẻ — Canonical Project Context

Owner quyết định 2026-10-15: tách sang repo \`tamnv2/supra-auto-in-tem-pack-le\`, độc lập với Inventory/Agent. Không copy credential/token hay dữ liệu thật sang repo public.

## Chu trình
1. Cửa sổ WMS riêng, dùng WebView2 InPrivate. Operator login WMS + chọn máy in, rồi ấn Ẩn Web. Không cấu hình lại máy in tự động trong từng lượt.
2. App chính: Tote → Enter; Số kiện → Enter/OK; toàn bộ tên nhân sự hiện để chọn, bắt buộc xác nhận người thực hiện. Nhân sự dùng hai ca, lưu local, nhập nhiều bằng dấu phẩy, sửa/xóa/xóa tất cả.
3. Bắt đầu: đưa Tote vào \`Quét mã PTVC để đóng gói\` + Enter; đúng WMS packing và đúng SO/DO; click \`ĐÓNG GÓI HÀNG CHẴN\`, đợi \`Số lượng\`; xóa sạch số cũ, nhập đúng số mới và đọc lại; \`Cập Nhật\` → \`Có\`.
4. Chờ UI đã đóng gói → nhấn duy nhất nút in trên trang hàng chẵn → \`In Nhãn\` → \`HOÀN THÀNH ĐÓNG GÓI\` → \`Có\` → \`Không\` (không in phiếu kiểm).
5. Chỉ khi WMS trở về route và ô quét PTVC đúng thì hoàn thành **vòng giao diện** và cho nhập lượt sau. Không khẳng định trạng thái máy in vật lý.
6. Tọa độ/index/Angular dynamic IDs không phải nguồn chọn nút. Mỗi bước bắt buộc một đích hợp lệ visible/enabled trong đúng ngữ cảnh; sai thì dừng và cho người thao tác thủ công.
7. Lịch sử local: \`dd/MM/yyyy\`, bắt đầu/kết thúc \`HH:mm:ss\`, giây, tên người, Mã SO/Mã DO lấy từ vùng \`THÔNG TIN SO ĐANG ĐÓNG GÓI\`, kết quả. Export lịch sử XLSX riêng; log ZIP riêng, chọn ngày hoặc khoảng ngày hoặc tất cả.
8. Log phải có phiên bản, stage, mã bước, thời gian, elapsed, lỗi dạng mã, số lượng target/điều kiện; không lưu raw Tote/số kiện/SO/DO/người, cookie, token, WMS HTML, header, body, mật khẩu. Lịch sử nghiệp vụ có SO/DO/người và chỉ gửi khi người dùng chủ động xuất.
9. Cập nhật chỉ khi ấn nút và idle; manifest/release đúng repo riêng, SHA256, tải staging, bản hiện tại chạy bình thường nếu mất mạng GitHub. Hỗ trợ cài đè nếu Office chặn.
10. Không dùng các dịch vụ Cloudflare/Firebase/Apps Script/Google Drive của mô hình khác; không sửa bất kỳ tài nguyên Inventory nào.

## Evidence 
Recorder v2: 66 sự kiện / 71,4 giây, bước quét Tote → đóng gói chẵn → số lượng → cập nhật/Có → in nhãn → hoàn thành/Có/Không → về PTVC. Lần chọn lại máy in trong mẫu là sai sót thao tác, không phải bước nghiệp vụ bắt buộc. Đây là **bằng chứng một vòng thủ công**; build pass không phải field pass.

## Test gates
- Build Windows x64 pass trên GitHub; test UI đối tượng/nhãn, số kiện 5→6, hai hộp thoại Có/Không, lỗi mạng, nhập tên hàng loạt và xóa tất cả, xuất xlsx/log lọc thời gian, bảo mật/log, update checksum/rollback.
- Test WMS tại mạng Office do Owner xác nhận; chưa test thật thì không tuyên bố phần mềm chạy ổn định hoặc in thành công.

## Field review 2026-10-15 — hậu kiểm v1.0.0, yêu cầu sửa v1.1
- Owner bổ sung ZIP log JSONL local; kiểm tra 51 sự kiện ngày 15/10. Lượt thứ nhất bị dừng thủ công trong khi chờ PACK_BUTTON; lượt thứ hai đã gửi In Nhãn và xác nhận hoàn tất, nhưng `ACTION_NO` bị `DIALOG_NOT_CHANGED` tại 19:40:30.929. Không đồng nghĩa xác nhận máy in vật lý đã in.
- WMS thực tế hiển thị câu hỏi in **phiếu xuất kho** sau hoàn tất. Chỉ được bấm **Không** nếu dialog đang hiện chứa chính câu hỏi này; không dùng định danh DOM của dialog trước đó và không bấm theo `noCount` đơn thuần.
- Chờ UI ổn định qua ít nhất hai lần quan sát trước các bước có tác động; không tự thử lại tác vụ in/đóng gói khi chưa xác định.
- Ứng dụng chỉ hiển thị lịch sử trong ngày, dữ liệu cũ vẫn ở ổ local; xuất XLSX một ngày/khoảng ngày/tất cả, có bộ lọc trạng thái.
- Dù lượt chưa hoàn tất vẫn có dòng lịch sử; trạng thái người dùng phải Việt hóa. Giữ log kỹ thuật dưới dạng mã, không ghi nội dung SO/DO/người/Tote vào log.
- Layout phải dùng hiệu quả diện tích cửa sổ và co giãn theo kích thước.
- PR #3 là **bản nháp chưa được phát hành** khi chưa sửa phần điều khiển hộp thoại ở `WmsWorkflowJs.cs`; tuyệt đối không tuyên bố WMS/physical print PASS chỉ dựa trên CI.
