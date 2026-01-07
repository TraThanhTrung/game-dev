---
name: server-mechanics-phase1-2
overview: Implement core game server mechanics (movement, input, state polling, enemy combat) with controllers and 20Hz tick.
todos:
  - id: setup-api
    content: Setup controllers pipeline + health endpoint
    status: completed
  - id: persistence
    content: Add EF Core + SQLite DbContext/entities for players/profiles
    status: completed
    dependencies:
      - setup-api
  - id: client-net
    content: Add Unity client NetClient + basic connect UI
    status: completed
    dependencies:
      - controllers
  - id: models-dto
    content: Add in-memory models/DTOs for player/enemy/projectile/state
    status: completed
    dependencies:
      - setup-api
  - id: world-tick
    content: Implement WorldService + 20Hz GameLoopService
    status: completed
    dependencies:
      - models-dto
  - id: controllers
    content: Add Auth/Sessions controllers with routes
    status: completed
    dependencies:
      - world-tick
  - id: combat-logic
    content: Implement melee/ranged combat and HP handling
    status: completed
    dependencies:
      - world-tick
---

# Kế hoạch Phase 1-2: Server cơ bản + combat

## Phạm vi

- ASP.NET Core Web API (controllers) cho bước 1-2: đăng ký/join, input, state polling, tick 20Hz, enemy AI đơn giản, melee/ranged hit, HP.

## Việc cần làm

1) **Thiết lập dự án Web API (controllers)**

- Cập nhật `GameServer.csproj` thêm các package cần: `Microsoft.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.Sqlite`, `Microsoft.EntityFrameworkCore.Tools`.
- Sửa `Program.cs`: bật controllers, add DbContext SQLite (connection string `Data Source=gameserver.db`), add hosted services, add Swagger dev-only, add health endpoint map.
- Thiết lập DI: đăng ký `WorldService`, `GameLoopService` (hosted), repo lớp mỏng nếu cần sau.
- File dự kiến chỉnh @server/:  
- `server/GameServer.csproj`  
- `server/Program.cs`

2) **Persistence (EF Core + SQLite)**

- Tạo `Data/GameDbContext.cs` với DbSet cho `PlayerProfile`, `PlayerStats`, `SkillUnlock`, `InventoryItem`.
- Entities tối thiểu:
- `PlayerProfile` (Guid Id, string Name, string TokenHash, int Level, int Exp, int Gold, DateTime CreatedAt).
- `PlayerStats` (Guid PlayerId, int Damage, float Range, float KnockbackForce, float Speed, int MaxHealth).
- `SkillUnlock` (Guid PlayerId, string SkillId, int Level).
- `InventoryItem` (Guid PlayerId, string ItemId, int Quantity).
- Thêm migration khởi tạo và update database (sẵn sàng chạy `dotnet ef database update`).
- Seed dữ liệu demo tùy chọn (1 player test) trong `OnModelCreating` hoặc service seed.
- File dự kiến chỉnh/đặt mới @server/:  
- `server/Data/GameDbContext.cs`  
- `server/Models/Entities/*.cs` (PlayerProfile, PlayerStats, SkillUnlock, InventoryItem)

3) **Model & DTO in-memory**

- Thêm thư mục `Models/` với: `PlayerState`, `EnemyState`, `ProjectileState`, `SessionState`, `SnapshotDTO` (PlayerSnapshot/EnemySnapshot/ProjectileSnapshot/StateResponse), `InputRequest`, `Register/Join` DTO.
- Thêm `Enums` cho enemy state, damage sources nếu cần.
- File dự kiến chỉnh/đặt mới @server/:  
- `server/Models/States/*.cs` (PlayerState, EnemyState, ProjectileState, SessionState)  
- `server/Models/Dto/*.cs` (InputRequest, RegisterRequest, JoinSessionRequest, Snapshot DTOs)

4) **World/Session manager + Tick (20Hz)**

- Thêm `Services/WorldService` (in-memory): quản lý sessions, players, enemies, projectiles, version counter; nhận input queue; clamp input; apply move; handle simple enemy AI (idle/chase/attack), hit detection (circle), HP update, death.
- Hosted service `GameLoopService` (BackgroundService) gọi `WorldService.TickAsync` mỗi 50ms.
- File dự kiến @server/:  
- `server/Services/WorldService.cs`  
- `server/Services/GameLoopService.cs`

5) **Controllers & Routes**

- `AuthController`: `POST /auth/register` (tạo playerId + token tạm), trả token.
- `SessionsController`: `POST /sessions/join` (vào session default), `POST /sessions/input` (moveX/moveY/attack/shoot/sequence), `GET /sessions/{id}/state?sinceVersion=` trả snapshot (players/enemies/projectiles, version).
- `HealthController`: `GET /health`.
- File dự kiến @server/:  
- `server/Controllers/AuthController.cs`  
- `server/Controllers/SessionsController.cs`  
- `server/Controllers/HealthController.cs`

6) **Logic combat cơ bản**

- Melee: khi input.attack=true, server tính hit circle tại player (dùng range từ default stats) lên enemy trong phạm vi.
- Ranged: khi input.shoot=true, spawn projectile (speed, life), tick di chuyển và kiểm tra va chạm enemy; remove on hit.
- HP: player/enemy giảm HP, enemy chết tăng version, respawn sẽ để TODO.

7) **Client (@game) - kết nối cơ bản**

- Thêm prefab Canvas “MultiplayerMenu” (đơn giản) với input Server URL, Player Name, nút Register/Join, text trạng thái.
- Thêm script `NetClient` (ScriptableObject hoặc singleton) để lưu baseUrl/playerId/token/sessionId; coroutine poll state (có thể tắt/mặc định off nếu chưa có server).
- Thêm `MultiplayerUIManager` để gọi API register/join, hiển thị lỗi/thành công.
- Hook nhẹ vào `PlayerMovement` để không gửi input nếu chưa kết nối (flag) — không thay đổi gameplay hiện tại.
- File dự kiến @game/:  
- `game/Assets/Scripts/Net/NetClient.cs` (mới)  
- `game/Assets/Scripts/Net/MultiplayerUIManager.cs` (mới)  
- Prefab UI: `game/Assets/Prefabs/UI/MultiplayerMenu.prefab` (mới)  