#!/bin/bash

echo "Démarrage de l'API..."
cd BattleShip.API
"/c/Program Files/dotnet/dotnet.exe" run &
API_PID=$!

echo "Démarrage de l'application Blazor..."
cd ../BattleShip.App
"/c/Program Files/dotnet/dotnet.exe" run &
APP_PID=$!

echo "API PID: $API_PID"
echo "App PID: $APP_PID"
echo "Appuyez sur Ctrl+C pour arrêter les deux applications"

wait
