#!/usr/bin/env python3
"""Minimal stdio MCP server for the Adams Haven farm toolchain.

Run with: python Tools/farm_mcp.py
The server uses only the Python standard library and delegates work to
Tools/farm_cli.py.  Battle operations are deliberately not exposed.
"""
from __future__ import annotations

import json
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CLI = ROOT / "Tools" / "farm_cli.py"

TOOLS = [
    {"name": "farm_status", "description": "Read the authored farm map and plant asset counts.", "inputSchema": {"type": "object", "properties": {}}},
    {"name": "farm_ensure_scene", "description": "Create or update Farm.unity through Unity batchmode.", "inputSchema": {"type": "object", "properties": {}}},
    {"name": "farm_refresh_assets", "description": "Force Unity to reimport farm Resources assets.", "inputSchema": {"type": "object", "properties": {}}},
    {"name": "farm_validate", "description": "Validate the farm scene and map through the Unity editor.", "inputSchema": {"type": "object", "properties": {}}},
    {"name": "farm_run_tests", "description": "Run farm edit-mode tests in Unity.", "inputSchema": {"type": "object", "properties": {"filter": {"type": "string", "default": "Farm"}}}},
]


def cli(*args: str) -> tuple[int, str]:
    result = subprocess.run([sys.executable, str(CLI), *args], cwd=ROOT, text=True,
                            stdout=subprocess.PIPE, stderr=subprocess.STDOUT, check=False)
    return result.returncode, result.stdout


def result_for(request_id, value):
    return {"jsonrpc": "2.0", "id": request_id, "result": value}


def error_for(request_id, code, message):
    return {"jsonrpc": "2.0", "id": request_id, "error": {"code": code, "message": message}}


def handle(message: dict):
    request_id = message.get("id")
    method = message.get("method")
    if method == "notifications/initialized":
        return None
    if method == "initialize":
        return result_for(request_id, {
            "protocolVersion": "2024-11-05",
            "capabilities": {"tools": {}},
            "serverInfo": {"name": "adams-haven-farm", "version": "0.1.0"},
        })
    if method == "tools/list":
        return result_for(request_id, {"tools": TOOLS})
    if method != "tools/call":
        return error_for(request_id, -32601, f"Unsupported method: {method}")
    params = message.get("params", {})
    name = params.get("name")
    arguments = params.get("arguments", {}) or {}
    commands = {
        "farm_status": ("status",),
        "farm_ensure_scene": ("ensure-scene",),
        "farm_refresh_assets": ("refresh-assets",),
        "farm_validate": ("validate",),
    }
    if name == "farm_run_tests":
        commands[name] = ("run-tests", "--filter", str(arguments.get("filter", "Farm")))
    if name not in commands:
        return error_for(request_id, -32602, f"Unknown farm tool: {name}")
    code, output = cli(*commands[name])
    return result_for(request_id, {"isError": code != 0, "content": [{"type": "text", "text": output}]})


def main() -> int:
    for line in sys.stdin:
        if not line.strip():
            continue
        try:
            response = handle(json.loads(line))
        except Exception as exc:  # Keep the stdio server alive for the next request.
            response = error_for(None, -32603, str(exc))
        if response is not None:
            sys.stdout.write(json.dumps(response) + "\n")
            sys.stdout.flush()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
