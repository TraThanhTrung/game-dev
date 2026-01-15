-- SQL INSERT statements for Enemies table
-- Run this script on SQL Server

USE GameServerDb;
GO

SET IDENTITY_INSERT Enemies ON;

-- Insert Enemies data
INSERT INTO Enemies
    (EnemyId, TypeId, Name, ExpReward, GoldReward, MaxHealth, Damage, Speed, DetectRange, AttackRange, AttackCooldown, WeaponRange, KnockbackForce, StunTime, RespawnDelay, IsActive, CreatedAt, UpdatedAt)
VALUES
    (1, 'slime', 'Slime', 1, 5, 3, 1, 2.0, 6.0, 1.2000000476837158, 3.0, 1.2000000476837158, 2.799999952316284, 0.30000001192092896, 10, 1, '2026-01-10 23:58:17.1243539', NULL),
    (2, 'gnome', 'Gnome', 2, 8, 4, 2, 3.0, 4.0, 1.0, 2.0, 1.2000000476837158, 3.0, 0.30000001192092896, 10, 1, '2026-01-10 23:59:10.9739976', NULL),
    (3, 'boss_fish', 'fish', 10, 20, 10, 2, 3.0, 7.0, 2.0, 1.0, 2.0, 2.0, 1.0, 999999, 1, '2026-01-12 17:40:04.112308', '2026-01-12 18:50:51.4982802');
GO

USE GameServerDb;
GO

UPDATE PlayerStats
SET Speed = 2.0;

