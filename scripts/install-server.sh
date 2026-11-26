#!/usr/bin/env bash
# Kiko's Mayhem — Linux setup (Ubuntu/Debian). Installs the SCP:SL dedicated server via SteamCMD
# and installs the plugin. Usage:
#   ./scripts/install-server.sh [--server-dir ~/scpsl] [--plugin path/to/KikosMayhem.dll] [--build] [--port 7777]
set -euo pipefail

SERVER_DIR="$HOME/scpsl"
PLUGIN=""
BUILD=0
PORT="global"
while [[ $# -gt 0 ]]; do
  case "$1" in
    --server-dir) SERVER_DIR="$2"; shift 2 ;;
    --plugin) PLUGIN="$2"; shift 2 ;;
    --build) BUILD=1; shift ;;
    --port) PORT="$2"; shift 2 ;;
    *) echo "unknown option $1"; exit 1 ;;
  esac
done
REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"

echo "== Kiko's Mayhem setup =="
if command -v apt-get >/dev/null; then
  sudo dpkg --add-architecture i386 || true
  sudo apt-get update -qq && sudo apt-get install -y -qq lib32gcc-s1 curl ca-certificates
fi

mkdir -p "$SERVER_DIR/steamcmd"
if [[ ! -x "$SERVER_DIR/steamcmd/steamcmd.sh" ]]; then
  curl -sSL https://steamcdn-a.akamaihd.net/client/installer/steamcmd_linux.tar.gz | tar xz -C "$SERVER_DIR/steamcmd"
fi
for i in 1 2 3; do
  "$SERVER_DIR/steamcmd/steamcmd.sh" +force_install_dir "$SERVER_DIR" +login anonymous +app_update 996560 validate +quit || true
  [[ -f "$SERVER_DIR/SCPSL_Data/Managed/Assembly-CSharp.dll" ]] && break
done
[[ -f "$SERVER_DIR/SCPSL_Data/Managed/Assembly-CSharp.dll" ]] || { echo "Server install failed"; exit 1; }

# Copy the game's DLLs into the repo's (git-ignored) references folder for building / your editor.
mkdir -p "$REPO_ROOT/references"
cp "$SERVER_DIR"/SCPSL_Data/Managed/*.dll "$REPO_ROOT/references/"
echo "Copied the game DLLs to $REPO_ROOT/references"

if [[ $BUILD -eq 1 ]]; then
  dotnet build "$REPO_ROOT/src/KikosMayhem/KikosMayhem.csproj" -c Release -p:SL_REFERENCES="$SERVER_DIR/SCPSL_Data/Managed"
  PLUGIN="$REPO_ROOT/src/KikosMayhem/bin/Release/net48/KikosMayhem.dll"
fi
[[ -z "$PLUGIN" && -f "$REPO_ROOT/KikosMayhem.dll" ]] && PLUGIN="$REPO_ROOT/KikosMayhem.dll"

if [[ -n "$PLUGIN" && -f "$PLUGIN" ]]; then
  DEST="$HOME/.config/SCP Secret Laboratory/LabAPI/plugins/$PORT"
  mkdir -p "$DEST" && cp "$PLUGIN" "$DEST/"
  echo "Installed plugin to $DEST"
else
  echo "WARNING: no KikosMayhem.dll — download it from Releases and pass --plugin, or use --build"
fi
chmod +x "$SERVER_DIR/LocalAdmin" "$SERVER_DIR/SCPSL.x86_64" 2>/dev/null || true
echo
echo "Done. Start with:  cd \"$SERVER_DIR\" && ./LocalAdmin 7777"
