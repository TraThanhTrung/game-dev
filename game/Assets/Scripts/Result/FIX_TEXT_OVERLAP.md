# Fix: Text bị gom thành 1 điểm (Text Overlap Issue)

## Nguyên nhân

Text bị gom thành 1 điểm thường do:

1. **Layout Group** đang control size của children (Child Control Width/Height = true)
2. **RectTransform Size Delta = 0** hoặc không được set
3. **TextMeshPro Auto Size** đang bật
4. **Anchors** không đúng

## Giải pháp nhanh

### Bước 1: Fix Layout Group

Với **EnemyListItem** và **PlayerListItem**:

1. Select GameObject có Layout Group (EnemyListItem hoặc TextContainer trong PlayerListItem)
2. Trong Inspector, tìm **Horizontal Layout Group** hoặc **Vertical Layout Group**
3. **UNCHECK các options sau:**
   - ❌ Child Control Width
   - ❌ Child Control Height
   - ❌ Child Force Expand (cả Width và Height)

### Bước 2: Fix RectTransform cho mỗi Text

Với **mỗi TextMeshPro text**:

1. Select Text GameObject
2. Trong Inspector, RectTransform component:

   - **Đảm bảo Width và Height có giá trị > 0**
   - Ví dụ: Width = 200, Height = 50
   - Nếu thấy Width/Height = 0, nhập giá trị cụ thể

3. **Setup Anchors đúng:**
   - Cho text trong Horizontal Layout: Anchor = Left-Stretch hoặc Left-Center
   - Cho text trong Vertical Layout: Anchor = Stretch-Stretch hoặc Stretch-Top

### Bước 3: Fix TextMeshPro Component

Với **mỗi TextMeshPro text**:

1. Select Text GameObject
2. Trong Inspector, TextMeshPro component:
   - **UNCHECK "Auto Size"** (bỏ tick)
   - Set Overflow = "Overflow"
   - Font Size: Set giá trị cụ thể (ví dụ: 22, 20, 18)

### Bước 4: Kiểm tra Hierarchy

Đảm bảo cấu trúc đúng:

```
EnemyListItem
├── Horizontal Layout Group (Child Control = false)
├── EnemyTypeText (Width=200, Height=50)
├── SectionNameText (Width=300, Height=50)
└── CheckpointNameText (Width=200, Height=50)
```

```
PlayerListItem
├── Horizontal Layout Group (Child Control = false)
├── AvatarImage (Width=70, Height=70)
└── TextContainer
    ├── Vertical Layout Group (Child Control = false)
    ├── NameText (Width=430, Height=30)
    ├── LevelText (Width=430, Height=25)
    └── GoldText (Width=430, Height=25)
```

## Checklist Fix

### EnemyListItem

- [ ] Horizontal Layout Group: Child Control Width = false
- [ ] Horizontal Layout Group: Child Control Height = false
- [ ] EnemyTypeText: Width > 0, Height > 0, Auto Size = false
- [ ] SectionNameText: Width > 0, Height > 0, Auto Size = false
- [ ] CheckpointNameText: Width > 0, Height > 0, Auto Size = false

### PlayerListItem

- [ ] Horizontal Layout Group (root): Child Control Width = false
- [ ] AvatarImage: Width = 70, Height = 70
- [ ] TextContainer: Vertical Layout Group, Child Control = false
- [ ] NameText: Width > 0, Height > 0, Auto Size = false
- [ ] LevelText: Width > 0, Height > 0, Auto Size = false
- [ ] GoldText: Width > 0, Height > 0, Auto Size = false

## Test sau khi fix

1. Select prefab trong Project window
2. Xem Preview window
3. Text phải hiển thị đúng với khoảng cách giữa các elements
4. Play scene và kiểm tra

## Nếu vẫn bị lỗi

1. **Delete và tạo lại prefab** theo hướng dẫn mới
2. **Kiểm tra TextMeshPro đã import:**
   - Window → TextMeshPro → Import TMP Essential Resources
3. **Reset RectTransform:**
   - Right-click RectTransform → Reset
   - Sau đó set lại Width/Height cụ thể




