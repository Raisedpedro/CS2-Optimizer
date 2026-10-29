<#
.SYNOPSIS
  CS2 / Windows Optimizer - diagnostico + ajustes seguros para CS2 (com interface grafica).

.DESCRIPTION
  Abre uma janela com tres abas:
    Diagnostico - le a maquina e aponta o que esta fora do ideal (nao altera nada)
    Correcoes   - lista o que pode ser aplicado; cada item marcado pede confirmacao
    Checklist   - o que o script nao consegue fazer (BIOS, driver, CS2, etc.)

  Antes de alterar qualquer coisa: export .reg, export do BCD e a pergunta
  "criar ponto de restauracao?" (nunca automatico - quem decide e voce).
  A janela tem botao para desfazer (importa o backup de novo).

  Backup e log ficam na MESMA pasta do .exe/.ps1 (do lado do programa).
  So cai para a Area de Trabalho se aquela pasta nao aceitar gravacao.

.PARAMETER Console
  Roda no modo texto antigo, sem interface.

.PARAMETER ReportOnly
  So diagnostica, nao altera nada (vale nos dois modos).

.PARAMETER Auto
  Modo texto: aplica sem perguntar apenas as correcoes "seguras".

.EXAMPLE
  Abrir CS2-Optimizer.bat     # duplo clique: abre a interface sem janela preta
  .\CS2-Optimizer.ps1         # abre a interface
  .\CS2-Optimizer.ps1 -Console      # modo texto interativo
  .\CS2-Optimizer.ps1 -ReportOnly   # so relatorio (interface)
  .\CS2-Optimizer.ps1 -Console -Auto
#>
[CmdletBinding()]
param(
    [switch]$Console,
    [switch]$ReportOnly,
    [switch]$Auto
)

$ErrorActionPreference = 'Continue'

$script:ModoConsole   = [bool]$Console
$script:UI            = $false
$script:Auto          = [bool]$Auto
$script:ReportOnly    = [bool]$ReportOnly
$script:ConfirmarCada = $true

# ----------------------------------------------------------------------------
# Auto-elevacao
# ----------------------------------------------------------------------------
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    # Rodando como .exe (ps2exe): relanca o proprio executavel pedindo elevacao
    if ($PSCommandPath -like '*.exe') {
        try { Start-Process -FilePath $PSCommandPath -Verb RunAs | Out-Null } catch {}
        exit
    }
    $a = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-STA', '-File', ('"{0}"' -f $PSCommandPath))
    if ($script:ModoConsole) { $a += '-Console' }
    if ($script:ReportOnly) { $a += '-ReportOnly' }
    if ($script:Auto) { $a += '-Auto' }
    # na interface, sobe sem a janela preta do console atrapalhando
    if (-not $script:ModoConsole) { $a += @('-WindowStyle', 'Hidden') }
    try { Start-Process powershell.exe -Verb RunAs -ArgumentList $a | Out-Null } catch {}
    exit
}

# ----------------------------------------------------------------------------
# Infra: caminhos, log, colecoes
# ----------------------------------------------------------------------------
$stamp            = Get-Date -Format 'yyyyMMdd-HHmmss'
$script:Desktop   = [Environment]::GetFolderPath('Desktop')
if ([string]::IsNullOrWhiteSpace($script:Desktop)) { $script:Desktop = [Environment]::GetFolderPath('DesktopDirectory') }
if ([string]::IsNullOrWhiteSpace($script:Desktop)) { $script:Desktop = Join-Path $env:USERPROFILE 'Desktop' }
if (-not (Test-Path -LiteralPath $script:Desktop)) { $script:Desktop = $env:USERPROFILE }

# Onde esta o arquivo que a pessoa abriu (o .exe ou o .ps1). O backup e o log
# ficam NESSA pasta, do lado do programa - nao mais na Area de Trabalho.
function Get-PastaDoPrograma {
    # .exe compilado (ps2exe): MainModule aponta para o proprio executavel.
    # .ps1 rodando pelo powershell.exe: MainModule aponta para o powershell
    # (C:\Windows\System32), que NAO e a pasta do programa -> usa o caminho do
    # proprio script.
    try {
        $mm = [System.Diagnostics.Process]::GetCurrentProcess().MainModule
        if ($mm -and $mm.FileName) {
            $nome = [System.IO.Path]::GetFileNameWithoutExtension($mm.FileName)
            if ($nome -ne 'powershell' -and $nome -ne 'pwsh') {
                $pasta = Split-Path -Parent $mm.FileName
                if ($pasta -and (Test-Path -LiteralPath $pasta)) { return $pasta }
            }
        }
    } catch {}
    foreach ($x in @($PSCommandPath, $PSScriptRoot, $MyInvocation.MyCommand.Path)) {
        if ($x) {
            $pasta = Split-Path -Parent $x
            if ($pasta -and (Test-Path -LiteralPath $pasta)) { return $pasta }
        }
    }
    return $null
}

# Escreve um arquivo de teste e apaga: e o unico jeito confiavel de saber se a
# pasta aceita gravacao (Program Files, pendrive protegido, pasta de rede...).
function Test-PastaGravavel([string]$dir) {
    if ([string]::IsNullOrWhiteSpace($dir)) { return $false }
    if (-not (Test-Path -LiteralPath $dir)) { return $false }
    $sonda = Join-Path $dir ('.cs2opt-teste-' + [guid]::NewGuid().ToString('N').Substring(0, 8) + '.tmp')
    try {
        [System.IO.File]::WriteAllText($sonda, 'x')
        Remove-Item -LiteralPath $sonda -Force -ErrorAction SilentlyContinue
        return $true
    } catch {
        return $false
    }
}

$script:AppDir  = Get-PastaDoPrograma
if (-not (Test-PastaGravavel $script:AppDir)) { $script:AppDir = $null }
# sem pasta gravavel do programa (raro), volta para a Area de Trabalho
$script:BaseDir   = if ($script:AppDir) { $script:AppDir } else { $script:Desktop }
$script:BackupDir = Join-Path $script:BaseDir "CS2-Optimizer-Backup-$stamp"
$script:LogFile   = Join-Path $script:BaseDir "CS2-Optimizer-Log-$stamp.txt"
$script:Stamp     = $stamp

# Log proprio (Write-FileLog) cobre os dois modos; nao usamos Start-Transcript
# porque ele segura o arquivo aberto e o nosso log escreve no mesmo caminho.

$script:Diag     = New-Object System.Collections.ArrayList
$script:Manual   = New-Object System.Collections.ArrayList
$script:Problems = New-Object System.Collections.ArrayList
$script:Applied  = New-Object System.Collections.ArrayList
$script:Skipped  = New-Object System.Collections.ArrayList
$script:Fixes    = @()
$script:Ctx      = @{}
$script:NeedReboot = $false
$script:BackupFeito = $false
# Ponto de restauracao: NADA automatico. A pessoa responde uma vez por execucao.
$script:CriarPonto      = $false

# Stub: o modo interface instala a implementacao real em $script:SetBusyImpl
function Set-Busy([bool]$ocupado, [string]$texto) {
    if ($script:SetBusyImpl) { & $script:SetBusyImpl $ocupado $texto }
}

function Write-FileLog([string]$t) {
    try { Add-Content -LiteralPath $script:LogFile -Value ("{0}  {1}" -f (Get-Date -Format 'HH:mm:ss'), $t) -Encoding UTF8 } catch {}
}

function Pump {
    if (-not $script:UI) { return }
    try { [System.Windows.Forms.Application]::DoEvents() } catch {}
}

# Mensagens do diagnostico (vai para a aba Diagnostico e para o log)
function Emit([string]$Kind, [string]$Text) {
    [void]$script:Diag.Add([pscustomobject]@{ Kind = $Kind; Text = $Text })
    Write-FileLog ("[{0}] {1}" -f $Kind, $Text)
    if ($Kind -eq 'warn' -or $Kind -eq 'bad') { [void]$script:Problems.Add($Text) }

    if ($script:UI) { Add-UiDiag $Kind $Text; return }

    $cores = @{ section = 'Cyan'; info = 'Gray'; ok = 'Green'; warn = 'Yellow'; bad = 'Red'; todo = 'White' }
    $prefixo = @{ section = ''; info = '  [i] '; ok = '  [OK] '; warn = '  [!] '; bad = '  [X] '; todo = '  [ ] ' }
    if ($Kind -eq 'section') {
        Write-Host ''
        Write-Host ("=== " + $Text + " ===") -ForegroundColor Cyan
    } else {
        Write-Host ($prefixo[$Kind] + $Text) -ForegroundColor $cores[$Kind]
    }
}

function Section($t) { Emit 'section' $t }
function Info($t)    { Emit 'info'    $t }
function Ok($t)      { Emit 'ok'      $t }
function Warn($t)    { Emit 'warn'    $t }
function Bad($t)     { Emit 'bad'     $t }
function Todo($t)    { if (-not ($script:Manual -contains $t)) { [void]$script:Manual.Add($t) } }

# Mensagens de acao (vai para o painel de log e para o console)
function Note([string]$Text, [string]$Kind = 'info') {
    Write-FileLog ('    ' + $Text)
    if ($script:UI) {
        if (Get-Command Add-UiNote -ErrorAction SilentlyContinue) { Add-UiNote $Text $Kind }
    } else {
        $cores = @{ info = 'Gray'; ok = 'Green'; warn = 'Yellow'; bad = 'Red'; titulo = 'Cyan' }
        Write-Host ('  ' + $Text) -ForegroundColor $cores[$Kind]
    }
    Pump
}

# ----------------------------------------------------------------------------
# Helpers de registro / arquivos / processos
# ----------------------------------------------------------------------------
function Get-Reg($path, $name) {
    try { $o = Get-ItemProperty -Path $path -Name $name -ErrorAction SilentlyContinue; if ($o) { return $o.$name } } catch {}
    return $null
}

function Set-Reg($path, $name, $value, $type = 'DWord') {
    if (-not (Test-Path $path)) { New-Item -Path $path -Force | Out-Null }
    New-ItemProperty -Path $path -Name $name -Value $value -PropertyType $type -Force | Out-Null
}

function Bytes-ToGB($b) { [math]::Round($b / 1GB, 1) }

# CORRIGIDO: Get-ChildItem -Recurse tambem devolve pastas, e pasta nao tem
# 'Length' -> o Measure-Object antigo estourava (MeasurePropertyNotFound).
function Get-FolderSizeMB($p) {
    if ([string]::IsNullOrWhiteSpace($p)) { return 0 }
    if (-not (Test-Path -LiteralPath $p)) { return 0 }
    $s = 0
    try {
        $s = (Get-ChildItem -LiteralPath $p -Recurse -Force -File -ErrorAction SilentlyContinue |
              Measure-Object -Property Length -Sum).Sum
    } catch { $s = 0 }
    if (-not $s) { return 0 }
    Pump
    return [math]::Round($s / 1MB, 1)
}

# Le o que apareceu de novo num arquivo que outro processo esta escrevendo.
# Usa FileShare.ReadWrite para nao brigar com o escritor.
function Read-TextoNovo([string]$arquivo, [ref]$pos) {
    if (-not (Test-Path -LiteralPath $arquivo)) { return '' }
    $fs = $null
    $sb = New-Object System.Text.StringBuilder
    try {
        $share = [System.IO.FileShare]::ReadWrite -bor [System.IO.FileShare]::Delete
        $fs = [System.IO.File]::Open($arquivo, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, $share)
        if ($fs.Length -le $pos.Value) { return '' }
        [void]$fs.Seek($pos.Value, [System.IO.SeekOrigin]::Begin)
        $buf = New-Object byte[] 8192
        while ($true) {
            $n = $fs.Read($buf, 0, $buf.Length)
            if ($n -le 0) { break }
            [void]$sb.Append([System.Text.Encoding]::Default.GetString($buf, 0, $n))
            $pos.Value += $n
        }
        return $sb.ToString()
    } catch {
        return ''
    } finally {
        if ($fs) { $fs.Dispose() }
    }
}

# Roda um executavel externo. No modo texto mostra a saida ao vivo; na interface
# redireciona para arquivo (nao trava a janela) e mostra o progresso ao vivo.
function Invoke-Exe([string]$exe, [string[]]$argv, [string]$rotulo) {
    if (-not $script:UI) {
        Note ("  executando: $exe " + ($argv -join ' '))
        & $exe @argv
        return $LASTEXITCODE
    }

    # Os arquivos de saida ficam na pasta de backup (caminho curto, conhecido e
    # gravavel). Usar $env:TEMP dava "caminho nao encontrado" com o nome 8.3
    # (C:\Users\USUARIO~1) quando o perfil tem acento no nome.
    $dir = $script:BackupDir
    if (-not $dir -or -not (Test-Path -LiteralPath $dir)) { $dir = $script:BaseDir }
    if (-not $dir -or -not (Test-Path -LiteralPath $dir)) { $dir = $script:Desktop }
    $base = 'saida-' + ($rotulo -replace '[^A-Za-z0-9]+', '-').Trim('-')
    $outFile = Join-Path $dir ($base + '.log')
    $errFile = Join-Path $dir ($base + '.err.log')
    $codeFile = Join-Path $dir ($base + '.codigo.txt')
    foreach ($f in @($outFile, $errFile, $codeFile)) {
        try { if (Test-Path -LiteralPath $f) { Remove-Item -LiteralPath $f -Force } } catch {}
    }

    $argLine = ($argv | ForEach-Object { if ($_ -match '[\s"]') { '"{0}"' -f $_ } else { $_ } }) -join ' '
    # Uma linha de cmd com expansao atrasada (!ERRORLEVEL!) para pegar o codigo
    # de saida. Nada de .cmd intermediario: arquivo em ANSI quebrava o caminho
    # quando a pasta tem acento e o codigo vinha vazio.
    $linhaCmd = ('{0} {1} 1>"{2}" 2>"{3}" & (echo !ERRORLEVEL!)>"{4}"' -f $exe, $argLine, $outFile, $errFile, $codeFile)
    try {
        $p = Start-Process -FilePath 'cmd.exe' -ArgumentList ('/v:on /c "' + $linhaCmd + '"') -PassThru -WindowStyle Hidden
    } catch {
        Note ('  nao consegui iniciar ' + $exe + ': ' + $_.Exception.Message) 'bad'
        return -1
    }

    # ---- progresso ao vivo ----
    # linhas completas vao para o painel; o pedaco em andamento (as barras
    # "==== 57.4% ====" que o DISM reescreve com \r) vai para a barra de status
    $ini = Get-Date
    $posOut = [ref]0
    $pend = ''
    while (-not $p.HasExited) {
        $novo = Read-TextoNovo $outFile $posOut
        if ($novo) {
            $pend += $novo
            while ($pend.Contains("`n")) {
                $i = $pend.IndexOf("`n")
                $linha = $pend.Substring(0, $i)
                $pend = $pend.Substring($i + 1)
                $linha = $linha.TrimEnd("`r")
                if ($linha.Contains("`r")) { $linha = ($linha -split "`r")[-1] }
                $linha = $linha.Trim()
                if ($linha) {
                    if ($linha.Length -gt 200) { $linha = $linha.Substring(0, 200) + '...' }
                    Note ('    ' + $linha)
                }
            }
        }
        $atual = if ($pend.Contains("`r")) { ($pend -split "`r")[-1] } else { $pend }
        $atual = $atual.Trim()
        if ($atual.Length -gt 60) { $atual = '...' + $atual.Substring($atual.Length - 57) }
        $texto = "{0} - {1:mm\:ss}" -f $rotulo, ((Get-Date) - $ini)
        if ($atual) { $texto += '   ' + $atual }
        Set-Busy $true $texto
        Start-Sleep -Milliseconds 400
        Pump
    }
    [void]$p.WaitForExit()

    # ---- resto da saida que ficou no buffer ----
    $novo = Read-TextoNovo $outFile $posOut
    if ($novo) { $pend += $novo }
    if ($pend.Trim()) {
        foreach ($linha in ($pend -split "[\r\n]+")) {
            $linha = $linha.Trim()
            if (-not $linha) { continue }
            if ($linha.Length -gt 200) { $linha = $linha.Substring(0, 200) + '...' }
            Note ('    ' + $linha)
        }
    }
    $pend = ''

    $errtxt = ''
    try {
        if (Test-Path -LiteralPath $errFile) { $errtxt = Get-Content -LiteralPath $errFile -Raw -ErrorAction SilentlyContinue }
    } catch {}
    if ($errtxt -and $errtxt.Trim()) {
        foreach ($linha in @($errtxt -split "[\r\n]+" | Where-Object { $_.Trim() } | Select-Object -Last 8)) {
            Note ('    [!] ' + $linha.Trim()) 'warn'
        }
    }

    # ---- codigo de saida ----
    $codigo = -1
    try {
        if (Test-Path -LiteralPath $codeFile) {
            $txtCod = (Get-Content -LiteralPath $codeFile -Raw -ErrorAction SilentlyContinue)
            if ($txtCod) { [void][int]::TryParse($txtCod.Trim(), [ref]$codigo) }
        }
    } catch { $codigo = -1 }
    if ($codigo -eq -1) { Note '    (nao consegui ler o codigo de saida)' 'warn' }
    else { Note ("    (codigo de saida: $codigo)") }

    foreach ($f in @($outFile, $errFile, $codeFile)) {
        try { Remove-Item -LiteralPath $f -Force -ErrorAction SilentlyContinue } catch {}
    }
    return $codigo
}

function Get-SteamLibraries {
    $steamPath = Get-Reg 'HKCU:\Software\Valve\Steam' 'SteamPath'
    if (-not $steamPath) { $steamPath = Get-Reg 'HKLM:\SOFTWARE\WOW6432Node\Valve\Steam' 'InstallPath' }
    if (-not $steamPath) { $steamPath = Get-Reg 'HKLM:\SOFTWARE\Valve\Steam' 'InstallPath' }
    if (-not $steamPath) {
        $sp = Get-Process steam -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($sp -and $sp.Path) { $steamPath = Split-Path $sp.Path }
    }
    if (-not $steamPath) {
        foreach ($c in @("${env:ProgramFiles(x86)}\Steam", "$env:ProgramFiles\Steam", 'C:\Steam', 'D:\Steam', 'D:\SteamLibrary', 'E:\Steam', 'E:\SteamLibrary')) {
            if (Test-Path (Join-Path $c 'steamapps')) { $steamPath = $c; break }
        }
    }
    $libs = @()
    if ($steamPath) {
        $steamPath = $steamPath -replace '/', '\'
        $libs += $steamPath
        $vdf = Join-Path $steamPath 'steamapps\libraryfolders.vdf'
        if (Test-Path $vdf) {
            foreach ($m in [regex]::Matches((Get-Content -LiteralPath $vdf -Raw), '"path"\s+"([^"]+)"')) {
                $libs += ($m.Groups[1].Value -replace '\\\\', '\')
            }
        }
        $libs = @($libs | Select-Object -Unique)
    }
    return [pscustomobject]@{ SteamPath = $steamPath; Libraries = $libs }
}

# Fecha Steam/CS2 quando a acao precisa (cache, configs)
function Stop-SteamCS2 {
    $procs = Get-Process steam, steamwebhelper, cs2 -ErrorAction SilentlyContinue
    if (-not $procs) { return }
    if ((Ask 'Steam/CS2 estao abertos. Fechar agora?' 'safe') -or $script:Auto) {
        Note '  fechando Steam/CS2...' 'warn'
        $procs | Stop-Process -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 3
        Pump
    } else { throw 'Feche a Steam/CS2 e tente de novo.' }
}
# ============================================================================
# FASE 1 - DIAGNOSTICO (somente leitura)
# ============================================================================
function Invoke-Diagnostics {
    $script:Diag.Clear()
    $script:Manual.Clear()
    $script:Problems.Clear()
    $script:Ctx = @{}
    $Ctx = $script:Ctx

    Section 'Sistema'
    $os    = Get-CimInstance Win32_OperatingSystem
    $cs    = Get-CimInstance Win32_ComputerSystem
    $bios  = Get-CimInstance Win32_BIOS
    $board = Get-CimInstance Win32_BaseBoard
    $enc   = Get-CimInstance Win32_SystemEnclosure
    Pump
    $build   = [int]$os.BuildNumber
    $isWin11 = ($build -ge 22000)
    $Ctx.IsWin11 = $isWin11
    Info "$($os.Caption) (build $build)"
    Info "Placa-mae: $($board.Manufacturer) $($board.Product)"
    Info "Maquina: $($cs.Manufacturer) $($cs.Model)".Trim()

    # CORRIGIDO: em Windows 11 o CIM ja devolve ReleaseDate como DateTime;
    # passar isso no ManagementDateTimeConverter estourava e a data saia vazia.
    $biosDate = $null
    $rd = $bios.ReleaseDate
    if ($rd -is [datetime]) { $biosDate = $rd }
    elseif ($rd) {
        try { $biosDate = [Management.ManagementDateTimeConverter]::ToDateTime($rd.ToString()) } catch {}
    }
    if ($biosDate) {
        Info "BIOS: $($bios.SMBIOSBIOSVersion) | $($bios.Manufacturer) | data: $($biosDate.ToString('dd/MM/yyyy'))"
        if ($biosDate -lt (Get-Date).AddMonths(-12)) { Warn 'BIOS com mais de 12 meses. Veja se ha atualizacao no site do fabricante da placa-mae.' }
    } else {
        Info "BIOS: $($bios.SMBIOSBIOSVersion) | $($bios.Manufacturer) | data: nao informada (confira em msinfo32 > Versao/data do BIOS)"
    }

    Todo 'Atualizar BIOS (site do fabricante da placa; energia estavel durante o processo) e driver de CHIPSET (AMD/Intel - vale pelo chipset, ex.: B550).'
    Todo 'Se possivel, formatar com pendrive bootavel (Rufus) e Windows 11 limpo; depois chipset e drivers da placa-mae. Formatar pelo proprio Windows deixa arquivos orfaos.'
    if (-not $isWin11) { Info 'Windows 10 detectado: se possivel migre para o Windows 11 (o HAGS, por exemplo, so compensa nele).' }

    $isLaptop = $false
    if (@($enc.ChassisTypes) | Where-Object { $_ -in 8, 9, 10, 11, 14, 30, 31, 32 }) { $isLaptop = $true }
    if ($isLaptop) { Warn 'Notebook detectado: limite de energia/temperatura pesa muito. Jogue na tomada, plano maximo e base/cooler.' }

    Section 'CPU'
    $cpu = Get-CimInstance Win32_Processor | Select-Object -First 1
    Info "$($cpu.Name)"
    Info "$($cpu.NumberOfCores) nucleos / $($cpu.NumberOfLogicalProcessors) threads - clock max. reportado: $($cpu.MaxClockSpeed) MHz"
    Todo 'Ganho grande de FPS: overclock de CPU (PBO / Curve Optimizer em Ryzen) e de RAM (frequencia/timings). Tweaks de registro "milagrosos" so causam instabilidade.'
    Todo 'Com HWiNFO + Cinebench, confira se o clock efetivo chega perto do esperado sob carga. Bem abaixo = limite de energia/temperatura/PBO mal ajustado.'

    Section 'Memoria RAM'
    $ram = @(Get-CimInstance Win32_PhysicalMemory)
    $Ctx.Ram = $ram
    $totalGB = Bytes-ToGB (@($ram | Measure-Object -Property Capacity -Sum).Sum)
    Info "Total: $totalGB GB em $($ram.Count) pente(s)"
    foreach ($m in $ram) {
        Info ("  {0} | {1} GB | rated {2} MHz | atual {3} MHz | {4}" -f $m.DeviceLocator, (Bytes-ToGB $m.Capacity), $m.Speed, $m.ConfiguredClockSpeed, $m.Manufacturer)
    }
    $locs = @($ram | Select-Object -ExpandProperty DeviceLocator -Unique)
    if ($ram.Count -ge 2 -and $locs.Count -lt $ram.Count) {
        Info 'A BIOS reporta o mesmo nome de slot para os pentes; nao da para confirmar os slots por script. Use o CPU-Z.'
    }
    if ($ram.Count -eq 1) {
        Bad 'Apenas 1 pente de RAM = single channel. Use 2 pentes identicos (dual channel) - um dos maiores ganhos no CS2.'
    } elseif ($ram.Count -ge 2) {
        Ok "$($ram.Count) pentes (confirme no manual da placa se estao nos slots corretos, em geral A2 e B2 / slots 2 e 4)."
        Todo 'Confirme no CPU-Z (aba Memory) que aparece "Dual" e que os pentes estao nos slots recomendados pelo manual.'
    }
    $memType = ($ram | Select-Object -First 1).SMBIOSMemoryType
    $cfg = ($ram | Measure-Object -Property ConfiguredClockSpeed -Maximum).Maximum
    if ($memType -eq 26 -and $cfg -le 2666) {
        Bad "DDR4 rodando a $cfg MHz: provavelmente XMP/DOCP desativado na BIOS."
        Todo 'Ativar XMP/DOCP/EXPO na BIOS.'
    } elseif ($memType -eq 34 -and $cfg -le 4800) {
        Bad "DDR5 rodando a $cfg MHz: provavelmente XMP/EXPO desativado na BIOS."
        Todo 'Ativar XMP/EXPO na BIOS.'
    } elseif ($memType -eq 34 -and $cfg -gt 6400) {
        Warn "DDR5 acima de 6400 MHz: em Ryzen pode cair em UCLK = MCLK/2. Confira MCLK x UCLK no Zen Timings."
    } else {
        Ok "Frequencia da RAM parece aplicada ($cfg MHz)."
    }

    Section 'GPU / Video'
    $gpus = @(Get-CimInstance Win32_VideoController)
    foreach ($g in $gpus) {
        Info "$($g.Name) | driver $($g.DriverVersion) | $($g.CurrentHorizontalResolution)x$($g.CurrentVerticalResolution) @ $($g.CurrentRefreshRate) Hz"
    }
    $hasNvidia = [bool]($gpus | Where-Object { $_.Name -match 'NVIDIA' })
    $hasAmd    = [bool]($gpus | Where-Object { $_.Name -match 'AMD|Radeon' })
    $nvsmi = Get-Command nvidia-smi.exe -ErrorAction SilentlyContinue
    if (-not $nvsmi -and (Test-Path "$env:SystemRoot\System32\nvidia-smi.exe")) { $nvsmi = "$env:SystemRoot\System32\nvidia-smi.exe" }
    if ($hasNvidia -and $nvsmi) {
        try {
            $q = & $nvsmi --query-gpu=pcie.link.gen.current,pcie.link.gen.max,pcie.link.width.current,power.limit --format=csv,noheader 2>$null
            Info "NVIDIA PCIe/power (gen atual, gen max, largura, power limit): $q"
            $mem = (& $nvsmi -q -d MEMORY 2>$null) -join "`n"
            if ($mem -match '(?s)BAR1 Memory Usage\s+Total\s+:\s+(\d+)\s+MiB') {
                $bar = [int]$Matches[1]
                if ($bar -gt 512) { Info "Resizable BAR: ATIVO (BAR1 = $bar MiB). Em NVIDIA o CS2 geralmente ignora - sem problema." }
                else { Info "Resizable BAR: desativado (BAR1 = $bar MiB)." }
            }
        } catch {}
    }
    if ($hasAmd) {
        Warn 'GPU AMD: Smart Access Memory/Resizable BAR vale para todos os jogos; no CS2 (limitado por CPU) pode ate perder desempenho. Teste ligado e desligado.'
        Todo 'AMD Adrenalin: resetar para padrao; desligar Radeon Boost / Super Resolution / Anti-Lag+ e overlay.'
    }
    if ($hasNvidia) {
        Todo 'NVIDIA - instalacao limpa: baixe o driver antes, rode o DDU (modo seguro, "Limpar e reiniciar") e instale so o driver, SEM o NVIDIA App. Nunca instale um driver por cima do outro.'
        Todo 'NVIDIA App/GeForce Experience: desative o overlay (Alt+Z), filtros de jogo e o Instant Replay.'
        Todo 'Painel NVIDIA > Gerenciar configuracoes 3D: global no padrao; no perfil do cs2.exe "Modo de gerenciamento de energia" = Preferir desempenho maximo. "Modo de baixa latencia" desligado se usar o Reflex no jogo.'
        Todo 'Painel NVIDIA > Alterar resolucao: escolha a taxa maxima do monitor na secao "PC" (a secao Ultra HD/HD nao mostra o maior Hz). Cor: "Usar configuracoes de cor NVIDIA".'
    }
    if ($gpus.Count -gt 1 -and ($gpus | Where-Object { $_.Name -match 'Intel|UHD|Radeon Graphics|Radeon\(TM\) Graphics' })) {
        Warn 'Mais de uma GPU (integrada + dedicada). Confirme que o monitor esta ligado na placa dedicada, nao na placa-mae.'
    }
    Todo 'Confirme a taxa de atualizacao real do monitor (OSD ou testufo.com). Se estiver abaixo do maximo, ajuste pelo painel do driver (nao pelo Windows).'
    Todo 'Windows > Sistema > Tela > Elementos graficos: confirme que o cs2.exe esta com "Alto desempenho" (o script define isso).'

    Section 'Armazenamento'
    $vols = @(Get-Volume | Where-Object { $_.DriveType -eq 'Fixed' -and $_.DriveLetter })
    $ssdLetters = @()
    foreach ($v in $vols) {
        $pct = 0
        if ($v.Size -gt 0) { $pct = [math]::Round(($v.SizeRemaining / $v.Size) * 100, 1) }
        $media = 'Desconhecido'
        try {
            $disk = Get-Partition -DriveLetter $v.DriveLetter -ErrorAction Stop | Get-Disk
            $pd = Get-PhysicalDisk | Where-Object { "$($_.DeviceId)" -eq "$($disk.Number)" } | Select-Object -First 1
            if ($pd) { $media = "$($pd.MediaType)" }
        } catch {}
        $line = "{0}: {1} | {2} GB livres de {3} GB ({4}%)" -f $v.DriveLetter, $media, (Bytes-ToGB $v.SizeRemaining), (Bytes-ToGB $v.Size), $pct
        if ($pct -lt 15) { Warn "$line -> menos de 15% livre (ideal 15-20% em SSD)" } else { Info $line }
        if ($media -eq 'SSD') { $ssdLetters += $v.DriveLetter }
    }
    Pump
    $Ctx.SsdLetters = $ssdLetters

    Section 'Steam / CS2'
    $steam = Get-SteamLibraries
    $steamPath = $steam.SteamPath
    $libraries = $steam.Libraries
    $Ctx.SteamPath = $steamPath
    if ($steamPath -and (Test-Path $steamPath)) { Info "Steam: $steamPath" } else { Warn 'Steam nao encontrada no registro.' }

    $cs2Exe = $null
    foreach ($lib in $libraries) {
        $p = Join-Path $lib 'steamapps\common\Counter-Strike Global Offensive\game\bin\win64\cs2.exe'
        if (Test-Path $p) { $cs2Exe = $p; break }
    }
    $Ctx.Cs2Exe = $cs2Exe
    if ($cs2Exe) {
        Ok "CS2: $cs2Exe"
        $drv = $cs2Exe.Substring(0, 1).ToUpper()
        if ($ssdLetters -and ($ssdLetters -notcontains $drv)) { Bad "O CS2 nao esta em um SSD (drive $drv). Mova para um SSD." }
    } else {
        Warn 'cs2.exe nao encontrado nas bibliotecas Steam.'
    }

    # Flags de compatibilidade do cs2.exe
    $fsoDisabled = $false
    $layerPaths = @('HKCU:\Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers', 'HKLM:\Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers')
    $Ctx.LayerPaths = $layerPaths
    if ($cs2Exe) {
        foreach ($lp in $layerPaths) {
            $v = Get-Reg $lp $cs2Exe
            if ($v -and $v -match 'DISABLEDXMAXIMIZEDWINDOWEDMODE') { $fsoDisabled = $true; Warn "cs2.exe tem 'Desativar otimizacoes de tela inteira' MARCADO ($lp). O ideal e desmarcado." }
        }
        if (-not $fsoDisabled) { Ok 'Otimizacoes de tela inteira do cs2.exe: nao desativadas.' }
    }
    $Ctx.FsoDisabled = $fsoDisabled

    # Cache de shaders
    $steamShader = @()
    foreach ($lib in $libraries) {
        $sp730 = Join-Path $lib 'steamapps\shadercache\730'
        if (Test-Path $sp730) { $steamShader += $sp730 }
    }
    $shaderPaths = @(
        "$env:LOCALAPPDATA\D3DSCache",
        "$env:LOCALAPPDATA\NVIDIA\DXCache",
        "$env:LOCALAPPDATA\NVIDIA\GLCache",
        "$env:LOCALAPPDATA\AMD\DxCache",
        "$env:LOCALAPPDATA\AMD\DxcCache",
        "$env:LOCALAPPDATA\AMD\GLCache",
        "$env:USERPROFILE\AppData\LocalLow\NVIDIA\PerDriverVersion\DXCache"
    ) | Where-Object { Test-Path $_ }
    $totalShaderMB = 0
    foreach ($sp in ($shaderPaths + $steamShader)) { $totalShaderMB += (Get-FolderSizeMB $sp) }
    $Ctx.ShaderPaths = @($shaderPaths)
    $Ctx.SteamShader = @($steamShader)
    $Ctx.ShaderMB     = $totalShaderMB
    Info "Cache de shaders (DirectX/driver/Steam): ~$totalShaderMB MB"

    # userdata 730 (configs do CS2)
    $userdata730 = @()
    if ($steamPath -and (Test-Path (Join-Path $steamPath 'userdata'))) {
        Get-ChildItem (Join-Path $steamPath 'userdata') -Directory -ErrorAction SilentlyContinue | ForEach-Object {
            $p = Join-Path $_.FullName '730'
            if (Test-Path $p) { $userdata730 += $p }
        }
    }
    $Ctx.Userdata730 = $userdata730
    Info "Pastas de config do CS2 (userdata\...\730): $($userdata730.Count)"

    # Cache da Steam (download/depot)
    $steamCachePaths = @()
    if ($steamPath) {
        foreach ($sub in @('depotcache', 'appcache\httpcache')) {
            $cp = Join-Path $steamPath $sub
            if (Test-Path $cp) { $steamCachePaths += $cp }
        }
    }
    $Ctx.SteamCachePaths = $steamCachePaths

    Section 'MPO / DWM'
    $dwm = 'HKLM:\SOFTWARE\Microsoft\Windows\Dwm'
    $Ctx.DwmPath = $dwm
    $overlayTest = Get-Reg $dwm 'OverlayTestMode'
    $Ctx.OverlayTest = $overlayTest
    if ($null -ne $overlayTest) { Bad "OverlayTestMode existe em $dwm (valor $overlayTest) -> MPO desativado." }
    else { Ok 'OverlayTestMode nao existe (MPO habilitado).' }

    Section 'Timer / HPET (BCDEdit)'
    $bcdText = (& bcdedit /enum '{current}' 2>&1) | Out-String
    $bcdKeys = @{}
    foreach ($k in @('useplatformclock', 'useplatformtick', 'disabledynamictick', 'tscsyncpolicy')) {
        if ($bcdText -match "(?im)^\s*$k\s+(\S+)") { $bcdKeys[$k] = $Matches[1] }
    }
    $Ctx.BcdKeys = $bcdKeys
    if ($bcdKeys.Count -eq 0) { Ok 'Nenhuma alteracao de clock/tick no BCD (padrao do sistema = TSC).' }
    foreach ($k in $bcdKeys.Keys) {
        if ($k -eq 'useplatformclock' -and $bcdKeys[$k] -match '^(no|false)$') { Ok "useplatformclock = $($bcdKeys[$k]) (HPET nao forcado)." }
        elseif ($k -eq 'useplatformclock') { Bad "useplatformclock = $($bcdKeys[$k]) -> HPET FORCADO." }
        else { Bad "$k = $($bcdKeys[$k]) -> ajuste de timer fora do padrao." }
    }

    Section 'Energia / Inicializacao rapida / Game Bar'
    $activeScheme = (& powercfg /getactivescheme) -join ' '
    Info "Plano ativo: $activeScheme"
    $activeGuid = ([regex]::Match($activeScheme, '[0-9a-fA-F]{8}-([0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12}')).Value
    $Ctx.ActiveGuid = $activeGuid

    # CORRIGIDO: comparar so o GUID nunca casa quando o plano e uma COPIA
    # (o caso do "CS2 Desempenho Maximo"). Agora olha o nome e o estado minimo
    # do processador em CA (100% = sem economia).
    $perfPorNome = [bool]($activeScheme -match 'Alto desempenho|High performance|Desempenho m.ximo|Ultimate')
    $minAC = -1
    if ($activeGuid) {
        try {
            $q = (& powercfg /query $activeGuid SUB_PROCESSOR PROCTHROTTLEMIN 2>&1) -join "`n"
            $ms = [regex]::Matches($q, '0x([0-9a-fA-F]{8})')
            if ($ms.Count -ge 2) { $minAC = [Convert]::ToInt32($ms[$ms.Count - 2].Groups[1].Value, 16) }
        } catch {}
    }
    $onPerf = $perfPorNome -or ($minAC -eq 100)
    $Ctx.OnPerf = $onPerf
    $Ctx.MinAC  = $minAC
    if ($onPerf) {
        Ok "Plano de energia de alto desempenho$(if ($minAC -ge 0) { " (estado minimo do processador em CA: $minAC%)" })."
    } else {
        Warn "Plano de energia nao e Alto/Desempenho maximo$(if ($minAC -ge 0) { " (estado minimo do processador em CA: $minAC%)" })."
    }

    $fastBoot = Get-Reg 'HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\Power' 'HiberbootEnabled'
    $Ctx.FastBoot = $fastBoot
    if ($fastBoot -eq 1) { Warn 'Inicializacao rapida ATIVADA (pode causar problemas).' } else { Ok 'Inicializacao rapida desativada.' }

    $gameDvr1 = Get-Reg 'HKCU:\Software\Microsoft\Windows\CurrentVersion\GameDVR' 'AppCaptureEnabled'
    $gameDvr2 = Get-Reg 'HKCU:\System\GameConfigStore' 'GameDVR_Enabled'
    $Ctx.GameDvr1 = $gameDvr1
    $Ctx.GameDvr2 = $gameDvr2
    if ($gameDvr1 -eq 1 -or $gameDvr2 -eq 1) { Warn 'Capturas do Xbox Game Bar / GameDVR ativadas.' } else { Ok 'Capturas do Game Bar desativadas.' }

    $gameMode = Get-Reg 'HKCU:\Software\Microsoft\GameBar' 'AutoGameModeEnabled'
    $Ctx.GameMode = $gameMode
    if ($gameMode -eq 0) { Warn 'Modo de Jogo DESATIVADO (deveria ficar ativo).' } else { Ok 'Modo de Jogo ativo/padrao.' }

    $hags = Get-Reg 'HKLM:\SYSTEM\CurrentControlSet\Control\GraphicsDrivers' 'HwSchMode'
    $Ctx.Hags = $hags
    if ($isWin11) {
        if ($hags -eq 2) { Ok 'HAGS ativado (correto para Windows 11).' } else { Warn 'HAGS nao esta ativado. No Windows 11 o recomendado e ATIVADO.' }
    } else {
        if ($hags -eq 2) { Warn 'HAGS ativado no Windows 10. O recomendado e DESATIVADO.' } else { Ok 'HAGS desativado (correto para Windows 10).' }
    }

    $bgPolicy = Get-Reg 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\AppPrivacy' 'LetAppsRunInBackground'
    $bgUser   = Get-Reg 'HKCU:\Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications' 'GlobalUserDisabled'
    if ($isWin11) { $bgDisabled = ($bgPolicy -eq 2) } else { $bgDisabled = (($bgUser -eq 1) -or ($bgPolicy -eq 2)) }
    $Ctx.BgDisabled = $bgDisabled
    if ($bgDisabled) { Ok 'Apps do Windows em segundo plano desativados.' } else { Warn 'Apps do Windows podem rodar em segundo plano.' }

    $mouseSpeed = Get-Reg 'HKCU:\Control Panel\Mouse' 'MouseSpeed'
    $Ctx.MouseSpeed = $mouseSpeed
    if ($mouseSpeed -ne '0') { Warn "Aceleracao do mouse ('Aprimorar precisao do ponteiro') ATIVADA." } else { Ok 'Aceleracao do mouse desativada.' }
    Todo 'Mouse: polling rate de 1000-4000 Hz (2K/4K costuma ser o ideal; acima disso so gera carga extra).'

    $responsiveness = Get-Reg 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile' 'SystemResponsiveness'
    $netThrottle    = Get-Reg 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile' 'NetworkThrottlingIndex'
    $Ctx.Responsiveness = $responsiveness
    $Ctx.NetThrottle    = $netThrottle
    if ($null -eq $responsiveness -or $null -eq $netThrottle) {
        Info 'MMCSS (prioridade multimidia/rede): padrao do Windows.'
    } elseif ($responsiveness -eq 0 -and $netThrottle -eq -1) {
        Ok 'MMCSS ja prioriza jogos (SystemResponsiveness=0, NetworkThrottlingIndex=-1).'
    } else {
        Info "MMCSS ajustado parcialmente (SystemResponsiveness=$responsiveness, NetworkThrottlingIndex=$netThrottle)."
    }

    if ($cs2Exe) {
        $gp = Get-Reg 'HKCU:\Software\Microsoft\DirectX\UserGpuPreferences' $cs2Exe
        $Ctx.GpuPref = $gp
        if ($gp -match 'GpuPreference=2') { Ok "cs2.exe ja esta com preferencia 'Alto desempenho'." }
        else { Warn "cs2.exe sem preferencia de GPU 'Alto desempenho' nas Configuracoes Graficas." }
    }

    Section 'Telemetria do Windows'
    $Ctx.TStart  = Get-Reg 'HKLM:\SYSTEM\CurrentControlSet\Services\DiagTrack' 'Start'
    $Ctx.TWap    = Get-Reg 'HKLM:\SYSTEM\CurrentControlSet\Services\dmwappushservice' 'Start'
    $Ctx.TLevel  = Get-Reg 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\DataCollection' 'AllowTelemetry'
    $Ctx.TCeip   = Get-Reg 'HKLM:\SOFTWARE\Policies\Microsoft\SQMClient\Windows' 'CEIPEnable'
    $Ctx.TAdId   = Get-Reg 'HKCU:\Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo' 'Enabled'
    $Ctx.TFeed   = Get-Reg 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\System' 'EnableActivityFeed'
    $Ctx.TWer    = Get-Reg 'HKLM:\SOFTWARE\Microsoft\Windows\Windows Error Reporting' 'Disabled'
    $Ctx.TVoz    = Get-Reg 'HKCU:\Software\Microsoft\Speech_OneCore\Settings\OnlineSpeechPrivacy' 'HasAccepted'
    $Ctx.TCompat = 'Ausente'
    try {
        $tk = Get-ScheduledTask -TaskPath '\Microsoft\Windows\Application Experience\' -TaskName 'Microsoft Compatibility Appraiser' -ErrorAction Stop
        if ($tk) { $Ctx.TCompat = [string]$tk.State }
    } catch {}

    if ($Ctx.TStart -eq 4) { Ok 'Servico de telemetria (DiagTrack) desativado.' }
    else { Warn 'Servico de telemetria (DiagTrack) LIGADO (envia dados de uso para a Microsoft).' }

    if ($Ctx.TLevel -eq 0) { Ok 'Dados de diagnostico no minimo (AllowTelemetry = 0).' }
    elseif ($null -eq $Ctx.TLevel) { Warn 'Dados de diagnostico no padrao do Windows (envia o pacote completo).' }
    else { Warn "Dados de diagnostico em nivel $($Ctx.TLevel) (o minimo e 0)." }

    if ($Ctx.TAdId -eq 0) { Ok 'ID de publicidade desligado.' }
    else { Warn 'ID de publicidade ligado (usado para anuncio/rastreio entre apps).' }

    if ($Ctx.TWer -eq 1) { Ok 'Envio de relatorios de erro (WER) desligado.' }
    else { Info 'Relatorios de erro: o Windows ainda envia pacotes de falha para a Microsoft.' }

    if ($Ctx.TVoz -eq 0) { Ok 'Dados de voz (reconhecimento online) desligados.' }
    else { Info 'Reconhecimento de voz online: o Windows pode enviar audio para a Microsoft.' }

    if ($Ctx.TCompat -ne 'Disabled' -and $Ctx.TCompat -ne 'Ausente') {
        Warn "Tarefa de telemetria ativa: 'Microsoft Compatibility Appraiser' ($($Ctx.TCompat))."
    }

    Section 'Seguranca (VBS / HVCI) - so informativo'
    try {
        $dg = Get-CimInstance -Namespace 'root\Microsoft\Windows\DeviceGuard' -ClassName Win32_DeviceGuard -ErrorAction Stop
        if (@($dg.SecurityServicesRunning) -contains 2) {
            Info 'Integridade de memoria (HVCI/VBS) ATIVA: custa alguns % de FPS. Desligar mexe na Seguranca do Windows (Isolamento de nucleo) e reduz protecao - decisao sua, o script nao altera.'
            Todo 'Seguranca do Windows > Seguranca do dispositivo > Isolamento de nucleo > Integridade de memoria: desligar devolve alguns % de FPS, mas tira uma camada de protecao do kernel. Decida.'
        } else {
            Ok 'Integridade de memoria (HVCI) desligada.'
        }
    } catch {
        Info 'Nao foi possivel ler o estado do VBS/HVCI.'
    }

    Section 'Processos e programas de inicializacao'
    $overlayProcs = 'Discord', 'GameOverlayUI', 'RTSS', 'MSIAfterburner', 'Overwolf', 'OverwolfBrowser', 'medal', 'obs64', 'nvsphelper64', 'GeForceExperience', 'chrome', 'msedge', 'RadeonSoftware', 'iCUE', 'SignalRgb', 'LGHUB', 'Razer Synapse 3', 'RazerCentralService'
    $running = @(Get-Process -ErrorAction SilentlyContinue | Where-Object { $overlayProcs -contains $_.ProcessName } | Select-Object -ExpandProperty ProcessName -Unique)
    if ($running) { Warn ('Rodando agora (podem causar overlay/conflito): ' + ($running -join ', ')) }
    else { Ok 'Nenhum overlay/app conhecido rodando.' }

    $suspect = 'RGB|iCUE|Razer|Synapse|Armoury|Aura|SignalRGB|G HUB|LGHUB|MSI Center|Dragon Center|Afterburner|HWiNFO|Overwolf|Telemetr|Spotify|Teams|Adobe|Updater'
    $startup = Get-CimInstance Win32_StartupCommand -ErrorAction SilentlyContinue
    $flagged = @($startup | Where-Object { $_.Name -match $suspect -or $_.Command -match $suspect })
    Info "Programas de inicializacao: $(@($startup).Count)"
    if ($flagged.Count -gt 0) {
        Warn "Suspeitos na inicializacao (RGB/telemetria/updaters): $(@($flagged | Select-Object -ExpandProperty Name -Unique) -join ', ')"
        Todo 'Gerenciador de Tarefas > Inicializar: desative RGB, telemetria, updaters e o que nao for essencial.'
    }

    Todo 'Steam > Configuracoes > Em jogo: desative o overlay (ao menos para o CS2) e as gravacoes.'
    Todo 'Steam > CS2 > Propriedades > Opcoes de inicializacao: -novid -high -nojoy. Teste -vulkan em algumas partidas: as vezes ganha FPS, as vezes piora.'
    Todo 'Discord: Sobreposicao do jogo = OFF; Aparencia > Aceleracao de hardware = OFF; feche para a bandeja ao jogar.'
    Todo 'Chrome/Edge: desative "Usar aceleracao grafica" e "Continuar executando apps em segundo plano" se deixar aberto durante o jogo.'
    Todo 'NVIDIA App / Adrenalin: desative o overlay do driver.'
    Todo 'Medir resultado: mapa "CS2 FPS Benchmark" da Oficina, anote FPS medio e 1% low (o 1% low importa mais que o medio).'
}
# ============================================================================
# Confirmacao + lista de correcoes
# ============================================================================
function Ask([string]$q, [string]$level = 'safe') {
    if ($script:ReportOnly) { return $false }
    if ($script:Auto) { return ($level -eq 'safe') }

    if ($script:UI) {
        # OBS: MessageBoxDefaultButton nao aceita "Yes"/"No"; com YesNo os nomes
        # dos botoes sao Button1 (= Sim) e Button2 (= Nao).
        $icone  = if ($level -eq 'destructive') { [System.Windows.Forms.MessageBoxIcon]::Warning } else { [System.Windows.Forms.MessageBoxIcon]::Question }
        $padrao = if ($level -eq 'destructive') { [System.Windows.Forms.MessageBoxDefaultButton]::Button2 } else { [System.Windows.Forms.MessageBoxDefaultButton]::Button1 }
        $r = [System.Windows.Forms.MessageBox]::Show($q, 'Confirmar', [System.Windows.Forms.MessageBoxButtons]::YesNo, $icone, $padrao)
        return ($r -eq [System.Windows.Forms.DialogResult]::Yes)
    }

    $sufixo = if ($level -eq 'destructive') { '[s/N]' } else { '[S/n]' }
    $r = Read-Host ("  >> " + $q + " " + $sufixo)
    if ([string]::IsNullOrWhiteSpace($r)) { return ($level -ne 'destructive') }
    return ($r.Trim().ToLower() -in @('s', 'sim', 'y', 'yes'))
}

function New-Fix($Id, $Titulo, $Situacao, $Detalhe, $Nivel, $Reinicia, $Acao) {
    [pscustomobject]@{
        Id       = $Id
        Titulo   = $Titulo
        Situacao = $Situacao
        Detalhe  = $Detalhe
        Nivel    = $Nivel
        Reinicia = $Reinicia
        Acao     = $Acao
    }
}

# O que o botao "Marcar recomendadas" liga: tudo que e seguro + os opcionais
# leves (manutencao rapida). Reparo demorado (DISM/SFC/CHKDSK) e destrutivo
# ficam sempre para o usuario marcar na mao.
$script:IdsRecomendados = @('mmcss', 'mouse', 'trim', 'temp', 'syscache', 'shaders')

function Test-Recomendada($fix) {
    if (-not $fix) { return $false }
    return (($fix.Nivel -eq 'safe') -or ($script:IdsRecomendados -contains $fix.Id))
}

function New-PerfPlan {
    # Reaproveita um "CS2 Desempenho Maximo" que ja exista (nao cria um novo a cada execucao)
    $lista = (& powercfg /list) -join "`n"
    $m = [regex]::Match($lista, '(?im)^[^\r\n]*?([0-9a-fA-F]{8}-([0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12})[^\r\n]*CS2 Desempenho [Mm].ximo')
    $novo = $null
    if ($m.Success) { $novo = $m.Groups[1].Value }

    if (-not $novo) {
        foreach ($tpl in @('e9a42b02-d5df-448d-aa00-03f14749eb61', '8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c')) {
            $out = (& powercfg /duplicatescheme $tpl 2>&1) -join "`n"
            $novo = ([regex]::Match($out, '[0-9a-fA-F]{8}-([0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12}')).Value
            if ($novo) { break }
        }
    }
    if (-not $novo) { throw 'Nao foi possivel criar/achar um plano de energia de desempenho neste sistema.' }

    & powercfg /changename $novo 'CS2 Desempenho Maximo' | Out-Null
    & powercfg /setacvalueindex $novo SUB_PCIEXPRESS ASPM 0 | Out-Null
    & powercfg /setacvalueindex $novo SUB_PROCESSOR PROCTHROTTLEMIN 100 | Out-Null
    & powercfg /setactive $novo | Out-Null
    return $novo
}

function Build-Fixes {
    $Ctx = $script:Ctx
    $f = @()

    # ---------------- MPO / BCD ----------------
    if ($null -ne $Ctx.OverlayTest) {
        $f += New-Fix 'mpo' 'Reabilitar MPO (remover OverlayTestMode)' `
            "OverlayTestMode = $($Ctx.OverlayTest)" `
            'Remover OverlayTestMode do DWM para reabilitar o MPO (multi-plane overlay)?' `
            'safe' $true { Remove-ItemProperty -Path $script:Ctx.DwmPath -Name 'OverlayTestMode' -Force }
    }

    foreach ($k in @($Ctx.BcdKeys.Keys)) {
        $k0 = $k
        $v0 = $Ctx.BcdKeys[$k]
        if ($k0 -eq 'useplatformclock') {
            if ($v0 -notmatch '^(no|false)$') {
                $acao = { & bcdedit /set useplatformclock no | Out-Null; if ($LASTEXITCODE -ne 0) { throw "bcdedit retornou $LASTEXITCODE" } }
                $f += New-Fix 'bcd-hpet' 'BCD: useplatformclock = no' "useplatformclock = $v0" `
                    'Definir useplatformclock = no (usar TSC em vez de forcar o HPET)?' `
                    'safe' $true $acao
            }
        } else {
            $kfix = $k0
            $acao = { & bcdedit /deletevalue $kfix | Out-Null; if ($LASTEXITCODE -ne 0) { throw "bcdedit retornou $LASTEXITCODE" } }.GetNewClosure()
            $f += New-Fix ('bcd-' + $kfix) ("BCD: remover " + $kfix) ("$kfix = $v0") `
                ("Remover '" + $kfix + "' do BCD (voltar ao padrao)?") `
                'safe' $true $acao
        }
    }

    # ---------------- Energia ----------------
    if (-not $Ctx.OnPerf) {
        $f += New-Fix 'power-plan' 'Plano de energia: CS2 Desempenho Maximo' 'plano atual sem desempenho maximo' `
            "Criar e ativar o plano 'CS2 Desempenho Maximo' (processador em 100%, PCIe sem economia de energia)?`r`n`r`nO plano antigo continua salvo e volta no desfazer." `
            'safe' $false { [void](New-PerfPlan) }
    }

    if ($Ctx.FastBoot -eq 1) {
        $f += New-Fix 'fastboot' 'Desativar Inicializacao Rapida' 'ativada' `
            'Desativar a Inicializacao Rapida (hibernacao hibrida)? Ela deixa o PC dias sem reiniciar de verdade.' `
            'safe' $false { Set-Reg 'HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\Power' 'HiberbootEnabled' 0 }
    }

    if ($Ctx.GameDvr1 -eq 1 -or $Ctx.GameDvr2 -eq 1) {
        $f += New-Fix 'gamedvr' 'Desativar capturas do Game Bar/GameDVR' 'capturas ativadas' `
            'Desativar as capturas em segundo plano do Xbox Game Bar (o Game Bar em si continua)?' `
            'safe' $false {
                Set-Reg 'HKCU:\Software\Microsoft\Windows\CurrentVersion\GameDVR' 'AppCaptureEnabled' 0
                Set-Reg 'HKCU:\System\GameConfigStore' 'GameDVR_Enabled' 0
            }
    }

    if ($Ctx.GameMode -eq 0) {
        $f += New-Fix 'gamemode' 'Reativar Modo de Jogo' 'desativado' `
            'Reativar o Modo de Jogo do Windows?' `
            'safe' $false {
                Set-Reg 'HKCU:\Software\Microsoft\GameBar' 'AutoGameModeEnabled' 1
                Set-Reg 'HKCU:\Software\Microsoft\GameBar' 'AllowAutoGameMode' 1
            }
    }

    if ($Ctx.IsWin11 -and $Ctx.Hags -ne 2) {
        $f += New-Fix 'hags-on' 'Ativar HAGS (Windows 11)' "HwSchMode = $($Ctx.Hags)" `
            'Ativar o agendamento de GPU acelerado por hardware (HAGS)? Requer GPU/driver compativeis (NVIDIA serie 1000+).' `
            'safe' $true { Set-Reg 'HKLM:\SYSTEM\CurrentControlSet\Control\GraphicsDrivers' 'HwSchMode' 2 }
    } elseif ((-not $Ctx.IsWin11) -and $Ctx.Hags -eq 2) {
        $f += New-Fix 'hags-off' 'Desativar HAGS (Windows 10)' 'HwSchMode = 2' `
            'Desativar o agendamento de GPU acelerado por hardware (HAGS) no Windows 10?' `
            'safe' $true { Set-Reg 'HKLM:\SYSTEM\CurrentControlSet\Control\GraphicsDrivers' 'HwSchMode' 1 }
    }

    # ---------------- Windows opcional ----------------
    if (-not $Ctx.BgDisabled) {
        $f += New-Fix 'bgapps' 'Desativar apps em segundo plano' 'permitido' `
            'Impedir que apps do Windows rodem em segundo plano? Pode atrasar notificacoes da Microsoft Store.' `
            'optional' $false {
                if ($script:Ctx.IsWin11) { Set-Reg 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\AppPrivacy' 'LetAppsRunInBackground' 2 }
                else { Set-Reg 'HKCU:\Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications' 'GlobalUserDisabled' 1 }
            }
    }

    if (-not ($Ctx.Responsiveness -eq 0 -and $Ctx.NetThrottle -eq -1)) {
        $nt = $Ctx.NetThrottle
        if ($nt -eq 4294967295) { $nt = '-1 (sem limite)' }
        $f += New-Fix 'mmcss' 'Priorizar jogos no MMCSS (midia/rede)' "SystemResponsiveness=$($Ctx.Responsiveness), NetworkThrottlingIndex=$nt" `
            'Ajustar o agendador multimidia do Windows para jogos (SystemResponsiveness = 0 e sem limite de rede)?' `
            'optional' $false {
                Set-Reg 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile' 'SystemResponsiveness' 0
                Set-Reg 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile' 'NetworkThrottlingIndex' -1
            }
    }

    # ---------------- cs2.exe ----------------
    if ($Ctx.Cs2Exe) {
        if ($Ctx.FsoDisabled) {
            $f += New-Fix 'cs2-fso' 'Reativar otimizacoes de tela inteira do cs2.exe' 'flag marcada' `
                'Remover a flag "Desativar otimizacoes de tela inteira" do cs2.exe? (com ela o CS2 perde o DirectFlip)' `
                'safe' $false {
                    foreach ($lp in $script:Ctx.LayerPaths) {
                        $v = Get-Reg $lp $script:Ctx.Cs2Exe
                        if ($v -and $v -match 'DISABLEDXMAXIMIZEDWINDOWEDMODE') {
                            $tokens = @($v -split '\s+' | Where-Object { $_ -and $_ -ne 'DISABLEDXMAXIMIZEDWINDOWEDMODE' })
                            if ($tokens.Count -eq 0 -or ($tokens.Count -eq 1 -and $tokens[0] -eq '~')) {
                                Remove-ItemProperty -Path $lp -Name $script:Ctx.Cs2Exe -Force
                            } else {
                                Set-ItemProperty -Path $lp -Name $script:Ctx.Cs2Exe -Value ($tokens -join ' ')
                            }
                        }
                    }
                }
        }

        if ($Ctx.GpuPref -notmatch 'GpuPreference=2') {
            $f += New-Fix 'cs2-gpu' 'GPU Alto Desempenho para cs2.exe' 'sem preferencia' `
                'Definir cs2.exe como GPU "Alto desempenho" (otimizacao de modo janela ON, VRR do Windows OFF)?' `
                'safe' $false {
                    Set-Reg 'HKCU:\Software\Microsoft\DirectX\UserGpuPreferences' $script:Ctx.Cs2Exe 'GpuPreference=2;SwapEffectUpgradeEnable=1;VRROptimizeEnable=0;' 'String'
                }
        }
    }

    # ---------------- Limpeza / manutencao ----------------
    if ($Ctx.MouseSpeed -ne '0') {
        $f += New-Fix 'mouse' 'Desativar aceleracao do mouse' "MouseSpeed = $($Ctx.MouseSpeed)" `
            'Desativar "Aprimorar precisao do ponteiro" (aceleracao do mouse)?' `
            'optional' $true {
                Set-Reg 'HKCU:\Control Panel\Mouse' 'MouseSpeed' '0' 'String'
                Set-Reg 'HKCU:\Control Panel\Mouse' 'MouseThreshold1' '0' 'String'
                Set-Reg 'HKCU:\Control Panel\Mouse' 'MouseThreshold2' '0' 'String'
            }
    }

    if (@($Ctx.SsdLetters).Count -gt 0) {
        $f += New-Fix 'trim' ("ReTrim nos SSDs (" + ($Ctx.SsdLetters -join ', ') + ')') 'manutencao' `
            'Rodar TRIM/otimizacao nos SSDs? E rapido e seguro.' `
            'optional' $false {
                foreach ($l in $script:Ctx.SsdLetters) { Optimize-Volume -DriveLetter $l -ReTrim -ErrorAction Stop | Out-Null }
            }
    }

    if ($Ctx.ShaderMB -gt 0) {
        $f += New-Fix 'shaders' ("Limpar cache de shaders (~$($Ctx.ShaderMB) MB)") "~$($Ctx.ShaderMB) MB" `
            "Limpar caches de shader (DirectX, driver, Steam 730 -> 730.old)?`r`nOs jogos recompilam na proxima abertura (a primeira partida trava um pouco)." `
            'optional' $false {
                Stop-SteamCS2
                foreach ($p in $script:Ctx.ShaderPaths) { Get-ChildItem -LiteralPath $p -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue }
                foreach ($sp in $script:Ctx.SteamShader) {
                    $novo = "$sp.old"
                    if (Test-Path $novo) { Remove-Item $novo -Recurse -Force -ErrorAction SilentlyContinue }
                    Rename-Item -Path $sp -NewName (Split-Path $novo -Leaf) -ErrorAction SilentlyContinue
                }
            }
    }

    $tempMB = Get-FolderSizeMB $env:TEMP
    $Ctx.TempMB = $tempMB
    if ($tempMB -gt 200) {
        $f += New-Fix 'temp' ("Limpar %TEMP% (~$tempMB MB)") "~$tempMB MB" `
            "Limpar a pasta TEMP do usuario (~$tempMB MB)?" `
            'optional' $false { Get-ChildItem -LiteralPath $env:TEMP -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue }
    }

    $f += New-Fix 'syscache' 'Limpar cache do sistema (DNS/Temp/Prefetch/Steam)' 'manutencao' `
        'Limpar DNS, Temp do Windows, Prefetch e cache de download da Steam? Tudo e recriado sozinho.' `
        'optional' $false {
            Stop-SteamCS2
            try { Clear-DnsClientCache } catch { & ipconfig /flushdns | Out-Null }
            foreach ($p in @("$env:SystemRoot\Temp", "$env:SystemRoot\Prefetch") + $script:Ctx.SteamCachePaths) {
                if (Test-Path $p) { Get-ChildItem -LiteralPath $p -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue }
            }
        }

    # ---------------- Telemetria (tudo reversivel pelo backup) ----------------
    if ($Ctx.TStart -ne 4 -or $Ctx.TWap -ne 4) {
        $sit = @()
        if ($Ctx.TStart -ne 4) { $sit += 'DiagTrack ligado' }
        if ($Ctx.TWap -ne 4) { $sit += 'WAP Push ligado' }
        $f += New-Fix 'telemetry-servico' 'Desativar servico de telemetria (DiagTrack)' ($sit -join ', ') `
            ("Parar e desativar o DiagTrack (Experiencias do Usuario Conectado) e o WAP Push?`r`n" +
             "E daqui que sai a maior parte dos dados de uso enviados para a Microsoft.`r`n" +
             "Nao mexe em Windows Update, Defender, Store, Xbox nem em login.") `
            'safe' $false {
                foreach ($n in @('DiagTrack', 'dmwappushservice')) {
                    try { Stop-Service -Name $n -Force -ErrorAction SilentlyContinue } catch {}
                    try { Set-Service -Name $n -StartupType Disabled -ErrorAction Stop } catch {
                        Set-Reg "HKLM:\SYSTEM\CurrentControlSet\Services\$n" 'Start' 4
                    }
                }
            }
    }

    if ($Ctx.TLevel -ne 0 -or $Ctx.TCeip -ne 0) {
        if ($Ctx.TLevel -ne 0) { $atual = if ($null -eq $Ctx.TLevel) { 'padrao do Windows' } else { "nivel $($Ctx.TLevel)" } }
        else { $atual = 'CEIP (experiencia do usuario) ligado' }
        $f += New-Fix 'telemetry-dados' 'Dados de diagnostico no minimo' $atual `
            ("Deixar o envio de diagnostico no minimo (AllowTelemetry = 0) e desligar o CEIP?`r`n" +
             "No Windows Home/Pro o sistema ainda manda o pacote 'obrigatorio', mas para de mandar o opcional " +
             "(apps abertos, uso do navegador, tempo de tela). Windows Update continua normal.") `
            'safe' $false {
                Set-Reg 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\DataCollection' 'AllowTelemetry' 0
                Set-Reg 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\DataCollection' 'LimitDiagnosticLogCollection' 1
                Set-Reg 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\DataCollection' 'LimitDumpCollection' 1
                Set-Reg 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection' 'AllowTelemetry' 0
                Set-Reg 'HKLM:\SOFTWARE\Policies\Microsoft\SQMClient\Windows' 'CEIPEnable' 0
            }
    }

    if ($Ctx.TAdId -ne 0 -or $Ctx.TFeed -ne 0) {
        $f += New-Fix 'telemetry-rastreio' 'Desativar rastreio de uso (ID de publicidade)' 'rastreio ligado' `
            ("Desligar ID de publicidade, experiencias personalizadas, historico de atividades e a coleta de escrita?`r`n" +
             "Sao os rastreadores do dia a dia (nao o diagnostico do sistema). Nao afeta login, Store nem OneDrive.") `
            'safe' $false {
                Set-Reg 'HKCU:\Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo' 'Enabled' 0
                Set-Reg 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Privacy' 'TailoredExperiencesWithDiagnosticDataEnabled' 0
                Set-Reg 'HKCU:\Software\Microsoft\Siuf\Rules' 'NumberOfSIUFInPeriod' 0
                Set-Reg 'HKCU:\Software\Microsoft\Siuf\Rules' 'PeriodInNanoSeconds' 0
                Set-Reg 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\System' 'EnableActivityFeed' 0
                Set-Reg 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\System' 'PublishUserActivities' 0
                Set-Reg 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\System' 'UploadUserActivities' 0
                Set-Reg 'HKCU:\Software\Microsoft\Input\TIPC' 'Enabled' 0
                Set-Reg 'HKCU:\Software\Microsoft\InputPersonalization' 'RestrictImplicitTextCollection' 1
                Set-Reg 'HKCU:\Software\Microsoft\InputPersonalization' 'RestrictImplicitInkCollection' 1
            }
    }

    if ($Ctx.TVoz -ne 0) {
        $f += New-Fix 'telemetry-voz' 'Desligar envio de voz (fala online)' 'reconhecimento online ligado' `
            ("Desligar o envio de audio para reconhecimento de fala na nuvem e a personalizacao de entrada?`r`n" +
             "O microfone do jogo (Steam/Discord/CS2) nao e afetado - isso e so o 'ditado'/'falar para o Windows'.") `
            'safe' $false {
                Set-Reg 'HKCU:\Software\Microsoft\Speech_OneCore\Settings\OnlineSpeechPrivacy' 'HasAccepted' 0
                Set-Reg 'HKLM:\SOFTWARE\Policies\Microsoft\InputPersonalization' 'AllowInputPersonalization' 0
            }
    }

    if ($Ctx.TWer -ne 1 -or ($Ctx.TCompat -and $Ctx.TCompat -ne 'Disabled' -and $Ctx.TCompat -ne 'Ausente')) {
        if ($Ctx.TWer -ne 1) { $sitErros = 'envio de relatorios ligado' }
        else { $sitErros = 'tarefa de compatibilidade ativa' }
        $f += New-Fix 'telemetry-erros' 'Parar envio de relatorios de erro (WER)' $sitErros `
            ("Desligar o envio de relatorios de erro (WER) para a Microsoft e as tarefas 'Experiencia de Aplicativos'?`r`n" +
             "O Windows continua mostrando o erro na tela; so para de empacotar e enviar. Jogos e programas nao sao afetados.") `
            'safe' $false {
                Set-Reg 'HKLM:\SOFTWARE\Microsoft\Windows\Windows Error Reporting' 'Disabled' 1
                Set-Reg 'HKLM:\SOFTWARE\Microsoft\Windows\Windows Error Reporting' 'DontSendAdditionalData' 1
                foreach ($tn in @('Microsoft Compatibility Appraiser', 'ProgramDataUpdater')) {
                    try { Disable-ScheduledTask -TaskPath '\Microsoft\Windows\Application Experience\' -TaskName $tn -ErrorAction SilentlyContinue | Out-Null } catch {}
                }
            }
    }

    # ---------------- Reparo ----------------
    $f += New-Fix 'dism-restore' 'DISM RestoreHealth (reparar imagem)' 'demorado' `
        'Reparar a imagem do Windows (DISM /RestoreHealth)? Leva de 5 a 20 min e usa o Windows Update.' `
        'optional' $false {
            $rc = Invoke-Exe 'DISM.exe' @('/Online', '/Cleanup-Image', '/RestoreHealth') 'DISM RestoreHealth'
            if ($rc -ne 0) { throw "DISM retornou codigo $rc" }
        }

    $f += New-Fix 'sfc' 'SFC /scannow (arquivos do sistema)' 'demorado' `
        'Verificar e reparar arquivos do sistema (sfc /scannow)? Leva de 5 a 15 min.' `
        'optional' $false {
            $rc = Invoke-Exe 'sfc.exe' @('/scannow') 'SFC /scannow'
            if ($rc -ne 0) { throw "SFC retornou codigo $rc" }
        }

    $f += New-Fix 'dism-cleanup' 'DISM StartComponentCleanup' 'demorado' `
        'Limpar componentes antigos do Windows (libera espaco em WinSxS)?' `
        'optional' $false {
            $rc = Invoke-Exe 'DISM.exe' @('/Online', '/Cleanup-Image', '/StartComponentCleanup') 'DISM Cleanup'
            if ($rc -ne 0) { throw "DISM retornou codigo $rc" }
        }

    $f += New-Fix 'chkdsk' 'CHKDSK /scan (erros no disco)' 'demorado' `
        "Verificar erros no disco do Windows (chkdsk $env:SystemDrive /scan, so leitura, sem reiniciar)?" `
        'optional' $false {
            $code = Invoke-Exe 'chkdsk.exe' @($env:SystemDrive, '/scan') 'CHKDSK /scan'
            if ($code -ge 2) { throw "CHKDSK retornou codigo $code (2/3 = problemas no disco ou acesso negado). Se houver erro, rode 'chkdsk /f /r' e reinicie." }
        }

    $f += New-Fix 'net-reset' 'Reset de rede (Winsock/TCP-IP)' 'so se tiver problema' `
        'Resetar Winsock e pilha TCP/IP? So se tiver problema de rede; pode apagar IP fixo/DNS manual e exige reiniciar.' `
        'destructive' $true {
            & netsh winsock reset | Out-Null
            & netsh int ip reset | Out-Null
            & ipconfig /flushdns | Out-Null
        }

    # ---------------- Destrutivo ----------------
    if (@($Ctx.Userdata730).Count -gt 0) {
        $f += New-Fix 'cs2-reset' 'Resetar configs do CS2 (userdata 730)' "$(@($Ctx.Userdata730).Count) pasta(s)" `
            "Renomear userdata\...\730 para 730.old?`r`nAPAGA binds, crosshair e configs de video. Desative a Steam Cloud do CS2 antes de abrir o jogo." `
            'destructive' $false {
                if (Get-Process steam, cs2 -ErrorAction SilentlyContinue) { throw 'Feche a Steam e o CS2 antes.' }
                foreach ($p in $script:Ctx.Userdata730) {
                    $novo = "$p.old"
                    if (Test-Path $novo) { Remove-Item $novo -Recurse -Force }
                    Rename-Item -Path $p -NewName (Split-Path $novo -Leaf)
                }
            }
    }

    $script:Fixes = $f
    return $f
}

# ============================================================================
# Backup / aplicar / desfazer
# ============================================================================
function New-Backup {
    if ($script:ReportOnly) { return }
    if ($script:BackupFeito) { return }
    Note ('Criando backup em: ' + $script:BackupDir) 'titulo'
    New-Item -ItemType Directory -Path $script:BackupDir -Force | Out-Null

    try {
        & bcdedit /export (Join-Path $script:BackupDir 'bcd.bak') | Out-Null
        if ($LASTEXITCODE -eq 0) { Note 'BCD exportado.' 'ok' } else { Note 'Nao consegui exportar o BCD.' 'warn' }
    } catch { Note 'Nao consegui exportar o BCD.' 'warn' }

    $regExports = [ordered]@{
        'dwm.reg'           = 'HKLM\SOFTWARE\Microsoft\Windows\Dwm'
        'power.reg'         = 'HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Power'
        'graphics.reg'      = 'HKLM\SYSTEM\CurrentControlSet\Control\GraphicsDrivers'
        'gamedvr.reg'       = 'HKCU\Software\Microsoft\Windows\CurrentVersion\GameDVR'
        'gameconfig.reg'    = 'HKCU\System\GameConfigStore'
        'gamebar.reg'       = 'HKCU\Software\Microsoft\GameBar'
        'mouse.reg'         = 'HKCU\Control Panel\Mouse'
        'gpupref.reg'       = 'HKCU\Software\Microsoft\DirectX\UserGpuPreferences'
        'compat.reg'        = 'HKCU\Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers'
        'bgapps-user.reg'   = 'HKCU\Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications'
        'bgapps-policy.reg' = 'HKLM\SOFTWARE\Policies\Microsoft\Windows\AppPrivacy'
        'mmcss.reg'         = 'HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile'
        'telemetry.reg'     = 'HKLM\SOFTWARE\Policies\Microsoft\Windows\DataCollection'
        'telemetry-mu.reg'  = 'HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection'
        'sqm.reg'           = 'HKLM\SOFTWARE\Policies\Microsoft\SQMClient'
        'diagtrack.reg'     = 'HKLM\SYSTEM\CurrentControlSet\Services\DiagTrack'
        'wappush.reg'       = 'HKLM\SYSTEM\CurrentControlSet\Services\dmwappushservice'
        'adinfo.reg'        = 'HKCU\Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo'
        'privacy.reg'       = 'HKCU\Software\Microsoft\Windows\CurrentVersion\Privacy'
        'siuf.reg'          = 'HKCU\Software\Microsoft\Siuf'
        'activity.reg'      = 'HKLM\SOFTWARE\Policies\Microsoft\Windows\System'
        'tipc.reg'          = 'HKCU\Software\Microsoft\Input\TIPC'
        'inputpers.reg'     = 'HKCU\Software\Microsoft\InputPersonalization'
        'inputpol.reg'      = 'HKLM\SOFTWARE\Policies\Microsoft\InputPersonalization'
        'speech.reg'        = 'HKCU\Software\Microsoft\Speech_OneCore'
        'wer.reg'           = 'HKLM\SOFTWARE\Microsoft\Windows\Windows Error Reporting'
    }
    foreach ($k in $regExports.Keys) { & reg export $regExports[$k] (Join-Path $script:BackupDir $k) /y 2>$null | Out-Null }
    Note 'Chaves de registro exportadas.' 'ok'

    $plano = (& powercfg /getactivescheme) -join ' '
    $guid  = ([regex]::Match($plano, '[0-9a-fA-F]{8}-([0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12}')).Value
    Set-Content -LiteralPath (Join-Path $script:BackupDir 'powerscheme.txt') -Value ($plano + "`r`n" + $guid) -Encoding UTF8

    if ($script:CriarPonto) {
        Set-Busy $true 'Criando ponto de restauracao... (1 a 3 min)'
        try {
            Enable-ComputerRestore -Drive "$env:SystemDrive\" -ErrorAction SilentlyContinue
            Checkpoint-Computer -Description "CS2-Optimizer $script:Stamp" -RestorePointType MODIFY_SETTINGS -ErrorAction Stop
            Note 'Ponto de restauracao criado.' 'ok'
        } catch {
            Note 'Ponto de restauracao nao criado (limite de 1 por 24h ou protecao do sistema desativada).' 'warn'
        }
        Set-Busy $true 'Aplicando as correcoes...'
    } else {
        Note 'Ponto de restauracao: nao criado (voce escolheu nao criar).' 'warn'
    }
    $script:BackupFeito = $true
}

# Onde procurar backups: primeiro a pasta do programa (novo padrao), depois a
# Area de Trabalho (backups antigos, de antes dessa mudanca).
function Get-BackupsEncontrados {
    $todos = @()
    foreach ($d in @($script:BaseDir, $script:Desktop)) {
        if ([string]::IsNullOrWhiteSpace($d)) { continue }
        if (-not (Test-Path -LiteralPath $d)) { continue }
        $todos += @(Get-ChildItem -LiteralPath $d -Directory -Filter 'CS2-Optimizer-Backup-*' -ErrorAction SilentlyContinue)
    }
    return @($todos | Sort-Object LastWriteTime -Descending)
}

# Monta a mensagem unica de confirmacao com tudo o que sera aplicado.
function Get-BatchMessage($lista) {
    $rotulos = @{ safe = 'seguro'; optional = 'opcional'; destructive = 'DESTRUTIVO' }
    $destrutivas = @($lista | Where-Object { $_.Nivel -eq 'destructive' })
    $linhas = @()
    foreach ($x in $lista) {
        $sufixo = if ($x.Reinicia) { '  (pede reinicio)' } else { '' }
        $linhas += ('  - [' + $rotulos[$x.Nivel] + '] ' + $x.Titulo + $sufixo)
    }
    $msg = "Vou aplicar $($lista.Count) correcao(oes):`r`n`r`n" + ($linhas -join "`r`n")
    if ($destrutivas.Count -gt 0) {
        $msg += "`r`n`r`nATENCAO: $($destrutivas.Count) item(ns) DESTRUTIVO(S) na lista. O backup devolve registro/BCD/plano de energia, mas nao desfaz tudo (ex.: rede resetada)."
    }
    $msg += "`r`n`r`nAntes de comecar e feito o backup (registro, BCD e plano de energia). O ponto de restauracao e perguntado logo em seguida."
    return $msg
}

function Confirm-Batch($lista) {
    $lista = @($lista)
    if ($lista.Count -eq 0) { return $false }
    $temDestrutivo = (@($lista | Where-Object { $_.Nivel -eq 'destructive' }).Count -gt 0)

    # console / Auto: pergunta simples
    if (-not $script:UI) {
        $nivel = if ($temDestrutivo) { 'destructive' } else { 'safe' }
        return (Ask (Get-BatchMessage $lista) $nivel)
    }

    # interface: janela com a lista, para dar para LER antes de decidir
    $f = New-Object System.Windows.Forms.Form
    $f.Text = 'Confirmar antes de aplicar'
    $f.Size = New-Object System.Drawing.Size(620, 470)
    $f.StartPosition = 'CenterParent'
    $f.FormBorderStyle = 'FixedDialog'
    $f.MaximizeBox = $false
    $f.MinimizeBox = $false
    $f.ShowInTaskbar = $false
    $f.Font = New-Object System.Drawing.Font('Segoe UI', 9)

    $lblTop = New-Object System.Windows.Forms.Label
    $lblTop.Text = "Vou aplicar $($lista.Count) correcao(oes) nesta maquina:"
    $lblTop.Location = New-Object System.Drawing.Point(14, 12)
    $lblTop.Size = New-Object System.Drawing.Size(580, 20)

    $lv = New-Object System.Windows.Forms.ListView
    $lv.Location = New-Object System.Drawing.Point(14, 36)
    $lv.Size = New-Object System.Drawing.Size(578, 240)
    $lv.View = 'Details'
    $lv.FullRowSelect = $true
    $lv.MultiSelect = $false
    $lv.HeaderStyle = 'Nonclickable'
    $lv.GridLines = $false
    $lv.Font = $script:FonteMono
    [void]$lv.Columns.Add('Correcao', 385)
    [void]$lv.Columns.Add('Tipo', 185)

    $rotulos = @{ safe = 'seguro'; optional = 'opcional'; destructive = 'DESTRUTIVO' }
    foreach ($x in $lista) {
        $it = New-Object System.Windows.Forms.ListViewItem($x.Titulo)
        [void]$it.SubItems.Add($rotulos[$x.Nivel])
        if ($x.Reinicia) { $it.SubItems[1].Text = $it.SubItems[1].Text + ' / reinicia' }
        if ($x.Nivel -eq 'destructive') { $it.ForeColor = [System.Drawing.Color]::FromArgb(170, 30, 30) }
        [void]$lv.Items.Add($it)
    }

    if ($temDestrutivo) {
        $n = @($lista | Where-Object { $_.Nivel -eq 'destructive' }).Count
        $rot = if ($n -eq 1) { 'item DESTRUTIVO' } else { 'itens DESTRUTIVOS' }
        $aviso = "$n $rot na lista (em vermelho). O backup devolve registro, BCD e plano de energia, mas nao desfaz tudo (ex.: rede resetada)."
    } else { $aviso = 'Nada aqui e destrutivo - tudo tem volta pelo backup.' }
    $lblAviso = New-Object System.Windows.Forms.Label
    $lblAviso.Text = $aviso
    $lblAviso.Location = New-Object System.Drawing.Point(14, 284)
    $lblAviso.Size = New-Object System.Drawing.Size(578, 50)
    $lblAviso.ForeColor = if ($temDestrutivo) { [System.Drawing.Color]::FromArgb(170, 30, 30) } else { [System.Drawing.Color]::FromArgb(60, 60, 60) }

    $lblBk = New-Object System.Windows.Forms.Label
    $lblBk.Text = "Antes de comecar: backup do registro, BCD e plano de energia.`r`nO ponto de restauracao voce escolhe na proxima tela."
    $lblBk.Location = New-Object System.Drawing.Point(14, 334)
    $lblBk.Size = New-Object System.Drawing.Size(578, 36)
    $lblBk.ForeColor = [System.Drawing.Color]::FromArgb(90, 90, 90)

    $btnOk = New-Object System.Windows.Forms.Button
    $btnOk.Text = 'Aplicar'
    $btnOk.Size = New-Object System.Drawing.Size(120, 32)
    $btnOk.Location = New-Object System.Drawing.Point(340, 380)

    $btnNo = New-Object System.Windows.Forms.Button
    $btnNo.Text = 'Cancelar'
    $btnNo.Size = New-Object System.Drawing.Size(120, 32)
    $btnNo.Location = New-Object System.Drawing.Point(472, 380)

    # com item destrutivo o padrao (Enter) e "Cancelar" - e o botao azul tambem
    $destaque = if ($temDestrutivo) { $btnNo } else { $btnOk }
    $destaque.BackColor = [System.Drawing.Color]::FromArgb(45, 127, 249)
    $destaque.ForeColor = [System.Drawing.Color]::White
    $destaque.FlatStyle = 'Flat'
    $destaque.FlatAppearance.BorderSize = 0
    $btnNo.DialogResult = [System.Windows.Forms.DialogResult]::Cancel

    if ($temDestrutivo) {
        $f.AcceptButton = $btnNo
        $f.CancelButton = $btnNo
        $btnOk.DialogResult = [System.Windows.Forms.DialogResult]::None
    } else {
        $f.AcceptButton = $btnOk
        $f.CancelButton = $btnNo
        $btnOk.DialogResult = [System.Windows.Forms.DialogResult]::OK
    }

    [void]$f.Controls.AddRange(@($lblTop, $lv, $lblAviso, $lblBk, $btnOk, $btnNo))
    $r = if ($script:UiForm) { $f.ShowDialog($script:UiForm) } else { $f.ShowDialog() }
    $f.Dispose()
    return ($r -eq [System.Windows.Forms.DialogResult]::OK)
}

# Pergunta se deve criar ponto de restauracao (uma vez a cada "Aplicar marcadas").
# Devolve $true  = criar
#         $false = seguir so com o backup (registro/BCD/plano de energia)
#         $null  = desistiu (fechou a janela) -> nao aplica nada
function Select-RestorePoint {
    if ($script:ReportOnly) { return $false }

    # sem janela (modo -Auto): nao tem quem responder, mantem o ponto como rede
    if ($script:Auto) {
        $script:CriarPonto = $true
        return $true
    }

    if (-not $script:UI) {
        $q = 'Criar um ponto de restauracao do Windows antes de aplicar? (recomendado: leva 1-3 min e o Windows so deixa 1 por dia)'
        $r = Ask $q 'safe'
        if ($r) { $script:CriarPonto = $true; Note 'Ponto de restauracao: sim.' 'info' }
        else { $script:CriarPonto = $false; Note 'Ponto de restauracao: nao.' 'warn' }
        return $r
    }

    $f = New-Object System.Windows.Forms.Form
    $f.Text = 'Ponto de restauracao'
    $f.Size = New-Object System.Drawing.Size(580, 320)
    $f.StartPosition = 'CenterParent'
    $f.FormBorderStyle = 'FixedDialog'
    $f.MaximizeBox = $false
    $f.MinimizeBox = $false
    $f.ShowInTaskbar = $false
    $f.Font = New-Object System.Drawing.Font('Segoe UI', 9)
    $f.Tag = 'cancelar'

    $lblTopo = New-Object System.Windows.Forms.Label
    $lblTopo.Text = 'Criar um ponto de restauracao do Windows antes de aplicar?'
    $lblTopo.Location = New-Object System.Drawing.Point(16, 16)
    $lblTopo.Size = New-Object System.Drawing.Size(530, 24)
    $lblTopo.Font = New-Object System.Drawing.Font('Segoe UI', 11, [System.Drawing.FontStyle]::Bold)

    $lblDetalhe = New-Object System.Windows.Forms.Label
    $lblDetalhe.Text = ("E a rede de seguranca do Windows: se algo der errado, da para voltar o sistema " +
        "inteiro pelo 'Restaurar sistema'.`r`n`r`n" +
        "Leva de 1 a 3 minutos e ocupa espaco em disco. O Windows so permite 1 ponto a cada 24h - " +
        "se ja houve um hoje, este pedido nao cria nada.`r`n`r`n" +
        "Sem o ponto, o backup do registro, do BCD e do plano de energia continua sendo feito do mesmo jeito.")
    $lblDetalhe.Location = New-Object System.Drawing.Point(16, 48)
    $lblDetalhe.Size = New-Object System.Drawing.Size(530, 170)

    $btnSim = New-Object System.Windows.Forms.Button
    $btnSim.Text = 'Sim, criar ponto'
    $btnSim.Size = New-Object System.Drawing.Size(150, 34)
    $btnSim.Location = New-Object System.Drawing.Point(232, 232)
    $btnSim.BackColor = [System.Drawing.Color]::FromArgb(45, 127, 249)
    $btnSim.ForeColor = [System.Drawing.Color]::White
    $btnSim.FlatStyle = 'Flat'
    $btnSim.FlatAppearance.BorderSize = 0
    $btnSim.DialogResult = [System.Windows.Forms.DialogResult]::OK

    $btnNao = New-Object System.Windows.Forms.Button
    $btnNao.Text = 'Nao, so o backup'
    $btnNao.Size = New-Object System.Drawing.Size(150, 34)
    $btnNao.Location = New-Object System.Drawing.Point(396, 232)
    $btnNao.DialogResult = [System.Windows.Forms.DialogResult]::Cancel

    $btnSim.Add_Click({ $f.Tag = 'sim' })
    $btnNao.Add_Click({ $f.Tag = 'nao' })

    $f.AcceptButton = $btnSim
    [void]$f.Controls.AddRange(@($lblTopo, $lblDetalhe, $btnSim, $btnNao))

    $r = if ($script:UiForm) { $f.ShowDialog($script:UiForm) } else { $f.ShowDialog() }
    $escolha = [string]$f.Tag
    $f.Dispose()

    # X -> seguir so com o backup (escolha explicita; fechar pelo X tambem)
    if ($escolha -eq 'sim') {
        $script:CriarPonto = $true; return $true
    }
    if ($escolha -eq 'nao' -or $r -eq [System.Windows.Forms.DialogResult]::Cancel) {
        $script:CriarPonto = $false; return $false
    }
    return $null
}

function Apply-Fixes($lista, [switch]$Confirmado) {
    $lista = @($lista)
    if ($lista.Count -eq 0) { Note 'Nada marcado para aplicar.' 'warn'; return }
    if ($script:ReportOnly) { Note 'Modo somente relatorio: nada foi alterado.' 'warn'; return }

    # Uma confirmacao so, com a lista inteira. Antes era uma pergunta por
    # correcao, o que virava um festival de cliques em "Sim".
    if ($script:ConfirmarCada -and -not $Confirmado) {
        if (-not (Confirm-Batch $lista)) { Note 'Cancelado - nada foi alterado.' 'warn'; return }
    }

    # Ponto de restauracao: nunca automatico. Na interface a pergunta ja foi
    # feita no clique de "Aplicar marcadas"; aqui cobre o modo texto.
    if (-not $Confirmado) {
        $ponto = Select-RestorePoint
        if ($null -eq $ponto) { Note 'Cancelado - nada foi alterado.' 'warn'; return }
    }

    if ($script:BackupFeito -eq $false) { New-Backup }

    $aplicados = 0
    foreach ($fix in $lista) {
        try {
            # sem a confirmacao geral, um item destrutivo ainda pede a dele
            if (-not $script:ConfirmarCada -and $fix.Nivel -eq 'destructive') {
                if (-not (Ask ($fix.Titulo.ToUpper() + "`r`n`r`n" + $fix.Detalhe) $fix.Nivel)) {
                    [void]$script:Skipped.Add($fix.Titulo)
                    Note ('PULADO  ' + $fix.Titulo) 'warn'
                    continue
                }
            }
            Note ('aplicando: ' + $fix.Titulo) 'titulo'
            & $fix.Acao
            [void]$script:Applied.Add($fix.Titulo)
            if ($fix.Reinicia) { $script:NeedReboot = $true }
            $aplicados++
            Note ('OK  ' + $fix.Titulo) 'ok'
        } catch {
            Note ('FALHOU  ' + $fix.Titulo + ' -> ' + $_.Exception.Message) 'bad'
        }
        Pump
    }
    if ($aplicados -gt 0) { Note "$aplicados correcao(oes) aplicada(s)." 'ok' }
}

function Restore-Backup([string]$dir) {
    if (-not (Test-Path -LiteralPath $dir)) { throw "Pasta de backup nao encontrada: $dir" }
    Note ("Restaurando de: " + $dir) 'titulo'

    foreach ($r in @(Get-ChildItem -LiteralPath $dir -Filter '*.reg' -ErrorAction SilentlyContinue)) {
        & reg import $r.FullName 2>&1 | Out-Null
        Note ('  importado: ' + $r.Name) 'info'
    }

    $bcd = Join-Path $dir 'bcd.bak'
    if (Test-Path $bcd) {
        & bcdedit /import $bcd | Out-Null
        if ($LASTEXITCODE -eq 0) { Note 'BCD restaurado.' 'ok' } else { Note 'Nao consegui restaurar o BCD.' 'warn' }
    }

    $psfile = Join-Path $dir 'powerscheme.txt'
    if (Test-Path $psfile) {
        $g = ([regex]::Match((Get-Content -LiteralPath $psfile -Raw), '[0-9a-fA-F]{8}-([0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12}')).Value
        if ($g) {
            & powercfg /setactive $g 2>$null
            if ($LASTEXITCODE -eq 0) { Note 'Plano de energia original reativado.' 'ok' } else { Note 'Plano original nao existe mais neste PC.' 'warn' }
        }
    }

    # Valor de GPU do cs2.exe: so remove se o backup nao tinha esse valor
    $Ctx = $script:Ctx
    if ($Ctx.Cs2Exe) {
        $gpBackup = Join-Path $dir 'gpupref.reg'
        $tinhaValor = $false
        if (Test-Path $gpBackup) {
            $txt = Get-Content -LiteralPath $gpBackup -Raw
            $tinhaValor = [bool]($txt -match [regex]::Escape(($Ctx.Cs2Exe -replace '\\', '\\\\')))
        }
        if (-not $tinhaValor) {
            Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\DirectX\UserGpuPreferences' -Name $Ctx.Cs2Exe -ErrorAction SilentlyContinue
            Note 'Preferencia de GPU do cs2.exe removida.' 'ok'
        }
    }

    Note 'Backup restaurado. Reinicie o PC.' 'ok'
}
function Get-Cs2Tips {
    @(
        'Video: TELA CHEIA (em modo janela, principalmente em 4:3, a imagem fica embacada); resolucao nativa ou a mesma do Windows; taxa de atualizacao no maximo.',
        'Configuracoes > Jogo: desative "Avatares animados". Se usa push-to-talk, ative "Pressionar para falar simplificado".',
        'Max. FPS (fps_max) em 0, a menos que tenha motivo. O padrao (ex.: 350) pode piorar o 1% low.',
        'NVIDIA Reflex: "Enable" ja basta; use "Enable + Boost" se a GPU vive perto de 99% (clock mais estavel, menos stutter).',
        'Aumentar contraste dos jogadores: opcional (da contorno em jogadores distantes), mas custa FPS.',
        'Antialiasing: CMAA2 em Full HD nativo; MSAA 4x em resolucoes baixas (ex.: 1280x960 4:3), onde ha poucos pixels.',
        'Qualidade de sombras: Baixa. Detalhe de texturas: Alto se sobrar VRAM. Sombras dinamicas: Todas (vantagem de ver a sombra antes) ou so Sol para FPS maximo.',
        'Shaders: Baixo | Particulas: Baixo | Oclusao de ambiente: Desativada | HDR: Qualidade (Desempenho deixa a imagem granulada) | FidelityFX Super Resolution: SEMPRE desativado.',
        'Para saber se a GPU limita: teste na sua resolucao e na menor possivel; se o FPS subir muito, a GPU era o limite.',
        'Rede (opcional, teste): cl_net_buffer_ticks 1 e largura de banda maxima ~2.5-3 MB podem deixar o subtick mais consistente.',
        'Engasgos mesmo com FPS alto? Teste com G-Sync: G-Sync ligado (janela e tela cheia), V-Sync LIGADO no perfil do CS2 no painel NVIDIA, Reflex ligado e V-Sync DESLIGADO no jogo. Pode somar input lag; se preferir FPS solto, mantenha sem.',
        'Rotina: rode a limpeza de cache de shaders/sistema de vez em quando (semanal/mensal). Na primeira partida depois, o jogo trava um pouco enquanto recompila.',
        'Diagnostico: PresentMon -> o jogo deve estar em "Hardware: Independent Flip". MSI Afterburner -> veja se o Power Limit da GPU bate (se sim, undervolt resolve).'
    )
}

# ============================================================================
# MODO INTERFACE (WinForms)
# ============================================================================
function Add-UiDiag([string]$Kind, [string]$Text) {
    $rt = $script:UiDiag
    if (-not $rt) { return }
    # se o usuario rolou para cima para ler, nao puxa ele de volta para o fim
    $noFim = ($rt.SelectionStart -ge ($rt.TextLength - 3))

    $cor = switch ($Kind) {
        'section' { [System.Drawing.Color]::FromArgb(31, 78, 121) }
        'ok'      { [System.Drawing.Color]::FromArgb(21, 115, 71) }
        'warn'    { [System.Drawing.Color]::FromArgb(154, 103, 0) }
        'bad'     { [System.Drawing.Color]::FromArgb(192, 57, 43) }
        default   { [System.Drawing.Color]::FromArgb(51, 51, 51) }
    }
    $rt.SelectionStart  = $rt.TextLength
    $rt.SelectionLength = 0
    $rt.SelectionColor  = $cor
    $rt.SelectionFont   = if ($Kind -eq 'section') { $script:FonteMonoNegrito } else { $script:FonteMono }

    $prefixo = switch ($Kind) {
        'section' { "`r`n--- " + $Text.ToUpper() + " ---`r`n" }
        'info'    { '  [i] ' + $Text + "`r`n" }
        'ok'      { '  [OK] ' + $Text + "`r`n" }
        'warn'    { '  [!] ' + $Text + "`r`n" }
        'bad'     { '  [X] ' + $Text + "`r`n" }
        default   { '  ' + $Text + "`r`n" }
    }
    $rt.AppendText($prefixo)
    if ($noFim) {
        $rt.SelectionStart = $rt.TextLength
        $rt.SelectionLength = 0
        $rt.ScrollToCaret()
    }
    Pump
}

function Add-UiNote([string]$Text, [string]$Kind) {
    $rt = $script:UiNote
    if (-not $rt) { return }
    $cor = switch ($Kind) {
        'ok'     { [System.Drawing.Color]::FromArgb(21, 115, 71) }
        'warn'   { [System.Drawing.Color]::FromArgb(154, 103, 0) }
        'bad'    { [System.Drawing.Color]::FromArgb(192, 57, 43) }
        'titulo' { [System.Drawing.Color]::FromArgb(31, 78, 121) }
        default  { [System.Drawing.Color]::FromArgb(70, 70, 70) }
    }
    $rt.SelectionStart  = $rt.TextLength
    $rt.SelectionLength = 0
    $rt.SelectionColor  = $cor
    $rt.AppendText(('{0}  {1}' -f (Get-Date -Format 'HH:mm:ss'), $Text) + "`r`n")
    $rt.SelectionStart = $rt.TextLength
    $rt.ScrollToCaret()
    Pump
}

function Update-Checklist {
    $sb = New-Object System.Text.StringBuilder
    [void]$sb.AppendLine('CHECKLIST MANUAL - o script nao consegue fazer/verificar')
    [void]$sb.AppendLine('')
    if ($script:Manual.Count -eq 0) { [void]$sb.AppendLine('(nada pendente)') }
    foreach ($m in $script:Manual) { [void]$sb.AppendLine('  [ ] ' + $m) }
    [void]$sb.AppendLine('')
    [void]$sb.AppendLine('DENTRO DO CS2')
    [void]$sb.AppendLine('')
    foreach ($t in (Get-Cs2Tips)) { [void]$sb.AppendLine('  - ' + $t) }
    if ($script:NeedReboot) {
        [void]$sb.AppendLine('')
        [void]$sb.AppendLine('REINICIE O PC para concluir as alteracoes (BCD / MPO / HAGS / mouse).')
    }
    $script:UiChecklist.Text = $sb.ToString()
}

function Update-FixList($fixes) {
    $lv = $script:UiLista
    $lv.BeginUpdate()
    $lv.Items.Clear()
    $rotulo = @{ safe = 'Seguro'; optional = 'Opcional'; destructive = 'Destrutivo' }
    foreach ($fx in $fixes) {
        $item = New-Object System.Windows.Forms.ListViewItem($fx.Titulo)
        [void]$item.SubItems.Add($fx.Situacao)
        [void]$item.SubItems.Add($rotulo[$fx.Nivel])
        $item.Tag = $fx
        if ($fx.Nivel -eq 'destructive') { $item.ForeColor = [System.Drawing.Color]::FromArgb(150, 40, 40) }
        elseif ($fx.Nivel -eq 'optional') { $item.ForeColor = [System.Drawing.Color]::FromArgb(110, 110, 110) }
        if ($fx.Reinicia) { $item.SubItems[1].Text = $item.SubItems[1].Text + ' (pede reinicio)' }
        [void]$lv.Items.Add($item)
        # recomendada = problema real (seguro) ou manutencao leve; o resto voce decide
        $item.Checked = (Test-Recomendada $fx)
    }
    $lv.EndUpdate()
}

function Invoke-GuiScan {
    Set-Busy $true 'Lendo a maquina...'
    try {
        $script:UiDiag.Clear()
        Invoke-Diagnostics
        $fixes = @(Build-Fixes)
        Update-FixList $fixes
        Update-Checklist
        $qtd = @($fixes | Where-Object { Test-Recomendada $_ }).Count
        if ($qtd -eq 0) { $msg = "Diagnostico concluido - nada critico encontrado ($($script:Problems.Count) aviso(s)). Opcionais na aba Correcoes." }
        else { $msg = "Diagnostico concluido - $($script:Problems.Count) ponto(s) de atencao, $qtd correcao(oes) recomendada(s) prontas." }
        Set-Busy $false $msg
    } catch {
        Add-UiDiag 'bad' ('Falha no diagnostico: ' + $_.Exception.Message)
        Set-Busy $false 'Falha no diagnostico (veja a aba Diagnostico).'
    }
}

function Start-Gui {
    Add-Type -AssemblyName System.Windows.Forms
    Add-Type -AssemblyName System.Drawing
    try {
        Add-Type -Namespace Win32 -Name Nativo -MemberDefinition '[DllImport("user32.dll")] public static extern bool SetProcessDPIAware();'
        [void][Win32.Nativo]::SetProcessDPIAware()
    } catch {}
    [System.Windows.Forms.Application]::EnableVisualStyles()

    $script:UI = $true

    $corFundo  = [System.Drawing.Color]::FromArgb(250, 250, 252)
    $corTopo   = [System.Drawing.Color]::FromArgb(32, 41, 58)
    $corAcento = [System.Drawing.Color]::FromArgb(45, 127, 249)
    $corBorda  = [System.Drawing.Color]::FromArgb(225, 228, 234)
    $script:FonteMono        = New-Object System.Drawing.Font('Consolas', 9.5)
    $script:FonteMonoNegrito = New-Object System.Drawing.Font('Consolas', 9.5, [System.Drawing.FontStyle]::Bold)

    $form = New-Object System.Windows.Forms.Form
    $form.Text = 'CS2 / Windows Optimizer'
    $form.Size = New-Object System.Drawing.Size(1060, 800)
    $form.MinimumSize = New-Object System.Drawing.Size(880, 620)
    $form.StartPosition = 'CenterScreen'
    $form.BackColor = $corFundo
    $form.Font = New-Object System.Drawing.Font('Segoe UI', 9)
    $script:UiForm = $form
    # rodando como .exe: usa o icone embutido no proprio executavel na barra de titulo
    if ($PSCommandPath -like '*.exe') {
        try { $form.Icon = [System.Drawing.Icon]::ExtractAssociatedIcon($PSCommandPath) } catch {}
    }

    # ---------------- abas ----------------
    $tabs = New-Object System.Windows.Forms.TabControl
    $tabs.Dock = 'Fill'
    $tabs.Padding = New-Object System.Drawing.Point(14, 6)
    $tabs.Font = New-Object System.Drawing.Font('Segoe UI', 9)

    # --- aba Diagnostico
    $tabDiag = New-Object System.Windows.Forms.TabPage
    $tabDiag.Text = '  Diagnostico  '
    $tabDiag.BackColor = [System.Drawing.Color]::White
    $tabDiag.Padding = New-Object System.Windows.Forms.Padding(10)

    $barraDiag = New-Object System.Windows.Forms.Panel
    $barraDiag.Dock = 'Top'
    $barraDiag.Height = 42
    $barraDiag.BackColor = [System.Drawing.Color]::White

    $btnScan = New-Object System.Windows.Forms.Button
    $btnScan.Text = 'Escanear de novo'
    $btnScan.Location = New-Object System.Drawing.Point(0, 4)
    $btnScan.Size = New-Object System.Drawing.Size(140, 30)
    $btnScan.FlatStyle = 'System'

    $btnSalvar = New-Object System.Windows.Forms.Button
    $btnSalvar.Text = 'Salvar relatorio...'
    $btnSalvar.Location = New-Object System.Drawing.Point(148, 4)
    $btnSalvar.Size = New-Object System.Drawing.Size(150, 30)
    $btnSalvar.FlatStyle = 'System'

    $lblProblemas = New-Object System.Windows.Forms.Label
    $lblProblemas.AutoSize = $true
    $lblProblemas.Location = New-Object System.Drawing.Point(310, 11)
    $lblProblemas.ForeColor = [System.Drawing.Color]::FromArgb(110, 110, 110)

    $barraDiag.Controls.AddRange(@($btnScan, $btnSalvar, $lblProblemas))

    $script:UiDiag = New-Object System.Windows.Forms.RichTextBox
    $script:UiDiag.Dock = 'Fill'
    $script:UiDiag.ReadOnly = $true
    $script:UiDiag.BorderStyle = 'None'
    $script:UiDiag.BackColor = [System.Drawing.Color]::White
    $script:UiDiag.Font = $script:FonteMono
    $script:UiDiag.WordWrap = $false
    $script:UiDiag.DetectUrls = $false

    $tabDiag.Controls.Add($script:UiDiag)
    $tabDiag.Controls.Add($barraDiag)

    # --- aba Correcoes
    $tabFix = New-Object System.Windows.Forms.TabPage
    $tabFix.Text = '  Correcoes  '
    $tabFix.BackColor = [System.Drawing.Color]::White
    $tabFix.Padding = New-Object System.Windows.Forms.Padding(10)

    $painelFix = New-Object System.Windows.Forms.Panel
    $painelFix.Dock = 'Bottom'
    $painelFix.Height = 104
    $painelFix.BackColor = [System.Drawing.Color]::White

    $lblDetalhe = New-Object System.Windows.Forms.Label
    $lblDetalhe.Location = New-Object System.Drawing.Point(0, 2)
    $lblDetalhe.Size = New-Object System.Drawing.Size(1000, 34)
    $lblDetalhe.Anchor = ([System.Windows.Forms.AnchorStyles]::Top -bor [System.Windows.Forms.AnchorStyles]::Left -bor [System.Windows.Forms.AnchorStyles]::Right)
    $lblDetalhe.ForeColor = [System.Drawing.Color]::FromArgb(90, 90, 90)

    $chkCada = New-Object System.Windows.Forms.CheckBox
    $chkCada.Text = 'Pedir confirmacao antes de aplicar'
    $chkCada.Checked = $false
    $chkCada.Location = New-Object System.Drawing.Point(3, 44)
    $chkCada.AutoSize = $true

    $btnAplicar = New-Object System.Windows.Forms.Button
    $btnAplicar.Text = 'Aplicar marcadas'
    $btnAplicar.Location = New-Object System.Drawing.Point(3, 68)
    $btnAplicar.Size = New-Object System.Drawing.Size(160, 32)
    $btnAplicar.FlatStyle = 'Flat'
    $btnAplicar.BackColor = $corAcento
    $btnAplicar.ForeColor = [System.Drawing.Color]::White
    $btnAplicar.FlatAppearance.BorderSize = 0

    $btnMarcar = New-Object System.Windows.Forms.Button
    $btnMarcar.Text = 'Marcar recomendadas'
    $btnMarcar.Location = New-Object System.Drawing.Point(171, 68)
    $btnMarcar.Size = New-Object System.Drawing.Size(155, 32)
    $btnMarcar.FlatStyle = 'System'

    $btnLimpar = New-Object System.Windows.Forms.Button
    $btnLimpar.Text = 'Desmarcar tudo'
    $btnLimpar.Location = New-Object System.Drawing.Point(334, 68)
    $btnLimpar.Size = New-Object System.Drawing.Size(120, 32)
    $btnLimpar.FlatStyle = 'System'

    $btnDesfazer = New-Object System.Windows.Forms.Button
    $btnDesfazer.Text = 'Desfazer backup...'
    $btnDesfazer.Location = New-Object System.Drawing.Point(462, 68)
    $btnDesfazer.Size = New-Object System.Drawing.Size(150, 32)
    $btnDesfazer.FlatStyle = 'System'

    $btnAbrirBk = New-Object System.Windows.Forms.Button
    $btnAbrirBk.Text = 'Abrir pasta de backup'
    $btnAbrirBk.Location = New-Object System.Drawing.Point(620, 68)
    $btnAbrirBk.Size = New-Object System.Drawing.Size(160, 32)
    $btnAbrirBk.FlatStyle = 'System'

    $painelFix.Controls.AddRange(@($lblDetalhe, $chkCada, $btnAplicar, $btnMarcar, $btnLimpar, $btnDesfazer, $btnAbrirBk))

    $script:UiLista = New-Object System.Windows.Forms.ListView
    $script:UiLista.Dock = 'Fill'
    $script:UiLista.View = 'Details'
    $script:UiLista.CheckBoxes = $true
    $script:UiLista.FullRowSelect = $true
    $script:UiLista.MultiSelect = $false
    $script:UiLista.HideSelection = $false
    $script:UiLista.BorderStyle = 'FixedSingle'
    $script:UiLista.BackColor = [System.Drawing.Color]::White
    [void]$script:UiLista.Columns.Add('Correcao', 420)
    [void]$script:UiLista.Columns.Add('Situacao atual', 400)
    [void]$script:UiLista.Columns.Add('Nivel', 95)

    # a coluna do meio acompanha a largura da janela
    $script:UiLista.Add_Resize({
        try {
            $larg = $script:UiLista.ClientSize.Width
            $meio = [Math]::Max(180, $larg - 420 - 95 - 4)
            $script:UiLista.Columns[1].Width = $meio
            $script:UiLista.Columns[0].Width = [Math]::Max(200, $larg - $meio - 95 - 4)
        } catch {}
    })

    $tabFix.Controls.Add($script:UiLista)
    $tabFix.Controls.Add($painelFix)

    # --- aba Checklist
    $tabList = New-Object System.Windows.Forms.TabPage
    $tabList.Text = '  Checklist  '
    $tabList.BackColor = [System.Drawing.Color]::White
    $tabList.Padding = New-Object System.Windows.Forms.Padding(10)
    $script:UiChecklist = New-Object System.Windows.Forms.TextBox
    $script:UiChecklist.Multiline = $true
    $script:UiChecklist.Dock = 'Fill'
    $script:UiChecklist.ReadOnly = $true
    $script:UiChecklist.ScrollBars = 'Vertical'
    $script:UiChecklist.BorderStyle = 'None'
    $script:UiChecklist.BackColor = [System.Drawing.Color]::White
    $script:UiChecklist.Font = New-Object System.Drawing.Font('Segoe UI', 9.5)
    $script:UiChecklist.WordWrap = $true
    $tabList.Controls.Add($script:UiChecklist)

    [void]$tabs.TabPages.Add($tabDiag)
    [void]$tabs.TabPages.Add($tabFix)
    [void]$tabs.TabPages.Add($tabList)

    # ---------------- log de atividade ----------------
    $painelLog = New-Object System.Windows.Forms.Panel
    $painelLog.Dock = 'Bottom'
    $painelLog.Height = 128
    $painelLog.BackColor = [System.Drawing.Color]::White
    $painelLog.Padding = New-Object System.Windows.Forms.Padding(10, 4, 10, 6)

    $lblLog = New-Object System.Windows.Forms.Label
    $lblLog.Text = 'ATIVIDADE'
    $lblLog.Dock = 'Top'
    $lblLog.Height = 16
    $lblLog.Font = New-Object System.Drawing.Font('Segoe UI', 7.5, [System.Drawing.FontStyle]::Bold)
    $lblLog.ForeColor = [System.Drawing.Color]::FromArgb(140, 145, 155)

    $script:UiNote = New-Object System.Windows.Forms.RichTextBox
    $script:UiNote.Dock = 'Fill'
    $script:UiNote.ReadOnly = $true
    $script:UiNote.BorderStyle = 'FixedSingle'
    $script:UiNote.BackColor = [System.Drawing.Color]::FromArgb(250, 250, 252)
    $script:UiNote.Font = New-Object System.Drawing.Font('Consolas', 8.5)
    $script:UiNote.WordWrap = $false
    $script:UiNote.DetectUrls = $false

    $painelLog.Controls.Add($script:UiNote)
    $painelLog.Controls.Add($lblLog)

    # ---------------- cabecalho ----------------
    $topo = New-Object System.Windows.Forms.Panel
    $topo.Dock = 'Top'
    $topo.Height = 76
    $topo.BackColor = $corTopo

    $lblTitulo = New-Object System.Windows.Forms.Label
    $lblTitulo.Text = 'CS2 / WINDOWS OPTIMIZER'
    $lblTitulo.Font = New-Object System.Drawing.Font('Segoe UI', 15, [System.Drawing.FontStyle]::Bold)
    $lblTitulo.ForeColor = [System.Drawing.Color]::White
    $lblTitulo.AutoSize = $true
    $lblTitulo.Location = New-Object System.Drawing.Point(20, 12)

    $lblSub = New-Object System.Windows.Forms.Label
    $lblSub.Text = "$($env:COMPUTERNAME)   |   $([Environment]::OSVersion.VersionString)   |   log: $(Split-Path $script:LogFile -Leaf)"
    $lblSub.Font = New-Object System.Drawing.Font('Segoe UI', 8.5)
    $lblSub.ForeColor = [System.Drawing.Color]::FromArgb(168, 178, 196)
    $lblSub.AutoSize = $true
    $lblSub.Location = New-Object System.Drawing.Point(22, 46)

    $lblAdmin = New-Object System.Windows.Forms.Label
    $lblAdmin.Text = 'ADMINISTRADOR'
    $lblAdmin.Font = New-Object System.Drawing.Font('Segoe UI', 8, [System.Drawing.FontStyle]::Bold)
    $lblAdmin.ForeColor = [System.Drawing.Color]::FromArgb(120, 220, 160)
    $lblAdmin.AutoSize = $true
    $lblAdmin.Anchor = 'Top,Right'
    $lblAdmin.Location = New-Object System.Drawing.Point(930, 20)

    $topo.Controls.AddRange(@($lblTitulo, $lblSub, $lblAdmin))

    # o selo ADMINISTRADOR fica grudado na direita
    $posAdmin = {
        try { $lblAdmin.Location = New-Object System.Drawing.Point([Math]::Max(10, $topo.Width - $lblAdmin.Width - 26), 20) } catch {}
    }
    $topo.Add_Resize($posAdmin)
    & $posAdmin

    # ---------------- barra de status ----------------
    $status = New-Object System.Windows.Forms.StatusStrip
    $status.BackColor = [System.Drawing.Color]::FromArgb(240, 242, 246)
    $script:UiStatus = New-Object System.Windows.Forms.ToolStripStatusLabel
    $script:UiStatus.Text = 'Pronto.'
    [void]$status.Items.Add($script:UiStatus)
    $script:UiProgresso = New-Object System.Windows.Forms.ToolStripProgressBar
    $script:UiProgresso.Style = 'Marquee'
    $script:UiProgresso.Visible = $false
    $script:UiProgresso.Width = 140
    [void]$status.Items.Add($script:UiProgresso)

    # ordem importa: Fill primeiro, depois as bordas
    $form.Controls.Add($tabs)
    $form.Controls.Add($painelLog)
    $form.Controls.Add($topo)
    $form.Controls.Add($status)

    # ---------------- comportamento ----------------
    $script:UiBotoes = @($btnAplicar, $btnMarcar, $btnLimpar, $btnDesfazer, $btnAbrirBk, $btnScan, $btnSalvar)
    $script:SetBusyImpl = {
        param([bool]$ocupado, [string]$texto)
        if ($texto) { $script:UiStatus.Text = $texto }
        $script:UiProgresso.Visible = $ocupado
        $script:UiProgresso.Style = if ($ocupado) { 'Marquee' } else { 'Blocks' }
        $script:UiLista.Enabled = -not $ocupado
        foreach ($b in $script:UiBotoes) { $b.Enabled = -not $ocupado }
        Pump
    }

    $btnScan.Add_Click({
        try { Invoke-GuiScan } catch { Add-UiNote ('erro: ' + $_.Exception.Message) 'bad' }
    })

    $btnSalvar.Add_Click({
        $sfd = New-Object System.Windows.Forms.SaveFileDialog
        $sfd.Filter = 'Texto (*.txt)|*.txt'
        $sfd.FileName = "CS2-Optimizer-Relatorio-$(Get-Date -Format 'yyyyMMdd-HHmm').txt"
        if ($sfd.ShowDialog() -eq 'OK') {
            $texto = @()
            foreach ($l in $script:Diag) { $texto += ('[{0}] {1}' -f $l.Kind, $l.Text) }
            $texto += ''
            $texto += 'CHECKLIST MANUAL'
            $texto += $script:Manual
            Set-Content -LiteralPath $sfd.FileName -Value $texto -Encoding UTF8
            Add-UiNote ('relatorio salvo em ' + $sfd.FileName) 'ok'
        }
    })

    $script:UiLista.Add_SelectedIndexChanged({
        if ($script:UiLista.SelectedItems.Count -gt 0) {
            $fx = $script:UiLista.SelectedItems[0].Tag
            if ($fx) { $lblDetalhe.Text = $fx.Detalhe }
        }
    })

    $btnMarcar.Add_Click({
        $marcadas = 0
        $total = 0
        foreach ($it in $script:UiLista.Items) {
            if (Test-Recomendada $it.Tag) {
                $total++
                if (-not $it.Checked) { $it.Checked = $true; $marcadas++ }
            }
        }
        if ($total -eq 0) {
            [void][System.Windows.Forms.MessageBox]::Show(
                "Nao ha correcoes recomendadas pendentes nesta maquina.`r`n`r`nA lista tem apenas itens de reparo demorado (DISM/SFC/CHKDSK) e o reset de rede, que ficam para voce marcar na mao.",
                'Nada recomendado', 'OK', 'Information')
        } elseif ($marcadas -eq 0) {
            Add-UiNote "As $total correcoes recomendadas ja estavam marcadas." 'info'
        } else {
            Add-UiNote "$marcadas correcao(oes) recomendada(s) marcada(s)." 'ok'
        }
    })

    $btnLimpar.Add_Click({
        foreach ($it in $script:UiLista.Items) { $it.Checked = $false }
    })

    $btnAplicar.Add_Click({
        $marcadas = @()
        foreach ($it in $script:UiLista.CheckedItems) { $marcadas += $it.Tag }
        if ($marcadas.Count -eq 0) {
            [void][System.Windows.Forms.MessageBox]::Show('Marque pelo menos uma correcao na lista.', 'Nada marcado', 'OK', 'Information')
            return
        }
        $script:ConfirmarCada = $chkCada.Checked
        # confirmacao unica, antes de comecar (fora do Set-Busy, com a janela limpa)
        if ($script:ConfirmarCada) {
            try {
                if (-not (Confirm-Batch $marcadas)) {
                    Add-UiNote 'Cancelado - nada foi alterado.' 'warn'
                    Set-Busy $false 'Cancelado.'
                    return
                }
            } catch {
                Add-UiNote ('erro na confirmacao: ' + $_.Exception.Message) 'bad'
                Set-Busy $false 'Pronto.'
                return
            }
        }
        # Ponto de restauracao: perguntado sempre, nunca automatico
        try {
            $ponto = Select-RestorePoint
            if ($null -eq $ponto) {
                Add-UiNote 'Cancelado - nada foi alterado.' 'warn'
                Set-Busy $false 'Cancelado.'
                return
            }
        } catch {
            Add-UiNote ('erro na pergunta do ponto de restauracao: ' + $_.Exception.Message) 'bad'
            Set-Busy $false 'Pronto.'
            return
        }
        try {
            Set-Busy $true 'Aplicando...'
            Apply-Fixes $marcadas -Confirmado
            Set-Busy $false 'Reavaliando a maquina...'
            Invoke-GuiScan
            if ($script:NeedReboot) { Add-UiNote 'REINICIE o PC para concluir as alteracoes.' 'warn' }
        } catch {
            Add-UiNote ('erro ao aplicar: ' + $_.Exception.Message) 'bad'
        } finally {
            Set-Busy $false 'Pronto.'
        }
    })

    $btnAbrirBk.Add_Click({
        $bk = $script:BackupDir
        if (-not (Test-Path $bk)) {
            $bks = @(Get-BackupsEncontrados)
            if ($bks.Count -gt 0) { $bk = $bks[0].FullName }
        }
        if (Test-Path $bk) { Start-Process explorer.exe $bk | Out-Null }
        else {
            # ainda nao houve backup: abre a pasta onde ele vai nascer
            if (Test-Path $script:BaseDir) { Start-Process explorer.exe $script:BaseDir | Out-Null }
            else { [void][System.Windows.Forms.MessageBox]::Show('Nenhum backup encontrado.', 'Backup', 'OK', 'Information') }
        }
    })

    $btnDesfazer.Add_Click({
        $fb = New-Object System.Windows.Forms.FolderBrowserDialog
        $fb.Description = 'Escolha a pasta CS2-Optimizer-Backup-* que deseja restaurar'
        $bks = @(Get-BackupsEncontrados)
        if ($bks.Count -gt 0) { $fb.SelectedPath = $bks[0].FullName }
        if ($fb.ShowDialog() -ne 'OK') { return }
        $r = [System.Windows.Forms.MessageBox]::Show("Restaurar o registro/BCD/plano de energia a partir de:`r`n$($fb.SelectedPath)`r`n`r`nIsso desfaz os ajustes feitos naquele dia. Continuar?", 'Desfazer', 'YesNo', 'Warning', 'No')
        if ($r -ne 'Yes') { return }
        try {
            Set-Busy $true 'Restaurando...'
            Restore-Backup $fb.SelectedPath
            Set-Busy $false 'Pronto.'
            [void][System.Windows.Forms.MessageBox]::Show('Backup restaurado. Reinicie o PC.', 'Desfazer', 'OK', 'Information')
            Invoke-GuiScan
        } catch {
            Add-UiNote ('erro ao desfazer: ' + $_.Exception.Message) 'bad'
            Set-Busy $false 'Falha ao desfazer.'
        }
    })

    $form.Add_Shown({
        try { Invoke-GuiScan } catch { Add-UiNote ('erro: ' + $_.Exception.Message) 'bad' }
        if ($script:ReportOnly) { Add-UiNote 'Modo somente relatorio: nenhuma correcao sera aplicada.' 'warn' }
    })

    $form.Add_KeyDown({
        param($s, $e)
        if ($e.KeyCode -eq 'Escape') { $form.Close() }
        if ($e.Control -and $e.KeyCode -eq 'S') { Invoke-GuiScan }
    })
    $form.KeyPreview = $true

    [void]$form.ShowDialog()
}
# ============================================================================
# MODO TEXTO
# ============================================================================
function Start-ConsoleMode {
    Clear-Host
    Write-Host '################################################' -ForegroundColor Magenta
    Write-Host '#          CS2 / Windows Optimizer             #' -ForegroundColor Magenta
    Write-Host '################################################' -ForegroundColor Magenta
    if ($script:ReportOnly) { Write-Host 'Modo: SOMENTE RELATORIO (nada sera alterado)' -ForegroundColor Yellow }
    elseif ($script:Auto)   { Write-Host 'Modo: AUTO (so correcoes seguras, sem perguntar)' -ForegroundColor Yellow }
    else                    { Write-Host 'Modo: INTERATIVO (cada correcao pede confirmacao)' -ForegroundColor Yellow }
    Write-Host "Log: $($script:LogFile)"

    Write-Host ''
    Write-Host '################ FASE 1: DIAGNOSTICO ################' -ForegroundColor Magenta
    Invoke-Diagnostics
    $fixes = @(Build-Fixes)

    Write-Host ''
    Write-Host '################ FASE 2: CORRECOES ################' -ForegroundColor Magenta
    if ($script:ReportOnly) {
        Info 'Modo -ReportOnly: nenhuma alteracao sera feita.'
    } elseif ($fixes.Count -eq 0) {
        Ok 'Nada a corrigir por aqui.'
    } else {
        # o backup e feito dentro de Apply-Fixes, depois da confirmacao
        Apply-Fixes $fixes
    }

    Write-Host ''
    Write-Host '################ FASE 3: RESUMO ################' -ForegroundColor Magenta
    Write-Host ''
    Write-Host '=== Aplicado ===' -ForegroundColor Cyan
    if ($script:Applied.Count -eq 0) { Write-Host '  [i] Nada foi alterado.' -ForegroundColor Gray }
    else { $script:Applied | ForEach-Object { Write-Host ('  [OK] ' + $_) -ForegroundColor Green } }

    if ($script:Skipped.Count -gt 0) {
        Write-Host ''
        Write-Host '=== Pulado ===' -ForegroundColor Cyan
        $script:Skipped | ForEach-Object { Write-Host ('  [i] ' + $_) -ForegroundColor Gray }
    }

    Write-Host ''
    Write-Host '=== Checklist MANUAL (o script nao consegue fazer/verificar) ===' -ForegroundColor Cyan
    foreach ($m in $script:Manual) { Write-Host ('  [ ] ' + $m) -ForegroundColor White }

    Write-Host ''
    Write-Host '=== Dentro do CS2 ===' -ForegroundColor Cyan
    foreach ($t in (Get-Cs2Tips)) { Write-Host ('  - ' + $t) -ForegroundColor White }

    Write-Host ''
    if ($script:NeedReboot) { Write-Host 'REINICIE O PC para concluir as alteracoes (BCD/MPO/HAGS/mouse).' -ForegroundColor Yellow }
    Write-Host "Backup: $($script:BackupDir)" -ForegroundColor Gray
    Write-Host "Log:    $($script:LogFile)" -ForegroundColor Gray
    Write-Host ''
    Write-Host 'Para reverter: use o botao "Desfazer backup" na interface, ou importe os .reg do backup e rode "bcdedit /import <bcd.bak>".' -ForegroundColor Gray

    Write-Host ''
    try {
        $Host.UI.RawUI.FlushInputBuffer()
        Write-Host 'Pressione QUALQUER TECLA para fechar...' -ForegroundColor Cyan
        [void]$Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown')
    } catch { Read-Host 'ENTER para sair' | Out-Null }
}

# ============================================================================
# ENTRADA
# ============================================================================
if ($script:ModoConsole) {
    Start-ConsoleMode
} else {
    Start-Gui
}

