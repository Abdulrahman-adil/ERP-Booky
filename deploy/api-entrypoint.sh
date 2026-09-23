#!/bin/sh
set -eu
export ConnectionStrings__PostgreSQL="$(cat /run/secrets/api_connection_string)"
export Authentication__Jwt__SigningKey="$(cat /run/secrets/jwt_signing_key)"
exec dotnet Erp.Api.dll
