using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BigWalk.DirectConnect.Localization;

/// <summary>
/// Converts the different ways a language can be reported ("pl", "pl-PL", "Polish", "ChineseSimplified", …)
/// into the codes used by <see cref="Translations"/>.
/// </summary>
internal static class LanguageCodes
{
    /// <summary>English language names (letters only, lower-case) mapped to codes.</summary>
    private static readonly Dictionary<string, string> Names = new()
    {
        ["english"] = "en", ["polish"] = "pl", ["german"] = "de", ["french"] = "fr", ["spanish"] = "es",
        ["italian"] = "it", ["portuguese"] = "pt", ["brazilianportuguese"] = "pt", ["portuguesebrazil"] = "pt",
        ["russian"] = "ru", ["ukrainian"] = "uk", ["czech"] = "cs", ["turkish"] = "tr", ["japanese"] = "ja",
        ["korean"] = "ko", ["chinese"] = "zh-Hans", ["chinesesimplified"] = "zh-Hans", ["schinese"] = "zh-Hans",
        ["chinesetraditional"] = "zh-Hant", ["tchinese"] = "zh-Hant",
    };

    /// <summary>Normalizes a language name or locale code.</summary>
    /// <param name="raw">Value reported by the config, the game or the OS.</param>
    /// <returns>A code such as <c>pl</c> or <c>zh-Hant</c>, or <c>null</c> for empty input.
    /// The result is not guaranteed to have a translation.</returns>
    public static string Normalize(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        // Language names, e.g. "Polish" or "Chinese_Simplified".
        string lettersOnly = new string(raw.Where(char.IsLetter).ToArray()).ToLowerInvariant();
        if (Names.TryGetValue(lettersOnly, out string byName)) return byName;

        // Locale codes, e.g. "pl", "pl_PL", "zh-TW".
        string code = raw.Trim().Replace('_', '-').ToLowerInvariant();
        if (code.StartsWith("zh"))
            return code.Contains("hant") || code.Contains("tw") || code.Contains("hk") || code.Contains("mo") ? "zh-Hant" : "zh-Hans";

        int dash = code.IndexOf('-');
        return dash > 0 ? code[..dash] : code;
    }

    /// <summary>Maps Unity's <see cref="SystemLanguage"/> to a code (unsupported languages → English).</summary>
    public static string FromSystemLanguage(SystemLanguage lang) => lang switch
    {
        SystemLanguage.Polish => "pl",
        SystemLanguage.German => "de",
        SystemLanguage.French => "fr",
        SystemLanguage.Spanish => "es",
        SystemLanguage.Italian => "it",
        SystemLanguage.Portuguese => "pt",
        SystemLanguage.Russian => "ru",
        SystemLanguage.Ukrainian => "uk",
        SystemLanguage.Czech => "cs",
        SystemLanguage.Turkish => "tr",
        SystemLanguage.Japanese => "ja",
        SystemLanguage.Korean => "ko",
        SystemLanguage.ChineseTraditional => "zh-Hant",
        SystemLanguage.Chinese or SystemLanguage.ChineseSimplified => "zh-Hans",
        _ => "en",
    };
}
