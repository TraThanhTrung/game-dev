#!/bin/bash

###############################################################################
# SQL Server Troubleshooting and Fix Script
###############################################################################

set -e

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

check_root() {
    if [ "$EUID" -ne 0 ]; then
        log_error "This script must be run as root or with sudo"
        exit 1
    fi
}

check_sqlserver_status() {
    log_step "Checking SQL Server status..."
    
    if systemctl is-active --quiet mssql-server 2>/dev/null; then
        log_info "SQL Server is running"
        return 0
    else
        log_warn "SQL Server is not running"
        return 1
    fi
}

check_memory() {
    log_step "Checking available memory..."
    
    TOTAL_MEM=$(free -m | awk '/^Mem:/{print $2}')
    AVAIL_MEM=$(free -m | awk '/^Mem:/{print $7}')
    
    log_info "Total RAM: ${TOTAL_MEM}MB"
    log_info "Available RAM: ${AVAIL_MEM}MB"
    
    if [ "$TOTAL_MEM" -lt 2048 ]; then
        log_warn "Server has less than 2GB RAM (SQL Server minimum requirement)"
        log_warn "SQL Server may fail to start"
        return 1
    fi
    
    return 0
}

check_sqlserver_logs() {
    log_step "Checking SQL Server error logs..."
    
    if [ -f /var/opt/mssql/log/errorlog ]; then
        log_info "Recent SQL Server errors:"
        tail -30 /var/opt/mssql/log/errorlog | grep -i error || log_info "No errors found in recent logs"
    else
        log_warn "SQL Server error log not found"
    fi
    
    log_info "Systemd status:"
    systemctl status mssql-server --no-pager -l || true
}

fix_sqlserver_memory() {
    log_step "Configuring SQL Server memory limit..."
    
    if [ ! -f /opt/mssql/bin/mssql-conf ]; then
        log_error "SQL Server is not installed"
        return 1
    fi
    
    # Get available memory
    TOTAL_MEM=$(free -m | awk '/^Mem:/{print $2}')
    
    if [ "$TOTAL_MEM" -lt 2048 ]; then
        # Server has less than 2GB, set SQL Server to use 512MB
        log_info "Server has ${TOTAL_MEM}MB RAM, setting SQL Server memory limit to 512MB"
        /opt/mssql/bin/mssql-conf set memory.memorylimitmb 512
    else
        # Server has 2GB+, set SQL Server to use 1GB
        log_info "Server has ${TOTAL_MEM}MB RAM, setting SQL Server memory limit to 1024MB"
        /opt/mssql/bin/mssql-conf set memory.memorylimitmb 1024
    fi
    
    log_info "SQL Server memory limit configured"
}

restart_sqlserver() {
    log_step "Restarting SQL Server..."
    
    systemctl restart mssql-server
    
    # Wait for SQL Server to start
    log_info "Waiting for SQL Server to start..."
    sleep 10
    
    # Check if started
    if systemctl is-active --quiet mssql-server; then
        log_info "SQL Server started successfully"
        return 0
    else
        log_error "SQL Server failed to start"
        check_sqlserver_logs
        return 1
    fi
}

setup_sqlserver() {
    log_step "Setting up SQL Server..."
    
    if [ -z "$SQL_SA_PASSWORD" ]; then
        log_error "SQL_SA_PASSWORD environment variable is not set"
        log_error "Please set it: export SQL_SA_PASSWORD='YourStrong@Password123'"
        return 1
    fi
    
    if [ ! -f /opt/mssql/bin/mssql-conf ]; then
        log_error "SQL Server is not installed"
        log_error "Please install it first: sudo ./deploy.sh install"
        return 1
    fi
    
    # Check if EULA is accepted and SQL Server is configured
    # If SQL Server fails with EULA error, it means it's not configured
    log_info "Configuring SQL Server (accepting EULA and setting SA password)..."
    
    # Stop SQL Server if it's trying to start
    systemctl stop mssql-server 2>/dev/null || true
    sleep 2
    
    # Setup SQL Server with EULA acceptance
    log_info "Running SQL Server setup..."
    ACCEPT_EULA=Y MSSQL_SA_PASSWORD="$SQL_SA_PASSWORD" /opt/mssql/bin/mssql-conf setup accept-eula
    
    if [ $? -eq 0 ]; then
        log_info "SQL Server configured successfully"
        return 0
    else
        log_error "Failed to configure SQL Server"
        return 1
    fi
}

main() {
    check_root
    
    log_info "SQL Server Troubleshooting Script"
    log_info "=================================="
    
    # Check status
    if check_sqlserver_status; then
        log_info "SQL Server is running. No action needed."
        exit 0
    fi
    
    # Check memory
    check_memory
    MEMORY_OK=$?
    
    # Check logs
    check_sqlserver_logs
    
    echo ""
    log_step "Attempting to fix SQL Server..."
    
    # Fix memory limit if needed
    if [ $MEMORY_OK -ne 0 ]; then
        fix_sqlserver_memory
    fi
    
    # Setup SQL Server (required if EULA not accepted)
    if [ -n "$SQL_SA_PASSWORD" ]; then
        log_info "SQL Server needs to be configured (EULA not accepted)"
        if ! setup_sqlserver; then
            log_error "Failed to setup SQL Server"
            exit 1
        fi
    else
        log_error "SQL_SA_PASSWORD not set. Cannot setup SQL Server."
        log_error "Please set: export SQL_SA_PASSWORD='YourStrong@Password123'"
        log_error "Then run: sudo ./fix-sqlserver.sh fix"
        exit 1
    fi
    
    # Restart
    if restart_sqlserver; then
        log_info ""
        log_info "=================================="
        log_info "SQL Server is now running!"
        log_info "=================================="
        exit 0
    else
        log_error ""
        log_error "=================================="
        log_error "Failed to start SQL Server"
        log_error "=================================="
        log_error "Please check logs:"
        log_error "  sudo journalctl -u mssql-server -n 50"
        log_error "  sudo cat /var/opt/mssql/log/errorlog | tail -50"
        exit 1
    fi
}

case "${1:-}" in
    check)
        check_root
        check_sqlserver_status
        check_memory
        check_sqlserver_logs
        ;;
    fix)
        main
        ;;
    restart)
        check_root
        restart_sqlserver
        ;;
    *)
        echo "Usage: $0 {check|fix|restart}"
        echo ""
        echo "Commands:"
        echo "  check   - Check SQL Server status, memory, and logs"
        echo "  fix     - Attempt to fix SQL Server (configure memory, setup, restart)"
        echo "  restart - Restart SQL Server"
        echo ""
        echo "Environment Variables:"
        echo "  SQL_SA_PASSWORD - SQL Server SA password (required for setup)"
        echo ""
        echo "Example:"
        echo "  export SQL_SA_PASSWORD='YourStrong@Password123'"
        echo "  sudo ./fix-sqlserver.sh fix"
        exit 1
        ;;
esac

