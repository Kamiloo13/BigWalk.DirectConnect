using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine;

namespace BigWalk.DirectConnect.Networking;

/// <summary>What kind of address the user typed.</summary>
internal enum AddressKind
{
    /// <summary>Neither an IP address nor a valid host name.</summary>
    Invalid,

    /// <summary>An IPv4 (dotted quad) or IPv6 address – can be used directly.</summary>
    IpAddress,

    /// <summary>A host name (e.g. <c>gra.twoja-domena.pl</c>, <c>localhost</c>) that must be resolved via DNS.</summary>
    HostName,
}

/// <summary>
/// Classifies typed addresses and resolves host names via DNS without blocking the game.
/// </summary>
/// <remarks>
/// Resolution runs on the .NET thread pool (<see cref="Dns.GetHostAddressesAsync(string)"/>); the result is
/// picked up on the main thread in <see cref="Tick"/>, because Unity objects may only be touched there.
/// Host names are resolved by the mod (rather than by the game) so that an unknown name can be reported in
/// the address field instead of leaving the player on the "connecting" screen.
/// </remarks>
internal static class AddressResolver
{
    /// <summary>Resolution attempts taking longer than this are treated as failed.</summary>
    private const float TimeoutSeconds = 10f;

    private static Task<IPAddress[]> _pending;
    private static string _pendingHost;
    private static float _startedAt;
    private static Action<string> _onResolved;
    private static Action _onFailed;

    /// <summary><c>true</c> while a DNS lookup is in progress (further join attempts are ignored).</summary>
    public static bool Busy => _pending != null;

    /// <summary>Validates and normalizes the text typed into the address field.</summary>
    /// <param name="input">Raw text, e.g. <c>192.168.1.10</c>, <c>[2001:db8::1]</c> or <c>gra.twoja-domena.pl</c>.</param>
    /// <param name="address">Trimmed address (IPv6 brackets removed), ready for connecting or resolving.</param>
    public static AddressKind Classify(string input, out string address)
    {
        address = (input ?? string.Empty).Trim().TrimStart('[').TrimEnd(']').TrimEnd('.');
        if (address.Length == 0) return AddressKind.Invalid;

        if (IPAddress.TryParse(address, out var ip))
        {
            return ip.AddressFamily switch
            {
                // IPAddress.TryParse also accepts shorthand such as "10" or "10.1"; require all four octets.
                AddressFamily.InterNetwork => address.Split('.').Length == 4 ? AddressKind.IpAddress : AddressKind.Invalid,
                AddressFamily.InterNetworkV6 => AddressKind.IpAddress,
                _ => AddressKind.Invalid,
            };
        }

        return Uri.CheckHostName(address) == UriHostNameType.Dns ? AddressKind.HostName : AddressKind.Invalid;
    }

    /// <summary>Starts resolving <paramref name="host"/> in the background.</summary>
    /// <param name="host">Host name classified as <see cref="AddressKind.HostName"/>.</param>
    /// <param name="onResolved">Called on the main thread with the chosen IP address.</param>
    /// <param name="onFailed">Called on the main thread if the name cannot be resolved.</param>
    public static void Resolve(string host, Action<string> onResolved, Action onFailed)
    {
        _pendingHost = host;
        _onResolved = onResolved;
        _onFailed = onFailed;
        _startedAt = Time.unscaledTime;
        _pending = Dns.GetHostAddressesAsync(host);
        Plugin.Logger.LogInfo($"Resolving {host}...");
    }

    /// <summary>Called every frame by <see cref="ModController"/>; delivers the result of a finished lookup.</summary>
    public static void Tick()
    {
        if (_pending == null) return;

        bool timedOut = Time.unscaledTime - _startedAt > TimeoutSeconds;
        if (!_pending.IsCompleted && !timedOut) return;

        Task<IPAddress[]> task = _pending;
        Action<string> onResolved = _onResolved;
        Action onFailed = _onFailed;
        _pending = null;
        _onResolved = null;
        _onFailed = null;

        IPAddress chosen = task.Status == TaskStatus.RanToCompletion ? Choose(task.Result) : null;
        if (chosen == null)
        {
            Plugin.Logger.LogWarning(timedOut ? $"Resolving {_pendingHost} timed out." : $"Host {_pendingHost} not found.");
            onFailed?.Invoke();
            return;
        }

        Plugin.Logger.LogInfo($"Resolved {_pendingHost} -> {chosen}.");
        onResolved?.Invoke(chosen.ToString());
    }

    /// <summary>
    /// Picks the address to connect to: IPv4 is preferred (e.g. <c>localhost</c> resolves to both
    /// <c>::1</c> and <c>127.0.0.1</c>), otherwise the first IPv6 address.
    /// </summary>
    private static IPAddress Choose(IPAddress[] addresses)
    {
        if (addresses == null || addresses.Length == 0) return null;
        return addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)
            ?? addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetworkV6);
    }
}
