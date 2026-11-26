#!/usr/bin/env bash
# Prepares a Linux cloud session (Claude Code on the web, CI…) to work on this project:
#   1. installs the Unity CLI (official installer, checks the binary's SHA-256);
#   2. installs the project's Editor version for Linux, only if UNITY_ACCEPT_EULA=1
#      (installing the Editor accepts Unity's terms: the owner gives that consent by setting
#      the variable in the environment, never the script on its own);
#   3. activates a license if one is provided:
#        UNITY_LICENSE_SERIAL  serial of a Plus/Pro license (read from the environment by the CLI)
#        UNITY_LICENSE_FILE    contents of a .ulf license file
#      Without either, the Editor is installed but cannot run: code can still be written,
#      tests run on the owner's machine.
#
# Usage: bash tools/cloud/setup-unity.sh
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
VERSION="$(sed -n 's/^m_EditorVersion: //p' "$ROOT/ProjectSettings/ProjectVersion.txt" | tr -d '\r')"
export UNITY_CLI_HOME="${UNITY_CLI_HOME:-$HOME/.unity-cli}"
export PATH="$UNITY_CLI_HOME/bin:$PATH"
export UNITY_NO_BANNER=1 UNITY_NON_INTERACTIVE=1

echo "== Unity for project $ROOT (Editor $VERSION)"

# 1. Unity CLI
if ! command -v unity >/dev/null 2>&1; then
  echo "== Installing the Unity CLI into $UNITY_CLI_HOME"
  installer="$(mktemp -d)/install.sh"
  curl -fsSL https://unity.com/install.sh -o "$installer"
  bash "$installer"
fi
unity --version

# 2. Editor
if unity editors --installed --format tsv --no-header 2>/dev/null | grep -q "$VERSION"; then
  echo "== Editor $VERSION already installed"
elif [ "${UNITY_ACCEPT_EULA:-}" = "1" ]; then
  echo "== Installing Editor $VERSION (several GB, this takes a while)"
  unity install "$VERSION" --yes --accept-eula
else
  echo "!! Editor $VERSION not installed: installing it accepts Unity's terms."
  echo "!! Set UNITY_ACCEPT_EULA=1 in the environment to allow it, then run this script again."
  exit 2
fi

# 3. License
if unity license status --format json >/dev/null 2>&1; then
  echo "== A Unity license is already active"
elif [ -n "${UNITY_LICENSE_SERIAL:-}" ]; then
  echo "== Activating the license from UNITY_LICENSE_SERIAL"
  unity license activate
elif [ -n "${UNITY_LICENSE_FILE:-}" ]; then
  echo "== Activating the license from UNITY_LICENSE_FILE"
  license="$(mktemp -d)/Unity_lic.ulf"
  printf '%s' "$UNITY_LICENSE_FILE" > "$license"
  unity license activate --file "$license"
  rm -f "$license"
else
  echo "!! No Unity license provided (UNITY_LICENSE_SERIAL or UNITY_LICENSE_FILE)."
  echo "!! The Editor is installed but cannot compile or run tests in this session."
  exit 3
fi

echo "== Ready: run  bash tools/cloud/run-tests.sh"
