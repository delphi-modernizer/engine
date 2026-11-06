#!/usr/bin/env bash
# Drives dmz through a workspace's life on a fixture tree and fails on any unexpected exit code or output.
set -euo pipefail

root=$(cd "$(dirname "$0")/.." && pwd)
dmz_dll="$root/src/DelphiModernizer.Cli/bin/Release/net10.0/dmz.dll"
work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT

dmz() { env -u CLAUDECODE dotnet "$dmz_dll" "$@"; }

expect() {
  local expected=$1
  shift
  local actual=0
  "$@" > "$work/out.txt" 2>&1 || actual=$?
  if [ "$actual" -ne "$expected" ]; then
    printf 'FAIL: expected exit %s, got %s from: %s\n' "$expected" "$actual" "$*"
    cat "$work/out.txt"
    exit 1
  fi
  printf 'ok   %s\n' "$*"
}

contains() {
  if ! grep -q -- "$1" "$work/out.txt"; then
    printf 'FAIL: output lacks "%s"\n' "$1"
    cat "$work/out.txt"
    exit 1
  fi
}

ws="$work/acme"
hook() { printf '{"tool_name":"%s","cwd":"%s","tool_input":%s}' "$1" "$ws" "$2" | dmz guard pre-tool-use; }

expect 0 dmz version
expect 1 dmz init "$ws" --name Acme --legacy "$root/tests/fixtures/delphi/legacy-erp" --by Person
expect 0 dmz init "$ws" --name Acme --legacy "$root/tests/fixtures/delphi/legacy-erp" --stack deferred --by Person --no-git
cd "$ws"
expect 0 dmz scan
contains "Database access: BDE"
expect 0 dmz status
contains "G0 Initialized: ready"
expect 0 dmz trace
contains "0 errors"
expect 2 hook Edit '{"file_path":"legacy/Sales/Pricing.pas"}'
expect 0 hook Write '{"file_path":"docs/01-discovery/discovery-report.md"}'
expect 2 hook Bash '{"command":"dmz approve G4"}'
expect 1 env CLAUDECODE=1 dotnet "$dmz_dll" approve G0 --by Agent
expect 0 dmz approve G0 --by Person --note "Charter reviewed" --no-commit
expect 0 dmz status
contains "G0 Initialized: approved"
printf '\nA late change.\n' >> docs/00-project-charter/D01-project-charter.md
expect 0 dmz status
contains "G0 Initialized: stale"
expect 0 dmz stack dotnet --by Person
expect 0 dmz trace
printf '\n### BR-001 — Undocumented\n' >> docs/02-functional-requirements/business-rules.md
expect 1 dmz trace
contains "TR005"
echo "end-to-end: all checks passed"
