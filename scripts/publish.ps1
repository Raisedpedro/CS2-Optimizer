param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$SkipInstaller,
    [switch]$SkipZip
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "src\CS2Optimizer.App\CS2Optimizer.App.csproj"
$portableDir = Join-Path $root "dist\portable"
$publishDir = Join-Path $root "dist\publish"
$installerDir = Join-Path $root "dist\installer"

Write-Host "==> Limpando dist..." -ForegroundColor Cyan
foreach ($dir in @($portableDir, $publishDir, $installerDir)) {
    if (Test-Path $dir) { Remove-Item $dir -Recurse -Force }
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
}

# Zip de versão anterior não pode sobrar: o workflow publica dist/*.zip e
# subiria duas versões no mesmo Release.
Get-ChildItem -Path (Join-Path $root "dist") -Filter "*.zip" -File -ErrorAction SilentlyContinue |
    Remove-Item -Force

Write-Host "==> Publicando portátil autocontido (arquivo único)..." -ForegroundColor Cyan
dotnet publish $project -c $Configuration -r $Runtime --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None -p:DebugSymbols=false -o $portableDir

if ($LASTEXITCODE -ne 0) { throw "Falha ao publicar o portátil." }

if (-not $SkipZip) {
    Write-Host "==> Montando o zip portátil (exe + Backups + LEIA-ME)..." -ForegroundColor Cyan
    & (Join-Path $PSScriptRoot "package-portable.ps1") -PortableDir $portableDir
}

Write-Host "==> Publicando versão para o instalador (dependente do runtime)..." -ForegroundColor Cyan
dotnet publish $project -c $Configuration -r $Runtime --self-contained false `
    -p:PublishSingleFile=false -p:DebugType=None -p:DebugSymbols=false -o $publishDir

if ($LASTEXITCODE -ne 0) { throw "Falha ao publicar a versão do instalador." }

if ($SkipInstaller) {
    Write-Host "Instalador ignorado (-SkipInstaller)." -ForegroundColor Yellow
    exit 0
}

$iscc = Get-Command "ISCC.exe" -ErrorAction SilentlyContinue
if (-not $iscc) {
    $candidates = @(
        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
        "C:\Program Files\Inno Setup 6\ISCC.exe"
    )
    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) { $iscc = Get-Item $candidate; break }
    }
}

if (-not $iscc) {
    Write-Warning "Inno Setup 6 não encontrado. Publique a pasta e compile installer\installer.iss manualmente."
    exit 0
}

Write-Host "==> Compilando instalador com Inno Setup..." -ForegroundColor Cyan
& $iscc.Source (Join-Path $root "installer\installer.iss")
if ($LASTEXITCODE -ne 0) { throw "Falha ao compilar o instalador." }

Write-Host "Pronto. Saídas em dist\portable e dist\installer." -ForegroundColor Green
