#!/bin/sh
set -eu

mkdir -p /app/uploads/.tmp /app/keys
chown -R "${APP_UID}:${APP_UID}" /app/uploads /app/keys

exec setpriv \
    --reuid="${APP_UID}" \
    --regid="${APP_UID}" \
    --clear-groups \
    dotnet API-PORTAL.dll
