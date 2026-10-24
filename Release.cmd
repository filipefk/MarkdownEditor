@echo off
setlocal

REM 1. Publicar em Release (x64): .NET framework-dependent e Windows App SDK self-contained
if exist MarkdownEditor\bin\Publicar rmdir /s /q MarkdownEditor\bin\Publicar
dotnet publish MarkdownEditor\MarkdownEditor.csproj -c Release -r win-x64 --self-contained false -o MarkdownEditor\bin\Publicar
if errorlevel 1 (
    echo Erro ao publicar o projeto.
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
