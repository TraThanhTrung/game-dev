#!/bin/bash

###############################################################################
# Update Database Password in appsettings.Production.json
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

INSTALL_DIR="/opt/game-server"
PROD_SETTINGS="$INSTALL_DIR/server/appsettings.Production.json"
DB_NAME="GameServerDb"
DB_USER="sa"

if [ -z "$SQL_SA_PASSWORD" ]; then
    log_error "SQL_SA_PASSWORD environment variable is not set"
    log_error "Usage: export SQL_SA_PASSWORD='YourPassword' && sudo ./update-db-password.sh"
    exit 1
fi

if [ ! -f "$PROD_SETTINGS" ]; then
    log_error "appsettings.Production.json not found at $PROD_SETTINGS"
    exit 1
fi

log_step "Updating database password in appsettings.Production.json..."

# Backup original
cp "$PROD_SETTINGS" "${PROD_SETTINGS}.backup.$(date +%Y%m%d_%H%M%S)"

# Update connection string
# Escape password for JSON (escape quotes and backslashes)
ESCAPED_PASSWORD=$(echo "$SQL_SA_PASSWORD" | sed 's/\\/\\\\/g' | sed 's/"/\\"/g')

# Use Python or sed to update JSON (Python is more reliable for JSON)
if command -v python3 &> /dev/null; then
    python3 <<EOF
import json
import sys

with open('$PROD_SETTINGS', 'r') as f:
    config = json.load(f)

config['ConnectionStrings']['GameDb'] = f"Server=localhost;Database=$DB_NAME;User Id=$DB_USER;Password=$SQL_SA_PASSWORD;TrustServerCertificate=True;"

with open('$PROD_SETTINGS', 'w') as f:
    json.dump(config, f, indent=2)

print("Updated successfully")
EOF
else
    # Fallback to sed (less reliable but works)
    log_warn "Python3 not found, using sed (may have issues with special characters)"
    sed -i "s|Password=[^;]*|Password=$SQL_SA_PASSWORD|g" "$PROD_SETTINGS"
fi

log_info "Password updated in $PROD_SETTINGS"
log_info "Backup saved at: ${PROD_SETTINGS}.backup.*"

# Restart service to apply changes
log_step "Restarting game-server service..."
systemctl restart game-server
sleep 3

if systemctl is-active --quiet game-server; then
    log_info "Service restarted successfully"
else
    log_warn "Service may have issues. Check logs: sudo ./deploy.sh logs"
fi

