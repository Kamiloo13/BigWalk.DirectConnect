// Direct Connect for BIG WALK.
// Based on "N2N Direct Connect" by lajilaji (Thunderstore: lajilaji/N2NDirectConnect).

using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using BigWalk.DirectConnect.Tuning;
using BigWalk.DirectConnect.Warnings;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;

namespace BigWalk.DirectConnect;

/// <summary>
/// BepInEx entry point of the Direct Connect mod.
/// </summary>
/// <remarks>
/// <para>The mod has three features:</para>
/// <list type="bullet">
///   <item><description>
///     <b>Join via IP</b> – adds a "Join via IP" section (IP address or host name + password) to the Join Game page,
///     between "Join via code" and "Join via friends list". The game's own join options are untouched.
///   </description></item>
///   <item><description>
///     <b>Network tuning</b> for direct (IP/KCP) sessions – raises Mirror's send rate and flushes KCP
///     every frame to reduce latency. See <see cref="SendRateTuner"/> and <see cref="KcpFlusher"/>.
///   </description></item>
///   <item><description>
///     <b>Hide warnings</b> – a key (default F5) hides/shows the game's red "WARNING: …" banners, e.g. "No network
///     connection", which refer to Epic Online Services and not to direct sessions. See <see cref="WarningHider"/>.
///   </description></item>
/// </list>
/// </remarks>
[BepInPlugin(Guid, Name, Version)]
public sealed class Plugin : BasePlugin
{
    /// <summary>Unique plugin identifier; also used as the Harmony id and the config file name.</summary>
    public const string Guid = "kamiloo13.bigwalk.direct-connect";

    /// <summary>Human-readable plugin name shown in the BepInEx log.</summary>
    public const string Name = "Direct Connect";

    /// <summary>Plugin version (keep in sync with the project's &lt;Version&gt;).</summary>
    public const string Version = "1.0.0";

    /// <summary>Shared logger for all parts of the mod.</summary>
    internal static ManualLogSource Logger;

    /// <summary>Language of the mod's UI texts ("auto" or a language code).</summary>
    internal static ConfigEntry<string> Language;

    /// <summary>When the send rate should be raised.</summary>
    internal static ConfigEntry<TuningMode> Mode;

    /// <summary>Target Mirror send rate in Hz for direct sessions.</summary>
    internal static ConfigEntry<int> SendRate;

    /// <summary>Whether KCP reliable data is flushed every frame.</summary>
    internal static ConfigEntry<bool> FlushKcp;

    /// <summary>Key that hides/shows the game's warning banners (Unity <c>KeyCode</c> name).</summary>
    internal static ConfigEntry<string> HideWarningsKey;

    /// <summary>Whether warning banners are hidden right after the game starts.</summary>
    internal static ConfigEntry<bool> HideWarningsOnStart;

    /// <summary>
    /// Called by BepInEx once the plugin is loaded: binds the configuration, creates the per-frame
    /// controller and installs the Harmony patches.
    /// </summary>
    public override void Load()
    {
        Logger = Log;
        BindConfig();

        // Custom MonoBehaviours must be registered with IL2CPP before they can be added to a GameObject.
        ClassInjector.RegisterTypeInIl2Cpp<ModController>();
        var controller = new GameObject("DirectConnectController");
        Object.DontDestroyOnLoad(controller);
        controller.AddComponent<ModController>();

        var harmony = new Harmony(Guid);
        harmony.PatchAll();          // Join Game page (attribute-based patches)
        KcpFlusher.Install(harmony); // KCP transport (target resolved via reflection)

        if (HideWarningsOnStart.Value) WarningHider.SetHidden(true);

        Logger.LogInfo($"Loaded. Mode={Mode.Value}, SendRate={SendRate.Value} Hz, FlushKcp={FlushKcp.Value}, " +
                       $"hide warnings: {HideWarningsKey.Value}.");
    }

    /// <summary>Creates (or reads) all entries of <c>BepInEx\config\kamiloo13.bigwalk.direct-connect.cfg</c>.</summary>
    private void BindConfig()
    {
        Language = Config.Bind("General", "Language", "auto",
            "Language of the mod's texts. 'auto' follows the game's language (falls back to the system language). " +
            "Or force one of: " + string.Join(", ", Localization.Translations.Codes) + ".");

        Mode = Config.Bind("Network", "SendRateMode", TuningMode.DirectIpOnly,
            "When to raise the send rate. DirectIpOnly = only when connected by IP (client) / when every remote " +
            "player is connected by IP (host). Always = every session (players without the mod may see jittery " +
            "movement). Off = never.");

        SendRate = Config.Bind("Network", "SendRate", 60,
            new ConfigDescription("Mirror send rate in Hz (game default: 30). All players should use the same value.",
                new AcceptableValueRange<int>(30, 120)));

        FlushKcp = Config.Bind("Network", "FlushKcpEveryFrame", true,
            "Flush KCP reliable data every frame instead of every 10 ms. Works without the mod on the other side.");

        HideWarningsKey = Config.Bind("Warnings", "ToggleKey", "F5",
            "Key that shows/hides the game's warning banners, e.g. 'No network connection' (Unity KeyCode name: F5, F6, Insert, ...).");

        HideWarningsOnStart = Config.Bind("Warnings", "HiddenOnStart", false,
            "Hide warning banners from the start, without pressing the key first.");
    }
}
