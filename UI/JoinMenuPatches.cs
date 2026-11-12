using System;
using HarmonyLib;

namespace BigWalk.DirectConnect.UI;

/// <summary>
/// Hooks the opening of the Join Game page. Only a postfix on <c>JoinMenu.OnEnable</c> is used – the game's own
/// "Join via code" logic (<c>JoinMenu.Connect</c>) is never patched.
/// </summary>
[HarmonyPatch(typeof(JoinMenu), "OnEnable")]
internal static class JoinMenuOnEnablePatch
{
    /// <summary>Runs after the game's <c>OnEnable</c>; schedules building the "Join via IP" section.</summary>
    /// <param name="__instance">The Join Game page being opened.</param>
    private static void Postfix(JoinMenu __instance)
    {
        try { IpJoinSection.Schedule(__instance); }
        catch (Exception e) { Plugin.Logger.LogError(e); }
    }
}
