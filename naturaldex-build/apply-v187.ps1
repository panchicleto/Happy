$ErrorActionPreference = "Stop"

$src = "NaturalDexSource/NaturalDex.Plugin"
if (!(Test-Path $src)) { throw "NaturalDex source tree not found." }

# Replace only the two event-specific components being extended.
Copy-Item "naturaldex-build/v187/WonderCardSelectionForm.cs" "$src/WonderCardSelectionForm.cs" -Force
Copy-Item "naturaldex-build/v187/WonderCardInjectionService.cs" "$src/WonderCardInjectionService.cs" -Force

$eventsPath = "$src/NaturalDexForm.Events.cs"
$events = Get-Content $eventsPath -Raw

# Move live-save resolution before the selector and pass it in so the selector can
# show only cards belonging to the currently opened game.
$old = @'
        using var selector = new WonderCardSelectionForm(entries);
        if (selector.ShowDialog(this) != DialogResult.OK)
            return;

        var selected = selector.SelectedEntries;
        if (selected.Count == 0)
            return;

        SaveFile live = _provider.SAV;
'@
$new = @'
        SaveFile live = _provider.SAV;
        using var selector = new WonderCardSelectionForm(entries, live);
        if (selector.ShowDialog(this) != DialogResult.OK)
            return;

        var selected = selector.SelectedEntries;
        if (selected.Count == 0)
            return;

        bool historyOnly = selector.HistoryOnly;
'@
if (-not $events.Contains($old)) { throw "v1.8.7: selector block not found." }
$events = $events.Replace($old, $new)

# Switch preflight depending on the action selected in the selector.
$old = @'
            bool strict = _strictValidation.Checked;
            WonderInjectionResult preflight = await Task.Run(
                () => WonderCardInjectionService.Preflight(live, selected, strict),
                token);
'@
$new = @'
            bool strict = _strictValidation.Checked;
            WonderInjectionResult preflight = await Task.Run(
                () => historyOnly
                    ? WonderCardInjectionService.PreflightHistoryOnly(live, selected)
                    : WonderCardInjectionService.Preflight(live, selected, strict),
                token);
'@
if (-not $events.Contains($old)) { throw "v1.8.7: preflight block not found." }
$events = $events.Replace($old, $new)

# Make confirmation explicit so receipt-only can never be confused with reward injection.
$old = @'
            string summary =
                $"Fuente: {sourceLabel}\r\n" +
                $"Eventos verificados: {selected.Count}\r\n\r\n" +
                $"Pokémon legales: {preflight.PokemonAdded}\r\n" +
                $"Recompensas aplicables: {preflight.RewardsApplied}\r\n" +
                $"Registros de Mystery Gift: {preflight.ReceiptsWritten}\r\n\r\n" +
                "Los eventos históricos transferidos no crean una Wonder Card falsa del juego actual.\r\n" +
                "¿Aplicar este preflight al save abierto?";
'@
$new = @'
            string modeText = historyOnly
                ? "SOLO HISTORIAL — no se entregarán Pokémon, objetos, ropa, puntos ni dinero."
                : "RECOMPENSA + HISTORIAL";

            string summary =
                $"Fuente: {sourceLabel}\r\n" +
                $"Modo: {modeText}\r\n" +
                $"Wonder Cards verificadas: {selected.Count}\r\n\r\n" +
                $"Pokémon a añadir: {preflight.PokemonAdded}\r\n" +
                $"Recompensas a aplicar: {preflight.RewardsApplied}\r\n" +
                $"Registros de Mystery Gift: {preflight.ReceiptsWritten}\r\n\r\n" +
                (historyOnly
                    ? "Solo se modificará el historial/recibo de Mystery Gift.\r\n"
                    : "Las recompensas y el historial se aplicarán según corresponda.\r\n") +
                "¿Aplicar este preflight al save abierto?";
'@
if (-not $events.Contains($old)) { throw "v1.8.7: confirmation block not found." }
$events = $events.Replace($old, $new)

# Use receipt-only audit when appropriate.
$old = @'
            if (!WonderCardInjectionService.AuditCommitted(live, selected, out string auditReason))
                throw new InvalidOperationException("Auditoría de legalidad posterior al commit: " + auditReason);
'@
$new = @'
            bool auditOk = historyOnly
                ? WonderCardInjectionService.AuditHistoryOnly(live, selected, out string auditReason)
                : WonderCardInjectionService.AuditCommitted(live, selected, out auditReason);

            if (!auditOk)
                throw new InvalidOperationException("Auditoría posterior al commit: " + auditReason);
'@
if (-not $events.Contains($old)) { throw "v1.8.7: audit block not found." }
$events = $events.Replace($old, $new)

# Improve final status / dialog wording.
$old = @'
            _eventStatus.Text =
                $"Eventos aplicados: {selected.Count}. Pokémon {preflight.PokemonAdded}, " +
                $"recompensas {preflight.RewardsApplied}, registros {preflight.ReceiptsWritten}.";
'@
$new = @'
            _eventStatus.Text = historyOnly
                ? $"Historial actualizado: {preflight.ReceiptsWritten} Wonder Card(s); recompensas no modificadas."
                : $"Eventos aplicados: {selected.Count}. Pokémon {preflight.PokemonAdded}, " +
                  $"recompensas {preflight.RewardsApplied}, registros {preflight.ReceiptsWritten}.";
'@
if (-not $events.Contains($old)) { throw "v1.8.7: status block not found." }
$events = $events.Replace($old, $new)

$old = @'
            MessageBox.Show(this,
                "Eventos aplicados correctamente.\r\n\r\n" +
                $"Backup: {backupPath}\r\n" +
                "Auditoría final: OK.",
                "NaturalDex — eventos",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
'@
$new = @'
            MessageBox.Show(this,
                (historyOnly
                    ? "Wonder Cards registradas SOLO en el historial.\r\nLas recompensas existentes no fueron modificadas.\r\n\r\n"
                    : "Eventos aplicados correctamente.\r\n\r\n") +
                $"Backup: {backupPath}\r\n" +
                "Auditoría final: OK.",
                "NDX — eventos",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
'@
if (-not $events.Contains($old)) { throw "v1.8.7: final dialog block not found." }
$events = $events.Replace($old, $new)

Set-Content $eventsPath $events -Encoding UTF8

# Promote version branding.
Get-ChildItem $src -Filter *.cs | ForEach-Object {
    $c = Get-Content $_.FullName -Raw
    $c = $c.Replace("NDX Tools v1.8.6", "NDX Tools v1.8.7")
    $c = $c.Replace("NDX-Tools/1.8.6", "NDX-Tools/1.8.7")
    $c = $c.Replace("NDX Tools 1.8.6", "NDX Tools 1.8.7")
    $c = $c.Replace("NDX Switch Tools v1.8.6", "NDX Switch Tools v1.8.7")
    Set-Content $_.FullName $c -Encoding UTF8
}

$projPath = "NaturalDexSource/NaturalDex.Plugin.v0.5.0.csproj"
$proj = Get-Content $projPath -Raw
$proj = [regex]::Replace($proj, '<Version>[^<]+</Version>', '<Version>1.8.7</Version>', 1)
$proj = [regex]::Replace($proj, '<FileVersion>[^<]+</FileVersion>', '<FileVersion>1.8.7.0</FileVersion>', 1)
$proj = [regex]::Replace($proj, '<AssemblyVersion>[^<]+</AssemblyVersion>', '<AssemblyVersion>1.8.7.0</AssemblyVersion>', 1)
$proj = [regex]::Replace($proj, '<InformationalVersion>[^<]+</InformationalVersion>', '<InformationalVersion>1.8.7</InformationalVersion>', 1)
Set-Content $projPath $proj -Encoding UTF8

# Build-time regression / feature guards.
$events = Get-Content $eventsPath -Raw
$selector = Get-Content "$src/WonderCardSelectionForm.cs" -Raw
$injector = Get-Content "$src/WonderCardInjectionService.cs" -Raw
$mgr = Get-Content "$src/WonderCardHistoryManagerForm.cs" -Raw
$viewer = Get-Content "$src/SwShLeagueCardGameViewForm.cs" -Raw

if ($selector -notmatch 'WonderCardSelectionForm\(IReadOnlyList<WonderCardEntry> entries, SaveFile sav\)') { throw "v1.8.7: current-game selector constructor missing." }
if ($selector -notmatch 'IsCardForCurrentGame') { throw "v1.8.7: current-game filtering missing." }
if ($selector -notmatch 'REGISTRAR SOLO EN HISTORIAL') { throw "v1.8.7: receipt-only button missing." }
if ($selector -notmatch 'RECOMPENSA \+ HISTORIAL') { throw "v1.8.7: full injection button missing." }
if ($injector -notmatch 'PreflightHistoryOnly') { throw "v1.8.7: history-only preflight missing." }
if ($injector -notmatch 'AuditHistoryOnly') { throw "v1.8.7: history-only audit missing." }
if ($events -notmatch 'historyOnly = selector\.HistoryOnly') { throw "v1.8.7: event mode wiring missing." }
if ($events -notmatch 'PreflightHistoryOnly') { throw "v1.8.7: receipt-only flow not wired." }

# Previous functionality must still exist.
if ($events -notmatch 'InitializeNdxSuiteTabs\(\)') { throw "v1.8.7 regression: suite tabs lost." }
if ($events -notmatch 'InitializeV150Tabs\(\)') { throw "v1.8.7 regression: Switch Tools lost." }
if ($events -notmatch 'ADMINISTRAR / BORRAR HISTORIAL DEL SAVE') { throw "v1.8.7 regression: history cleaner lost." }
if ($mgr -notmatch 'BORRAR TODO EL HISTORIAL') { throw "v1.8.7 regression: history manager lost." }
if ($viewer -notmatch 'VOLTEAR TARJETA') { throw "v1.8.7 regression: Game Style League Card viewer lost." }

Write-Host "NDX Tools v1.8.7 current-game Wonder Card filter + history-only registration applied."
