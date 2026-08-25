#!/usr/bin/env python3
"""Farm-only command line bridge for Adams Haven Unity.

This intentionally exposes no battle commands.  The MCP server delegates to
this file so the CLI and MCP always have the same behaviour.
"""
from __future__ import annotations

import argparse
import json
import os
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PROJECT = ROOT / "Unity"
MAP_PATH = PROJECT / "Assets" / "Resources" / "Farm" / "farm-map-5x5.json"
PLANTS_PATH = PROJECT / "Assets" / "Resources" / "Farm" / "Art" / "Plants"
UNITY_CANDIDATES = (
    Path(os.environ.get("UNITY_EXE", "")),
    Path(r"S:\AI\Game Engine\Unity\UnityEditors\Editor\6000.5.7f1\Editor\Unity.exe"),
    Path(r"S:\AI\Unity\UnityEditors\Editor\6000.5.7f1\Editor\Unity.exe"),
)


def unity_exe() -> Path:
    for candidate in UNITY_CANDIDATES:
        if str(candidate) and candidate.is_file():
            return candidate
    raise RuntimeError("Unity.exe not found; set UNITY_EXE to the Unity 6 executable.")


def status() -> dict:
    data = json.loads(MAP_PATH.read_text(encoding="utf-8")) if MAP_PATH.is_file() else {}
    sprites = sorted(PLANTS_PATH.glob("*.png")) if PLANTS_PATH.is_dir() else []
    return {
        "project": str(PROJECT),
        "farmScene": (PROJECT / "Assets" / "Scenes" / "Farm.unity").is_file(),
        "mapId": data.get("id"),
        "grid": [data.get("width"), data.get("height")],
        "plantSprites": len(sprites),
        "battleFilesChangedByThisBridge": False,
    }


def run_unity(*extra: str) -> int:
    command = [str(unity_exe()), "-batchmode", "-nographics", "-quit",
               "-projectPath", str(PROJECT), *extra, "-logFile", "-"]
    completed = subprocess.run(command, cwd=ROOT, check=False)
    return completed.returncode


def split_plants(sources: list[str]) -> int:
    script = ROOT / "Tools" / "split_plant_sheets.py"
    destination = str(PLANTS_PATH)
    command = [sys.executable, str(script), destination, *sources]
    return subprocess.run(command, cwd=ROOT, check=False).returncode


def parser() -> argparse.ArgumentParser:
    p = argparse.ArgumentParser(description="Farm-only Adams Haven Unity bridge")
    sub = p.add_subparsers(dest="command", required=True)
    sub.add_parser("status", help="print farm metadata and asset counts")
    sub.add_parser("ensure-scene", help="create/update the farm starter scene in batchmode")
    sub.add_parser("refresh-assets", help="force Unity to reimport farm assets")
    sub.add_parser("validate", help="validate farm scene, map, and Resources assets")
    tests = sub.add_parser("run-tests", help="run Unity edit-mode tests")
    tests.add_argument("--filter", default="Farm", help="Unity test filter")
    split = sub.add_parser("split-plants", help="split supplied light-background sheets")
    split.add_argument("sources", nargs="+", help="source PNG files")
    return p


def main(argv: list[str] | None = None) -> int:
    args = parser().parse_args(argv)
    if args.command == "status":
        print(json.dumps(status(), indent=2))
        return 0
    if args.command == "ensure-scene":
        return run_unity("-executeMethod", "Game.EditorTools.FarmCli.EnsureScene")
    if args.command == "refresh-assets":
        return run_unity("-executeMethod", "Game.EditorTools.FarmCli.RefreshAssets")
    if args.command == "validate":
        return run_unity("-executeMethod", "Game.EditorTools.FarmCli.Validate")
    if args.command == "run-tests":
        return run_unity("-runTests", "-testPlatform", "editmode", "-testFilter", args.filter)
    if args.command == "split-plants":
        return split_plants(args.sources)
    return 2


if __name__ == "__main__":
    raise SystemExit(main())
