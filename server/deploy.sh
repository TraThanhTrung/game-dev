#!/bin/bash

###############################################################################
# Game Server Linux Deployment Script
# Deploys ASP.NET Core 10.0 application with SQL Server, Redis, and Nginx
###############################################################################

set -e  # Exit on error

###############################################################################
# Configuration Variables
###############################################################################

# GitHub Repository (user must set this)
GITHUB_REPO_URL="${GITHUB_REPO_URL:-}"
GITHUB_BRANCH="${GITHUB_BRANCH:-main}"

# Installation paths
INSTALL_DIR="/opt/game-server"
PUBLISH_DIR="/opt/game-server-published"
LOG_DIR="/var/log/game-server"

# Application settings
APP_NAME="game-server"
APP_USER="gameserver"
APP_PORT=5220
NGINX_PORT=80

# Database settings
DB_NAME="GameServerDb"
DB_USER="sa"
DB_PASSWORD="${SQL_SA_PASSWORD:-}"

# Redis settings
REDIS_HOST="localhost"
REDIS_PORT=6379

# Service settings
SERVICE_FILE="/etc/systemd/system/${APP_NAME}.service"
NGINX_CONFIG="/etc/nginx/sites-available/${APP_NAME}"
NGINX_ENABLED="/etc/nginx/sites-enabled/${APP_NAME}"

###############################################################################
# Colors for output
###############################################################################

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

###############################################################################
# Helper Functions
###############################################################################

log_info() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

log_warn() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

log_step() {
    echo -e "${BLUE}[STEP]${NC} $1"
}

check_root() {
    if [ "$EUID" -ne 0 ]; then
        log_error "This script must be run as root or with sudo"
        exit 1
    fi
}

check_command() {
    if ! command -v "$1" &> /dev/null; then
        return 1
    fi
    return 0
}

###############################################################################
# Installation Functions
###############################################################################

install_dotnet() {
    log_step "Installing .NET 10 SDK..."
    
    if check_command dotnet; then
        DOTNET_VERSION=$(dotnet --version | cut -d'.' -f1)
        if [ "$DOTNET_VERSION" -ge 10 ]; then
            log_info ".NET 10+ already installed: $(dotnet --version)"
            return 0
        fi
    fi
    
    # Add Microsoft package repository
    wget https://packages.microsoft.com/config/ubuntu/$(lsb_release -rs)/packages-microsoft-prod.deb -O /tmp/packages-microsoft-prod.deb
    dpkg -i /tmp/packages-microsoft-prod.deb
    rm /tmp/packages-microsoft-prod.deb
    
    # Update package list
    apt-get update
    
    # Install .NET 10 SDK
    apt-get install -y dotnet-sdk-10.0
    
    log_info ".NET SDK installed: $(dotnet --version)"
}

install_sqlserver() {
    log_step "Installing SQL Server..."
    
    if systemctl is-active --quiet mssql-server 2>/dev/null; then
        log_info "SQL Server is already installed and running"
        return 0
    fi
    
    if [ -f /opt/mssql/bin/mssql-conf ]; then
        log_info "SQL Server is installed but not running"
        systemctl start mssql-server || true
        return 0
    fi
    
    # Add Microsoft SQL Server repository
    UBUNTU_VERSION=$(lsb_release -rs)
    curl -o /tmp/mssql-server-2022.gpg https://packages.microsoft.com/keys/microsoft.asc
    gpg --dearmor /tmp/mssql-server-2022.gpg
    mv /tmp/mssql-server-2022.gpg.gpg /etc/apt/trusted.gpg.d/mssql-server-2022.gpg
    
    # Add repository - handle different Ubuntu versions
    REPO_URL="https://packages.microsoft.com/config/ubuntu/${UBUNTU_VERSION}/mssql-server-2022.list"
    if curl -f "$REPO_URL" > /dev/null 2>&1; then
        curl "$REPO_URL" | tee /etc/apt/sources.list.d/mssql-server-2022.list
    else
        log_warn "Official repo not available for Ubuntu ${UBUNTU_VERSION}, trying alternative method"
        echo "deb [arch=amd64,arm64,armhf signed-by=/etc/apt/trusted.gpg.d/mssql-server-2022.gpg] https://packages.microsoft.com/ubuntu/${UBUNTU_VERSION}/prod $(lsb_release -cs) main" | tee /etc/apt/sources.list.d/mssql-server-2022.list
    fi
    
    apt-get update
    
    # Install SQL Server
    apt-get install -y mssql-server
    
    # Configure SQL Server
    if [ -z "$DB_PASSWORD" ]; then
        log_warn "SQL_SA_PASSWORD not set. Please set it before running SQL Server setup."
        log_warn "Run: sudo /opt/mssql/bin/mssql-conf setup"
        log_warn "Or set SQL_SA_PASSWORD environment variable and re-run this script"
    else
        ACCEPT_EULA=Y MSSQL_SA_PASSWORD="$DB_PASSWORD" /opt/mssql/bin/mssql-conf setup accept-eula
    fi
    
    # Start and enable SQL Server
    systemctl enable mssql-server
    systemctl start mssql-server
    
    # Wait for SQL Server to be ready
    log_info "Waiting for SQL Server to start..."
    sleep 10
    
    log_info "SQL Server installed and started"
}

install_redis() {
    log_step "Installing Redis..."
    
    if systemctl is-active --quiet redis-server 2>/dev/null; then
        log_info "Redis is already installed and running"
        return 0
    fi
    
    apt-get update
    apt-get install -y redis-server
    
    # Configure Redis
    sed -i 's/^supervised no/supervised systemd/' /etc/redis/redis.conf
    sed -i 's/^# bind 127.0.0.1/bind 127.0.0.1/' /etc/redis/redis.conf
    
    # Start and enable Redis
    systemctl enable redis-server
    systemctl start redis-server
    
    log_info "Redis installed and started"
}

install_nginx() {
    log_step "Installing Nginx..."
    
    if check_command nginx; then
        log_info "Nginx is already installed"
        return 0
    fi
    
    apt-get update
    apt-get install -y nginx
    
    # Start and enable Nginx
    systemctl enable nginx
    systemctl start nginx
    
    log_info "Nginx installed and started"
}

install_git() {
    log_step "Installing Git..."
    
    if check_command git; then
        log_info "Git is already installed: $(git --version)"
        return 0
    fi
    
    apt-get update
    apt-get install -y git
    
    log_info "Git installed"
}

install_dependencies() {
    log_step "Installing all dependencies..."
    
    # Update package list
    apt-get update
    apt-get install -y curl wget gnupg lsb-release software-properties-common
    
    install_dotnet
    install_sqlserver
    install_redis
    install_nginx
    install_git
    
    log_info "All dependencies installed"
}

###############################################################################
# Repository Functions
###############################################################################

clone_repository() {
    log_step "Cloning/Updating repository..."
    
    if [ -z "$GITHUB_REPO_URL" ]; then
        log_error "GITHUB_REPO_URL is not set!"
        log_error "Please set it: export GITHUB_REPO_URL='https://github.com/yourusername/yourrepo.git'"
        exit 1
    fi
    
    if [ -d "$INSTALL_DIR" ]; then
        log_info "Repository exists, pulling latest changes..."
        cd "$INSTALL_DIR"
        git fetch origin
        git checkout "$GITHUB_BRANCH"
        git pull origin "$GITHUB_BRANCH"
    else
        log_info "Cloning repository..."
        mkdir -p "$(dirname $INSTALL_DIR)"
        git clone -b "$GITHUB_BRANCH" "$GITHUB_REPO_URL" "$INSTALL_DIR"
    fi
    
    log_info "Repository ready at $INSTALL_DIR"
}

###############################################################################
# Database Functions
###############################################################################

setup_database() {
    log_step "Setting up database..."
    
    if [ -z "$DB_PASSWORD" ]; then
        log_warn "SQL_SA_PASSWORD not set. Skipping database setup."
        log_warn "Please configure database manually or set SQL_SA_PASSWORD"
        return 0
    fi
    
    # Install SQL Server command-line tools if not present
    if ! check_command sqlcmd; then
        log_info "Installing SQL Server command-line tools..."
        
        # Install ODBC driver
        UBUNTU_VERSION=$(lsb_release -rs)
        ARCH=$(dpkg --print-architecture)
        
        # Try to install from Microsoft repository
        if ! apt-get install -y msodbcsql18 unixodbc-dev 2>/dev/null; then
            log_warn "Could not install ODBC driver from repo. You may need to install manually."
            log_warn "See: https://docs.microsoft.com/en-us/sql/connect/odbc/linux-mac/install-microsoft-odbc-driver-sql-server-macos"
        fi
        
        # Install SQL Server tools
        if ! apt-get install -y mssql-tools18 unixodbc-dev 2>/dev/null; then
            log_warn "Could not install SQL Server tools from repo. Database setup may fail."
            log_warn "You can create the database manually using SQL Server Management Studio or Azure Data Studio"
        fi
        
        # Add tools to PATH
        if [ -d /opt/mssql-tools18/bin ]; then
            echo 'export PATH="$PATH:/opt/mssql-tools18/bin"' >> /etc/profile
        fi
    fi
    
    # Wait for SQL Server to be ready
    log_info "Waiting for SQL Server to be ready..."
    MAX_RETRIES=30
    RETRY_COUNT=0
    SQL_READY=0
    
    while [ $RETRY_COUNT -lt $MAX_RETRIES ]; do
        if sqlcmd -S localhost -U sa -P "$DB_PASSWORD" -Q "SELECT 1" -C -b 2>/dev/null; then
            SQL_READY=1
            break
        fi
        RETRY_COUNT=$((RETRY_COUNT + 1))
        sleep 2
    done
    
    if [ $SQL_READY -eq 0 ]; then
        log_warn "SQL Server is not responding. Database setup skipped."
        log_warn "Please ensure SQL Server is running and try again."
        return 0
    fi
    
    # Create database if it doesn't exist
    log_info "Creating database $DB_NAME..."
    sqlcmd -S localhost -U sa -P "$DB_PASSWORD" -C -Q "IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = '$DB_NAME') CREATE DATABASE [$DB_NAME];" -b || {
        log_warn "Failed to create database. It may already exist."
    }
    
    log_info "Database setup complete"
}

###############################################################################
# Application Configuration
###############################################################################

create_app_user() {
    log_step "Creating application user..."
    
    if id "$APP_USER" &>/dev/null; then
        log_info "User $APP_USER already exists"
    else
        useradd -r -s /bin/false -d "$INSTALL_DIR" "$APP_USER"
        log_info "User $APP_USER created"
    fi
}

configure_appsettings() {
    log_step "Configuring appsettings.json..."
    
    local APP_SETTINGS="$INSTALL_DIR/server/appsettings.json"
    local PROD_SETTINGS="$INSTALL_DIR/server/appsettings.Production.json"
    
    # Create production settings if it doesn't exist
    if [ ! -f "$PROD_SETTINGS" ]; then
        log_info "Creating appsettings.Production.json..."
        cat > "$PROD_SETTINGS" <<EOF
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.AspNetCore.Hosting.Diagnostics": "Warning"
    }
  },
  "ConnectionStrings": {
    "GameDb": "Server=localhost;Database=$DB_NAME;User Id=$DB_USER;Password=$DB_PASSWORD;TrustServerCertificate=True;",
    "Redis": "$REDIS_HOST:$REDIS_PORT"
  },
  "Authentication": {
    "Google": {
      "ClientId": "391831042184-duafgo6inqp4r13gv546g3doh28rjo1o.apps.googleusercontent.com",
      "ClientSecret": "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"
    }
  },
  "AllowedHosts": "*"
}
EOF
        log_info "Production settings created"
    else
        log_info "Production settings already exists"
    fi
}

###############################################################################
# Build and Publish
###############################################################################

build_application() {
    log_step "Building and publishing application..."
    
    cd "$INSTALL_DIR/server"
    
    # Restore packages
    log_info "Restoring NuGet packages..."
    dotnet restore
    
    # Publish application
    log_info "Publishing application..."
    dotnet publish -c Release -o "$PUBLISH_DIR" \
        -p:PublishSingleFile=false \
        -p:IncludeNativeLibrariesForSelfExtract=true
    
    # Set permissions
    chown -R "$APP_USER:$APP_USER" "$PUBLISH_DIR"
    
    # Ensure shared folder is accessible
    if [ -d "$INSTALL_DIR/shared" ]; then
        chown -R "$APP_USER:$APP_USER" "$INSTALL_DIR/shared"
        log_info "Shared folder permissions set"
    else
        log_warn "Shared folder not found at $INSTALL_DIR/shared"
        log_warn "Make sure the repository contains the shared/ directory"
    fi
    
    log_info "Application published to $PUBLISH_DIR"
}

###############################################################################
# Systemd Service
###############################################################################

create_systemd_service() {
    log_step "Creating systemd service..."
    
    # Get server IP address
    SERVER_IP=$(hostname -I | awk '{print $1}')
    
    cat > "$SERVICE_FILE" <<EOF
[Unit]
Description=Game Server ASP.NET Core Application
After=network.target mssql-server.service redis-server.service

[Service]
Type=notify
User=$APP_USER
Group=$APP_USER
WorkingDirectory=$PUBLISH_DIR
ExecStart=/usr/bin/dotnet $PUBLISH_DIR/GameServer.dll
Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=$APP_NAME

# Environment variables
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://localhost:$APP_PORT
Environment=GAME_CONFIG_PATH=$INSTALL_DIR/shared/game-config.json

# Logging
StandardOutput=journal
StandardError=journal

# Security
NoNewPrivileges=true
PrivateTmp=true

[Install]
WantedBy=multi-user.target
EOF
    
    # Reload systemd
    systemctl daemon-reload
    
    log_info "Systemd service created at $SERVICE_FILE"
}

###############################################################################
# Nginx Configuration
###############################################################################

configure_nginx() {
    log_step "Configuring Nginx..."
    
    # Get server IP address
    SERVER_IP=$(hostname -I | awk '{print $1}')
    
    cat > "$NGINX_CONFIG" <<EOF
server {
    listen $NGINX_PORT;
    server_name $SERVER_IP _;

    # Increase timeouts for long-running requests
    proxy_connect_timeout 600s;
    proxy_send_timeout 600s;
    proxy_read_timeout 600s;
    send_timeout 600s;

    # Increase buffer sizes
    client_max_body_size 50M;
    proxy_buffer_size 128k;
    proxy_buffers 4 256k;
    proxy_busy_buffers_size 256k;

    # Static files
    location / {
        proxy_pass http://localhost:$APP_PORT;
        proxy_http_version 1.1;
        proxy_set_header Upgrade \$http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host \$host;
        proxy_set_header X-Real-IP \$remote_addr;
        proxy_set_header X-Forwarded-For \$proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto \$scheme;
        proxy_cache_bypass \$http_upgrade;
    }

    # WebSocket support (if needed in future)
    location /ws {
        proxy_pass http://localhost:$APP_PORT;
        proxy_http_version 1.1;
        proxy_set_header Upgrade \$http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host \$host;
        proxy_set_header X-Real-IP \$remote_addr;
        proxy_set_header X-Forwarded-For \$proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto \$scheme;
    }
}
EOF
    
    # Enable site
    ln -sf "$NGINX_CONFIG" "$NGINX_ENABLED"
    
    # Test Nginx configuration
    nginx -t
    
    # Reload Nginx
    systemctl reload nginx
    
    log_info "Nginx configured and reloaded"
    log_info "Server will be accessible at http://$SERVER_IP"
}

###############################################################################
# Firewall Configuration
###############################################################################

configure_firewall() {
    log_step "Configuring firewall..."
    
    if check_command ufw; then
        log_info "Configuring UFW firewall..."
        ufw allow $NGINX_PORT/tcp comment "Game Server HTTP"
        ufw allow $APP_PORT/tcp comment "Game Server Direct" 2>/dev/null || true
        log_info "Firewall rules added"
    elif check_command firewall-cmd; then
        log_info "Configuring firewalld..."
        firewall-cmd --permanent --add-port=$NGINX_PORT/tcp
        firewall-cmd --permanent --add-port=$APP_PORT/tcp
        firewall-cmd --reload
        log_info "Firewall rules added"
    else
        log_warn "No firewall tool detected (ufw/firewalld). Please configure manually."
    fi
}

###############################################################################
# Log Directory
###############################################################################

create_log_directory() {
    log_step "Creating log directory..."
    
    mkdir -p "$LOG_DIR"
    chown -R "$APP_USER:$APP_USER" "$LOG_DIR"
    
    log_info "Log directory created at $LOG_DIR"
}

###############################################################################
# Service Management
###############################################################################

start_service() {
    log_step "Starting $APP_NAME service..."
    systemctl enable "$APP_NAME"
    systemctl start "$APP_NAME"
    sleep 3
    systemctl status "$APP_NAME" --no-pager || true
    log_info "Service started"
}

stop_service() {
    log_step "Stopping $APP_NAME service..."
    systemctl stop "$APP_NAME" || true
    log_info "Service stopped"
}

restart_service() {
    log_step "Restarting $APP_NAME service..."
    systemctl restart "$APP_NAME"
    sleep 3
    systemctl status "$APP_NAME" --no-pager || true
    log_info "Service restarted"
}

show_status() {
    log_step "Service Status:"
    systemctl status "$APP_NAME" --no-pager || true
    
    echo ""
    log_step "Nginx Status:"
    systemctl status nginx --no-pager || true
    
    echo ""
    log_step "SQL Server Status:"
    systemctl status mssql-server --no-pager || true
    
    echo ""
    log_step "Redis Status:"
    systemctl status redis-server --no-pager || true
    
    echo ""
    SERVER_IP=$(hostname -I | awk '{print $1}')
    log_info "Server accessible at: http://$SERVER_IP"
}

###############################################################################
# Main Installation
###############################################################################

full_install() {
    log_info "Starting full installation..."
    
    check_root
    
    install_dependencies
    clone_repository
    create_app_user
    create_log_directory
    setup_database
    configure_appsettings
    build_application
    create_systemd_service
    configure_nginx
    configure_firewall
    start_service
    
    log_info ""
    log_info "=========================================="
    log_info "Installation completed successfully!"
    log_info "=========================================="
    SERVER_IP=$(hostname -I | awk '{print $1}')
    log_info "Server URL: http://$SERVER_IP"
    log_info "Service: $APP_NAME"
    log_info "Logs: journalctl -u $APP_NAME -f"
    log_info "=========================================="
}

update_application() {
    log_info "Updating application..."
    
    check_root
    
    stop_service
    clone_repository
    build_application
    start_service
    
    log_info "Update completed"
}

###############################################################################
# Main Script
###############################################################################

case "${1:-}" in
    install)
        full_install
        ;;
    update)
        update_application
        ;;
    start)
        check_root
        start_service
        ;;
    stop)
        check_root
        stop_service
        ;;
    restart)
        check_root
        restart_service
        ;;
    status)
        show_status
        ;;
    *)
        echo "Usage: $0 {install|update|start|stop|restart|status}"
        echo ""
        echo "Commands:"
        echo "  install  - Full installation (run this first)"
        echo "  update   - Update code and rebuild application"
        echo "  start    - Start the service"
        echo "  stop     - Stop the service"
        echo "  restart  - Restart the service"
        echo "  status   - Show service status"
        echo ""
        echo "Environment Variables:"
        echo "  GITHUB_REPO_URL  - GitHub repository URL (required)"
        echo "  GITHUB_BRANCH    - Branch to deploy (default: main)"
        echo "  SQL_SA_PASSWORD  - SQL Server SA password (required for DB setup)"
        echo ""
        echo "Example:"
        echo "  export GITHUB_REPO_URL='https://github.com/user/repo.git'"
        echo "  export SQL_SA_PASSWORD='YourStrong@Password123'"
        echo "  sudo ./deploy.sh install"
        exit 1
        ;;
esac

