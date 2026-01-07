---
name: Shared Config Sync
overview: Align defaults, level/exp/gold, and enemy rewards via shared config and keep server-client state consistent.
todos:
  - id: update-config
    content: Define player/enemy defaults in shared/game-config.json
    status: completed
  - id: server-config
    content: Load config in server and replace hardcoded defaults
    status: completed
    dependencies:
      - update-config
  - id: client-config
    content: Load config in Unity and init stats/progress
    status: completed
    dependencies:
      - update-config
  - id: sync-ui
    content: Apply server snapshot to level/exp/gold UI and reconcile local rewards
    status: completed
    dependencies:
      - client-config
      - server-config
  - id: todo-1767554583716-i6i9yn7u0
    content: |-
      Các bước cần kiểm tra và sửa
      - @server/Services/WorldService.cs  có nhiều đoạn code thừa. Hãy loại bỏ chúng tránh gây hiểu nhầm vì chúng cũng đang sử dụng một số thông tin default để tạo Player hay Enemy.
      - Hiện tại quá trình Interval-polling đang khiến di chuyển trở nên bị giật
      - Máu bị trừ liên tục chưa rõ nguyên nhân.
    status: pending
---

# Plan: Shared Config Sync

## Steps

- **Define shared schema**: Expand `shared/game-config.json` with player defaults (level/exp/gold + base stats), enemy stats/rewards keyed by `typeId`, exp curve, and poll interval; keep current numbers as defaults.
- **Server uses config (no enemy simulation)**: Add a config loader singleton; in `PlayerService` and `WorldService` load player defaults/exp curve/rewards from config, remove hardcoded defaults and enemy AI/state. Keep only reward lookup when a client reports a kill with `enemyTypeId`.
- **Unity uses config**: Add a lightweight loader in `game/Assets/Scripts` to read the same JSON. Initialize `StatsManager`/progression from config on join; use config enemy rewards for local kill detection reporting.
- **Enemy key on prefabs**: Add `typeId` (serialized string) component/field on Enemy GameObjects to map to config for kill reporting.
- **State sync & UI**: Update `ServerStateApplier` (and related UI) to apply level/exp/gold/maxHp from snapshots; reconcile local gains with authoritative state; reduce polling jitter by allowing poll interval from config and guarding updates by sequence/timestamps to avoid choppy movement.
- **Cleanup**: Remove redundant/hardcoded code in `WorldService` (e.g., default enemy/player constructors and unused enemy snapshot data) to avoid confusion.

## Notes

- Keep existing default numbers; only centralize them in the config.
- Ensure both server and client read the same `shared/game-config.json` path relative to repo root.
- Server stops simulating enemies; it only tracks rewards by `enemyTypeId` on kill reports from client.
- Address polling jitter and unexplained continuous HP loss by tightening sequence checks and removing any unintended repeated damage sources during sync.
```mermaid
flowchart TD
config[shared/game-config.json]
config --> serverLoader[Server config loader]
config --> clientLoader[Unity config loader]
serverLoader --> world[WorldService defaults & rewards]
serverLoader --> playerSvc[PlayerService defaults]
world --> state[StateResponse (level/exp/gold/stats)]
state --> stateApplier[ServerStateApplier]
clientLoader --> statsMgr[StatsManager init]
clientLoader --> expLocal[Local kill reward hint]
stateApplier --> statsMgr
stateApplier --> ui[UI updates]



```