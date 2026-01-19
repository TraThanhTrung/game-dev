# Result Scene Setup Checklist

## Quick Setup Steps

### ✅ Phase 1: Canvas & Structure
- [ ] Mở Result.unity scene
- [ ] Tạo Canvas (ResultCanvas)
- [ ] Tạo EventSystem (nếu chưa có)

### ✅ Phase 2: GameSession Info Panel
- [ ] Tạo GameSessionInfoPanel
- [ ] Tạo 4 TextMeshPro: StartTimeText, EndTimeText, PlayerCountText, StatusText
- [ ] Add GameSessionInfoDisplay component và assign references

### ✅ Phase 3: Enemy List Panel
- [ ] Tạo EnemyListPanel
- [ ] Tạo ScrollView với Content có Vertical Layout Group
- [ ] Tạo EnemyEmptyMessage
- [ ] Add EnemyListManager component

### ✅ Phase 4: Player List Panel
- [ ] Tạo PlayerListPanel
- [ ] Tạo ScrollView với Content có Vertical Layout Group
- [ ] Tạo PlayerEmptyMessage
- [ ] Add PlayerListManager component

### ✅ Phase 5: EnemyListItem Prefab
- [ ] Tạo EnemyListItem với Horizontal Layout Group
- [ ] Tạo 3 TextMeshPro: EnemyTypeText, SectionNameText, CheckpointNameText
- [ ] Add EnemyListItem component
- [ ] Save as Prefab trong Assets/Prefabs/
- [ ] Assign prefab vào EnemyListManager

### ✅ Phase 6: PlayerListItem Prefab
- [ ] Tạo PlayerListItem với Horizontal Layout Group
- [ ] Tạo AvatarImage (Image component)
- [ ] Tạo TextContainer với Vertical Layout Group
- [ ] Tạo 3 TextMeshPro: NameText, LevelText, GoldText
- [ ] Add PlayerListItem component
- [ ] Save as Prefab trong Assets/Prefabs/
- [ ] Assign prefab vào PlayerListManager

### ✅ Phase 7: UI Elements
- [ ] Tạo LoadingPanel (full screen với text "Loading...")
- [ ] Tạo ErrorText (center bottom)
- [ ] Tạo BackToHomeButton (bottom center)

### ✅ Phase 8: ResultScreenManager
- [ ] Tạo ResultScreenManager GameObject
- [ ] Add ResultScreenManager component
- [ ] Assign tất cả references:
  - [ ] GameSessionInfoDisplay
  - [ ] EnemyListManager
  - [ ] PlayerListManager
  - [ ] LoadingPanel
  - [ ] ErrorText
  - [ ] BackToHomeButton

### ✅ Phase 9: Final Checks
- [ ] Tất cả Text dùng TextMeshPro (không dùng Text cũ)
- [ ] Tất cả prefabs đã được save và assign
- [ ] Test scene trong Play mode
- [ ] Kiểm tra không có missing references

## File Locations

- **Scene**: `Assets/Scenes/Result.unity`
- **Prefabs**: `Assets/Prefabs/EnemyListItem.prefab`, `Assets/Prefabs/PlayerListItem.prefab`
- **Scripts**: `Assets/Scripts/Result/`

## Common Issues

1. **TextMeshPro not imported**: Window → TextMeshPro → Import TMP Essential Resources
2. **Prefab not found**: Đảm bảo prefab được save trong Assets/Prefabs/
3. **Layout broken**: Kiểm tra Layout Group và Content Size Fitter components
4. **Missing references**: Re-assign trong Inspector





