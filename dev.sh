#!/usr/bin/env bash
set -euo pipefail

cd -- "$(dirname -- "${BASH_SOURCE[0]}")"

if [[ ! -d src/Statik.Web/node_modules ]]; then
    echo "Install frontend dependencies first: npm --prefix src/Statik.Web ci" >&2
    exit 1
fi

docker compose -f docker-compose.dev.yaml up -d --wait --wait-timeout 60

pids=()

cleanup() {
    trap '' INT TERM
    for pid in "${pids[@]}"; do
        # Stop the watchers and their children, including the running backend.
        kill -TERM -- "-$pid" 2>/dev/null || true
    done
    for ((attempt = 0; attempt < 30; attempt++)); do
        alive=false
        for pid in "${pids[@]}"; do
            if kill -0 -- "-$pid" 2>/dev/null; then
                alive=true
            fi
        done
        if [[ "$alive" == false ]]; then
            break
        fi
        sleep 0.1
    done
    # Some watchers survive SIGTERM after stopping their application.
    for pid in "${pids[@]}"; do
        kill -KILL -- "-$pid" 2>/dev/null || true
    done
    wait || true
}

trap cleanup EXIT
trap 'exit 130' INT
trap 'exit 143' TERM

setsid dotnet watch --project src/Statik.Server &
pids+=("$!")

setsid npm --prefix src/Statik.Web run dev &
pids+=("$!")

echo "React app: http://localhost:5173"
echo "Public page: http://localhost:5173/gilles"
echo "Ctrl+C stops both servers."
echo "PostgreSQL stays running; stop it with: docker compose -f docker-compose.dev.yaml stop"

# If either server exits, stop the other and preserve the exit status.
wait -n "${pids[@]}"
