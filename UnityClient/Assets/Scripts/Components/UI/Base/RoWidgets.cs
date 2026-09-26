using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Controls that look like the official client's, from its interface textures: the window title
/// bar and the three piece buttons (basic_interface/btn_*_left|mid|right).
/// </summary>
public static class RoWidgets {

    public const float BUTTON_HEIGHT = 20f;
    private const float BUTTON_SIDE = 6f;
    public static readonly Color TextColor = new Color32(0, 0, 0, 255);
    public static readonly Color DisabledTextColor = new Color(0.55f, 0.55f, 0.55f);
    public static readonly Color LineColor = new Color32(198, 198, 206, 255);
    private static readonly Color InputColor = new Color32(231, 231, 231, 255);

    public static Texture2D Texture(string path) {
        return TextureAssetLoader.Load($"{DBManager.INTERFACE_PATH}{path}");
    }

    /// <summary>
    /// A picture placed from the top left corner of its parent
    /// </summary>
    public static RawImage Picture(Transform parent, string name, Texture2D texture, Vector2 position, Vector2 size) {
        var image = new GameObject(name, typeof(RectTransform)).AddComponent<RawImage>();
        image.transform.SetParent(parent, false);
        image.texture = texture;
        image.raycastTarget = false;
        if (texture == null) {
            image.color = Color.clear;
        }
        var rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return image;
    }

    public static TextMeshProUGUI Text(Transform parent, string text, float size, Color color, Vector2 position, Vector2 box,
        TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft) {
        var tmp = new GameObject("Text", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        tmp.transform.SetParent(parent, false);
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = alignment;
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.raycastTarget = false;
        var rect = tmp.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = box;
        return tmp;
    }

    /// <summary>
    /// The windows' title bar (Base UI/Title Bar: titlebar_fix, the window icon and a label),
    /// across the top of the window
    /// </summary>
    public static TextMeshProUGUI TitleBar(RectTransform window, string title) {
        var bar = Object.Instantiate(Resources.Load<GameObject>("Prefabs/UI/Base UI/Title Bar"), window, false);
        bar.name = "Title Bar";
        // Not the picture's own size: its SetNativeSize (on Start) would also pin the bar to the
        // window's corner, undoing the stretch below
        var panel = bar.GetComponent<CustomPanel>();
        if (panel != null) {
            panel.overrideSize = false;
        }
        var rect = bar.transform as RectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(0f, 17f);
        var label = bar.GetComponentInChildren<TextMeshProUGUI>(true);
        label.text = title;
        label.fontSize = 12f;
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Ellipsis;
        // Just past the window icon at the left end whatever the width (the prefab starts it at a
        // fraction of it, over the icon in a narrow window), with room for the X at the right end
        var labelRect = label.rectTransform;
        labelRect.anchorMin = new Vector2(0f, labelRect.anchorMin.y);
        labelRect.anchorMax = new Vector2(1f, labelRect.anchorMax.y);
        labelRect.offsetMin = new Vector2(18f, labelRect.offsetMin.y);
        labelRect.offsetMax = new Vector2(-24f, labelRect.offsetMax.y);
        return label;
    }

    /// <summary>
    /// A button of three pieces, lit when pointed at, pressed in when held, grey when disabled
    /// </summary>
    public static Button Button(Transform parent, string text, UnityAction onClick, float width = -1f) {
        var root = new GameObject(text, typeof(RectTransform));
        root.transform.SetParent(parent, false);
        // Takes the taps over the whole button
        var hit = root.AddComponent<Image>();
        hit.color = Color.clear;
        var layout = root.AddComponent<HorizontalLayoutGroup>();
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;

        var left = Piece(root.transform, "Left", BUTTON_SIDE, 0f);
        var mid = Piece(root.transform, "Mid", 0f, 1f);
        var right = Piece(root.transform, "Right", BUTTON_SIDE, 0f);

        var label = new GameObject("Label", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        label.transform.SetParent(root.transform, false);
        label.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        label.text = text;
        label.fontSize = 12f;
        label.color = TextColor;
        label.alignment = TextAlignmentOptions.Center;
        label.enableWordWrapping = false;
        label.raycastTarget = false;
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;

        var element = root.AddComponent<LayoutElement>();
        element.minWidth = element.preferredWidth = width > 0f ? width : Mathf.Max(48f, label.preferredWidth + 2f * BUTTON_SIDE + 8f);
        element.minHeight = element.preferredHeight = BUTTON_HEIGHT;

        var button = root.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = hit;
        if (onClick != null) {
            button.onClick.AddListener(onClick);
        }

        var looks = root.AddComponent<ButtonLooks>();
        looks.Button = button;
        looks.Pieces = new[] { left, mid, right };
        looks.Label = label;
        looks.Show("out");
        return button;
    }

    /// <summary>
    /// A window as the official client draws them: white under the title bar, whose X calls
    /// <paramref name="onClose"/>. Centred in its parent; what goes in it is placed from its top
    /// left corner, the title bar taking the first 17 pixels
    /// </summary>
    public static RectTransform Window(Transform parent, string name, string title, Vector2 size, UnityAction onClose) {
        var window = new GameObject(name, typeof(RectTransform)).AddComponent<Image>();
        window.transform.SetParent(parent, false);
        window.color = Color.white;
        window.gameObject.AddComponent<Outline>().effectColor = LineColor;
        var rect = window.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        TitleBar(rect, title);
        WindowCloseButton.Add(rect, onClose);
        return rect;
    }

    /// <summary>
    /// The row of buttons at a window's bottom right (確認 取消 and the like)
    /// </summary>
    public static RectTransform ButtonRow(RectTransform window) {
        var row = new GameObject("Buttons", typeof(RectTransform)).GetComponent<RectTransform>();
        row.SetParent(window, false);
        row.anchorMin = row.anchorMax = row.pivot = new Vector2(1f, 0f);
        row.anchoredPosition = new Vector2(-6f, 6f);
        var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 4f;
        layout.childAlignment = TextAnchor.LowerRight;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        var fitter = row.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return row;
    }

    /// <summary>
    /// A radio button (radiobtn_on|off) with its label, one of <paramref name="group"/>
    /// </summary>
    public static Toggle Radio(Transform parent, string label, bool on, ToggleGroup group, Vector2 position, float width, bool interactable = true) {
        var root = new GameObject(label, typeof(RectTransform)).AddComponent<Image>();
        root.transform.SetParent(parent, false);
        // The label takes the tap as well as the button
        root.color = Color.clear;
        var rect = root.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(width, 16f);

        var off = Picture(rect, "Off", Texture("radiobtn_off.bmp"), new Vector2(0f, -2f), new Vector2(12f, 12f));
        var check = Picture(rect, "On", Texture("radiobtn_on.bmp"), new Vector2(0f, -2f), new Vector2(12f, 12f));
        Text(rect, label, 12f, interactable ? TextColor : DisabledTextColor, new Vector2(16f, 0f), new Vector2(width - 16f, 16f));
        if (!interactable) {
            off.color = check.color = new Color(1f, 1f, 1f, 0.5f);
        }

        var toggle = root.gameObject.AddComponent<Toggle>();
        toggle.transition = Selectable.Transition.None;
        toggle.targetGraphic = root;
        toggle.graphic = check;
        toggle.group = group;
        toggle.isOn = on;
        toggle.interactable = interactable;
        return toggle;
    }

    /// <summary>
    /// The grey text box of the official windows
    /// </summary>
    public static TMP_InputField InputField(Transform parent, string value, int characterLimit, Vector2 position, Vector2 size) {
        var background = new GameObject("Input", typeof(RectTransform)).AddComponent<Image>();
        background.transform.SetParent(parent, false);
        background.color = InputColor;
        var rect = background.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        // Built inactive: the field sets its caret up when enabled, which needs its text in place
        background.gameObject.SetActive(false);

        var area = new GameObject("Text Area", typeof(RectTransform)).GetComponent<RectTransform>();
        area.SetParent(rect, false);
        area.anchorMin = Vector2.zero;
        area.anchorMax = Vector2.one;
        area.offsetMin = new Vector2(4f, 1f);
        area.offsetMax = new Vector2(-4f, -1f);
        area.gameObject.AddComponent<RectMask2D>();
        var text = new GameObject("Text", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        text.transform.SetParent(area, false);
        text.fontSize = 12f;
        text.color = TextColor;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.enableWordWrapping = false;
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;

        var input = background.gameObject.AddComponent<TMP_InputField>();
        input.targetGraphic = background;
        input.textViewport = area;
        input.textComponent = text;
        input.characterLimit = characterLimit;
        input.customCaretColor = true;
        input.caretColor = TextColor;
        input.text = value ?? "";
        background.gameObject.SetActive(true);
        return input;
    }

    private static RawImage Piece(Transform parent, string name, float width, float flexible) {
        var piece = new GameObject(name, typeof(RectTransform)).AddComponent<RawImage>();
        piece.transform.SetParent(parent, false);
        piece.raycastTarget = false;
        var element = piece.gameObject.AddComponent<LayoutElement>();
        element.minWidth = element.preferredWidth = width;
        element.flexibleWidth = flexible;
        // A RawImage has no height of its own to give the layout
        element.minHeight = element.preferredHeight = BUTTON_HEIGHT;
        return piece;
    }

    // Swaps the pieces for the button's state: btn_out|over|press|disable_*
    private class ButtonLooks : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler {
        private static readonly string[] Sides = { "left", "mid", "right" };

        public Button Button;
        public RawImage[] Pieces;
        public TextMeshProUGUI Label;
        private bool Over;
        private bool Down;
        private bool WasInteractable = true;

        public void Show(string state) {
            for (var i = 0; i < Pieces.Length; i++) {
                Pieces[i].texture = Texture($"basic_interface/btn_{state}_{Sides[i]}.bmp");
            }
            Label.color = state == "disable" ? new Color(0.55f, 0.55f, 0.55f) : TextColor;
        }

        private void Refresh() {
            Show(!Button.IsInteractable() ? "disable" : Down ? "press" : Over ? "over" : "out");
        }

        private void LateUpdate() {
            if (Button.IsInteractable() != WasInteractable) {
                WasInteractable = Button.IsInteractable();
                Refresh();
            }
        }

        private void OnDisable() {
            Over = Down = false;
            Refresh();
        }

        public void OnPointerEnter(PointerEventData eventData) {
            Over = true;
            Refresh();
        }

        public void OnPointerExit(PointerEventData eventData) {
            Over = false;
            Refresh();
        }

        public void OnPointerDown(PointerEventData eventData) {
            Down = true;
            Refresh();
        }

        public void OnPointerUp(PointerEventData eventData) {
            Down = false;
            Refresh();
        }
    }
}
