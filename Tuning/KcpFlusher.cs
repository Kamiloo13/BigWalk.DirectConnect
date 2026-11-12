using System;
using System.Collections.Generic;
using System.Reflection;
using BigWalk.DirectConnect.Common;
using HarmonyLib;

namespace BigWalk.DirectConnect.Tuning;

/// <summary>
/// Flushes every KCP connection right after <c>KcpTransport</c>'s own per-frame update.
/// </summary>
/// <remarks>
/// kcp2k only flushes reliable data (RPCs, interactions) on its internal 10 ms timer. Flushing right after
/// the transport update sends it in the same frame instead. Unreliable data and the wire format are unaffected,
/// so the other side does not need the mod.
/// </remarks>
internal static class KcpFlusher
{
    /// <summary>Consecutive failures after which flushing is switched off for the rest of the game.</summary>
    private const int MaxErrors = 5;

    /// <summary>Cached <c>Kcp.Flush()</c> methods per runtime type.</summary>
    private static readonly Dictionary<Type, MethodInfo> FlushMethods = new();

    private static bool _disabled;
    private static int _errors;

    /// <summary><c>true</c> once at least one KCP connection has been flushed.</summary>
    public static bool Active { get; private set; }

    /// <summary>Patches <c>KcpTransport.ServerLateUpdate</c> and <c>ClientLateUpdate</c> with postfixes.</summary>
    public static void Install(Harmony harmony)
    {
        Type kcpTransport = Reflect.FindType("kcp2k.KcpTransport")
                         ?? Reflect.FindType("Il2Cppkcp2k.KcpTransport")
                         ?? Reflect.FindTypeByName("KcpTransport");
        if (kcpTransport == null)
        {
            Plugin.Logger.LogWarning("KcpTransport not found - KCP flush disabled.");
            return;
        }

        Patch(harmony, kcpTransport, "ServerLateUpdate", nameof(ServerPostfix));
        Patch(harmony, kcpTransport, "ClientLateUpdate", nameof(ClientPostfix));
    }

    private static void Patch(Harmony harmony, Type type, string method, string postfix)
    {
        MethodInfo target = type.GetMethod(method,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly,
            null, Type.EmptyTypes, null);
        if (target == null)
        {
            Plugin.Logger.LogWarning($"{type.Name}.{method} not found - not patched.");
            return;
        }

        try
        {
            var postfixMethod = typeof(KcpFlusher).GetMethod(postfix, BindingFlags.NonPublic | BindingFlags.Static);
            harmony.Patch(target, postfix: new HarmonyMethod(postfixMethod));
        }
        catch (Exception e)
        {
            Plugin.Logger.LogWarning($"Could not patch {type.Name}.{method}: {e.Message}");
        }
    }

    /// <summary>Host: flushes every client connection of the KCP server.</summary>
    private static void ServerPostfix(object __instance)
    {
        if (!Enabled) return;
        try
        {
            object server = Reflect.Get(__instance, "server");
            foreach (object connection in Reflect.Iterate(Reflect.Get(server, "connections")))
                FlushPeer(connection);
        }
        catch (Exception e) { Fail(e); }
    }

    /// <summary>Client: flushes the connection to the host.</summary>
    private static void ClientPostfix(object __instance)
    {
        if (!Enabled) return;
        try { FlushPeer(Reflect.Get(__instance, "client")); }
        catch (Exception e) { Fail(e); }
    }

    private static bool Enabled => !_disabled && Plugin.FlushKcp.Value;

    /// <summary>
    /// Finds the <c>Kcp</c> object of a peer and calls its <c>Flush()</c>. Supports the different kcp2k layouts:
    /// <c>peer.kcp</c>, <c>connection.peer.kcp</c> and <c>client.connection.kcp</c>.
    /// </summary>
    private static void FlushPeer(object peer)
    {
        if (peer == null) return;
        object kcp = Reflect.Get(peer, "kcp")
                  ?? Reflect.Get(Reflect.Get(peer, "peer"), "kcp")
                  ?? Reflect.Get(Reflect.Get(peer, "connection"), "kcp");
        if (kcp == null) return; // not connected yet - normal while in menus

        MethodInfo flush = GetFlush(kcp.GetType());
        if (flush == null) return;

        flush.Invoke(kcp, null);
        _errors = 0;

        if (!Active)
        {
            Active = true;
            Plugin.Logger.LogInfo("KCP per-frame flush is active.");
        }
    }

    private static MethodInfo GetFlush(Type type)
    {
        if (FlushMethods.TryGetValue(type, out MethodInfo flush)) return flush;

        flush = type.GetMethod("Flush", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);
        FlushMethods[type] = flush;
        if (flush == null) Plugin.Logger.LogWarning($"{type.FullName} has no Flush(); KCP flush inactive.");
        return flush;
    }

    /// <summary>Counts consecutive failures and disables flushing after <see cref="MaxErrors"/>.</summary>
    private static void Fail(Exception e)
    {
        if (++_errors < MaxErrors) return;
        _disabled = true;
        Plugin.Logger.LogWarning($"KCP flush disabled after repeated errors: {e.InnerException?.Message ?? e.Message}");
    }
}
