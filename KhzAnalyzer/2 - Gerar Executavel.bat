@echo off
title kHz Analyzer - Gerando executavel
cd /d "%~dp0"

echo.
echo  ============================================
echo   Gerando o executavel (Release, x64)
echo  ============================================
echo.
echo  Isso demora mais que o modo normal. Aguarde.
echo.

dotnet publish -c Release -r win-x64 --self-contained false

if errorlevel 1 (
    echo.
    echo  ERRO ao gerar. Leia a mensagem acima.
    echo.
    pause
    exit /b 1
)

echo.
echo  ============================================
echo   Pronto! Abrindo a pasta do executavel...
echo  ============================================
echo.

for /d %%D in ("bin\Release\net10.0-windows*") do (
    if exist "%%D\win-x64\publish" (
        start "" explorer "%%~fD\win-x64\publish"
        goto :fim
    )
)

echo  Nao encontrei a pasta publish automaticamente.
echo  Procure manualmente em: bin\Release\
echo.

:fim
pause
