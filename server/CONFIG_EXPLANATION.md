# Configuration Files và Connection String

## Thứ tự load config trong ASP.NET Core

ASP.NET Core load configuration files theo thứ tự sau (file sau override file trước):

1. **appsettings.json** (base configuration)
2. **appsettings.{Environment}.json** (environment-specific, override base)

Với `ASPNETCORE_ENVIRONMENT=Production`, thứ tự sẽ là:

1. `appsettings.json`
2. `appsettings.Production.json` (override)

## Connection String được dùng khi deploy

Khi deploy trên Linux server:

### Environment

- `ASPNETCORE_ENVIRONMENT=Production` (set trong systemd service)

### Files được load

1. `/opt/game-server/server/appsettings.json`
2. `/opt/game-server/server/appsettings.Production.json` (override)

### Connection String cuối cùng

**Nếu `appsettings.Production.json` tồn tại:**

```json
{
  "ConnectionStrings": {
    "GameDb": "Server=localhost;Database=GameServerDb;User Id=sa;Password=YOUR_PASSWORD;TrustServerCertificate=True;"
  }
}
```

**Nếu `appsettings.Production.json` KHÔNG tồn tại:**
Sẽ dùng từ `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "GameDb": "Server=localhost;Database=GameServerDb;Trusted_Connection=True;TrustServerCertificate=True;"
  }
}
```

⚠️ **Vấn đề:** `Trusted_Connection=True` là Windows Authentication, **KHÔNG hoạt động trên Linux**. Cần dùng `User Id` và `Password`.

## Script deploy.sh tự động tạo appsettings.Production.json

Script `deploy.sh` sẽ tự động tạo `appsettings.Production.json` với connection string đúng cho Linux:

```bash
# Trong function configure_appsettings()
cat > "$PROD_SETTINGS" <<EOF
{
  "ConnectionStrings": {
    "GameDb": "Server=localhost;Database=$DB_NAME;User Id=$DB_USER;Password=$DB_PASSWORD;TrustServerCertificate=True;",
    "Redis": "$REDIS_HOST:$REDIS_PORT"
  }
}
EOF
```

## Kiểm tra connection string đang dùng

### Cách 1: Xem file trực tiếp

```bash
# Xem appsettings.Production.json
cat /opt/game-server/server/appsettings.Production.json

# Hoặc trong published app
cat /opt/game-server-published/appsettings.Production.json
```

### Cách 2: Xem từ application logs

```bash
# Application sẽ log connection string khi start (nếu có logging)
sudo journalctl -u game-server | grep -i "connection"
```

### Cách 3: Check environment variable

```bash
# Xem systemd service config
cat /etc/systemd/system/game-server.service | grep Environment
```

## Update connection string

### Cách 1: Dùng script (khuyến nghị)

```bash
export SQL_SA_PASSWORD='YourNewPassword123'
chmod +x update-db-password.sh
sudo ./update-db-password.sh
```

### Cách 2: Sửa thủ công

```bash
sudo nano /opt/game-server/server/appsettings.Production.json
# Sửa password trong connection string
# Sau đó rebuild và restart
sudo ./deploy.sh update
```

### Cách 3: Re-run configure

```bash
export SQL_SA_PASSWORD='YourNewPassword123'
cd /opt/game-server/server
# Script sẽ update appsettings.Production.json
sudo ../../deploy.sh update
```

## Lưu ý quan trọng

1. **appsettings.json** có `Trusted_Connection=True` - chỉ dùng cho Windows/Development
2. **appsettings.Production.json** được tạo tự động với `User Id` và `Password` - dùng cho Linux Production
3. Nếu `appsettings.Production.json` không tồn tại, application sẽ dùng `appsettings.json` → **SẼ FAIL** trên Linux
4. Password trong connection string phải match với password đã setup trong SQL Server

## Troubleshooting

### Lỗi: "Login failed for user 'sa'"

**Nguyên nhân:** Password trong `appsettings.Production.json` không đúng

**Giải pháp:**

```bash
# 1. Reset SQL Server password
export SQL_SA_PASSWORD='NewPassword123'
sudo ./fix-sqlserver.sh reset-password

# 2. Update appsettings
sudo ./update-db-password.sh

# 3. Restart service
sudo ./deploy.sh restart
```

### Lỗi: "A network-related error occurred"

**Nguyên nhân:** SQL Server không chạy hoặc connection string sai

**Giải pháp:**

```bash
# 1. Check SQL Server
sudo systemctl status mssql-server

# 2. Fix SQL Server nếu cần
export SQL_SA_PASSWORD='YourPassword123'
sudo ./fix-sqlserver.sh fix

# 3. Check connection string
cat /opt/game-server/server/appsettings.Production.json
```



