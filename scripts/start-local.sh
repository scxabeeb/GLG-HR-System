#!/bin/sh
# Do not echo commands or Docker output: configuration can contain secrets.
set +x
set -eu

fail() {
    printf '\n%s\n' "$1" >&2
    exit 1
}

PROJECT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
cd "$PROJECT_DIR"

command -v docker >/dev/null 2>&1 || fail \
    'Docker is not installed or is not on PATH. Install Docker Desktop (or Docker Engine with the Compose plugin), start it, then run ./scripts/start-local.sh again.'
docker info >/dev/null 2>&1 || fail \
    'Docker daemon is unavailable. Start Docker Desktop or your Docker service, check your Docker context and access permissions, then retry ./scripts/start-local.sh.'
docker compose version >/dev/null 2>&1 || fail \
    'Docker Compose is unavailable. Install/enable the Docker Compose v2 plugin, then retry ./scripts/start-local.sh.'
command -v curl >/dev/null 2>&1 || fail \
    'curl is required to check the app. Install curl, then retry ./scripts/start-local.sh.'
[ -f .env ] || fail \
    'Missing .env. Run cp .env.example .env, edit .env to replace all password placeholders with strong, unique passwords, then retry ./scripts/start-local.sh.'

# Pin the documented file and environment even when invoked from another folder.
compose() {
    docker compose --project-directory "$PROJECT_DIR" --env-file "$PROJECT_DIR/.env" -f "$PROJECT_DIR/compose.yaml" "$@"
}
compose config --quiet >/dev/null 2>&1 || fail \
    'Compose configuration is invalid. Check .env for MYSQL_PASSWORD, MYSQL_ROOT_PASSWORD, BOOTSTRAP_EMAIL and BOOTSTRAP_PASSWORD and review compose.yaml syntax. Do not share .env or expanded Compose configuration. Then retry ./scripts/start-local.sh.'

printf '%s\n' 'Starting the Docker Compose stack (the first build may take several minutes)...'
compose up --build -d >/dev/null 2>&1 || fail \
    'Stack startup failed. Check Docker resources/network access, .env values, and whether port 5189 is already in use. From the project folder, inspect docker compose ps and, privately, docker compose logs --tail=100 db web. Logs may contain sensitive data; redact before sharing. Correct the issue and retry ./scripts/start-local.sh. Existing database volumes retain their original passwords.'

printf '%s\n' 'Waiting up to 180 seconds for http://localhost:5189 ...'
deadline=$(($(date +%s) + 180))
while [ "$(date +%s)" -lt "$deadline" ]; do
    # Ignore local proxy settings; accept success or the normal login redirect.
    status=$(curl --noproxy '*' --silent --output /dev/null --write-out '%{http_code}' \
        --connect-timeout 2 --max-time 3 http://localhost:5189 2>/dev/null) || status=000
    case "$status" in
        2??|3??)
            printf '\n%s\n' 'HR app is ready: http://localhost:5189' \
                'Sign in using BOOTSTRAP_EMAIL and BOOTSTRAP_PASSWORD from your private .env file; change the password on first sign-in.'
            exit 0
            ;;
    esac
    sleep 2
done
fail 'The HR app did not become ready within 180 seconds. The stack has been left running. From the project folder, inspect docker compose ps and, privately, docker compose logs --tail=100 db web for database/migration errors. Logs may contain sensitive data; redact before sharing. Check port 5189 and .env values, then retry ./scripts/start-local.sh. Existing database volumes retain their original passwords. To stop the stack without deleting data, run docker compose down (do not add -v).'
