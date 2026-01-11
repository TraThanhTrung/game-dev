---
name: ""
overview: ""
todos: []
---

---

name: "Plan 1: Auth & Authorization + Admin Dashboard"

overview: Xây dựng hệ thống authentication/authorization cho admin và trang admin dashboard hoàn chỉnh với quản lý users, enemies, game sections, match history, và monitoring.

todos:

- id: db_identity_setup

content: Cài đặt ASP.NET Core Identity, tạo AdminIdentityDbContext, entities (GameSession, SessionPlayer, Enemy, GameSection) và migrations

status: pending

- id: enemy_migration

content: Tạo script migration để import enemies từ config.json sang database

status: pending

dependencies:

- db_identity_setup
- id: auth_system

content: Configure Identity trong Program.cs, xây dựng login/logout Razor Pages sử dụng SignInManager, setup [Authorize]

status: pending

dependencies:

- db_identity_setup
- id: redis_basic

content: Cấu hình Redis cơ bản cho caching (RedisService với caching methods)

status: pending

- id: enemy_config_service

content: Tạo EnemyConfigService để load enemy từ database, cache trong Redis

status: completed

dependencies:

- db_identity_setup
- enemy_migration
- redis_basic
- id: admin_service

content: Tạo AdminService với các methods để query statistics, quản lý enemies và game sections

status: completed

dependencies:

- db_identity_setup
- id: session_tracking

content: Tạo SessionTrackingService để track sessions vào database (chưa tích hợp WorldService)

status: pending

dependencies:

- db_identity_setup
- id: enemy_management

content: Xây dựng Enemy Management pages (Index, Create, Edit, Delete)

status: pending

dependencies:

- auth_system
- admin_service
- enemy_config_service
- id: game_section_management

content: Xây dựng Game Section Management pages (Index, Create, Edit, Details)

status: pending

dependencies:

- auth_system
- admin_service
- enemy_management
- id: dashboard

content: Xây dựng Dashboard page (Index) với thống kê tổng quan

status: pending

dependencies:

- auth_system
- admin_service
- id: users_management

content: Xây dựng Users Management pages (Index, Details) với search và pagination

status: completed

dependencies:

- auth_system
- admin_service
- id: match_history

content: Xây dựng Match History pages (Index, Details) với filter theo ngày và game section

status: pending

dependencies:

- auth_system
- admin_service
- id: active_sessions

content: Xây dựng Active Sessions page để hiển thị sessions đang chơi (từ database, chưa real-time)

status: pending

dependencies:

- auth_system
- admin_service
- id: template_integration

content: Tích hợp template vào _Layout.cshtml và styling

status: pending

dependencies:

- dashboard

---

# Plan

1: Auth & Authorization + Admin Dashboard

## Tổng quan

Xây dựng hệ thống authentication và authorization cho admin, cùng với trang admin dashboard để quản lý toàn bộ hệ thống game server. Plan này tập trung vào admin area hoàn chỉnh.

## Phạm vi

- ASP.NET Core Identity setup cho admin authentication
- Admin login/logout pages
- Admin dashboard với thống kê tổng quan
- Users Management (xem danh sách, chi tiết users)
- Enemy Management (CRUD enemies)
- Game Section Management (CRUD game sections/maps)
- Match History (xem lịch sử trận đấu)
- Active Sessions monitoring
- Session tracking service

## Database Entities (SQL)

**Entities cần tạo:**

- `GameSession` - Lịch sử sessions/matches
- `SessionPlayer` - Player trong từng session
- `Enemy` - Cấu hình quái vật (chuyển từ config.json)
- `GameSection` - Template màn chơi

**Lưu ý:** Room và GameResult sẽ được tạo ở Plan 3.

## Services

- `AdminService` - Business logic cho admin operations
- `SessionTrackingService` - Track sessions vào database
- `EnemyConfigService` - Load enemy config từ database, cache trong Redis

## Admin Pages

- Login/Logout
- Dashboard (Index)
- Users/Index, Users/Details
- Enemies/Index, Enemies/Create, Enemies/Edit, Enemies/Delete
- GameSections/Index, GameSections/Create, GameSections/Edit, GameSections/Details
- Matches/Index, Matches/Details
- Sessions/Index (Active sessions)

## Dependencies

- ASP.NET Core Identity
- Entity Framework Core
- Razor Pages
- StackExchange.Redis (cho caching)

## Files to Create

**Database:**

- `server/Data/AdminIdentityDbContext.cs`
- `server/Models/Entities/GameSession.cs`
- `server/Models/Entities/SessionPlayer.cs`
- `server/Models/Entities/Enemy.cs`
- `server/Models/Entities/GameSection.cs`

**Services:**

- `server/Services/AdminService.cs`
- `server/Services/SessionTrackingService.cs`
- `server/Services/EnemyConfigService.cs`
- `server/Services/RedisService.cs` (basic caching methods)

**Admin Pages:**

- `server/Areas/Admin/Pages/Login.cshtml` + `.cshtml.cs`
- `server/Areas/Admin/Pages/Logout.cshtml.cs`
- `server/Areas/Admin/Pages/Index.cshtml` + `.cshtml.cs`
- `server/Areas/Admin/Pages/Users/Index.cshtml` + `.cshtml.cs`
- `server/Areas/Admin/Pages/Users/Details.cshtml` + `.cshtml.cs`
- `server/Areas/Admin/Pages/Enemies/Index.cshtml` + `.cshtml.cs`
- `server/Areas/Admin/Pages/Enemies/Create.cshtml` + `.cshtml.cs`
- `server/Areas/Admin/Pages/Enemies/Edit.cshtml` + `.cshtml.cs`
- `server/Areas/Admin/Pages/Enemies/Delete.cshtml` + `.cshtml.cs`
- `server/Areas/Admin/Pages/GameSections/Index.cshtml` + `.cshtml.cs`
- `server/Areas/Admin/Pages/GameSections/Create.cshtml` + `.cshtml.cs`
- `server/Areas/Admin/Pages/GameSections/Edit.cshtml` + `.cshtml.cs`
- `server/Areas/Admin/Pages/GameSections/Details.cshtml` + `.cshtml.cs`
- `server/Areas/Admin/Pages/Matches/Index.cshtml` + `.cshtml.cs`
- `server/Areas/Admin/Pages/Matches/Details.cshtml` + `.cshtml.cs`
- `server/Areas/Admin/Pages/Sessions/Index.cshtml` + `.cshtml.cs`
- `server/Areas/Admin/Pages/Shared/_Layout.cshtml`
- `server/Areas/Admin/Pages/_ViewImports.cshtml`
- `server/Areas/Admin/Pages/_ViewStart.cshtml`

**Scripts:**

- `server/Scripts/MigrateEnemiesFromConfig.cs`

## Files to Modify

- `server/Data/GameDbContext.cs` - Thêm DbSets
- `server/Program.cs` - Configure Identity, Razor Pages, admin area
- `server/Services/GameConfigService.cs` - Update để dùng EnemyConfigService
- `server/appsettings.json` - Redis connection string