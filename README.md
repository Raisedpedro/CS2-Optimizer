# CS2 / Windows Optimizer

Diagnóstico e ajustes **seguros e reversíveis** para jogar CS2 com menos travadas no
Windows 10/11. Tem interface gráfica (WinForms), não precisa instalar nada e mostra
**o que está fora do ideal** antes de mexer em qualquer coisa.

> Não é “debloat” nem “tweak mágico”: cada correção diz o que faz, quanto custa e
> como voltar atrás. O que o script não consegue fazer (BIOS, driver, placa de vídeo,
> configuração do jogo) aparece como **checklist manual**.

## Baixar

**[⬇ Última versão (Releases)](https://github.com/Raisedpedro/CS2-Optimizer/releases/latest)**

- `CS2-Optimizer.exe` — programa pronto, só baixar e abrir.
- `CS2-Optimizer-*-fontes.zip` — `.ps1` + `.bat` + ícone, para quem não quer executável.

## Como usar

1. Baixe o `CS2-Optimizer.exe` (ou o `CS2-Optimizer.ps1` + `icone.ico`).
2. Dê **duplo clique**. O programa pede administrador sozinho (UAC) — precisa, porque
   mexe em registro, BCD, serviços e política de energia.
3. Aba **Diagnóstico**: ele lê a máquina e lista o que encontrou. Nada é alterado.
4. Aba **Correções**: marque o que quiser (ou use **Marcar recomendadas**) e clique em
   **Aplicar marcadas**. Antes de aplicar ele pergunta se você quer criar um **ponto de
   restauração** — a escolha é sempre sua, nada é automático.
5. Aba **Checklist**: o que só você pode fazer (BIOS/XMP, driver, CS2, Steam…).

### Sem .exe (só PowerShell)

```powershell
# interface (pede elevação sozinho)
powershell -NoProfile -ExecutionPolicy Bypass -STA -File .\CS2-Optimizer.ps1

# duplo clique no Windows: use o "Abrir CS2-Optimizer.bat"

# modo texto
.\CS2-Optimizer.ps1 -Console

# só relatório, sem alterar nada
.\CS2-Optimizer.ps1 -ReportOnly

# aplica apenas as correções seguras, sem perguntar
.\CS2-Optimizer.ps1 -Console -Auto
```

Requisitos: Windows 10 ou 11, PowerShell 5.1 (já vem no Windows) e uma conta de
administrador.

## O que ele verifica

- **Hardware e sistema**: CPU, RAM (XMP/EXPO), GPU, SSD vs HD, plano de energia.
- **CS2**: onde está instalado, se está em SSD, preferência de GPU, overlays.
- **Windows**: HAGS, MPO, Game DVR/Game Bar, Modo de Jogo, aceleração do mouse,
  MMCSS (prioridade multimídia/rede), VBS/HVCI (só informa, não altera).
- **Telemetria**: serviço DiagTrack, nível de diagnóstico, ID de publicidade,
  histórico de atividades, WER, voz online e tarefas de compatibilidade.
- **Limpeza**: cache de shaders, %TEMP%, DNS, Prefetch, cache da Steam.
- **Reparos**: DISM RestoreHealth, SFC, StartComponentCleanup, CHKDSK.
- **Rede**: reset de Winsock/TCP-IP (marcado como **cuidado**, só sob demanda).

## Segurança e como desfazer

Antes de aplicar qualquer coisa é criado um backup na **mesma pasta do programa**:

```
CS2-Optimizer-Backup-AAAAMMDD-HHMMSS\
    bcd.bak, *.reg, powerscheme.txt
```

- `bcd.bak` — BCD (`bcdedit /import`)
- `*.reg` — chaves de registro que o programa toca (`reg import`)
- `powerscheme.txt` — plano de energia original

O botão **Desfazer backup** lista os backups e restaura tudo (registro, BCD, plano de
energia e preferência de GPU). Dois itens mexem mais fundo — reset de rede e reset das
configs do CS2 — e aparecem marcados como **Cuidado**, em laranja, pedindo confirmação
à parte.

O log da execução (`CS2-Optimizer-Log-*.txt`) fica na mesma pasta do programa.

## Instalar sem .exe (compilar)

```powershell
Install-Module ps2exe -Scope CurrentUser
Invoke-PS2EXE -InputFile .\CS2-Optimizer.ps1 -OutputFile .\CS2-Optimizer.exe `
    -noConsole -STA -requireAdmin -DPIAware -iconFile .\icone.ico `
    -title 'CS2 / Windows Optimizer' -version '1.0.0.0'
```

O `.exe` é gerado com [PS2EXE](https://github.com/MScholtes/PS2EXE), que às vezes é
marcado por antivírus (falso positivo do empacotador). Se preferir, use direto o `.ps1`.

## Aviso

O programa altera configurações do Windows. Ele foi escrito para ser conservador e
tudo o que aplica é reversível pelo backup, mas **use por sua conta e risco**. Não
faça overclock, não promete FPS: mede o que dá para medir e explica o resto.

## Licença

MIT — veja [LICENSE](LICENSE).
