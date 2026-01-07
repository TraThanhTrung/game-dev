# Hướng dẫn cấu hình Google OAuth

## Lỗi: redirect_uri_mismatch

Lỗi này xảy ra khi redirect URI trong code không khớp với URI đã đăng ký trong Google Cloud Console.

## Cách sửa:

### 1. Truy cập Google Cloud Console

- Vào: https://console.cloud.google.com/
- Chọn project của bạn

### 2. Vào OAuth 2.0 Client IDs

- APIs & Services → Credentials
- Tìm OAuth 2.0 Client ID của bạn (Client ID: `391831042184-duafgo6inqp4r13gv546g3doh28rjo1o.apps.googleusercontent.com`)
- Click vào để chỉnh sửa

### 3. Thêm Authorized redirect URIs

Thêm các URI sau vào phần **"Authorized redirect URIs"**:

**Cho Development (localhost):**

```
http://localhost:5220/signin-google
```

**Cho Production (nếu có):**

```
https://yourdomain.com/signin-google
```

### 4. Lưu ý quan trọng:

- URI phải khớp **chính xác** (bao gồm protocol, domain, port, và path)
- Không có trailing slash (`/`) ở cuối
- Phân biệt chữ hoa/thường
- Phải có `http://` hoặc `https://` ở đầu

### 5. Sau khi cấu hình:

- Lưu thay đổi trong Google Cloud Console
- Đợi vài phút để thay đổi có hiệu lực
- Thử lại đăng nhập bằng Gmail

## Kiểm tra cấu hình hiện tại:

Trong code, callback path được cấu hình là:

- **Program.cs**: `options.CallbackPath = "/signin-google"` (default của Google middleware)
- **AuthController**: Route `[HttpGet("signin-google")]` để xử lý sau khi middleware authenticate

Vậy redirect URI đầy đủ sẽ là:

- Development: `http://localhost:5220/signin-google`
- Production: `https://yourdomain.com/signin-google`

## Nếu vẫn gặp lỗi:

1. Kiểm tra lại URI trong Google Cloud Console có khớp chính xác không
2. Đảm bảo server đang chạy trên đúng port (5220)
3. Kiểm tra log của server để xem redirect URI thực tế được sử dụng
4. Thử xóa cache trình duyệt và thử lại
