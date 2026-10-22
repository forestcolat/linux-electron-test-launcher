# Builds the ready-to-use DualSense package: one self-contained exe (no .NET needed), the art
# folder and two double-click shortcuts.
#
#   powershell -ExecutionPolicy Bypass -File Tools/ReStreetAssetScan/pack.ps1 [-Art private/dualsense-art] [-Output dist/ReStreet-DualSense]
#       [-Originals private/dualsense*.png,private/*.json] [-References private/dualsense-reference/*.png]
#
# -Art adds or replaces art in the package's DualSense folder (for example the Select Sides and
# controls-screen pictures built from your own controller picture). Pictures you did not make
# yourself may not be yours to share; check before giving the package to anyone.
param(
    [string]$Art = "",
    [string]$Output = "dist/ReStreet-DualSense",
    [string[]]$Originals = @(),    # files or wildcards copied to Imagens-originais
    [string[]]$References = @()    # files or wildcards copied to Referencias
)
$ErrorActionPreference = "Stop"
$project = Join-Path $PSScriptRoot "ReStreetAssetScan.csproj"
$out = [IO.Path]::GetFullPath($Output)
# Empty the folder rather than deleting it, so it works while an Explorer window shows it.
if (Test-Path $out) { Get-ChildItem $out -Force | Remove-Item -Recurse -Force }

dotnet publish $project -c Release -r win-x64 --self-contained true -nologo -v q `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:AssemblyName=ReStreetDualSense -o $out
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }
Get-ChildItem $out -Filter *.pdb | Remove-Item

if ($Art -ne "") {
    $artDir = [IO.Path]::GetFullPath($Art)
    if (-not (Test-Path $artDir)) { throw "Art folder not found: $artDir" }
    Copy-Item (Join-Path $artDir "*.png") (Join-Path $out "DualSense") -Force
}

# The report and the tool's source travel with the package.
New-Item -ItemType Directory -Force (Join-Path $out "Relatorio") | Out-Null
Copy-Item (Join-Path $PSScriptRoot "RELATORIO-DUALSENSE.md") (Join-Path $out "Relatorio")
$source = Join-Path $out "Codigo-fonte"
New-Item -ItemType Directory -Force $source | Out-Null
Get-ChildItem $PSScriptRoot -Exclude bin, obj | Copy-Item -Destination $source -Recurse
Copy-Item (Join-Path $PSScriptRoot "..\..\Installer\FifaStreetSetupTool\ThirdParty\LzxDecoder.cs") $source
Copy-Item (Join-Path $PSScriptRoot "..\..\Installer\FifaStreetSetupTool\ThirdParty\MonoGame-LICENSE.txt") $source
# The copied project picks up its own LzxDecoder.cs by default, so drop the link to the installer's.
$csproj = Join-Path $source "ReStreetAssetScan.csproj"
(Get-Content $csproj | Where-Object { $_ -notmatch 'LzxDecoder\.cs' }) | Set-Content $csproj -Encoding utf8

# Optional extras: the pictures the art was made from, and the reference sheets.
foreach ($pair in @(@($Originals, "Imagens-originais"), @($References, "Referencias"))) {
    # "powershell -File" passes a comma list as one string, so split it here.
    $files = @($pair[0] | ForEach-Object { $_ -split ',' } | Where-Object { $_.Trim() -ne "" } | ForEach-Object { Get-ChildItem $_.Trim() -File })
    if ($files.Count -eq 0) { continue }
    $dest = Join-Path $out $pair[1]
    New-Item -ItemType Directory -Force $dest | Out-Null
    $files | Copy-Item -Destination $dest
}

$utf8 = New-Object System.Text.UTF8Encoding($false)
[IO.File]::WriteAllText((Join-Path $out "Instalar DualSense.cmd"), "@echo off`r`nchcp 65001 >nul`r`n`"%~dp0ReStreetDualSense.exe`" install %*`r`n", $utf8)
[IO.File]::WriteAllText((Join-Path $out "Remover DualSense.cmd"), "@echo off`r`nchcp 65001 >nul`r`n`"%~dp0ReStreetDualSense.exe`" uninstall %*`r`n", $utf8)
$readme = @"
ReStreet - botões de PlayStation 5 (DualSense)

INSTALAR
  1. Feche o FIFA Street (o launcher pode ficar aberto).
  2. Dê dois cliques em "Instalar DualSense.cmd".
     A instalação do ReStreet é encontrada sozinha (pelo atalho do launcher).
     Se não for, arraste a pasta do ReStreet (a que tem o FifaStreetLauncher.exe)
     para cima de "Instalar DualSense.cmd".

REMOVER
  Dê dois cliques em "Remover DualSense.cmd". O jogo volta exatamente como era.

ANTES DE ATUALIZAR O RESTREET
  Use "Remover DualSense.cmd", atualize pelo launcher e depois instale de novo.

O QUE MUDA
  Botões dos menus e do HUD, lista e dicas de skill moves, o controle da seleção
  de lados e as telas de controles (loading, Manual do Jogo, configurações).
  Só data0.big, data1.big e seus .bh são alterados; um backup fica ao lado deles
  (*.dualsense-backup) e é o que o "Remover" usa.

Também dá para usar pelo terminal:
  ReStreetDualSense.exe install|uninstall|status [pasta do ReStreet]

NESTA PASTA
  Relatorio\        relatório técnico (o que muda, como e por quê)
  Codigo-fonte\     código da ferramenta (no projeto: Tools\ReStreetAssetScan)
  DualSense\        a arte que o instalador aplica
  Imagens-originais\, Referencias\   (quando incluídas) as imagens de origem e as folhas de referência
"@
[IO.File]::WriteAllText((Join-Path $out "LEIA-ME.txt"), $readme.Replace("`n", "`r`n"), $utf8)

Write-Host "Package ready: $out"
Get-ChildItem $out | Format-Table Name, Length -AutoSize
