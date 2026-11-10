param(
    [string]$PortableDir,
    [string]$OutDir,
    [string]$Version
)

# Monta o zip portátil: CS2Optimizer.exe + pasta Backups + LEIA-ME.
# Uso normal: scripts\package-portable.ps1  (depois de scripts\publish.ps1)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($PortableDir)) { $PortableDir = Join-Path $root "dist\portable" }
if ([string]::IsNullOrWhiteSpace($OutDir)) { $OutDir = Join-Path $root "dist" }

$exe = Join-Path $PortableDir "CS2Optimizer.exe"
if (-not (Test-Path $exe)) {
    throw "Não achei $exe. Rode scripts\publish.ps1 antes."
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    $csproj = [xml](Get-Content (Join-Path $root "src\CS2Optimizer.App\CS2Optimizer.App.csproj") -Raw)
    $Version = @($csproj.Project.PropertyGroup.Version) | Where-Object { $_ } | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($Version)) { $Version = "1.0.0" }
}

$packageRoot = Join-Path $root "dist\package"
$stage = Join-Path $packageRoot "CS2Optimizer"
if (Test-Path $packageRoot) { Remove-Item $packageRoot -Recurse -Force }
New-Item -ItemType Directory -Path $stage -Force | Out-Null

Write-Host "==> Montando pasta portátil..." -ForegroundColor Cyan
Copy-Item $exe (Join-Path $stage "CS2Optimizer.exe")
Copy-Item (Join-Path $root "LICENSE") (Join-Path $stage "LICENSE.txt")
Copy-Item (Join-Path $PSScriptRoot "LEIA-ME-portatil.txt") (Join-Path $stage "LEIA-ME.txt")

# O zip precisa da pasta Backups já existente, senão o usuário não sabe onde os
# backups vão cair. Compress-Archive ignora pasta vazia, então vai um aviso dentro.
$backups = Join-Path $stage "Backups"
New-Item -ItemType Directory -Path $backups -Force | Out-Null
$aviso = @(
    "Esta é a pasta onde o programa guarda os backups.",
    "Cada aplicação cria aqui uma subpasta CS2-Optimizer-Backup-<data-hora>, com:",
    "  journal.json        valor ANTERIOR de cada item alterado (é o que o Desfazer usa)",
    "  *.reg               cópia das chaves de registro usadas",
    "  bcd.bak             cópia do BCD",
    "  powerscheme.txt     plano de energia anterior",
    "",
    "Não apague esta pasta enquanto quiser poder desfazer alterações."
)
Set-Content -Path (Join-Path $backups "_LEIA-ME.txt") -Value $aviso -Encoding UTF8

$zip = Join-Path $OutDir ("CS2Optimizer-{0}-portable-win-x64.zip" -f $Version)
if (Test-Path $zip) { Remove-Item $zip -Force }

Write-Host "==> Compactando..." -ForegroundColor Cyan
Compress-Archive -Path (Join-Path $stage "*") -DestinationPath $zip -CompressionLevel Optimal

$sizeMb = [math]::Round((Get-Item $zip).Length / 1MB, 1)
Write-Host "Pronto: $zip ($sizeMb MB)" -ForegroundColor Green
Write-Output $zip
