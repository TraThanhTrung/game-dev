#!/bin/bash

###############################################################################
# Create appsettings.Production.json với connection string đúng
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
REDIS_HOST="localhost"
REDIS_PORT=6379

# Get password from environment or prompt
if [ -z "$SQL_SA_PASSWORD" ]; then
    log_error "SQL_SA_PASSWORD environment variable is not set"
    log_error "Usage: export SQL_SA_PASSWORD='YourPassword' && sudo ./create-prod-config.sh"
    exit 1
fi

DB_PASSWORD="$SQL_SA_PASSWORD"

# Check if install directory exists
if [ ! -d "$INSTALL_DIR/server" ]; then
    log_error "Server directory not found: $INSTALL_DIR/server"
    log_error "Please run deploy.sh install first or clone repository manually"
    exit 1
fi

log_step "Creating appsettings.Production.json..."

# Create directory if needed
mkdir -p "$(dirname "$PROD_SETTINGS")"

# Use Python for proper JSON escaping if available
if command -v python3 &> /dev/null; then
    log_info "Using Python3 to create JSON file (handles special characters properly)..."
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
        "GameDb": f"Server=localhost;Database=$DB_NAME;User Id=$DB_USER;Password={os.environ.get('SQL_SA_PASSWORD', '$DB_PASSWORD')};TrustServerCertificate=True;",
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

print("File created successfully")
PYEOF
else
    # Fallback: use heredoc
    log_info "Using heredoc to create JSON file..."
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

# Verify file was created
if [ ! -f "$PROD_SETTINGS" ]; then
    log_error "Failed to create appsettings.Production.json"
    exit 1
fi

log_info "File created: $PROD_SETTINGS"

# Show connection string (hide password)
log_info "Connection string (password hidden):"
CONN_STR=$(grep -o '"GameDb": "[^"]*' "$PROD_SETTINGS" | sed 's/Password=[^;]*/Password=***/')
log_info "  $CONN_STR"

# Verify JSON is valid
if command -v python3 &> /dev/null; then
    if python3 -m json.tool "$PROD_SETTINGS" > /dev/null 2>&1; then
        log_info "JSON file is valid"
    else
        log_error "JSON file is invalid!"
        exit 1
    fi
fi

log_info ""
log_info "=========================================="
log_info "appsettings.Production.json created!"
log_info "=========================================="
log_info "You can now run: sudo ./deploy.sh update"
log_info "=========================================="

