#!/usr/bin/env bash
set -euo pipefail

project=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --project|-p)
      project="${2:-}"
      shift 2
      ;;
    *)
      if [[ -z "$project" ]]; then project="$1"; fi
      shift
      ;;
  esac
done

resolve_mcc() {
  if command -v startMcc >/dev/null 2>&1; then command -v startMcc; return; fi
  local cand
  cand=$(ls -1d /opt/microchip/*[Mm][Cc][Cc]*/*/startMcc* /opt/microchip/*[Mm][Cc][Cc]*/*/bin/startMcc* 2>/dev/null | sort -V | tail -n 1 || true)
  [[ -n "${cand:-}" ]] && { echo "$cand"; return; }
  return 1
}

exe="$(resolve_mcc)" || { echo "MCC launcher not found. Ensure startMcc is on PATH or under /opt/microchip." >&2; exit 1; }

if [[ -n "$project" && -e "$project" ]]; then
  exec "$exe" "$project"
else
  exec "$exe"
fi