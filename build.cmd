powershell .\build.ps1 -ScriptArgs '-coin="BTC"'
IF %ERRORLEVEL% NEQ 0 (
  REM Error compiling BTC!
  EXIT /B
)
powershell .\build.ps1 -ScriptArgs '-coin="BCH"'