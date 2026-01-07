---
name: "Plan 2: Player Web Panel"
overview: Xây dựng trang web panel cho players để xem profile, stats, lịch sử chơi, và quản lý tài khoản của mình. Sử dụng authentication riêng hoặc tích hợp với hệ thống hiện có.
todos:
  - id: player_auth_decision
    content: Quyết định authentication method cho players (Identity riêng / Token-based / Session-based)
    status: pending
  - id: player_auth_setup
    content: Setup authentication system cho players theo method đã chọn
    status: pending
    dependencies:
      - player_auth_decision
  - id: player_web_service
    content: Tạo PlayerWebService với các methods để query player data (profile, stats, history)
    status: pending
  - id: player_layout
    content: Tạo layout và styling cho player web panel
    status: pending
  - id: player_dashboard
    content: Xây dựng Dashboard/Home page cho players
    status: pending
    dependencies:
      - player_auth_setup
      - player_web_service
      - player_layout
  - id: player_profile
    content: Xây dựng Profile page để xem thông tin cá nhân
    status: pending
    dependencies:
      - player_auth_setup
      - player_web_service
      - player_layout
  - id: player_stats
    content: Xây dựng Stats page để xem stats chi tiết
    status: pending
    dependencies:
      - player_auth_setup
      - player_web_service
      - player_layout
  - id: player_history
    content: Xây dựng History page để xem lịch sử chơi (sử dụng GameSession, SessionPlayer từ Plan 1)
    status: pending
    dependencies:
      - player_auth_setup
      - player_web_service
      - player_layout
  - id: player_results
    content: Xây dựng Game Results page (sử dụng GameResult từ Plan 3)
    status: pending
    dependencies:
      - player_auth_setup
      - player_web_service
      - player_layout
  - id: player_api
    content: Tạo PlayerApiController cho AJAX calls (optional)
    status: pending
    dependencies:
      - player_web_service
---

# Plan 2: Player Web Panel

## Tổng quan

Xây dựng trang web panel dành cho players để họ có thể xem thông tin tài khoản, stats, lịch sử chơi, và các thông tin liên quan đến game. Đây là giao diện web riêng biệt với admin panel.

## Phạm vi

- Player authentication (có thể dùng Identity riêng hoặc token-based)
- Player profile page (xem thông tin cá nhân)
- Player stats page (xem stats, level, exp, gold)
- Player match history (lịch sử các trận đã chơi)
- Player achievements/leaderboard (nếu có)
- Player settings (đổi tên, cài đặt)

## Authentication Options

**Option 1: Sử dụng ASP.NET Core Identity riêng cho Players**

- Tạo `PlayerIdentityDbContext` riêng
- Players đăng ký/đăng nhập qua web
- Tách biệt hoàn toàn với admin

**Option 2: Token-based Authentication (Recommended)**

- Players đăng nhập qua game client (Unity) trước
- Server trả về token
- Web panel sử dụng token để authenticate
- Đơn giản hơn, không cần duplicate authentication

**Option 3: Sử dụng PlayerProfile hiện có**

- Players đăng nhập bằng PlayerName (như trong game)
- Session-based authentication
- Đơn giản nhất, phù hợp student project

## Database Entities

**Sử dụng entities đã có:**

- `PlayerProfile` - Thông tin người chơi
- `PlayerStats` - Stats của player
- `GameSession`, `SessionPlayer` - Lịch sử chơi
- `GameResult` - Kết quả các trận đấu (từ Plan 3)

**Có thể thêm:**

- `PlayerSettings` - Cài đặt của player (nếu cần)

## Services

- `PlayerWebService` - Business logic cho player web panel
- `GetPlayerProfileAsync(playerId)` - Lấy profile
- `GetPlayerStatsAsync(playerId)` - Lấy stats
- `GetPlayerMatchHistoryAsync(playerId, page, pageSize)` - Lịch sử chơi
- `GetPlayerGameResultsAsync(playerId, page, pageSize)` - Kết quả các trận
- `GetPlayerPlayTimeAsync(playerId)` - Tổng thời gian chơi
- `UpdatePlayerSettingsAsync(playerId, settings)` - Cập nhật settings

## Player Web Pages

**`server/Areas/Player/Pages/`**

- `Login.cshtml` + `.cshtml.cs` - Đăng nhập (nếu dùng Identity riêng)
- `Register.cshtml` + `.cshtml.cs` - Đăng ký (nếu dùng Identity riêng)
- `Index.cshtml` + `.cshtml.cs` - Dashboard/Home
- `Profile.cshtml` + `.cshtml.cs` - Xem profile
- `Stats.cshtml` + `.cshtml.cs` - Xem stats chi tiết
- `History.cshtml` + `.cshtml.cs` - Lịch sử chơi
- `Settings.cshtml` + `.cshtml.cs` - Cài đặt (nếu cần)

## API Endpoints (Optional - cho AJAX)

**`server/Areas/Player/Controllers/PlayerApiController.cs`**

- `GET /player/api/profile` - JSON profile
- `GET /player/api/stats` - JSON stats
- `GET /player/api/history` - JSON match history
- `GET /player/api/results` - JSON game results
- `POST /player/api/settings` - Update settings

## Dependencies

- ASP.NET Core Identity (nếu dùng Option 1)
- Entity Framework Core
- Razor Pages
- Redis (cho caching player data)

## Files to Create

**Services:**

- `server/Services/PlayerWebService.cs` - Service cho player web panel

**Player Pages:**

- `server/Areas/Player/Pages/Index.cshtml` + `.cshtml.cs` - Dashboard
- `server/Areas/Player/Pages/Profile.cshtml` + `.cshtml.cs` - Profile
- `server/Areas/Player/Pages/Stats.cshtml` + `.cshtml.cs` - Stats
- `server/Areas/Player/Pages/History.cshtml` + `.cshtml.cs` - Match History
- `server/Areas/Player/Pages/Settings.cshtml` + `.cshtml.cs` - Settings (optional)
- `server/Areas/Player/Pages/Login.cshtml` + `.cshtml.cs` - Login (nếu cần)
- `server/Areas/Player/Pages/Register.cshtml` + `.cshtml.cs` - Register (nếu cần)
- `server/Areas/Player/Pages/Shared/_Layout.cshtml` - Layout
- `server/Areas/Player/Pages/_ViewImports.cshtml` - View imports
- `server/Areas/Player/Pages/_ViewStart.cshtml` - View start

**Controllers (Optional):**

- `server/Areas/Player/Controllers/PlayerApiController.cs` - API cho AJAX

**Models:**

- `server/Areas/Player/Models/PlayerProfileViewModel.cs`
- `server/Areas/Player/Models/PlayerStatsViewModel.cs`
- `server/Areas/Player/Models/MatchHistoryViewModel.cs`

## Files to Modify

- `server/Program.cs` - Configure player area routing (nếu dùng Identity riêng)
- `server/appsettings.json` - Thêm config cho player area (nếu cần)

## Implementation Order

1. **Authentication Setup** (chọn option phù hợp)

- Nếu Option 1: Setup Identity riêng cho players
- Nếu Option 2: Setup token-based auth
- Nếu Option 3: Setup session-based với PlayerName

2. **PlayerWebService**

- Implement các methods để query player data

3. **Player Pages**

- Dashboard/Home page
- Profile page
- Stats page
- History page
- Settings page (nếu cần)

4. **Layout & Styling**

- Tạo layout riêng cho player area
- Styling phù hợp với game theme

5. **API Endpoints** (Optional)

- Tạo API controller cho AJAX calls

## Notes