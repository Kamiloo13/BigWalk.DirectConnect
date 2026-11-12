using System;

namespace BigWalk.DirectConnect.Networking;

/// <summary>Connects to a host directly over the game's KCP transport.</summary>
internal static class DirectJoin
{
    /// <summary>UDP port of the game's KCP transport.</summary>
    public const int Port = 7777;

    /// <summary>
    /// Starts connecting with the game's own code path (<c>NetworkMinder.SetTransportAndConnect</c>), which
    /// switches to the KCP transport (dual-mode UDP socket, IPv4 + IPv6) and connects to port 7777.
    /// </summary>
    /// <param name="menu">The Join Game page; switched to the "connecting" screen.</param>
    /// <param name="address">IPv4 or IPv6 address (host names are resolved beforehand by <see cref="AddressResolver"/>).</param>
    /// <param name="password">Password submitted automatically when the host asks for it.</param>
    public static void Join(JoinMenu menu, string address, string password)
    {
        try
        {
            PasswordAutoFill.Arm(password);
            menu.GoToConnectingMenu();
            NetworkMinder.SetTransportAndConnect(address);

            string endpoint = address.Contains(':') ? $"[{address}]:{Port}" : $"{address}:{Port}";
            Plugin.Logger.LogInfo($"Connecting directly to {endpoint}.");
        }
        catch (Exception e)
        {
            PasswordAutoFill.Disarm();
            Plugin.Logger.LogError($"Join via IP failed: {e}");
        }
    }
}
