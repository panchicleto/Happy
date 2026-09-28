using System;
using System.Windows.Forms;
using PKHeX.Core;

namespace NaturalDex.Plugin;

internal static class NDXCompatibility
{
    internal static readonly Version TargetCore = new(26, 7, 7, 0);

    internal static Version LoadedCoreVersion => typeof(PKM).Assembly.GetName().Version ?? new Version(0, 0, 0, 0);

    internal static bool IsExactCore => LoadedCoreVersion == TargetCore;

    internal static string Summary =>
        $"PKHeX.Core cargado: {LoadedCoreVersion} | NDX objetivo: {TargetCore} | " +
        (IsExactCore ? "COMPATIBLE EXACTO" : "VERSIÓN DISTINTA");

    internal static void WarnIfNeeded(IWin32Window owner)
    {
        if (IsExactCore)
            return;

        MessageBox.Show(owner,
            "Esta build de NDX fue compilada y validada específicamente para PKHeX.Core 26.7.7.0.\r\n\r\n" +
            $"Core cargado: {LoadedCoreVersion}\r\n\r\n" +
            "Puedes continuar, pero los editores de bloques Switch quedan fuera de la combinación validada.",
            "NDX — Compatibilidad PKHeX",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
    }
}
