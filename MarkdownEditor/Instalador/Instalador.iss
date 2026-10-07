[Languages]
Name: "brazilian"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Setup]
ArchitecturesInstallIn64BitMode=x64compatible
AppName=Markdown Editor
AppVersion=1.0.0
AppPublisher=Filipe Kanitz
DefaultDirName={commonpf}\MarkdownEditor
DefaultGroupName=MarkdownEditor
OutputDir=.
SetupIconFile="..\..\Markdown.ico"
UninstallDisplayIcon={app}\MarkdownEditor.exe
OutputBaseFilename=MarkdownEditor-1.0.0
Compression=lzma
SolidCompression=yes
DisableProgramGroupPage=yes
ChangesAssociations=yes

[Files]
; Copia todos os arquivos da pasta Release (inclui wwwroot e runtimes do WebView2) para a pasta de instalação
Source: "..\bin\Release\net10.0-windows\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Markdown Editor"; Filename: "{app}\MarkdownEditor.exe"
Name: "{commondesktop}\Markdown Editor"; Filename: "{app}\MarkdownEditor.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Criar atalho na área de trabalho"; GroupDescription: "Opções adicionais:"
Name: "associarmd"; Description: "Abrir arquivos .md e .markdown com o Markdown Editor por padrão"; GroupDescription: "Opções adicionais:"; Flags: unchecked

[Registry]
; ProgID usado pelo "Abrir com" e pela associação padrão
Root: HKA; Subkey: "Software\Classes\MarkdownEditor.Arquivo"; ValueType: string; ValueName: ""; ValueData: "Arquivo do Markdown Editor"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\MarkdownEditor.Arquivo\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\MarkdownEditor.exe,0"
Root: HKA; Subkey: "Software\Classes\MarkdownEditor.Arquivo\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\MarkdownEditor.exe"" ""%1"""

; Registro do aplicativo com as extensões suportadas
Root: HKA; Subkey: "Software\Classes\Applications\MarkdownEditor.exe"; ValueType: string; ValueName: "FriendlyAppName"; ValueData: "Markdown Editor"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\Applications\MarkdownEditor.exe\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\MarkdownEditor.exe"" ""%1"""
Root: HKA; Subkey: "Software\Classes\Applications\MarkdownEditor.exe\SupportedTypes"; ValueType: string; ValueName: ".md"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\Applications\MarkdownEditor.exe\SupportedTypes"; ValueType: string; ValueName: ".markdown"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\Applications\MarkdownEditor.exe\SupportedTypes"; ValueType: string; ValueName: ".json"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\Applications\MarkdownEditor.exe\SupportedTypes"; ValueType: string; ValueName: ".xml"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\Applications\MarkdownEditor.exe\SupportedTypes"; ValueType: string; ValueName: ".txt"; ValueData: ""

; Opção no menu "Abrir com" para todas as extensões suportadas
Root: HKA; Subkey: "Software\Classes\.md\OpenWithProgids"; ValueType: string; ValueName: "MarkdownEditor.Arquivo"; ValueData: ""; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\.markdown\OpenWithProgids"; ValueType: string; ValueName: "MarkdownEditor.Arquivo"; ValueData: ""; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\.json\OpenWithProgids"; ValueType: string; ValueName: "MarkdownEditor.Arquivo"; ValueData: ""; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\.xml\OpenWithProgids"; ValueType: string; ValueName: "MarkdownEditor.Arquivo"; ValueData: ""; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\.txt\OpenWithProgids"; ValueType: string; ValueName: "MarkdownEditor.Arquivo"; ValueData: ""; Flags: uninsdeletevalue

; Associação padrão de .md e .markdown (opcional)
Root: HKA; Subkey: "Software\Classes\.md"; ValueType: string; ValueName: ""; ValueData: "MarkdownEditor.Arquivo"; Flags: uninsdeletevalue; Tasks: associarmd
Root: HKA; Subkey: "Software\Classes\.markdown"; ValueType: string; ValueName: ""; ValueData: "MarkdownEditor.Arquivo"; Flags: uninsdeletevalue; Tasks: associarmd

[Run]
; Executa o programa após a instalação
Filename: "{app}\MarkdownEditor.exe"; Description: "Executar o Markdown Editor"; Flags: nowait postinstall skipifsilent
