# Farm CLI and MCP bridge

The bridge is intentionally farm-only. It does not expose commands that edit or
launch the battle scene.

From the repository root:

```text
python Tools/farm_cli.py status
python Tools/farm_cli.py ensure-scene
python Tools/farm_cli.py refresh-assets
python Tools/farm_cli.py validate
python Tools/farm_cli.py run-tests
```

If Unity is installed in a different location, set `UNITY_EXE` to the Unity 6
executable before using an editor command.

To connect an MCP client, copy `farm-mcp.example.json` into that client's MCP
configuration and adjust the path if needed. The server uses stdio and exposes
`farm_status`, `farm_ensure_scene`, `farm_refresh_assets`, `farm_validate`, and
`farm_run_tests`.
