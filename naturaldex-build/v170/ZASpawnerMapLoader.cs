using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace NaturalDex.Plugin;

internal sealed record ZASpawnerLocation(
    ulong Hash,
    float X,
    float Y,
    float Z,
    string Map,
    string Location);

internal static class ZASpawnerMapLoader
{
    internal static Dictionary<ulong, ZASpawnerLocation> LoadCsv(string path, out int rejected)
    {
        var result = new Dictionary<ulong, ZASpawnerLocation>();
        rejected = 0;

        foreach (string raw in File.ReadLines(path))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                continue;

            string[] parts = line.Split(',', 6);
            if (parts.Length < 5)
            {
                rejected++;
                continue;
            }

            string hashText = parts[0].Trim();
            if (hashText.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                hashText = hashText[2..];

            bool hashOk = ulong.TryParse(hashText, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong hash);
            bool xOk = float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float x);
            bool yOk = float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float y);
            bool zOk = float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float z);
            if (!hashOk || !xOk || !yOk || !zOk || hash == 0)
            {
                rejected++;
                continue;
            }

            string map = parts[4].Trim();
            string location = parts.Length >= 6 ? parts[5].Trim() : string.Empty;
            result[hash] = new ZASpawnerLocation(hash, x, y, z, map, location);
        }

        return result;
    }

    internal static void WriteTemplate(string path)
    {
        File.WriteAllText(path,
            "# NDX Legends Z-A spawner map\r\n" +
            "# hash,x,y,z,map,location\r\n" +
            "# Hash is hexadecimal, with or without 0x. Coordinates use '.' as decimal separator.\r\n" +
            "0123456789ABCDEF,0.0,0.0,0.0,Lumiose City,Example location\r\n");
    }
}
