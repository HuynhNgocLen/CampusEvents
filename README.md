# School Event Management (CampusEvents)

Hệ thống quản lý sự kiện cho trường đại học (đề tài NCKH 2026-2027). Sinh viên đăng nhập bằng tài khoản Google domain trường, đăng ký / yêu thích sự kiện, check-in bằng QR; quản trị viên quản lý sự kiện, danh mục, địa điểm, thống kê và xuất báo cáo Excel.

> Stack: **ASP.NET MVC 5 + .NET Framework 4.7.2 + Entity Framework 6 (Database First) + SQL Server**.

---

## 1. Yêu cầu hệ thống

Trước khi clone về máy mới, cần cài sẵn:

| Thành phần | Phiên bản đề nghị | Ghi chú |
|---|---|---|
| **Visual Studio** | 2019 / 2022 (Community trở lên) | Cài workload **ASP.NET and web development** |
| **.NET Framework Developer Pack** | 4.7.2 | VS có thể đề nghị cài tự động khi mở project |
| **SQL Server** | 2017 / 2019 / 2022 (Express cũng OK) | Để chạy DB |
| **SQL Server Management Studio (SSMS)** | 18+ / 19+ | Để import / restore DB |
| **IIS Express** | Cài kèm Visual Studio | Dùng để chạy debug |
| **NuGet Package Manager** | Có sẵn trong VS | Để restore packages |
| **Git** | mới nhất | Dùng để clone repo |

> Nếu dùng máy chưa có SQL Server, có thể cài **SQL Server Express + SSMS** (miễn phí) là đủ.

---

## 2. Clone và mở project

```bash
git clone <repo-url> school_event_management
cd school_event_management
```

Mở file `school_event_management.sln` bằng **Visual Studio**.

Khi VS mở lần đầu sẽ tự động:
- Restore NuGet packages (xem `packages.config`)
- Build project trên target `.NET Framework 4.7.2`

Nếu restore không tự chạy, mở **Tools → NuGet Package Manager → Package Manager Console** rồi gõ:

```powershell
Update-Package -reinstall
```

---

## 3. Tạo database

Script SQL được tách thành **3 file** trong thư mục `SQL/`, chạy lần lượt:

| Thứ tự | File | Nội dung |
|---|---|---|
| 1 | [`SQL/01_schema_and_data.sql`](SQL/01_schema_and_data.sql) | Drop & tạo lại DB, tạo toàn bộ **bảng + index** (Vien, MaNghanh, SinhVien, DanhMuc, DiaDiem, QuanTriVien, EVENT, DangKySuKien, SuKienYeuThich, QTVAdminDangNhapLog, QTVHanhDongLog) + insert **dữ liệu mẫu** (10 viện/khoa, 49 ngành, 6 danh mục, 6 địa điểm, 4 sự kiện) |
| 2 | [`SQL/02_views.sql`](SQL/02_views.sql) | 3 **views**: `vw_SoChoConLai`, `vw_UserStatsByYear`, `vw_DiemRenLuyenTheoKy` |
| 3 | [`SQL/03_triggers.sql`](SQL/03_triggers.sql) | 2 **triggers** + 1 **stored procedure** `sp_AutoUpdate_TrangThaiEvent` |

### Cách 1 - Chạy script (khuyến nghị)

1. Mở **SSMS**, kết nối tới SQL Server instance của bạn.
2. Mở và **Execute (F5)** lần lượt 3 file theo đúng thứ tự ở trên.
3. Sau mỗi file sẽ hiện thông báo `✓ Đã tạo ...` ở tab Messages.

### Cách 2 - Restore từ file backup

Trong thư mục `SQL/` có sẵn file `school_event_management.bak` (snapshot DB đầy đủ, kể cả dữ liệu thực tế đang chạy). Vào SSMS → chuột phải **Databases → Restore Database → Device → chọn file `.bak`**.

> Lưu ý: nếu restore từ `.bak`, **không cần chạy** 3 file `.sql` ở Cách 1.

---

## 4. Cấu hình project

Có 2 file cấu hình **không commit lên Git** (đã liệt kê trong `.gitignore`). Bạn cần tạo lại từ file mẫu:

### 4.1. `Web.config`

Copy từ file mẫu:

```powershell
Copy-Item Web.config.example Web.config
```

Sau đó mở `Web.config` và sửa:

#### a) Connection string tới SQL Server của bạn

Tìm phần `<connectionStrings>` và đổi `data source` thành tên SQL instance của máy bạn:

```xml
<add name="school_event_managementEntities"
     connectionString="metadata=res://*/Models.Model1.csdl|res://*/Models.Model1.ssdl|res://*/Models.Model1.msl;
       provider=System.Data.SqlClient;
       provider connection string=&quot;
         data source=YOUR_SQL_INSTANCE;
         initial catalog=school_event_management;
         integrated security=True;
         trustservercertificate=True;
         MultipleActiveResultSets=True;
         App=EntityFramework&quot;"
     providerName="System.Data.EntityClient" />
```

Ví dụ thực tế:
- Local SQL Express: `data source=.\SQLEXPRESS`
- Local default instance: `data source=localhost` hoặc `data source=.`
- SQL named instance: `data source=DESKTOP-XXX\SQLEXPRESS`

#### b) Cấu hình SMTP (gửi OTP đăng ký, quên mật khẩu)

```xml
<add key="SmtpEmail"       value="your-email@gmail.com" />
<add key="SmtpPassword"    value="your-gmail-app-password" />
<add key="SmtpDisplayName" value="CampusEvents" />
```

> Với Gmail phải dùng **App Password** (16 ký tự), không dùng mật khẩu Gmail thường. Tạo tại: https://myaccount.google.com/apppasswords (yêu cầu bật 2FA).

#### c) Khóa ký QR điểm danh

```xml
<add key="AttendanceQrSigningKey" value="ChangeThisToARandomString" />
```

> Đổi giá trị này sẽ làm mất hiệu lực toàn bộ mã QR đã phát ra trước đó.

### 4.2. `AppSettings.secret.config`

File này lưu các khóa nhạy cảm tách riêng khỏi `Web.config`. Copy từ mẫu:

```powershell
Copy-Item AppSettings.secret.config.example AppSettings.secret.config
```

Mở file và thêm 2 khóa Google OAuth (lấy từ Google Cloud Console → OAuth 2.0 Client IDs):

```xml
<appSettings>
  <add key="GoogleClientId"     value="YOUR_GOOGLE_CLIENT_ID.apps.googleusercontent.com" />
  <add key="GoogleClientSecret" value="YOUR_GOOGLE_CLIENT_SECRET" />
</appSettings>
```

Trong **Google Cloud Console**, khi tạo OAuth Client phải khai:
- **Authorized JavaScript origins**: `https://localhost:44300` (đổi theo port IIS Express bạn dùng)
- **Authorized redirect URIs**: `https://localhost:44300/Account/GoogleCallback`

> Hệ thống chỉ chấp nhận đăng nhập bằng email domain `student.tdmu.edu.vn` (hard-code trong `Controllers/AccountController.cs`, có thể đổi nếu cần).

---

## 5. Chạy project

1. Trong Visual Studio, đặt `school_event_management` làm **Startup Project** (mặc định đã là).
2. Bấm **F5** (debug) hoặc **Ctrl+F5** (run không debug).
3. Trình duyệt sẽ mở `https://localhost:<port>` với trang chủ public.

### URL chính

| URL | Mục đích |
|---|---|
| `/` | Trang chủ (danh sách sự kiện) |
| `/Account/Login` | Đăng nhập sinh viên (Google OAuth) |
| `/Events` | Danh sách / chi tiết sự kiện |
| `/Registration` | Sự kiện đã đăng ký của user |
| `/Schedule` | Lịch cá nhân |
| `/Admin/AdminAccount/Login` | Đăng nhập admin (Forms Authentication) |
| `/Admin/AdminDashboard` | Trang quản trị |

### Tạo tài khoản admin đầu tiên

Bảng `QuanTriVien` không có dữ liệu mẫu. Sau khi tạo DB, vào SSMS chạy 1 trong 2 cách:

**Cách A** - Insert thẳng (mật khẩu sẽ tự nâng cấp lên Argon2id ở lần login đầu):

```sql
USE school_event_management;
INSERT INTO QuanTriVien (TenDN, MatKhau, Quyen, MaQTV, TrangThaiKhoa)
VALUES ('admin',
        CONVERT(VARCHAR(64), HASHBYTES('SHA2_256', 'admin@123'), 2), -- SHA-256 hex
        2,        -- 2 = admin (0=xem, 1=quản lý, 2=admin)
        'CNS',    -- Mã viện (phải tồn tại trong bảng Vien)
        0);
```

Sau đó đăng nhập `/Admin/AdminAccount/Login` với `admin / admin@123`.

**Cách B** - Đăng nhập 1 lần để tạo hash Argon2 chuẩn ngay từ đầu: nhờ developer khác chạy hàm `PasswordHasher.HashPassword(...)` trong file `Helpers/PasswordHasher.cs` rồi paste hash kết quả vào cột `MatKhau`.

---

## 6. Cấu trúc thư mục chính

```
school_event_management/
├── App_Start/              # Bundle, Filter, Route config
├── Areas/Admin/            # Khu vực quản trị (Controllers + Views + ViewModels riêng)
├── Controllers/            # Controllers cho user (Account, Events, Registration, ...)
├── Models/                 # Entity Framework Database First (Model1.edmx) + POCO
├── ViewModels/             # ViewModels dùng cho View
├── Views/                  # Razor Views (.cshtml)
├── Services/               # SmtpEmailSender, EmailService
├── Helpers/                # JwtService, PasswordHasher (Argon2id), QRCodeHelper, ...
├── Repositories/           # Tầng truy cập dữ liệu
├── Filters/                # Action filters (logging, ...)
├── Infrastructure/         # Code hạ tầng dùng chung
├── Content/                # CSS, ảnh
├── Scripts/                # JS (jQuery, Bootstrap, app/*)
├── Uploads/                # Ảnh sự kiện do admin upload (runtime)
├── Images/                 # Ảnh tĩnh
├── SQL/
│   ├── 01_schema_and_data.sql        # Tables + dataset mẫu
│   ├── 02_views.sql                  # 3 views thống kê
│   ├── 03_triggers.sql               # 2 triggers + stored procedure
│   ├── school_event_management.bak   # Backup full DB
│   └── SchoolEventManagement_Database.docx
├── Web.config.example                # Mẫu Web.config (KHÔNG sửa file gốc)
├── AppSettings.secret.config.example  # Mẫu AppSettings.secret.config
├── packages.config                    # Khai báo NuGet packages
└── school_event_management.sln
```

Các file **bị `.gitignore`** (không có sau khi clone, phải tự tạo):
- `Web.config`
- `AppSettings.secret.config`
- `bin/`, `obj/`, `.vs/`

---

## 7. Công nghệ sử dụng

- **Backend**: ASP.NET MVC 5.2.9, C#, .NET Framework 4.7.2
- **ORM**: Entity Framework 6.4.4 (Database First qua `Model1.edmx`)
- **Database**: SQL Server (script v2 - 27/04/2026)
- **Authentication**:
  - User: **Google OAuth2** + **JWT** (`System.IdentityModel.Tokens.Jwt`) lưu trong cookie HttpOnly
  - Admin: **Forms Authentication**
  - Mật khẩu: **Argon2id** (`Isopoh.Cryptography.Argon2`), tự động nâng cấp hash SHA-256 cũ
- **Frontend**: Razor Views + Bootstrap 5.2.3 + jQuery 3.7.0 + jQuery Validation
- **Tiện ích**:
  - **ClosedXML + DocumentFormat.OpenXml** - xuất Excel (báo cáo, danh sách đăng ký)
  - **QRCoder** - tạo QR code điểm danh
  - **Newtonsoft.Json**
  - **SMTP** (Gmail) - gửi OTP, reset mật khẩu

Chi tiết thêm: xem `CONG_NGHE_SU_DUNG.txt`.

---

## 8. Troubleshooting

| Lỗi | Cách khắc phục |
|---|---|
| `Cannot open database "school_event_management"` | Chưa chạy 3 file SQL trong `SQL/` (01 → 02 → 03) hoặc connection string sai instance |
| `Login failed for user ...` | Connection string đang dùng SQL Auth nhưng nên dùng `Integrated Security=True` (Windows Auth) |
| Build lỗi thiếu reference | Restore NuGet (`Tools → NuGet → Restore`) hoặc `Update-Package -reinstall` |
| Đăng nhập Google báo `redirect_uri_mismatch` | Vào Google Cloud Console thêm đúng redirect URI khớp port hiện tại |
| Gửi OTP báo SMTP fail | Đảm bảo `SmtpPassword` là **App Password** Gmail (không phải mật khẩu chính), bật 2FA tài khoản Google trước |
| `The Web.config file does not contain ...` | Chưa copy `Web.config.example` thành `Web.config` |
| Trang admin báo redirect liên tục | Chưa có user trong bảng `QuanTriVien` - xem mục 5 (Tạo admin đầu tiên) |
| Trạng thái sự kiện không tự cập nhật | Tạo SQL Server Agent Job gọi `EXEC dbo.sp_AutoUpdate_TrangThaiEvent` mỗi 1-5 phút |

---

## 9. Checklist khi clone project về máy mới

- [ ] Cài Visual Studio 2019/2022 + workload ASP.NET
- [ ] Cài SQL Server + SSMS
- [ ] `git clone <repo-url>` và mở `school_event_management.sln`
- [ ] Restore NuGet packages
- [ ] Mở SSMS, chạy lần lượt `SQL/01_schema_and_data.sql` → `02_views.sql` → `03_triggers.sql` (hoặc restore `.bak`)
- [ ] Copy `Web.config.example` → `Web.config`, sửa connection string + SMTP + AttendanceQrSigningKey
- [ ] Copy `AppSettings.secret.config.example` → `AppSettings.secret.config`, điền Google OAuth keys
- [ ] Insert tài khoản admin đầu tiên vào bảng `QuanTriVien`
- [ ] Bấm F5 - chạy được `https://localhost:<port>`

---

## 10. Liên hệ

Đề tài NCKH 2026-2027 - Trường Đại học Thủ Dầu Một (TDMU).
