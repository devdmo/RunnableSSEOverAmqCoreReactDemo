@echo off
echo Starting backend and frontend applications...

start cmd /k "cd %~dp0 && dotnet run"
start cmd /k "cd %~dp0..\frontend-react-sse && npm start"

echo Both applications are now running.
echo Backend: http://localhost:5262
echo Frontend: http://localhost:3000