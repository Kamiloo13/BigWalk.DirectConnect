using System;
using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine.Events;

namespace BigWalk.DirectConnect.UI;

/// <summary>Helpers for the cloned TextMeshPro input fields.</summary>
internal static class InputFields
{
    /// <summary>
    /// Turns a clone of the game's join-code field into a plain input field.
    /// </summary>
    /// <remarks>
    /// Every event is replaced with a new, empty instance; this also removes listeners serialized in the scene
    /// (which <c>RemoveAllListeners</c> would keep), so the field can never trigger "join by code".
    /// </remarks>
    /// <param name="field">Field to reset.</param>
    /// <param name="type">Content type, e.g. <see cref="TMP_InputField.ContentType.Password"/>.</param>
    /// <param name="characterLimit">Maximum number of characters.</param>
    /// <param name="onSubmit">Called when Enter is pressed in the field.</param>
    public static void Reset(TMP_InputField field, TMP_InputField.ContentType type, int characterLimit, Action<string> onSubmit)
    {
        field.text = string.Empty;
        field.onSubmit = new TMP_InputField.SubmitEvent();
        field.onEndEdit = new TMP_InputField.SubmitEvent();
        field.onValueChanged = new TMP_InputField.OnChangeEvent();
        field.onSelect = new TMP_InputField.SelectionEvent();
        field.onDeselect = new TMP_InputField.SelectionEvent();
        field.onValidateInput = null;
        field.inputValidator = null;
        field.lineType = TMP_InputField.LineType.SingleLine;
        field.contentType = type;
        field.characterLimit = characterLimit;
        field.onSubmit.AddListener(DelegateSupport.ConvertDelegate<UnityAction<string>>(onSubmit));
        field.ForceLabelUpdate();
    }

    /// <summary>Sets the grey hint text shown while the field is empty.</summary>
    public static void SetPlaceholder(TMP_InputField field, string value)
    {
        if (field == null || field.placeholder == null) return;
        var text = field.placeholder.GetComponent<TMP_Text>();
        if (text != null) text.text = value;
    }

    /// <summary>Clears the field, shows <paramref name="message"/> as its hint and focuses it.</summary>
    public static void ShowError(TMP_InputField field, string message)
    {
        if (field == null) return;
        field.text = string.Empty;
        SetPlaceholder(field, message);
        field.Select();
    }
}
