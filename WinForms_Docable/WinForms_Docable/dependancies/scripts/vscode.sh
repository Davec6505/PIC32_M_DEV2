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

# Resolve VS Code launcher across package types (apt/rpm/arch/snap/flatpak) and variants
launcher=""
extra_args=()

if command -v code >/dev/null 2>&1; then
  launcher="$(command -v code)"
elif command -v code-insiders >/dev/null 2>&1; then
  launcher="$(command -v code-insiders)"
elif command -v codium >/dev/null 2>&1; then
  launcher="$(command -v codium)"
elif command -v flatpak >/dev/null 2>&1 && flatpak info --show-commit com.visualstudio.code >/dev/null 2>&1; then
  launcher="$(command -v flatpak)"
  extra_args=(run com.visualstudio.code)
elif command -v flatpak >/dev/null 2>&1 && flatpak info --show-commit com.visualstudio.code.insiders >/dev/null 2>&1; then
  launcher="$(command -v flatpak)"
  extra_args=(run com.visualstudio.code.insiders)
else
  echo "VS Code not found. Ensure 'code' is in PATH, or install via your package manager (or Flatpak: com.visualstudio.code)." >&2
  exit 1
fi

if [[ -n "${project}" && -e "${project}" ]]; then
  exec "${launcher}" "${extra_args[@]}" "${project}"
else
  exec "${launcher}" "${extra_args[@]}"
fi