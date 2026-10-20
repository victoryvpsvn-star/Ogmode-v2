# OGMode
Công cụ tối ưu hoá Windows cho gaming. Chế độ game mode một chạm cho máy tính và laptop.


[🇻🇳 Xem bản tiếng Việt](./README_VIE.md) | [🇬🇧 View English version](./README.md)


Chỉ với **một cú click**, OGMode sẽ giúp máy tính cấu hình yếu chơi game mượt hơn với các tinh chỉnh có sẵn. 🚀

**Trang chính:** [OGMode trên Osteup](https://osteup.com/ogmode/)

Được phát triển bởi **Osteup** với **C#** và **Batch** để tối ưu hệ thống.

![Ảnh OGMode](https://github.com/SiroCandy06/OGMode/assets/101639160/e50f465b-47b9-4b1a-9c4d-c6fdafaff1c5)

## Tính năng
- **Game Mode một chạm**: tắt hiệu ứng, chuyển power plan Hiệu năng cao, bật Windows Game Mode, tắt Game DVR, dừng dịch vụ nền không cần thiết, giải phóng RAM
- **Hoàn tác chính xác**: OGMode sao lưu giá trị gốc (lưu ở `%AppData%\OGMode\state.json`) và chỉ khôi phục đúng những gì nó đã đổi, kể cả sau khi tắt/mở lại app
- **Dọn rác**: quét trước, hiện dung lượng từng mục, rồi mới dọn (tệp tạm, cache Windows Update, cache trình duyệt, file dump/báo cáo lỗi, cache ảnh thu nhỏ, shader cache, thùng rác)
- **Dọn RAM sâu**: thu gọn working set + xoá standby list
- **Công cụ nhanh**: làm mới DNS, khởi động lại Explorer, khôi phục mặc định

## Nhược điểm
- Giao diện có thể xấu đi (giảm hiệu ứng để tăng hiệu suất)
- **Phiên bản beta** đang thử nghiệm

## Yêu cầu
Phù hợp với **máy cấu hình yếu**. Windows 10/11 64-bit.
- **[.NET Desktop Runtime 10](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)** (.NET 8 hết hỗ trợ ngày 10/11/2026)
- **[Tải & Cài đặt OGMode](https://github.com/SiroCandy06/OGMode/releases)**

## Cách sử dụng
1. Tải và cài đặt OGMode
2. Mở chương trình (tự xin quyền **Administrator**)
3. Dùng **Game Mode**, **Công Cụ**, **Dọn Rác** để tối ưu hệ thống

## Tự build
```
dotnet build ogmm.csproj -c Release
```
Cần .NET 10 SDK. Nếu mở bằng Visual Studio, hãy chạy VS bằng quyền Admin (do app yêu cầu Administrator).

### Chạy toàn bộ test
```
dotnet test OGMode.slnx -c Release
```
Trên máy bạn chỉ chạy các test an toàn; test thay đổi hệ thống thật (bật/tắt Game Mode, registry, power plan, dịch vụ, dọn rác thư mục thật) sẽ bị bỏ qua, chỉ chạy trên máy ảo CI dùng một lần (`OGMODE_INTEGRATION=1`).

### CI (GitHub Actions)
Vào **Actions -> Build & Test -> Run workflow**. Workflow tự restore, build, chạy toàn bộ test, publish `ogmm.exe` 1 file, chạy thử exe và upload kết quả. Tick **release** (hoặc push tag `v*`) để tạo luôn GitHub Release.

![Giao diện OGMode](https://github.com/SiroCandy06/OGMode/assets/101639160/445d2bcc-2220-4553-bc5f-73528130fd8f)

## Lịch sử phiên bản
| Phiên bản | Ngày        | Cập nhật |
|-----------|------------|-----------------------------------------------------------------------------------------------------------------------------------|
| **v2.0** | 10/2026 | Nâng lên .NET 10, trang Dọn Rác, dọn RAM sâu, power plan/Game Mode/Game DVR, sao lưu & hoàn tác chính xác, danh sách dịch vụ an toàn hơn (bỏ Firewall/DNS/EventLog/Themes), sửa nhiều lỗi (crash khi bấm Giới Thiệu, luồng giao diện, rò rỉ Task). |
| **v1.4** | 23/06/2024 | Sửa lỗi tải chậm, thêm kiểm tra cập nhật, cải thiện Game Mode, dọn RAM, tinh chỉnh hệ thống, nút khởi động lại explorer. |
| **v1.3** | 06/03/2024 | Fix lỗi Roblox, cải thiện trình duyệt & YouTube, sửa lỗi explorer, công cụ dọn rác, thêm DNS Google giảm ping. |
| **v1.2** | 19/01/2024 | Sửa lỗi mất ảnh trong Explorer, thêm dọn RAM, tuỳ chọn tắt Bluetooth, cải tiến UI, fix lag YouTube. |
| **v1.1** | 12/01/2024 | Hiệu ứng fade-in/out, tối ưu hiệu suất, sửa lỗi navigation, tối ưu dịch vụ Windows. |
| **v1.0** | 03/01/2024 | Phiên bản C# đầu tiên, giới thiệu Game Mode, tăng FPS, nâng cấp từ Batch lên C#. |

Phiên bản batch script trước v1 (v0.1 -> v0.5) có thể xem [tại đây](https://osteup.com/tool-ogmode-fix-lag-windows-toi-uu-hoa-may-tinh/).

## Hỗ trợ
Tham gia **Discord** hoặc liên hệ qua mục "Liên hệ".

![Hỗ trợ](https://github.com/SiroCandy06/OGMode/assets/101639160/504aeffc-4e15-4d63-8c31-ceeec8c700fe)
