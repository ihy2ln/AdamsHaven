@echo off
set UNITY="S:\AI\Game Engine\Unity\UnityEditors\Editor\6000.5.7f1\Editor\Unity.exe"
set PROJECT=S:\AI\Game\test\AI.Game\Unity
set LOG=S:\AI\Game\test\AI.Game\logs\unity-open.log
echo Opening Adams Haven Unity project...
start "" %UNITY% -projectPath "%PROJECT%"
