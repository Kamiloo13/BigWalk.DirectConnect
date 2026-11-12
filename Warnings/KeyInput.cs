using System;
using System.Reflection;
using BigWalk.DirectConnect.Common;

namespace BigWalk.DirectConnect.Warnings;

/// <summary>
/// Reads single key presses with whichever input API the game has enabled.
/// </summary>
/// <remarks>
/// Unity games use either the legacy Input Manager (<c>UnityEngine.Input</c>), the Input System package
/// (<c>UnityEngine.InputSystem.Keyboard</c>) or both. Calling the legacy API while it is disabled throws, so both
/// are accessed via reflection and the legacy one is dropped after its first failure.
/// </remarks>
internal static class KeyInput
{
    private static bool _initialized;

    // Legacy Input Manager: Input.GetKeyDown(KeyCode)
    private static MethodInfo _legacyGetKeyDown;
    private static Type _keyCodeType;
    private static bool _legacyFailed;

    // Input System: Keyboard.current.<key>Key.wasPressedThisFrame
    private static PropertyInfo _keyboardCurrent;

    /// <summary>Returns <c>true</c> in the frame in which <paramref name="keyName"/> was pressed.</summary>
    /// <param name="keyName">Unity <c>KeyCode</c> name, e.g. <c>F5</c> (case-insensitive).</param>
    public static bool WasPressed(string keyName)
    {
        if (string.IsNullOrWhiteSpace(keyName)) return false;
        if (!_initialized) Initialize();

        return LegacyPressed(keyName) || InputSystemPressed(keyName);
    }

    /// <summary>Looks up both input APIs once.</summary>
    private static void Initialize()
    {
        _initialized = true;

        Type input = Reflect.FindType("UnityEngine.Input");
        _keyCodeType = Reflect.FindType("UnityEngine.KeyCode");
        if (input != null && _keyCodeType != null)
            _legacyGetKeyDown = input.GetMethod("GetKeyDown", BindingFlags.Public | BindingFlags.Static, null, new[] { _keyCodeType }, null);

        Type keyboard = Reflect.FindType("UnityEngine.InputSystem.Keyboard");
        _keyboardCurrent = keyboard?.GetProperty("current", BindingFlags.Public | BindingFlags.Static);

        Plugin.Logger.LogInfo($"Keyboard input: legacy Input Manager={_legacyGetKeyDown != null}, Input System={_keyboardCurrent != null}.");
    }

    private static bool LegacyPressed(string keyName)
    {
        if (_legacyGetKeyDown == null || _legacyFailed) return false;
        try
        {
            object keyCode = Enum.Parse(_keyCodeType, keyName, true);
            return (bool)_legacyGetKeyDown.Invoke(null, new[] { keyCode });
        }
        catch (ArgumentException)
        {
            return false; // unknown key name in the config
        }
        catch
        {
            _legacyFailed = true; // legacy Input Manager is disabled in this game
            return false;
        }
    }

    private static bool InputSystemPressed(string keyName)
    {
        if (_keyboardCurrent == null) return false;
        try
        {
            object keyboard = _keyboardCurrent.GetValue(null);
            if (keyboard == null) return false;

            // Keyboard exposes one property per key: F5 -> "f5Key", Insert -> "insertKey".
            string propertyName = char.ToLowerInvariant(keyName[0]) + keyName.Substring(1) + "Key";
            object key = keyboard.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)?.GetValue(keyboard);
            return key?.GetType().GetProperty("wasPressedThisFrame")?.GetValue(key) is bool pressed && pressed;
        }
        catch
        {
            return false;
        }
    }
}
