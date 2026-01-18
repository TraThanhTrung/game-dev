-- SQL INSERT statements for GameSections table
-- Run this script on SQL Server

USE GameServerDb;
GO
SET IDENTITY_INSERT GameSections ON;
-- Insert GameSections data
INSERT INTO GameSections
    (SectionId, Name, Description, EnemyTypeId, EnemyCount, EnemyLevel, SpawnRate, Duration, IsActive, CreatedAt, UpdatedAt)
VALUES
    (1, 'RPG', 'RPG base level', 'boss_fish', 10, 1, 5.0, 60, 1, '2026-01-10 23:59:32.7248033', '2026-01-12 18:00:30.8511439'),
    (2, 'Warm-up', 'Section đầu tiên để làm quen với game', 'boss_fish', 1, 2, 1.0, 60, 1, '2026-01-12 09:28:02', '2026-01-12 19:13:39.593652');
GO
SET IDENTITY_INSERT GameSections OFF;




