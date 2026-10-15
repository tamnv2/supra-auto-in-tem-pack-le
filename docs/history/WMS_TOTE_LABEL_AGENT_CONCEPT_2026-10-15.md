# LƯU TRỮ LỊCH SỬ – KHÔNG PHẢI ĐẶC TẢ HIỆN HÀNH

**Nguồn:** nhánh proposal của repo Inventory ngày 15/10/2026. Tài liệu này chỉ để tham khảo nguồn gốc quyết định. **Quyết định hiện hành nằm ở `AGENTS.md` và `docs/PROJECT_AUTHORITY.md` trong repo mới.** Các hạn chế D169 và lựa chọn không cập nhật tự động cũ đã được thay bằng repo độc lập và yêu cầu cập nhật theo nút của Owner. Không dùng tài liệu này để mở rộng quyền WMS hoặc dữ liệu phiên.

---

# Đề án độc lập — SUPRA Tote Packing & Label Agent

**Ngày tiếp nhận ý tưởng:** 2026-10-15 (Asia/Ho_Chi_Minh)  
**Trạng thái:** PARKED_PROPOSAL / OWNER-REQUESTED DOCUMENTATION ONLY / CHỜ LOG THỬ NGHIỆM  
**Repository lưu tham chiếu:** `tamnv2/supra-inventory` (repo public)  
**Nhánh độc lập:** `proposal/wms-tote-label-automation-20261015`  
**Tích hợp runtime hiện hành:** KHÔNG  
**Change control:** D169 đang mở và chưa Owner PASS. Tài liệu này **không tạo Dxxx mới**, không mở PR để merge, không làm thay đổi base được chấp nhận, không mở rộng quyền thao tác WMS đang được đăng ký. Chỉ tiếp tục nguồn/triển khai khi đúng các bước duyệt scope và Owner-PASS theo quy trình hiện hành.

## 1. Mục tiêu của Owner

Xây dựng **ứng dụng Windows dạng EXE chạy trực tiếp (portable)** tương tự Agent đang có, có trình duyệt WebView2 bên trong để Owner/operator tự đăng nhập trang **đóng gói trên SUPRA WMS**, điều khiển thao tác như người dùng bằng giao diện (không gọi trực tiếp API WMS) và từng bước tự động hóa nghiệp vụ:

1. Người dùng quét/nhập **Tote** (không phải SO), ứng dụng chuyển Tote vào ô nhập PTVC trên trang đóng gói.
2. Enter, chờ WMS chuyển sang giao diện tương ứng.
3. Chọn đúng hành động **Đóng gói hàng chẵn**.
4. Đọc trạng thái/hộp thoại; nhập **số kiện** do người dùng cung cấp và xác nhận.
5. Thao tác **In tem**.
6. **Kết thúc đơn**, chọn **không in phiếu kiểm**, đợi quay lại giao diện quét Tote.
7. Hoàn tất vòng xử lý, báo trạng thái rõ ràng. Không tự khẳng định in thành công chỉ dựa vào event click: phải phân biệt WMS phản hồi, tác vụ in được gửi và kết quả máy in (nếu không thể kiểm chứng, trả trạng thái chưa xác minh).

Đây là **một sản phẩm khác với Inventory Báo hàng/Agent Picklist**; repo hiện dùng như nơi lưu hồ sơ kỹ thuật và tham khảo kiến trúc. Không dùng chung session, thư mục profile WebView2, dữ liệu, tài khoản đăng nhập, kết nối backend hoặc update channel với Agent hiện tại.

## 2. Yêu cầu tức thời — Recorder v2, CHƯA tự động thao tác

Đang chờ log test của Owner trên máy thực tế. Recorder v2 dự kiến là ứng dụng Windows mở WMS qua WebView2, có duy nhất 2 nút nghiệp vụ ghi nhận:

- **BẮT ĐẦU:** nhấn ngay trước khi nhập/quét Tote; đặt mốc đầu phiên.
- **KẾT THÚC:** Owner nhấn sau khi đã hoàn thành việc đóng gói/in tem và trở lại màn hình quét Tote; đặt mốc cuối phiên.
- **Xuất log:** mỗi phiên hoặc nhóm phiên xuất JSONL + Excel/ZIP để gửi phân tích (nút hỗ trợ xuất log không phải bước nghiệp vụ).
- **Không yêu cầu** nhập hoặc ghi riêng mã Tote, mã SO, nhân sự trong UI ở giai đoạn recorder.

### 2.1. Hợp đồng log semantic/diagnostics dự kiến

Mỗi event cần có: `session_id` ngẫu nhiên, số thứ tự đơn điệu, timestamp UTC và VN có offset, elapsed_monotonic_ms, loại event, trạng thái trước/sau nếu biết, kết quả/error category, phiên bản recorder/browser/runtime, cấu hình hiển thị cơ bản (không ghi định danh thiết bị).

Quan sát trên **trình duyệt WMS nhúng** (nếu API/DOM cho phép):

- Click/Enter/input `focus/change` **chỉ lưu loại trường, không lưu dữ liệu đã nhập**.
- Tên/nội dung nút thao tác nằm trong tập thuật ngữ cho phép, `role`, accessible name/`aria-label`, `title`, `placeholder`, `type`, trạng thái `enabled/disabled/visible`, text đã chuẩn hóa và giới hạn độ dài.
- Semantic selector candidate: role + tên, `data-testid` (nếu có), CSS selector ổn định đã được rà soát; vị trí DOM chỉ là dữ liệu chẩn đoán, **không** là tiêu chí chọn duy nhất.
- Nhận diện hộp thoại mới/đóng, nút xác nhận, chuyển bước, navigation (chỉ URL origin + path allowlist; loại query/fragment), trạng thái quay lại ô PTVC, timeout, lỗi WebView2/internet/print.
- Thứ tự, delta thời gian, sự kiện trùng, tải trang bất đồng bộ, phiên hết hạn, popup/new window và tình huống một nút có nhiều bản sao.
- Tuyệt đối **không lấy/tải/lưu** mật khẩu, cookie, auth token, request headers, local/session storage WMS, raw HTML/screenshot có dữ liệu nghiệp vụ, mã Tote, số kiện hay PII vào log mặc định. Không bật CDP Network interception / request dumping. Mask mọi nội dung không được allowlist.

Cần có log export kiểm tra kích thước, giới hạn retention cục bộ và quyền xóa log thủ công; dữ liệu luôn trên máy đến khi operator chủ động xuất/chia sẻ. **Không đưa log thực tế hoặc số liệu vận hành nhạy cảm vào repo public.**

## 3. Lộ trình và cổng xác nhận

### P0 — Hoàn thiện bộ quan sát
- Nhận log v2 thật từ 3–5 vòng thao tác tối thiểu: thao tác chuẩn, WMS chậm/dialog thay đổi, nếu có thì lỗi/hủy; so sánh ít nhất hai kích cỡ màn hình/máy nếu khả thi.
- Dựng timeline từ BẮT ĐẦU đến KẾT THÚC, liệt kê nút/dialog theo tên/role/selector semantic, trạng thái chứng minh từng bước.
- Kiểm tra event capture có đầy đủ nút **Đóng gói hàng chẵn / Xác nhận số kiện / In tem / Kết thúc đơn / Không in phiếu kiểm**.
- Nếu log chưa đủ, sửa recorder và thu thêm; **không đoán button theo index/tọa độ**.

### P1 — Thiết kế và code candidate độc lập, sau khi Owner duyệt
- EXE Windows portable, UI gồm WebView2 WMS, ô quét Tote, ô số kiện, trạng thái từng bước, nút bắt đầu/dừng hoặc hỗ trợ chạy tự động, khu vực xuất log và báo lỗi.
- Mỗi lần quét/nhập Tote xử lý đúng một phiên nghiệp vụ; không nhận lượt mới khi lượt trước chưa đóng.
- Máy trạng thái: `READY -> TOTE_SUBMITTED -> PACKING_ACTION_READY -> PACKAGE_COUNT_READY -> PACKED_CONFIRMED -> LABEL_PRINT_REQUESTED -> FINISH_DIALOG -> RETURNED_TO_TOTE -> COMPLETE`; nhánh `WAIT_LOGIN/NEEDS_HUMAN/UNCERTAIN/FAILED/CANCELLED`.
- WebView2 theo origin/route allowlist, không thao tác ngoài trang nghiệp vụ, WMS login do con người nhập; profile riêng ứng dụng, không đọc profile trình duyệt hoặc Agent khác.
- Chọn phần tử theo tên/role/thuộc tính semantic kết hợp ngữ cảnh dialog/trạng thái; yêu cầu đúng một phần tử khớp, visible/enabled; sai/thiếu/trùng → dừng và yêu cầu người dùng xử lý, không dùng click theo tọa độ.
- Mỗi hành động phải chờ WMS chứng minh bước tiếp theo bằng DOM/state; timeout có thông báo, không click liên tiếp mù hoặc retry click `In tem/Kết thúc` khi kết quả không chắc chắn; chống in trùng, yêu cầu con người xác nhận khi trạng thái không rõ.
- Không dùng direct WMS APIs, không trích xuất/replay phiên đăng nhập, không tự bypass điều khiển công ty.
- Phân biệt: hoàn thành nghiệp vụ WMS, yêu cầu gửi lệnh in, xác nhận in vật lý (chỉ khi có bằng chứng đáng tin cậy). 

### P2 — Portable + tự cập nhật (yêu cầu Owner cho bản chính thức)
- **Không bắt cài đặt chương trình mỗi lần cập nhật.** Đóng gói thành EXE portable x64, chạy bằng nhấp đúp. Mọi phụ thuộc (ví dụ WebView2 Fixed Runtime nếu cần) phải được đóng gói cùng gói portable hoặc kiểm tra có sẵn; "portable" không đồng nghĩa mọi máy đều tương thích tuyệt đối.
- Tách hẳn **product id, dữ liệu local, kênh cập nhật, tag phát hành, manifest, đường dẫn exe/hash** khỏi Agent Picklist hiện có.
- Tham khảo `relay-agent/AgentUpdater.cs`, `relay-agent-webview2-host/Program.cs`, `relay-agent/README.md`: manifest version, source tin cậy, kiểm tra SHA-256, tải staging, thay thế nhị phân an toàn và lỗi mạng không chặn phiên bản đang dùng.
- Đề xuất kênh phát hành riêng `tote-label-test-vN` rồi `tote-label-vN` (đây là tên **đề xuất**, chưa tạo release/resource). Manifest và vị trí tải cập nhật phải được Owner duyệt + đăng ký đúng scope trước khi dùng.
- Chỉ kiểm tra cập nhật lúc khởi động hoặc khi nhàn rỗi; **không** đổi phiên bản giữa một vòng đóng gói/in tem, không hủy phiên WMS giữa chừng. Tải staged, xác minh checksum/chữ ký khi triển khai, atomic replace + rollback. Giữ local logs và profile riêng qua update.
- Nếu mất kết nối dịch vụ cập nhật, ứng dụng tiếp tục chạy bản hiện có (nếu WMS vẫn truy cập được); không thêm polling liên tục hoặc Firebase/Cloudflare/Google Apps Script chỉ để phục vụ check version.
- Thiết kế kênh update nội bộ và phát hành cần tính đến hạn mức GitHub API, cache/cadence để tránh lỗi 403 theo số máy.

## 4. Ranh giới an toàn, phạm vi và chi phí

- Trang WMS **đóng gói** do Owner cung cấp để tham chiếu đang **chưa có đăng ký nghiệp vụ cụ thể trong** `ops/project-scope.json`; key `supra-wms-ui-d080` hiện chỉ cho phép tham chiếu trang xác nhận Picklist trong Beta POC. Việc tự động thao tác trang packing là **ngoài phạm vi đã phê duyệt của Inventory**. Khi sẵn sàng thi hành, cần Owner chốt phạm vi/tách sản phẩm và cập nhật scope + registry phù hợp trước khi kết nối/chạy trên WMS thật.
- Không sửa existing Inventory Agent, Android, Worker, Firebase, Cloudflare, Agent CI/update manifests hoặc Stable; không triển khai server hay provision resource từ đề án này.
- Cân nhắc tách repo riêng khi ý tưởng trở thành sản phẩm chính thức để giữ governance/resource/scope riêng; hiện Owner yêu cầu lưu ý tưởng trong repo Inventory để dễ tham khảo.
- P0 hoàn toàn local: không phát sinh Cloudflare/Firestore/RTDB/Apps Script usage; P1 vẫn local UI-to-WMS, chi phí provider cập nhật nếu có sẽ cần đo. Không cam kết độ trễ/in thành công khi chưa có log và thử nghiệm thực tế.
- Kết quả test đủ để code không đồng nghĩa Owner/field PASS; chỉ phát hành sau CI, kiểm tra thật, phê duyệt nguồn và đường cập nhật. D169 đang mở thì không khởi chạy unrelated Dxxx hoặc merge nhánh này.

## 5. Ma trận chấp nhận trước build tự động

| Tình huống | Hợp đồng mong đợi | Bằng chứng bắt buộc |
| --- | --- | --- |
| Tote hợp lệ, WMS phản hồi | Tiến bước tuần tự, số kiện đúng theo user | Timeline + trạng thái DOM |
| WMS chậm/hộp thoại xuất hiện muộn | Chờ giới hạn; không click sớm/sai nút | Event wait/timeout |
| Nút đổi vị trí/giao diện | Chọn theo semantic, không theo pixel/index | 2 layout hoặc regression DOM |
| Nút cùng tên, nhiều hộp thoại | Ambiguous -> dừng, không click | Case test 2 candidate |
| In tem không chắc đã gửi | UNCERTAIN, không tự in lại | Log + manual recovery |
| Mất mạng/đăng xuất giữa chừng | Chặn bước tiếp, không báo COMPLETE | Network/login test |
| Người dùng kết thúc không đúng trang Tote | Cảnh báo trạng thái chưa xác minh | Recorder state check |
| Cập nhật phiên bản khi đang xử lý | Hoãn update tới khi IDLE | Update scenario test |
| Manifest checksum sai/mạng chặn | Giữ bản cũ, không thực thi binary mới | Update rollback test |
| Máy khác/không có runtime | Hướng dẫn kiểm tra runtime hoặc gói portable | Cold-start trên Windows |
| Dữ liệu đăng nhập/Tote trong log | Không được lưu/được xuất | Privacy redaction test |

## 6. Điểm cần chờ, không tự suy đoán

1. Log Recorder v2 thật Owner gửi lên sau khi bấm BẮT ĐẦU, thao tác đủ các bước và nhấn KẾT THÚC khi về trang Tote.
2. Xác minh nhãn, dialog, trường số kiện, nút `Không in phiếu kiểm`, trạng thái kết thúc và cách WMS/driver trả kết quả in.
3. Chốt cơ chế phân phối portable/update phù hợp mạng cơ quan và danh sách máy trước khi thiết lập kênh release.
4. Phê duyệt đúng phạm vi WMS packing và governance nhánh/nguồn sản phẩm sau cổng D169 Owner PASS.
5. Chỉ khi log đủ + Owner duyệt phương án thi hành, mới chuyển sang build auto-click, regression test và phát hành độc lập.

**Liên kết kỹ thuật tham khảo (không đồng nghĩa thừa kế quyền runtime):**
- `relay-agent/AgentUpdater.cs`
- `relay-agent/SupraConfirmBrowser.cs`
- `relay-agent-webview2-host/Program.cs`
- `relay-agent/AgentLogUploadBridge.cs`
- `docs/specs/OBSERVABILITY_LOGGING.md`
- `ops/authority-manifest.json`, `ops/project-scope.json`, `ops/project-state.json`

**Ghi chú:** Bản Recorder v2 được trao đổi ở hội thoại dưới dạng gói nguồn cho người thử nghiệm; chưa có chứng cứ log v2/field PASS, chưa commit gói nguồn/vật liệu WMS lên nhánh này. 
