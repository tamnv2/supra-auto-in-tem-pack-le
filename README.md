# SUPRA Auto In Tem Pack Lẻ

Ứng dụng Windows độc lập, thao tác **WMS Packing** bằng WebView2. Đây không phải nhánh của SUPRA Inventory / Agent Picklist.

## Trạng thái

- Dự án đang phát triển và cần kiểm thử trên máy Windows sử dụng mạng Office.
- Chỉ thao tác trên WMS sau xác nhận người dùng; tự động hóa theo tên nút và trạng thái giao diện, không click theo tọa độ.
- Không cam kết máy in vật lý đã in thành công: một vòng hoàn tất khi WMS quay về màn hình quét Tote.
- Không lưu mật khẩu, cookie, token, header hoặc dữ liệu phiên WMS vào log. Đăng nhập lại khi mở phần mềm.
- Dữ liệu nghiệp vụ và log lưu local, chỉ xuất khi người dùng yêu cầu.

Các quy tắc, mô tả và hướng dẫn cập nhật sẽ có trong nhánh phát triển, qua PR và GitHub Actions trước khi hợp nhất.
