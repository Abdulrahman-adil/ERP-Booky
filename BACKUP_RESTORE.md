# PostgreSQL and attachment backup / restore

These are executable operator procedures, **not an installed backup schedule**. Work from `/opt/rabt-erp` with the production `.env.production` and standalone Compose file. Do not restore into the current ERP database or delete current volumes. Dumps and attachments contain confidential business data.

## Manual and pre-migration backup

Use a write-maintenance window for a consistent DB/file pair. Stop only this production project's writers, not PostgreSQL. Block other direct writers too. `pg_dump` itself obtains a consistent database snapshot.

```sh
cd /opt/rabt-erp
umask 077
dc() { docker compose --env-file .env.production -f docker-compose.prod.yml "$@"; }
stamp=$(date -u +%Y%m%dT%H%M%SZ)
backup="backups/$stamp"
mkdir -p "$backup"
dc stop proxy frontend api
dc exec -T postgres sh -c 'pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" --format=custom' > "$backup/database.dump"
dc exec -T postgres sh -c 'pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" --schema-only' > "$backup/schema.sql"
dc exec -T postgres sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -c '\''COPY (SELECT * FROM erp."__EFMigrationsHistory" ORDER BY "MigrationId") TO STDOUT WITH CSV HEADER'\''' > "$backup/migration-history.csv"
dc run --rm --no-deps --entrypoint sh api -c 'tar -C /app/data/attachments -czf - .' > "$backup/attachments.tar.gz"
test -s "$backup/database.dump"
dc exec -T postgres pg_restore --list < "$backup/database.dump" > "$backup/restore-list.txt"
tar -tzf "$backup/attachments.tar.gz" > "$backup/attachment-list.txt"
(cd "$backup" && sha256sum database.dump schema.sql migration-history.csv attachments.tar.gz > SHA256SUMS)
dc up -d api frontend proxy
```

Run with `set -e` in an operator script so failures stop the procedure. Check every exit code and never publish a success marker until archive verification succeeds. If migration is next, leave writers stopped, apply reviewed migrations and then start them. Record the release/image digests and UTC recovery point with the backup. Do not include raw environment or rendered Compose output if it contains secrets.

Encrypt backup archives using your organization's key management and copy them off-host to restricted storage, ideally immutable/versioned. Do not store the only copy next to the live database. Back up secret configuration separately with access controls; restore runbooks must not expose credentials. Logical dumps do not include server roles: role initialization and protected runtime/owner credentials are managed separately.

## Schedule and retention recommendation

Recommended starting policy: nightly logical backup, retain 7 daily / 4 weekly / 12 monthly snapshots, and always a pre-migration backup. Select actual RPO/RTO with the owner; for tighter RPO add PostgreSQL WAL archiving/PITR and test it. Use a root-owned systemd service/timer or your managed backup platform with failure alerts. A sample timer schedule is `OnCalendar=*-*-* 02:00:00`, `Persistent=true`; the service must execute a reviewed absolute-path script with `set -eu`, correct working directory and protected secret access. The maintenance-window example above causes downtime and should not be scheduled without approval. A live-traffic backup needs a tested immutable attachment snapshot strategy.

Retention deletion must be restricted to verified backup directories, never the database volume. Do not delete an old snapshot until at least one newer backup has completed a successful restore drill. Scheduling/off-site copy/retention are **not configured by this change**.

## Safe restore drill: NEW database only

Use a separate PostgreSQL deployment where possible. The following creates a distinct database in the selected stack without overwriting existing data; `createdb` fails if the target already exists. Use a name beginning `erp_restorecheck_` and confirm it differs from `POSTGRES_DB`.

```sh
cd /opt/rabt-erp
set -eu
dc() { docker compose --env-file .env.production -f docker-compose.prod.yml "$@"; }
backup="backups/REPLACE_WITH_BACKUP_TIMESTAMP"
restore_db="erp_restorecheck_$(date -u +%Y%m%dT%H%M%SZ)"
(cd "$backup" && sha256sum --check SHA256SUMS)
dc exec -T postgres sh -c 'test "$1" != "$POSTGRES_DB" && createdb -U "$POSTGRES_USER" "$1"' sh "$restore_db"
dc exec -T postgres sh -c 'pg_restore -U "$POSTGRES_USER" -d "$1" --no-owner --no-privileges --exit-on-error --single-transaction' sh "$restore_db" < "$backup/database.dump"
dc exec -T postgres sh -c 'psql -U "$POSTGRES_USER" -d "$1" -v ON_ERROR_STOP=1 -c '\''SELECT count(*) FROM erp."__EFMigrationsHistory";'\''' sh "$restore_db"
```

Restore attachments into a **new named volume**, not the live volume. For example (replace the volume name consistently):

```sh
docker volume create erp_restorecheck_attachments
docker run --rm -i -v erp_restorecheck_attachments:/restore alpine:3.22 sh -c 'test -z "$(ls -A /restore)" && tar -C /restore -xzf -' < "$backup/attachments.tar.gz"
docker run --rm -v erp_restorecheck_attachments:/restore alpine:3.22 chown -R 1654:1654 /restore
```

Attach a separate staging API to the restored database/attachment volume with isolated JWT/Google settings and no public exposure. Reapply least-privilege grants for its runtime role; do not give the application the owner connection. Compare schema, migration history, row counts, posted journal balance checks and selected reports with the backup manifest. Download a known attachment and compare its checksum. Verify traditional login, permissions and tenant isolation. Never automatically process pending accounting transactions during a drill.

An archive listing is not a restore test. Record actual restore duration, errors, migration state, business verification and attachment checks. Obtain approval before any real cutover. Keep the old live database intact; switching to a restored recovery point discards later changes from the application's view. Test the documented procedure periodically and after migration/storage changes.
