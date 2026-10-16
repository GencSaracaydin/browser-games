#!/usr/bin/env bash
# Mirror the Unity project into the browser-games repo (games/operation-long-shadow)
# and push it to the "operation-long-shadow" branch. Usage:
#   Tools/github/sync_browser_games.sh "commit message"
# Never syncs Library/Temp/Logs/UserSettings/Builds/RawAssets/Staging, venvs or caches.
# Aborts on anything that looks like a secret or a file over 95 MB.
set -euo pipefail
SRC="$(cd "$(dirname "$0")/../.." && pwd)"
REPO="/Users/gencsaracaydin/Documents/fun/browser-games"
DEST="$REPO/games/operation-long-shadow"
BRANCH="operation-long-shadow"
MSG="${1:-Sync Operation Long Shadow}"

[ -d "$REPO/.git" ] || gh repo clone GencSaracaydin/browser-games "$REPO"
cd "$REPO"
git fetch -q origin
if git show-ref -q --verify "refs/heads/$BRANCH"; then git checkout -q "$BRANCH"; \
elif git ls-remote -q --exit-code --heads origin "$BRANCH" >/dev/null; then git checkout -q -b "$BRANCH" "origin/$BRANCH"; \
else git checkout -q -b "$BRANCH" origin/main; fi

mkdir -p "$DEST"
rsync -a --delete \
  --exclude '/Library/' --exclude '/Temp/' --exclude '/Logs/' --exclude '/UserSettings/' \
  --exclude '/Builds/' --exclude '/RawAssets/' --exclude '/Staging/' --exclude '/obj/' \
  --exclude '/.locks/' --exclude '/.git/' --exclude '/Data/' --exclude '.venv/' --exclude 'node_modules/' \
  --exclude '__pycache__/' --exclude '.DS_Store' --exclude '*.csproj' --exclude '*.sln' --exclude '*.slnx' \
  --exclude 'Tools/fal/ledger.lock' --exclude '.editor_lock' \
  --include '/Assets/***' --include '/Packages/***' --include '/ProjectSettings/***' \
  --include '/Docs/***' --include '/Tools/***' --include '/README.md' --include '/CREDITS.md' --include '/.gitignore' \
  --exclude '*' \
  "$SRC/" "$DEST/"

# Secret scan (fal keys are uuid:hex; GitHub/Anthropic/OpenAI tokens; private keys; .env files).
if grep -rIlE '([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}:[0-9a-f]{32})|gh[pousr]_[A-Za-z0-9]{30,}|sk-ant-[A-Za-z0-9_-]{20,}|sk-[A-Za-z0-9]{40,}|BEGIN (RSA |EC |OPENSSH )?PRIVATE KEY' --exclude=sync_browser_games.sh "$DEST" ; then
  echo "ABORT: possible secret found in the files above" >&2; exit 1
fi
find "$DEST" -name '.env*' | grep . && { echo "ABORT: .env file in sync" >&2; exit 1; } || true
BIG=$(find "$DEST" -type f -size +95M)
[ -z "$BIG" ] || { echo "ABORT: files over 95 MB:" >&2; echo "$BIG" >&2; exit 1; }

git add -A "games/operation-long-shadow" README.md
if git diff --cached --quiet; then echo "Nothing to sync"; exit 0; fi
git -c user.name="Genc Saracaydin" -c user.email="genc@fal.ai" commit -q -m "$MSG

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
git push -q -u origin "$BRANCH"
echo "Pushed $(git rev-parse --short HEAD) to $BRANCH ($(du -sh "$DEST" | cut -f1) in games/operation-long-shadow)"
