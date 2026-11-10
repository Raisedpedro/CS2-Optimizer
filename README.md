# CS2 / Windows Optimizer (porta C# / WPF)

Porta do protótipo `CS2-Optimizer.ps1` (WinForms) para **C# / .NET 8 / WPF**, com tema escuro, cartões, lista agrupada por categoria, escala de DPI e execução como administrador pelo manifesto (sem relançar processo).

> **Sem promessa de FPS.** O programa mede o que dá para medir, explica o que cada item faz e deixa claro quando o ganho é incerto. Meça antes/depois (mapa *CS2 FPS Benchmark* da Oficina) e olhe o **1% low**, não só a média.

## Baixar e usar (portátil)

O Release do GitHub traz um único zip: **`CS2Optimizer-<versão>-portable-win-x64.zip`**.

1. Baixe o zip do Release e **extraia** em uma pasta sua (ex.: `C:\CS2Optimizer`).
   Não rode o `.exe` de dentro do zip — o Windows abriria em pasta temporária.
2. Execute `CS2Optimizer.exe` e aceite o UAC (o programa altera o registro).
3. Use **Reler a máquina** e depois **Aplicar recomendadas**.

Dentro do zip:

```
CS2Optimizer.exe
Backups\        <- backups criados antes de cada alteração
LEIA-ME.txt
LICENSE.txt
```

Tudo o que o programa grava fica **junto do executável**:

| o quê | onde |
| --- | --- |
| backups (`.reg`, `bcd.bak`, `powerscheme.txt`, `journal.json`) | `Backups\CS2-Optimizer-Backup-<data-hora>\` |
| log de cada sessão | `logs\CS2-Optimizer-Log-<data-hora>.txt` |
| tamanho/posição da janela | `janela.json` |

Se a pasta do programa não aceitar escrita (ex.: copiado para
`Arquivos de Programas`), essas pastas passam a ser criadas em
`%LOCALAPPDATA%\CS2-Optimizer` — a aba **Diagnóstico** mostra o caminho usado.
Mover a pasta (ou o zip) leva os backups junto.

> Versões antigas gravavam os backups em `%ProgramData%\CS2-Optimizer\Backups`.
> Na primeira execução desta versão, esses backups são **movidos** para a pasta
> `Backups` ao lado do executável (se a pasta nova ainda estiver vazia).

## Requisitos

- Windows 10 ou Windows 11, x64.
- .NET 8 SDK para compilar.
- .NET 8 Desktop Runtime para a versão dependente do runtime (o instalador verifica/instala).
- Permissão de administrador para aplicar alterações (o manifesto pede UAC).

## Como compilar e executar

```powershell
dotnet build src\CS2Optimizer.App\CS2Optimizer.App.csproj
dotnet run --project src\CS2Optimizer.App\CS2Optimizer.App.csproj
```

A janela abre já elevada pelo UAC. Nada é aplicado sem confirmação.

### CLI

O mesmo executável aceita os modos:

```powershell
CS2Optimizer.exe --report-only
CS2Optimizer.exe --auto
CS2Optimizer.exe --clean-only
CS2Optimizer.exe --revert "C:\CS2Optimizer\Backups\CS2-Optimizer-Backup-AAAAAMMDD-HHMMSS"
CS2Optimizer.exe --help
```

- `--report-only`: só diagnóstico, não altera nada.
- `--auto`: aplica **somente itens `Safe`**, sem perguntar; itens `Risky` nunca entram.
- `--clean-only`: somente `trim`, `temp`, `shaders` e `syscache`.
- `--revert <pasta>`: restaura um backup (mesmo fluxo da aba **Backups / Desfazer**).
- `--no-pause`: não espera ENTER no fim (para scripts).
- `--help`: mostra a ajuda.

Argumentos são validados: opção desconhecida, dois modos ao mesmo tempo ou
`--revert` sem a pasta terminam em ajuda + código `2`, **sem** cair no fluxo que
aplica alterações.

## Segurança e reversibilidade

- Antes de qualquer gravação, o programa cria uma pasta de backup em
  `Backups\CS2-Optimizer-Backup-<data-hora>` (ao lado do `CS2Optimizer.exe`) com:
  - exportação das chaves de registro usadas (`.reg`);
  - `bcd.bak`;
  - `powerscheme.txt`;
  - `journal.json` com o valor **anterior** de cada item alterado.
- O desfazer percorre o journal do mais novo para o mais antigo: regrava o valor antigo ou **apaga o que o programa criou**.
- Um `.reg` só é importado se tiver o cabeçalho correto, nenhuma exclusão de chave (`[-...]`) e apenas chaves que o próprio programa exporta.
- Ponto de restauração do Windows é sempre perguntado (nunca automático). O Windows limita a um ponto a cada 24 horas.
- Itens `Risky` nunca são recomendados, nunca entram em `--auto` e mostram a explicação completa com padrão **Não**.

## Mitigações de CPU (Spectre/Meltdown/MDS...)

Baseado no KB4073119. O programa só mexe em:

- `FeatureSettingsOverride`
- `FeatureSettingsOverrideMask`
- `MinVmVersionForCpuBasedMitigations` (apenas quando o perfil exige e o papel Hyper-V está instalado)

Regras preservadas:

- **Padrão do Windows** = **apagar** as três chaves (não gravar `0`).
- A máscara é sempre `3` quando há sobrescrita.
- Perfis combináveis por **OR**.
- **Não** se mistura perfil que reduz a proteção (`REDUZIDA`) com perfil que aumenta (`MAIOR`).
- Perfis bloqueados por fabricante (Intel/AMD/ARM) não podem ser marcados.
- O efeito só vale depois de reiniciar; o programa nunca reinicia sozinho.

## O&O ShutUp10

A aba **O&O ShutUp10** traz os ajustes do arquivo `OOSU10.reg` traduzidos para
PT-BR e organizados por grupo. Cada item mostra o estado atual lido do
registro, o que será gravado ao ativar e o estado alternativo usado para
reverter.

- Três recomendações: **recomendado**, **opcional** e **evitar**. Itens
  `evitar` reduzem proteção (ex.: SmartScreen do Edge) e exigem confirmação
  reforçada.
- O botão **Selecionar sugeridas p/ CS2** marca o preset conservador: só
  ajustes de privacidade/telemetria que não afetam jogos nem segurança.
- **Aplicar** e **Reverter** usam o mesmo journal/backup das outras abas;
  itens que gravam em HKLM pedem execução como administrador.
- O catálogo é mantido em `src/CS2Optimizer.Core/Oosu10/Oosu10Catalog.cs`
  (não é lido do `.reg` em tempo de execução, porque o arquivo exporta o
  estado da máquina e mistura linhas ativas com comentadas).

## Estrutura

```
src/
  CS2Optimizer.Core/   # modelos, registro/journal, backup, scanner, mitigações,
                       # catálogo de correções, motor, diagnóstico e CLI
  CS2Optimizer.App/    # WPF (MVVM), tema escuro, telas, diálogos, manifesto admin
tests/
  CS2Optimizer.Tests/  # xUnit: journal, mitigações, .reg, catálogo
installer/
  installer.iss        # Inno Setup
scripts/
  publish.ps1          # publica portátil + pasta para o instalador (+ zip)
  package-portable.ps1 # monta o zip: exe + Backups + LEIA-ME
.github/workflows/
  release.yml          # em tag v*: testa, publica e anexa o zip no Release
```

## Distribuição

### Portátil (um arquivo, autocontido)

```powershell
dotnet publish src\CS2Optimizer.App\CS2Optimizer.App.csproj -c Release -r win-x64 `
  --self-contained true -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true -o dist\portable
```

O resultado é `dist\portable\CS2Optimizer.exe`.

Para gerar o zip que vai no Release (exe + `Backups` + `LEIA-ME`):

```powershell
powershell -ExecutionPolicy Bypass -File scripts\package-portable.ps1
```

Saída: `dist\CS2Optimizer-<versão>-portable-win-x64.zip`. O `scripts\publish.ps1`
já chama esse passo (use `-SkipZip` para pular).

### Instalador (Inno Setup)

1. Publique a versão dependente do runtime:

```powershell
dotnet publish src\CS2Optimizer.App\CS2Optimizer.App.csproj -c Release -r win-x64 `
  --self-contained false -o dist\publish
```

2. Compile `installer\installer.iss` com o Inno Setup 6. O instalador:
   - instala em `%LOCALAPPDATA%\Programs\CS2 Optimizer` (pasta do usuário, para
     os backups ficarem ao lado do executável em vez de ir para outro caminho);
   - detecta o **.NET 8 Desktop Runtime** em
     `HKLM\SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App`;
   - oferece instalação automática via `winget install Microsoft.DotNet.DesktopRuntime.8`;
   - cria atalhos e entrada de desinstalação.

Ou use o script único:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\publish.ps1
```

> Para reduzir alertas do SmartScreen/antivírus, assine o executável e o instalador com um certificado de code signing.

## Testes

```powershell
dotnet test tests\CS2Optimizer.Tests\CS2Optimizer.Tests.csproj
```

Cobrem:

- nomes de propriedade do journal (`Path`, `Name`, `Existia`, `Tipo`, `Valor`) e ordem de restauração (mais novo → mais antigo);
- resolução/combinação de mitigações, bloqueio `REDUZIDA` × `MAIOR`, bloqueio por fabricante, `padrao` = apagar, `MinVm` só com Hyper-V;
- decodificação do estado de mitigações e bits;
- validação de `.reg` (cabeçalho, exclusões, whitelist de chaves);
- catálogo: explicação obrigatória, itens recomendados, `power-plan` em desktop/notebook;
- catálogo do O&O ShutUp10: ids únicos, grupos válidos, caminhos HKLM/HKCU, tipos suportados e preset sem itens `evitar`.

## Limitações conhecidas

- Não há garantia de ganho de FPS; vários itens são de fluidez, latência ou apenas limpeza.
- O backup **não** desfaz `net-reset` (Winsock/TCP-IP) nem o `cs2-reset` (pastas `730` renomeadas para `730.old`); esses casos exigem ação manual.
- Mitigações e BCD só passam a valer após reiniciar.
- HVCI/VBS é apenas informativo nesta versão; desligar mexe na Segurança do Windows e não é feito pelo programa.
- O programa foi testado neste ambiente (Windows 11, AMD/NVIDIA). Teste em VM Windows 10/11, Intel e AMD, desktop e notebook antes de distribuir em larga escala.
