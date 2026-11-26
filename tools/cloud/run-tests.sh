#!/usr/bin/env bash
# Compiles the project and runs the EditMode tests without a window (batch mode).
# The first run imports every package and asset: expect a long wait.
# Report: Temp/test-results.xml (NUnit). Exit code 0 = all passed, 8 = tests failed.
#
# Usage: bash tools/cloud/run-tests.sh
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
VERSION="$(sed -n 's/^m_EditorVersion: //p' "$ROOT/ProjectSettings/ProjectVersion.txt" | tr -d '\r')"
export UNITY_CLI_HOME="${UNITY_CLI_HOME:-$HOME/.unity-cli}"
export PATH="$UNITY_CLI_HOME/bin:$PATH"
export UNITY_NO_BANNER=1 UNITY_NON_INTERACTIVE=1

mkdir -p "$ROOT/Temp"
unity test "$ROOT" \
  --editor-version "$VERSION" \
  --mode EditMode \
  --output "$ROOT/Temp/test-results.xml" \
  --timeout 5400
