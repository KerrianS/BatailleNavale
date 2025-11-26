#!/bin/bash

GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
NC='\033[0m' 

echo -e "${BLUE}=== Demarrage de l'application Bataille Navale ===${NC}"
echo ""

cleanup() {
    echo -e "\n${YELLOW}Arret des applications...${NC}"
    kill $API_PID 2>/dev/null
    kill $APP_PID 2>/dev/null
    wait $API_PID 2>/dev/null
    wait $APP_PID 2>/dev/null
    echo -e "${GREEN}Applications arretees${NC}"
    exit 0
}

trap cleanup SIGINT SIGTERM

echo -e "${GREEN}[1/2] Demarrage de l'API (port 5001)...${NC}"
cd BattleShip.API
"/c/Program Files/dotnet/dotnet.exe" run --launch-profile http > ../api.log 2>&1 &
API_PID=$!
cd ..

echo -e "${YELLOW}Attente du demarrage de l'API...${NC}"
sleep 5

echo -e "${GREEN}[2/2] Demarrage de l'application Blazor (port 5208)...${NC}"
cd BattleShip.App
"/c/Program Files/dotnet/dotnet.exe" run > ../app.log 2>&1 &
APP_PID=$!
cd ..

echo -e "${YELLOW}Attente du demarrage de l'application...${NC}"
sleep 3

echo ""
echo -e "${GREEN}=== Applications demarrees avec succes ! ===${NC}"
echo ""
echo -e "${BLUE}API:${NC}          http://localhost:5001"
echo -e "${BLUE}API (legacy):${NC} http://localhost:5224"
echo -e "${BLUE}Application:${NC}  http://localhost:5208"
echo ""
echo -e "${YELLOW}Logs:${NC}"
echo -e "  - API:         tail -f api.log"
echo -e "  - Blazor:      tail -f app.log"
echo ""
echo -e "${YELLOW}Appuyez sur Ctrl+C pour arreter les applications${NC}"
echo ""

tail -f api.log app.log &
TAIL_PID=$!

wait $API_PID $APP_PID

kill $TAIL_PID 2>/dev/null
