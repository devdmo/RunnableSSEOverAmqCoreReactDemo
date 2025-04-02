@echo off
echo Starting backend and frontend applications...

start cmd /k "cd %~dp0..\backend-netcore-sse-amq && dotnet run"
start cmd /k "cd %~dp0 && npm start"

echo Both applications are now running.
echo Backend: http://localhost:5262
echo Frontend: http://localhost:3000