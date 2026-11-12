using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace BigWalk.DirectConnect.UI;

/// <summary>Vertical extent of a UI element (local units of some reference transform).</summary>
internal struct YRange
{
    /// <summary>Bottom edge.</summary>
    public float Min;

    /// <summary>Top edge.</summary>
    public float Max;

    /// <summary>Creates a range from its bottom and top edge.</summary>
    public YRange(float min, float max)
    {
        Min = min;
        Max = max;
    }

    /// <summary>Distance between the top and bottom edge.</summary>
    public float Height => Max - Min;
}

/// <summary>Helpers for measuring, cloning and moving uGUI elements of the Join Game page.</summary>
internal static class UiLayout
{
    /// <summary>Reusable buffer for <see cref="RectTransform.GetWorldCorners"/> (IL2CPP needs a native array).</summary>
    private static readonly Il2CppStructArray<Vector3> Corners = new(4);

    /// <summary>
    /// Measures everything that is actually drawn (every active <see cref="Graphic"/>) under
    /// <paramref name="t"/>. Container rects are often empty or larger than their content, so this is more
    /// reliable than the element's own rect.
    /// </summary>
    /// <param name="t">Element to measure.</param>
    /// <param name="space">Transform whose local coordinates the result is expressed in.</param>
    public static YRange VisualRange(Transform t, Transform space)
    {
        bool any = false;
        var r = new YRange(float.MaxValue, float.MinValue);
        foreach (Graphic g in t.GetComponentsInChildren<Graphic>(false))
        {
            YRange o = OwnRange(g.transform, space);
            r.Min = Mathf.Min(r.Min, o.Min);
            r.Max = Mathf.Max(r.Max, o.Max);
            any = true;
        }
        return any ? r : OwnRange(t, space);
    }

    /// <summary>Measures the element's own rect (or its pivot, for plain transforms).</summary>
    public static YRange OwnRange(Transform t, Transform space)
    {
        var rt = t.GetComponent<RectTransform>();
        if (rt == null)
        {
            float y = space.InverseTransformPoint(t.position).y;
            return new YRange(y, y);
        }

        rt.GetWorldCorners(Corners);
        var r = new YRange(float.MaxValue, float.MinValue);
        for (int i = 0; i < 4; i++)
        {
            float y = space.InverseTransformPoint(Corners[i]).y;
            r.Min = Mathf.Min(r.Min, y);
            r.Max = Mathf.Max(r.Max, y);
        }
        return r;
    }

    /// <summary>Finds the closest active child of <paramref name="container"/> whose top lies below <paramref name="y"/>.</summary>
    /// <param name="container">Parent to search.</param>
    /// <param name="y">Reference height in <paramref name="container"/> coordinates.</param>
    /// <param name="skip">Child to ignore (usually the element <paramref name="y"/> was taken from).</param>
    /// <param name="range">Visual range of the child found.</param>
    /// <returns>The child, or <c>null</c> if nothing is below.</returns>
    public static Transform NearestBelow(Transform container, float y, Transform skip, out YRange range)
    {
        Transform best = null;
        range = default;
        for (int i = 0; i < container.childCount; i++)
        {
            Transform c = container.GetChild(i);
            if (c == skip || !c.gameObject.activeInHierarchy) continue;

            YRange r = VisualRange(c, container);
            if (r.Max <= y + 1f && (best == null || r.Max > range.Max))
            {
                best = c;
                range = r;
            }
        }
        return best;
    }

    /// <summary>
    /// Moves every child of <paramref name="parent"/> whose top lies below <paramref name="belowY"/> down by
    /// <paramref name="amount"/>. Elements are only moved, never resized, so anchored content inside them
    /// (e.g. the friends list) keeps its position relative to its container.
    /// </summary>
    /// <param name="parent">Parent whose children are moved.</param>
    /// <param name="belowY">Reference height in <paramref name="parent"/> coordinates.</param>
    /// <param name="amount">Distance in <paramref name="parent"/> units.</param>
    /// <param name="skip">Children that must not move.</param>
    public static void PushDown(Transform parent, float belowY, float amount, params Transform[] skip)
    {
        Vector3 worldShift = parent.TransformVector(new Vector3(0f, amount, 0f));
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform c = parent.GetChild(i);
            if (Array.IndexOf(skip, c) >= 0) continue;
            if (VisualRange(c, parent).Max <= belowY + 1f) c.position -= worldShift;
        }
    }

    /// <summary>
    /// Clones <paramref name="source"/> under <paramref name="parent"/>. The copy is created inside an inactive
    /// holder, so no game script runs on it before the game's localization components are removed – otherwise
    /// they would overwrite our texts (e.g. back to "JOIN VIA CODE:").
    /// </summary>
    public static GameObject CloneInactive(GameObject source, Transform parent)
    {
        var holder = new GameObject("DirectConnectHolder");
        holder.SetActive(false);
        holder.transform.SetParent(parent, false);

        GameObject clone = Object.Instantiate(source, holder.transform, false);

        var toRemove = new List<Component>();
        foreach (Component c in clone.GetComponentsInChildren<Component>(true))
            if (c != null && c.GetIl2CppType().Name.IndexOf("Locali", StringComparison.OrdinalIgnoreCase) >= 0)
                toRemove.Add(c);
        foreach (var c in toRemove) Object.DestroyImmediate(c);

        clone.transform.SetParent(parent, false); // activates the clone with its final components
        Object.Destroy(holder);
        return clone;
    }
}
