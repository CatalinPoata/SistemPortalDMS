#!/usr/bin/env bash
set -euo pipefail

source "$(cd -- "$(dirname -- "$0")" && pwd)/test-common.sh"

reset_database=false
if [[ "${1:-}" == --reset ]]; then
  reset_database=true
elif [[ $# -gt 0 ]]; then
  echo "Utilizare: ./scripts/test-portal.sh [--reset]" >&2
  exit 2
fi

load_local_environment
require_environment_value POSTGRES_PASSWORD
require_environment_value PORTAL_DB_PASSWORD
require_environment_value DMS_DB_PASSWORD
wait_for_postgres
initialize_test_database portal_portal_test portal_app "$reset_database"

run_project_tests \
  API-PORTAL/API-PORTAL.csproj \
  API-PORTAL-TESTS/API-PORTAL-TESTS.csproj \
  PortalDbContext \
  PORTAL_TEST_CONNECTION \
  "Host=localhost;Port=5432;Database=portal_portal_test;Username=portal_app;Password=$PORTAL_DB_PASSWORD;SearchPath=portal"
