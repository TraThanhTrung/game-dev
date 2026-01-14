#!/bin/bash

###############################################################################
# Optimization Script for 1GB RAM Server
# Tối ưu hóa server cho demo với RAM hạn chế
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

###############################################################################
# Main Optimization
###############################################################################

optimize_server() {
    log_info "Starting server optimization for 1GB RAM..."
    check_root
    
    # 1. Create swap file
    create_swap
    
    # 2. Optimize SQL Server memory
    optimize_sqlserver
    
    # 3. Optimize Redis
    optimize_redis
    
    # 4. Disable unnecessary services
    disable_services
    
    # 5. Limit journal logs
    limit_journal_logs
    
    # 6. Optimize system settings
    optimize_system
    
    log_info ""
    log_info "=========================================="
    log_info "Optimization completed!"
    log_info "=========================================="
    log_info "Current memory usage:"
    free -h
}

create_swap() {
    log_step "Creating 2GB swap file..."
    
    if [ -f /swapfile ]; then
        log_info "Swap file already exists"
        swapon --show
        return 0
    fi
    
    # Create 2GB swap
    fallocate -l 2G /swapfile
    chmod 600 /swapfile
    mkswap /swapfile
    swapon /swapfile
    
    # Add to fstab
    if ! grep -q "/swapfile" /etc/fstab; then
        echo '/swapfile none swap sw 0 0' >> /etc/fstab
    fi
    
    # Set swappiness (how often to use swap)
    # 10 = use swap less aggressively
    sysctl vm.swappiness=10
    if ! grep -q "vm.swappiness" /etc/sysctl.conf; then
        echo "vm.swappiness=10" >> /etc/sysctl.conf
    fi
    
    log_info "Swap file created: 2GB"
}

optimize_sqlserver() {
    log_step "Optimizing SQL Server memory..."
    
    if [ ! -f /opt/mssql/bin/mssql-conf ]; then
        log_warn "SQL Server not installed, skipping..."
        return 0
    fi
    
    # Set memory limit to 512MB (minimum for SQL Server)
    /opt/mssql/bin/mssql-conf set memory.memorylimitmb 512
    
    # Restart SQL Server
    systemctl restart mssql-server
    
    log_info "SQL Server memory limit set to 512MB"
}

optimize_redis() {
    log_step "Optimizing Redis..."
    
    if [ ! -f /etc/redis/redis.conf ]; then
        log_warn "Redis not installed, skipping..."
        return 0
    fi
    
    # Backup original config
    if [ ! -f /etc/redis/redis.conf.backup ]; then
        cp /etc/redis/redis.conf /etc/redis/redis.conf.backup
    fi
    
    # Set max memory to 128MB
    if ! grep -q "^maxmemory " /etc/redis/redis.conf; then
        sed -i '/^# maxmemory /a maxmemory 128mb' /etc/redis/redis.conf
        sed -i 's/^# maxmemory /maxmemory /' /etc/redis/redis.conf
    else
        sed -i 's/^maxmemory .*/maxmemory 128mb/' /etc/redis/redis.conf
    fi
    
    # Set eviction policy
    if ! grep -q "^maxmemory-policy " /etc/redis/redis.conf; then
        sed -i '/^maxmemory /a maxmemory-policy allkeys-lru' /etc/redis/redis.conf
    else
        sed -i 's/^maxmemory-policy .*/maxmemory-policy allkeys-lru/' /etc/redis/redis.conf
    fi
    
    # Restart Redis
    systemctl restart redis-server
    
    log_info "Redis max memory set to 128MB"
}

disable_services() {
    log_step "Disabling unnecessary services..."
    
    # List of services to disable (safe to disable)
    SERVICES_TO_DISABLE=(
        "snapd"
        "unattended-upgrades"
        "apparmor"
    )
    
    for service in "${SERVICES_TO_DISABLE[@]}"; do
        if systemctl is-enabled "$service" &>/dev/null; then
            systemctl disable "$service" 2>/dev/null || true
            log_info "Disabled: $service"
        fi
    done
    
    log_info "Unnecessary services disabled"
}

limit_journal_logs() {
    log_step "Limiting systemd journal logs..."
    
    # Create journald config if it doesn't exist
    if [ ! -f /etc/systemd/journald.conf.backup ]; then
        cp /etc/systemd/journald.conf /etc/systemd/journald.conf.backup 2>/dev/null || true
    fi
    
    # Set log limits
    if ! grep -q "^SystemMaxUse=" /etc/systemd/journald.conf; then
        echo "SystemMaxUse=100M" >> /etc/systemd/journald.conf
    else
        sed -i 's/^SystemMaxUse=.*/SystemMaxUse=100M/' /etc/systemd/journald.conf
    fi
    
    if ! grep -q "^SystemKeepFree=" /etc/systemd/journald.conf; then
        echo "SystemKeepFree=200M" >> /etc/systemd/journald.conf
    else
        sed -i 's/^SystemKeepFree=.*/SystemKeepFree=200M/' /etc/systemd/journald.conf
    fi
    
    # Restart journald
    systemctl restart systemd-journald
    
    log_info "Journal logs limited to 100MB"
}

optimize_system() {
    log_step "Optimizing system settings..."
    
    # Increase file descriptor limits
    if ! grep -q "gameserver" /etc/security/limits.conf; then
        echo "gameserver soft nofile 4096" >> /etc/security/limits.conf
        echo "gameserver hard nofile 8192" >> /etc/security/limits.conf
    fi
    
    # Optimize kernel parameters
    cat >> /etc/sysctl.conf <<EOF

# Game Server Optimizations
vm.overcommit_memory=1
net.core.somaxconn=1024
net.ipv4.tcp_max_syn_backlog=2048
EOF
    
    sysctl -p
    
    log_info "System optimizations applied"
}

###############################################################################
# Show Current Status
###############################################################################

show_status() {
    log_info "Current Server Status:"
    echo ""
    
    log_info "Memory Usage:"
    free -h
    echo ""
    
    log_info "Swap Usage:"
    swapon --show
    echo ""
    
    log_info "SQL Server Memory Limit:"
    if [ -f /opt/mssql/bin/mssql-conf ]; then
        /opt/mssql/bin/mssql-conf get memory.memorylimitmb || echo "Not configured"
    else
        echo "SQL Server not installed"
    fi
    echo ""
    
    log_info "Redis Max Memory:"
    if [ -f /etc/redis/redis.conf ]; then
        grep "^maxmemory " /etc/redis/redis.conf || echo "Not configured"
    else
        echo "Redis not installed"
    fi
    echo ""
    
    log_info "Journal Log Size:"
    journalctl --disk-usage
}

###############################################################################
# Main
###############################################################################

case "${1:-}" in
    optimize)
        optimize_server
        ;;
    status)
        show_status
        ;;
    *)
        echo "Usage: $0 {optimize|status}"
        echo ""
        echo "Commands:"
        echo "  optimize  - Apply all optimizations for 1GB RAM server"
        echo "  status    - Show current optimization status"
        echo ""
        echo "This script optimizes the server for running with 1GB RAM:"
        echo "  - Creates 2GB swap file"
        echo "  - Limits SQL Server to 512MB"
        echo "  - Limits Redis to 128MB"
        echo "  - Disables unnecessary services"
        echo "  - Limits journal logs to 100MB"
        exit 1
        ;;
esac

