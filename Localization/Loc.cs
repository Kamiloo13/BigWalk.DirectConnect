using System;
using System.Collections.Generic;
using UnityEngine;

namespace BigWalk.DirectConnect.Localization;

/// <summary>
/// Selects the language of the mod's texts. Candidates are tried in order – config override, game language,
/// system language – and the first one that has a translation wins; English is the final fallback.
/// </summary>
internal static class Loc
{
    /// <summary>Texts of the currently selected language.</summary>
    public static LocStrings Current { get; private set; } = English();

    /// <summary>Code of the currently selected language; used to log only actual changes.</summary>
    private static string _currentCode;

    /// <summary>
    /// Re-evaluates the language. Called every time the Join Game page opens, so a language change in the
    /// game's settings is picked up without restarting.
    /// </summary>
    public static void Refresh()
    {
        foreach (var (source, raw) in Candidates())
        {
            string code = LanguageCodes.Normalize(raw);
            if (code == null || !Translations.TryGet(code, out var strings)) continue;

            Current = strings;
            if (code != _currentCode)
            {
                _currentCode = code;
                Plugin.Logger.LogInfo($"Language: '{raw}' (from {source}) -> '{code}'.");
            }
            return;
        }

        Current = English();
    }

    /// <summary>Yields the raw language values to try, in priority order.</summary>
    private static IEnumerable<(string source, string raw)> Candidates()
    {
        string forced = Plugin.Language.Value;
        if (!string.IsNullOrWhiteSpace(forced) && !forced.Equals("auto", StringComparison.OrdinalIgnoreCase))
            yield return ("config", forced);

        string game = GameLanguage.Read();
        if (game != null) yield return ("game", game);

        yield return ("system", LanguageCodes.FromSystemLanguage(Application.systemLanguage));
    }

    private static LocStrings English()
    {
        Translations.TryGet("en", out var en);
        return en;
    }
}
