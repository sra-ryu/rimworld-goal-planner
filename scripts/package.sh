#!/usr/bin/env bash
# Build a clean copy of the mod for Steam Workshop upload.
#
#   scripts/package.sh            Release build -> dist/GoalPlanner/
#   scripts/package.sh link-dist  Point the game's Mods/GoalPlanner link at dist (for uploading)
#   scripts/package.sh link-dev   Point it back at the repo; copies a new About/PublishedFileId.txt back
#
# The in-game uploader uploads the mod folder as is, so only the files players need go into dist.
set -euo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DIST="$REPO/dist/GoalPlanner"
GAME="${RIMWORLD_DIR:-$HOME/Library/Application Support/Steam/steamapps/common/RimWorld}"
LINK="$GAME/Mods/GoalPlanner"

relink() {
  local target="$1"
  if [ -e "$LINK" ] && [ ! -L "$LINK" ]; then
    echo "Error: $LINK exists and is not a symlink; not touching it." >&2
    exit 1
  fi
  ln -sfn "$target" "$LINK"
  echo "Mods/GoalPlanner -> $target"
  echo "Restart RimWorld for the change to take effect."
}

package() {
  export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
  export PATH="$DOTNET_ROOT:$PATH"

  echo "== Release build"
  (cd "$REPO/Source/GoalPlanner" && dotnet build -c Release --nologo -v q)

  echo "== Copying files to $DIST"
  rm -rf "$DIST"
  mkdir -p "$DIST/Assemblies"
  rsync -a --exclude 'Screenshots/' --exclude '.DS_Store' "$REPO/About" "$DIST/"
  cp "$REPO/Assemblies/GoalPlanner.dll" "$DIST/Assemblies/"
  rsync -a --exclude '.DS_Store' "$REPO/Defs" "$REPO/Languages" "$DIST/"
  cp "$REPO/LICENSE" "$REPO/README.md" "$DIST/"

  echo "== Done"
  (cd "$DIST" && find . -type f | sort)
  du -sh "$DIST" | awk '{print "Total: " $1}'
}

case "${1:-package}" in
  package)
    package
    ;;
  link-dist)
    [ -d "$DIST" ] || { echo "Error: run scripts/package.sh first." >&2; exit 1; }
    relink "$DIST"
    ;;
  link-dev)
    if [ -f "$DIST/About/PublishedFileId.txt" ] && [ ! -f "$REPO/About/PublishedFileId.txt" ]; then
      cp "$DIST/About/PublishedFileId.txt" "$REPO/About/"
      echo "Copied PublishedFileId.txt ($(cat "$REPO/About/PublishedFileId.txt")) to About/ - commit it."
    fi
    relink "$REPO"
    ;;
  *)
    echo "Usage: $0 [package|link-dist|link-dev]" >&2
    exit 1
    ;;
esac
