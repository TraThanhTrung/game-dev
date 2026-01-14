# Quick Start Guide - Demo Server

Hướng dẫn nhanh để setup server cho demo trong 15 phút.

## Bước 1: Tạo Server (5 phút)

### Option A: Oracle Cloud (Free Forever) - Khuyến nghị

1. Đăng ký tại: https://www.oracle.com/cloud/free/
2. Tạo VM Instance:
   - Image: Ubuntu 22.04
   - Shape: VM.Standard.E2.1.Micro (Always Free)
   - 1 OCPU, 1GB RAM
3. Mở ports: 22 (SSH), 80 (HTTP)
4. Lưu SSH private key

### Option B: DigitalOcean ($6/tháng)

1. Đăng ký tại: https://www.digitalocean.com/
2. Tạo Droplet:
   - Image: Ubuntu 22.04
   - Plan: Basic $6/month (1GB RAM)
3. Mở ports: 22, 80

## Bước 2: SSH vào Server (1 phút)

```bash
# Windows (PowerShell)
ssh -i path/to/private-key ubuntu@YOUR_SERVER_IP

# Linux/Mac
ssh -i ~/.ssh/private-key ubuntu@YOUR_SERVER_IP
```

## Bước 3: Clone Repository (2 phút)

```bash
# Clone repo
git clone https://github.com/YOUR_USERNAME/YOUR_REPO.git game-dev
cd game-dev/server
```

Hoặc nếu repo private, dùng SSH:
```bash
git clone git@github.com:YOUR_USERNAME/YOUR_REPO.git game-dev
cd game-dev/server
```

## Bước 4: Tối Ưu Server (Nếu 1GB RAM) (2 phút)

```bash
chmod +x optimize-for-1gb.sh
sudo ./optimize-for-1gb.sh optimize
```

**Bỏ qua bước này nếu server có >= 2GB RAM**

## Bước 5: Deploy Application (5 phút)

```bash
# Set environment variables
export GITHUB_REPO_URL='https://github.com/YOUR_USERNAME/YOUR_REPO.git'
export GITHUB_BRANCH='main'
export SQL_SA_PASSWORD='YourStrong@Password123'  # Đổi password mạnh!

# Deploy
chmod +x deploy.sh
sudo ./deploy.sh install
```

Script sẽ tự động:
- Cài .NET 10, SQL Server, Redis, Nginx
- Clone code từ GitHub
- Setup database
- Build và publish app
- Tạo systemd service
- Cấu hình Nginx
- Start tất cả services

## Bước 6: Kiểm Tra (1 phút)

```bash
# Check status
sudo ./deploy.sh status

# Xem logs
sudo journalctl -u game-server -f

# Test từ browser
curl http://YOUR_SERVER_IP
```

## Kết Quả

Server sẽ chạy tại: `http://YOUR_SERVER_IP`

- Game API: `http://YOUR_SERVER_IP/api/...`
- Admin Panel: `http://YOUR_SERVER_IP/Admin`
- Player Portal: `http://YOUR_SERVER_IP/Player`

## Troubleshooting

### Service không start

```bash
# Check logs
sudo journalctl -u game-server -n 50

# Check SQL Server
sudo systemctl status mssql-server

# Check Redis
sudo systemctl status redis-server
```

### Out of Memory

```bash
# Check memory
free -h

# Nếu hết RAM, chạy lại optimize script
sudo ./optimize-for-1gb.sh optimize
```

### Database connection failed

```bash
# Test SQL Server
sqlcmd -S localhost -U sa -P 'YOUR_PASSWORD' -Q "SELECT @@VERSION"

# Nếu fail, restart SQL Server
sudo systemctl restart mssql-server
```

## Các Lệnh Hữu Ích

```bash
# Start/Stop/Restart
sudo ./deploy.sh start
sudo ./deploy.sh stop
sudo ./deploy.sh restart

# Update code
sudo ./deploy.sh update

# Check status
sudo ./deploy.sh status

# View logs
sudo journalctl -u game-server -f
sudo journalctl -u nginx -f
```

## Dọn Dẹp (Khi Không Dùng)

```bash
# Stop services
sudo ./deploy.sh stop

# Backup database (nếu cần)
sudo -u gameserver sqlcmd -S localhost -U sa -P 'PASSWORD' \
  -Q "BACKUP DATABASE GameServerDb TO DISK='/tmp/backup.bak'"

# Delete server từ cloud console
```

## Chi Phí

- **Oracle Cloud**: $0 (free forever)
- **DigitalOcean**: $6/tháng
- **AWS/GCP/Azure**: Free trial 30-90 ngày, sau đó ~$10-15/tháng

## Lưu Ý

1. **Đổi SQL SA Password**: Dùng password mạnh, không dùng mặc định
2. **Backup**: Backup database trước khi dỡ bỏ server
3. **Security**: Chỉ mở ports cần thiết (22, 80)
4. **Monitoring**: Check `free -h` thường xuyên để đảm bảo không hết RAM

---

**Tổng thời gian setup: ~15 phút**

Sau khi setup xong, server sẵn sàng cho demo!

