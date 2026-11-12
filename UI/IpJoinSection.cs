using System;
using System.Text.RegularExpressions;
using BigWalk.DirectConnect.Localization;
using BigWalk.DirectConnect.Networking;
using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace BigWalk.DirectConnect.UI;

/// <summary>
/// Builds and drives the "Join via IP" section of the Join Game page.
/// </summary>
/// <remarks>
/// <para>The section is a clone of the game's <c>Join_Code</c> block (heading, input field, play button with icon,
/// divider), so it matches the game's styling exactly. A second copy of the input field becomes the password
/// field. The section is placed directly below "Join via code" and everything below it (the friends list) is
/// pushed down by the same amount.</para>
/// <para>While a host name is being resolved, the section is dimmed and locked and the join button shows
/// animated dots instead of its arrow (see <see cref="SetBusy"/>).</para>
/// <para>The page is rebuilt with every new main-menu scene; the section is created once per page instance and
/// only its texts are refreshed when the page is opened again.</para>
/// </remarks>
internal static class IpJoinSection
{
    private const string SectionName = "IpJoinSection";
    private const string PasswordName = "IpJoinPassword";

    // Paths inside the cloned Join_Code block (taken from the game's UI hierarchy).
    private const string HeadingPath = "Subheading";
    private const string PlayButtonPath = "PlayButton";
    private const string PlayLabelPath = "PlayButton/Text (TMP)";

    /// <summary>Max. length of the address field: the longest possible DNS name (253 characters) fits.</summary>
    private const int AddressCharacterLimit = 253;

    /// <summary>Max. length of the password field (same as the game's password prompt).</summary>
    private const int PasswordCharacterLimit = 32;

    /// <summary>Opacity of the section while a host name is being resolved.</summary>
    private const float BusyAlpha = 0.5f;

    /// <summary>Seconds per frame of the "loading" dots on the join button.</summary>
    private const float DotsFrameSeconds = 0.35f;

    // Delegates are kept in static fields so they are not garbage-collected while IL2CPP references them.
    private static readonly Action JoinAction = OnJoinClicked;
    private static readonly Action<string> SubmitAction = _ => OnJoinClicked();

    private static JoinMenu _pendingMenu;
    private static int _framesToWait;

    private static JoinMenu _menu;
    private static TMP_InputField _addressField, _passwordField;
    private static TMP_Text _heading, _buttonLabel;

    /// <summary>Dims the section and blocks all input while a DNS lookup is running.</summary>
    private static CanvasGroup _group;

    /// <summary>Whether the section is currently in the "resolving" state.</summary>
    private static bool _busy;

    /// <summary>
    /// Arrow icon of the game's "Play" button, e.g. <c>&lt;font="HouseHouseIcons-Regular v1 SDF"&gt;…&lt;/font&gt;</c>.
    /// The icon is a glyph of a separate icon font embedded in the label's rich text, so it is copied from the
    /// game's label and appended to ours.
    /// </summary>
    private static string _buttonIcon;

    /// <summary>Matches the icon-font part of a button label.</summary>
    private static readonly Regex IconMarkup = new("<font=[^>]*>.*?</font>", RegexOptions.Singleline | RegexOptions.IgnoreCase);

    /// <summary>
    /// Called when the Join Game page opens. Building is delayed by two frames so that the game has finished
    /// laying out the page before it is measured.
    /// </summary>
    public static void Schedule(JoinMenu menu)
    {
        _pendingMenu = menu;
        _framesToWait = 2;
    }

    /// <summary>Called every frame by <see cref="ModController"/>.</summary>
    public static void Tick()
    {
        AnimateBusyLabel();

        if (_pendingMenu == null) return;
        if (!_pendingMenu.gameObject.activeInHierarchy)
        {
            _pendingMenu = null; // page was closed before it was built
            return;
        }
        if (--_framesToWait > 0) return;

        var menu = _pendingMenu;
        _pendingMenu = null;
        try { Setup(menu); }
        catch (Exception e) { Plugin.Logger.LogError($"Failed to set up the 'Join via IP' section: {e}"); }
    }

    /// <summary>Creates the section if needed, binds its controls and applies the current language.</summary>
    private static void Setup(JoinMenu menu)
    {
        _menu = menu;
        Loc.Refresh();

        TMP_InputField codeField = menu.addressField;
        if (codeField == null)
        {
            Plugin.Logger.LogWarning("JoinMenu.addressField is missing; skipping.");
            return;
        }

        Transform codeBlock = codeField.transform.parent; // "Join_Code"
        _buttonIcon = ExtractIcon(codeBlock.Find(PlayLabelPath)?.GetComponent<TMP_Text>());
        Transform section = codeBlock.parent.Find(SectionName);
        if (section == null) section = Build(codeBlock, codeField);

        Bind(section, codeField.name);
        SetBusy(AddressResolver.Busy);
        ApplyTexts();
    }

    /// <summary>Clones the "Join via code" block into the new section and makes room for it.</summary>
    /// <param name="codeBlock">The game's <c>Join_Code</c> block.</param>
    /// <param name="codeField">The join-code input field inside it.</param>
    /// <returns>The new section.</returns>
    private static Transform Build(Transform codeBlock, TMP_InputField codeField)
    {
        Canvas.ForceUpdateCanvases();
        Transform container = codeBlock.parent;

        // Measure before anything moves. One "slot" = the code block's height + the gap to the next element.
        YRange code = UiLayout.VisualRange(codeBlock, container);
        Transform next = UiLayout.NearestBelow(container, code.Min, codeBlock, out YRange nextRange);
        float gap = next != null ? Mathf.Clamp(code.Min - nextRange.Max, 0f, 200f) : 24f;
        float sectionShift = code.Height + gap;

        Transform section = UiLayout.CloneInactive(codeBlock.gameObject, container).transform;
        section.name = SectionName;
        section.gameObject.AddComponent<CanvasGroup>(); // used by SetBusy
        section.SetSiblingIndex(codeBlock.GetSiblingIndex() + 1);

        // Address field.
        var address = section.Find(codeField.name).GetComponent<TMP_InputField>();
        InputFields.Reset(address, TMP_InputField.ContentType.Standard, AddressCharacterLimit, SubmitAction);

        // Buttons: drop everything copied from "join by code"; only the play button does something (join via IP).
        foreach (Button b in section.GetComponentsInChildren<Button>(true))
            b.onClick = new Button.ButtonClickedEvent();
        Transform playTransform = section.Find(PlayButtonPath);
        Button play = playTransform != null ? playTransform.GetComponent<Button>() : section.GetComponentInChildren<Button>(true);
        if (play != null) play.onClick.AddListener(DelegateSupport.ConvertDelegate<UnityAction>(JoinAction));

        // Longer labels than the game's "Play" (e.g. "Beitreten" + arrow) shrink instead of wrapping.
        var label = section.Find(PlayLabelPath)?.GetComponent<TMP_Text>();
        if (label != null)
        {
            label.fontSizeMax = label.fontSize;
            label.fontSizeMin = label.fontSize * 0.6f;
            label.enableAutoSizing = true;
        }

        // Password field directly under the address field; the section's divider moves down with it.
        float passwordShift = AddPasswordField(section, address);

        // Make room: push the friends list down by the whole new section, then move the section into the gap.
        UiLayout.PushDown(container, code.Min, sectionShift + passwordShift, codeBlock, section);
        section.position -= container.TransformVector(new Vector3(0f, sectionShift, 0f));

        Plugin.Logger.LogInfo($"Added 'Join via IP' section (offset {sectionShift:0.#} + {passwordShift:0.#}).");
        return section;
    }

    /// <summary>Clones the address field as the password field and places it right below it.</summary>
    /// <returns>Extra height taken by the password field, in section units.</returns>
    private static float AddPasswordField(Transform section, TMP_InputField address)
    {
        YRange addressRange = UiLayout.OwnRange(address.transform, section);
        float shift = addressRange.Height + Mathf.Max(12f, addressRange.Height * 0.15f);

        // Everything below the address field inside the section (the divider) moves down first.
        UiLayout.PushDown(section, addressRange.Min, shift, address.transform);

        GameObject password = Object.Instantiate(address.gameObject, section, false);
        password.name = PasswordName;
        password.transform.SetSiblingIndex(address.transform.GetSiblingIndex() + 1);
        password.transform.position = address.transform.position - section.TransformVector(new Vector3(0f, shift, 0f));
        InputFields.Reset(password.GetComponent<TMP_InputField>(), TMP_InputField.ContentType.Password, PasswordCharacterLimit, SubmitAction);

        return shift;
    }

    /// <summary>Looks up the controls of an existing section.</summary>
    private static void Bind(Transform section, string addressFieldName)
    {
        _addressField = section.Find(addressFieldName)?.GetComponent<TMP_InputField>();
        _passwordField = section.Find(PasswordName)?.GetComponent<TMP_InputField>();
        _heading = section.Find(HeadingPath)?.GetComponent<TMP_Text>();
        _buttonLabel = section.Find(PlayLabelPath)?.GetComponent<TMP_Text>();
        _group = section.GetComponent<CanvasGroup>();
    }

    /// <summary>Writes the texts of the current language into the section (also resets error hints).</summary>
    private static void ApplyTexts()
    {
        var s = Loc.Current;
        if (_heading != null) _heading.text = s.Heading;
        SetButtonLabel(_buttonIcon);
        InputFields.SetPlaceholder(_addressField, s.AddressPlaceholder);
        InputFields.SetPlaceholder(_passwordField, s.PasswordPlaceholder);
    }

    /// <summary>Sets the join button's label: the localized text followed by <paramref name="suffix"/> (icon or dots).</summary>
    private static void SetButtonLabel(string suffix)
    {
        if (_buttonLabel == null) return;
        string text = Loc.Current.Button;
        _buttonLabel.text = string.IsNullOrEmpty(suffix) ? text : $"{text} {suffix}";
    }

    /// <summary>
    /// Switches the "resolving" state: the section is dimmed and every field and button inside it is disabled
    /// (via <see cref="CanvasGroup.interactable"/>), and the arrow on the join button is replaced by animated dots.
    /// </summary>
    private static void SetBusy(bool busy)
    {
        _busy = busy;

        if (_group != null)
        {
            _group.alpha = busy ? BusyAlpha : 1f;
            _group.interactable = busy == false;
        }

        if (busy)
        {
            // A focused input field would otherwise keep accepting keystrokes.
            _addressField?.DeactivateInputField();
            _passwordField?.DeactivateInputField();
        }
        else
        {
            SetButtonLabel(_buttonIcon);
        }
    }

    /// <summary>Animates the "loading" dots (".", "..", "...") while the section is busy.</summary>
    private static void AnimateBusyLabel()
    {
        if (!_busy || _buttonLabel == null) return;

        int frame = (int)(Time.unscaledTime / DotsFrameSeconds) % 3;
        // Padded to a constant width; the game's font is monospaced, so the label does not jump.
        SetButtonLabel(new string('.', frame + 1).PadRight(3));
    }

    /// <summary>Returns the icon-font markup of a label, or <c>null</c> if it has none.</summary>
    private static string ExtractIcon(TMP_Text label)
    {
        if (label == null || string.IsNullOrEmpty(label.text)) return null;
        Match match = IconMarkup.Match(label.text);
        return match.Success ? match.Value : null;
    }

    /// <summary>
    /// Join button / Enter: validates both fields, resolves host names via DNS and starts connecting.
    /// </summary>
    private static void OnJoinClicked()
    {
        if (_menu == null || _addressField == null || _passwordField == null) return;
        if (AddressResolver.Busy) return; // a lookup for the previous click is still running

        AddressKind kind = AddressResolver.Classify(_addressField.text, out string address);
        if (kind == AddressKind.Invalid)
        {
            Plugin.Logger.LogWarning($"Rejected invalid address: {_addressField.text}");
            InputFields.ShowError(_addressField, Loc.Current.InvalidAddress);
            return;
        }

        // Hosts always set a password in BIG WALK, so at least one character is required.
        string password = _passwordField.text;
        if (string.IsNullOrEmpty(password))
        {
            InputFields.ShowError(_passwordField, Loc.Current.PasswordRequired);
            return;
        }

        if (kind == AddressKind.IpAddress)
        {
            DirectJoin.Join(_menu, address, password);
            return;
        }

        // Host name: block the section until DNS answers (or the lookup times out).
        JoinMenu menu = _menu;
        SetBusy(true);
        AddressResolver.Resolve(address,
            ip =>
            {
                SetBusy(false);
                // The player may have left the page while the lookup was running.
                if (menu != null && menu.gameObject.activeInHierarchy) DirectJoin.Join(menu, ip, password);
            },
            () =>
            {
                SetBusy(false); // re-enable the field first, ShowError focuses it
                InputFields.ShowError(_addressField, Loc.Current.HostNotFound);
            });
    }
}
