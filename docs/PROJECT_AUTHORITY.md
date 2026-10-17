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
- Owner đã commit sửa `WmsWorkflowJs.cs` lên PR #3: chỉ bấm Không khi nội dung hộp thoại xác nhận đúng câu hỏi phiếu xuất kho; mã này còn phải qua CI và thử nghiệm WMS tại mạng Office. `tests/wms-dialog-regression.cjs` kiểm tra hai câu hỏi cùng một DOM node và trường hợp nút không hợp lệ.
- Build/cập nhật dự kiến v1.1.0; workflow lấy phiên bản từ csproj. GitHub Actions PASS không tương đương xác nhận nhãn được in vật lý.

## v1.2.0 — quyết định bổ sung và phân tích log 20:27–20:33 ngày 15/10/2026
- Log do Owner cung cấp có 75 sự kiện (không đưa log gốc hoặc dữ liệu khách hàng lên repo). Lượt 20:31 bấm nút Print rồi chờ hộp thoại In Nhãn quá 20 giây và dừng. Lượt 20:32 đã click In Nhãn, Hoàn thành/Có/Không và trở về ô quét Tote lúc 20:33:06. Log cũ **không có event xác nhận ghi lịch sử** nên không thể xác quyết nguyên nhân cụ thể chỉ từ log.
- Lịch sử phiên mới sử dụng **nhật ký append-only theo mỗi lượt** với AttemptId; ghi ngay sau xác nhận nhân sự và cập nhật khi nhận thông tin WMS, sẵn sàng in, đã yêu cầu in, đã gửi lệnh in nhãn, kết thúc. Khi thất bại/crash vẫn tồn tại bản ghi gần nhất; trang lịch sử và Excel chỉ hiển thị bản ghi sau cùng của từng AttemptId. Không xóa hay thay đổi format các dòng lịch sử phiên cũ.
- Ghi file trực tiếp vào LocalAppData, flush xuống đĩa; log mỗi mốc SAVE_* DONE/ERROR không chứa Tote/SO/DO/nhân sự. Nếu lưu lỗi, dừng nghiệp vụ và yêu cầu kiểm tra ổ đĩa/quyền ghi. Khi đọc có lỗi sẽ hiển thị cảnh báo.
- Danh sách nhân sự không tự chọn mặc định; lựa chọn được tô nổi, in đậm, font lớn hơn 20%; nhấn Enter xác nhận, không thêm hộp thoại xác nhận sau đó. Click chọn và Enter, hoặc phím mũi tên chọn rồi Enter.
- Sau khi vòng giao diện kết thúc và cửa sổ sẵn sàng, app tự kích hoạt và đưa dấu nhập liệu về Tote. Không tự chạy lại thao tác không idempotent; nếu WMS chưa trở về quét Tote cần xác minh thủ công.
- Kiểm thử CI chỉ có giá trị biên dịch, smoke dữ liệu và giả lập. Chưa được xem như bằng chứng máy in vật lý hoặc WMS production PASS.

## v1.3.0 — Điều tra lịch sử trống sau khi in thành công (15/10/2026)
- Owner gửi ZIP log 21:00, v1.2.0: `SAVE_START`, `SAVE_WMS_INFO`, `SAVE_PRINT_READY`, `SAVE_LABEL_REQUEST`, `SAVE_FINISH_CONFIRMED`, `SAVE_FINAL` đều `DONE` và `JOB_UI_COMPLETE DONE`. XLSX vẫn trống theo báo cáo Owner; file `history.jsonl` thực tế không nằm trong ZIP nên chưa chứng minh được định dạng ngày đang lưu trên máy.
- Lỗi xác định trong source: `DateTime.ToString("dd/MM/yyyy")` **không dùng dấu / cố định** (nó là ký hiệu DateSeparator theo Windows Culture), trong khi lọc lịch sử lại dùng `TryParseExact` với InvariantCulture. Ngày kiểu `15-10-2026` hoặc `15.10.2026` bị ẩn khỏi bảng và XLSX dù file history được ghi thành công.
- Từ v1.3, ghi ngày cố định dùng dấu phân cách literal với `CultureInfo.InvariantCulture`. Khi đọc, chấp nhận ngày cũ dùng `/`, `-`, `.` (và ISO yyyy-MM-dd), chuẩn hóa **trong bộ nhớ**, không sửa/xóa bản ghi cũ, vẫn gộp AttemptId như trước.
- Ở START/FINAL, sau khi ghi `history.jsonl` phải kiểm tra **đọc ngược lại qua cùng logic như bảng/Excel**. Log `READBACK_START/FINAL DONE/ERROR`, `DISPLAY_COUNTS TODAY=...;READ_ERROR=...`, `HISTORY_XLSX ROWS=...`, không ghi PII/SO/DO/Tote vào log.
- Kiểm thử thêm tình huống Windows Culture dấu chấm và dữ liệu ngày lịch sử cũ dấu chấm/gạch; xuất XLSX chọn ngày cũ phải có số dòng đúng.
- Vẫn chưa thể xác nhận in nhãn vật lý / thao tác WMS bằng GitHub Actions; Owner test thực tế sau phát hành.


## v1.4.0 — Approved 2026-10-17: embedded tabs, browser password profile, standard user startup, manual update

- Name: WMS Packing & Productivity Assistant, clearly described as independent packing automation **and** productivity tracker. App writes parcel quantity in an authorized workflow, requests label printing and confirms completion; never describe it as read-only or official WMS software.
- UI: Operations, WMS Browser and About/Privacy tabs in one WinForms window; WebView2 is one persistent control, not a separate Form. Initially show WMS tab for manual login/printer setup; after successful WMS return, focus Tote on Operations. On uncertainty, show browser tab.
- Persistent WebView2 profile under existing LocalStore root. InPrivate disabled; native password autosave enabled at user request. Important exception to prior policy: native WebView2 profile **may store** password, cookies and session token; app code must not inspect, manipulate, log or export any credentials. General autofill disabled.
- Keep existing WMS SO/DO reading and future ability to read SKU count, parcel count, SO points; these additional metrics are **not implemented** without confirmed DOM source. Do not export business data in debug logs.
- Delete in-app online updater and update button. Release ZIP/EXE only for manual installation; preserve old history root, rename EXE to WmsPackingAssistant.exe.
- Optional auto-run with Windows uses current-user HKCU Run only, no admin, no service/task and no security-policy bypass; indicate failure if denied.
- About tab explicitly discloses UI WMS interactions, history vs technical logs, browser-managed credential persistence, no automated data uploads and independent product status.
- Field PASS requires testing on actual managed Windows machine/mạng Office for password save, background WebView2 while another tab is visible, in-label print dialogs, old history compatibility and standard-user autostart; CI cannot verify corporate permissions or physical printing.


## v1.5.0 — Owner yêu cầu 2026-10-17, UI sản lượng và thiết lập WMS

- Sau đăng nhập, thử điều hướng về `/sft3/app/saleorder/packing` chỉ từ WMS authenticated app route. Không can thiệp form đăng nhập, SSO, cookie hoặc token.
- Nếu đúng popup `NHẬP MÃ QUẦY ĐÓNG GÓI`, chỉ trong popup đúng ngữ cảnh tự nhập `SDG.24` và xác nhận sau khi kiểm tra giá trị. Không can thiệp các form khác.
- Chỉ trong menu tài khoản đúng ngữ nghĩa mở `Thiết lập máy in`: nếu chưa chọn, chọn duy nhất `21-1129-D29 (Online)`, kiểm tra lại, xác nhận; nếu đã có bất kỳ máy in nào, chỉ Đóng. Mọi selector phải duy nhất, enabled, visible; sai/không rõ thì dừng auto setup, yêu cầu người dùng xử lý thủ công qua tab WMS; không click theo tọa độ hoặc DOM index.
- Xác minh sẵn sàng sau thiết lập; nếu không xác định được menu/selector, người dùng được xác nhận bằng tay khi đang ở ô quét Tote và đã tự kiểm tra máy in. Không coi failed selector là sẵn sàng.
- Chỉnh UI WinForms: tab owner draw cao/rộng hơn, trạng thái chọn nổi bật; ô Tote/số kiện lớn chữ đậm; nút rõ và không che nội dung; lịch sử có horizontal scroll với `Thời gian xử lí`.
- Nhân sự: chỉ một nút thêm, nhập `A, B, C` hoặc `A,B,C` đều hợp lệ; chọn mặc định hàng đầu tiên trong hộp lựa chọn, nhấn Enter xác nhận, mũi tên để đổi tên.
- Mở rộng `HistoryRow` bằng các trường nullable SkuCount, ParcelCount, Pcs, Points để đọc/ghi tương thích bản cũ, xuất Excel và hiển thị lịch sử. ParcelCount lấy trực tiếp **số kiện nhân viên nhập trong ứng dụng**, kể cả lượt bị dừng; chưa phải số kiện độc lập được WMS xác minh. Số SKU, Pcs, Điểm chưa có logic WMS từ Owner: để null, không tự bịa số và không ghi 0 thay null.
- Tab `Sản lượng hôm nay` sử dụng đúng lịch sử đã ghi trong LocalStore: số lượt, số DO duy nhất hoàn tất theo người, các lượt cần kiểm tra, tổng/ trung vị thời gian xử lý **chỉ trong lượt hoàn tất**. Điểm chỉ cộng những lượt có điểm, kèm số lượng được ghi nhận; trống phải hiển thị `Chưa có dữ liệu`. Không suy luận số giờ làm việc, không xếp hạng năng suất từ dữ liệu chưa đầy đủ.
- CI: test popup quầy, máy in, URL không hợp lệ, lịch sử null tương thích, DO dedup, điểm và XLSX. Build PASS không chứng minh WMS automation trên mạng Office: Owner phải thử nghiệm thật.
