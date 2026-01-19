# Thông Số Chuẩn Khi Tạo Enemy

Tài liệu này liệt kê các thông số chuẩn để tạo enemy trong game Top-down 2D Multiplayer.

## 📋 Danh Sách Thông Số Bắt Buộc

### 1. **Thông Tin Cơ Bản**
```csharp
TypeId: string          // ID duy nhất của enemy (ví dụ: "slime", "gnome", "boss_fish")
Name: string            // Tên hiển thị của enemy (ví dụ: "Slime", "Gnome")
IsActive: bool          // Trạng thái hoạt động (mặc định: true)
```

### 2. **Phần Thưởng (Rewards)**
```csharp
ExpReward: int          // EXP nhận được khi tiêu diệt enemy
GoldReward: int         // Vàng nhận được khi tiêu diệt enemy
```

### 3. **Sinh Mạng (Health)**
```csharp
MaxHealth: int          // Máu tối đa của enemy
```

### 4. **Tấn Công (Combat)**
```csharp
Damage: int             // Sát thương gây ra mỗi đòn đánh
WeaponRange: float      // Tầm đánh của vũ khí (đơn vị: Unity units)
AttackRange: float      // Khoảng cách tối thiểu để bắt đầu tấn công
AttackCooldown: float   // Thời gian chờ giữa các lần tấn công (giây)
KnockbackForce: float   // Lực đẩy lùi khi đánh trúng player
StunTime: float         // Thời gian làm choáng player (giây)
```

### 5. **Di Chuyển (Movement)**
```csharp
Speed: float            // Tốc độ di chuyển của enemy (đơn vị: Unity units/giây)
DetectRange: float      // Tầm phát hiện player (đơn vị: Unity units)
```

### 6. **Hồi Sinh (Respawn)**
```csharp
RespawnDelay: float     // Thời gian chờ trước khi enemy hồi sinh (giây)
                        // Boss thường dùng: 999999 (không hồi sinh)
```

## 📊 Giá Trị Chuẩn Theo Loại Enemy

### 🔵 Enemy Cơ Bản (Basic Enemy) - Ví dụ: Slime
```sql
TypeId: 'slime'
Name: 'Slime'
ExpReward: 1
GoldReward: 5
MaxHealth: 3
Damage: 1
Speed: 2.0
DetectRange: 6.0
AttackRange: 1.2
AttackCooldown: 3.0
WeaponRange: 1.2
KnockbackForce: 2.8
StunTime: 0.3
RespawnDelay: 10
```

### 🟢 Enemy Trung Bình (Medium Enemy) - Ví dụ: Gnome
```sql
TypeId: 'gnome'
Name: 'Gnome'
ExpReward: 2
GoldReward: 8
MaxHealth: 4
Damage: 2
Speed: 3.0
DetectRange: 4.0
AttackRange: 1.0
AttackCooldown: 2.0
WeaponRange: 1.2
KnockbackForce: 3.0
StunTime: 0.3
RespawnDelay: 10
```

### 🔴 Boss Enemy - Ví dụ: Boss Fish
```sql
TypeId: 'boss_fish'
Name: 'fish'
ExpReward: 10
GoldReward: 20
MaxHealth: 10
Damage: 2
Speed: 3.0
DetectRange: 7.0
AttackRange: 2.0
AttackCooldown: 1.0
WeaponRange: 2.0
KnockbackForce: 2.0
StunTime: 1.0
RespawnDelay: 999999  -- Boss không hồi sinh
```

## 📐 Khoảng Giá Trị Đề Xuất

### ExpReward
- **Weak Enemy**: 1-2 EXP
- **Normal Enemy**: 3-5 EXP
- **Strong Enemy**: 6-10 EXP
- **Boss**: 10-50 EXP

### GoldReward
- **Weak Enemy**: 5-10 Gold
- **Normal Enemy**: 10-20 Gold
- **Strong Enemy**: 20-30 Gold
- **Boss**: 30-100 Gold

### MaxHealth
- **Weak Enemy**: 3-5 HP
- **Normal Enemy**: 6-10 HP
- **Strong Enemy**: 11-20 HP
- **Boss**: 20-50 HP

### Damage
- **Weak Enemy**: 1-2 DMG
- **Normal Enemy**: 2-3 DMG
- **Strong Enemy**: 3-5 DMG
- **Boss**: 5-10 DMG

### Speed
- **Slow Enemy**: 1.5-2.0
- **Normal Enemy**: 2.0-3.0
- **Fast Enemy**: 3.0-4.0
- **Boss**: 2.5-3.5

### DetectRange
- **Short Range**: 4.0-5.0
- **Normal Range**: 6.0-7.0
- **Long Range**: 7.0-10.0
- **Boss**: 7.0-10.0

### AttackRange
- **Melee**: 1.0-1.5
- **Close Range**: 1.5-2.0
- **Medium Range**: 2.0-3.0
- **Boss**: 2.0-3.0

### AttackCooldown
- **Fast Attacker**: 1.0-2.0 giây
- **Normal**: 2.0-3.0 giây
- **Slow Attacker**: 3.0-5.0 giây
- **Boss**: 1.0-2.0 giây

### WeaponRange
- **Melee**: 1.0-1.5
- **Close Range**: 1.5-2.0
- **Medium Range**: 2.0-3.0
- **Boss**: 2.0-3.0

### KnockbackForce
- **Weak**: 2.0-3.0
- **Normal**: 3.0-5.0
- **Strong**: 5.0-8.0
- **Boss**: 2.0-4.0 (để không đẩy quá xa)

### StunTime
- **Weak**: 0.2-0.3 giây
- **Normal**: 0.3-0.5 giây
- **Strong**: 0.5-1.0 giây
- **Boss**: 0.5-1.5 giây

### RespawnDelay
- **Regular Enemy**: 5-15 giây
- **Elite Enemy**: 15-30 giây
- **Boss**: 999999 (không hồi sinh)

## 🎯 Công Thức Cân Bằng

### Tỷ Lệ EXP/Gold
- **Weak**: EXP:Gold ≈ 1:5 (ví dụ: 1 EXP / 5 Gold)
- **Normal**: EXP:Gold ≈ 1:4 (ví dụ: 5 EXP / 20 Gold)
- **Boss**: EXP:Gold ≈ 1:2 (ví dụ: 10 EXP / 20 Gold)

### Mối Quan Hệ HP/Damage
- **Weak**: HP:DMG ≈ 3:1 (ví dụ: 3 HP / 1 DMG)
- **Normal**: HP:DMG ≈ 2:1 (ví dụ: 6 HP / 3 DMG)
- **Boss**: HP:DMG ≈ 5:1 (ví dụ: 20 HP / 4 DMG)

### Tốc Độ vs Tầm Phát Hiện
- **Tank** (chậm, tầm ngắn): Speed ≤ 2.0, DetectRange ≤ 5.0
- **Balanced** (vừa phải): Speed 2.0-3.0, DetectRange 6.0-7.0
- **Aggressive** (nhanh, tầm dài): Speed ≥ 3.0, DetectRange ≥ 7.0

## 💡 Gợi Ý Thiết Kế

### Weak Enemy (Kẻ Yếu)
- Dễ tiêu diệt, phần thưởng thấp
- Sử dụng làm enemy thường xuyên xuất hiện
- **Ví dụ**: Slime, Rat, Goblin

### Normal Enemy (Kẻ Bình Thường)
- Cân bằng giữa độ khó và phần thưởng
- Là enemy chính trong game
- **Ví dụ**: Gnome, Orc, Skeleton

### Strong Enemy (Kẻ Mạnh)
- Khó tiêu diệt, phần thưởng cao
- Xuất hiện ít hơn, có thể là mini-boss
- **Ví dụ**: Elite Orc, Dark Knight

### Boss Enemy
- Rất khó, phần thưởng rất cao
- Chỉ xuất hiện 1 lần, không hồi sinh
- Có thể có tầm đánh và phát hiện lớn hơn
- **Ví dụ**: Boss Fish, Boss Troll, Boss Minotaur

## 📝 Lưu Ý Kỹ Thuật

1. **TypeId phải duy nhất**: Không được trùng với enemy khác
2. **Tất cả giá trị phải ≥ 0**: Không được âm
3. **RespawnDelay**: Boss nên dùng 999999 để không hồi sinh
4. **AttackRange ≤ DetectRange**: Tầm tấn công không được lớn hơn tầm phát hiện
5. **WeaponRange ≈ AttackRange**: Thường bằng nhau hoặc WeaponRange lớn hơn một chút
6. **Speed**: Giá trị hợp lý từ 1.0 đến 5.0 (Unity units/giây)
7. **Ranges**: Giá trị hợp lý từ 1.0 đến 10.0 (Unity units)

## 🔄 Ví Dụ SQL INSERT

```sql
INSERT INTO Enemies
    (TypeId, Name, ExpReward, GoldReward, MaxHealth, Damage, Speed, 
     DetectRange, AttackRange, AttackCooldown, WeaponRange, 
     KnockbackForce, StunTime, RespawnDelay, IsActive, CreatedAt)
VALUES
    ('new_enemy', 'New Enemy Name', 
     3,                    -- ExpReward
     15,                   -- GoldReward
     6,                    -- MaxHealth
     2,                    -- Damage
     2.5,                  -- Speed
     6.0,                  -- DetectRange
     1.2,                  -- AttackRange
     2.5,                  -- AttackCooldown
     1.2,                  -- WeaponRange
     3.5,                  -- KnockbackForce
     0.3,                  -- StunTime
     10,                   -- RespawnDelay
     1,                    -- IsActive
     GETUTCDATE()          -- CreatedAt
    );
```

