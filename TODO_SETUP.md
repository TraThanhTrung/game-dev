# TODO: Setup và Testing Guide

## ✅ Đã Hoàn Thành (Code Implementation)

- ✅ Database schema changes (SectionId FK to Checkpoint)
- ✅ CheckpointService với section-based loading
- ✅ Enemy Config database-first với fallback
- ✅ Admin UI cho GameSection và Checkpoint management
- ✅ WorldService sử dụng GameSection
- ✅ Migration file đã được tạo: `AddGameSectionToCheckpoint`
- ✅ Unity EnemySpawner script đã tồn tại
- ✅ Unity Resources folder với enemy prefabs đã có

## 📋 TODO List - Các Bước Cần Thực Hiện

### Bước 1: Apply Database Migration

**Lưu ý:** Phải dừng server trước khi chạy migration!

```bash
# Dừng ASP.NET Core server nếu đang chạy
# Sau đó chạy:

cd server
dotnet ef database update

# Nếu migration đã apply rồi, sẽ hiện: "No migrations were applied."
# Nếu chưa apply, sẽ thấy: "Applying migration '20260110132435_AddGameSectionToCheckpoint'"
```

**Kiểm tra:** Migration đã được apply nếu:

- Checkpoints table có column `SectionId` (nullable INTEGER)
- Có index `IX_Checkpoints_SectionId`
- Có foreign key `FK_Checkpoints_GameSections_SectionId`

### Bước 2: Tạo Dữ Liệu Mẫu qua Admin Panel

#### 2.1. Tạo GameSection

1. **Truy cập:** `http://localhost:5000/Admin/GameSections/Create`
2. **Điền form:**
   - **Name:** `RPG Scene` (hoặc tên scene của bạn)
   - **Description:** `Main game scene with checkpoints` (tùy chọn)
   - **IsActive:** ✓ (checked)
   - **EnemyTypeId, EnemyCount, EnemyLevel, SpawnRate, Duration:** Có thể để mặc định hoặc bỏ trống (những field này là legacy, không dùng cho checkpoint system mới)
3. **Click "Create"**
4. **Lưu SectionId** (sẽ cần khi tạo checkpoints)

#### 2.2. Tạo Checkpoints

1. **Truy cập:** `http://localhost:5000/Admin/Checkpoints/Create`
2. **Điền form cho mỗi checkpoint:**
   - **GameSection:** Chọn GameSection vừa tạo (required)
   - **Checkpoint Name:** `checkpoint_1`, `checkpoint_2`, ... (unique)
   - **Position X, Y:** Lấy từ Unity scene (CheckPoints GameObject positions)
     - Ví dụ: `5.785`, `4.400`
   - **Enemy Pool:** JSON array, ví dụ: `["slime", "gnome"]` hoặc `["slime", "slime", "gnoll"]`
   - **Max Enemies:** `2` (số lượng enemies spawn tại checkpoint này)
   - **IsActive:** ✓ (checked)
3. **Click "Create"**
4. **Lặp lại** cho tất cả checkpoints trong scene

**Lưu ý:** Enemy Pool phải match với TypeId trong database (case-sensitive):

- `"slime"` ✅
- `"gnome"` ✅
- `"gnoll"` ✅
- `"bear"` ✅
- `"fish"` ✅

#### 2.3. Verify Enemy Configs trong Database

1. **Truy cập:** `http://localhost:5000/Admin/Enemies`
2. **Kiểm tra:** Tất cả enemy types được dùng trong checkpoints phải tồn tại:
   - `slime`
   - `gnome`
   - `gnoll`
   - `bear`
   - `fish`
3. **Nếu thiếu:** Tạo enemy với `TypeId` khớp với tên trong EnemyPool JSON

**Ví dụ Enemy Config cần có:**

- TypeId: `slime` (khớp với `"slime"` trong JSON)
- MaxHealth: `100`
- Damage: `10`
- Speed: `2.0`
- ... (các field khác)

#### 2.4. Update Existing Checkpoints (Nếu có)

Nếu bạn đã có checkpoints từ trước (trước khi migration), chúng sẽ có `SectionId = NULL`:

1. **Truy cập:** `http://localhost:5000/Admin/Checkpoints`
2. **Filter:** Bỏ filter để xem tất cả checkpoints
3. **Edit** mỗi checkpoint và assign vào một GameSection

### Bước 3: Verify Unity Setup

#### 3.1. Kiểm tra EnemySpawner Component

1. **Mở Unity Editor**
2. **Mở scene:** `Assets/Scenes/RPG.unity`
3. **Tìm GameObject:** Có `EnemySpawner` component
   - Nếu chưa có: Tạo empty GameObject, add `EnemySpawner` component
4. **Verify:** `ServerStateApplier` component cũng có trong scene và có reference đến `EnemySpawner`

#### 3.2. Kiểm tra Resources Folder

1. **Verify folder structure:**
   ```
   Assets/Resources/Prefabs/Enemies/
     ├── slime.prefab ✅
     ├── gnome.prefab ✅
     ├── gnoll.prefab ✅
     ├── bear.prefab ✅
     └── fish.prefab ✅
   ```
2. **Lưu ý:** Prefab names phải match với TypeId trong database (case-sensitive)

#### 3.3. Xóa Pre-placed Enemies (Optional)

1. **Tìm tất cả enemy GameObjects** trong scene được place sẵn
2. **Delete hoặc Deactivate** chúng (enemies sẽ spawn từ server)
3. **Giữ lại:** Checkpoint markers (nếu có) để reference

### Bước 4: Test Server Startup

```bash
cd server
dotnet run
```

**Kiểm tra logs:**

- ✅ Không có errors về database schema
- ✅ Không có errors về missing services
- ✅ Server starts successfully
- ⚠️ Nếu có warnings về fallback to game-config.json: OK (chỉ warning, không phải error)

### Bước 5: Test Admin Panel

1. **GameSections List:** `/Admin/GameSections`

   - ✅ Hiển thị checkpoint counts
   - ✅ Click "X Checkpoints" button → filter checkpoints by section
   - ✅ Click "Manage Checkpoints" → filtered checkpoints page

2. **Checkpoints List:** `/Admin/Checkpoints`

   - ✅ Hiển thị Section column
   - ✅ Filter dropdown hoạt động
   - ✅ Click section name → link đến GameSection details

3. **Create/Edit Checkpoint:**

   - ✅ GameSection dropdown hiển thị các active sections
   - ✅ Validation: SectionId là required
   - ✅ Save thành công

4. **GameSection Details:** `/Admin/GameSections/Details/{id}`
   - ✅ Hiển thị list checkpoints thuộc section
   - ✅ "Add Checkpoint" button pre-selects section
   - ✅ "Manage All Checkpoints" link filters by section

### Bước 6: Test Enemy Spawning (Unity)

1. **Start Server:** `dotnet run` (trong server folder)
2. **Start Unity Game:** Press Play trong Unity Editor
3. **Login/Create Account** (nếu cần)
4. **Create Room hoặc Join Room**
5. **Kiểm tra Console logs:**

   - Unity: `[EnemySpawner] Spawned enemy...`
   - Server: `Initializing room {SessionId} with seed {Seed} and {CheckpointCount} checkpoints`

6. **Verify trong Scene:**
   - ✅ Enemies spawn tại đúng vị trí checkpoints (X, Y coordinates)
   - ✅ Enemy types match với EnemyPool
   - ✅ Số lượng enemies <= MaxEnemies per checkpoint

### Bước 7: Test Multiplayer Sync

1. **Build Unity Game** hoặc open 2 Unity Editor instances
2. **Client 1:** Join room `test-room-1`
3. **Client 2:** Join cùng room `test-room-1`
4. **Verify:**
   - ✅ Cả 2 clients thấy cùng enemies
   - ✅ Cùng vị trí (X, Y)
   - ✅ Cùng enemy types
   - ✅ Deterministic spawning: Same SessionId = Same seed = Same enemies

### Bước 8: Test Enemy Kill

1. **Kill enemy** trên Client 1
2. **Verify:**
   - ✅ Enemy biến mất trên Client 1
   - ✅ Enemy biến mất trên Client 2 (polling nhận state update)
   - ✅ Server logs: `ReportKill: Enemy {EnemyId} killed by player {PlayerId}`

### Bước 9: Test Fallback (Optional)

1. **Tạo checkpoint** với EnemyPool chứa enemy type không có trong database:
   - Ví dụ: `["unknown_enemy"]`
2. **Start room** với checkpoint này
3. **Verify Server logs:**
   - ⚠️ Warning: `Enemy type unknown_enemy not found in database, falling back to game-config.json`
   - ✅ Nếu `unknown_enemy` có trong `game-config.json`: Enemy vẫn spawn
   - ❌ Nếu không có trong cả 2: Enemy bị skip, log warning

## 🔍 Troubleshooting

### Migration không apply được

**Error:** `The database is locked`

**Giải pháp:**

- Dừng server hoàn toàn (Ctrl+C, kill process)
- Đảm bảo không có SQLite browser mở database file
- Chạy lại `dotnet ef database update`

### Enemies không spawn

**Kiểm tra:**

1. ✅ Checkpoint đã được tạo trong database và có `SectionId` assigned
2. ✅ GameSection có `IsActive = true`
3. ✅ Checkpoint có `IsActive = true`
4. ✅ EnemyPool JSON đúng format: `["slime", "gnome"]`
5. ✅ Enemy types trong EnemyPool có trong database với đúng TypeId
6. ✅ Unity EnemySpawner component exists trong scene
7. ✅ Server logs show: `InitializeRoomCheckpoints` được gọi
8. ✅ Unity Console logs show: `OnStateReceived` nhận được enemies từ server

### Enemies spawn sai vị trí

**Kiểm tra:**

1. ✅ Checkpoint X, Y coordinates trong database match với Unity scene coordinates
2. ✅ Unity coordinate system (2D: X, Y) match với server coordinates
3. ✅ Check CheckPoints GameObject positions trong Unity scene

### Admin Panel không load được

**Kiểm tra:**

1. ✅ Server đang chạy
2. ✅ Đã login (Admin Panel requires authentication)
3. ✅ Browser console không có JavaScript errors
4. ✅ Network tab: API calls return 200 OK

### Prefab không load được trong Unity

**Error:** `[EnemySpawner] Failed to load prefab: slime`

**Giải pháp:**

1. ✅ Check prefab có trong `Assets/Resources/Prefabs/Enemies/`
2. ✅ Tên prefab match với TypeId (case-sensitive): `slime.prefab` ↔ `"slime"`
3. ✅ Resources folder structure đúng: `Resources/Prefabs/Enemies/`
4. ✅ Unity đã import prefabs (có .meta files)

## ✅ Completion Checklist

Khi tất cả các bước trên hoàn thành, bạn sẽ có:

- ✅ Database schema updated với SectionId relationship
- ✅ GameSection và Checkpoints được quản lý qua Admin Panel
- ✅ Enemies spawn deterministically từ server
- ✅ Multiplayer sync hoạt động (same room = same enemies)
- ✅ Enemy kill được sync giữa clients
- ✅ Fallback to game-config.json hoạt động khi enemy không có trong database

## 📝 Notes

- **Backward Compatibility:** Existing checkpoints với `SectionId = NULL` vẫn hoạt động (fallback to all active checkpoints)
- **game-config.json:** Giữ làm fallback, không remove (emergency fallback)
- **Deterministic Spawning:** Same SessionId = same seed = same enemies (important for multiplayer sync)
- **SectionId nullable:** Cho phép orphaned checkpoints (backward compatibility)
