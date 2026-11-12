using UnityEngine;

namespace BigWalk.DirectConnect.Networking;

/// <summary>
/// Answers the host's password prompt with the password typed in the "Join via IP" section, so the user does
/// not have to type it a second time.
/// </summary>
/// <remarks>
/// After connecting, the game shows its own password screen (<c>MainMenuManager.passwordMenu</c>). While armed,
/// <see cref="Tick"/> waits for that screen, fills in the password and submits it – once. If the screen does not
/// appear within <see cref="TimeoutSeconds"/>, the stored password is discarded.
/// </remarks>
internal static class PasswordAutoFill
{
    /// <summary>How long to wait for the password screen after starting to connect.</summary>
    private const float TimeoutSeconds = 60f;

    private static string _password = string.Empty;
    private static bool _armed;
    private static float _armedAt;

    /// <summary>Stores the password and starts waiting for the host's prompt.</summary>
    public static void Arm(string password)
    {
        _password = password ?? string.Empty;
        _armed = _password.Length > 0;
        _armedAt = Time.unscaledTime;
    }

    /// <summary>Stops waiting and forgets the stored password.</summary>
    public static void Disarm()
    {
        _armed = false;
        _password = string.Empty;
    }

    /// <summary>Called every frame by <see cref="ModController"/>.</summary>
    public static void Tick()
    {
        if (!_armed) return;
        if (Time.unscaledTime - _armedAt > TimeoutSeconds)
        {
            Disarm();
            return;
        }

        var manager = MainMenuManager.instance;
        if (manager == null) return;

        var menu = manager.passwordMenu;
        if (menu == null || !menu.gameObject.activeInHierarchy) return;
        if (menu.passwordField == null)
        {
            Disarm();
            return;
        }

        menu.passwordField.text = _password;
        Disarm();
        menu.SubmitPassword();
        Plugin.Logger.LogInfo("Submitted the password requested by the host.");
    }
}
