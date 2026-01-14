#!/bin/bash

###############################################################################
# Run SQL Scripts on SQL Server Linux
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

# Configuration
DB_NAME="GameServerDb"
DB_USER="sa"
DB_SERVER="localhost"
SCRIPTS_DIR="$(dirname "$0")/Scripts"

# Get password from environment
if [ -z "$SQL_SA_PASSWORD" ]; then
    log_error "SQL_SA_PASSWORD environment variable is not set"
    log_error "Usage: export SQL_SA_PASSWORD='YourPassword' && ./run-sql-script.sh <script.sql>"
    exit 1
fi

DB_PASSWORD="$SQL_SA_PASSWORD"

# Check if sqlcmd is installed
if ! command -v sqlcmd &> /dev/null; then
    log_error "sqlcmd is not installed"
    log_error "Please install SQL Server command-line tools:"
    log_error "  sudo apt-get install -y mssql-tools18 unixodbc-dev"
    log_error "  echo 'export PATH=\"\$PATH:/opt/mssql-tools18/bin\"' >> ~/.bashrc"
    exit 1
fi

# Check if SQL Server is running
if ! systemctl is-active --quiet mssql-server 2>/dev/null; then
    log_error "SQL Server is not running"
    log_error "Please start it: sudo systemctl start mssql-server"
    exit 1
fi

# Function to run a SQL script
run_sql_script() {
    local SCRIPT_FILE="$1"
    
    if [ ! -f "$SCRIPT_FILE" ]; then
        log_error "SQL script not found: $SCRIPT_FILE"
        return 1
    fi
    
    log_step "Running SQL script: $SCRIPT_FILE"
    
    # Run the script
    if sqlcmd -S "$DB_SERVER" -U "$DB_USER" -P "$DB_PASSWORD" -d "$DB_NAME" \
        -C -i "$SCRIPT_FILE" -b; then
        log_info "Script executed successfully: $SCRIPT_FILE"
        return 0
    else
        log_error "Failed to execute script: $SCRIPT_FILE"
        return 1
    fi
}

# Function to run SQL command directly
run_sql_command() {
    local SQL_COMMAND="$1"
    
    log_step "Running SQL command..."
    
    if sqlcmd -S "$DB_SERVER" -U "$DB_USER" -P "$DB_PASSWORD" -d "$DB_NAME" \
        -C -Q "$SQL_COMMAND" -b; then
        log_info "Command executed successfully"
        return 0
    else
        log_error "Failed to execute command"
        return 1
    fi
}

# Main function
main() {
    local SCRIPT_FILE="$1"
    
    if [ -z "$SCRIPT_FILE" ]; then
        log_error "No SQL script specified"
        echo ""
        echo "Usage:"
        echo "  $0 <script.sql>              - Run a SQL script file"
        echo "  $0 --command \"SQL_COMMAND\"   - Run a SQL command directly"
        echo "  $0 --list                    - List available SQL scripts"
        echo "  $0 --all                     - Run all SQL scripts in Scripts directory"
        echo ""
        echo "Examples:"
        echo "  export SQL_SA_PASSWORD='YourPassword'"
        echo "  $0 Scripts/Insert_Enemies.sql"
        echo "  $0 --command \"SELECT COUNT(*) FROM Enemies\""
        echo "  $0 --all"
        exit 1
    fi
    
    # Check database exists
    log_step "Checking database connection..."
    if ! sqlcmd -S "$DB_SERVER" -U "$DB_USER" -P "$DB_PASSWORD" \
        -C -Q "SELECT 1" -b > /dev/null 2>&1; then
        log_error "Cannot connect to SQL Server"
        log_error "Please check:"
        log_error "  1. SQL Server is running: sudo systemctl status mssql-server"
        log_error "  2. Password is correct: export SQL_SA_PASSWORD='YourPassword'"
        exit 1
    fi
    
    # Check if database exists, create if not
    log_step "Checking database $DB_NAME..."
    if ! sqlcmd -S "$DB_SERVER" -U "$DB_USER" -P "$DB_PASSWORD" \
        -C -Q "USE [$DB_NAME]" -b > /dev/null 2>&1; then
        log_warn "Database $DB_NAME does not exist. Creating it..."
        sqlcmd -S "$DB_SERVER" -U "$DB_USER" -P "$DB_PASSWORD" \
            -C -Q "CREATE DATABASE [$DB_NAME]" -b
        log_info "Database $DB_NAME created"
    fi
    
    # Handle different modes
    case "$SCRIPT_FILE" in
        --list)
            log_step "Available SQL scripts:"
            if [ -d "$SCRIPTS_DIR" ]; then
                find "$SCRIPTS_DIR" -name "*.sql" -type f | while read -r file; do
                    echo "  - $file"
                done
            else
                log_warn "Scripts directory not found: $SCRIPTS_DIR"
            fi
            ;;
        --all)
            log_step "Running all SQL scripts in $SCRIPTS_DIR..."
            if [ ! -d "$SCRIPTS_DIR" ]; then
                log_error "Scripts directory not found: $SCRIPTS_DIR"
                exit 1
            fi
            
            FAILED=0
            for script in "$SCRIPTS_DIR"/*.sql; do
                if [ -f "$script" ]; then
                    if ! run_sql_script "$script"; then
                        FAILED=$((FAILED + 1))
                    fi
                    echo ""
                fi
            done
            
            if [ $FAILED -eq 0 ]; then
                log_info "All scripts executed successfully"
            else
                log_error "$FAILED script(s) failed"
                exit 1
            fi
            ;;
        --command)
            if [ -z "$2" ]; then
                log_error "No SQL command provided"
                exit 1
            fi
            run_sql_command "$2"
            ;;
        *)
            # Run specific script file
            if [ ! -f "$SCRIPT_FILE" ]; then
                # Try to find in Scripts directory
                if [ -f "$SCRIPTS_DIR/$SCRIPT_FILE" ]; then
                    SCRIPT_FILE="$SCRIPTS_DIR/$SCRIPT_FILE"
                elif [ -f "$SCRIPTS_DIR/$(basename "$SCRIPT_FILE")" ]; then
                    SCRIPT_FILE="$SCRIPTS_DIR/$(basename "$SCRIPT_FILE")"
                else
                    log_error "SQL script not found: $SCRIPT_FILE"
                    log_info "Available scripts:"
                    if [ -d "$SCRIPTS_DIR" ]; then
                        find "$SCRIPTS_DIR" -name "*.sql" -type f | while read -r file; do
                            echo "  - $file"
                        done
                    fi
                    exit 1
                fi
            fi
            
            run_sql_script "$SCRIPT_FILE"
            ;;
    esac
}

# Run main function
main "$@"

