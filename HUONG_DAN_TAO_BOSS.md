# Hướng Dẫn Tạo Boss

## Tổng Quan

Hệ thống tạo boss có 3 cách, được ưu tiên theo thứ tự:

1. **GameSection.EnemyTypeId** (Ưu tiên cao nhất)
2. **Quy ước `boss_{enemyType}`** (Ưu tiên thứ 2)
3. **Tự động tạo từ enemy thường** (Fallback)

## Cách 1: Tạo Boss Config Trong Database (Khuyến Nghị)

### Bước 1: Tạo Boss Config trong bảng `Enemies`

Chạy SQL để tạo boss config:

```sql
INSERT INTO Enemies (TypeId, MaxHealth, Damage, Speed, DetectRange, AttackRange, AttackCooldown, RespawnDelay, ExpReward, GoldReward)
VALUES ('boss_slime', 300, 20, 1.6, 8, 1.5, 2.0, 999999, 250, 100);
```

**Giải thích các trường:**
- `TypeId`: Tên boss (phải bắt đầu với `boss_` hoặc tên tùy chỉnh)
- `MaxHealth`: HP tối đa của boss
- `Damage`: Sát thương của boss
- `Speed`: Tốc độ di chuyển
- `DetectRange`: Tầm phát hiện player
- `AttackRange`: Tầm tấn công
- `AttackCooldown`: Thời gian chờ giữa các đòn tấn công
- `RespawnDelay`: **Phải là 999999** (boss không respawn)
- `ExpReward`: EXP thưởng khi đánh bại
- `GoldReward`: Gold thưởng khi đánh bại

### Bước 2: Đặt EnemyTypeId trong GameSection

Cập nhật `GameSection` để chỉ định boss:

```sql
UPDATE GameSections 
SET EnemyTypeId = 'boss_slime' 
WHERE SectionId = 5;
```

**Lưu ý:**
- Nếu `EnemyTypeId = "-"` hoặc `NULL`, section sẽ **KHÔNG có boss**
- Section không có boss sẽ chuyển section khi đánh hết tất cả enemies thường

### Ví Dụ Hoàn Chỉnh

```sql
-- 1. Tạo boss config
INSERT OR IGNORE INTO Enemies (TypeId, MaxHealth, Damage, Speed, DetectRange, AttackRange, AttackCooldown, RespawnDelay, ExpReward, GoldReward)
VALUES 
    ('boss_slime', 300, 20, 1.6, 8, 1.5, 2.0, 999999, 250, 100),
    ('boss_goblin', 400, 30, 2.0, 10, 2.0, 1.5, 999999, 350, 150);

-- 2. Cập nhật GameSection để dùng boss
UPDATE GameSections 
SET EnemyTypeId = 'boss_slime' 
WHERE SectionId = 5 AND Name = 'Final';
```

## Cách 2: Dùng Quy Ước `boss_{enemyType}`

Nếu không đặt `EnemyTypeId` trong `GameSection`, server sẽ tự động tìm boss theo quy ước:

### Quy Ước

- Boss typeId = `boss_{firstEnemyType}`
- `firstEnemyType` = enemy type đầu tiên trong enemy pool của checkpoint đầu tiên

### Ví Dụ

Nếu checkpoint đầu tiên có `EnemyPool = '["slime", "goblin"]'`:
- Server sẽ tìm boss với `TypeId = 'boss_slime'`
- Nếu tìm thấy trong database → dùng config đó
- Nếu không tìm thấy → tự động tạo từ `slime` enemy với stats nâng cao

### Cách Tạo

```sql
-- Tạo boss theo quy ước
INSERT INTO Enemies (TypeId, MaxHealth, Damage, Speed, DetectRange, AttackRange, AttackCooldown, RespawnDelay, ExpReward, GoldReward)
VALUES ('boss_slime', 300, 20, 1.6, 8, 1.5, 2.0, 999999, 250, 100);
```

**Lưu ý:** Không cần đặt `EnemyTypeId` trong `GameSection`, server sẽ tự động tìm `boss_slime`.

## Cách 3: Tự Động Tạo (Fallback)

Nếu không tìm thấy boss config trong database, server sẽ **tự động tạo** từ enemy thường đầu tiên với công thức:

### Công Thức Tự Động

- **HP**: 3x base enemy HP
- **Damage**: 2x base enemy damage
- **Speed**: 0.8x base enemy speed (chậm hơn)
- **EXP Reward**: 5x base enemy EXP
- **Gold Reward**: 5x base enemy Gold
- **RespawnDelay**: `float.MaxValue` (không respawn)

### Ví Dụ

Nếu enemy `slime` có:
- HP: 100
- Damage: 10
- Speed: 2.0
- EXP: 25
- Gold: 10

Thì boss tự động tạo sẽ có:
- HP: 300 (3x)
- Damage: 20 (2x)
- Speed: 1.6 (0.8x)
- EXP: 125 (5x)
- Gold: 50 (5x)

## Vị Trí Spawn Boss

Boss luôn được spawn tại **checkpoint cuối cùng** (CheckpointId cao nhất) của section.

### Ví Dụ

```sql
-- Section 1 có 3 checkpoints
INSERT INTO Checkpoints (CheckpointName, SectionId, X, Y, EnemyPool, MaxEnemies, IsActive)
VALUES 
    ('Entry Point', 1, -16, 12, '["slime"]', 3, 1),      -- Checkpoint 1
    ('Middle Area', 1, 0, 0, '["goblin"]', 2, 1),        -- Checkpoint 2
    ('Boss Arena', 1, 16, -12, '["slime"]', 1, 1);       -- Checkpoint 3 (BOSS SPAWN Ở ĐÂY)
```

## Level Scaling

Boss stats sẽ được scale theo `GameSection.EnemyLevel`:

### Công Thức

```
multiplier = 1 + (EnemyLevel - 1) * 0.1
```

### Ví Dụ

- **Level 1**: 100% stats (không scale)
- **Level 2**: 110% stats (+10%)
- **Level 3**: 120% stats (+20%)
- **Level 5**: 140% stats (+40%)

**Lưu ý:** Chỉ scale `MaxHp`, `Hp`, `Damage`. `Speed` và ranges không scale.

## Checklist Tạo Boss

- [ ] Tạo boss config trong bảng `Enemies` với `TypeId = 'boss_xxx'`
- [ ] Đặt `RespawnDelay = 999999` (boss không respawn)
- [ ] Đặt `EnemyTypeId` trong `GameSection` (nếu muốn chỉ định cụ thể)
- [ ] Đảm bảo checkpoint cuối cùng có `EnemyPool` không rỗng
- [ ] Kiểm tra `GameSection.EnemyLevel` để tính toán stats scaling

## Ví Dụ Hoàn Chỉnh

```sql
-- ============================================
-- Tạo Boss Config
-- ============================================
INSERT OR IGNORE INTO Enemies (TypeId, MaxHealth, Damage, Speed, DetectRange, AttackRange, AttackCooldown, RespawnDelay, ExpReward, GoldReward)
VALUES ('boss_slime', 300, 20, 1.6, 8, 1.5, 2.0, 999999, 250, 100);

-- ============================================
-- Tạo Section với Boss
-- ============================================
INSERT INTO GameSections (Name, Description, EnemyTypeId, EnemyCount, EnemyLevel, SpawnRate, Duration, IsActive)
VALUES ('Final', 'Section cuối cùng với boss mạnh nhất', 'boss_slime', 3, 3, 1.5, NULL, 1);

-- ============================================
-- Tạo Checkpoints (Boss spawn tại checkpoint cuối)
-- ============================================
INSERT INTO Checkpoints (CheckpointName, SectionId, X, Y, EnemyPool, MaxEnemies, IsActive)
VALUES 
    ('Arena', 5, 0, 0, '["skeleton"]', 2, 1),
    ('Boss Throne', 5, 20, 0, '["slime"]', 1, 1);  -- Boss spawn ở đây
```

## Troubleshooting

### Boss không spawn?

1. Kiểm tra `GameSection.EnemyTypeId`:
   - Nếu `NULL` hoặc `"-"` → Section không có boss
   - Phải có giá trị hợp lệ (ví dụ: `'boss_slime'`)

2. Kiểm tra boss config trong database:
   ```sql
   SELECT * FROM Enemies WHERE TypeId = 'boss_slime';
   ```

3. Kiểm tra checkpoint cuối cùng:
   ```sql
   SELECT * FROM Checkpoints 
   WHERE SectionId = 5 
   ORDER BY CheckpointId DESC 
   LIMIT 1;
   ```

### Boss stats không đúng?

1. Kiểm tra `GameSection.EnemyLevel` (ảnh hưởng đến scaling)
2. Kiểm tra boss config trong database
3. Xem logs server để biết boss được tạo như thế nào

### Section không chuyển sau khi đánh bại boss?

1. Kiểm tra `CurrentBossId` trong session state
2. Kiểm tra logs: `"Boss {BossId} defeated"`
3. Đảm bảo `IsBossAlive = false` sau khi boss chết


