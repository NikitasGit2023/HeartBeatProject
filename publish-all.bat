@echo off
setlocal
set ROOT=%~dp0

echo.
echo [TX] Publishing...
dotnet publish "%ROOT%HeartBeatProject.Tx\HeartBeatProject.Tx.csproj" /p:PublishProfile=Release-win-x64
if %errorlevel% neq 0 (
    echo [TX] FAILED.
    exit /b %errorlevel%
)
echo [TX] Done.

echo.
echo [RX] Publishing...
dotnet publish "%ROOT%HeartBeatProject.Rx\HeartBeatProject.Rx.csproj" /p:PublishProfile=Release-win-x64
if %errorlevel% neq 0 (
    echo [RX] FAILED.
    exit /b %errorlevel%
)
echo [RX] Done.

echo.
echo All packages ready:
echo   TX -^> %ROOT%publish\TX
echo   RX -^> %ROOT%publish\RX
