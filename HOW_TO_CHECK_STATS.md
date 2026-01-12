# Cách Kiểm Tra Stats của Player

## Flow Sync Stats

1. **Database (PlayerStats)** → Load khi player join session
2. **Server In-Memory (PlayerState)** → Chứa stats hiện tại trong session
3. **Client (StatsManager)** → Nhận stats từ server qua polling/SignalR

## Cách Kiểm Tra

### 1. Kiểm Tra Trong Unity Console (Client)

Khi game chạy, bật logging trong `ServerStateApplier`:

- Enable `Enable Logging` trong Inspector
- Xem console log: `[StateApplier] Stats from server: Damage=X, Speed=Y, ...`

Hoặc check trong `StatsManager`:

- Mở Stats Panel trong game (nút toggle stats)
- Xem các giá trị hiển thị

### 2. Kiểm Tra Database (Server)

Dùng SQLite browser hoặc command line:

```sql
-- Xem stats của player
SELECT
    pp.Name,
    ps.Damage,
    ps.Speed,
    ps.MaxHealth,
    ps.KnockbackForce,
    ps.BonusDamagePercent,
    ps.ExpBonusPercent,
    ps.DamageReductionPercent
FROM PlayerProfiles pp
JOIN PlayerStats ps ON pp.Id = ps.PlayerId
WHERE pp.Name = 'YourPlayerName';

-- Xem skills của player
SELECT
    pp.Name,
    su.SkillId,
    su.Level
FROM PlayerProfiles pp
JOIN SkillUnlocks su ON pp.Id = su.PlayerId
WHERE pp.Name = 'YourPlayerName';
```

### 3. Kiểm Tra Server Logs

Khi upgrade skill, server sẽ log:

```
✅ Reloaded player stats for {PlayerId}: MaxHp=X, Speed=Y, Damage=Z, ...
```

Khi player join session, server sẽ log:

```
Player {Name} loaded from database: DMG={Damage}, SPD={Speed}, HP={Hp}/{MaxHp}, ...
```

### 4. Kiểm Tra API Response

Test API endpoint `/sessions/{sessionId}/state`:

```bash
# Lấy player ID từ login response
# Sau đó test state endpoint
curl http://localhost:5220/sessions/{sessionId}/state?sinceVersion=0
```

Response sẽ chứa PlayerSnapshot với các stats:

- `damage`
- `speed`
- `knockbackForce`
- `bonusDamagePercent`
- `damageReductionPercent`

## Troubleshooting

### Stats không sync từ database?

1. Check database có đúng values không (dùng SQL query ở trên)
2. Check server logs khi player join session
3. Check `ReloadPlayerStatsAsync()` được gọi sau skill upgrade không

### Stats không sync từ server về client?

1. Check Unity console logs: `[StateApplier] Stats from server: ...`
2. Check `StatsManager.Instance` có được gọi `ApplyServerStats()` không
3. Check network connection (polling/SignalR)

### Stats không apply trong game?

1. Check `PlayerMovement` sử dụng `StatsManager.Instance.speed` không
2. Check `Player_Combat` sử dụng `StatsManager.Instance.GetDamageWithBonus()` không
3. Check UI có update khi stats thay đổi không

## Test Cases

1. **Upgrade Speed skill** → Check database `Speed` tăng → Check server log → Check client log → Check player movement nhanh hơn
2. **Upgrade Damage skill** → Check database `Damage` tăng → Check server log → Check client log → Check damage dealt tăng
3. **Upgrade Knockback skill** → Check database `KnockbackForce` tăng → Check server log → Check client log → Check knockback mạnh hơn
4. **Upgrade Exp Bonus skill** → Check database `ExpBonusPercent` tăng → Check server log → Kill enemy và check exp nhận được

