#!/usr/bin/env bash
# Opens the package package.py just built in desktop/dist and runs the app from it, as a user would get it:
# the Linux tarball unpacked, the macOS disk image mounted (its signature checked on the way).
set -euo pipefail
export MINIINFOS_DATA_DIR="$RUNNER_TEMP/miniinfos-package-smoke"
if [ "$RUNNER_OS" = Linux ]; then
  mkdir -p "$RUNNER_TEMP/unpacked"
  tar -xzf desktop/dist/MiniInfos-*.tar.gz -C "$RUNNER_TEMP/unpacked"
  "$RUNNER_TEMP/unpacked/MiniInfos/miniinfos" --diagnostics
  xvfb-run -a "$RUNNER_TEMP/unpacked/MiniInfos/miniinfos" --smoke-ui
else
  hdiutil attach desktop/dist/MiniInfos-*.dmg -mountpoint "$RUNNER_TEMP/miniinfos-dmg"
  codesign --verify --deep --strict "$RUNNER_TEMP/miniinfos-dmg/MiniInfos.app"
  "$RUNNER_TEMP/miniinfos-dmg/MiniInfos.app/Contents/MacOS/MiniInfos.Desktop" --diagnostics
  "$RUNNER_TEMP/miniinfos-dmg/MiniInfos.app/Contents/MacOS/MiniInfos.Desktop" --smoke-ui
  hdiutil detach "$RUNNER_TEMP/miniinfos-dmg"
fi
