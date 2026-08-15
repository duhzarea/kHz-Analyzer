@echo off
title kHz Analyzer - Executando
cd /d "%~dp0"

echo.
echo  ============================================
echo   kHz Analyzer - iniciando...
echo  ============================================
echo.
echo  Na primeira vez pode demorar (compilando).
echo  Esta janela pode ser fechada depois que o app abrir.
echo.

dotnet run

if errorlevel 1 (
    echo.
    echo  ============================================
    echo   ERRO ao iniciar. Leia a mensagem acima.
    echo  ============================================
    echo.
    pause
)
