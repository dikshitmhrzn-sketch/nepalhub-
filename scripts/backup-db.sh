#!/usr/bin/env bash
# Nightly logical backup of the site's database. One consistent snapshot, compressed,
# integrity-checked, pruned to KEEP_DAYS. Fails loudly so a cron mail or a supervisor notices.
#
# Required: DB_USER, plus either MYSQL_PWD or MYSQL_DEFAULTS_FILE (a .my.cnf with the password).
# Optional: DB_HOST (127.0.0.1), DB_PORT (3306), DB_NAME (nepalhub),
#           BACKUP_DIR (/var/backups/nepalhub), KEEP_DAYS (14), MYSQLDUMP_BIN (mysqldump).
set -euo pipefail

DB_HOST="${DB_HOST:-127.0.0.1}"
DB_PORT="${DB_PORT:-3306}"
DB_NAME="${DB_NAME:-nepalhub}"
DB_USER="${DB_USER:-}"
BACKUP_DIR="${BACKUP_DIR:-/var/backups/nepalhub}"
KEEP_DAYS="${KEEP_DAYS:-14}"
MYSQLDUMP_BIN="${MYSQLDUMP_BIN:-mysqldump}"

die() { echo "backup-db: $*" >&2; exit 1; }

[ -n "$DB_USER" ] || die "DB_USER is not set"
[ -n "${MYSQL_PWD:-}" ] || [ -n "${MYSQL_DEFAULTS_FILE:-}" ] || die "set MYSQL_PWD or MYSQL_DEFAULTS_FILE"
command -v "$MYSQLDUMP_BIN" >/dev/null 2>&1 || die "$MYSQLDUMP_BIN not found"
command -v gzip >/dev/null 2>&1 || die "gzip not found"
mkdir -p "$BACKUP_DIR" || die "cannot create $BACKUP_DIR"

dump_args=(--host="$DB_HOST" --port="$DB_PORT" --user="$DB_USER"
    --single-transaction --quick --skip-lock-tables --no-tablespaces
    --routines --triggers --events --hex-blob "$DB_NAME")
if [ -n "${MYSQL_DEFAULTS_FILE:-}" ]; then
    dump_args=(--defaults-extra-file="$MYSQL_DEFAULTS_FILE" "${dump_args[@]}")
fi

stamp="$(date -u +%Y%m%dT%H%M%SZ)"
out="$BACKUP_DIR/$DB_NAME-$stamp.sql.gz"

"$MYSQLDUMP_BIN" "${dump_args[@]}" | gzip > "$out" || { rm -f "$out"; die "mysqldump failed for $DB_NAME"; }
gzip -t "$out" || { rm -f "$out"; die "gzip verification failed"; }
[ -s "$out" ] || { rm -f "$out"; die "dump came out empty"; }

find "$BACKUP_DIR" -name "$DB_NAME-*.sql.gz" -mtime +"$KEEP_DAYS" -delete

echo "backup-db: wrote $out ($(du -h "$out" | cut -f1))"
