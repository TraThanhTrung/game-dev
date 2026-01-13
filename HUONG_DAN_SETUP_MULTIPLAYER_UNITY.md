# Hướng Dẫn Setup Unity Scene Cho Multiplayer

## Vấn Đề Hiện Tại

- Scene đã có sẵn 1 Player object
- Khi 2 người chơi, mỗi client chỉ thấy 1 player (không hiển thị remote player)
- Nguyên nhân: Scene chưa được setup đúng cho multiplayer

## Giải Pháp

### Bước 1: Kiểm Tra Scene Hiện Tại

1. Mở scene game (ví dụ: `RPG2.unity`)
2. Kiểm tra xem có các GameObject sau không:
   - `Player` (object có sẵn trong scene)
   - `RemotePlayerManager` (quản lý remote players)
   - `PlayerSpawner` (spawn local player)
   - `ServerStateApplier` (sync state từ server)

### Bước 2: Setup RemotePlayerManager

**2.1. Tạo RemotePlayerManager GameObject**

1. Trong Hierarchy, tạo GameObject mới: `GameObject > Create Empty`
2. Đặt tên: `RemotePlayerManager`
3. Add Component: `RemotePlayerManager` script

**2.2. Tạo Remote Player Prefabs**

Bạn cần tạo 2 prefabs cho remote players (khác với local player):

1. **Tạo RemotePlayer_Lancer Prefab:**

   - Duplicate Player prefab hoặc tạo mới từ Lancer prefab
   - Đặt tên: `RemotePlayer_Lancer`
   - Remove các components không cần cho remote player:
     - `PlayerMovement` (remote players không nhận input)
     - `Player_Combat` (combat được xử lý bởi server)
     - `ClientPredictor` (chỉ local player cần - sẽ tự động add nếu thiếu, nhưng nên có trong prefab)
     - `ServerStateApplier` (chỉ local player cần)
   - Add Component: `RemotePlayerSync` (quan trọng!)
   - Set Tag: `RemotePlayer` (khác với `Player`)
   - Save as Prefab

2. **Tạo RemotePlayer_Warrious Prefab:**
   - Tương tự như trên, nhưng dùng Warrious prefab
   - Đặt tên: `RemotePlayer_Warrious`
   - Add `RemotePlayerSync` component
   - Set Tag: `RemotePlayer`
   - Save as Prefab

**2.3. Assign Prefabs vào RemotePlayerManager**

1. Select `RemotePlayerManager` GameObject
2. Trong Inspector, tìm `RemotePlayerManager` component
3. Assign các prefabs:
   - `Remote Player Lancer Prefab` → `RemotePlayer_Lancer` prefab
   - `Remote Player Warrious Prefab` → `RemotePlayer_Warrious` prefab
   - `Default Remote Player Prefab` → `RemotePlayer_Lancer` (fallback)

**2.4. Tạo Container cho Remote Players (Optional)**

1. Tạo GameObject mới: `GameObject > Create Empty`
2. Đặt tên: `RemotePlayers` (hoặc để trống, script sẽ tự tạo)
3. Assign vào `Remote Player Container` field trong RemotePlayerManager

### Bước 3: Setup PlayerSpawner (Nếu Chưa Có)

**3.1. Tạo PlayerSpawner GameObject**

1. Tạo GameObject mới: `GameObject > Create Empty`
2. Đặt tên: `PlayerSpawner`
3. Add Component: `PlayerSpawner` script

**3.2. Assign Prefabs vào PlayerSpawner**

1. Select `PlayerSpawner` GameObject
2. Trong Inspector:
   - `Lancer Prefab` → Lancer prefab của bạn
   - `Warrious Prefab` → Warrious prefab của bạn
   - `Spawn Position` → Vị trí spawn (ví dụ: -16, 12)
   - `Replace Existing Player` → `true` (nếu muốn replace player có sẵn)

### Bước 4: Setup Local Player Object

**Mục đích:** Đảm bảo local player prefab có đầy đủ components cần thiết cho multiplayer.

**4.1. Kiểm Tra Player.lancer.prefab**

Mở prefab `Player.lancer.prefab` và kiểm tra các components sau:

**Components BẮT BUỘC:**

1. ✅ **Transform** - Component cơ bản
2. ✅ **SpriteRenderer** - Hiển thị sprite
3. ✅ **Animator** - Animation controller
4. ✅ **CapsuleCollider2D** - Collision detection
5. ✅ **Rigidbody2D** - Physics body
6. ✅ **PlayerMovement** - Xử lý input và di chuyển
7. ✅ **Player_Combat** - Xử lý combat
8. ✅ **Enemy_Health** - Quản lý HP (dùng chung với enemy)
9. ✅ **ServerStateApplier** - **QUAN TRỌNG!** Sync state từ server

**Components KHUYẾN NGHỊ (sẽ tự động add nếu thiếu, nhưng nên có trong prefab):** 10. ⚠️ **ClientPredictor** - **KHUYẾN NGHỊ!** Client-side prediction cho local player - Nếu không có: `PlayerSpawner` hoặc `PlayerMovement` sẽ tự động add - Nên có trong prefab để tránh delay khi spawn và đảm bảo responsive gameplay ngay từ đầu

**Components TÙY CHỌN:**

- `Player_Weapon` - Quản lý weapon
- `Player_Bow` - Quản lý bow (có thể disabled nếu không dùng)

**Tag và Settings:**

- ✅ Tag: `Player` (không phải `RemotePlayer`)
- ✅ Layer: Phù hợp với game (thường là Layer 8)

**4.2. Kiểm Tra ServerStateApplier Settings**

Trong Inspector của `ServerStateApplier` component:

- ✅ `Auto Poll` = `true` (tự động poll state từ server)
- ✅ `Enable Logging` = `true` (để debug, có thể tắt sau)
- ✅ `Auto Save` = `true` (tự động save progress)
- ✅ `Lerp Speed` = 15 (tốc độ interpolation)
- ✅ `Poll Interval` = 0.2 (khoảng thời gian giữa các lần poll)

**4.3. Kiểm Tra ClientPredictor Settings**

Trong Inspector của `ClientPredictor` component:

- ✅ `Correction Threshold` = 0.5 (ngưỡng để correction)
- ✅ `Smooth Corrections` = `true` (smooth khi server correction)
- ✅ `Correction Speed` = 15 (tốc độ correction)

**4.4. Option A: Sử Dụng Player Object Có Sẵn Trong Scene**

Nếu scene đã có sẵn Player object:

1. Select `Player` object trong scene
2. Kiểm tra có đầy đủ components như trên
3. Đảm bảo Tag = `Player`
4. Set `PlayerSpawner.Replace Existing Player = false` nếu muốn giữ object này

**4.5. Option B: Spawn Player Mới Từ Prefab (Khuyến Nghị)**

Nếu muốn spawn player mới từ prefab:

1. Set `PlayerSpawner.Replace Existing Player = true`
2. Assign `Player.lancer.prefab` vào `Lancer Prefab` field
3. Assign `Player.warrious.prefab` vào `Warrious Prefab` field (nếu có)
4. Player sẽ được spawn mới khi join session
5. Player object có sẵn trong scene sẽ bị destroy và thay thế

**4.6. Lưu Ý Quan Trọng**

- **KHÔNG** có `RemotePlayerSync` trên local player (chỉ remote players mới có)
- **CÓ** `ClientPredictor` trên local player (chỉ local player mới có - sẽ tự động add nếu thiếu)
- `ServerStateApplier` tự động gọi `RemotePlayerManager.UpdateFromSnapshot()` để sync remote players

### Bước 5: Kiểm Tra ServerStateApplier

1. Select `Player` object (local player)
2. Đảm bảo có `ServerStateApplier` component
3. Trong Inspector, kiểm tra:
   - `Auto Poll` = `true` (tự động poll state từ server)
   - `Enable Logging` = `true` (để debug)
   - `Auto Save` = `true` (tự động save progress)

### Bước 6: Kiểm Tra NetClient

1. Đảm bảo có `NetClient` GameObject trong scene (hoặc được tạo từ Login scene)
2. `NetClient` phải được khởi tạo trước khi join session
3. Kiểm tra `NetClient.Instance` không null khi vào game scene

### Bước 7: Test Multiplayer

**7.1. Build và Test**

1. Build game thành 2 executables
2. Chạy cả 2 clients
3. Login và join cùng session
4. Kiểm tra Console logs:
   - `[RemotePlayerManager] UpdateFromSnapshot: 2 players`
   - `[RemotePlayerManager] Creating new remote player: {id} ({characterType})`
   - `[RemotePlayerManager] Created remote player: {id} ({characterType})`

**7.2. Kiểm Tra Hierarchy**

Trong Unity Editor (Play Mode) hoặc trong game:

- Phải có 1 `Player` object (local player)
- Phải có 1+ `RemotePlayer_*` objects (remote players)
- Tất cả trong `RemotePlayers` container

## Cấu Trúc Scene Đúng

```
Scene Hierarchy:
├── Player (Local Player)
│   ├── ServerStateApplier
│   ├── ClientPredictor
│   ├── PlayerMovement
│   └── Player_Combat
├── RemotePlayerManager
│   └── RemotePlayerManager (Component)
│       ├── Remote Player Lancer Prefab → RemotePlayer_Lancer
│       ├── Remote Player Warrious Prefab → RemotePlayer_Warrious
│       └── Remote Player Container → RemotePlayers (optional)
├── PlayerSpawner
│   └── PlayerSpawner (Component)
│       ├── Lancer Prefab → Lancer prefab
│       └── Warrious Prefab → Warrious prefab
├── RemotePlayers (Container - tự động tạo nếu không assign)
│   ├── RemotePlayer_{PlayerId1} (spawned khi có remote player)
│   └── RemotePlayer_{PlayerId2} (spawned khi có remote player)
└── NetClient (từ Login scene hoặc DontDestroyOnLoad)
```

## Troubleshooting

### Vấn Đề 1: Không Thấy Remote Players

**Nguyên nhân:**

- RemotePlayerManager chưa được setup
- Prefabs chưa được assign
- Local player ID chưa được set đúng

**Giải pháp:**

1. Kiểm tra Console logs xem có `[RemotePlayerManager]` logs không
2. Kiểm tra `RemotePlayerManager.Instance` không null
3. Kiểm tra prefabs đã được assign chưa
4. Enable `Enable Logging` trong RemotePlayerManager để debug

### Vấn Đề 2: Remote Players Không Di Chuyển

**Nguyên nhân:**

- `RemotePlayerSync` component chưa được add
- `UpdateState` không được gọi

**Giải pháp:**

1. Đảm bảo remote player prefabs có `RemotePlayerSync` component
2. Kiểm tra `ServerStateApplier` có gọi `RemotePlayerManager.UpdateFromSnapshot()` không

### Vấn Đề 3: Cả 2 Clients Thấy Cùng 1 Player

**Nguyên nhân:**

- Local player ID chưa được set
- RemotePlayerManager skip local player không đúng

**Giải pháp:**

1. Kiểm tra `RemotePlayerManager.SetLocalPlayerId()` được gọi
2. Kiểm tra `ServerStateApplier.InitializeRemotePlayerManager()` được gọi
3. Kiểm tra logs: `[RemotePlayerManager] Skipping local player: {id}`

### Vấn Đề 4: Remote Players Spawn Sai Character Type

**Nguyên nhân:**

- Server chưa gửi `characterType` trong `PlayerSnapshot`
- Client dùng sai `characterType`

**Giải pháp:**

1. Đảm bảo server code đã được update (đã fix trong code)
2. Kiểm tra `PlayerSnapshot.characterType` có giá trị đúng không
3. Kiểm tra `ConvertToRemotePlayerSnapshots()` dùng `p.characterType` từ server

## Checklist Setup

- [ ] RemotePlayerManager GameObject được tạo
- [ ] RemotePlayerManager component được add
- [ ] Remote Player prefabs được tạo (Lancer, Warrious)
- [ ] Remote Player prefabs có `RemotePlayerSync` component
- [ ] Remote Player prefabs được assign vào RemotePlayerManager
- [ ] PlayerSpawner GameObject được tạo
- [ ] PlayerSpawner component được add
- [ ] Player prefabs được assign vào PlayerSpawner
- [ ] Local Player object có `ServerStateApplier` component
- [ ] NetClient được khởi tạo đúng cách
- [ ] Test với 2 clients và kiểm tra cả 2 players hiển thị

## Lưu Ý Quan Trọng

1. **Local Player vs Remote Players:**

   - Local player: Tag = `Player`, có `ClientPredictor`, nhận input
   - Remote players: Tag = `RemotePlayer`, có `RemotePlayerSync`, không nhận input

2. **Prefabs:**

   - Local player prefabs: Dùng cho PlayerSpawner
   - Remote player prefabs: Dùng cho RemotePlayerManager (có thể khác local player prefabs)

3. **Character Type:**

   - Server gửi `characterType` trong `PlayerSnapshot`
   - Client dùng `characterType` từ server để spawn đúng prefab

4. **Container:**
   - Remote players được spawn vào `RemotePlayers` container
   - Container tự động tạo nếu không assign

## Kết Luận

Sau khi setup đúng, khi 2 người chơi join cùng session:

- Client 1: Thấy 1 local player (mình) + 1 remote player (người kia)
- Client 2: Thấy 1 local player (mình) + 1 remote player (người kia)
- Tổng cộng: 2 players hiển thị trên mỗi client
