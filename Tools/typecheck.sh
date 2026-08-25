#!/usr/bin/env bash
# Type-check every C# source in this project WITHOUT opening or locking Unity.
#
# Why this exists: `-batchmode -runTests` was the only verification this project had,
# and it cannot run while an Editor has the project open (shared lockfile). During a
# session where the project owner is playtesting, that means code piles up entirely
# unverified -- which is exactly how M19 and M20 came to be written blind.
#
# This drives Unity's own bundled Roslyn compiler straight at the sources, using Unity's
# reference assemblies, and emits to a throwaway DLL outside the project. It never
# touches Library/, never takes the lock, and finishes in a couple of seconds alongside
# a live Editor session.
#
# What it proves, and what it does not:
#   DOES     -- everything compiles: no syntax errors, no missing members, no bad types,
#               no wrong overloads, across Game.Data + Game.Battle + Game.Farm + Game.Town
#               + Game.Navigation + Game.Tests.
#   DOES NOT -- run a single test. Assertions can still fail at runtime, and anything
#               needing Resources/ or a live scene is untouched. Still run
#               `-batchmode -runTests` once Unity is closed.
#
# Two things about corelibs that took a while to get right:
#   * Unity's engine DLLs are netstandard2.1, but the NUnit build Unity ships is net472
#     and resolves [Test] through mscorlib. Reference only one of the two and it fails;
#     reference a real mscorlib alongside netstandard and they collide on
#     SerializableAttribute and friends.
#   * The combination that works is netstandard.dll plus the *netfx shim* mscorlib
#     (NetStandard/compat/2.1.0/shims/netfx/mscorlib.dll), which type-forwards rather
#     than defines, so nothing is declared twice.
#
# Paths must reach csc.exe as Windows paths -- it is a Windows binary and does not
# understand Git Bash's /s/AI/... form. Everything below goes through winpath().
#
# Usage:  Tools/typecheck.sh          (from anywhere)
#         UNITY_EDITOR=/path/to/Editor Tools/typecheck.sh

set -u

# Git Bash ships cygpath; fall back to a /x/... -> X:/... rewrite if it's missing.
winpath() {
  if command -v cygpath >/dev/null 2>&1; then cygpath -m "$1"
  else echo "$1" | sed -E 's|^/([A-Za-z])/|\1:/|'; fi
}

UNITY_EDITOR="${UNITY_EDITOR:-S:/AI/Game Engine/Unity/UnityEditors/Editor/6000.5.7f1/Editor}"
REPO_ROOT="$(winpath "$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)")"
PROJECT="${PROJECT:-$REPO_ROOT/Unity}"
OUT_DIR="$(winpath "${TMPDIR:-/tmp}")/aigame-typecheck"

CSC="$UNITY_EDITOR/Data/DotNetSdk/sdk/8.0.318/Roslyn/bincore/csc.dll"
NETSTANDARD="$UNITY_EDITOR/Data/NetStandard/ref/2.1.0/netstandard.dll"
MSCORLIB_SHIM="$UNITY_EDITOR/Data/NetStandard/compat/2.1.0/shims/netfx/mscorlib.dll"
ENGINE_DIR="$UNITY_EDITOR/Data/Managed/UnityEngine"

for required in "$CSC" "$NETSTANDARD" "$MSCORLIB_SHIM" "$ENGINE_DIR"; do
  if [ ! -e "$required" ]; then
    echo "typecheck: missing $required" >&2
    echo "typecheck: set UNITY_EDITOR to the Editor dir of the installed Unity version." >&2
    exit 2
  fi
done

# NUnit lives in the package cache under a version hash that changes between package
# versions, so find it rather than hardcoding the folder name.
NUNIT="$(find "$PROJECT/Library/PackageCache" -name 'nunit.framework.dll' -path '*net472*' 2>/dev/null | head -1)"

mkdir -p "$OUT_DIR"
RSP="$OUT_DIR/build.rsp"

{
  echo "/target:library"
  echo "/nostdlib+"
  echo "/langversion:9.0"
  echo "/nologo"
  echo "/define:UNITY_2023_1_OR_NEWER;UNITY_EDITOR;UNITY_STANDALONE_WIN;UNITY_INCLUDE_TESTS"
  echo "/out:\"$OUT_DIR/typecheck.dll\""
  echo "/r:\"$NETSTANDARD\""
  echo "/r:\"$MSCORLIB_SHIM\""
  [ -n "$NUNIT" ] && echo "/r:\"$(winpath "$NUNIT")\""
  for dll in "$PROJECT/Library/ScriptAssemblies/UnityEngine.TestRunner.dll" \
             "$PROJECT/Library/ScriptAssemblies/UnityEditor.TestRunner.dll"; do
    [ -f "$dll" ] && echo "/r:\"$dll\""
  done
  for dll in "$ENGINE_DIR"/*.dll; do echo "/r:\"$dll\""; done

  # Assembly-CSharp and Assembly-CSharp-Editor are deliberately excluded: the Editor
  # assembly needs UnityEditor.dll and a pile of editor-only references, and none of it
  # is gameplay code. The asmdef'd gameplay/test assemblies below are compiled together.
  for dir in "$PROJECT/Assets/Scripts/Data" "$PROJECT/Assets/Scripts/Battle" "$PROJECT/Assets/Scripts/Farm" "$PROJECT/Assets/Scripts/Town" "$PROJECT/Assets/Scripts/Navigation" "$PROJECT/Assets/Tests"; do
    while IFS= read -r src; do echo "\"$(winpath "$src")\""; done < <(find "$dir" -name '*.cs')
  done
} > "$RSP"

errors="$(dotnet "$CSC" "@$RSP" 2>&1 | grep -E 'error CS')"

if [ -n "$errors" ]; then
  echo "$errors" | head -40
  echo
  echo "typecheck: FAILED ($(echo "$errors" | wc -l) errors)"
  exit 1
fi

echo "typecheck: clean -- Game.Data + Game.Battle + Game.Farm + Game.Town + Game.Navigation + Game.Tests all compile."
echo "typecheck: this does NOT run tests. Run -batchmode -runTests once Unity is closed."
