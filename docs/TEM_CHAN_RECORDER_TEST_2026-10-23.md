# Bản test độc lập — Ghi thao tác IN TEM CHẴN (23/10/2026)

## Yêu cầu Owner
Cùng hệ thống WMS có hai nghiệp vụ khác nhau:
1. In tem lẻ / đóng thùng: tiếp tục dùng ứng dụng chính, KHÔNG chỉnh luồng đã có.
2. In tem chẵn: trước khi lập trình tự động hóa, cần ghi đủ các bước nhân viên tự thao tác trên WMS ở `https://wms-supra.winmart.vn/sft3/app/saleorder/list`.

## Công cụ thử nghiệm
Mã nguồn: `tools/TemChanRecorder/`. Đây là ứng dụng WinForms .NET 8 + WebView2 **độc lập**, không sửa MainForm, WmsWorkflowJs, LocalStore hoặc dữ liệu của phần mềm đang chạy.

- Mở app thử nghiệm `SUPRA-InTemChan-Recorder-TEST.exe`, đăng nhập WMS thủ công trong vùng WebView2 (có thể phải qua SSO).
- Xác nhận màn hình danh sách SO, bấm **BẮT ĐẦU GHI**, thao tác một vòng in tem chẵn bằng tay, bấm **KẾT THÚC · LƯU DESKTOP**.
- Trên Desktop xuất `SUPRA_InTemChan_DOM_YYYYMMDD_HHMMSS_mmm.zip` gồm `events.jsonl`, `README.txt`.
- Log trình tự thời gian: click/pointerdown, focus, change, phím điều hướng/Enter, submit, navigation, route SPA, danh sách control/hộp thoại sau biến động DOM, beforeprint/afterprint nếu site phát sự kiện.
- Selector có kiểm tra tính duy nhất; ghi role, tag, name, title, aria-label, placeholder, formControlName, testId, text của các nút, tiêu đề hộp thoại, loại phần tử, enabled/visible. **Không dùng tọa độ, index hàng, CSS :nth-child hay :nth-of-type để tự click.**
- Tại giai đoạn này hoàn toàn không tự động bấm WMS hoặc in tem; tool chỉ quan sát thao tác người dùng.

## Bảo mật và giới hạn
- Không đọc **giá trị nhập** (input.value), password, token, cookie, header/body HTTP, localStorage/sessionStorage, HTML toàn trang, ảnh chụp, clipboard, hoặc profile WebView2.
- Chặn ghi khi không ở trang WMS sau login, bỏ qua control đăng nhập, xác thực OTP, trường/mẫu form nhạy cảm. Mọi text/selector đều qua lọc thêm tại .NET host.
- Không tự tải lên, không gửi qua dịch vụ khác; log ghi local. WebView2 profile thử nghiệm dưới `%LOCALAPPDATA%\SupraTemChanRecorder\WebView2-Profile` **tách biệt** profile app chính. Để browser không tự lưu mật khẩu qua autofill, `IsPasswordAutosaveEnabled=false`.
- Chỉ cho phép điều hướng sang `wms-supra.winmart.vn` hoặc `auth-supra.winmart.vn` qua HTTPS. Dữ liệu phiên login có thể do WebView2 tự quản lý trong profile; mã ứng dụng không truy xuất profile.
- Đã loại bỏ giá trị ô nhập, do đó nếu trình tự chọn SO/đơn cần một khóa nghiệp vụ cụ thể, phải xác định thêm quy tắc locator ở giai đoạn xây automation; **không được đoán hoặc click bản ghi đầu tiên**.
- Sự kiện Windows native print dialog / máy in vật lý không thuộc DOM; `beforeprint/afterprint` không chứng minh giấy/tem đã in. Một số widget trong iframe khác origin hoặc canvas cũng không thể nhận diện đầy đủ bằng DOM hiện tại.
- Text WMS có thể có thông tin kinh doanh, nên chủ động kiểm tra ZIP trước khi chia sẻ; không commit ZIP/log có dữ liệu thật vào repository public.

## Quy trình đánh giá
1. CI: Node JS syntax + regression về loại trừ thông tin nhạy cảm, .NET Windows x64 publish, ZIP và SHA256.
2. Field: Owner chạy trên PC công ty với WebView2 Runtime và WMS thực, đăng nhập và thao tác đầy đủ; gửi ZIP đã rà soát.
3. Sau khi có log: dựng state machine in tem chẵn (thứ tự màn hình, DOM selectors ưu tiên ngữ nghĩa, điều kiện chặn lỗi/tránh in trùng) trước khi tích hợp vào app chính.

**CI PASS chỉ chứng minh build và kiểm thử tĩnh, KHÔNG chứng minh WMS/SSO/máy in thật hoạt động.**
