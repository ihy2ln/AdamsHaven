#!/usr/bin/env bash
# A small command surface over this project's Unity workflows -- consolidates the
# ad-hoc commands this session ran by hand (repeatedly, across M17-M27) into one
# reusable entry point. Nothing here is new capability; it's the same commands this
# project has been running all along, given names so the next session doesn't have to
# re-derive the exact flags from scratch.
#
# Usage:  Tools/unity.sh <subcommand>
#
# Subcommands:
#   status      Is Unity currently running (blocks batchmode)? Is the working tree
#               clean? A one-glance "can I safely do X right now" check.
#   typecheck   Compile every C# source without opening or locking Unity. Delegates
#               to Tools/typecheck.sh -- see that file's own header for how and why.
#               Safe to run anytime, including while Unity is open.
#   test        Run the full EditMode suite headlessly (-batchmode -runTests).
#               Requires Unity to be CLOSED (shared project lockfile) -- checks first
#               and explains, rather than surfacing Unity's own cryptic lock error.
#
# Deliberately NOT included: a `build` subcommand. Building (BuildAndroid.Build /
# BuildBattleStandalone.Build) calls BattleSceneBuilder.CreateBattleScene(), which
# calls BattleAssetBuilder.Build() -- AssetDatabase.SaveAssets()/CreateAsset() run
# headlessly after a script change, in the same invocation, is this project's own
# documented ScriptableObject-corruption risk (see PROJECT-README.md's "Known gaps" --
# found and fought at length across M9/M10). Whether it's currently safe depends on
# whether an interactive Editor session has "blessed" the current scripts since the
# last change -- a judgment call this project has always made by hand, not something
# that belongs one command away from muscle memory. Build through the Editor's own
# menu instead (AI.Game > Battle > Build Android APK / Build Windows Standalone).
#
# Config: same UNITY_EDITOR/PROJECT override pattern as typecheck.sh.
#         UNITY_EDITOR=/path/to/Editor Tools/unity.sh test

set -u

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

winpath() {
  if command -v cygpath >/dev/null 2>&1; then cygpath -m "$1"
  else echo "$1" | sed -E 's|^/([A-Za-z])/|\1:/|'; fi
}

UNITY_EDITOR="${UNITY_EDITOR:-S:/AI/Game Engine/Unity/UnityEditors/Editor/6000.5.7f1/Editor}"
PROJECT="${PROJECT:-$REPO_ROOT/Unity}"
UNITY_EXE="$UNITY_EDITOR/Unity.exe"

unity_running() {
  # tasklist's /FI filter, not a plain grep, so this doesn't false-positive on an
  # unrelated process that happens to have "Unity" in its command line somewhere.
  # MSYS_NO_PATHCONV=1 is required here: Git Bash's path-mangling rewrites a bare
  # "/FI" into a drive-relative path (e.g. "S:/AI/Git/FI") before tasklist ever sees
  # it, which makes tasklist error out -- silently swallowed by 2>/dev/null, so this
  # always reported "closed" regardless of whether Unity was actually running. Found
  # this session when 'status' claimed Unity was closed while a live Editor window
  # (with this exact project open) was sitting right there.
  MSYS_NO_PATHCONV=1 tasklist /FI "IMAGENAME eq Unity.exe" 2>/dev/null | grep -qi "Unity.exe"
}

cmd_status() {
  echo "=== unity.sh status ==="
  if unity_running; then
    n=$(MSYS_NO_PATHCONV=1 tasklist /FI "IMAGENAME eq Unity.exe" 2>/dev/null | grep -ci "Unity.exe")
    echo "Unity: RUNNING ($n process(es) -- includes asset-import workers, not just the main Editor window)"
    echo "  -> 'test' will refuse to run (batchmode needs exclusive access to the project lock)."
    echo "  -> 'typecheck' still works fine."
  else
    echo "Unity: closed -- batchmode ('test') is available."
  fi
  echo
  echo "--- git status (this repo) ---"
  (cd "$REPO_ROOT" && git status --porcelain=v1 | head -20)
  local n_changes
  n_changes=$(cd "$REPO_ROOT" && git status --porcelain=v1 | wc -l)
  echo "($n_changes changed file(s) total; only the first 20 are listed above)"
}

cmd_typecheck() {
  bash "$SCRIPT_DIR/typecheck.sh"
}

cmd_test() {
  if unity_running; then
    echo "unity.sh test: Unity is currently running -- batchmode can't share the project" >&2
    echo "lock with an open Editor. Close Unity first, or run 'Tools/unity.sh status' to" >&2
    echo "see what's holding it." >&2
    exit 1
  fi

  local out_dir results log
  out_dir="$(winpath "${TMPDIR:-/tmp}")/aigame-unity-test"
  mkdir -p "$out_dir"
  results="$out_dir/results.xml"
  log="$out_dir/test.log"
  rm -f "$results" "$log"

  echo "unity.sh test: running EditMode suite headlessly (this can take a couple of minutes)..."
  "$UNITY_EXE" -batchmode -nographics -projectPath "$(winpath "$PROJECT")" \
    -runTests -testPlatform EditMode -testResults "$results" -logFile "$log"
  local exit_code=$?

  if grep -qE "error CS" "$log" 2>/dev/null; then
    echo
    echo "=== COMPILE ERRORS ==="
    grep -E "error CS" "$log" | head -40
    echo
    echo "unity.sh test: compile failed -- see $log for the full log."
    exit 1
  fi

  if [ ! -f "$results" ]; then
    echo "unity.sh test: no results.xml produced (exit code $exit_code) -- see $log."
    exit 1
  fi

  python - "$results" <<'PY'
import sys, xml.etree.ElementTree as ET
r = ET.parse(sys.argv[1]).getroot()
total, passed, failed = r.get('total'), r.get('passed'), r.get('failed')
print(f"\n=== RESULT: total={total} passed={passed} failed={failed} ===")
for tc in r.iter('test-case'):
    if tc.get('result') != 'Passed':
        m = tc.find('failure/message')
        print(f" FAIL: {tc.get('fullname')}")
        if m is not None and m.text:
            print("   ", m.text.strip()[:300])
sys.exit(0 if failed == '0' else 1)
PY
}

case "${1:-}" in
  status)    cmd_status ;;
  typecheck) cmd_typecheck ;;
  test)      cmd_test ;;
  *)
    echo "usage: Tools/unity.sh {status|typecheck|test}" >&2
    exit 2
    ;;
esac
