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
GITHUB_REPO_URL="https://github.com/TraThanhTrung/game-dev.git"
GITHUB_BRANCH="main"

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

stop_unattended_upgrades() {
    log_step "Stopping unattended-upgrades..."
    
    if systemctl is-active --quiet unattended-upgrades 2>/dev/null; then
        log_info "Stopping unattended-upgrades service..."
        systemctl stop unattended-upgrades
        log_info "unattended-upgrades stopped"
        return 0
    fi
    
    # Check if process is running even if service is stopped
    if pgrep -f unattended-upgrade > /dev/null; then
        log_info "Killing unattended-upgrade process..."
        pkill -f unattended-upgrade || true
        sleep 2
        log_info "unattended-upgrade process killed"
        return 0
    fi
    
    log_info "unattended-upgrades is not running"
    return 0
}

wait_for_dpkg_lock() {
    log_step "Checking for dpkg lock..."
    
    MAX_WAIT=60  # 1 minute max wait (reduced since we can stop unattended-upgrades)
    WAIT_TIME=0
    SLEEP_INTERVAL=3
    
    # First, try to stop unattended-upgrades if it's running
    if pgrep -f unattended-upgrade > /dev/null || systemctl is-active --quiet unattended-upgrades 2>/dev/null; then
        log_info "unattended-upgrades detected, stopping it..."
        stop_unattended_upgrades
        sleep 2  # Wait a bit for it to fully stop
    fi
    
    while [ $WAIT_TIME -lt $MAX_WAIT ]; do
        # Check if lock exists
        if [ -f /var/lib/dpkg/lock-frontend ] || [ -f /var/lib/dpkg/lock ]; then
            # Check if unattended-upgrades is running again
            if pgrep -f unattended-upgrade > /dev/null; then
                log_info "unattended-upgrades restarted, stopping again..."
                stop_unattended_upgrades
                sleep 2
                WAIT_TIME=$((WAIT_TIME + 2))
                continue
            fi
            
            # Check if apt/dpkg is running
            if pgrep -x apt-get > /dev/null || pgrep -x apt > /dev/null || pgrep -x dpkg > /dev/null; then
                log_info "apt/dpkg is running, waiting... (${WAIT_TIME}s/${MAX_WAIT}s)"
                sleep $SLEEP_INTERVAL
                WAIT_TIME=$((WAIT_TIME + SLEEP_INTERVAL))
                continue
            fi
            
            # Lock exists but no process, might be stale - try to remove it
            log_warn "Lock file exists but no process found. Attempting to remove stale lock..."
            # Only remove if we're sure no process is using it
            if ! lsof /var/lib/dpkg/lock-frontend > /dev/null 2>&1 && ! lsof /var/lib/dpkg/lock > /dev/null 2>&1; then
                rm -f /var/lib/dpkg/lock-frontend /var/lib/dpkg/lock /var/cache/apt/archives/lock 2>/dev/null || true
                log_info "Stale lock files removed"
                sleep 1
            else
                log_warn "Lock is in use, waiting a bit more..."
                sleep $SLEEP_INTERVAL
                WAIT_TIME=$((WAIT_TIME + SLEEP_INTERVAL))
            fi
            continue
        fi
        
        # No lock, we're good
        return 0
    done
    
    # Timeout reached
    log_error "Timeout waiting for dpkg lock. Another process may be using apt/dpkg."
    log_error "Please wait for unattended-upgrades or other apt processes to finish."
    log_error "Check with: ps aux | grep -E '(apt|dpkg|unattended)'"
    log_error "Or manually stop: sudo systemctl stop unattended-upgrades"
    return 1
}

check_dotnet_version() {
    log_step "Checking .NET SDK version..."
    
    # Determine DOTNET_ROOT - check apt installation first (Ubuntu package manager)
    DOTNET_ROOT_PATH=""
    if [ -d "/usr/share/dotnet" ] && [ -f "/usr/share/dotnet/dotnet" ]; then
        DOTNET_ROOT_PATH="/usr/share/dotnet"
        log_info "Found .NET SDK installed via apt at $DOTNET_ROOT_PATH"
    # Check snap packages (fallback)
    elif snap list dotnet-sdk-100 2>/dev/null | grep -q dotnet-sdk-100; then
        DOTNET_ROOT_PATH="/snap/dotnet-sdk-100/current"
    elif snap list dotnet-sdk-90 2>/dev/null | grep -q dotnet-sdk-90; then
        DOTNET_ROOT_PATH="/snap/dotnet-sdk-90/current"
    elif snap list dotnet-sdk 2>/dev/null | grep -q dotnet-sdk; then
        DOTNET_ROOT_PATH="/snap/dotnet-sdk/current"
    fi
    
    # Set DOTNET_ROOT environment variable (per Microsoft docs)
    if [ -n "$DOTNET_ROOT_PATH" ]; then
        export DOTNET_ROOT="$DOTNET_ROOT_PATH"
    fi
    
    # Ensure PATH includes dotnet locations
    if [ "$DOTNET_ROOT_PATH" = "/usr/share/dotnet" ]; then
        export PATH="/usr/share/dotnet:$PATH"
    else
        export PATH="/snap/bin:/usr/local/bin:$PATH"
    fi
    
    # Try to find dotnet command
    DOTNET_CMD=""
    # For apt installation
    if [ "$DOTNET_ROOT_PATH" = "/usr/share/dotnet" ]; then
        DOTNET_CMD="/usr/share/dotnet/dotnet"
    else
        # For snap installation - check symlink first
        for path in /usr/local/bin/dotnet /snap/bin/dotnet "$DOTNET_ROOT_PATH/dotnet"; do
            if [ -f "$path" ] && [ -x "$path" ]; then
                DOTNET_CMD="$path"
                break
            fi
        done
    fi
    
    # If not found, try which
    if [ -z "$DOTNET_CMD" ]; then
        DOTNET_CMD=$(which dotnet 2>/dev/null || echo "")
    fi
    
    if [ -z "$DOTNET_CMD" ]; then
        log_error ".NET SDK is not installed!"
        log_error "Please install .NET SDK:"
        log_error "  Ubuntu: sudo add-apt-repository ppa:dotnet/backports && sudo apt-get install -y dotnet-sdk-10.0"
        log_error "  Snap: sudo snap install dotnet-sdk-100 --classic"
        return 1
    fi
    
    DOTNET_VERSION=$($DOTNET_CMD --version 2>/dev/null)
    if [ -z "$DOTNET_VERSION" ]; then
        log_error "Failed to get .NET SDK version from $DOTNET_CMD"
        return 1
    fi
    
    log_info ".NET SDK version: $DOTNET_VERSION"
    
    # Check major version
    MAJOR_VERSION=$(echo $DOTNET_VERSION | cut -d'.' -f1)
    
    # Project targets net10.0, but can work with .NET 8+ (with warnings)
    if [ "$MAJOR_VERSION" -lt 8 ]; then
        log_error ".NET SDK version $DOTNET_VERSION is too old!"
        log_error "Project requires .NET 8.0 or higher"
        log_error "Please install: sudo snap install dotnet-sdk-100 --classic"
        log_error "Or update: sudo snap refresh dotnet-sdk"
        return 1
    elif [ "$MAJOR_VERSION" -lt 10 ]; then
        log_warn ".NET SDK version $DOTNET_VERSION is installed"
        log_warn "Project targets net10.0, but .NET $MAJOR_VERSION is installed"
        log_warn "This may cause build errors. Consider:"
        log_warn "  1. Install .NET 10: sudo snap install dotnet-sdk-100 --classic"
        log_warn "  2. Or update GameServer.csproj TargetFramework to net${MAJOR_VERSION}.0"
        log_warn "Continuing anyway..."
    else
        log_info ".NET SDK version is compatible (>= 10.0)"
    fi
    
    # Show dotnet path
    log_info "Dotnet path: $DOTNET_CMD"
    
    # Verify dotnet can run
    if ! $DOTNET_CMD --info > /dev/null 2>&1; then
        log_error ".NET SDK is installed but not working properly"
        return 1
    fi
    
    return 0
}

install_dotnet() {
    log_step "Installing .NET SDK..."
    
    # Check if dotnet is already installed
    if check_command dotnet; then
        DOTNET_VERSION=$(dotnet --version 2>/dev/null | cut -d'.' -f1)
        if [ -n "$DOTNET_VERSION" ] && [ "$DOTNET_VERSION" -ge 10 ]; then
            log_info ".NET 10+ already installed: $(dotnet --version)"
            return 0
        fi
    fi
    
    # Method 1: Try Ubuntu package manager (recommended by Microsoft for Ubuntu)
    log_info "Attempting to install .NET SDK via Ubuntu package manager..."
    
    # Check if Ubuntu backports repository is added
    if ! grep -q "ppa:dotnet/backports" /etc/apt/sources.list.d/*.list 2>/dev/null; then
        log_info "Adding Ubuntu .NET backports repository..."
        wait_for_dpkg_lock || {
            log_error "Cannot add repository: dpkg lock is held"
            return 1
        }
        
        # Install prerequisites
        apt-get update
        apt-get install -y software-properties-common
        
        # Add .NET backports repository (per Microsoft docs)
        add-apt-repository -y ppa:dotnet/backports
        apt-get update
    fi
    
    # Try to install .NET 10 SDK via apt (per Microsoft docs for Ubuntu)
    wait_for_dpkg_lock || {
        log_error "Cannot install .NET SDK: dpkg lock is held"
        return 1
    }
    
    # Check if package is available
    apt-get update
    if apt-cache search dotnet-sdk-10.0 2>/dev/null | grep -q dotnet-sdk-10.0; then
        log_info "Installing .NET SDK 10.0 via Ubuntu package manager..."
        if apt-get install -y dotnet-sdk-10.0; then
            log_info ".NET SDK 10.0 installed via Ubuntu package manager"
            export DOTNET_ROOT="/usr/share/dotnet"
            export PATH="/usr/share/dotnet:$PATH"
            
            # Verify installation
            sleep 2  # Wait for installation to complete
            if check_command dotnet; then
                DOTNET_VER=$(dotnet --version)
                log_info ".NET SDK installed successfully: $DOTNET_VER"
                log_info "Installation method: Ubuntu package manager (apt)"
                log_info "DOTNET_ROOT: /usr/share/dotnet"
                return 0
            else
                log_warn "dotnet command not found after apt install, trying Snap..."
            fi
        else
            log_warn "Failed to install via apt, trying Snap..."
        fi
    else
        log_warn ".NET SDK 10.0 not available in Ubuntu repository, trying Snap..."
    fi
    
    # Method 2: Fallback to Snap (if apt method failed or not available)
    log_info "Installing .NET SDK via Snap (fallback method)..."
    
    # Install snapd if not installed
    if ! check_command snap; then
        log_info "Installing snapd..."
        
        # Wait for dpkg lock
        wait_for_dpkg_lock || {
            log_error "Cannot install snapd: dpkg lock is held"
            return 1
        }
        
        apt-get update
        apt-get install -y snapd
        systemctl enable snapd
        systemctl start snapd
        
        # Wait for snapd to be ready
        sleep 5
    fi
    
    # Install .NET SDK via snap
    log_info "Installing .NET SDK via snap..."
    
    # Try to install .NET 10 SDK first (dotnet-sdk-100) - version-specific package for .NET 9+
    log_info "Attempting to install .NET SDK 10.0..."
    INSTALL_SUCCESS=0
    DOTNET_PACKAGE=""
    DOTNET_ROOT_PATH=""
    
    if snap install dotnet-sdk-100 --classic 2>/dev/null; then
        log_info ".NET SDK 10.0 installed via snap (dotnet-sdk-100)"
        INSTALL_SUCCESS=1
        DOTNET_PACKAGE="dotnet-sdk-100"
        DOTNET_ROOT_PATH="/snap/dotnet-sdk-100/current"
    elif snap list dotnet-sdk-100 2>/dev/null | grep -q dotnet-sdk-100; then
        log_info ".NET SDK 10.0 already installed (dotnet-sdk-100)"
        INSTALL_SUCCESS=1
        DOTNET_PACKAGE="dotnet-sdk-100"
        DOTNET_ROOT_PATH="/snap/dotnet-sdk-100/current"
    else
        # Fallback: Try .NET 9 (dotnet-sdk-90)
        log_warn ".NET SDK 10.0 (dotnet-sdk-100) not available, trying .NET 9..."
        if snap install dotnet-sdk-90 --classic 2>/dev/null; then
            INSTALL_SUCCESS=1
            DOTNET_PACKAGE="dotnet-sdk-90"
            DOTNET_ROOT_PATH="/snap/dotnet-sdk-90/current"
        elif snap list dotnet-sdk-90 2>/dev/null | grep -q dotnet-sdk-90; then
            log_info ".NET SDK 9.0 already installed (dotnet-sdk-90)"
            INSTALL_SUCCESS=1
            DOTNET_PACKAGE="dotnet-sdk-90"
            DOTNET_ROOT_PATH="/snap/dotnet-sdk-90/current"
        else
            # Fallback: Try .NET 8 (dotnet-sdk with channel)
            log_warn ".NET SDK 9.0 not available, trying .NET 8..."
            if snap install dotnet-sdk --classic --channel 8.0/stable 2>/dev/null; then
                INSTALL_SUCCESS=1
                DOTNET_PACKAGE="dotnet-sdk"
                DOTNET_ROOT_PATH="/snap/dotnet-sdk/current"
            elif snap list dotnet-sdk 2>/dev/null | grep -q dotnet-sdk; then
                log_info ".NET SDK already installed (dotnet-sdk)"
                INSTALL_SUCCESS=1
                DOTNET_PACKAGE="dotnet-sdk"
                DOTNET_ROOT_PATH="/snap/dotnet-sdk/current"
            fi
        fi
    fi
    
    if [ $INSTALL_SUCCESS -eq 0 ]; then
        log_error "Failed to install .NET SDK via snap"
        log_error "Please install manually: sudo snap install dotnet-sdk-100 --classic"
        exit 1
    fi
    
    # Wait for snap to link binaries
    log_info "Waiting for snap to link binaries..."
    sleep 3
    
    # Create snap alias if it doesn't exist (per Microsoft docs)
    log_info "Setting up snap alias for dotnet command..."
    if ! snap aliases | grep -q "dotnet.*${DOTNET_PACKAGE}.dotnet"; then
        snap alias "${DOTNET_PACKAGE}.dotnet" dotnet 2>/dev/null || {
            log_warn "Failed to create snap alias, will use symlink instead"
        }
    fi
    
    # Create symlink to /usr/local/bin/dotnet (per Microsoft docs troubleshooting)
    log_info "Creating symlink for dotnet command..."
    mkdir -p /usr/local/bin
    if [ ! -f /usr/local/bin/dotnet ] && [ ! -L /usr/local/bin/dotnet ]; then
        ln -s "${DOTNET_ROOT_PATH}/dotnet" /usr/local/bin/dotnet
        log_info "Symlink created: /usr/local/bin/dotnet -> ${DOTNET_ROOT_PATH}/dotnet"
    else
        log_info "Symlink already exists or file exists at /usr/local/bin/dotnet"
    fi
    
    # Set DOTNET_ROOT environment variable (per Microsoft docs)
    export DOTNET_ROOT="$DOTNET_ROOT_PATH"
    export PATH="/snap/bin:/usr/local/bin:$PATH"
    
    # Verify installation
    DOTNET_CMD=""
    for path in /usr/local/bin/dotnet /snap/bin/dotnet "${DOTNET_ROOT_PATH}/dotnet"; do
        if [ -f "$path" ] && [ -x "$path" ]; then
            DOTNET_CMD="$path"
            break
        fi
    done
    
    if [ -z "$DOTNET_CMD" ]; then
        DOTNET_CMD=$(which dotnet 2>/dev/null || echo "")
    fi
    
    if [ -n "$DOTNET_CMD" ] && $DOTNET_CMD --version > /dev/null 2>&1; then
        DOTNET_VER=$($DOTNET_CMD --version)
        log_info ".NET SDK installed successfully: $DOTNET_VER"
        log_info "Installation method: Snap (${DOTNET_PACKAGE})"
        log_info "DOTNET_ROOT: $DOTNET_ROOT_PATH"
        log_info "Dotnet path: $DOTNET_CMD"
        
        # Check version compatibility
        MAJOR_VERSION=$(echo $DOTNET_VER | cut -d'.' -f1)
        if [ "$MAJOR_VERSION" -lt 10 ]; then
            log_warn "Installed .NET $DOTNET_VER (less than 10.0)"
            log_warn "Your project targets net10.0"
            log_warn "You may need to update TargetFramework in GameServer.csproj to net${MAJOR_VERSION}.0"
        fi
    else
        log_error "Failed to verify .NET SDK installation"
        log_error "Please check: snap list | grep dotnet"
        log_error "Try manually: sudo snap alias ${DOTNET_PACKAGE}.dotnet dotnet"
        exit 1
    fi
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
    
    # Wait for dpkg lock before apt operations
    wait_for_dpkg_lock || {
        log_error "Cannot install SQL Server: dpkg lock is held"
        return 1
    }
    
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
    
    # Wait for dpkg lock
    wait_for_dpkg_lock || {
        log_error "Cannot install Redis: dpkg lock is held"
        return 1
    }
    
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
    
    # Wait for dpkg lock
    wait_for_dpkg_lock || {
        log_error "Cannot install Nginx: dpkg lock is held"
        return 1
    }
    
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
    
    # Wait for dpkg lock
    wait_for_dpkg_lock || {
        log_error "Cannot install Git: dpkg lock is held"
        return 1
    }
    
    apt-get update
    apt-get install -y git
    
    log_info "Git installed"
}

install_dependencies() {
    log_step "Installing all dependencies..."
    
    # Wait for dpkg lock to be released
    if ! wait_for_dpkg_lock; then
        log_error "Cannot proceed: dpkg lock is held by another process"
        exit 1
    fi
    
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
    
    # Check if SQL_SA_PASSWORD is set
    if [ -z "$DB_PASSWORD" ]; then
        log_warn "SQL_SA_PASSWORD not set. Cannot configure database connection string."
        log_warn "Please set it: export SQL_SA_PASSWORD='YourStrong@Password123'"
        log_warn "Then update appsettings.Production.json manually or re-run deploy.sh"
        
        # If file exists, keep it; otherwise create with placeholder
        if [ ! -f "$PROD_SETTINGS" ]; then
            log_info "Creating appsettings.Production.json with placeholder (update password later)..."
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
    "GameDb": "Server=localhost;Database=$DB_NAME;User Id=$DB_USER;Password=CHANGE_ME;TrustServerCertificate=True;",
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
            log_warn "Created appsettings.Production.json with placeholder password. Please update it."
        fi
        return 0
    fi
    
    # Create or update production settings with correct password
    log_info "Creating/updating appsettings.Production.json with connection string..."
    
    # Escape password for JSON (handle special characters)
    # Use Python if available for proper JSON escaping, otherwise use sed
    if command -v python3 &> /dev/null; then
        python3 <<PYEOF
import json
import os

config = {
    "Logging": {
        "LogLevel": {
            "Default": "Information",
            "Microsoft.AspNetCore": "Warning",
            "Microsoft.AspNetCore.Hosting.Diagnostics": "Warning"
        }
    },
    "ConnectionStrings": {
        "GameDb": f"Server=localhost;Database=$DB_NAME;User Id=$DB_USER;Password={os.environ.get('DB_PASSWORD', '$DB_PASSWORD')};TrustServerCertificate=True;",
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

with open('$PROD_SETTINGS', 'w') as f:
    json.dump(config, f, indent=2)
PYEOF
    else
        # Fallback: use heredoc with escaped password
        # Note: This may have issues with special characters in password
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
    fi
    
    log_info "Production settings created/updated at $PROD_SETTINGS"
    log_info "Connection string configured: Server=localhost;Database=$DB_NAME;User Id=$DB_USER;Password=***"
    
    # Verify file was created
    if [ ! -f "$PROD_SETTINGS" ]; then
        log_error "Failed to create appsettings.Production.json"
        return 1
    fi
    
    # Show connection string (hide password)
    log_info "Connection string preview:"
    grep -o "GameDb\": \"[^\"]*" "$PROD_SETTINGS" | sed 's/Password=[^;]*/Password=***/' || true
}

###############################################################################
# Build and Publish
###############################################################################

build_application() {
    log_step "Building and publishing application..."
    
    cd "$INSTALL_DIR/server"
    
    # Ensure snap bin is in PATH
    export PATH="/snap/bin:$PATH"
    
    # Check dotnet version before building
    if ! check_dotnet_version; then
        log_error "Cannot proceed with build. .NET SDK check failed."
        exit 1
    fi
    
    # Find dotnet command - check apt installation first, then snap
    DOTNET_CMD=""
    
    # Check apt installation first (Ubuntu package manager)
    if [ -f "/usr/share/dotnet/dotnet" ] && [ -x "/usr/share/dotnet/dotnet" ]; then
        DOTNET_CMD="/usr/share/dotnet/dotnet"
        export DOTNET_ROOT="/usr/share/dotnet"
        export PATH="/usr/share/dotnet:$PATH"
    else
        # Check snap installation
        for path in /usr/local/bin/dotnet /snap/bin/dotnet; do
            if [ -f "$path" ] && [ -x "$path" ]; then
                DOTNET_CMD="$path"
                break
            fi
        done
        
        if [ -z "$DOTNET_CMD" ]; then
            DOTNET_CMD=$(which dotnet 2>/dev/null || echo "")
        fi
        
        if [ -z "$DOTNET_CMD" ]; then
            if snap list dotnet-sdk-100 2>/dev/null | grep -q dotnet-sdk-100; then
                DOTNET_CMD="snap run dotnet-sdk-100.dotnet"
                export DOTNET_ROOT="/snap/dotnet-sdk-100/current"
            elif snap list dotnet-sdk 2>/dev/null | grep -q dotnet-sdk; then
                DOTNET_CMD="snap run dotnet-sdk.dotnet"
                export DOTNET_ROOT="/snap/dotnet-sdk/current"
            fi
        fi
    fi
    
    if [ -z "$DOTNET_CMD" ]; then
        log_error "dotnet command not found"
        exit 1
    fi
    
    log_info "Using dotnet: $DOTNET_CMD"
    
    # Restore packages
    log_info "Restoring NuGet packages..."
    if ! $DOTNET_CMD restore; then
        log_error "Failed to restore NuGet packages"
        exit 1
    fi
    
    # Publish application
    log_info "Publishing application..."
    if ! $DOTNET_CMD publish -c Release -o "$PUBLISH_DIR" \
        -p:PublishSingleFile=false \
        -p:IncludeNativeLibrariesForSelfExtract=true; then
        log_error "Failed to publish application"
        exit 1
    fi
    
    log_info "Application published successfully to $PUBLISH_DIR"
    
    # Copy appsettings.Production.json to published directory if it exists
    PROD_SETTINGS="$INSTALL_DIR/server/appsettings.Production.json"
    if [ -f "$PROD_SETTINGS" ]; then
        log_info "Copying appsettings.Production.json to published directory..."
        cp "$PROD_SETTINGS" "$PUBLISH_DIR/appsettings.Production.json"
        log_info "appsettings.Production.json copied to published directory"
        
        # Verify connection string doesn't have Trusted_Connection
        if grep -q "Trusted_Connection=True" "$PUBLISH_DIR/appsettings.Production.json"; then
            log_error "ERROR: appsettings.Production.json contains Trusted_Connection=True!"
            log_error "This will not work on Linux. Please fix the connection string."
            log_error "File location: $PROD_SETTINGS"
            exit 1
        fi
        
        # Verify connection string has User Id and Password
        if ! grep -q "User Id=" "$PUBLISH_DIR/appsettings.Production.json" || ! grep -q "Password=" "$PUBLISH_DIR/appsettings.Production.json"; then
            log_error "ERROR: appsettings.Production.json missing User Id or Password!"
            log_error "Connection string must have: User Id=sa;Password=...;"
            log_error "File location: $PROD_SETTINGS"
            exit 1
        fi
        
        log_info "Connection string verified (User Id + Password, no Trusted_Connection)"
    else
        log_warn "appsettings.Production.json not found at $PROD_SETTINGS"
        log_warn "Application will use appsettings.json which has Trusted_Connection=True"
        log_warn "This will FAIL on Linux! Please create appsettings.Production.json:"
        log_warn "  export SQL_SA_PASSWORD='YourPassword'"
        log_warn "  sudo ./create-prod-config.sh"
        log_warn "  sudo ./deploy.sh update"
        
        # Check if appsettings.json has Trusted_Connection
        if grep -q "Trusted_Connection=True" "$PUBLISH_DIR/appsettings.json"; then
            log_error "ERROR: Published app will use Trusted_Connection=True which doesn't work on Linux!"
            log_error "Please create appsettings.Production.json before publishing."
            exit 1
        fi
    fi
    
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
    
    # Determine DOTNET_ROOT and dotnet path - check apt first, then snap
    DOTNET_ROOT_PATH=""
    DOTNET_PATH=""
    
    # Check apt installation first (Ubuntu package manager - recommended)
    if [ -f "/usr/share/dotnet/dotnet" ] && [ -x "/usr/share/dotnet/dotnet" ]; then
        DOTNET_ROOT_PATH="/usr/share/dotnet"
        DOTNET_PATH="/usr/share/dotnet/dotnet"
        log_info "Found .NET SDK installed via apt at $DOTNET_PATH"
    elif [ -f "/usr/bin/dotnet" ] && [ -x "/usr/bin/dotnet" ]; then
        # Some installations put dotnet in /usr/bin
        DOTNET_PATH="/usr/bin/dotnet"
        # Try to find DOTNET_ROOT
        if [ -d "/usr/share/dotnet" ]; then
            DOTNET_ROOT_PATH="/usr/share/dotnet"
        else
            # Get from dotnet --info or use default
            DOTNET_ROOT_PATH=$(dotnet --info 2>/dev/null | grep "Base Path" | awk '{print $3}' || echo "/usr/share/dotnet")
        fi
        log_info "Found .NET SDK at $DOTNET_PATH (DOTNET_ROOT: $DOTNET_ROOT_PATH)"
    else
        # Check snap packages (fallback)
        if snap list dotnet-sdk-100 2>/dev/null | grep -q dotnet-sdk-100; then
            DOTNET_ROOT_PATH="/snap/dotnet-sdk-100/current"
        elif snap list dotnet-sdk-90 2>/dev/null | grep -q dotnet-sdk-90; then
            DOTNET_ROOT_PATH="/snap/dotnet-sdk-90/current"
        elif snap list dotnet-sdk 2>/dev/null | grep -q dotnet-sdk; then
            DOTNET_ROOT_PATH="/snap/dotnet-sdk/current"
        fi
        
        if [ -n "$DOTNET_ROOT_PATH" ]; then
            # Use symlink at /usr/local/bin/dotnet (per Microsoft docs)
            DOTNET_PATH="/usr/local/bin/dotnet"
            
            # Create symlink if it doesn't exist
            if [ ! -f "$DOTNET_PATH" ] && [ ! -L "$DOTNET_PATH" ]; then
                mkdir -p /usr/local/bin
                ln -s "${DOTNET_ROOT_PATH}/dotnet" "$DOTNET_PATH"
                log_info "Created symlink: $DOTNET_PATH -> ${DOTNET_ROOT_PATH}/dotnet"
            fi
        fi
    fi
    
    # Verify dotnet path exists
    if [ -z "$DOTNET_PATH" ] || ([ ! -f "$DOTNET_PATH" ] && [ ! -L "$DOTNET_PATH" ]); then
        log_error "dotnet not found"
        log_error "Please ensure .NET SDK is installed:"
        log_error "  Ubuntu: sudo add-apt-repository ppa:dotnet/backports && sudo apt-get install -y dotnet-sdk-10.0"
        log_error "  Snap: sudo snap install dotnet-sdk-100 --classic"
        log_error "Current PATH: $PATH"
        log_error "Checking for dotnet:"
        which dotnet || log_error "  which dotnet: not found"
        ls -la /usr/share/dotnet/dotnet 2>/dev/null || log_error "  /usr/share/dotnet/dotnet: not found"
        ls -la /usr/local/bin/dotnet 2>/dev/null || log_error "  /usr/local/bin/dotnet: not found"
        snap list | grep dotnet || log_error "  snap dotnet packages: not found"
        exit 1
    fi
    
    log_info "Using dotnet at: $DOTNET_PATH"
    log_info "DOTNET_ROOT: $DOTNET_ROOT_PATH"
    
    # Create service file
    log_info "Creating systemd service file at $SERVICE_FILE..."
    cat > "$SERVICE_FILE" <<EOF
[Unit]
Description=Game Server ASP.NET Core Application
After=network.target mssql-server.service redis-server.service snapd.service

[Service]
Type=notify
User=$APP_USER
Group=$APP_USER
WorkingDirectory=$PUBLISH_DIR
ExecStart=$DOTNET_PATH $PUBLISH_DIR/GameServer.dll
Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=$APP_NAME

# Environment variables
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://localhost:$APP_PORT
Environment=GAME_CONFIG_PATH=$INSTALL_DIR/shared/game-config.json
Environment=DOTNET_ROOT=$DOTNET_ROOT_PATH
Environment=PATH=$DOTNET_ROOT_PATH:/snap/bin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin

# Logging
StandardOutput=journal
StandardError=journal

# Security
NoNewPrivileges=true
PrivateTmp=true

[Install]
WantedBy=multi-user.target
EOF
    
    # Verify service file was created
    if [ ! -f "$SERVICE_FILE" ]; then
        log_error "Failed to create systemd service file at $SERVICE_FILE"
        exit 1
    fi
    
    # Reload systemd
    log_info "Reloading systemd daemon..."
    systemctl daemon-reload
    
    # Verify service is recognized
    if ! systemctl list-unit-files | grep -q "$APP_NAME.service"; then
        log_error "Service file created but not recognized by systemd"
        log_error "Service file content:"
        cat "$SERVICE_FILE"
        exit 1
    fi
    
    log_info "Systemd service created successfully at $SERVICE_FILE"
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
    
    # Check if service file exists
    if [ ! -f "$SERVICE_FILE" ]; then
        log_error "Service file not found at $SERVICE_FILE"
        log_error "Please create it first: sudo ./deploy.sh create-service"
        exit 1
    fi
    
    systemctl enable "$APP_NAME"
    systemctl start "$APP_NAME"
    sleep 5  # Wait longer for startup
    
    # Check if service started successfully
    if systemctl is-active --quiet "$APP_NAME"; then
        log_info "Service started successfully"
        systemctl status "$APP_NAME" --no-pager || true
    else
        log_error "Service failed to start"
        log_error "Checking logs..."
        journalctl -u "$APP_NAME" -n 30 --no-pager || true
        log_error "Please check logs for details: sudo ./deploy.sh logs"
        exit 1
    fi
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
    log_step "Recent Game Server Logs:"
    journalctl -u "$APP_NAME" -n 20 --no-pager || true
    
    echo ""
    log_step ".NET SDK Status:"
    check_dotnet_version || true
    
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
    
    # Check dotnet after installation
    if ! check_dotnet_version; then
        log_error "Installation failed: .NET SDK check failed"
        exit 1
    fi
    
    clone_repository
    create_app_user
    create_log_directory
    setup_database
    configure_appsettings
    build_application
    create_systemd_service
    
    # Verify service file exists before starting
    if [ ! -f "$SERVICE_FILE" ]; then
        log_error "Systemd service file was not created. Cannot start service."
        exit 1
    fi
    
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
    
    # Check dotnet version before update
    if ! check_dotnet_version; then
        log_error "Cannot proceed with update. .NET SDK check failed."
        exit 1
    fi
    
    stop_service
    clone_repository
    
    # Update appsettings.Production.json if SQL_SA_PASSWORD is set
    if [ -n "$DB_PASSWORD" ]; then
        log_info "Updating appsettings.Production.json with current password..."
        configure_appsettings
    else
        log_warn "SQL_SA_PASSWORD not set. Using existing appsettings.Production.json (if exists)."
        log_warn "If connection fails, set SQL_SA_PASSWORD and run: sudo ./create-prod-config.sh"
    fi
    
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
    check-dotnet)
        check_dotnet_version
        ;;
    stop-updates)
        check_root
        stop_unattended_upgrades
        log_info "unattended-upgrades stopped. You can now run install/update commands."
        ;;
    logs)
        log_step "Game Server Logs (last 50 lines):"
        journalctl -u "$APP_NAME" -n 50 --no-pager || true
        ;;
    logs-follow)
        log_info "Following game server logs (Ctrl+C to exit)..."
        journalctl -u "$APP_NAME" -f
        ;;
    create-service)
        check_root
        create_systemd_service
        systemctl daemon-reload
        log_info "Service file created. You can now start it with: sudo ./deploy.sh start"
        ;;
    *)
        echo "Usage: $0 {install|update|start|stop|restart|status|check-dotnet|stop-updates|logs|logs-follow|create-service}"
        echo ""
        echo "Commands:"
        echo "  install      - Full installation (run this first)"
        echo "  update       - Update code and rebuild application"
        echo "  start        - Start the service"
        echo "  stop         - Stop the service"
        echo "  restart      - Restart the service"
        echo "  status       - Show service status"
        echo "  check-dotnet  - Check .NET SDK version and compatibility"
        echo "  stop-updates  - Stop unattended-upgrades (if blocking installation)"
        echo "  create-service - Create systemd service file manually"
        echo "  logs          - Show recent game server logs"
        echo "  logs-follow   - Follow game server logs in real-time"
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
        echo ""
        echo "  # If blocked by unattended-upgrades:"
        echo "  sudo ./deploy.sh stop-updates"
        echo "  sudo ./deploy.sh install"
        echo ""
        echo "  # Check .NET version"
        echo "  ./deploy.sh check-dotnet"
        exit 1
        ;;
esac

