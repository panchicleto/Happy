$ErrorActionPreference = "Stop"
$fp = "NaturalDexSource/NaturalDex.Plugin/NaturalDexForm.cs"
$c = Get-Content $fp -Raw
$c = $c.Replace("NaturalDex v0.9.3 PKHeXth — Strict Validation","NaturalDex v0.9.5 PKHeXth — Gift Cleanup")
$anchor = "private void ClearBoxes_Click"
$ix = $c.IndexOf($anchor)
if ($ix -lt 0) { throw "ClearBoxes_Click not found" }
$method = @'
private void ClearGiftHistory_Click(object? sender, EventArgs e)
{
    if (MessageBox.Show("Se limpiará únicamente el historial de Regalos Misteriosos soportado. No se borrarán cajas ni equipo. ¿Continuar?", "NaturalDex", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
        return;
    if (MessageBox.Show("CONFIRMACIÓN FINAL. Se creará un backup antes de modificar el save.", "NaturalDex", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) != DialogResult.Yes)
        return;
    SaveFile original = _sav.Clone();
    string backup;
    try { backup = SaveBackupManager.Create(_sav); }
    catch (Exception ex)
    {
        MessageBox.Show("No se pudo crear el backup. No se modificó el save.\r\n" + ex.Message, "NaturalDex", MessageBoxButtons.OK, MessageBoxIcon.Error);
        return;
    }
    if (!EventReceiptWriter.TryClearSupportedHistory(_sav, out string result))
    {
        _sav.CopyChangesFrom(original);
        MessageBox.Show(result + "\r\nNo se modificó el save.", "NaturalDex", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return;
    }
    _batch = null;
    MessageBox.Show(result + "\r\n\r\nBackup: " + backup, "NaturalDex", MessageBoxButtons.OK, MessageBoxIcon.Information);
}

'@
$c = $c.Insert($ix,$method)
Set-Content $fp $c -Encoding UTF8

$ep = "NaturalDexSource/NaturalDex.Plugin/EventReceiptWriter.cs"
$e = Get-Content $ep -Raw
$pos = $e.LastIndexOf("}")
if ($pos -lt 0) { throw "EventReceiptWriter closing brace not found" }
$m = @'

    public static bool TryClearSupportedHistory(SaveFile sav, out string result)
    {
        try
        {
            uint key;
            int bytes;
            string label;
            if (sav is SAV8SWSH) { key = 0x112D5141u; bytes = 50 * 0x68; label = "Sword/Shield"; }
            else if (sav is SAV8LA) { key = 0x99E1625Eu; bytes = 50 * 0x278; label = "Legends Arceus"; }
            else if (sav is SAV9SV) { key = 0x99E1625Eu; bytes = 32 * 0x278; label = "Scarlet/Violet"; }
            else if (sav is SAV9ZA) { result = "Legends Z-A no se modifica: layout no verificado."; return false; }
            else { result = "La limpieza segura del historial no está implementada para este formato."; return false; }

            var blocks = sav.GetType().GetProperty("Blocks")?.GetValue(sav);
            if (blocks is null) { result = "No se encontró el accessor de bloques."; return false; }
            object? block = null;
            foreach (var method in blocks.GetType().GetMethods().Where(z => z.Name == "GetBlock"))
            {
                var ps = method.GetParameters();
                if (ps.Length == 1 && ps[0].ParameterType == typeof(uint))
                {
                    block = method.Invoke(blocks, new object[] { key });
                    if (block is not null) break;
                }
            }
            if (block is null) { result = $"No se encontró el bloque 0x{key:X8}."; return false; }
            object? raw = block.GetType().GetProperty("Data")?.GetValue(block);
            if (raw is Memory<byte> mem)
            {
                mem.Span[..Math.Min(bytes, mem.Length)].Clear();
                sav.State.Edited = true;
                result = $"Historial {label} limpiado.";
                return true;
            }
            result = "El bloque no expone datos modificables de forma compatible.";
            return false;
        }
        catch (Exception ex)
        {
            result = "Error limpiando historial: " + ex.Message;
            return false;
        }
    }
'@
$e = $e.Insert($pos,$m)
Set-Content $ep $e -Encoding UTF8
