#define MyAppName "CS2 / Windows Optimizer"
#define MyAppVersion "1.0.9"
#define MyAppPublisher "CS2 Optimizer"
#define MyAppExeName "CS2Optimizer.exe"
#define MyAppId "{{8E2E9C1A-6D7B-4E6F-9B3A-2C1D5E7F9A01}"
#define PublishDir "..\dist\publish"

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\CS2 Optimizer
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=..\dist\installer
OutputBaseFilename=CS2Optimizer-Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
UninstallDisplayIcon={app}\{#MyAppExeName}
SetupLogging=yes

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "Criar atalho na área de trabalho"; GroupDescription: "Atalhos:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Desinstalar {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Abrir {#MyAppName}"; Flags: nowait postinstall skipifsilent

[Code]
function IsDotNet8DesktopRuntimeInstalled: Boolean;
var
  Names: TArrayOfString;
  I: Integer;
begin
  Result := False;
  if not RegGetSubkeyNames(HKLM64,
    'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App',
    Names) then
    Exit;

  for I := 0 to GetArrayLength(Names) - 1 do
  begin
    if Pos('8.', Names[I]) = 1 then
    begin
      Result := True;
      Exit;
    end;
  end;
end;

function InitializeSetup: Boolean;
var
  ResultCode: Integer;
begin
  Result := True;

  if IsDotNet8DesktopRuntimeInstalled then
    Exit;

  if MsgBox('O .NET 8 Desktop Runtime não foi encontrado.' + #13#10 + #13#10 +
            'Deseja tentar instalar automaticamente pelo winget agora?',
            mbConfirmation, MB_YESNO) = IDYES then
  begin
    if (not Exec('winget.exe',
                 'install --id Microsoft.DotNet.DesktopRuntime.8 -e --silent ' +
                 '--accept-package-agreements --accept-source-agreements',
                 '', SW_HIDE, ewWaitUntilTerminated, ResultCode)) or (ResultCode <> 0) then
    begin
      MsgBox('Não foi possível instalar o .NET 8 Desktop Runtime automaticamente.' + #13#10 +
             'Baixe em https://dotnet.microsoft.com/download/dotnet/8.0 e rode o instalador novamente.',
             mbError, MB_OK);
      Result := False;
    end;
  end
  else
  begin
    MsgBox('O programa precisa do .NET 8 Desktop Runtime.' + #13#10 +
           'Baixe em https://dotnet.microsoft.com/download/dotnet/8.0 e rode o instalador novamente.',
           mbInformation, MB_OK);
    Result := False;
  end;
end;


