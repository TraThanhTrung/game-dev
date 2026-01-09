---
name: ""
overview: ""
todos: []
---

---name: "Plan 3: Update Gameplay with Room System"overview: Cập nhật gameplay multiplayer với room system, game sections, game results tracking. Tích hợp Redis cho real-time data và cập nhật WorldService để sử dụng database và Redis thay vì in-memory storage.todos:

- id: room_game_result_entities

content: Tạo Room và GameResult entities, migrations, update GameDbContextstatus: pending

- id: redis_service_extension

content: Mở rộng RedisService với session/room management methods (thay thế ConcurrentDictionary)status: pending

- id: room_service

content: Tạo RoomService với CRUD operations, start/end game section logic, tích hợp database và Redisstatus: pendingdependencies:

- room_game_result_entities
- redis_service_extension
- id: rooms_controller

content: Tạo RoomsController với tất cả API endpoints (create, join, leave, start-section, end-section)status: pendingdependencies:

- room_service
- id: game_sections_controller

content: Tạo GameSectionsController API để lấy danh sách game sectionsstatus: pending

- id: player_stats_api

content: Tạo endpoint GET /api/players/{playerId}/stats để trả về player statsstatus: pending

- id: worldservice_redis

content: Cập nhật WorldService để sử dụng Redis thay vì ConcurrentDictionarystatus: pendingdependencies:

- redis_service_extension
- id: worldservice_database

content: Cập nhật WorldService để load enemy config từ database (EnemyConfigService từ Plan 1)status: pending

- id: worldservice_integration

content: Tích hợp WorldService với RoomService và SessionTrackingServicestatus: pendingdependencies:

- room_service
- worldservice_redis
- worldservice_database
- id: game_result_tracking

content: "Implement game result tracking: lưu GameResult, update player stats, update GameSession"status: pendingdependencies:

- room_service
- id: player_service_update

content: Cập nhật PlayerService để update stats sau game result (exp, gold, level)status: pendingdependencies:

- game_result_tracking
- id: testing_integration

content: Test room system, game sections, game results, Redis integration với Unity clientstatus: pendingdependencies:

- rooms_controller
- game_sections_controller
- worldservice_integration
- game_result_tracking

---

# Plan 3: Update Gameplay with Room System

## Tổng quan

Cập nhật gameplay multiplayer với hệ thống phòng chơi (rooms), game sections, và tracking kết quả. Tích hợp Redis cho real-time game state và cập nhật WorldService để sử dụng database và Redis thay vì in-memory ConcurrentDictionary.

## Phạm vi

- Room system (tạo phòng, join/leave phòng)
- Game Section API (lấy danh sách game sections)
- Game Result tracking (lưu kết quả mỗi game section)
- Redis integration (thay thế ConcurrentDictionary)
- WorldService updates (tích hợp với database và Redis)
- Player stats API (trả về stats khi join room)

## Database Entities (SQL)

**Entities cần tạo:**

- `Room` - Thông tin phòng chơi
- Fields: RoomId, CreatedBy (PlayerId), CreatedAt, MaxPlayers, CurrentPlayers, Status (Waiting/Playing/Finished), GameSectionId
- `GameResult` - Kết quả mỗi game section
- Fields: ResultId, RoomId, GameSectionId, PlayerId, Score, EnemiesKilled, Deaths, ExpGained, GoldGained, Duration, CompletedAt

**Lưu ý:** GameSession, SessionPlayer, Enemy, GameSection đã được tạo ở Plan 1.

## Redis Integration (NoSQL)

**Redis sẽ lưu:**

- Active Game Sessions - SessionState đang chơi
- Player-to-Session Mapping - Mapping player đang trong session nào
- Input Queue - Queue input commands từ players
- Active Rooms - Rooms đang chờ/chơi với real-time state
- Player Online Status - Cache trạng thái online/offline
- Game State Cache - Cache game state để giảm database queries

## Services

### 1. RoomService

**`server/Services/RoomService.cs`**

- `CreateRoomAsync(playerId, maxPlayers, gameSectionId)` - Tạo phòng chơi
- Lưu Room vào SQL database
- Lưu room state vào Redis
- `JoinRoomAsync(roomId, playerId)` - Player join phòng
- Update Room trong database
- Update room state trong Redis
- Trả về player stats từ PlayerProfile/PlayerStats
- `LeaveRoomAsync(roomId, playerId)` - Player rời phòng
- Update Room trong database
- Update room state trong Redis
- `GetRoomAsync(roomId)` - Lấy thông tin phòng
- Lấy từ database, kết hợp với Redis state nếu đang active
- `GetActiveRoomsAsync()` - Lấy danh sách phòng đang chờ/chơi
- Lấy từ Redis (real-time)
- `StartGameSectionAsync(roomId, gameSectionId)` - Bắt đầu game section
- Update Room status
- Tạo GameSession trong database
- Lưu session state vào Redis
- `EndGameSectionAsync(roomId, results)` - Kết thúc game section và lưu kết quả
- Lưu GameResult vào database cho mỗi player
- Update GameSession status
- Xóa session state khỏi Redis
- Update player stats (exp, gold, level)
- `GetPlayerStatsAsync(playerId)` - Lấy player stats từ PlayerProfile/PlayerStats

### 2. RedisService (Extended)

**Mở rộng `server/Services/RedisService.cs` từ Plan 1:**

- **Session Management:**
- `GetSessionStateAsync(sessionId)` - Lấy session state từ Redis
- `SetSessionStateAsync(sessionId, state, ttl)` - Lưu session state với TTL
- `DeleteSessionAsync(sessionId)` - Xóa session khi kết thúc
- **Player Mapping:**
- `GetPlayerSessionAsync(playerId)` - Lấy session ID của player
- `SetPlayerSessionAsync(playerId, sessionId)` - Map player với session
- `RemovePlayerSessionAsync(playerId)` - Xóa mapping
- **Input Queue:**
- `EnqueueInputAsync(playerId, input)` - Thêm input vào Redis queue
- `DequeueInputsAsync()` - Lấy tất cả inputs từ queue (batch processing)
- `ClearInputQueueAsync()` - Clear queue sau khi xử lý
- **Room State:**
- `GetActiveRoomsAsync()` - Lấy danh sách rooms đang active
- `GetRoomStateAsync(roomId)` - Lấy room state
- `SetRoomStateAsync(roomId, state, ttl)` - Lưu room state
- **Player Status:**
- `SetPlayerOnlineAsync(playerId, isOnline)` - Set online status
- `IsPlayerOnlineAsync(playerId)` - Check online status
- `GetOnlinePlayersAsync()` - Lấy danh sách players online

### 3. WorldService Updates

**Cập nhật `server/Services/WorldService.cs`:**

- Thay thế `ConcurrentDictionary` bằng Redis
- Load enemy config từ database (qua EnemyConfigService từ Plan 1)
- Tích hợp với RoomService để quản lý sessions
- Sử dụng RedisService để lưu/load session state
- Track sessions vào database qua SessionTrackingService (từ Plan 1)

## API Endpoints

### Gameplay API (Cho Unity Client)

**`server/Controllers/RoomsController.cs`**

- `POST /api/rooms/create` - Tạo phòng chơi
- Request: `{ playerId, maxPlayers, gameSectionId }`
- Response: `{ roomId, room }`
- `POST /api/rooms/{roomId}/join` - Join phòng
- Request: `{ playerId }`
- Response: `{ room, playerStats }` (player stats từ PlayerProfile/PlayerStats)
- `POST /api/rooms/{roomId}/leave` - Rời phòng
- Request: `{ playerId }`
- Response: `{ success }`
- `GET /api/rooms/{roomId}` - Lấy thông tin phòng
- Response: `{ room, players, gameSection }`
- `GET /api/rooms/active` - Lấy danh sách phòng đang chờ
- Response: `[{ roomId, currentPlayers, maxPlayers, gameSection, status }]`
- `POST /api/rooms/{roomId}/start-section` - Bắt đầu game section
- Request: `{ gameSectionId }`
- Response: `{ sessionId, gameSection }`
- `POST /api/rooms/{roomId}/end-section` - Kết thúc game section và lưu kết quả
- Request: `{ results: [{ playerId, score, enemiesKilled, deaths, expGained, goldGained, duration }] }`
- Response: `{ success, updatedStats }`
- `GET /api/players/{playerId}/stats` - Lấy player stats
- Response: `{ playerId, name, level, exp, gold, stats: { damage, speed, maxHealth, etc. } }`

**`server/Controllers/GameSectionsController.cs`**

- `GET /api/game-sections` - Lấy danh sách game sections available
- Query params: `?active=true`
- Response: `[{ sectionId, name, description, enemyType, enemyCount, enemyLevel, duration }]`
- `GET /api/game-sections/{sectionId}` - Lấy chi tiết game section
- Response: `{ sectionId, name, description, enemyType, enemyConfig, enemyCount, enemyLevel, spawnRate, duration }`

## Dependencies

- Plan 1: GameSession, SessionPlayer, Enemy, GameSection entities
- Plan 1: EnemyConfigService, RedisService (basic)
- Plan 1: SessionTrackingService
- StackExchange.Redis
- Entity Framework Core

## Files to Create

**Database Entities:**

- `server/Models/Entities/Room.cs`
- `server/Models/Entities/GameResult.cs`

**Services:**

- `server/Services/RoomService.cs`

**Controllers:**

- `server/Controllers/RoomsController.cs`
- `server/Controllers/GameSectionsController.cs`

**DTOs:**

- `server/Models/Dto/RoomDto.cs`
- `server/Models/Dto/GameResultDto.cs`
- `server/Models/Dto/CreateRoomRequest.cs`
- `server/Models/Dto/JoinRoomRequest.cs`
- `server/Models/Dto/StartSectionRequest.cs`
- `server/Models/Dto/EndSectionRequest.cs`
- `server/Models/Dto/PlayerStatsResponse.cs`

## Files to Modify

- `server/Data/GameDbContext.cs` - Thêm DbSet<Room>, DbSet<GameResult>
- `server/Services/RedisService.cs` - Mở rộng với session/room management methods
- `server/Services/WorldService.cs` - Tích hợp với Redis và database
- `server/Services/PlayerService.cs` - Thêm method update stats sau game result
- `server/Program.cs` - Register RoomService
- `server/appsettings.json` - Đảm bảo Redis connection string đã có

## Implementation Order

1. **Database Entities**

- Tạo Room và GameResult entities
- Tạo migrations
- Update GameDbContext

2. **RedisService Extension**

- Mở rộng RedisService với session/room management methods
- Test Redis operations

3. **RoomService**

- Implement CRUD operations cho rooms
- Tích hợp với database và Redis
- Implement start/end game section logic

4. **API Controllers**

- RoomsController với tất cả endpoints
- GameSectionsController
- Error handling và validation

5. **WorldService Updates**

- Thay thế ConcurrentDictionary bằng Redis
- Tích hợp với RoomService
- Load enemy config từ database
- Track sessions vào database

6. **Game Result Tracking**

- Lưu GameResult khi game section kết thúc
- Update player stats (exp, gold, level)
- Update GameSession status

7. **Testing & Integration**

- Test room creation/join/leave
- Test game section start/end
- Test game result saving
- Test Redis integration
- Test với Unity client

## Notes

- Room system cho phép players tạo và join phòng chơi
- Game sections được admin quản lý ở Plan 1, players chọn khi tạo room