param(
    [switch]$Launch
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$AppDir = Join-Path $Root "app"
$PetsDir = Join-Path $Root "pets"
$GenScript = Join-Path $Root "scripts\generate-default-pets.ps1"
$ExePath = Join-Path $AppDir "AntigravityPet.exe"
$CsPath = Join-Path $AppDir "AntigravityPet.cs"

Write-Host "[1/4] 正在生成 6 套 8x11 像素风宠物图集 (1536x2288)..." -ForegroundColor Cyan
& powershell -NoProfile -ExecutionPolicy Bypass -File $GenScript -OutRoot $PetsDir

Write-Host "[2/4] 检查并关闭旧进程..." -ForegroundColor Cyan
try {
    Invoke-RestMethod -Uri "http://127.0.0.1:47821/control" -Method Post -Body '{"action":"quit","restart":true}' -ContentType "application/json" -TimeoutSec 1 | Out-Null
    Start-Sleep -Milliseconds 600
} catch {}

Write-Host "[3/4] 正在使用 .NET Framework csc.exe 编译 AntigravityPet.exe..." -ForegroundColor Cyan
$fw = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319"
$wpf = "$fw\WPF"
if (-not (Test-Path "$fw\csc.exe")) {
    throw "未找到 .NET Framework csc.exe: $fw\csc.exe"
}

& "$fw\csc.exe" /nologo /target:winexe /optimize+ /utf8output /out:"$ExePath" `
    /r:"$fw\System.dll" `
    /r:"$fw\System.Core.dll" `
    /r:"$fw\System.Drawing.dll" `
    /r:"$fw\System.Windows.Forms.dll" `
    /r:"$fw\System.Web.Extensions.dll" `
    /r:"$fw\System.Xaml.dll" `
    /r:"$wpf\WindowsBase.dll" `
    /r:"$wpf\PresentationCore.dll" `
    /r:"$wpf\PresentationFramework.dll" `
    "$CsPath"

if ($LASTEXITCODE -ne 0) {
    throw "编译失败，退出码: $LASTEXITCODE"
}

Write-Host "[4/4] 正在注册交互式桌面按需计划任务 AntigravityPetLauncher..." -ForegroundColor Cyan
$Action = New-ScheduledTaskAction -Execute $ExePath
$Principal = New-ScheduledTaskPrincipal -UserId $env:USERNAME -LogonType Interactive -RunLevel Limited
Register-ScheduledTask -TaskName "AntigravityPetLauncher" -Action $Action -Principal $Principal -Force | Out-Null

if ($Launch) {
    Write-Host "正在启动桌宠..." -ForegroundColor Green
    schtasks /Run /TN "AntigravityPetLauncher" | Out-Null
}

Write-Host "构建完成: $ExePath" -ForegroundColor Green
