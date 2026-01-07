# TỔNG HỢP CÂU HỎI VÀ CÂU TRẢ LỜI
## DỰ ÁN TOP-DOWN 2D MULTIPLAYER GAME

---

## MỤC LỤC

1. [Script Báo Cáo Tiến Độ (High-Level)](#1-script-báo-cáo-tiến-độ-high-level)
2. [3 Cơ Chế Cốt Lõi - Chi Tiết](#2-3-cơ-chế-cốt-lõi---chi-tiết)
3. [Polling Interval](#3-polling-interval)
4. [Database Storage](#4-database-storage)
5. [File Configuration](#5-file-configuration)
6. [Chức Năng Server (High-Level)](#6-chức-năng-server-high-level)

---

## 1. SCRIPT BÁO CÁO TIẾN ĐỘ (HIGH-LEVEL)

### 1.1 Cơ Chế Màn Hình LOGIN

**Kiến trúc:** Client-Server với REST API

**Luồng xác thực:**

1. **Khởi tạo:** Client tạo singleton network manager khi vào Login scene
2. **Đăng ký:** Client gửi tên player → Server tìm/tạo player trong database → Trả về PlayerId, Token, SessionId
3. **Join Session:** Client gửi credentials → Server thêm player vào session (in-memory)
4. **Chuyển Scene:** Client chuyển sang scene game, network manager được giữ lại

**Design Decisions:**
- REST API (đơn giản, dễ debug, phù hợp dự án học thuật)
- Token-based authentication (đơn giản cho demo)
- Singleton network manager (quản lý kết nối tập trung)

---

### 1.2 Cơ Chế Màn Hình RPG - Gameplay & Synchronization

**Kiến trúc:** Authoritative Server (Thin Client)

```
Unity Client (Thin)          ←→          Server (Authoritative)
├─ Input Handling                       ├─ Game Logic
├─ Rendering                            ├─ State Management  
├─ Animation                            ├─ Validation
└─ API Communication                    └─ Database
```

**Client:** Xử lý input, rendering, animation, giao tiếp API  
**Server:** Xử lý logic, validation, quản lý state, persistence

#### A. Gameplay Systems

- **Movement System:** Client di chuyển ngay (responsive), server xác nhận position
- **Combat System:** Client-side animation/visuals, server-side validation/rewards
- **Progression System:** Server quản lý level/exp/gold authoritatively
- **Inventory & Economy:** Client UI, server validation

#### B. Synchronization Architecture

**Client → Server (Input):**
- Gửi theo chu kỳ (20Hz - fixed interval)
- Gói tin nhẹ: movement direction, actions, sequence number
- Server queue và xử lý input trong game loop

**Server → Client (State):**
- Polling (periodic requests)
- Versioning: Chỉ nhận delta state
- Interpolation: Smooth position updates

**Event-driven:**
- Kill events, damage events, respawn events

#### C. State Consistency

- **Server là source of truth**
- Client predictive rendering
- Server state override client state khi có conflict

---

## 2. 3 CƠ CHẾ CỐT LÕI - CHI TIẾT

### 2.1 Cơ Chế SESSION

**Khái niệm:** Session là container chứa game state cho một nhóm players cùng chơi.

**Cấu trúc dữ liệu:**
```csharp
SessionState {
    SessionId: string
    Version: int  // Tăng mỗi khi state thay đổi
    Players: Dictionary<Guid, PlayerState>
    Enemies: Dictionary<Guid, EnemyState>
    Projectiles: Dictionary<Guid, ProjectileState>
}
```

**Vòng đời Session:**

1. **Khởi tạo:** Tạo session mới khi player đầu tiên đăng ký
2. **Join Session:** Thêm player vào session, tăng version
3. **Versioning:** Tăng version mỗi khi state thay đổi
4. **Lưu trữ:** In-memory (ConcurrentDictionary), không persistent

**Thread Safety:** 
- ConcurrentDictionary cho collections
- Lock cho các thao tác modify state

---

### 2.2 Cơ Chế SYNC STATE POLLING

**Khái niệm:** Client gửi request định kỳ để lấy state mới từ server.

**Client-side:**
- **Polling loop:** Gửi request mỗi 0.07s (từ config)
- **sinceVersion:** Chỉ nhận delta state (optimization)
- **Interval:** 0.07s = ~14.3 requests/giây

**Server-side:**
- **GetState():** Kiểm tra sinceVersion
- Nếu client đã có state mới nhất → Trả về empty (HTTP 204)
- Nếu có thay đổi → Trả về full snapshot

**Position Interpolation:**
- SmoothDamp để làm mượt
- Threshold để tránh micro-jitter

**Tại sao Polling:**
- Đơn giản hơn WebSocket
- RESTful, dễ debug
- Phù hợp cho small-scale multiplayer

---

### 2.3 Cơ Chế SERVER AUTHORITATIVE

**Khái niệm:** Server là source of truth - mọi quyết định quan trọng đều do server xác nhận.

**Ví dụ: Player Movement**
- Client: Gửi input, render predictive
- Server: Tính position authoritatively (20Hz game loop)
- Client: Nhận state và sync (override local position)

**Ví dụ: Damage System**
- Client: Báo cáo damage (nhưng không cập nhật HP ngay)
- Server: Tính HP authoritatively
- Client: Nhận HP từ server qua polling

**Ví dụ: Kill Rewards**
- Client: Báo cáo kill
- Server: Trao EXP/Gold, tính level up authoritatively
- Client: Nhận state mới qua polling

**Validation & Anti-Cheat:**
- Input validation (clamp values)
- Speed validation (server speed, không tin client)
- Sequence numbers (phát hiện packet loss)

**Nguyên tắc:**
- Client: Input, Rendering, Prediction
- Server: Validation, Logic, State Management
- Conflict Resolution: Server state luôn override client state

---

## 3. POLLING INTERVAL

**Câu hỏi:** Client gửi request định kỳ mỗi bao nhiêu giây? Có phải 0.07s hay không?

**Trả lời:** 

✅ **Đúng:** `stateIntervalSeconds: 0.07` nghĩa là client gửi request mỗi **0.07 giây (70ms)**

**Chi tiết:**
- **0.07 giây** = 70 milliseconds = **~14.3 lần mỗi giây** (1 ÷ 0.07 ≈ 14.3 Hz)

**Timeline:**
```
Time (giây):
0.00s → Client gửi Request #1 → Server trả Response #1
0.07s → Client gửi Request #2 → Server trả Response #2
0.14s → Client gửi Request #3 → Server trả Response #3
0.21s → Client gửi Request #4 → Server trả Response #4
...
```

**Giải thích các giá trị trong config:**
```json
"polling": {
    "stateIntervalSeconds": 0.07,     // ← Khoảng cách giữa các request (0.07s)
    "lerpSpeed": 55.0,                 // ← Tốc độ smooth interpolation (KHÔNG liên quan đến polling)
    "positionChangeThreshold": 0.005   // ← Ngưỡng để cập nhật position (KHÔNG liên quan đến polling)
}
```

**Tại sao 0.07 giây?**
- Cân bằng giữa responsiveness và overhead
- 70ms latency tối đa giữa các update
- Phù hợp cho small-scale multiplayer

---

## 4. DATABASE STORAGE

**Câu hỏi:** Database lưu trữ qua đâu?

**Trả lời:** 

✅ **Database Engine:** SQLite (file-based database)  
✅ **File Database:** `server/gameserver.db`  
✅ **Connection String:** `Data Source=gameserver.db`

**Cấu trúc Database:**

- **PlayerProfile:** Player info (Id, Name, Level, Exp, Gold, CreatedAt)
- **PlayerStats:** Stats (Damage, Speed, HP, etc.) - One-to-One với PlayerProfile
- **InventoryItem:** Inventory items - One-to-Many với PlayerProfile
- **SkillUnlock:** Unlocked skills - One-to-Many với PlayerProfile

**Khi nào lưu vào Database:**

1. **Player đăng ký** (nếu player mới)
2. **Auto-save** (mỗi 30s hoặc khi disconnect)
3. **Manual save** (API `/sessions/save`)

**Phân biệt:**

- **In-Memory (WorldService):** Position, HP real-time, Enemy states - **Mất khi server restart**
- **Database (SQLite):** Player profiles, Stats, Inventory - **Persistent qua server restarts**

**Migration System:**
- Entity Framework Migrations
- Tự động chạy khi server start

**Ưu/Nhược điểm SQLite:**
- ✅ Ưu: Không cần database server, dễ backup, phù hợp small-scale
- ❌ Nhược: Không phù hợp large-scale, khó scale horizontal

---

## 5. FILE CONFIGURATION

**Câu hỏi:** File config polling này nằm ở đâu?

**Trả lời:**

✅ **File:** `shared/game-config.json`  
✅ **Đường dẫn đầy đủ:** `G:\game-dev\shared\game-config.json`

**Cấu trúc thư mục:**
```
g:\game-dev\
  ├── game\              (Unity project)
  │   └── Assets\
  └── shared\            ← Thư mục chứa config
      └── game-config.json  ← File config ở đây!
```

**Cách Unity load:**
- Relative path: `../../shared/game-config.json` (từ Assets folder)
- `Application.dataPath` = `G:\game-dev\game\Assets`
- `../../` = lên 2 cấp → `G:\game-dev\shared\game-config.json`

**Tại sao đặt trong `shared/`?**
- Dùng chung cho Unity client và ASP.NET server
- Dễ quản lý, tránh duplicate
- Dễ version control

**Nội dung config:**
- `playerDefaults`: Spawn position, default stats
- `expCurve`: EXP curve configuration
- `polling`: Polling settings (interval, lerpSpeed, threshold)
- `enemyStats`: Config cho từng enemy type

---

## 6. CHỨC NĂNG SERVER (HIGH-LEVEL)

**Câu hỏi:** Chức năng hiện tại của server bao gồm những gì? (Mô tả high-level, không quá chi tiết)

**Trả lời:**

### I. AUTHENTICATION & PLAYER MANAGEMENT

- **Đăng ký/Đăng nhập:** Tìm hoặc tạo player trong database, cấp credentials
- **Quản lý Profile:** Lưu và load thông tin player (level, exp, gold, stats)

### II. SESSION MANAGEMENT

- **Quản lý Sessions:** Tạo và quản lý game sessions (in-memory)
- **Join/Leave:** Tham gia/ngắt kết nối khỏi session
- **State Versioning:** Đánh số version để theo dõi thay đổi state

### III. GAMEPLAY SYSTEMS

#### A. Input Processing
- Nhận input từ client (movement, actions)
- Xử lý trong game loop (20Hz)
- Tính toán position authoritatively

#### B. State Synchronization
- **Polling:** Client request state định kỳ
- **Delta Updates:** Chỉ trả về thay đổi (optimization)
- **State Snapshot:** Trả về trạng thái game (players, enemies, projectiles)

#### C. Combat & Progression
- **Kill System:** Trao EXP/Gold khi giết enemy
- **Damage System:** Quản lý HP authoritatively
- **Level System:** Tính level up dựa trên EXP
- **Respawn System:** Hồi sinh tại spawn với 50% HP

### IV. PERSISTENCE

- **Database Storage:** SQLite lưu player data persistent
- **Auto-save:** Tự động lưu progress (mỗi 30s hoặc khi disconnect)
- **Manual Save:** API để lưu progress thủ công
- **Migration System:** Tự động cập nhật database schema

### V. BACKGROUND SERVICES

#### A. Game Loop Service
- Chạy game loop 20Hz (mỗi 50ms)
- Xử lý input queue
- Cập nhật game state

#### B. Configuration Service
- Load game config từ JSON
- Cung cấp config cho game logic (player defaults, enemy stats, exp curve)

### VI. INFRASTRUCTURE

- **Request Logging:** Log HTTP requests
- **Health Check:** Endpoint kiểm tra server status
- **API Documentation:** OpenAPI/Scalar UI (development mode)
- **Thread Safety:** Concurrent collections và locks cho multi-threading

---

## TÓM TẮT CHÍNH

### Kiến Trúc Tổng Quan

```
Client (Unity)
    ↓
REST API (ASP.NET Core)
    ↓
├─→ WorldService (In-Memory Game State)
│   └─ Sessions, Players, Enemies, State Versioning
│
├─→ PlayerService (Database Operations)
│   └─ SQLite: Player Profiles, Stats, Inventory
│
├─→ GameLoopService (Background Processing)
│   └─ 20Hz Game Loop, Input Processing
│
└─→ GameConfigService (Configuration)
    └─ Load game-config.json, Provide Config
```

### 3 Cơ Chế Cốt Lõi

1. **Session:** Quản lý game state tập trung
2. **Polling:** Đồng bộ state từ server xuống client
3. **Server Authoritative:** Đảm bảo tính nhất quán và công bằng

### Design Principles

- **Thin Client:** Client chỉ xử lý input, rendering, API communication
- **Authoritative Server:** Server xử lý logic, validation, state management
- **REST API:** Đơn giản, dễ debug, phù hợp dự án học thuật
- **Polling:** Thay vì WebSocket (đơn giản hơn)
- **SQLite:** File-based database (phù hợp small-scale)

---

**Tổng kết:** Server là authoritative backend, xử lý authentication, session management, game logic, state synchronization, và persistence cho multiplayer game.

---

*Document này được tổng hợp từ các câu hỏi và câu trả lời trong quá trình phát triển dự án.*




