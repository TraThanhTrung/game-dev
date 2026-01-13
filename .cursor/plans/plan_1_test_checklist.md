# Plan 1: Test Checklist

## Thông tin đăng nhập Admin

**URL:** http://localhost:5220/Admin/Login

**Tài khoản:**

- **Username:** `admin`
- **Password:** `Admin123!`

> **Lưu ý:** Tài khoản này được tạo tự động khi server khởi động lần đầu. Nếu chưa có, server sẽ tự động tạo.

taskkill /F /IM GameServer.exe
   cd G:\game-dev\server
   dotnet run

## Checklist Test

### ✅ 1. Authentication & Authorization

#### 1.1 Login ( done )

- [ ] Truy cập `/Admin/Login` - hiển thị form login
- [ ] Đăng nhập với username/password sai → hiển thị lỗi
- [ ] Đăng nhập với `admin / Admin123!` → redirect đến Dashboard
- [ ] Kiểm tra UI/UX theo template (form đẹp, styling đúng)

#### 1.2 Authorization ( done )

- [ ] Truy cập `/Admin` khi chưa login → redirect đến `/Admin/Login`
- [ ] Sau khi login, có thể truy cập các trang admin
- [ ] Kiểm tra sidebar hiển thị đúng menu items

#### 1.3 Logout ( done )

- [ ] Click logout → đăng xuất thành công
- [ ] Sau logout → redirect về `/Admin/Login`
- [ ] Sau logout không thể truy cập các trang admin

---

### ✅ 2. Dashboard (Index)

**URL:** `/Admin` hoặc `/Admin/Index`

- [ ] Hiển thị 4 stats cards: ( done )
  - [ ] Total Users (với icon và số liệu)
  - [ ] Total Sessions (với icon và số liệu)
  - [ ] Active Sessions (với icon và số liệu)
  - [ ] Matches Today (với icon và số liệu)
- [ ] Kiểm tra UI/UX: cards đẹp, icons đúng, colors phù hợp
- [ ] Kiểm tra breadcrumb navigation ( làm lại  )
- [ ] Kiểm tra sidebar navigation active state

---

### ✅ 3. Users Management

#### 3.1 Users Index (`/Admin/Users`)

- [ ] Hiển thị danh sách users trong table
- [ ] Table có các cột: ID, Name, Level, Exp, Gold, Created At, Actions
- [ ] Có nút "View" cho mỗi user
- [ ] Có search box (tìm theo name)
- [ ] Có pagination (nếu có nhiều users)
- [ ] Kiểm tra UI: table styling đẹp, badges nếu có

#### 3.2 User Details (`/Admin/Users/Details?id={userId}`)

- [ ] Hiển thị thông tin profile: ID, Name, Level, Exp, Gold, Created At, Play Time
- [ ] Hiển thị Stats: Damage, Range, Knockback Force, Speed, Max Health, Current Health
- [ ] Hiển thị Skills list (nếu có)
- [ ] Có nút "Back to List"
- [ ] Kiểm tra breadcrumb navigation

---

### ✅ 4. Enemies Management

#### 4.1 Enemies Index (`/Admin/Enemies`) ( done )

- [ ] Hiển thị danh sách enemies trong table
- [ ] Table có các cột: ID, Type ID, Name, Exp Reward, Gold Reward, Max Health, Damage, Speed, Status, Actions
- [ ] Status hiển thị badge (Active/Inactive)
- [ ] Có nút "Create New"
- [ ] Có nút Edit và Delete cho mỗi enemy

#### 4.2 Create Enemy (`/Admin/Enemies/Create`) ( done)

- [ ] Form có đầy đủ fields:
  - Type ID (required)
  - Name
  - Exp Reward, Gold Reward
  - Max Health, Damage
  - Speed, Detect Range
  - Attack Range, Attack Cooldown
  - Weapon Range, Knockback Force
  - Stun Time, Respawn Delay
  - IsActive checkbox
- [ ] Validation hoạt động (required fields)
- [ ] Submit thành công → redirect về Index
- [ ] Kiểm tra UI: form đẹp, styling đúng template

#### 4.3 Edit Enemy (`/Admin/Enemies/Edit?id={id}`) ( xem lại cache )

- [ ] Form pre-filled với dữ liệu hiện tại
- [ ] Có thể chỉnh sửa tất cả fields
- [ ] Submit thành công → redirect về Index
- [ ] Kiểm tra cache invalidation (enemy config trong Redis)

#### 4.4 Delete Enemy (`/Admin/Enemies/Delete?id={id}`)

- [ ] Hiển thị confirmation page với thông tin enemy
- [ ] Có warning message
- [ ] Delete thành công → redirect về Index
- [ ] Enemy đã bị xóa khỏi database

---

### ✅ 5. Game Sections Management

#### 5.1 Game Sections Index (`/Admin/GameSections`)

- [ ] Hiển thị danh sách game sections trong table
- [ ] Table có các cột: ID, Name, Enemy Type, Enemy Count, Enemy Level, Spawn Rate, Duration, Status, Actions
- [ ] Status hiển thị badge (Active/Inactive)
- [ ] Có nút "Create New"
- [ ] Có nút View Details và Edit cho mỗi section

#### 5.2 Create Game Section (`/Admin/GameSections/Create`)

- [ ] Form có đầy đủ fields:
  - Name (required)
  - Description
  - Enemy Type ID
  - Enemy Count, Enemy Level
  - Spawn Rate
  - Duration (optional, null = unlimited)
  - IsActive checkbox
- [ ] Validation hoạt động
- [ ] Submit thành công → redirect về Index

#### 5.3 Edit Game Section (`/Admin/GameSections/Edit?id={id}`)

- [ ] Form pre-filled với dữ liệu hiện tại
- [ ] Có thể chỉnh sửa tất cả fields
- [ ] Submit thành công → redirect về Index

#### 5.4 Game Section Details (`/Admin/GameSections/Details?id={id}`)

- [ ] Hiển thị đầy đủ thông tin section
- [ ] Có nút Edit và Back to List
- [ ] Kiểm tra breadcrumb navigation

---

### ✅ 6. Match History

#### 6.1 Matches Index (`/Admin/Matches`)

- [ ] Hiển thị danh sách matches trong table
- [ ] Table có các cột: Session ID, Start Time, End Time, Player Count, Status, Duration, Actions
- [ ] Status hiển thị badge (Active/Completed/Abandoned)
- [ ] Có filter: From Date, To Date
- [ ] Filter hoạt động đúng
- [ ] Có pagination
- [ ] Có nút "View" cho mỗi match

#### 6.2 Match Details (`/Admin/Matches/Details?id={sessionId}`)

- [ ] Hiển thị thông tin session: Session ID, Start Time, End Time, Duration, Player Count, Status
- [ ] Hiển thị danh sách players trong session: Player ID, Join Time, Leave Time, Play Duration
- [ ] Có nút "Back to List"
- [ ] Kiểm tra breadcrumb navigation

---

### ✅ 7. Active Sessions

**URL:** `/Admin/Sessions`

- [ ] Hiển thị danh sách active sessions
- [ ] Table có các cột: Session ID, Start Time, Player Count, Duration, Status, Actions
- [ ] Duration được tính real-time (thời gian từ Start Time đến hiện tại)
- [ ] Status hiển thị badge "Active"
- [ ] Có nút "View" để xem chi tiết
- [ ] Page tự động refresh mỗi 5 giây (kiểm tra JavaScript)
- [ ] Nếu không có active sessions → hiển thị message "No active sessions"

---

### ✅ 8. Template Integration & UI/UX

#### 8.1 Layout

- [ ] Sidebar hiển thị đúng với logo
- [ ] Sidebar có profile section với username
- [ ] Sidebar menu items có icons đúng
- [ ] Sidebar có active state highlighting
- [ ] Navbar hiển thị đúng
- [ ] Navbar có profile dropdown
- [ ] Footer hiển thị đúng

#### 8.2 Styling

- [ ] Tất cả pages sử dụng template CSS
- [ ] Cards có styling đúng
- [ ] Tables có styling đúng (striped, responsive)
- [ ] Buttons có styling đúng (colors, icons)
- [ ] Forms có styling đúng
- [ ] Badges có màu sắc đúng (success, danger, warning, info)
- [ ] Icons Material Design hiển thị đúng

#### 8.3 Responsive

- [ ] Sidebar collapse khi màn hình nhỏ
- [ ] Tables responsive trên mobile
- [ ] Forms responsive trên mobile

---

### ✅ 9. Database & Services

#### 9.1 Database

- [ ] Kiểm tra file `gameserver.db` đã được tạo
- [ ] Kiểm tra file `identity.db` đã được tạo
- [ ] Kiểm tra tables đã được tạo: GameSessions, SessionPlayers, Enemies, GameSections
- [ ] Kiểm tra Identity tables: AspNetUsers, AspNetRoles, etc.

#### 9.2 Enemy Migration

- [ ] Enemy "slime" đã được import từ config.json
- [ ] Có thể query enemy từ database
- [ ] Enemy config được cache trong Redis (nếu Redis đang chạy)

#### 9.3 Services

- [ ] AdminService hoạt động đúng (query stats, CRUD operations)
- [ ] SessionTrackingService hoạt động đúng
- [ ] EnemyConfigService load từ database và cache Redis

---

### ✅ 10. Security

- [ ] Không thể truy cập admin pages khi chưa login
- [ ] Session timeout hoạt động (8 hours)
- [ ] Password được hash đúng cách
- [ ] CSRF protection hoạt động (forms có anti-forgery token)

---

## Test Data Suggestions

### Tạo test data để test đầy đủ:

1. **Users:**

   - Tạo vài player profiles qua Unity client hoặc API

2. **Enemies:**

   - Tạo thêm enemies khác ngoài "slime" để test CRUD

3. **Game Sections:**

   - Tạo vài game sections với các thông số khác nhau

4. **Sessions:**
   - Tạo vài game sessions (có thể qua API hoặc trực tiếp trong database)

---

## Lưu ý khi test

1. **Redis:** Nếu Redis không chạy, EnemyConfigService sẽ fallback về config.json (không ảnh hưởng chức năng chính)

2. **Database:** Tất cả migrations sẽ tự động chạy khi server khởi động

3. **Admin User:** Được tạo tự động lần đầu, nếu đã có thì sẽ skip

4. **Template Assets:** Đảm bảo các file CSS/JS từ template được load đúng từ `~/admin/dist/assets/`

---

## Kết quả mong đợi

Sau khi test xong, bạn sẽ có:

- ✅ Admin dashboard hoàn chỉnh với UI/UX đẹp
- ✅ Quản lý users, enemies, game sections
- ✅ Xem match history và active sessions
- ✅ Authentication/Authorization hoạt động đúng
- ✅ Tất cả CRUD operations hoạt động
- ✅ Template được tích hợp đầy đủ














