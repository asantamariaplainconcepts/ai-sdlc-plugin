#!/usr/bin/env bash
# The one gate for this repository: warnings are errors on both sides.
# Exits non-zero on any failure. Declared in .harness/commands.json with "gate": true.
set -euo pipefail
cd "$(dirname "$0")"

echo "— dotnet build + test (warnaserror via Directory.Build.props) —"
dotnet build src/Plugin/AiSdlcPlugin.csproj
dotnet build src/Plugin.Tests/AiSdlcPlugin.Tests.csproj
dotnet test src/Plugin.Tests/AiSdlcPlugin.Tests.csproj --no-build

echo "— web typecheck + build —"
(cd src/web && npm run build)

echo "— openspec validate —"
openspec validate --all --strict

echo "gate passed"
