@echo off
rem ============================================================
rem  CS2 / Windows Optimizer - abre a interface grafica
rem  (sem janela preta; pede administrador sozinho)
rem ============================================================
start "" powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -WindowStyle Hidden -File "%~dp0CS2-Optimizer.ps1" %*
