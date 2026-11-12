using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BigWalk.DirectConnect.Warnings;

/// <summary>
/// Finds the game's warning banners and makes them invisible while hiding is enabled.
/// </summary>
/// <remarks>
/// <para>A banner is recognized by its text: an active TextMeshPro label whose visible text starts with the
/// localized word "WARNING" (e.g. "UWAGA:", "ACHTUNG:"). The banner root is the closest ancestor with a
/// component whose type name contains "Warning" (e.g. the game's <c>WarningDisplay</c>), or else the label's
/// parent (the banner's background and "!" icon).</para>
/// <para>Hiding adds/uses a <see cref="CanvasGroup"/> on the root and sets its alpha to 0. The banner stays
/// active, so the game can show, update and remove it as usual; showing banners again restores the alpha.</para>
/// </remarks>
internal static class WarningHider
{
    /// <summary>Seconds between full scans for new banners (re-applying alpha happens every frame).</summary>
    private const float ScanInterval = 0.25f;

    /// <summary>How many ancestors are searched for a "Warning" component.</summary>
    private const int MaxAncestorDepth = 4;

    /// <summary>The word "WARNING" in the game's languages, upper-case.</summary>
    private static readonly string[] WarningWords =
    {
        "WARNING", "UWAGA", "ACHTUNG", "ATTENTION", "AVERTISSEMENT", "ATENCIÓN", "ADVERTENCIA", "ATTENZIONE",
        "AVVISO", "AVISO", "ATENÇÃO", "ВНИМАНИЕ", "ПРЕДУПРЕЖДЕНИЕ", "УВАГА", "ПОПЕРЕДЖЕННЯ", "VAROVÁNÍ", "POZOR",
        "UYARI", "DİKKAT", "警告", "注意", "경고", "주의",
    };

    /// <summary>Removes TextMeshPro rich-text tags such as <c>&lt;b&gt;</c> or <c>&lt;font=…&gt;</c>.</summary>
    private static readonly Regex RichTextTags = new("<[^>]*>", RegexOptions.Compiled);

    /// <summary>Banners currently hidden by the mod.</summary>
    private static readonly List<HiddenBanner> Banners = new();

    private static float _nextScan;

    /// <summary>Whether hiding is currently enabled.</summary>
    public static bool Hidden { get; private set; }

    /// <summary>Called every frame: toggles hiding when the configured key is pressed.</summary>
    public static void HandleToggleKey()
    {
        if (KeyInput.WasPressed(Plugin.HideWarningsKey.Value)) SetHidden(!Hidden);
    }

    /// <summary>Enables or disables hiding. Enabling scans immediately and logs what was hidden.</summary>
    public static void SetHidden(bool hidden)
    {
        Hidden = hidden;
        if (hidden)
        {
            Scan(logResult: true);
            Plugin.Logger.LogInfo("Warning banners: hidden.");
        }
        else
        {
            RestoreAll();
            Plugin.Logger.LogInfo("Warning banners: visible.");
        }
    }

    /// <summary>
    /// Called every frame (after the game's UI scripts): keeps hidden banners at alpha 0 and periodically
    /// looks for newly shown banners.
    /// </summary>
    public static void Tick()
    {
        if (!Hidden) return;

        if (Time.unscaledTime >= _nextScan)
        {
            _nextScan = Time.unscaledTime + ScanInterval;
            Scan(logResult: false);
        }

        // The game may fade a banner in again by animating its own CanvasGroup; force it back every frame.
        Banners.RemoveAll(b => b.Group == null); // destroyed by the game
        foreach (var banner in Banners)
        {
            banner.Group.alpha = 0f;
            banner.Group.blocksRaycasts = false;
        }
    }

    /// <summary>Hides every banner that is currently on screen.</summary>
    /// <param name="logResult">Log the banners found (or, if none, the visible texts to help identify them).</param>
    private static void Scan(bool logResult)
    {
        int found = 0;
        foreach (TMP_Text label in Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None))
        {
            if (label == null || !IsWarningText(label.text)) continue;

            Transform root = FindBannerRoot(label.transform);
            if (Hide(root) && logResult)
                Plugin.Logger.LogInfo($"Hiding banner '{PathOf(root)}' (text: \"{Plain(label.text)}\").");
            found++;
        }

        if (logResult && found == 0)
            Plugin.Logger.LogInfo("No warning banner is on screen right now; new ones will be hidden as they appear.");
    }

    /// <summary>Returns <c>true</c> if the visible text starts with a localized "WARNING".</summary>
    private static bool IsWarningText(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        string plain = Plain(text).ToUpperInvariant();
        return WarningWords.Any(word => plain.StartsWith(word, StringComparison.Ordinal));
    }

    /// <summary>
    /// Finds the object to hide for a warning label: the closest ancestor carrying a "Warning" component,
    /// otherwise the label's parent.
    /// </summary>
    private static Transform FindBannerRoot(Transform label)
    {
        Transform current = label;
        for (int depth = 0; depth <= MaxAncestorDepth && current != null; depth++)
        {
            foreach (Component c in current.GetComponents<Component>())
                if (c != null && c.GetIl2CppType().Name.IndexOf("Warning", StringComparison.OrdinalIgnoreCase) >= 0)
                    return current;
            current = current.parent;
        }
        return label.parent != null ? label.parent : label;
    }

    /// <summary>Makes <paramref name="root"/> invisible; returns <c>true</c> if it was not hidden before.</summary>
    private static bool Hide(Transform root)
    {
        CanvasGroup group = root.GetComponent<CanvasGroup>();
        bool added = group == null;
        if (added) group = root.gameObject.AddComponent<CanvasGroup>();

        if (Banners.Any(b => b.Group != null && b.Group.Pointer == group.Pointer)) return false;

        Banners.Add(new HiddenBanner(group, group.alpha, group.blocksRaycasts, added));
        group.alpha = 0f;
        group.blocksRaycasts = false;
        return true;
    }

    /// <summary>Shows all hidden banners again; CanvasGroups added by the mod are removed.</summary>
    private static void RestoreAll()
    {
        foreach (var banner in Banners)
        {
            if (banner.Group == null) continue;
            if (banner.AddedByMod)
            {
                Object.Destroy(banner.Group);
            }
            else
            {
                banner.Group.alpha = banner.OriginalAlpha;
                banner.Group.blocksRaycasts = banner.OriginalBlocksRaycasts;
            }
        }
        Banners.Clear();
    }

    /// <summary>Visible text without rich-text tags, collapsed to one line.</summary>
    private static string Plain(string text) => RichTextTags.Replace(text ?? "", "").Replace('\n', ' ').Trim();

    /// <summary>Hierarchy path of a transform, for the log.</summary>
    private static string PathOf(Transform t)
    {
        var sb = new StringBuilder(t.name);
        for (Transform p = t.parent; p != null; p = p.parent) sb.Insert(0, p.name + "/");
        return sb.ToString();
    }

    /// <summary>A banner hidden by the mod, with the state needed to restore it.</summary>
    private sealed class HiddenBanner
    {
        public readonly CanvasGroup Group;
        public readonly float OriginalAlpha;
        public readonly bool OriginalBlocksRaycasts;
        public readonly bool AddedByMod;

        public HiddenBanner(CanvasGroup group, float originalAlpha, bool originalBlocksRaycasts, bool addedByMod)
        {
            Group = group;
            OriginalAlpha = originalAlpha;
            OriginalBlocksRaycasts = originalBlocksRaycasts;
            AddedByMod = addedByMod;
        }
    }
}
