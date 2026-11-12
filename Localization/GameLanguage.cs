using System;
using System.Reflection;
using BigWalk.DirectConnect.Common;

namespace BigWalk.DirectConnect.Localization;

/// <summary>
/// Reads the language selected in the game's settings.
/// </summary>
/// <remarks>
/// BIG WALK uses its own localization system (components named <c>LocalizedText</c>) instead of Unity
/// Localization or I2. Rather than hard-coding a class name that may change between game versions, this
/// searches Assembly-CSharp for a localization/language class that exposes a string or enum member such as
/// <c>currentLanguage</c> – either static or on a singleton (<c>instance</c>). The member that was found is logged
/// once; if nothing suitable exists, <see cref="Read"/> returns <c>null</c> and the caller falls back.
/// </remarks>
internal static class GameLanguage
{
    private const BindingFlags Static = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly;
    private const BindingFlags Instance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    /// <summary>Common names of singleton accessors.</summary>
    private static readonly string[] SingletonNames = { "instance", "Instance", "singleton", "Singleton", "I" };

    private static bool _searched;

    /// <summary>The member holding the language, or <c>null</c> if none was found.</summary>
    private static MemberInfo _member;

    /// <summary>Type that declares <see cref="_member"/>.</summary>
    private static Type _owner;

    /// <summary>Name of the singleton accessor for instance members; <c>null</c> for static members.</summary>
    private static string _singletonName;

    /// <summary>Returns the game's current language as reported by the game (e.g. "Polish" or "pl").</summary>
    /// <returns>The raw value, or <c>null</c> if it cannot be determined (yet).</returns>
    public static string Read()
    {
        if (!_searched) Search();
        if (_member == null) return null;

        try
        {
            object target = _singletonName == null ? null : Reflect.GetStatic(_owner, _singletonName);
            if (_singletonName != null && target == null) return null; // singleton not created yet

            object value = _member is PropertyInfo p ? p.GetValue(target) : ((FieldInfo)_member).GetValue(target);
            return value?.ToString();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Scans Assembly-CSharp once and remembers the best-scoring language member.</summary>
    private static void Search()
    {
        _searched = true;
        int bestScore = 0;

        foreach (Type t in Reflect.AllTypes())
        {
            if (t.Assembly.GetName().Name != "Assembly-CSharp") continue;
            if (t.Name.IndexOf("Locali", StringComparison.OrdinalIgnoreCase) < 0 &&
                t.Name.IndexOf("Language", StringComparison.OrdinalIgnoreCase) < 0) continue;

            Consider(t, t.GetProperties(Static), null, ref bestScore);
            Consider(t, t.GetFields(Static), null, ref bestScore);

            string singleton = FindSingleton(t);
            if (singleton != null)
            {
                Consider(t, t.GetProperties(Instance), singleton, ref bestScore);
                Consider(t, t.GetFields(Instance), singleton, ref bestScore);
            }
        }

        Plugin.Logger.LogInfo(_member == null
            ? "Game language setting not found - using the system language (set General.Language in the config to override)."
            : $"Game language read from {_owner.FullName}{(_singletonName != null ? "." + _singletonName : "")}.{_member.Name}");
    }

    /// <summary>
    /// Scores candidate members. Names containing "lang"/"locale" qualify; "current"/"selected"/"active" are
    /// preferred, "default"/"fallback"/"system"/"source" are penalized.
    /// </summary>
    private static void Consider(Type owner, MemberInfo[] members, string singleton, ref int bestScore)
    {
        foreach (MemberInfo m in members)
        {
            Type type = m is PropertyInfo p ? p.PropertyType : (m as FieldInfo)?.FieldType;
            if (type == null || (type != typeof(string) && !type.IsEnum)) continue;

            string n = m.Name.ToLowerInvariant();
            if (!n.Contains("lang") && !n.Contains("locale")) continue;

            int score = 1;
            if (n.Contains("current") || n.Contains("selected") || n.Contains("active")) score += 2;
            if (n.Contains("default") || n.Contains("fallback") || n.Contains("system") || n.Contains("source")) score -= 2;
            if (score <= bestScore) continue;

            bestScore = score;
            _member = m;
            _owner = owner;
            _singletonName = singleton;
        }
    }

    /// <summary>Returns the name of a static member that returns an instance of <paramref name="t"/>, if any.</summary>
    private static string FindSingleton(Type t)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        foreach (string name in SingletonNames)
        {
            var p = t.GetProperty(name, flags);
            if (p != null && t.IsAssignableFrom(p.PropertyType)) return name;

            var f = t.GetField(name, flags);
            if (f != null && t.IsAssignableFrom(f.FieldType)) return name;
        }
        return null;
    }
}
