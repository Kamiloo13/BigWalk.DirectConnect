using System;
using System.Collections.Generic;
using System.Reflection;
using Il2CppInterop.Runtime.InteropTypes;

namespace BigWalk.DirectConnect.Common;

/// <summary>
/// Reflection helpers for IL2CPP interop types (Mirror, kcp2k, game code) that are not referenced at compile
/// time. Accessing them by name keeps the mod independent of the exact versions of those assemblies.
/// </summary>
/// <remarks>
/// Every method is defensive: on any failure it returns <c>null</c> (or does nothing) instead of throwing, so a
/// game update can at worst disable a feature, never crash the game.
/// </remarks>
internal static class Reflect
{
    private const BindingFlags Inst = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    private const BindingFlags Stat = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.FlattenHierarchy;

    #region Type lookup

    /// <summary>Finds a type by its full name in any loaded assembly.</summary>
    /// <param name="fullName">Namespace-qualified type name, e.g. <c>kcp2k.KcpTransport</c>.</param>
    /// <returns>The type, or <c>null</c> if it is not loaded.</returns>
    public static Type FindType(string fullName)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                var t = asm.GetType(fullName, false);
                if (t != null) return t;
            }
            catch
            {
                // Some dynamic/broken assemblies throw on GetType - skip them.
            }
        }
        return null;
    }

    /// <summary>Finds the first loaded type with the given simple name (namespace ignored).</summary>
    public static Type FindTypeByName(string name)
    {
        foreach (var t in AllTypes())
            if (t.Name == name) return t;
        return null;
    }

    /// <summary>
    /// Finds a Mirror type, with or without the <c>Il2Cpp</c> namespace prefix that some Il2CppInterop
    /// versions add to non-Unity namespaces.
    /// </summary>
    /// <param name="name">Simple type name, e.g. <c>NetworkServer</c>.</param>
    public static Type Mirror(string name) => FindType("Mirror." + name) ?? FindType("Il2CppMirror." + name);

    /// <summary>Enumerates every loadable type of every loaded assembly.</summary>
    public static IEnumerable<Type> AllTypes()
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types;
            try { types = asm.GetTypes(); }
            catch (ReflectionTypeLoadException e) { types = e.Types; } // partially loadable assembly
            catch { continue; }

            foreach (var t in types)
                if (t != null) yield return t;
        }
    }

    #endregion

    #region Member access

    /// <summary>Reads an instance property or field (public or not).</summary>
    /// <returns>The value, or <c>null</c> if the object is <c>null</c> or the member does not exist.</returns>
    public static object Get(object o, string name)
    {
        if (o == null) return null;
        Type t = o.GetType();
        try
        {
            var p = Prop(t, name, Inst);
            if (p != null && p.CanRead) return p.GetValue(o);
            return t.GetField(name, Inst)?.GetValue(o);
        }
        catch { return null; }
    }

    /// <summary>Writes an instance property or field, converting the value to the member's type.</summary>
    public static void Set(object o, string name, object value)
    {
        if (o == null) return;
        Type t = o.GetType();
        try
        {
            var p = Prop(t, name, Inst);
            if (p != null && p.CanWrite)
            {
                p.SetValue(o, Convert.ChangeType(value, p.PropertyType));
                return;
            }

            var f = t.GetField(name, Inst);
            if (f != null && !f.IsInitOnly) f.SetValue(o, Convert.ChangeType(value, f.FieldType));
        }
        catch
        {
            // Member is read-only or has an incompatible type in this version - ignore.
        }
    }

    /// <summary>Reads a static property, field or constant.</summary>
    /// <returns>The value, or <c>null</c> if the type is <c>null</c> or the member does not exist.</returns>
    public static object GetStatic(Type t, string name)
    {
        if (t == null) return null;
        try
        {
            var p = Prop(t, name, Stat);
            if (p != null && p.CanRead) return p.GetValue(null);

            var f = t.GetField(name, Stat);
            if (f != null) return f.IsLiteral ? f.GetRawConstantValue() : f.GetValue(null);
        }
        catch { }
        return null;
    }

    /// <summary>Writes a static property or field, converting the value to the member's type.</summary>
    public static void SetStatic(Type t, string name, object value)
    {
        if (t == null) return;
        try
        {
            var p = Prop(t, name, Stat);
            if (p != null && p.CanWrite)
            {
                p.SetValue(null, Convert.ChangeType(value, p.PropertyType));
                return;
            }

            var f = t.GetField(name, Stat);
            if (f != null && !f.IsLiteral && !f.IsInitOnly) f.SetValue(null, Convert.ChangeType(value, f.FieldType));
        }
        catch { }
    }

    /// <summary>Reads a static boolean member; anything other than <c>true</c> yields <c>false</c>.</summary>
    public static bool StaticBool(Type t, string name) => GetStatic(t, name) is bool b && b;

    /// <summary>Converts any numeric value to <see cref="double"/>.</summary>
    /// <returns>The number, or <c>null</c> if <paramref name="o"/> is <c>null</c> or not numeric.</returns>
    public static double? Number(object o)
    {
        if (o == null) return null;
        try { return Convert.ToDouble(o); }
        catch { return null; }
    }

    #endregion

    #region IL2CPP specifics

    /// <summary>
    /// Casts an interop object to the managed wrapper of its real IL2CPP class.
    /// </summary>
    /// <remarks>
    /// Interop returns objects typed by the <i>declared</i> type (e.g. <c>Transport</c>). Members that only exist
    /// on the real class (e.g. <c>KcpTransport.NoDelay</c>) are invisible to reflection until the object is cast.
    /// </remarks>
    /// <returns>The cast object, or the original object if no cast is possible.</returns>
    public static object CastToRuntimeType(object o)
    {
        if (o is not Il2CppSystem.Object io) return o;
        try
        {
            string fullName = io.GetIl2CppType().FullName;
            Type managed = FindType(fullName) ?? FindType("Il2Cpp" + fullName);
            if (managed == null || managed == o.GetType()) return o;

            MethodInfo tryCast = typeof(Il2CppObjectBase).GetMethod("TryCast").MakeGenericMethod(managed);
            return tryCast.Invoke(o, null) ?? o;
        }
        catch { return o; }
    }

    /// <summary>
    /// Iterates a managed or IL2CPP collection. For dictionaries the values are yielded.
    /// </summary>
    /// <remarks>IL2CPP collections do not implement <see cref="System.Collections.IEnumerable"/>, so their
    /// <c>GetEnumerator</c>/<c>MoveNext</c>/<c>Current</c> members are invoked by reflection.</remarks>
    public static IEnumerable<object> Iterate(object collection)
    {
        if (collection == null) yield break;
        object source = Get(collection, "Values") ?? collection;

        if (source is System.Collections.IEnumerable managed && source is not string)
        {
            foreach (var x in managed) yield return x;
            yield break;
        }

        object enumerator = source.GetType().GetMethod("GetEnumerator", Type.EmptyTypes)?.Invoke(source, null);
        if (enumerator == null) yield break;

        MethodInfo moveNext = enumerator.GetType().GetMethod("MoveNext", Type.EmptyTypes);
        PropertyInfo current = enumerator.GetType().GetProperty("Current");
        if (moveNext == null || current == null) yield break;

        while ((bool)moveNext.Invoke(enumerator, null))
            yield return current.GetValue(enumerator);
    }

    #endregion

    /// <summary>
    /// Finds a non-indexed property; tolerates types where a derived class hides a base property with the same
    /// name (which makes <see cref="Type.GetProperty(string, BindingFlags)"/> throw).
    /// </summary>
    private static PropertyInfo Prop(Type t, string name, BindingFlags flags)
    {
        try
        {
            var p = t.GetProperty(name, flags);
            return p != null && p.GetIndexParameters().Length == 0 ? p : null;
        }
        catch (AmbiguousMatchException)
        {
            foreach (var p in t.GetProperties(flags))
                if (p.Name == name && p.GetIndexParameters().Length == 0) return p;
            return null;
        }
    }
}
