// language: csharp
using System;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Biletado.Utils
{
    public static class DeletedAtValidator
    {
        // Regex akzeptiert z.B. 2026-01-10T11:15:16Z oder 2026-01-10T11:15:16.081215Z
        private static readonly Regex IsoUtcRegex = new Regex(
            @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?Z$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>
        /// Prüft ob der übergebene String dem erwarteten deleted_at Format entspricht.
        /// </summary>
        public static bool IsValidFormat(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return false;
            if (!IsoUtcRegex.IsMatch(s)) return false;

            // Versuche genaue Prüfung/Parsen (flexible Bruchteile)
            var formats = new[]
            {
                "yyyy-MM-dd'T'HH:mm:ss'Z'",
                "yyyy-MM-dd'T'HH:mm:ss.F'Z'",
                "yyyy-MM-dd'T'HH:mm:ss.FF'Z'",
                "yyyy-MM-dd'T'HH:mm:ss.FFF'Z'",
                "yyyy-MM-dd'T'HH:mm:ss.FFFF'Z'",
                "yyyy-MM-dd'T'HH:mm:ss.FFFFF'Z'",
                "yyyy-MM-dd'T'HH:mm:ss.FFFFFF'Z'",
                "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'"
            };

            return DateTimeOffset.TryParseExact(
                s,
                formats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out _);
        }

        /// <summary>
        /// Hilfsmethode: prüft und liefert Fehler-Nachricht für Controller-Antwort.
        /// </summary>
        public static string? ValidateJsonProperty(JsonElement prop)
        {
            if (prop.ValueKind == JsonValueKind.Null) return null; // Null ist erlaubt (Restore-Fall)
            if (prop.ValueKind != JsonValueKind.String) return "deleted_at must be a string or null.";
            var s = prop.GetString();
            if (!IsValidFormat(s)) return "deleted_at has invalid format. Expected e.g. 2026-01-10T11:15:16.081215Z";
            return null;
        }
    }
}


