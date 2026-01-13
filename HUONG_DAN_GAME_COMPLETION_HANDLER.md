# Hướng dẫn thêm GameCompletionHandler vào Unity

## Tổng quan
`GameCompletionHandler` là component quản lý flow khi game kết thúc (completed/failed). Nó tự động hiển thị loading screen với status và quay về Home scene.

## Cách 1: Tự động (Đã có sẵn - Khuyến nghị)

GameCompletionHandler **đã được tự động tạo** khi cần thiết:
- Trong `GameSceneInitializer.Awake()` - khi game scene load
- Trong `NetClient` - khi detect status "Completed" hoặc "Failed"

**Không cần làm gì thêm!** Code sẽ tự động tạo khi cần.

## Cách 2: Thêm thủ công vào Scene (Tùy chọn)

Nếu muốn thêm thủ công vào scene:

### Bước 1: Mở Scene (RPG.unity)
1. Mở Unity Editor
2. Mở scene `Assets/Scenes/RPG.unity` (game scene)

### Bước 2: Tạo GameObject
1. Trong Hierarchy, click chuột phải → Create Empty
2. Đặt tên: `GameCompletionHandler`
3. Đặt vị trí: Position (0, 0, 0) - không quan trọng vì không render

### Bước 3: Add Component
1. Chọn GameObject `GameCompletionHandler`
2. Trong Inspector, click "Add Component"
3. Tìm và chọn: `Game Completion Handler`

### Bước 4: Setup (Nếu cần)
- Component không cần config gì thêm
- Script sẽ tự động:
  - Set Instance trong Awake()
  - DontDestroyOnLoad (persist across scenes)

## Cách 3: Thêm vào GameManager Persistent Objects (Tùy chọn)

Nếu muốn thêm vào GameManager để persist:

### Bước 1: Tạo Prefab (Tùy chọn)
1. Tạo GameObject `GameCompletionHandler` trong scene (theo Cách 2)
2. Kéo GameObject vào `Assets/Prefabs/PersistentPrefabs/` để tạo prefab
3. Xóa GameObject khỏi scene (vì sẽ được spawn từ GameManager)

### Bước 2: Thêm vào GameManager
1. Mở `Assets/Prefabs/PersistentPrefabs/GameManager.prefab`
2. Chọn GameObject `GameManager`
3. Trong Inspector, tìm component `Game Manager`
4. Trong `Persistent Objects` array, thêm:
   - Element mới: kéo prefab `GameCompletionHandler` vào
   - Hoặc kéo GameObject từ scene vào

### Bước 3: Xóa code tự động tạo (Tùy chọn)
Nếu đã thêm thủ công, có thể xóa code tự động tạo trong:
- `GameSceneInitializer.cs` - dòng 47-52
- `NetClient.cs` - các dòng tạo handler khi null

**Lưu ý:** Không cần xóa, code sẽ tự detect nếu Instance đã tồn tại và không tạo duplicate.

## Kiểm tra hoạt động

### Test trong Game:
1. Chơi game và giết boss cuối cùng
2. Server sẽ set `Status = "Completed"`
3. Client nhận status → GameCompletionHandler tự động được gọi
4. Loading screen hiển thị: "Game Completed! All sections cleared! Victory!"
5. Sau 3 giây → Quay về Home scene

### Debug Logs:
Khi game completed, bạn sẽ thấy logs:
```
[NetClient] Session completed! All sections finished.
[GameCompletion] Game completed! Starting completion sequence...
[GameCompletion] Disconnecting from server...
[GameCompletion] Loading Home scene...
```

## Troubleshooting

### GameCompletionHandler không được gọi?
- Kiểm tra server có set `Status = "Completed"` không
- Kiểm tra client có nhận được status từ server không
- Xem logs trong Unity Console

### Loading screen không hiển thị?
- Kiểm tra `LoadingScreenManager.Instance` có tồn tại không
- Kiểm tra LoadingScreenManager có được setup trong scene không

### Không quay về Home scene?
- Kiểm tra scene name có đúng "Home" không
- Kiểm tra scene "Home" có trong Build Settings không

## Kết luận

**Khuyến nghị:** Sử dụng Cách 1 (tự động) - đơn giản và đã hoạt động tốt.

Chỉ cần thêm thủ công (Cách 2 hoặc 3) nếu:
- Muốn kiểm soát chính xác khi nào handler được tạo
- Muốn thêm vào persistent prefabs để dễ quản lý
- Debugging và muốn thấy GameObject trong Hierarchy

