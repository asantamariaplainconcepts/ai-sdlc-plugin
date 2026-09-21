#!/usr/bin/env bash
# Compatibility entry point. DESIGN.md owns the gate; the old token source was retired.
set -euo pipefail

if [[ "${1:-}" == "--help" ]]; then
  echo "Run the complete local gate declared in DESIGN.md. Additional arguments go to aspire do ci."
  exit 0
fi

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../../.." && pwd)"
cd "$REPO_ROOT"
exec aspire do ci \
  --apphost "$REPO_ROOT/src/root/AiOrchestrator.AppHost/AiOrchestrator.AppHost.csproj" \
  --non-interactive "$@"
