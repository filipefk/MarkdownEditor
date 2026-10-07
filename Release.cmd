@echo off
setlocal

REM 1. Build completo da solução em modo Release
dotnet build MarkdownEditor.slnx -c Release
if errorlevel 1 (
    echo Erro ao compilar a solução.
	pause
    exit /b 1
)

REM 2. Excluir o arquivo do instalador anterior, se existir
if exist MarkdownEditor\Instalador\MarkdownEditor-*.exe (
    del /f /q MarkdownEditor\Instalador\MarkdownEditor-*.exe
)

REM 3. Buildar o instalador com o Inno Setup
iscc MarkdownEditor\Instalador\Instalador.iss
if errorlevel 1 (
    echo Erro ao gerar o instalador.
	pause
    exit /b 1
)

echo Processo concluído com sucesso.
endlocal
pause
