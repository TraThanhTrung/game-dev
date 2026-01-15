# API Documentation - 10 APIs Chính & 10 APIs Hành Động Player

## 📋 MỤC LỤC
1. [10 API Chính Tiêu Biểu](#10-api-chính-tiêu-biểu)
2. [10 API Hỗ Trợ Hành Động Player](#10-api-hỗ-trợ-hành-động-player)

---

## 10 API CHÍNH TIÊU BIỂU

### 1. POST /sessions/join - Tham gia Session
**Mục đích:** Player tham gia vào một game session
**Hành động trong game:** Click "Join Room" hoặc tự động join sau khi tạo room

```22:31:server/Controllers/SessionsController.cs
    [HttpPost("join")]
    public IActionResult Join([FromBody] JoinSessionRequest request)
    {
        if (request.PlayerId == Guid.Empty)
            return BadRequest("PlayerId required");

        HttpContext.Items["playerId"] = request.PlayerId.ToString();
        _world.JoinSession(request);
        return Ok(new JoinSessionResponse { SessionId = request.SessionId });
    }
```

---

### 2. POST /sessions/input - Gửi Input từ Player
**Mục đích:** Unity client gửi input (di chuyển, bắn) lên server
**Hành động trong game:** Player di chuyển (WASD) hoặc bắn (Click chuột)

```37:47:server/Controllers/SessionsController.cs
    [HttpPost("input")]
    public IActionResult SendInput([FromBody] InputRequest request)
    {
        if (request.PlayerId == Guid.Empty)
            return BadRequest("PlayerId required");

        HttpContext.Items["playerId"] = request.PlayerId.ToString();

        _world.EnqueueInput(request);
        return Ok(new { accepted = true });
    }
```

---

### 3. GET /sessions/{sessionId}/state - Lấy Game State
**Mục đích:** Client polling để lấy trạng thái game (vị trí players, enemies, etc.)
**Hành động trong game:** Tự động gọi mỗi 100-200ms để sync game state

```49:59:server/Controllers/SessionsController.cs
    [HttpGet("{sessionId}/state")]
    public ActionResult<StateResponse> GetState([FromRoute] string sessionId, [FromQuery] int? sinceVersion)
    {
        var state = _world.GetState(sessionId, sinceVersion);
        // Return NoContent if client already has latest version (optimization)
        if (sinceVersion.HasValue && sinceVersion.Value >= state.Version)
        {
            return NoContent();
        }
        return Ok(state);
    }
```

---

### 4. POST /rooms/create - Tạo Room Mới
**Mục đích:** Player tạo một room/session mới
**Hành động trong game:** Click "Create Room" button

```32:81:server/Controllers/RoomsController.cs
    [HttpPost("create")]
    public async Task<IActionResult> CreateRoom([FromBody] CreateRoomRequest request)
    {
        if (request == null)
            return BadRequest("Request body is required");

        // Get PlayerId (handles both Guid and string formats from Unity)
        var playerId = request.GetPlayerId();
        if (playerId == Guid.Empty)
        {
            return BadRequest("PlayerId required and must be a valid GUID");
        }

        // Validate player exists
        var player = await _playerService.GetPlayerAsync(playerId);
        if (player == null)
        {
            return Unauthorized("Invalid player");
        }

        HttpContext.Items["playerId"] = playerId.ToString();

        // 1. Create GameSession in DB (SessionTrackingService.StartSessionAsync)
        var gameSession = await _tracking.StartSessionAsync(playerCount: 1);

        // 2. Create SessionState in WorldService (in-memory)
        _world.CreateRoom(gameSession.SessionId);

        // 3. Track player join in DB
        await _tracking.TrackPlayerJoinAsync(gameSession.SessionId, playerId);

        // 4. Move player to the new room session
        var joinRequest = new JoinSessionRequest
        {
            PlayerId = playerId,
            PlayerName = player.Name,
            SessionId = gameSession.SessionId.ToString(),
            Token = request.Token
        };
        _world.JoinSession(joinRequest);

        _logger.LogInformation("Room created and joined: {RoomId} by player {PlayerId}",
            gameSession.SessionId, playerId);

        // 5. Return Room ID (GameSession.SessionId as GUID string)
        return Ok(new CreateRoomResponse
        {
            RoomId = gameSession.SessionId.ToString()
        });
    }
```

---

### 5. POST /rooms/join - Tham gia Room
**Mục đích:** Player tham gia vào một room đã tồn tại bằng Room ID
**Hành động trong game:** Nhập Room ID và click "Join Room"

```86:151:server/Controllers/RoomsController.cs
    [HttpPost("join")]
    public async Task<IActionResult> JoinRoom([FromBody] JoinRoomRequest request)
    {
        if (request == null)
            return BadRequest("Request body is required");

        // Get PlayerId (handles both Guid and string formats from Unity)
        var playerId = request.GetPlayerId();
        if (playerId == Guid.Empty)
        {
            return BadRequest("PlayerId required and must be a valid GUID");
        }

        if (string.IsNullOrWhiteSpace(request.RoomId))
            return BadRequest("RoomId required");

        // Validate player exists
        var player = await _playerService.GetPlayerAsync(playerId);
        if (player == null)
        {
            return Unauthorized("Invalid player");
        }

        // 1. Validate Room ID exists in DB (GameSessions table)
        if (!Guid.TryParse(request.RoomId, out var sessionId))
        {
            return BadRequest("Invalid Room ID format");
        }

        var gameSession = await _tracking.GetSessionAsync(sessionId);
        if (gameSession == null)
        {
            return NotFound("Room not found");
        }

        if (gameSession.Status != "Active")
        {
            return BadRequest("Room is not active");
        }

        HttpContext.Items["playerId"] = playerId.ToString();

        // 2. Create SessionPlayer in DB (SessionTrackingService.TrackPlayerJoinAsync)
        await _tracking.TrackPlayerJoinAsync(sessionId, playerId);

        // 3. Join SessionState in WorldService
        var joinRequest = new JoinSessionRequest
        {
            PlayerId = playerId,
            PlayerName = player.Name,
            SessionId = request.RoomId,
            Token = request.Token
        };

        _world.JoinSession(joinRequest);

        _logger.LogInformation("Player {PlayerId} joined room {RoomId}",
            playerId, request.RoomId);

        // 4. Return success
        return Ok(new JoinRoomResponse
        {
            Success = true,
            RoomId = request.RoomId
        });
    }
```

---

### 6. POST /auth/register - Đăng ký/Đăng nhập Player
**Mục đích:** Player đăng ký hoặc đăng nhập vào game
**Hành động trong game:** Nhập tên player và click "Login" hoặc "Register"

```36:57:server/Controllers/AuthController.cs
    [HttpPost("register")]
    public async Task<ActionResult<RegisterResponse>> Register([FromBody] RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.PlayerName))
        {
            return BadRequest("PlayerName is required");
        }

        // Find player in database (do NOT create automatically)
        var player = await _playerService.FindPlayerAsync(request.PlayerName);
        if (player == null)
        {
            return NotFound("Player not found. Please register via Admin Panel or Register page first.");
        }

        // Register in WorldService (in-memory state)
        var result = await _world.RegisterOrLoadPlayerAsync(player, isNew: false);

        _logger.LogInformation("Loaded player: {Name} (ID: {Id})", player.Name, player.Id);

        return Ok(result);
    }
```

---

### 7. POST /sessions/kill - Báo cáo Kill Enemy
**Mục đích:** Client báo cáo khi player kill một enemy để nhận reward
**Hành động trong game:** Enemy bị tiêu diệt → tự động gọi API này

```186:213:server/Controllers/SessionsController.cs
    [HttpPost("kill")]
    public IActionResult ReportKill([FromBody] KillReportRequest request)
    {
        if (request.PlayerId == Guid.Empty)
            return BadRequest("PlayerId required");

        HttpContext.Items["playerId"] = request.PlayerId.ToString();

        var granted = _world.ReportKill(request.PlayerId, request.EnemyTypeId);
        if (!granted)
        {
            return NotFound(new { granted = false, message = "Kill not applied" });
        }

        var playerState = _world.GetPlayerState(request.PlayerId);
        if (playerState == null)
        {
            return NotFound(new { granted = false, message = "Player state missing" });
        }

        return Ok(new KillReportResponse
        {
            Granted = true,
            Level = playerState.Level,
            Exp = playerState.Exp,
            Gold = playerState.Gold
        });
    }
```

---

### 8. POST /sessions/damage - Báo cáo Damage nhận từ Enemy
**Mục đích:** Client báo cáo khi player nhận damage từ enemy
**Hành động trong game:** Player bị enemy tấn công → HP giảm

```218:241:server/Controllers/SessionsController.cs
    [HttpPost("damage")]
    public IActionResult ReportDamage([FromBody] DamageReportRequest request)
    {
        if (request.PlayerId == Guid.Empty)
            return BadRequest("PlayerId required");

        if (request.DamageAmount <= 0)
            return BadRequest("DamageAmount must be positive");

        HttpContext.Items["playerId"] = request.PlayerId.ToString();

        var result = _world.ApplyDamage(request.PlayerId, request.DamageAmount);
        if (result == null)
        {
            return NotFound(new { accepted = false, message = "Player not found" });
        }

        return Ok(new DamageReportResponse
        {
            Accepted = true,
            CurrentHp = result.Value.hp,
            MaxHp = result.Value.maxHp
        });
    }
```

---

### 9. POST /sessions/save - Lưu Tiến Độ
**Mục đích:** Lưu tiến độ player (Level, Exp, Gold) vào database
**Hành động trong game:** Tự động lưu định kỳ hoặc khi player disconnect

```119:148:server/Controllers/SessionsController.cs
    [HttpPost("save")]
    public async Task<IActionResult> SaveProgress([FromBody] SaveProgressRequest request)
    {
        if (request.PlayerId == Guid.Empty)
            return BadRequest("PlayerId required");

        HttpContext.Items["playerId"] = request.PlayerId.ToString();

        // Get current player state from WorldService
        var playerState = _world.GetPlayerState(request.PlayerId);
        if (playerState == null)
        {
            _logger.LogWarning("SaveProgress: Player not found in session: {Id}", request.PlayerId);
            return NotFound("Player not in session");
        }

        // Save to database
        await _playerService.SaveProgressAsync(
            request.PlayerId,
            playerState.Exp,
            playerState.Gold,
            playerState.Level,
            playerState.Hp
        );

        _logger.LogInformation("Saved progress for player {Id}: Level={Level}, Exp={Exp}, Gold={Gold}",
            request.PlayerId.ToString()[..8], playerState.Level, playerState.Exp, playerState.Gold);

        return Ok(new { saved = true });
    }
```

---

### 10. GET /sessions/{sessionId}/metadata - Lấy Session Metadata
**Mục đích:** Lấy thông tin session (số players, version) cho loading screen
**Hành động trong game:** Hiển thị trong loading screen trước khi vào game

```72:90:server/Controllers/SessionsController.cs
    [HttpGet("{sessionId}/metadata")]
    public ActionResult<SessionMetadataResponse> GetSessionMetadata([FromRoute] string sessionId)
    {
        var roomInfo = _world.GetRoomInfo(sessionId);
        if (roomInfo == null)
        {
            return NotFound(new { message = "Session not found" });
        }

        var response = new SessionMetadataResponse
        {
            SessionId = sessionId,
            PlayerCount = roomInfo.Value.playerCount,
            Version = roomInfo.Value.version,
            Players = _world.GetPlayerMetadata(sessionId)
        };

        return Ok(response);
    }
```

---

## 10 API HỖ TRỢ HÀNH ĐỘNG PLAYER

### 1. POST /sessions/input - Input Di Chuyển & Bắn
**Hành động:** Player di chuyển (WASD) hoặc bắn (Click chuột)
**Mô tả:** Unity gửi input liên tục khi player di chuyển/bắn

```37:47:server/Controllers/SessionsController.cs
    [HttpPost("input")]
    public IActionResult SendInput([FromBody] InputRequest request)
    {
        if (request.PlayerId == Guid.Empty)
            return BadRequest("PlayerId required");

        HttpContext.Items["playerId"] = request.PlayerId.ToString();

        _world.EnqueueInput(request);
        return Ok(new { accepted = true });
    }
```

**Có thể chụp:** Player đang di chuyển trong game, hoặc đang bắn đạn

---

### 2. POST /sessions/kill - Hành Động Kill Enemy
**Hành động:** Player tiêu diệt một enemy
**Mô tả:** Khi enemy HP = 0, client gọi API này để nhận Exp và Gold

```186:213:server/Controllers/SessionsController.cs
    [HttpPost("kill")]
    public IActionResult ReportKill([FromBody] KillReportRequest request)
    {
        if (request.PlayerId == Guid.Empty)
            return BadRequest("PlayerId required");

        HttpContext.Items["playerId"] = request.PlayerId.ToString();

        var granted = _world.ReportKill(request.PlayerId, request.EnemyTypeId);
        if (!granted)
        {
            return NotFound(new { granted = false, message = "Kill not applied" });
        }

        var playerState = _world.GetPlayerState(request.PlayerId);
        if (playerState == null)
        {
            return NotFound(new { granted = false, message = "Player state missing" });
        }

        return Ok(new KillReportResponse
        {
            Granted = true,
            Level = playerState.Level,
            Exp = playerState.Exp,
            Gold = playerState.Gold
        });
    }
```

**Có thể chụp:** Enemy bị tiêu diệt, hiệu ứng death animation, Exp/Gold tăng lên

---

### 3. POST /sessions/damage - Hành Động Nhận Damage
**Hành động:** Player bị enemy tấn công và mất HP
**Mô tả:** Khi enemy tấn công player, client báo cáo damage để server cập nhật HP

```218:241:server/Controllers/SessionsController.cs
    [HttpPost("damage")]
    public IActionResult ReportDamage([FromBody] DamageReportRequest request)
    {
        if (request.PlayerId == Guid.Empty)
            return BadRequest("PlayerId required");

        if (request.DamageAmount <= 0)
            return BadRequest("DamageAmount must be positive");

        HttpContext.Items["playerId"] = request.PlayerId.ToString();

        var result = _world.ApplyDamage(request.PlayerId, request.DamageAmount);
        if (result == null)
        {
            return NotFound(new { accepted = false, message = "Player not found" });
        }

        return Ok(new DamageReportResponse
        {
            Accepted = true,
            CurrentHp = result.Value.hp,
            MaxHp = result.Value.maxHp
        });
    }
```

**Có thể chụp:** Player bị enemy tấn công, HP bar giảm, hiệu ứng damage

---

### 4. POST /sessions/enemy-damage - Hành Động Gây Damage cho Enemy
**Hành động:** Player tấn công enemy và gây damage
**Mô tả:** Khi đạn của player trúng enemy, client báo cáo damage

```246:273:server/Controllers/SessionsController.cs
    [HttpPost("enemy-damage")]
    public IActionResult ReportEnemyDamage([FromBody] EnemyDamageRequest request)
    {
        if (request.PlayerId == Guid.Empty)
            return BadRequest("PlayerId required");

        if (request.EnemyId == Guid.Empty)
            return BadRequest("EnemyId required");

        if (request.DamageAmount <= 0)
            return BadRequest("DamageAmount must be positive");

        HttpContext.Items["playerId"] = request.PlayerId.ToString();

        var result = _world.ApplyDamageToEnemy(request.PlayerId, request.EnemyId, request.DamageAmount);
        if (result == null)
        {
            return NotFound(new { accepted = false, message = "Enemy not found or player not in session" });
        }

        return Ok(new EnemyDamageResponse
        {
            Accepted = true,
            CurrentHp = result.Value.hp,
            MaxHp = result.Value.maxHp,
            IsDead = result.Value.hp <= 0
        });
    }
```

**Có thể chụp:** Đạn trúng enemy, enemy HP bar giảm, hiệu ứng hit

---

### 5. POST /sessions/respawn - Hành Động Respawn
**Hành động:** Player chết và respawn lại
**Mô tả:** Khi player HP = 0, có thể respawn tại spawn point với 50% HP

```278:300:server/Controllers/SessionsController.cs
    [HttpPost("respawn")]
    public IActionResult Respawn([FromBody] RespawnRequest request)
    {
        if (request.PlayerId == Guid.Empty)
            return BadRequest("PlayerId required");

        HttpContext.Items["playerId"] = request.PlayerId.ToString();

        var result = _world.RespawnPlayer(request.PlayerId);
        if (result == null)
        {
            return NotFound(new { accepted = false, message = "Player not found" });
        }

        return Ok(new RespawnResponse
        {
            Accepted = true,
            X = result.Value.x,
            Y = result.Value.y,
            CurrentHp = result.Value.hp,
            MaxHp = result.Value.maxHp
        });
    }
```

**Có thể chụp:** Player chết, respawn animation, player xuất hiện lại tại spawn point

---

### 6. POST /skills/upgrade - Hành Động Upgrade Skill
**Hành động:** Player nâng cấp skill (tăng damage, speed, HP, etc.)
**Mô tả:** Khi player đủ level/gold, có thể upgrade skill trong skill tree

```26:85:server/Controllers/SkillsController.cs
    [HttpPost("upgrade")]
    public async Task<IActionResult> UpgradeSkill([FromBody] SkillUpgradeRequest request)
    {
        if (string.IsNullOrEmpty(request.PlayerId) || !Guid.TryParse(request.PlayerId, out var playerId))
        {
            return BadRequest(new SkillUpgradeResponse
            {
                Success = false,
                Message = "Invalid PlayerId"
            });
        }

        if (string.IsNullOrEmpty(request.SkillId))
        {
            return BadRequest(new SkillUpgradeResponse
            {
                Success = false,
                Message = "SkillId is required"
            });
        }

        // Get player's session ID
        var playerState = _worldService.GetPlayerState(playerId);
        if (playerState == null)
        {
            return BadRequest(new SkillUpgradeResponse
            {
                Success = false,
                SkillId = request.SkillId,
                Message = "Player not found in any session"
            });
        }

        // Get session ID from WorldService
        string sessionId = _worldService.GetPlayerSessionId(playerId) ?? "default";

        // Upgrade temporary skill in Redis
        var result = await _temporarySkillService.UpgradeTemporarySkillAsync(sessionId, playerId, request.SkillId);
        
        if (!result.Success)
        {
            return BadRequest(new SkillUpgradeResponse
            {
                Success = false,
                SkillId = request.SkillId,
                Message = result.ErrorMessage ?? "Failed to upgrade skill"
            });
        }

        // Reload player stats with temporary bonuses
        await ReloadPlayerStatsWithBonusesAsync(playerId, sessionId);

        return Ok(new SkillUpgradeResponse
        {
            Success = true,
            SkillId = request.SkillId,
            Level = result.Level,
            Message = "Skill upgraded successfully"
        });
    }
```

**Có thể chụp:** Skill tree UI, click upgrade button, skill level tăng, stats tăng

---

### 7. POST /sessions/ready - Hành Động Ready để Bắt Đầu
**Hành động:** Player báo hiệu đã sẵn sàng bắt đầu game
**Mô tả:** Sau khi loading screen hoàn tất, player click "Ready"

```96:114:server/Controllers/SessionsController.cs
    [HttpPost("{sessionId}/ready")]
    public IActionResult SignalReady([FromRoute] string sessionId, [FromBody] ReadyRequest request)
    {
        if (request.PlayerId == Guid.Empty)
            return BadRequest("PlayerId required");

        HttpContext.Items["playerId"] = request.PlayerId.ToString();

        // Update player's CharacterType if provided
        if (!string.IsNullOrEmpty(request.CharacterType))
        {
            _world.SetPlayerCharacterType(request.PlayerId, request.CharacterType);
        }

        _logger.LogInformation("[Sessions] Player {PlayerId} ready for session {SessionId} with character {CharacterType}",
            request.PlayerId.ToString()[..8], sessionId, request.CharacterType ?? "default");

        return Ok(new { ready = true, sessionId });
    }
```

**Có thể chụp:** Loading screen, "Ready" button, character selection screen

---

### 8. POST /sessions/save - Hành Động Lưu Tiến Độ
**Hành động:** Lưu tiến độ game của player
**Mô tả:** Tự động lưu hoặc khi player disconnect

```119:148:server/Controllers/SessionsController.cs
    [HttpPost("save")]
    public async Task<IActionResult> SaveProgress([FromBody] SaveProgressRequest request)
    {
        if (request.PlayerId == Guid.Empty)
            return BadRequest("PlayerId required");

        HttpContext.Items["playerId"] = request.PlayerId.ToString();

        // Get current player state from WorldService
        var playerState = _world.GetPlayerState(request.PlayerId);
        if (playerState == null)
        {
            _logger.LogWarning("SaveProgress: Player not found in session: {Id}", request.PlayerId);
            return NotFound("Player not in session");
        }

        // Save to database
        await _playerService.SaveProgressAsync(
            request.PlayerId,
            playerState.Exp,
            playerState.Gold,
            playerState.Level,
            playerState.Hp
        );

        _logger.LogInformation("Saved progress for player {Id}: Level={Level}, Exp={Exp}, Gold={Gold}",
            request.PlayerId.ToString()[..8], playerState.Level, playerState.Exp, playerState.Gold);

        return Ok(new { saved = true });
    }
```

**Có thể chụp:** "Saving..." notification, hoặc khi disconnect game

---

### 9. POST /sessions/disconnect - Hành Động Disconnect
**Hành động:** Player rời khỏi game session
**Mô tả:** Khi player đóng game hoặc rời room, tự động lưu và disconnect

```153:181:server/Controllers/SessionsController.cs
    [HttpPost("disconnect")]
    public async Task<IActionResult> Disconnect([FromBody] DisconnectRequest request)
    {
        if (request.PlayerId == Guid.Empty)
            return BadRequest("PlayerId required");

        HttpContext.Items["playerId"] = request.PlayerId.ToString();

        // Get current player state and save before removing
        var playerState = _world.GetPlayerState(request.PlayerId);
        if (playerState != null)
        {
            await _playerService.SaveProgressAsync(
                request.PlayerId,
                playerState.Exp,
                playerState.Gold,
                playerState.Level,
                playerState.Hp
            );

            _logger.LogInformation("Player disconnected and saved: {Id} Level={Level}",
                request.PlayerId.ToString()[..8], playerState.Level);
        }

        // Remove player from session (optional: implement in WorldService)
        // _world.RemovePlayer(request.PlayerId);

        return Ok(new { disconnected = true });
    }
```

**Có thể chụp:** Click "Quit" button, hoặc đóng game window

---

### 10. POST /sessions/{sessionId}/reset - Hành Động Reset Session
**Hành động:** Reset lại session (xóa tất cả players, enemies)
**Mô tả:** Admin hoặc player có thể reset session để bắt đầu lại

```61:66:server/Controllers/SessionsController.cs
    [HttpPost("{sessionId}/reset")]
    public IActionResult ResetSession([FromRoute] string sessionId)
    {
        _world.ResetSession(sessionId);
        return Ok(new { reset = true, sessionId });
    }
```

**Có thể chụp:** "Reset" button trong game, hoặc admin panel

---

## 📸 HƯỚNG DẪN CHỤP ẢNH

Để chụp ảnh minh họa cho từng API, bạn có thể:

1. **Input API** - Chụp player đang di chuyển hoặc bắn
2. **Kill API** - Chụp enemy bị tiêu diệt với hiệu ứng
3. **Damage API** - Chụp player bị tấn công, HP bar giảm
4. **Enemy Damage API** - Chụp đạn trúng enemy, enemy HP giảm
5. **Respawn API** - Chụp player respawn tại spawn point
6. **Skill Upgrade API** - Chụp skill tree UI khi upgrade
7. **Ready API** - Chụp loading screen hoặc ready button
8. **Save API** - Chụp "Saving..." notification
9. **Disconnect API** - Chụp quit button hoặc disconnect dialog
10. **Reset API** - Chụp reset button hoặc admin panel

---

## 🔗 ENDPOINT SUMMARY

### Main APIs (10)
- `POST /sessions/join`
- `POST /sessions/input`
- `GET /sessions/{sessionId}/state`
- `POST /rooms/create`
- `POST /rooms/join`
- `POST /auth/register`
- `POST /sessions/kill`
- `POST /sessions/damage`
- `POST /sessions/save`
- `GET /sessions/{sessionId}/metadata`

### Player Action APIs (10)
- `POST /sessions/input` - Movement/Shooting
- `POST /sessions/kill` - Kill enemy
- `POST /sessions/damage` - Take damage
- `POST /sessions/enemy-damage` - Deal damage
- `POST /sessions/respawn` - Respawn
- `POST /skills/upgrade` - Upgrade skill
- `POST /sessions/ready` - Ready to start
- `POST /sessions/save` - Save progress
- `POST /sessions/disconnect` - Disconnect
- `POST /sessions/{sessionId}/reset` - Reset session



