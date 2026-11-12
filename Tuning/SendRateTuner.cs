using System;
using System.Net;
using BigWalk.DirectConnect.Common;
using UnityEngine;

namespace BigWalk.DirectConnect.Tuning;

/// <summary>
/// Raises Mirror's send rate during direct (IP) sessions and restores the game's value afterwards.
/// </summary>
/// <remarks>
/// <para>Mirror's server only flushes outgoing messages – including the replies to the clients' ping
/// messages – at its send rate. BIG WALK uses 30 Hz, so every reply waits up to 33 ms. At 60 Hz this drops
/// to ~17 ms, which also halves the interpolation delay of other players' movement.</para>
/// <para>Clients assume the server sends at their own rate, so every player of a session should use the same
/// value; that is why the default mode only applies to IP sessions, where all players run this mod.</para>
/// </remarks>
internal static class SendRateTuner
{
    private static bool _searched;
    private static Type _server, _client, _manager, _transport;

    /// <summary>The game's own send rate, captured at the start of the first session.</summary>
    private static int? _originalRate;

    private static bool _inSession;
    private static bool _kcpChecked;
    private static float _sessionStart;

    /// <summary>Called twice per second by <see cref="ModController"/>; applies or restores the send rate.</summary>
    public static void Evaluate()
    {
        if (!_searched) FindTypes();
        if (_server == null) return;

        bool server = Reflect.StaticBool(_server, "active");
        bool client = Reflect.StaticBool(_client, "isConnected");
        double? currentRate = Reflect.Number(Reflect.GetStatic(_server, "tickRate"));
        if (currentRate == null) return;
        int current = (int)currentRate.Value;

        if (!server && !client)
        {
            EndSession(current);
            return;
        }
        if (!_inSession) BeginSession(current);

        bool raise = ShouldRaise(server, out string reason);
        WarnIfKcpFlushInactive(raise);

        int target = raise ? Plugin.SendRate.Value : _originalRate.Value;
        if (current == target) return;

        Apply(target);
        Plugin.Logger.LogInfo($"Send rate {current} -> {target} Hz ({reason}). " +
                              $"Now: server={Reflect.GetStatic(_server, "tickRate")} Hz, client={Reflect.GetStatic(_client, "sendRate")} Hz.");
    }

    /// <summary>Resolves the Mirror types once.</summary>
    private static void FindTypes()
    {
        _searched = true;
        _server = Reflect.Mirror("NetworkServer");
        _client = Reflect.Mirror("NetworkClient");
        _manager = Reflect.Mirror("NetworkManager");
        _transport = Reflect.Mirror("Transport");
        if (_server == null) Plugin.Logger.LogWarning("Mirror.NetworkServer not found - send rate tuning disabled.");
    }

    private static void BeginSession(int current)
    {
        _inSession = true;
        _sessionStart = Time.unscaledTime;
        _kcpChecked = false;
        _originalRate ??= current;
    }

    /// <summary>Restores the game's send rate once the session is over.</summary>
    private static void EndSession(int current)
    {
        if (!_inSession) return;
        _inSession = false;

        if (_originalRate.HasValue && current != _originalRate.Value)
        {
            Apply(_originalRate.Value);
            Plugin.Logger.LogInfo($"Session ended - send rate restored to {_originalRate.Value} Hz.");
        }
    }

    /// <summary>Decides, according to the configured <see cref="TuningMode"/>, whether to raise the rate.</summary>
    /// <param name="server">Whether this instance is the host.</param>
    /// <param name="reason">Short explanation for the log.</param>
    private static bool ShouldRaise(bool server, out string reason)
    {
        switch (Plugin.Mode.Value)
        {
            case TuningMode.Always:
                reason = "mode Always";
                return true;
            case TuningMode.Off:
                reason = "mode Off";
                return false;
            default:
                return IsDirectSession(server, out reason);
        }
    }

    /// <summary>
    /// Client: the active transport is KCP. Host: at least one remote player and every remote player has an IP
    /// address (KCP connections report an IPv4/IPv6 address, Epic connections an Epic user id).
    /// </summary>
    private static bool IsDirectSession(bool server, out string reason)
    {
        if (!server)
        {
            object transport = Reflect.CastToRuntimeType(Reflect.GetStatic(_transport, "active"));
            string name = transport?.GetType().Name ?? "unknown";
            reason = $"client transport {name}";
            return name.Contains("Kcp");
        }

        int remote = 0;
        foreach (object connection in Reflect.Iterate(Reflect.GetStatic(_server, "connections")))
        {
            string address = Reflect.Get(connection, "address") as string;
            if (string.IsNullOrEmpty(address) || address == "localhost") continue; // the host's own client

            remote++;
            if (!IPAddress.TryParse(address, out _))
            {
                reason = "a non-IP (Epic) player is connected";
                return false;
            }
        }

        reason = remote > 0 ? $"host, {remote} IP player(s)" : "host, no remote players";
        return remote > 0;
    }

    /// <summary>Writes the rate to every place Mirror (and the game's NetworkManager) keeps it.</summary>
    private static void Apply(int rate)
    {
        Reflect.SetStatic(_server, "tickRate", rate);  // server send interval (NetworkServer.sendRate derives from it)
        Reflect.SetStatic(_client, "sendRate", rate);  // only if writable in this Mirror version

        object manager = Reflect.CastToRuntimeType(Reflect.GetStatic(_manager, "singleton"));
        Reflect.Set(manager, "sendRate", rate);        // re-applied by Mirror when a new session starts
        Reflect.Set(manager, "serverTickRate", rate);
    }

    /// <summary>Logs a warning (once per session) if a direct session runs without the KCP flush.</summary>
    private static void WarnIfKcpFlushInactive(bool directSession)
    {
        if (_kcpChecked || Time.unscaledTime - _sessionStart < 10f) return;
        _kcpChecked = true;

        if (directSession && Plugin.FlushKcp.Value && !KcpFlusher.Active)
            Plugin.Logger.LogWarning("KCP flush found no KCP connection in this session (unknown kcp2k layout?).");
    }
}
