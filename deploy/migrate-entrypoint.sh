#!/bin/sh
set -eu
export ConnectionStrings__PostgreSQL="$(cat /run/secrets/migration_connection_string)"
exec /tools/dotnet-ef "$@"
