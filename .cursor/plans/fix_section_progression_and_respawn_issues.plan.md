# Kế Hoạch Sửa Chữa Vấn Đề Section Progression và Respawn

## Tổng Quan

Có 2 vấn đề chính cần sửa:

1. **Quái sinh ra liên tục không dừng**: Respawn không dừng khi section duration hết hoặc đạt capacity
2. **Section không chuyển khi đánh hết quái**: Section 1 không có boss (EnemyTypeId = "-"), nên logic chỉ dựa trên boss defeat không hoạt động

## Phân Tích Vấn Đề

### Vấn Đề 1: Respawn Liên Tục

**Nguyên nhân có thể:**

- Logic kiểm tra section duration có thể không hoạt động đúng
- Logic kiểm tra capacity (checkpoint MaxEnemies, section EnemyCount) có thể không chính xác
- Duration có thể không được lưu đúng vào CachedSectionConfig

**Code hiện tại:**

````628:637:server/Services/WorldService.cs
        // Check section duration
        if (sectionCache.Duration > 0 && session.SectionStartTime.HasValue)
        {
            var elapsed = (DateTime.UtcNow - session.SectionStartTime.Value).TotalSeconds;
            if (elapsed >= sectionCache.Duration)
            {
                // Section duration expired, no more respawns
                return false;
            }
        }
```

**Vấn đề:**
- Logic này có vẻ đúng, nhưng cần kiểm tra xem `sectionCache.Duration` có được lưu đúng không
- Cần đảm bảo Duration = 0 được xử lý đúng (unlimited duration)

### Vấn Đề 2: Section Không Chuyển

**Nguyên nhân:**
- Logic chuyển section chỉ dựa trên boss defeat (`CheckBossDefeatedAndAdvanceSection`)
- Section 1 có EnemyTypeId = "-" (null hoặc empty), nên không có boss được spawn
- Nếu không có boss, `CurrentBossId` sẽ null, và logic sẽ không chuyển section

**Code hiện tại:**
```1609:1701:server/Services/WorldService.cs
    private async Task CheckBossDefeatedAndAdvanceSection(SessionState session)
    {
        if (session.Status != SessionStatus.InProgress)
            return; // Session already completed or failed

        if (!session.CurrentBossId.HasValue || !session.IsBossAlive)
            return; // No boss to check
        // ... boss defeat logic ...
    }
```

**Vấn đề:**
- Nếu section không có boss, method này sẽ return sớm và không chuyển section
- Cần logic chuyển section khi tất cả enemies thường bị đánh bại (nếu section không có boss)

## Giải Pháp

### Giai Đoạn 1: Sửa Logic Respawn

**File cần chỉnh sửa:**
- `server/Services/WorldService.cs`

**Các thay đổi:**

1. **Sửa logic kiểm tra section duration:**
    - Đảm bảo Duration được lưu đúng vào CachedSectionConfig (0 = unlimited, >0 = seconds)
    - Kiểm tra lại logic: nếu Duration > 0 và elapsed >= Duration, block respawn
    - Thêm log để debug

2. **Sửa logic kiểm tra capacity:**
    - Đảm bảo logic đếm alive enemies chính xác (chỉ đếm enemies có Status != Dead và Hp > 0)
    - Kiểm tra lại logic: nếu aliveAtCheckpoint >= MaxEnemies, block respawn
    - Kiểm tra lại logic: nếu aliveInSection >= EnemyCount, block respawn
    - Thêm log để debug

3. **Kiểm tra cách lưu Duration:**
    - Xem lại code lưu Duration vào CachedSectionConfig trong `InitializeRoomCheckpointsAsync()`
    - Đảm bảo Duration được lưu đúng (0 = unlimited, >0 = seconds)

### Giai Đoạn 2: Thêm Logic Chuyển Section Khi Không Có Boss

**File cần chỉnh sửa:**
- `server/Services/WorldService.cs`

**Các thay đổi:**

1. **Tạo method mới `CheckAllEnemiesDefeatedAndAdvanceSection()`:**
    - Kiểm tra nếu section không có boss (`!CurrentBossId.HasValue`)
    - Kiểm tra nếu tất cả enemies thường (không phải boss) đã bị đánh bại
    - Nếu đúng: chuyển sang section tiếp theo (tương tự logic boss defeat)
    - Được gọi mỗi tick trong `TickAsync()`

2. **Cập nhật `TickAsync()`:**
    - Gọi `CheckBossDefeatedAndAdvanceSection()` trước
    - Sau đó gọi `CheckAllEnemiesDefeatedAndAdvanceSection()` (chỉ nếu không có boss)

3. **Sửa logic spawn boss:**
    - Chỉ spawn boss nếu `GameSection.EnemyTypeId` không null/empty và không phải "-"
    - Nếu không có boss, `CurrentBossId` sẽ null, và logic sẽ dùng `CheckAllEnemiesDefeatedAndAdvanceSection()`

## Chi Tiết Triển Khai

### 1. Sửa Logic Respawn Duration Check

**File:** `server/Services/WorldService.cs`

**Method:** `CanRespawnEnemy()`

**Thay đổi:**
```csharp
// Check section duration
if (sectionCache.Duration > 0 && session.SectionStartTime.HasValue)
{
    var elapsed = (DateTime.UtcNow - session.SectionStartTime.Value).TotalSeconds;
    if (elapsed >= sectionCache.Duration)
    {
        // Section duration expired, no more respawns
        _logger.LogDebug("Respawn blocked: Section {SectionId} duration expired ({Elapsed}s >= {Duration}s)",
            session.CurrentSectionId, elapsed, sectionCache.Duration);
        return false;
    }
}
```

### 2. Sửa Logic Respawn Capacity Check

**File:** `server/Services/WorldService.cs`

**Method:** `CanRespawnEnemy()`

**Thay đổi:**
- Thêm log để debug capacity checks
- Đảm bảo logic đếm alive enemies chính xác

### 3. Kiểm Tra Cách Lưu Duration

**File:** `server/Services/WorldService.cs`

**Method:** `InitializeRoomCheckpointsAsync()`

**Kiểm tra:**
```csharp
sessionState.CachedSection = new CachedSectionConfig
{
    // ...
    Duration = section.Duration ?? 0f // 0 = unlimited duration
};
```

**Đảm bảo:**
- Duration = 0 nghĩa là unlimited (không block respawn)
- Duration > 0 nghĩa là seconds (block respawn sau khi hết)

### 4. Tạo Method CheckAllEnemiesDefeatedAndAdvanceSection()

**File:** `server/Services/WorldService.cs`

**Method mới:**
```csharp
/// <summary>
/// Check if all regular enemies are defeated and advance to next section if applicable.
/// Only called if section has no boss (CurrentBossId == null).
/// Called each tick in TickAsync().
/// </summary>
private async Task CheckAllEnemiesDefeatedAndAdvanceSection(SessionState session)
{
    if (session.Status != SessionStatus.InProgress)
        return; // Session already completed or failed

    // Only check if section has no boss
    if (session.CurrentBossId.HasValue)
        return; // Section has boss, use boss defeat logic instead

    lock (_sessionLock)
    {
        // Count alive regular enemies (non-boss) in current section
        int aliveRegularEnemies = session.Enemies.Values
            .Count(e => e.SectionId == session.CurrentSectionId &&
                       !e.IsBoss &&
                       e.Status != EnemyStatus.Dead &&
                       e.Hp > 0);

        // If all regular enemies are defeated, advance section
        if (aliveRegularEnemies == 0 && session.CurrentSectionId.HasValue)
        {
            _logger.LogInformation("All regular enemies defeated in section {SectionId} (no boss), advancing to next section",
                session.CurrentSectionId.Value);

            // Mark section as complete
            if (!session.CompletedSections.Contains(session.CurrentSectionId.Value))
            {
                session.CompletedSections.Add(session.CurrentSectionId.Value);
            }

            // Load next section (async, outside lock)
            _ = Task.Run(async () =>
            {
                try
                {
                    var nextSection = await LoadNextSectionAsync(session.CurrentSectionId.Value);

                    if (nextSection != null)
                    {
                        // Initialize next section
                        _logger.LogInformation("Advancing to next section {SectionId} ({SectionName}) in session {SessionId}",
                            nextSection.SectionId, nextSection.Name, session.SessionId);

                        // Clear old enemies before initializing next section
                        lock (_sessionLock)
                        {
                            session.Enemies.Clear();
                            session.CurrentBossId = null;
                            session.IsBossAlive = false;
                        }

                        await InitializeRoomCheckpointsAsync(session.SessionId, nextSection.SectionId);
                    }
                    else
                    {
                        // No more sections, session completed
                        lock (_sessionLock)
                        {
                            session.Status = SessionStatus.Completed;
                            _logger.LogInformation("All sections completed in session {SessionId}. Session status set to Completed.", session.SessionId);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error advancing section in session {SessionId}", session.SessionId);
                }
            });
        }
    }
}
```

### 5. Cập Nhật TickAsync()

**File:** `server/Services/WorldService.cs`

**Method:** `TickAsync()`

**Thay đổi:**
```csharp
public async Task TickAsync(CancellationToken cancellationToken)
{
    foreach (var session in _sessions.Values)
    {
        ProcessInputs(session);
        ProcessEnemyRespawns(session);
        CleanupDeadEnemies(session);

        // Check boss defeat and advance section
        await CheckBossDefeatedAndAdvanceSection(session);

        // Check all enemies defeated (if no boss)
        await CheckAllEnemiesDefeatedAndAdvanceSection(session);

        // Check if all players are dead
        CheckAllPlayersDead(session);

        session.Version++;
    }
}
```

### 6. Sửa Logic Spawn Boss

**File:** `server/Services/WorldService.cs`

**Method:** `LoadBossConfigAsync()`

**Thay đổi:**
- Chỉ spawn boss nếu `GameSection.EnemyTypeId` không null/empty và không phải "-"
- Nếu không có boss, `CurrentBossId` sẽ null

**Method:** `InitializeRoomCheckpointsAsync()`

**Thay đổi:**
- Chỉ gọi `LoadBossConfigAsync()` nếu `section.EnemyTypeId` không null/empty và không phải "-"
- Nếu không có boss, không set `CurrentBossId` và `IsBossAlive`

## Checklist Kiểm Thử

- [ ] Section 1 (Duration = 60s): Respawn dừng sau 60 giây
- [ ] Section 1 (EnemyCount = 10): Respawn dừng khi đạt 10 enemies alive
- [ ] Section 1 (không có boss): Chuyển section khi đánh hết 10 quái
- [ ] Section 2 (Duration = Unlimited): Respawn không dừng (trừ khi đạt capacity)
- [ ] Section 5 (có boss): Chuyển section khi đánh bại boss
- [ ] Section progression: Section 1 → Section 2 → Section 4 → Section 5
- [ ] Respawn limitation: Checkpoint MaxEnemies được enforce đúng
- [ ] Respawn limitation: Section EnemyCount được enforce đúng

## Tóm Tắt Files

**Được chỉnh sửa:**
- `server/Services/WorldService.cs` - Sửa logic respawn và thêm logic chuyển section khi không có boss

**Không cần thay đổi:**
- `server/Models/States/SessionState.cs` - Đã có đủ fields


````