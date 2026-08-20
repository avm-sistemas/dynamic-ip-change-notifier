#!/bin/bash

# --- Configurações de Segurança (Modo Estrito) ---
set -e          # Para o script imediatamente se qualquer comando falhar (exit code != 0)
set -u          # Trata variáveis não definidas como erro (evita 'rm -rf $VAR/' se VAR estiver vazia)
set -o pipefail # Garante que erros em comandos encadeados (ex: grep | xargs) sejam detectados
#set -x          # Opcional: Descomente para ver cada comando antes de ser executado (Debug mode)

echo "🚀 [$(date +'%H:%M:%S')] Initializing build..."
sleep 1
# echo "🧹 [$(date +'%T')] Deep clean of the files bin/obj..."
# Remove the local compilation folders to prevent the CS0101 error
# find . -type d \( -name "obj" -o -name "bin" \) -exec rm -rf {} + 2>/dev/null || true
echo " "
echo "Trying to shutdown containers..."
docker compose down -v --remove-orphans
clear

echo "Pruning cache..."
docker builder prune -a -f
clear

echo "Cleaning old images..."
IMAGES=$(docker images --filter="reference=ip-monitor*" -q)

# It only attempts to delete if the variable is not empty.
if [ -n "$IMAGES" ]; then
    docker rmi -f $IMAGES
fi
clear

echo "🏗️ Building containers (No-Cache)..."
DOCKER_BUILDKIT=1 docker compose build --no-cache
clear

echo "Starting containers (Force-Recreate)..."
docker-compose up -d --force-recreate
echo " "
echo "Container recreated and Up!"
echo " "
sleep 5

echo " "
echo "✅ [$(date +'%T')] Worker in action!"

