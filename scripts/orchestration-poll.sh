#!/bin/bash
# Orchestration Mode Poller for Woody (Claude Code on Lito)
#
# Polls the LucyAPI handoff endpoint at a configurable interval.
# Exits with code 0 when a handoff is found (stdout = JSON payload).
# Exits with code 1 on timeout (no handoffs found).
# Exits with code 2 on SIGTERM (clean shutdown).
#
# Usage: orchestration-poll.sh <agent_key> [interval_seconds] [max_checks]
#   agent_key       - required, API key for authentication
#   interval_seconds - optional, default 60
#   max_checks      - optional, default 300 (5 hours at 60s intervals)

set -euo pipefail

AGENT_KEY="${1:?Usage: orchestration-poll.sh <agent_key> [interval] [max_checks]}"
INTERVAL="${2:-60}"
MAX_CHECKS="${3:-300}"

API="https://lucyapi.snowcapsystems.com/agents/woody/handoffs?agent_key=${AGENT_KEY}"

# Clean shutdown on SIGTERM
trap 'echo "[$(date +%H:%M:%S)] Orchestration poller stopped."; exit 2' TERM

for i in $(seq 1 "$MAX_CHECKS"); do
    RESPONSE=$(curl -sf "$API" 2>/dev/null || echo '{"handoffs":[]}')

    COUNT=$(echo "$RESPONSE" | python3 -c "
import sys, json
d = json.load(sys.stdin)
print(len(d.get('handoffs', [])))" 2>/dev/null || echo "0")

    if [ "$COUNT" -gt "0" ]; then
        echo "$RESPONSE"
        exit 0
    fi

    if [ "$i" -lt "$MAX_CHECKS" ]; then
        sleep "$INTERVAL"
    fi
done

echo '{"handoffs":[]}'
exit 1
