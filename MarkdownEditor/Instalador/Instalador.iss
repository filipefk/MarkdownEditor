[Languages]
Name: "brazilian"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Setup]
; Identificador fixo da instalação, para upgrades e desinstalação estáveis
AppId=Markdown Editor
ArchitecturesInstallIn64BitMode=x64compatible
AppName=Markdown Editor
AppVersion=2.0.0
AppPublisher=Filipe Kanitz
DefaultDirName={commonpf}\MarkdownEditor
DefaultGroupName=MarkdownEditor
OutputDir=.
SetupIconFile="..\..\Markdown.ico"
UninstallDisplayIcon={app}\MarkdownEditor.exe
OutputBaseFilename=MarkdownEditor-2.0.0
Compression=lzma
SolidCompression=yes
DisableProgramGroupPage=yes
ChangesAssociations=yes

[Files]
; Copia todo o resultado do dotnet publish (inclui wwwroot com o Monaco, Assets e o Windows App SDK self-contained)
Source: "..\bin\Publicar\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

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

[Code]
const
  UrlDotNet = 'https://dotnet.microsoft.com/download/dotnet/10.0';
  UrlWebView2 = 'https://go.microsoft.com/fwlink/p/?LinkId=2124703';
  ChaveWebView2 = 'Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}';

// O WinUI 3 usa só o runtime base do .NET (não o Desktop Runtime do WinForms/WPF).
function DotNetInstalado: Boolean;
var
  Busca: TFindRec;
begin
  Result := FindFirst(ExpandConstant('{commonpf64}\dotnet\shared\Microsoft.NETCore.App\10.*'), Busca);
  if Result then
    FindClose(Busca);
end;

function VersaoValida(Versao: String): Boolean;
begin
  Result := (Versao <> '') and (Versao <> '0.0.0.0');
end;

function WebView2Instalado: Boolean;
var
  Versao: String;
begin
  Result :=
    (RegQueryStringValue(HKLM, 'SOFTWARE\WOW6432Node\' + ChaveWebView2, 'pv', Versao) and VersaoValida(Versao)) or
    (RegQueryStringValue(HKCU, 'Software\' + ChaveWebView2, 'pv', Versao) and VersaoValida(Versao));
end;

procedure OferecerDownload(Mensagem, Url: String);
var
  CodigoErro: Integer;
begin
  if SuppressibleMsgBox(Mensagem + #13#10#13#10 + 'Deseja abrir a página de download agora? A instalação continuará normalmente.',
    mbConfirmation, MB_YESNO, IDNO) = IDYES then
    ShellExec('open', Url, '', '', SW_SHOWNORMAL, ewNoWait, CodigoErro);
end;

function InitializeSetup: Boolean;
begin
  Result := True;

  if not DotNetInstalado then
    OferecerDownload('O Markdown Editor precisa do .NET 10 Runtime (x64), que não foi encontrado neste computador.', UrlDotNet);

  if not WebView2Instalado then
    OferecerDownload('A visualização e a edição usam o Microsoft Edge WebView2 Runtime, que não foi encontrado neste computador.', UrlWebView2);
end;
