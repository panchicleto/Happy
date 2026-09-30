$ErrorActionPreference = "Stop"

$src = "NaturalDexSource/NaturalDex.Plugin"
if (!(Test-Path $src)) { throw "NaturalDex source tree not found." }

Copy-Item "naturaldex-build/v185/NaturalDexForm.Events.cs" "$src/NaturalDexForm.Events.cs" -Force
Copy-Item "naturaldex-build/v185/WonderCardHistoryManagerForm.cs" "$src/WonderCardHistoryManagerForm.cs" -Force

Get-ChildItem $src -Filter *.cs | ForEach-Object {
    $c = Get-Content $_.FullName -Raw
    $c = $c.Replace("NDX Tools v1.8.4", "NDX Tools v1.8.5")
    $c = $c.Replace("NDX-Tools/1.8.4", "NDX-Tools/1.8.5")
    $c = $c.Replace("NDX Tools 1.8.4", "NDX Tools 1.8.5")
    $c = $c.Replace("NDX Switch Tools v1.8.4", "NDX Switch Tools v1.8.5")
    Set-Content $_.FullName $c -Encoding UTF8
}

$projPath = "NaturalDexSource/NaturalDex.Plugin.v0.5.0.csproj"
$proj = Get-Content $projPath -Raw
$proj = [regex]::Replace($proj, '<Version>[^<]+</Version>', '<Version>1.8.5</Version>', 1)
$proj = [regex]::Replace($proj, '<FileVersion>[^<]+</FileVersion>', '<FileVersion>1.8.5.0</FileVersion>', 1)
$proj = [regex]::Replace($proj, '<AssemblyVersion>[^<]+</AssemblyVersion>', '<AssemblyVersion>1.8.5.0</AssemblyVersion>', 1)
$proj = [regex]::Replace($proj, '<InformationalVersion>[^<]+</InformationalVersion>', '<InformationalVersion>1.8.5</InformationalVersion>', 1)
Set-Content $projPath $proj -Encoding UTF8

$events = Get-Content "$src/NaturalDexForm.Events.cs" -Raw
$mgr = Get-Content "$src/WonderCardHistoryManagerForm.cs" -Raw

if ($events -notmatch 'ADMINISTRAR / BORRAR HISTORIAL DEL SAVE') { throw "v1.8.5 verification: history manager button missing." }
if ($events -notmatch 'ManageWonderCardHistoryClick') { throw "v1.8.5 verification: history manager handler missing." }
if ($mgr -notmatch 'BORRAR TODO EL HISTORIAL') { throw "v1.8.5 verification: clear-all action missing." }
if ($mgr -notmatch '0x112D5141') { throw "v1.8.5 verification: SWSH Mystery Gift block missing." }
if ($mgr -notmatch '0x99E1625E') { throw "v1.8.5 verification: PLA/SV Mystery Gift block missing." }
if ($mgr -notmatch 'MysteryBlock8b') { throw "v1.8.5 verification: BDSP structured history support missing." }
if ($mgr -notmatch 'SAV9ZA') { throw "v1.8.5 verification: Z-A safety gate missing." }
if ($mgr -notmatch 'SaveBackupManager.Create') { throw "v1.8.5 verification: automatic save backup missing." }
if ($mgr -notmatch 'Las recompensas existentes no fueron eliminadas') { throw "v1.8.5 verification: reward-preservation disclosure missing." }

Write-Host "NDX Tools v1.8.5 Wonder Card history manager applied."
