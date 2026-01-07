---

name: "Plan 2: Player Web Panel"

overview: Xây dựng trang web panel cho players để xem profile, stats, lịch sử chơi, và quản lý tài khoản của mình. Sử dụng authentication riêng hoặc tích hợp với hệ thống hiện có.

todos:

- id: player_auth_decision

content: Quyết định authentication method cho players (Identity riêng / Token-based / Session-based)

status: completed

- id: player_auth_setup

content: Setup authentication system cho players theo method đã chọn

status: completed

dependencies:

- player_auth_decision
- id: player_web_service

content: Tạo PlayerWebService với các methods để query player data (profile, stats, history)

status: completed

- id: player_layout

content: Tạo layout và styling cho player web panel

status: completed

- id: player_dashboard

content: Xây dựng Dashboard/Home page cho players

status: completed

dependencies:

- player_auth_setup
- player_web_service
- player_layout
- id: player_profile

content: Xây dựng Profile page để xem thông tin cá nhân

status: completed

dependencies:

- player_auth_setup
- player_web_service
- player_layout
- id: player_stats

content: Xây dựng Stats page để xem stats chi tiết

status: completed

dependencies:

- player_auth_setup
- player_web_service
- player_layout
- id: player_history

content: Xây dựng History page để xem lịch sử chơi (sử dụng GameSession, SessionPlayer từ Plan 1)

status: completed

dependencies:

- player_auth_setup
- player_web_service
- player_layout
- id: player_results

content: Xây dựng Game Results page (sử dụng GameResult từ Plan 3)

status: completed

dependencies:

- player_auth_setup
- player_web_service
- player_layout
- id: player_api

content: Tạo PlayerApiController cho AJAX calls (optional)

status: completed

dependencies:

- player_web_service
- id: ui_improvements

content: Cải thiện UI/UX cho Login/Register với theme game, animated particles, Google OAuth

status: completed

- id: database_auth_fields

content: Thêm Email, PasswordHash, GoogleId vào PlayerProfile entity

status: completed

- id: migration_auth_fields

content: Tạo migration cho các fields authentication mới

status: pending

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
- ✅ **ADDED:** `GetPlayerProfileByEmailAsync(email)` - Tìm player theo email
- ✅ **ADDED:** `GetPlayerProfileByGoogleIdAsync(googleId)` - Tìm player theo Google ID
- ✅ **ADDED:** `CreatePlayerAccountAsync(username, email, password)` - Tạo account mới
- ✅ **ADDED:** `VerifyPassword(player, password)` - Xác thực password
- ✅ **ADDED:** `CreateOrLinkGoogleAccountAsync(googleId, email, username)` - Tạo/link Google account
- `UpdatePlayerSettingsAsync(playerId, settings)` - Cập nhật settings (TODO)

## Player Web Pages

**`server/Pages/` (Root Level - NEW)**

- ✅ **CREATED:** `Index.cshtml` + `.cshtml.cs` - Root login page với UI đẹp, theme game
  - Username/Password login
  - Google OAuth integration
  - Gmail login flow với username setup
- ✅ **CREATED:** `Register.cshtml` + `.cshtml.cs` - Registration page với UI đẹp, theme game

**`server/Areas/Player/Pages/`**

- `Login.cshtml` + `.cshtml.cs` - Đăng nhập (legacy, có thể giữ hoặc remove)
- ✅ **CREATED:** `Index.cshtml` + `.cshtml.cs` - Dashboard/Home
- ✅ **CREATED:** `Profile.cshtml` + `.cshtml.cs` - Xem profile
- ✅ **CREATED:** `Stats.cshtml` + `.cshtml.cs` - Xem stats chi tiết
- ✅ **CREATED:** `History.cshtml` + `.cshtml.cs` - Lịch sử chơi
- ✅ **CREATED:** `Results.cshtml` + `.cshtml.cs` - Game Results (placeholder cho Plan 3)
- `Settings.cshtml` + `.cshtml.cs` - Cài đặt (nếu cần - TODO)

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

## Files Created

**Services:**

- ✅ `server/Services/PlayerWebService.cs` - Service cho player web panel (ENHANCED với auth methods)

**Root Pages (NEW):**

- ✅ `server/Pages/Index.cshtml` + `.cshtml.cs` - Root login page với UI đẹp
- ✅ `server/Pages/Register.cshtml` + `.cshtml.cs` - Registration page với UI đẹp

**Player Pages:**

- ✅ `server/Areas/Player/Pages/Index.cshtml` + `.cshtml.cs` - Dashboard
- ✅ `server/Areas/Player/Pages/Profile.cshtml` + `.cshtml.cs` - Profile
- ✅ `server/Areas/Player/Pages/Stats.cshtml` + `.cshtml.cs` - Stats
- ✅ `server/Areas/Player/Pages/History.cshtml` + `.cshtml.cs` - Match History
- ✅ `server/Areas/Player/Pages/Results.cshtml` + `.cshtml.cs` - Game Results (placeholder)
- ✅ `server/Areas/Player/Pages/Login.cshtml` + `.cshtml.cs` - Login (legacy)
- ✅ `server/Areas/Player/Pages/Logout.cshtml` + `.cshtml.cs` - Logout
- ✅ `server/Areas/Player/Pages/BasePlayerPageModel.cs` - Base class cho authenticated pages
- ✅ `server/Areas/Player/Pages/Shared/_Layout.cshtml` - Layout
- ✅ `server/Areas/Player/Pages/_ViewImports.cshtml` - View imports
- ✅ `server/Areas/Player/Pages/_ViewStart.cshtml` - View start
- `server/Areas/Player/Pages/Settings.cshtml` + `.cshtml.cs` - Settings (optional - TODO)

**Controllers:**

- ✅ `server/Areas/Player/Controllers/PlayerApiController.cs` - API cho AJAX
- ✅ **UPDATED:** `server/Controllers/AuthController.cs` - Added Google OAuth endpoint

**Models:**

- ✅ `server/Areas/Player/Models/PlayerProfileViewModel.cs`
- ✅ `server/Areas/Player/Models/PlayerStatsViewModel.cs`
- ✅ `server/Areas/Player/Models/MatchHistoryViewModel.cs`
- ✅ **CREATED:** `server/Models/Dto/PlayerProfileDto.cs` - DTO cho API

## Files Modified

- ✅ **UPDATED:** `server/Program.cs` 
  - Added session middleware configuration
  - Added PlayerWebService registration với GameConfigService dependency
  - Allow anonymous access to root pages
- ✅ **UPDATED:** `server/Models/Entities/PlayerProfile.cs`
  - Added Email, PasswordHash, GoogleId fields
- ✅ **UPDATED:** `server/Services/PlayerWebService.cs`
  - Added authentication-related methods
  - Updated constructor để inject GameConfigService
- `server/appsettings.json` - Thêm config cho player area (nếu cần - TODO cho Google OAuth)

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

## Implementation Changes & Updates

### ✅ Completed Changes

#### 1. Authentication System (Option 3 - Session-based với Username/Password + Google OAuth)

**Decision:** Chọn Option 3 với mở rộng:

- Session-based authentication
- Username/Password login
- Google OAuth integration (mock flow, ready for real OAuth)
- Email-based registration

**Files Created/Modified:**

- `server/Pages/Index.cshtml` + `.cshtml.cs` - Root login page với UI đẹp
- `server/Pages/Register.cshtml` + `.cshtml.cs` - Registration page với UI đẹp
- `server/Areas/Player/Pages/Login.cshtml` + `.cshtml.cs` - Player area login (legacy, có thể giữ hoặc remove)

**Key Features:**

- Root URL (`/`) hiển thị login page nếu chưa đăng nhập
- Auto-redirect đến `/Player` nếu đã đăng nhập
- Google OAuth flow: Gmail login → form nhập username với password disabled (mặc định "1234")
- Password field disabled cho Gmail accounts với note rõ ràng

#### 2. Database Schema Updates

**Entity Changes:**

- `server/Models/Entities/PlayerProfile.cs`
  - ✅ Added: `Email` (string?, MaxLength(256))
  - ✅ Added: `PasswordHash` (string?, MaxLength(256))
  - ✅ Added: `GoogleId` (string?, MaxLength(256))

**Migration Required:**

- ⚠️ **PENDING:** `AddPlayerAuthFields` migration
- Run after stopping server: `dotnet ef migrations add AddPlayerAuthFields && dotnet ef database update`

#### 3. PlayerWebService Enhancements

**New Methods Added:**

- `GetPlayerProfileByEmailAsync(string email)` - Tìm player theo email
- `GetPlayerProfileByGoogleIdAsync(string googleId)` - Tìm player theo Google ID
- `CreatePlayerAccountAsync(string username, string email, string password)` - Tạo account mới với password
- `VerifyPassword(PlayerProfile player, string password)` - Xác thực password
- `CreateOrLinkGoogleAccountAsync(string googleId, string email, string? username)` - Tạo/link Google account
- `HashPassword(string password)` - Hash password bằng SHA256 (private method)

**Constructor Updated:**

- Thêm optional `GameConfigService` parameter để tạo player với default config

**File:** `server/Services/PlayerWebService.cs`

#### 4. UI/UX Improvements

**Design Features:**

- ✅ Animated background particles (20 particles floating)
- ✅ Gradient backgrounds (purple/blue theme)
- ✅ Glassmorphism effect (backdrop-filter blur)
- ✅ Smooth animations (slideUp, pulse)
- ✅ Game-themed styling với gamepad icon
- ✅ Responsive design
- ✅ Modern card-based layout

**Login Page (`server/Pages/Index.cshtml`):**

- Username/Password form
- Google OAuth button
- Gmail login flow với info box hiển thị email và note về password mặc định
- Password field disabled khi Gmail login với value "1234"
- Link đến Register page

**Register Page (`server/Pages/Register.cshtml`):**

- Username, Email, Password, Confirm Password fields
- Validation messages
- Google OAuth button
- Link đến Login page

**Styling:**

- Custom CSS với animations
- Bootstrap 5.3.0
- Font Awesome 6.4.0 icons
- Gradient buttons với hover effects
- Professional color scheme

#### 5. Google OAuth Integration (Mock)

**Current Implementation:**

- `server/Controllers/AuthController.cs` - Thêm `GoogleAuth` endpoint
- Mock flow: `/auth/google?email=xxx` → redirect với Gmail email
- Session storage cho Gmail email
- Ready for real Google OAuth (cần Google Cloud Console setup)

**Flow:**

1. User clicks "Continue with Google"
2. Redirects to `/auth/google` (mock) hoặc real Google OAuth
3. After OAuth success → redirect to `/?gmail=success&email=xxx`
4. Login page shows Gmail info box
5. User enters username
6. Password field disabled với value "1234"
7. Submit → create/link account → redirect to `/Player`

#### 6. Root Index Page

**New File:** `server/Pages/Index.cshtml` + `.cshtml.cs`

**Purpose:**

- Root URL (`http://localhost:5220/`) hiển thị login page
- Replaces simple redirect logic
- Beautiful UI với game theme
- Handles both regular login và Gmail login flow

**Logic:**

- `OnGet()`: Check session → redirect to `/Player` if logged in, else show login
- `OnPostAsync()`: Handle username/password login hoặc Gmail username setup

#### 7. Program.cs Updates

**Changes:**

- ✅ Added session middleware configuration
- ✅ Added `PlayerWebService` registration với `GameConfigService` dependency
- ✅ Allow anonymous access to root pages (`/`, `/Register`)

**Code:**

```csharp
// Session configuration
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options => { ... });

// PlayerWebService with GameConfigService
builder.Services.AddScoped<PlayerWebService>(sp => {
    var db = sp.GetRequiredService<GameDbContext>();
    var logger = sp.GetRequiredService<ILogger<PlayerWebService>>();
    var configService = sp.GetService<GameConfigService>();
    return new PlayerWebService(db, logger, configService);
});
```

#### 8. Additional Files Created

**DTOs:**

- `server/Models/Dto/PlayerProfileDto.cs` - DTO cho API responses

**Controllers:**

- Updated `server/Controllers/AuthController.cs` - Added Google OAuth endpoint

### ⚠️ Pending Tasks

1. **Migration Creation:**

   - Need to create migration for `Email`, `PasswordHash`, `GoogleId` fields
   - Command: `dotnet ef migrations add AddPlayerAuthFields`
   - Then: `dotnet ef database update`

2. **Real Google OAuth Setup (Optional):**

   - Install package: `Microsoft.AspNetCore.Authentication.Google`
   - Configure Google Cloud Console
   - Add ClientId và ClientSecret to appsettings.json
   - Update `Program.cs` với Google authentication middleware

### 📝 Notes

- **Password Hashing:** Hiện tại dùng SHA256 (simple cho student project). Production nên dùng bcrypt hoặc ASP.NET Core Identity PasswordHasher.
- **Google OAuth:** Hiện tại là mock flow. Để implement thật, cần:

  1. Google Cloud Console project
  2. OAuth 2.0 credentials
  3. Redirect URIs configuration
  4. Package installation và middleware setup

- **Session Management:** Session timeout 8 hours, stored in memory (DistributedMemoryCache). Production nên dùng Redis hoặc SQL Server session store.
- **UI Theme:** Game-themed với purple/blue gradients, animated particles, modern glassmorphism effects.
- **Gmail Password:** Mặc định "1234" cho Gmail accounts, không thể thay đổi (password field disabled). Có thể cải thiện sau bằng cách cho phép đổi password.

### 🔄 Migration from Old System

Nếu có players cũ chỉ có `Name` và `TokenHash`:

- Có thể login bằng username (legacy support)
- Khi login, có thể prompt để set password/email
- Hoặc tự động tạo password mặc định

## Notes