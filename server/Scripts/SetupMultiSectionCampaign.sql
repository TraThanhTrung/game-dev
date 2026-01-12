-- ============================================
-- Setup Script for Multi-Section Campaign System
-- ============================================
-- This script creates sample GameSections and Checkpoints for testing
-- Run this script after database migrations are complete

-- ============================================
-- 1. Create GameSections
-- ============================================

-- Section 1: Warm-up (Level 1, Easy)
INSERT INTO GameSections (Name, Description, EnemyTypeId, EnemyCount, EnemyLevel, SpawnRate, Duration, IsActive, CreatedAt)
VALUES 
    ('Warm-up', 'Section đầu tiên để làm quen với game', NULL, 5, 1, 1.0, NULL, 1, datetime('now'));

-- Section 2: Escalation (Level 2, Medium)
INSERT INTO GameSections (Name, Description, EnemyTypeId, EnemyCount, EnemyLevel, SpawnRate, Duration, IsActive, CreatedAt)
VALUES 
    ('Escalation', 'Section khó hơn với enemies mạnh hơn', NULL, 8, 2, 1.2, NULL, 1, datetime('now'));

-- Section 3: Final (Level 3, Hard)
INSERT INTO GameSections (Name, Description, EnemyTypeId, EnemyCount, EnemyLevel, SpawnRate, Duration, IsActive, CreatedAt)
VALUES 
    ('Final', 'Section cuối cùng với boss mạnh nhất', 'boss_slime', 3, 3, 1.5, NULL, 1, datetime('now'));

-- ============================================
-- 2. Create Checkpoints for Section 1
-- ============================================

INSERT INTO Checkpoints (CheckpointName, SectionId, X, Y, EnemyPool, MaxEnemies, IsActive, CreatedAt)
VALUES 
    ('Entry Point', 1, -16, 12, '["slime"]', 3, 1, datetime('now')),
    ('Middle Area', 1, 0, 0, '["goblin"]', 2, 1, datetime('now')),
    ('Boss Arena', 1, 16, -12, '["slime"]', 1, 1, datetime('now'));  -- Boss checkpoint (cuối cùng)

-- ============================================
-- 3. Create Checkpoints for Section 2
-- ============================================

INSERT INTO Checkpoints (CheckpointName, SectionId, X, Y, EnemyPool, MaxEnemies, IsActive, CreatedAt)
VALUES 
    ('Forest Entrance', 2, 20, 10, '["goblin"]', 4, 1, datetime('now')),
    ('Deep Forest', 2, 30, 5, '["skeleton"]', 3, 1, datetime('now')),
    ('Boss Lair', 2, 40, 0, '["goblin"]', 1, 1, datetime('now'));  -- Boss checkpoint

-- ============================================
-- 4. Create Checkpoints for Section 3 (Final)
-- ============================================

INSERT INTO Checkpoints (CheckpointName, SectionId, X, Y, EnemyPool, MaxEnemies, IsActive, CreatedAt)
VALUES 
    ('Final Arena', 3, 50, 0, '["skeleton"]', 2, 1, datetime('now')),
    ('Boss Throne', 3, 60, 0, '["slime"]', 1, 1, datetime('now'));  -- Final Boss

-- ============================================
-- 5. Create Boss Enemy Configs (Optional)
-- ============================================
-- Note: If boss configs don't exist, server will create them automatically
-- from regular enemy configs with enhanced stats (3x HP, 2x Damage, etc.)

-- Boss Slime (if slime enemy exists with HP=100, DMG=10, SPD=2.0)
-- Boss will have: HP=300, DMG=20, SPD=1.6, EXP=125, Gold=50
INSERT OR IGNORE INTO Enemies (TypeId, MaxHealth, Damage, Speed, DetectRange, AttackRange, AttackCooldown, RespawnDelay, ExpReward, GoldReward)
VALUES ('boss_slime', 300, 20, 1.6, 8, 1.5, 2.0, 999999, 250, 100);

-- Boss Goblin (if goblin enemy exists)
INSERT OR IGNORE INTO Enemies (TypeId, MaxHealth, Damage, Speed, DetectRange, AttackRange, AttackCooldown, RespawnDelay, ExpReward, GoldReward)
VALUES ('boss_goblin', 400, 30, 2.0, 10, 2.0, 1.5, 999999, 350, 150);

-- ============================================
-- 6. Verify Setup
-- ============================================

-- Check sections
SELECT '=== GameSections ===' AS Info;
SELECT SectionId, Name, EnemyLevel, SpawnRate, IsActive FROM GameSections ORDER BY SectionId;

-- Check checkpoints
SELECT '=== Checkpoints ===' AS Info;
SELECT CheckpointId, CheckpointName, SectionId, X, Y, EnemyPool, MaxEnemies, IsActive 
FROM Checkpoints 
ORDER BY SectionId, CheckpointId;

-- Check boss configs
SELECT '=== Boss Configs ===' AS Info;
SELECT TypeId, MaxHealth, Damage, Speed, ExpReward, GoldReward 
FROM Enemies 
WHERE TypeId LIKE 'boss_%';

-- ============================================
-- Notes:
-- ============================================
-- 1. EnemyPool format: JSON array string, e.g., '["slime", "goblin"]'
-- 2. Last checkpoint (highest CheckpointId) in each section will spawn boss
-- 3. Boss RespawnDelay = 999999 (effectively no respawn)
-- 4. If boss config doesn't exist, server creates it from regular enemy with:
--    - HP: 3x base
--    - Damage: 2x base
--    - Speed: 0.8x base
--    - EXP/Gold: 5x base
-- 5. EnemyLevel scales stats: multiplier = 1 + (level - 1) * 0.1
--    - Level 1: 100% stats
--    - Level 2: 110% stats
--    - Level 3: 120% stats


