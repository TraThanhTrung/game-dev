# Hướng dẫn Setup Result Scene trong Unity Editor

## Bước 1: Mở Result Scene

1. Mở Unity Editor
2. Trong Project window, điều hướng đến `Assets/Scenes/Result.unity`
3. Double-click để mở scene

## Bước 2: Tạo Canvas Structure

### 2.1. Tạo Canvas chính

1. Right-click trong Hierarchy → `UI` → `Canvas`
2. Đặt tên: `ResultCanvas`
3. Trong Canvas component:
   - Render Mode: `Screen Space - Overlay`
   - Canvas Scaler: `Scale With Screen Size`
   - Reference Resolution: `1920 x 1080`

### 2.2. Tạo EventSystem (nếu chưa có)

- Unity sẽ tự động tạo khi tạo Canvas đầu tiên
- Nếu chưa có: Right-click → `UI` → `Event System`

## Bước 3: Tạo GameSession Info Panel

### 3.1. Tạo Panel container

1. Right-click trên `ResultCanvas` → `UI` → `Panel`
2. Đặt tên: `GameSessionInfoPanel`
3. RectTransform:
   - Anchor: Top-Left
   - Position: X=50, Y=-50
   - Size: Width=600, Height=200

### 3.2. Tạo Header Text

1. Right-click trên `GameSessionInfoPanel` → `UI` → `Text - TextMeshPro`
2. Đặt tên: `HeaderText`
3. Text: "Game Session Info"
4. Font Size: 32
5. Alignment: Center
6. RectTransform:
   - Anchor: Top-Stretch
   - Position: Y=-20
   - Height: 40

### 3.3. Tạo các Text fields cho thông tin

Tạo 4 TextMeshPro text cho mỗi field:

**StartTimeText:**

- Right-click trên `GameSessionInfoPanel` → `UI` → `Text - TextMeshPro`
- Đặt tên: `StartTimeText`
- Text: "Start Time: --"
- Font Size: 24
- RectTransform: Position Y=-60, Width=550, Height=30

**EndTimeText:**

- Tương tự, đặt tên: `EndTimeText`
- Text: "End Time: --"
- RectTransform: Position Y=-95

**PlayerCountText:**

- Tương tự, đặt tên: `PlayerCountText`
- Text: "Players: --"
- RectTransform: Position Y=-130

**StatusText:**

- Tương tự, đặt tên: `  `
- Text: "Status: --"
- RectTransform: Position Y=

### 3.4. Add GameSessionInfoDisplay Component

1. Select `GameSessionInfoPanel`
2. Add Component → `GameSessionInfoDisplay`
3. Assign các Text fields:
   - Start Time Text → `StartTimeText`
   - End Time Text → `EndTimeText`
   - Player Count Text → `PlayerCountText`
   - Status Text → `StatusText`

## Bước 4: Tạo Enemy List Panel

### 4.1. Tạo Panel container

1. Right-click trên `ResultCanvas` → `UI` → `Panel`
2. Đặt tên: `EnemyListPanel`
3. RectTransform:
   - Anchor: Top-Left
   - Position: X=50, Y=-280
   - Size: Width=600, Height=300

### 4.2. Tạo Header

1. Right-click trên `EnemyListPanel` → `UI` → `Text - TextMeshPro`
2. Đặt tên: `EnemyListHeader`
3. Text: "Enemies Encountered"
4. Font Size: 28
5. Alignment: Center
6. RectTransform: Position Y=-15, Height: 40

### 4.3. Tạo ScrollView

1. Right-click trên `EnemyListPanel` → `UI` → `Scroll View`
2. Đặt tên: `EnemyScrollView`
3. RectTransform:
   - Anchor: Stretch-Stretch
   - Position: X=0, Y=-30 ///////
   - Left/Right/Top/Bottom: 10
   - Top: 50 (để tránh header)

**Lưu ý về cấu trúc ScrollView:**
Khi tạo ScrollView, Unity tự động tạo cấu trúc:

```
EnemyScrollView (ScrollRect component)
└── Viewport (Mask component)
    └── Content (RectTransform - đây là nơi các items sẽ được spawn)
```

### 4.4. Setup ScrollView Content

1. **Tìm Content GameObject:**

   - Mở rộng `EnemyScrollView` trong Hierarchy
   - Mở rộng `Viewport` bên trong
   - Tìm `Content` GameObject (nằm BÊN TRONG Viewport)

2. **Select `Content` GameObject** (không phải Viewport!)

3. Add Component → `Vertical Layout Group`

   - Child Alignment: Upper Center
   - Spacing: 10
   - Padding: Top/Bottom/Left/Right = 10
   - Child Force Expand: Width = true, Height = false

4. Add Component → `Content Size Fitter`
   - Vertical Fit: `Preferred Size`

### 4.5. Tạo Empty Message

1. **Select `Content` GameObject** (nằm trong Viewport, không phải Viewport)
2. Right-click trên `Content` → `UI` → `Text - TextMeshPro`
3. Đặt tên: `EnemyEmptyMessage`
4. Text: "No enemies encountered"
5. Alignment: Center
6. Font Size: 20
7. Color: Gray
8. Set Active = false (sẽ được enable khi không có enemies)

**Quan trọng:** EmptyMessage phải là **child của Content**, không phải Viewport!

### 4.6. Add EnemyListManager Component

1. Select `EnemyListPanel`
2. Add Component → `EnemyListManager`
3. Assign:
   - **Enemy List Container** → `EnemyScrollView/Viewport/Content` (kéo Content từ Hierarchy)
   - **Empty Message** → `EnemyEmptyMessage` (kéo từ EnemyScrollView/Viewport/Content/EnemyEmptyMessage)
   - **Enemy List Item Prefab** → (sẽ tạo ở Bước 6)

## Bước 5: Tạo Player List Panel

### 5.1. Tạo Panel container

1. Right-click trên `ResultCanvas` → `UI` → `Panel`
2. Đặt tên: `PlayerListPanel`
3. RectTransform:
   - Anchor: Top-Right
   - Position: X=-50, Y=-50
   - Size: Width=600, Height=530

### 5.2. Tạo Header

1. Right-click trên `PlayerListPanel` → `UI` → `Text - TextMeshPro`
2. Đặt tên: `PlayerListHeader`
3. Text: "Players"
4. Font Size: 28
5. Alignment: Center
6. RectTransform: Position Y=-15, Height: 40

### 5.3. Tạo ScrollView

1. Right-click trên `PlayerListPanel` → `UI` → `Scroll View`
2. Đặt tên: `PlayerScrollView`
3. RectTransform:
   - Anchor: Stretch-Stretch
   - Position: X=0, Y=-30
   - Left/Right/Top/Bottom: 10
   - Top: 50

**Lưu ý về cấu trúc ScrollView:**
Khi tạo ScrollView, Unity tự động tạo cấu trúc:

```
PlayerScrollView (ScrollRect component)
└── Viewport (Mask component)
    └── Content (RectTransform - đây là nơi các items sẽ được spawn)
```

### 5.4. Setup ScrollView Content

1. **Tìm Content GameObject:**

   - Mở rộng `PlayerScrollView` trong Hierarchy
   - Mở rộng `Viewport` bên trong
   - Tìm `Content` GameObject (nằm BÊN TRONG Viewport)

2. **Select `Content` GameObject** (không phải Viewport!)

3. Add Component → `Vertical Layout Group`

   - Child Alignment: Upper Center
   - Spacing: 10
   - Padding: Top/Bottom/Left/Right = 10
   - Child Force Expand: Width = true, Height = false

4. Add Component → `Content Size Fitter`
   - Vertical Fit: `Preferred Size`

### 5.5. Tạo Empty Message

1. **Select `Content` GameObject** (nằm trong Viewport, không phải Viewport)
2. Right-click trên `Content` → `UI` → `Text - TextMeshPro`
3. Đặt tên: `PlayerEmptyMessage`
4. Text: "No players"
5. Alignment: Center
6. Font Size: 20
7. Color: Gray
8. Set Active = false

**Quan trọng:** EmptyMessage phải là **child của Content**, không phải Viewport!

### 5.6. Add PlayerListManager Component

1. Select `PlayerListPanel`
2. Add Component → `PlayerListManager`
3. Assign:
   - **Player List Container** → `PlayerScrollView/Viewport/Content` (kéo Content từ Hierarchy)
   - **Empty Message** → `PlayerEmptyMessage` (kéo từ PlayerScrollView/Viewport/Content/PlayerEmptyMessage)
   - **Player List Item Prefab** → (sẽ tạo ở Bước 7)

## Bước 6: Tạo EnemyListItem Prefab

### 6.1. Tạo GameObject

1. Right-click trong Hierarchy → `UI` → `Panel`
2. Đặt tên: `EnemyListItem`
3. RectTransform:
   - Width: 560
   - Height: 60

### 6.2. Tạo Layout

1. Add Component → `Horizontal Layout Group`
   - Spacing: 15
   - Padding: Left/Right = 10, Top/Bottom = 5
   - Child Alignment: Middle Left
   - Child Force Expand: Height = true

### 6.3. Tạo EnemyTypeText

1. Right-click trên `EnemyListItem` → `UI` → `Text - TextMeshPro`
2. Đặt tên: `EnemyTypeText`
3. Text: "Enemy Type"
4. Font Size: 22
5. RectTransform: Width = 200, Height = 50

### 6.4. Tạo SectionNameText

1. Right-click trên `EnemyListItem` → `UI` → `Text - TextMeshPro`
2. Đặt tên: `SectionNameText`
3. Text: "Section Name"
4. Font Size: 20
5. Color: Light Gray
6. RectTransform: Width = 300, Height = 50

### 6.5. Tạo CheckpointNameText (Optional)

1. Right-click trên `EnemyListItem` → `UI` → `Text - TextMeshPro`
2. Đặt tên: `CheckpointNameText`
3. Text: ""
4. Font Size: 18
5. Color: Dark Gray
6. RectTransform: Width = 200, Height = 50
7. Set Active = false (sẽ được enable nếu có checkpoint name)

### 6.6. Add EnemyListItem Component

1. Select `EnemyListItem`
2. Add Component → `EnemyListItem`
3. Assign:
   - Enemy Type Text → `EnemyTypeText`
   - Section Name Text → `SectionNameText`
   - Checkpoint Name Text → `CheckpointNameText`

### 6.7. Tạo Prefab

1. Kéo `EnemyListItem` từ Hierarchy vào `Assets/Prefabs/`
2. Xóa `EnemyListItem` khỏi Hierarchy (giữ prefab)

## Bước 7: Tạo PlayerListItem Prefab

### 7.1. Tạo GameObject

1. Right-click trong Hierarchy → `UI` → `Panel`
2. Đặt tên: `PlayerListItem`
3. RectTransform:
   - Width: 560
   - Height: 80

### 7.2. Tạo Layout

1. Add Component → `Horizontal Layout Group`
   - Spacing: 15
   - Padding: Left/Right = 10, Top/Bottom = 5
   - Child Alignment: Middle Left
   - Child Force Expand: Height = true

### 7.3. Tạo Avatar Image

1. Right-click trên `PlayerListItem` → `UI` → `Image`
2. Đặt tên: `AvatarImage`
3. RectTransform:
   - Width: 70
   - Height: 70
4. Image component:

   - **Image Type:** Simple (mặc định - thuộc tính này phải là "Simple" để "Preserve Aspect" xuất hiện)
   - **Source Image:** Gán sprite avatar (có thể để None tạm thời)
   - **Preserve Aspect:** ✅ true (checkbox này giữ nguyên tỷ lệ khung hình của sprite khi RectTransform thay đổi kích thước, tránh bị méo hình)
   - **Color:** White

   **Lưu ý quan trọng:**

   - "Preserve Aspect" chỉ xuất hiện khi **Image Type** được đặt là **Simple** hoặc **Filled**
   - Nếu bạn không thấy "Preserve Aspect" trong Inspector, hãy kiểm tra xem Image Type có phải là "Simple" không
   - "Image Type" thường nằm ở đầu Image component, ngay sau Source Image ////////////

### 7.4. Tạo Vertical Layout cho Text

1. Right-click trên `PlayerListItem` → `UI` → `Panel`
2. Đặt tên: `TextContainer`
3. Add Component → `Vertical Layout Group`
   - Spacing: 5
   - Padding: Left = 10
   - Child Alignment: Middle Left
   - Child Force Expand: Width = true
4. RectTransform: Width = 450, Height = 70

### 7.5. Tạo NameText

1. Right-click trên `TextContainer` → `UI` → `Text - TextMeshPro`
2. Đặt tên: `NameText`
3. Text: "Player Name"
4. Font Size: 24
5. Font Style: Bold
6. RectTransform: Width = 430, Height = 30

### 7.6. Tạo LevelText

1. Right-click trên `TextContainer` → `UI` → `Text - TextMeshPro`
2. Đặt tên: `LevelText`
3. Text: "Level: 1"
4. Font Size: 20
5. RectTransform: Width = 430, Height = 25

### 7.7. Tạo GoldText

1. Right-click trên `TextContainer` → `UI` → `Text - TextMeshPro`
2. Đặt tên: `GoldText`
3. Text: "Gold: 0"
4. Font Size: 20
5. RectTransform: Width = 430, Height = 25

### 7.8. Add PlayerListItem Component

1. Select `PlayerListItem`
2. Add Component → `PlayerListItem`
3. Assign:
   - Avatar Image → `AvatarImage`
   - Name Text → `NameText`
   - Level Text → `LevelText`
   - Gold Text → `GoldText`
   - Default Avatar → (có thể để null hoặc assign một sprite mặc định)

### 7.9. Tạo Prefab

1. Kéo `PlayerListItem` từ Hierarchy vào `Assets/Prefabs/`
2. Xóa `PlayerListItem` khỏi Hierarchy (giữ prefab)

## Bước 8: Tạo Loading Panel và Error Text

### 8.1. Tạo Loading Panel

1. Right-click trên `ResultCanvas` → `UI` → `Panel`
2. Đặt tên: `LoadingPanel`
3. RectTransform: Stretch-Stretch (full screen)
4. Image Color: Black với Alpha = 200
5. Add child: `UI` → `Text - TextMeshPro`
   - Text: "Loading..."
   - Font Size: 36
   - Alignment: Center
   - Color: White

### 8.2. Tạo Error Text

1. Right-click trên `ResultCanvas` → `UI` → `Text - TextMeshPro`
2. Đặt tên: `ErrorText`
3. Text: ""
4. Font Size: 24
5. Color: Red
6. Alignment: Center
7. RectTransform:
   - Anchor: Bottom-Center
   - Position: Y=100
   - Width: 1800, Height: 50
8. Set Active = false

## Bước 9: Tạo Back to Home Button

### 9.1. Tạo Button

1. Right-click trên `ResultCanvas` → `UI` → `Button - TextMeshPro`
2. Đặt tên: `BackToHomeButton`
3. RectTransform:
   - Anchor: Bottom-Center
   - Position: Y=50
   - Width: 200, Height: 50
4. Button Text: "Back to Home"
5. Font Size: 24

## Bước 10: Setup ResultScreenManager

### 10.1. Tạo GameObject

1. Right-click trong Hierarchy → `Create Empty`
2. Đặt tên: `ResultScreenManager`

### 10.2. Add Component

1. Select `ResultScreenManager`
2. Add Component → `ResultScreenManager`

### 10.3. Assign References

Trong ResultScreenManager component, assign:

- **Game Session Info Display** → `GameSessionInfoPanel` (GameSessionInfoDisplay component)
- **Enemy List Manager** → `EnemyListPanel` (EnemyListManager component)
- **Player List Manager** → `PlayerListPanel` (PlayerListManager component)
- **Loading Panel** → `LoadingPanel`
- **Error Text** → `ErrorText`
- **Back To Home Button** → `BackToHomeButton`

### 10.4. Assign Prefabs vào Managers

1. Select `EnemyListPanel`
2. Trong EnemyListManager component:

   - **Enemy List Item Prefab** → Kéo `EnemyListItem` prefab từ `Assets/Prefabs/`

3. Select `PlayerListPanel`
4. Trong PlayerListManager component:
   - **Player List Item Prefab** → Kéo `PlayerListItem` prefab từ `Assets/Prefabs/`

## Bước 11: Kiểm tra và Test

### 11.1. Kiểm tra Structure

Hierarchy nên có cấu trúc:

```
ResultCanvas
├── GameSessionInfoPanel
│   ├── HeaderText
│   ├── StartTimeText
│   ├── EndTimeText
│   ├── PlayerCountText
│   └── StatusText
├── EnemyListPanel
│   ├── EnemyListHeader
│   └── EnemyScrollView
│       ├── Viewport
│       └── Content
│           └── EnemyEmptyMessage
├── PlayerListPanel
│   ├── PlayerListHeader
│   └── PlayerScrollView
│       ├── Viewport
│       └── Content
│           └── PlayerEmptyMessage
├── LoadingPanel
│   └── (Loading text)
├── ErrorText
└── BackToHomeButton
ResultScreenManager (root level)
```

### 11.2. Test Scene

1. Play scene trong Unity Editor
2. Kiểm tra xem ResultScreenManager có load được không
3. Kiểm tra các references đã được assign đúng chưa

## Lưu ý

- Tất cả Text phải dùng **TextMeshPro** (không dùng Text cũ)
- Đảm bảo các prefabs được lưu trong `Assets/Prefabs/`
- Có thể điều chỉnh kích thước và vị trí các panels theo ý muốn
- Màu sắc và font size có thể tùy chỉnh theo design của game

## Troubleshooting

- **Prefab không hiển thị**: Đảm bảo prefab được assign vào Manager components
- **Text không hiển thị**: Kiểm tra TextMeshPro đã được import chưa (Window → TextMeshPro → Import TMP Essential Resources)
- **Layout không đúng**: Kiểm tra Layout Group components và Content Size Fitter
- **Loading không tắt**: Kiểm tra ResultScreenManager có được assign đúng references không
