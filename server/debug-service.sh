#!/bin/bash

###############################################################################
# Debug Game Server Service Script
###############################################################################

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

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

APP_NAME="game-server"
PUBLISH_DIR="/opt/game-server-published"
INSTALL_DIR="/opt/game-server"
APP_USER="gameserver"

check_service_file() {
    log_step "Checking service file..."
    SERVICE_FILE="/etc/systemd/system/${APP_NAME}.service"
    
    if [ ! -f "$SERVICE_FILE" ]; then
        log_error "Service file not found: $SERVICE_FILE"
        return 1
    fi
    
    log_info "Service file exists: $SERVICE_FILE"
    log_info "Service file content:"
    cat "$SERVICE_FILE"
    echo ""
    return 0
}

check_published_app() {
    log_step "Checking published application..."
    
    if [ ! -d "$PUBLISH_DIR" ]; then
        log_error "Published directory not found: $PUBLISH_DIR"
        return 1
    fi
    
    if [ ! -f "$PUBLISH_DIR/GameServer.dll" ]; then
        log_error "GameServer.dll not found in $PUBLISH_DIR"
        return 1
    fi
    
    log_info "Published application found"
    ls -lh "$PUBLISH_DIR/GameServer.dll"
    return 0
}

check_dotnet() {
    log_step "Checking .NET runtime..."
    
    DOTNET_PATH=$(which dotnet || echo "")
    if [ -z "$DOTNET_PATH" ]; then
        log_error "dotnet command not found"
        return 1
    fi
    
    log_info "Dotnet found: $DOTNET_PATH"
    log_info "Dotnet version: $(dotnet --version)"
    
    # Test if can run the DLL
    log_info "Testing if dotnet can load the DLL..."
    if dotnet "$PUBLISH_DIR/GameServer.dll" --help > /dev/null 2>&1; then
        log_info "DLL can be loaded"
    else
        log_warn "DLL may have issues (this is normal for web apps)"
    fi
    
    return 0
}

check_permissions() {
    log_step "Checking permissions..."
    
    # Check published directory
    if [ ! -r "$PUBLISH_DIR" ]; then
        log_error "$PUBLISH_DIR is not readable"
        return 1
    fi
    
    if [ ! -r "$PUBLISH_DIR/GameServer.dll" ]; then
        log_error "$PUBLISH_DIR/GameServer.dll is not readable"
        return 1
    fi
    
    # Check if user exists
    if ! id "$APP_USER" &>/dev/null; then
        log_error "User $APP_USER does not exist"
        return 1
    fi
    
    log_info "Permissions OK"
    return 0
}

check_dependencies() {
    log_step "Checking dependencies..."
    
    # Check SQL Server
    if ! systemctl is-active --quiet mssql-server 2>/dev/null; then
        log_warn "SQL Server is not running (this may cause connection errors)"
    else
        log_info "SQL Server is running"
    fi
    
    # Check Redis
    if ! systemctl is-active --quiet redis-server 2>/dev/null; then
        log_warn "Redis is not running"
    else
        log_info "Redis is running"
    fi
    
    # Check shared folder
    if [ ! -f "$INSTALL_DIR/shared/game-config.json" ]; then
        log_warn "game-config.json not found at $INSTALL_DIR/shared/game-config.json"
    else
        log_info "game-config.json found"
    fi
    
    return 0
}

check_logs() {
    log_step "Recent service logs:"
    journalctl -u "$APP_NAME" -n 50 --no-pager --no-hostname || true
}

test_run_manual() {
    log_step "Testing manual run (as current user)..."
    
    cd "$PUBLISH_DIR"
    
    export ASPNETCORE_ENVIRONMENT=Production
    export ASPNETCORE_URLS=http://localhost:5220
    export GAME_CONFIG_PATH="$INSTALL_DIR/shared/game-config.json"
    
    log_info "Running: dotnet $PUBLISH_DIR/GameServer.dll"
    log_warn "This will run in foreground. Press Ctrl+C to stop after a few seconds."
    log_warn "If it crashes immediately, check the error message above."
    
    timeout 10 dotnet "$PUBLISH_DIR/GameServer.dll" 2>&1 || {
        log_error "Application crashed or failed to start"
        return 1
    }
}

main() {
    log_info "Game Server Service Debug Script"
    log_info "================================="
    echo ""
    
    check_service_file
    echo ""
    
    check_published_app
    echo ""
    
    check_dotnet
    echo ""
    
    check_permissions
    echo ""
    
    check_dependencies
    echo ""
    
    check_logs
    echo ""
    
    log_step "Summary:"
    if systemctl is-active --quiet "$APP_NAME" 2>/dev/null; then
        log_info "Service is currently RUNNING"
    else
        log_warn "Service is currently NOT RUNNING"
    fi
    
    echo ""
    log_info "To test manually (will show errors):"
    log_info "  sudo -u $APP_USER bash -c 'cd $PUBLISH_DIR && dotnet GameServer.dll'"
}

case "${1:-}" in
    test)
        test_run_manual
        ;;
    *)
        main
        ;;
esac




