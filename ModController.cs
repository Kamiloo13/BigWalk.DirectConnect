using System;
using BigWalk.DirectConnect.Networking;
using BigWalk.DirectConnect.Tuning;
using BigWalk.DirectConnect.UI;
using BigWalk.DirectConnect.Warnings;
using UnityEngine;

namespace BigWalk.DirectConnect;

/// <summary>
/// Single per-frame driver for every part of the mod. It lives on a <c>DontDestroyOnLoad</c> object, so it
/// keeps running across scene changes (main menu ⇄ world).
/// </summary>
public sealed class ModController : MonoBehaviour
{
    /// <summary>How often (in seconds) the send rate is re-evaluated.</summary>
    private const float TuningInterval = 0.5f;

    /// <summary>Unscaled time at which the send rate is evaluated next.</summary>
    private float _nextTuning;

    /// <summary>Constructor required by Il2CppInterop for types injected into IL2CPP.</summary>
    /// <param name="pointer">Pointer to the native IL2CPP object.</param>
    public ModController(IntPtr pointer) : base(pointer) { }

    /// <summary>Unity callback, called once per frame.</summary>
    private void Update()
    {
        IpJoinSection.Tick();     // builds the "Join via IP" section once the menu is laid out
        AddressResolver.Tick();   // delivers finished DNS lookups
        PasswordAutoFill.Tick();  // answers the host's password prompt

        try { WarningHider.HandleToggleKey(); } // shows/hides the game's warning banners
        catch (Exception e) { Plugin.Logger.LogError($"Warning toggle failed: {e.Message}"); }

        if (Time.unscaledTime < _nextTuning) return;
        _nextTuning = Time.unscaledTime + TuningInterval;

        try { SendRateTuner.Evaluate(); }
        catch (Exception e) { Plugin.Logger.LogError($"Send rate evaluation failed: {e.Message}"); }
    }

    /// <summary>
    /// Unity callback, called after all <c>Update</c> calls. Runs after the game's own UI scripts, so a warning
    /// banner that the game fades in again is hidden before the frame is rendered.
    /// </summary>
    private void LateUpdate()
    {
        try { WarningHider.Tick(); }
        catch (Exception e) { Plugin.Logger.LogError($"Hiding warnings failed: {e.Message}"); }
    }
}
