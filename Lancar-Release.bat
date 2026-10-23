@echo off
chcp 65001 >nul
title TrashAll - Lançador de Release

echo =======================================================
echo           🗑️ TrashAll - Lançar Nova Release
echo =======================================================
echo.

set /p VERSION="Digite a versão da release (ex: v1.0.0): "

if "%VERSION%"=="" (
    echo [ERRO] Nenhuma versão informada. Cancelando.
    pause
    exit /b 1
)

:: Garante prefixo 'v'
if not "%VERSION:~0,1%"=="v" (
    set VERSION=v%VERSION%
)

echo.
echo [1/3] Criando commit com alterações pendentes...
git add .
git commit -m "Release %VERSION%" 2>nul

echo [2/3] Criando Tag git %VERSION%...
git tag -a %VERSION% -m "TrashAll %VERSION%"

echo [3/3] Enviando para o GitHub (commits e tags)...
git push origin HEAD --tags

echo.
echo =======================================================
echo  ✅ Tag %VERSION% enviada com sucesso!
echo  O GitHub Actions agora irá compilar e criar a Release
echo  automaticamente com os arquivos portáteis (.zip).
echo =======================================================
echo.
pause
