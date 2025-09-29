#!/usr/bin/env bash
set -euo pipefail

project=""

# Accept either --project <path> or a single positional arg
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

resolve_mplab() {
  if command -v mplab_ide >/dev/null 2>&1; then command -v mplab_ide; return; fi
  if command -v mplab_ide64 >/dev/null 2>&1; then command -v mplab_ide64; return; fi
  local cand
  cand=$(ls -1d /opt/microchip/mplabx/v*/mplab_platform/bin/mplab_ide* 2>/dev/null | sort -V | tail -n 1 || true)
  [[ -n "${cand:-}" ]] && { echo "$cand"; return; }
  return 1
}

exe="$(resolve_mplab)" || { echo "MPLAB X not found. Ensure mplab_ide is on PATH or installed under /opt/microchip/mplabx." >&2; exit 1; }

if [[ -n "$project" && -e "$project" ]]; then
  exec "$exe" "$project"
else
  exec "$exe"
fi