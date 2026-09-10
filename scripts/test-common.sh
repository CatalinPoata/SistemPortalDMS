#!/usr/bin/env bash
set -euo pipefail

repository_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"

load_local_environment() {
  local environment_file="$repository_root/.env"
  if [[ ! -f "$environment_file" ]]; then
    echo "Lipsește .env. Copiază .env.example în .env și completează valorile locale." >&2
    exit 1
  fi

  while IFS= read -r line || [[ -n "$line" ]]; do
    line="${line#"${line%%[![:space:]]*}"}"
    [[ -z "$line" || "$line" == \#* ]] && continue
    [[ "$line" == *=* ]] || { echo "Linie .env invalidă: $line" >&2; exit 1; }

    local name="${line%%=*}"
    local value="${line#*=}"
    value="${value#\"}"
    value="${value%\"}"
    export "$name=$value"
  done < "$environment_file"
}

require_environment_value() {
  local name="$1"
  local value="${!name:-}"
  if [[ -z "$value" || "$value" == CHANGE_ME_* ]]; then
    echo "Variabila $name trebuie configurată în .env." >&2
    exit 1
  fi
}

wait_for_postgres() {
  docker compose -f "$repository_root/docker-compose.yml" up -d postgres

  for _ in $(seq 1 30); do
    if docker compose -f "$repository_root/docker-compose.yml" exec -T postgres \
      pg_isready -U postgres -d portal_dms_db >/dev/null 2>&1; then
      return
    fi
    sleep 1
  done

  echo "PostgreSQL nu a devenit disponibil în 30 de secunde." >&2
  exit 1
}

initialize_test_database() {
  local database="$1"
  local owner="$2"
  local reset_database="$3"

  if [[ "$reset_database" == true ]]; then
    docker compose -f "$repository_root/docker-compose.yml" exec -T postgres \
      dropdb -U postgres --if-exists "$database"
  fi

  if ! docker compose -f "$repository_root/docker-compose.yml" exec -T postgres \
    psql -U postgres -d postgres -tAc "SELECT 1 FROM pg_database WHERE datname = '$database'" | grep -qx '1'; then
    docker compose -f "$repository_root/docker-compose.yml" exec -T postgres \
      createdb -U postgres -O "$owner" "$database"
  fi

  docker compose -f "$repository_root/docker-compose.yml" exec -T postgres \
    psql -U postgres -d "$database" -v ON_ERROR_STOP=1 \
    -c 'CREATE EXTENSION IF NOT EXISTS pg_trgm WITH SCHEMA public;'
}

run_project_tests() {
  local api_project="$1"
  local tests_project="$2"
  local context="$3"
  local connection_name="$4"
  local connection_string="$5"
  local configuration_name

  if [[ "$context" == DmsDbContext ]]; then
    configuration_name=ConnectionStrings__DmsDatabase
  else
    configuration_name=ConnectionStrings__PortalDatabase
  fi

  export "$connection_name=$connection_string"
  export "$configuration_name=$connection_string"

  (
    cd "$repository_root"
    dotnet ef database update --project "$api_project" --startup-project "$api_project" --context "$context"
    dotnet test "$tests_project"
  )
}
