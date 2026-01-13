# Hướng Dẫn Setup ServerConfig cho Build

## Vấn đề
Khi build game, ServerConfig asset có thể không được include hoặc reference bị mất, dẫn đến game luôn fallback về `localhost:5220`.

## Giải pháp

### Cách 1: Đặt ServerConfig vào Resources Folder (Khuyến nghị)

1. **Tạo thư mục Resources/Config** (nếu chưa có):
   - Trong Project window, right-click `Assets`
   - Create > Folder
   - Đặt tên: `Resources`
   - Trong `Resources`, tạo thư mục `Config`

2. **Di chuyển ServerConfig asset vào Resources/Config**:
   - Tìm ServerConfig asset hiện tại (thường ở `Assets/Scripts/Config/ServerConfig.asset`)
   - Kéo thả vào `Assets/Resources/Config/`
   - **Quan trọng**: Đảm bảo tên file là `ServerConfig.asset` (không có số, không có suffix)

3. **Kiểm tra**:
   - Path cuối cùng phải là: `Assets/Resources/Config/ServerConfig.asset`
   - Unity sẽ tự động include vào build

### Cách 2: Assign trong Inspector (Chỉ hoạt động nếu GameObject tồn tại trong scene)

1. **Tìm NetClient GameObject**:
   - Mở scene có NetClient (thường là Login scene hoặc scene đầu tiên)
   - Hoặc NetClient được tạo tự động bởi `MultiplayerUIManager`

2. **Assign ServerConfig**:
   - Chọn NetClient GameObject
   - Inspector → NetClient component
   - Kéo ServerConfig asset vào field "Server Config"

**Lưu ý**: Cách này chỉ hoạt động nếu NetClient GameObject tồn tại trong scene. Nếu NetClient được tạo runtime, reference sẽ bị mất khi build.

### Cách 3: Tạo Prefab với ServerConfig (Cho Persistent Objects)

1. **Tạo NetClient Prefab**:
   - Tạo GameObject mới, đặt tên "NetClient"
   - Add component `NetClient`
   - Assign ServerConfig vào Inspector
   - Kéo vào Prefabs folder để tạo prefab

2. **Sử dụng Prefab trong scene**:
   - Thay vì tạo NetClient runtime, sử dụng prefab
   - Đảm bảo prefab được reference trong scene

## Kiểm tra sau khi build

1. **Chạy build và kiểm tra Console**:
   - Nếu thấy: `[NetClient] ServerConfig loaded: ServerConfig, BaseUrl: http://your-server:5220` → ✅ Thành công
   - Nếu thấy: `[NetClient] ServerConfig not found! Using fallback URL: http://localhost:5220` → ❌ Cần fix

2. **Test kết nối**:
   - Game phải connect đến server URL đúng (không phải localhost)

## Troubleshooting

### Vấn đề: Vẫn fallback về localhost sau khi đặt vào Resources

**Nguyên nhân có thể**:
1. Tên file không đúng: Phải là `ServerConfig.asset` (không có số, không có suffix)
2. Path không đúng: Phải là `Resources/Config/ServerConfig` (không có `.asset` trong path khi load)
3. Asset chưa được import: Right-click asset → Reimport

**Giải pháp**:
1. Kiểm tra tên file trong Project window
2. Kiểm tra path: `Assets/Resources/Config/ServerConfig.asset`
3. Reimport asset: Right-click → Reimport
4. Clean build: Xóa thư mục `Library/` và build lại

### Vấn đề: ServerConfig bị mất khi build

**Nguyên nhân**: Unity có thể strip unused assets

**Giải pháp**:
1. Đảm bảo ServerConfig được reference trong scene hoặc Resources
2. Kiểm tra Project Settings → Player → Other Settings → Strip Engine Code = OFF (nếu cần)
3. Sử dụng Resources folder (Unity luôn include Resources vào build)

## Best Practice

**Khuyến nghị**: Luôn đặt ServerConfig vào `Resources/Config/ServerConfig.asset`

- ✅ Tự động include vào build
- ✅ Không cần reference trong scene
- ✅ Hoạt động với runtime-created GameObjects
- ✅ Dễ quản lý và version control

## Cấu trúc thư mục đề xuất

```
Assets/
├── Resources/
│   └── Config/
│       └── ServerConfig.asset  ← Đặt ở đây
├── Scripts/
│   └── Config/
│       └── ServerConfig.cs     ← Script definition
└── ...
```

