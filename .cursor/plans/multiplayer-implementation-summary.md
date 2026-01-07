# Multiplayer Implementation Summary

## Tổng quan Kiến trúc

```
┌─────────────────────────────────────────────────────────────────────────┐
│                           UNITY CLIENT                                   │
│  ┌─────────────┐  ┌──────────────┐  ┌─────────────────┐                │
│  │  NetClient  │  │PlayerInput   │  │ServerState      │                │
│  │  (Singleton)│  │Sender        │  │Applier          │                │
│  └──────┬──────┘  └──────┬───────┘  └────────┬────────┘                │
│         │                │                    │                         │
│         │ Register/Join  │ Input (20Hz)      │ Poll State              │
└─────────┼────────────────┼────────────────────┼─────────────────────────┘
          │                │                    │
          ▼                ▼                    ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                         ASP.NET CORE SERVER                             │
│  ┌─────────────┐  ┌──────────────┐  ┌─────────────────┐                │
│  │   Auth      │  │  Sessions    │  │   Health        │                │
│  │  Controller │  │  Controller  │  │   Controller    │                │
│  └──────┬──────┘  └──────┬───────┘  └─────────────────┘                │
│         │                │                                              │
│         ▼                ▼                                              │
│  ┌─────────────────────────────────────────────────────────────────┐   │
│  │                    WORLD SERVICE (Singleton)                     │   │
│  │  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────────────┐│   │
│  │  │ Sessions │  │ Players  │  │ Enemies  │  │ Projectiles      ││   │
│  │  └──────────┘  └──────────┘  └──────────┘  └──────────────────┘│   │
│  └─────────────────────────────────────────────────────────────────┘   │
│         │                                                               │
│         ▼                                                               │
│  ┌─────────────────────────────────────────────────────────────────┐   │
│  │               PLAYER SERVICE (Scoped)                            │   │
│  │  Database CRUD: PlayerProfile, PlayerStats, Inventory, Skills   │   │
│  └─────────────────────────────────────────────────────────────────┘   │
│         │                                                               │
│         ▼                                                               │
│  ┌─────────────────────────────────────────────────────────────────┐   │
│  │                    SQLite Database                               │   │
│  │  Tables: PlayerProfiles, PlayerStats, SkillUnlocks, InventoryItems │
│  └─────────────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## SERVER (`server/`)

### 1. Program.cs - Entry Point & Configuration

| Tính năng        | Mô tả                                        |
| ---------------- | -------------------------------------------- |
| Controllers      | ASP.NET Core MVC Controllers                 |
| JSON Options     | Case-insensitive để nhận camelCase từ client |
| EF Core + SQLite | Database persistence                         |
| WorldService     | Singleton quản lý game state                 |
| PlayerService    | Scoped service cho database operations       |
| GameLoopService  | Hosted service chạy game tick 20Hz           |
| Request Logging  | Middleware log tất cả requests               |
| Scalar UI        | API documentation tại `/scalar/v1`           |

### 2. Controllers

#### AuthController (`/auth`)

| Endpoint                   | Method | Mô tả                                                          |
| -------------------------- | ------ | -------------------------------------------------------------- |
| `/auth/register`           | POST   | Đăng ký/đăng nhập bằng tên. Tìm hoặc tạo player trong database |
| `/auth/profile/{playerId}` | GET    | Lấy thông tin profile player                                   |

#### SessionsController (`/sessions`)

| Endpoint               | Method | Mô tả                           |
| ---------------------- | ------ | ------------------------------- |
| `/sessions/join`       | POST   | Tham gia session (default)      |
| `/sessions/input`      | POST   | Gửi input (move, attack, shoot) |
| `/sessions/{id}/state` | GET    | Lấy game state snapshot         |
| `/sessions/{id}/reset` | POST   | Reset session (debug)           |
| `/sessions/save`       | POST   | Lưu progress vào database       |
| `/sessions/disconnect` | POST   | Ngắt kết nối + lưu progress     |

#### HealthController (`/health`)

| Endpoint  | Method | Mô tả        |
| --------- | ------ | ------------ |
| `/health` | GET    | Health check |

### 3. Services

#### WorldService (Singleton)

| Chức năng          | Mô tả                                             |
| ------------------ | ------------------------------------------------- |
| Session Management | Quản lý nhiều sessions (hiện chỉ có "default")    |
| Player State       | Theo dõi vị trí, HP, Level, Exp, Gold của players |
| Enemy AI           | Chase/Attack logic cho enemies                    |
| Combat             | Melee hit detection, Ranged projectiles           |
| Input Processing   | Xử lý input từ clients, clamp values              |
| Game Tick          | Cập nhật mỗi 50ms (20Hz)                          |

#### PlayerService (Scoped)

| Chức năng               | Mô tả                            |
| ----------------------- | -------------------------------- |
| FindOrCreatePlayerAsync | Tìm player theo tên hoặc tạo mới |
| GetPlayerAsync          | Lấy player theo ID               |
| SaveProgressAsync       | Lưu Exp, Gold, Level, HP         |
| AddInventoryItemAsync   | Thêm item vào inventory          |
| AddSkillUnlockAsync     | Thêm skill unlock                |

#### GameLoopService (Hosted)

| Chức năng       | Mô tả                               |
| --------------- | ----------------------------------- |
| Background Loop | Chạy liên tục trong background      |
| Tick Rate       | Gọi WorldService.TickAsync mỗi 50ms |

### 4. Models

#### Entities (Database)

| Entity        | Fields                                                                   |
| ------------- | ------------------------------------------------------------------------ |
| PlayerProfile | Id, Name, TokenHash, Level, Exp, Gold, CreatedAt                         |
| PlayerStats   | PlayerId, Damage, Range, KnockbackForce, Speed, MaxHealth, CurrentHealth |
| SkillUnlock   | PlayerId, SkillId, Level                                                 |
| InventoryItem | Id, PlayerId, ItemId, Quantity                                           |

#### States (In-Memory)

| State           | Fields                                                                        |
| --------------- | ----------------------------------------------------------------------------- |
| PlayerState     | Id, Name, X, Y, Hp, MaxHp, Damage, Range, Speed, Level, Exp, Gold, Sequence   |
| EnemyState      | Id, TypeId, X, Y, Hp, MaxHp, Speed, Damage, Range, Status (Idle/Chase/Attack) |
| ProjectileState | Id, OwnerId, X, Y, DirX, DirY, Speed, Damage, Radius, Lifetime                |
| SessionState    | SessionId, Version, Players, Enemies, Projectiles                             |

#### DTOs (API)

| DTO                         | Usage                          |
| --------------------------- | ------------------------------ |
| RegisterRequest/Response    | Đăng ký player                 |
| JoinSessionRequest/Response | Join session                   |
| InputRequest                | Gửi player input               |
| StateResponse               | Game state snapshot            |
| PlayerSnapshot              | Player data trong snapshot     |
| EnemySnapshot               | Enemy data trong snapshot      |
| ProjectileSnapshot          | Projectile data trong snapshot |
| SaveProgressRequest         | Save progress                  |
| DisconnectRequest           | Disconnect                     |

---

## CLIENT (`game/Assets/Scripts/Net/`)

### 1. NetClient.cs (Singleton, DontDestroyOnLoad)

| Chức năng                | Mô tả                                |
| ------------------------ | ------------------------------------ |
| Singleton Pattern        | Instance duy nhất persist qua scenes |
| ConfigureBaseUrl         | Cấu hình server URL                  |
| RegisterPlayer           | Đăng ký với server                   |
| JoinSession              | Tham gia session                     |
| SendInput                | Gửi input lên server                 |
| PollState                | Poll game state từ server            |
| StartPolling/StopPolling | Quản lý polling loop                 |
| SaveProgress             | Lưu progress lên server              |
| Disconnect               | Ngắt kết nối + lưu                   |

**Properties:**

- `PlayerId` - GUID của player
- `Token` - Authentication token
- `SessionId` - ID của session hiện tại
- `IsConnected` - Trạng thái kết nối

### 2. MultiplayerUIManager.cs

| Chức năng             | Mô tả                                                       |
| --------------------- | ----------------------------------------------------------- |
| UI Bindings           | ServerUrl input, PlayerName input, Join button, Status text |
| ConnectViaLoginButton | Gọi Register → Join → Load RPG scene                        |
| Disconnect            | Gọi NetClient.Disconnect                                    |
| PlayerPrefs           | Nhớ URL/Name đã nhập                                        |

### 3. PlayerInputSender.cs

| Chức năng     | Mô tả                                          |
| ------------- | ---------------------------------------------- |
| SendLoop      | Coroutine gửi input mỗi 50ms (20Hz)            |
| Input Capture | moveX, moveY từ Input.GetAxisRaw               |
| Attack/Shoot  | pendingAttack/pendingShoot flags (event-based) |
| Sequence      | Tăng dần cho mỗi input packet                  |

### 4. ServerStateApplier.cs

| Chức năng         | Mô tả                                           |
| ----------------- | ----------------------------------------------- |
| ApplySnapshot     | Áp dụng snapshot vào player (position, HP)      |
| Lerp Movement     | Smoothly di chuyển về target position           |
| Auto-Poll         | Tự động poll state khi connected                |
| Auto-Save         | Lưu progress mỗi 30 giây                        |
| OnApplicationQuit | Lưu progress khi tắt game                       |
| Dev Mode          | Auto-connect khi load RPG scene trực tiếp       |
| Sync StatsManager | Cập nhật StatsManager.Instance với HP từ server |

### 5. StateLogger.cs

| Chức năng | Mô tả                              |
| --------- | ---------------------------------- |
| OnState   | Log game state vào Console (debug) |

---

## Data Flow

### 1. Đăng ký & Kết nối

```
1. User nhập Name, click Join
2. Client: POST /auth/register { playerName: "Khoa" }
3. Server: FindOrCreatePlayerAsync("Khoa")
   - Nếu tồn tại: load từ DB
   - Nếu mới: tạo mới với default stats
4. Server: RegisterOrLoadPlayer() → thêm vào in-memory session
5. Server: Return { playerId, token, sessionId }
6. Client: Lưu PlayerId, Token, SessionId
7. Client: POST /sessions/join
8. Client: Load scene "RPG"
9. Client: Bắt đầu polling + sending input
```

### 2. Gameplay Loop

```
Client (20Hz):
├── PlayerInputSender: POST /sessions/input { moveX, moveY, attack, shoot }
└── ServerStateApplier: GET /sessions/default/state → ApplySnapshot

Server (20Hz):
├── ProcessInputs: Xử lý movement, attack, shoot
├── UpdateProjectiles: Di chuyển, hit detection
├── UpdateEnemies: AI chase/attack
└── Increment Version
```

### 3. Lưu Progress

```
Auto-Save (30s):
└── Client: POST /sessions/save { playerId }
    └── Server: PlayerService.SaveProgressAsync(exp, gold, level, hp)

On Disconnect/Quit:
└── Client: POST /sessions/disconnect { playerId }
    └── Server: Save + Remove from session
```

---

## Authoritative Server Model

| Aspect         | Client                       | Server                     |
| -------------- | ---------------------------- | -------------------------- |
| Input          | Gửi raw input (move, attack) | Nhận và validate           |
| Movement       | Hiển thị position từ server  | Tính toán position         |
| Combat         | Gửi "attack=true"            | Tính hit detection, damage |
| HP/Damage      | Hiển thị từ server           | Source of truth            |
| Exp/Level/Gold | Hiển thị từ server           | Source of truth            |
| Persistence    | Không lưu local              | Lưu vào SQLite             |

---

## Files Changed Summary

### Server (New Files)

```
server/
├── Program.cs                           # Entry point
├── GameServer.csproj                    # Project file
├── appsettings.json                     # Config
├── appsettings.Development.json         # Dev config with logging
├── gameserver.db                        # SQLite database
├── Controllers/
│   ├── AuthController.cs                # /auth endpoints
│   ├── SessionsController.cs            # /sessions endpoints
│   └── HealthController.cs              # /health endpoint
├── Services/
│   ├── WorldService.cs                  # Game state management
│   ├── PlayerService.cs                 # Database operations
│   └── GameLoopService.cs               # Background tick loop
├── Data/
│   └── GameDbContext.cs                 # EF Core DbContext
├── Models/
│   ├── Entities/
│   │   ├── PlayerProfile.cs
│   │   ├── PlayerStats.cs
│   │   ├── SkillUnlock.cs
│   │   └── InventoryItem.cs
│   ├── States/
│   │   ├── PlayerState.cs
│   │   ├── EnemyState.cs
│   │   ├── ProjectileState.cs
│   │   └── SessionState.cs
│   └── Dto/
│       ├── RegisterDto.cs
│       ├── JoinSessionDto.cs
│       ├── InputRequest.cs
│       ├── SnapshotDto.cs
│       └── SaveProgressDto.cs
└── Migrations/                          # EF Core migrations
```

### Client (New Files)

```
game/Assets/Scripts/Net/
├── NetClient.cs                         # Network singleton
├── MultiplayerUIManager.cs              # Login UI
├── PlayerInputSender.cs                 # Input sender
├── ServerStateApplier.cs                # State applier
└── StateLogger.cs                       # Debug logger
```

### Client (Modified Files)

```
game/Assets/Scripts/PlayerScripts/
├── PlayerMovement.cs                    # Added null checks
├── PlayerHealth.cs                      # Added events, Respawn()
├── PlayerDeathHandler.cs                # NEW: Death/respawn handler
├── StatsManager.cs                      # Synced with server
└── StatsUI.cs                           # Added UpdateHealth()

game/Assets/Scripts/Enemy/
└── Enemy_Combat.cs                      # Check player active before knockback

game/Assets/Scripts/Inventory & Shop/
└── ShopKeeper.cs                        # Null check for canvas
```

---

## Còn lại / TODO

1. **Enemy respawn** - Server cần respawn enemies sau khi chết
2. **Loot system** - Drop items khi enemy chết
3. **Shop integration** - Server validate purchases
4. **Skill system** - Server manage skill unlocks
5. **Multiple enemies** - Spawn nhiều enemy types
6. **Player attack animation** - Sync với server
7. **Multiplayer sync** - Hiển thị players khác
8. **Anti-cheat** - Rate limiting, token validation
