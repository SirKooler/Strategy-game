#!/usr/bin/env bash
#
# Cloud Agent bootstrap for the Strategy-game Unity project.
#
# Responsibilities (all idempotent so it is safe to re-run and safe to bake
# into an environment build snapshot):
#   1. Install the OS libraries the Linux Unity Editor needs to run headless.
#   2. Install the exact Unity Editor version pinned by ProjectSettings/ProjectVersion.txt.
#   3. Activate a Unity license from repository/team secrets (Personal .ulf or Pro serial).
#   4. Import the project once so the Library cache and script assemblies are warm.
#
# Steps 3 and 4 are skipped (with a clear message) when no license secret is
# present, so the script still succeeds on the license-independent setup and
# the editor install remains reusable.

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

log() { printf '\n[install] %s\n' "$*"; }

# --- 1. Resolve the pinned Editor version ----------------------------------
UNITY_VERSION="$(grep '^m_EditorVersion:' ProjectSettings/ProjectVersion.txt | awk '{print $2}')"
UNITY_CHANGESET="$(grep '^m_EditorVersionWithRevision:' ProjectSettings/ProjectVersion.txt | sed -E 's/.*\(([0-9a-f]+)\).*/\1/')"
UNITY_ROOT="/opt/unity/${UNITY_VERSION}"
UNITY_BIN="${UNITY_ROOT}/Editor/Unity"
log "Project requires Unity ${UNITY_VERSION} (changeset ${UNITY_CHANGESET})"

# --- 2. System libraries required by the headless Editor -------------------
if [ ! -f /opt/unity/.deps-installed ]; then
  log "Installing system libraries for the headless Unity Editor"
  sudo DEBIAN_FRONTEND=noninteractive apt-get update -qq
  sudo DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends \
    curl ca-certificates xz-utils \
    xvfb libgtk-3-0t64 libglu1-mesa libnss3 libgbm1 libxtst6 libxss1 libnotify4 \
    libxrandr2 libxcursor1 libxi6 libxrender1 libxcomposite1 libxdamage1 libxfixes3 \
    libcups2t64 libatk1.0-0t64 libatk-bridge2.0-0t64 libpango-1.0-0 libcairo2 \
    libasound2t64 libdbus-1-3 libgl1 libunwind8 libssl3
  sudo mkdir -p /opt/unity
  sudo touch /opt/unity/.deps-installed
else
  log "System libraries already installed; skipping apt"
fi

# --- 3. Unity Editor install (version-pinned) ------------------------------
if [ ! -x "$UNITY_BIN" ]; then
  log "Downloading Unity Editor ${UNITY_VERSION} (~4.3 GB)"
  sudo mkdir -p "$UNITY_ROOT"
  sudo chown "$(id -u):$(id -g)" "$UNITY_ROOT"
  TARBALL="/opt/unity/Unity-${UNITY_VERSION}.tar.xz"
  URL="https://download.unity3d.com/download_unity/${UNITY_CHANGESET}/LinuxEditorInstaller/Unity.tar.xz"
  curl -fL -C - -o "$TARBALL" "$URL"
  log "Extracting Unity Editor"
  tar -xf "$TARBALL" -C "$UNITY_ROOT"
  rm -f "$TARBALL"
else
  log "Unity Editor already present at ${UNITY_BIN}; skipping download"
fi
"$UNITY_BIN" -version 2>/dev/null || true

# --- 4. License activation --------------------------------------------------
# A Unity license is mandatory: the Editor exits with code 198 for any project
# operation (import/compile/test/build) until one is activated.
activate_license() {
  if [ -n "${UNITY_LICENSE:-}" ]; then
    log "Activating Personal license from UNITY_LICENSE (.ulf)"
    local ulf
    ulf="$(mktemp --suffix=.ulf)"
    printf '%s' "$UNITY_LICENSE" > "$ulf"
    # -manualLicenseFile can return non-zero even on success; verify afterwards.
    xvfb-run -a "$UNITY_BIN" -batchmode -nographics -quit \
      -manualLicenseFile "$ulf" -logFile - || true
    rm -f "$ulf"
  elif [ -n "${UNITY_SERIAL:-}" ] && [ -n "${UNITY_EMAIL:-}" ] && [ -n "${UNITY_PASSWORD:-}" ]; then
    log "Activating Pro/Plus license from UNITY_SERIAL + UNITY_EMAIL + UNITY_PASSWORD"
    xvfb-run -a "$UNITY_BIN" -batchmode -nographics -quit \
      -serial "$UNITY_SERIAL" -username "$UNITY_EMAIL" -password "$UNITY_PASSWORD" \
      -logFile - || true
  else
    log "No Unity license secret found (UNITY_LICENSE or UNITY_SERIAL/UNITY_EMAIL/UNITY_PASSWORD)."
    log "Editor installed but not licensed; skipping activation, import and compile."
    return 1
  fi
}

if activate_license; then
  # --- 5. Warm the project (import assets + compile scripts) ---------------
  log "Importing project and compiling scripts (warms the Library cache)"
  xvfb-run -a "$UNITY_BIN" -batchmode -nographics -quit \
    -projectPath "$REPO_ROOT" -logFile -
  log "Project imported and scripts compiled successfully."
fi

log "Bootstrap complete."
