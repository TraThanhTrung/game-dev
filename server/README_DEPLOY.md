# Game Server Linux Deployment Guide

Script tự động deploy Game Server lên Linux server với ASP.NET Core 10, SQL Server, Redis, và Nginx.

## Quick Start (Cho Demo)

### 1. Chọn Cloud Provider (Khuyến nghị: Oracle Cloud Free Tier)

- **Oracle Cloud**: Free forever, 1GB RAM, đủ cho demo 2-4 players
- **DigitalOcean**: $6/tháng, 1GB RAM
- Xem chi tiết: [SERVER_CONFIG_DEMO.md](SERVER_CONFIG_DEMO.md)

### 2. Tạo Ubuntu 22.04 Server

- Mở ports: 22 (SSH), 80 (HTTP)

### 3. Deploy

```bash
# SSH vào server
ssh ubuntu@YOUR_SERVER_IP

# Clone repo
git clone YOUR_REPO_URL game-dev
cd game-dev/server

# Set variables
export GITHUB_REPO_URL='YOUR_REPO_URL'
export SQL_SA_PASSWORD='YourStrong@Password123'

# Tối ưu (nếu server 1GB RAM)
chmod +x optimize-for-1gb.sh
sudo ./optimize-for-1gb.sh optimize

# Deploy
chmod +x deploy.sh
sudo ./deploy.sh install
```

### 4. Test

```bash
# Check status
sudo ./deploy.sh status

# Test từ browser
curl http://YOUR_SERVER_IP
```

## Yêu cầu

- Ubuntu 20.04+ hoặc Debian 11+ (hoặc các distro Linux tương thích)
- Quyền root/sudo
- Kết nối Internet để download dependencies và clone repository

## Cấu hình trước khi chạy

### 1. Set Environment Variables

```bash
export GITHUB_REPO_URL='https://github.com/yourusername/yourrepo.git'
export GITHUB_BRANCH='main'  # Optional, default: main
export SQL_SA_PASSWORD='YourStrong@Password123'  # Required for database setup
```

### 2. Chạy script

```bash
# Clone hoặc download script
cd server

# Make script executable (nếu chưa có)
chmod +x deploy.sh

# Chạy installation
sudo ./deploy.sh install
```

## Các lệnh có sẵn

```bash
sudo ./deploy.sh install    # Cài đặt đầy đủ (chạy lần đầu)
sudo ./deploy.sh update     # Update code và rebuild
sudo ./deploy.sh start      # Khởi động service
sudo ./deploy.sh stop       # Dừng service
sudo ./deploy.sh restart    # Khởi động lại service
sudo ./deploy.sh status     # Xem trạng thái các services
```

## Cấu trúc sau khi cài đặt

```
/opt/game-server/              # Source code từ GitHub
/opt/game-server-published/    # Published application
/var/log/game-server/          # Log files
/etc/systemd/system/game-server.service  # Systemd service
/etc/nginx/sites-available/game-server  # Nginx config
```

## Kiểm tra

### 1. Kiểm tra service status

```bash
sudo systemctl status game-server
sudo systemctl status nginx
sudo systemctl status mssql-server
sudo systemctl status redis-server
```

### 2. Xem logs

```bash
# Application logs
sudo journalctl -u game-server -f

# Nginx logs
sudo tail -f /var/log/nginx/access.log
sudo tail -f /var/log/nginx/error.log
```

### 3. Kiểm tra kết nối

```bash
# Lấy IP server
hostname -I

# Test từ browser hoặc curl
curl http://YOUR_SERVER_IP
```

## Tối ưu hóa cho Server 1GB RAM

Nếu server chỉ có 1GB RAM (như Oracle Cloud Free Tier), chạy script tối ưu:

```bash
cd server
chmod +x optimize-for-1gb.sh
sudo ./optimize-for-1gb.sh optimize
```

Script này sẽ:
- Tạo 2GB swap file
- Giới hạn SQL Server memory = 512MB
- Giới hạn Redis memory = 128MB
- Tắt các services không cần thiết
- Giới hạn journal logs = 100MB

Xem status:
```bash
sudo ./optimize-for-1gb.sh status
```

**Lưu ý:** Nên chạy script tối ưu TRƯỚC khi deploy application nếu server chỉ có 1GB RAM.

Xem chi tiết: [SERVER_CONFIG_DEMO.md](SERVER_CONFIG_DEMO.md)

## Cấu hình thủ công (nếu cần)

### SQL Server

Nếu chưa set `SQL_SA_PASSWORD` trước khi chạy script, có thể setup thủ công:

```bash
sudo /opt/mssql/bin/mssql-conf setup
```

### Database Connection String

Connection string được tự động tạo trong `appsettings.Production.json`:

```
Server=localhost;Database=GameServerDb;User Id=sa;Password=YOUR_PASSWORD;TrustServerCertificate=True;
```

### Redis

Redis mặc định chạy tại `localhost:6379`. Nếu cần thay đổi, sửa trong `appsettings.Production.json`.

### Nginx Port

Mặc định Nginx listen trên port 80. Để thay đổi, sửa biến `NGINX_PORT` trong `deploy.sh` hoặc sửa file config tại `/etc/nginx/sites-available/game-server`.

### Application Port

Mặc định ứng dụng chạy trên port 5220 (internal). Để thay đổi, sửa biến `APP_PORT` trong `deploy.sh` và update Nginx config tương ứng.

## Troubleshooting

### Service không start

```bash
# Kiểm tra logs
sudo journalctl -u game-server -n 50

# Kiểm tra permissions
sudo ls -la /opt/game-server-published
sudo ls -la /opt/game-server/shared
```

### Database connection failed

```bash
# Kiểm tra SQL Server
sudo systemctl status mssql-server
sudo /opt/mssql/bin/mssql-conf validate

# Test connection
sqlcmd -S localhost -U sa -P 'YOUR_PASSWORD' -Q "SELECT @@VERSION"
```

### Nginx không proxy được

```bash
# Test Nginx config
sudo nginx -t

# Kiểm tra Nginx logs
sudo tail -f /var/log/nginx/error.log

# Kiểm tra app có chạy không
curl http://localhost:5220
```

### Permission issues

```bash
# Fix permissions
sudo chown -R gameserver:gameserver /opt/game-server-published
sudo chown -R gameserver:gameserver /opt/game-server/shared
sudo chown -R gameserver:gameserver /var/log/game-server
```

## Security Notes

- Script tạo user `gameserver` với quyền hạn chế để chạy application
- SQL Server SA password nên được set mạnh
- Firewall chỉ mở port 80 (HTTP) và 5220 (direct access, optional)
- Không có SSL/HTTPS (theo yêu cầu), chỉ HTTP qua IP

## Update Application

Khi có code mới trên GitHub:

```bash
sudo ./deploy.sh update
```

Script sẽ:
1. Pull latest code từ GitHub
2. Rebuild application
3. Restart service

## Uninstall (nếu cần)

```bash
# Stop và disable services
sudo systemctl stop game-server
sudo systemctl disable game-server
sudo systemctl stop nginx
sudo systemctl stop mssql-server
sudo systemctl stop redis-server

# Remove files
sudo rm -rf /opt/game-server
sudo rm -rf /opt/game-server-published
sudo rm -f /etc/systemd/system/game-server.service
sudo rm -f /etc/nginx/sites-available/game-server
sudo rm -f /etc/nginx/sites-enabled/game-server

# Reload systemd và nginx
sudo systemctl daemon-reload
sudo systemctl reload nginx
```

