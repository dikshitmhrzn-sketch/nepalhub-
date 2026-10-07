#!/usr/bin/env bash
# Restore one backup file from backup-db.sh over the database named by DB_NAME.
# Loud on purpose: this drops and recreates tables. Stop the API first.
#
# Usage: RESTORE_CONFIRM=yes DB_NAME=... DB_USER=... MYSQL_PWD=... bash restore-db.sh <file.sql.gz>
# Optional env as in backup-db.sh: DB_HOST, DB_PORT, MYSQL_DEFAULTS_FILE, MYSQL_BIN (mysql).
set -euo pipefail

DB_HOST="${DB_HOST:-127.0.0.1}"
DB_PORT="${DB_PORT:-3306}"
DB_NAME="${DB_NAME:-nepalhub}"
DB_USER="${DB_USER:-}"
MYSQL_BIN="${MYSQL_BIN:-mysql}"

die() { echo "restore-db: $*" >&2; exit 1; }

file="${1:-}"
[ -n "$file" ] || die "usage: restore-db.sh <backup.sql[.gz]> (plus the environment described at the top of this script)"
[ -f "$file" ] || die "no such file: $file"
[ "${RESTORE_CONFIRM:-}" = "yes" ] || die "this overwrites tables in $DB_NAME; re-run with RESTORE_CONFIRM=yes"
[ -n "$DB_USER" ] || die "DB_USER is not set"
[ -n "${MYSQL_PWD:-}" ] || [ -n "${MYSQL_DEFAULTS_FILE:-}" ] || die "set MYSQL_PWD or MYSQL_DEFAULTS_FILE"
command -v "$MYSQL_BIN" >/dev/null 2>&1 || die "$MYSQL_BIN not found"

mysql_args=(--host="$DB_HOST" --port="$DB_PORT" --user="$DB_USER")
if [ -n "${MYSQL_DEFAULTS_FILE:-}" ]; then
    mysql_args=(--defaults-extra-file="$MYSQL_DEFAULTS_FILE" "${mysql_args[@]}")
fi

echo "restore-db: restoring $file into $DB_NAME at $DB_HOST:$DB_PORT"

case "$file" in
    *.gz) gzip -dc "$file" ;;
    *)    cat "$file" ;;
esac | "$MYSQL_BIN" "${mysql_args[@]}" "$DB_NAME"

echo "restore-db: done"
