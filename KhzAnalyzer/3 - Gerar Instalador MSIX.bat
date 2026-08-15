@echo off
title kHz Analyzer - Gerando instalador MSIX
cd /d "%~dp0"

echo.
echo  ============================================
echo   Gerando instalador MSIX
echo  ============================================
echo.
echo  Este e o formato usado pela Microsoft Store.
echo  Depois de instalado, o app aparece no menu
echo  Iniciar como qualquer programa normal.
echo.

winapp pack

if errorlevel 1 (
    echo.
    echo  ERRO ao empacotar. Leia a mensagem acima.
    echo.
    echo  Se aparecer erro de assinatura/certificado, isso e
    echo  esperado num primeiro teste: para instalar localmente
    echo  o pacote precisa ser assinado, e para a Store a
    echo  assinatura e feita pela propria Microsoft.
    echo.
    pause
    exit /b 1
)

echo.
echo  ============================================
echo   Pronto! Procure o arquivo .msix gerado.
echo  ============================================
echo.
pause
