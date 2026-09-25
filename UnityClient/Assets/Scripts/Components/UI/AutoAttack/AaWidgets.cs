using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Controls for the auto attack window, built in code like the rest of the runtime UI: rows that
/// lay themselves out in a vertical list, big enough to use with a finger.
/// </summary>
public static class AaWidgets {

    public const float ROW_HEIGHT = 36f;
    public const float FONT_SIZE = 16f;
    public const float SMALL_FONT_SIZE = 13f;
    private const float CONTROL_HEIGHT = 30f;
    private const float PICKER_ROW_HEIGHT = 42f;

    public static readonly Color PanelColor = new Color(0.09f, 0.1f, 0.13f, 0.96f);
    public static readonly Color BarColor = new Color(0.14f, 0.15f, 0.2f, 1f);
    public static readonly Color TextColor = new Color(0.93f, 0.93f, 0.96f);
    public static readonly Color DimTextColor = new Color(0.62f, 0.64f, 0.7f);
    public static readonly Color AccentColor = new Color(0.45f, 0.75f, 1f);
    public static readonly Color ControlColor = new Color(0.22f, 0.24f, 0.3f, 1f);
    public static readonly Color SelectedColor = new Color(0.25f, 0.45f, 0.72f, 1f);
    public static readonly Color GoodColor = new Color(0.4f, 0.88f, 0.45f);
    public static readonly Color WarnColor = new Color(1f, 0.65f, 0.3f);
    public static readonly Color BadColor = new Color(1f, 0.45f, 0.45f);

    public static RectTransform NewRect(string name, Transform parent) {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.transform as RectTransform;
    }

    public static void Stretch(RectTransform rect, float inset = 0f) {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }

    public static Image NewImage(string name, Transform parent, Color color) {
        var image = NewRect(name, parent).gameObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    public static LayoutElement Layout(Component component, float width = -1f, float height = -1f, float flexibleWidth = -1f) {
        // Not ??: a missing component can come back as Unity's fake null
        var element = component.GetComponent<LayoutElement>();
        if (element == null) {
            element = component.gameObject.AddComponent<LayoutElement>();
        }
        if (width >= 0f) {
            element.minWidth = element.preferredWidth = width;
        }
        if (height >= 0f) {
            element.minHeight = element.preferredHeight = height;
        }
        if (flexibleWidth >= 0f) {
            element.flexibleWidth = flexibleWidth;
        }
        return element;
    }

    public static TextMeshProUGUI Text(Transform parent, string text, float size = FONT_SIZE, Color? color = null,
        TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft) {
        var tmp = NewRect("Text", parent).gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color ?? TextColor;
        tmp.alignment = alignment;
        tmp.raycastTarget = false;
        return tmp;
    }

    /// <summary>
    /// A line of text across the list, wrapping as needed
    /// </summary>
    public static TextMeshProUGUI Label(Transform parent, string text, float size = FONT_SIZE, Color? color = null) {
        var tmp = Text(parent, text, size, color);
        tmp.enableWordWrapping = true;
        return tmp;
    }

    public static TextMeshProUGUI Header(Transform parent, string text) {
        var header = Label(parent, text, FONT_SIZE + 1f, AccentColor);
        header.fontStyle = FontStyles.Bold;
        header.margin = new Vector4(0f, 8f, 0f, 2f);
        return header;
    }

    /// <summary>
    /// A row of controls, left to right
    /// </summary>
    public static RectTransform Row(Transform parent, float height = ROW_HEIGHT) {
        var rect = NewRect("Row", parent);
        var layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        Layout(rect, height: height, flexibleWidth: 1f);
        return rect;
    }

    public static Button Button(Transform parent, string text, UnityAction onClick, float width = -1f, Color? color = null) {
        var image = NewImage("Button", parent, color ?? ControlColor);
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        if (onClick != null) {
            button.onClick.AddListener(onClick);
        }

        var label = Text(image.transform, text, FONT_SIZE, TextColor, TextAlignmentOptions.Center);
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Ellipsis;
        Stretch(label.rectTransform, 4f);

        Layout(image, width, CONTROL_HEIGHT, width < 0f ? 1f : 0f);
        return button;
    }

    public static void SetButtonText(Button button, string text) {
        button.GetComponentInChildren<TextMeshProUGUI>().text = text;
    }

    /// <summary>
    /// A button showing what's picked, which opens a picker
    /// </summary>
    public static Button Select(Transform parent, string current, UnityAction onClick, float width = -1f) {
        var button = Button(parent, current + "  ▼", onClick, width);
        button.GetComponentInChildren<TextMeshProUGUI>().alignment = TextAlignmentOptions.MidlineLeft;
        return button;
    }

    /// <summary>
    /// A check box with its label; the whole row takes the tap
    /// </summary>
    public static Toggle Toggle(Transform parent, string label, bool value, Action<bool> onChange, bool interactable = true) {
        var row = Row(parent);
        // Makes the row, not only the box, a tap target
        row.gameObject.AddComponent<Image>().color = Color.clear;

        var box = NewImage("Box", row, ControlColor);
        Layout(box, 24f, 24f);
        var check = NewImage("Check", box.transform, AccentColor);
        Stretch(check.rectTransform, 5f);

        var text = Text(row, label, FONT_SIZE, interactable ? TextColor : DimTextColor);
        text.enableWordWrapping = true;
        Layout(text, flexibleWidth: 1f);

        var toggle = row.gameObject.AddComponent<Toggle>();
        toggle.targetGraphic = box;
        toggle.graphic = check;
        toggle.isOn = value;
        toggle.interactable = interactable;
        toggle.onValueChanged.AddListener(v => onChange(v));
        return toggle;
    }

    /// <summary>
    /// A slider with its label and value
    /// </summary>
    public static Slider Slider(Transform parent, string label, int min, int max, int value, Func<int, string> format, Action<int> onChange, bool interactable = true) {
        var row = Row(parent);
        var text = Text(row, label, FONT_SIZE, interactable ? TextColor : DimTextColor);
        text.enableWordWrapping = true;
        Layout(text, flexibleWidth: 1f);

        var sliderRect = NewRect("Slider", row);
        Layout(sliderRect, 190f, 26f);

        var background = NewImage("Background", sliderRect, ControlColor);
        Stretch(background.rectTransform);
        background.rectTransform.offsetMin = new Vector2(0f, 9f);
        background.rectTransform.offsetMax = new Vector2(0f, -9f);

        var fillArea = NewRect("Fill Area", sliderRect);
        Stretch(fillArea);
        fillArea.offsetMin = new Vector2(0f, 9f);
        fillArea.offsetMax = new Vector2(0f, -9f);
        var fill = NewImage("Fill", fillArea, SelectedColor);
        fill.rectTransform.sizeDelta = Vector2.zero;

        var handleArea = NewRect("Handle Area", sliderRect);
        Stretch(handleArea);
        handleArea.offsetMin = new Vector2(10f, 0f);
        handleArea.offsetMax = new Vector2(-10f, 0f);
        var handle = NewImage("Handle", handleArea, AccentColor);
        handle.rectTransform.sizeDelta = new Vector2(20f, 0f);

        var valueText = Text(row, format(value), FONT_SIZE, AccentColor, TextAlignmentOptions.MidlineRight);
        Layout(valueText, 72f);

        var slider = sliderRect.gameObject.AddComponent<Slider>();
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
        slider.minValue = min;
        slider.maxValue = max;
        slider.wholeNumbers = true;
        slider.value = Mathf.Clamp(value, min, max);
        slider.interactable = interactable;
        slider.onValueChanged.AddListener(v => {
            valueText.text = format((int) v);
            onChange((int) v);
        });
        return slider;
    }

    /// <summary>
    /// − value + (skill levels and the like)
    /// </summary>
    public static void Stepper(Transform row, int min, int max, int value, Func<int, string> format, Action<int> onChange) {
        TextMeshProUGUI valueText = null;
        var current = Mathf.Clamp(value, min, max);
        void Change(int delta) {
            var next = Mathf.Clamp(current + delta, min, max);
            if (next == current) {
                return;
            }
            current = next;
            valueText.text = format(current);
            onChange(current);
        }

        Button(row, "-", () => Change(-1), 34f);
        valueText = Text(row, format(current), FONT_SIZE, TextColor, TextAlignmentOptions.Center);
        Layout(valueText, 64f);
        Button(row, "+", () => Change(1), 34f);
    }

    /// <summary>
    /// A number box; the value is taken when editing ends
    /// </summary>
    public static TMP_InputField NumberField(Transform parent, int value, int min, int max, Action<int> onChange, float width = 90f) {
        var background = NewImage("Input", parent, ControlColor);
        Layout(background, width, CONTROL_HEIGHT);
        // Built inactive: the field sets its caret up when enabled, which needs its text in place
        background.gameObject.SetActive(false);

        var area = NewRect("Text Area", background.transform);
        Stretch(area, 5f);
        area.gameObject.AddComponent<RectMask2D>();
        var text = Text(area, "", FONT_SIZE);
        text.enableWordWrapping = false;
        Stretch(text.rectTransform);

        var input = background.gameObject.AddComponent<TMP_InputField>();
        input.targetGraphic = background;
        input.textViewport = area;
        input.textComponent = text;
        input.contentType = TMP_InputField.ContentType.IntegerNumber;
        input.characterLimit = 9;
        input.text = value.ToString();
        input.onEndEdit.AddListener(entered => {
            int.TryParse(entered, out var number);
            number = Mathf.Clamp(number, min, max);
            input.SetTextWithoutNotify(number.ToString());
            onChange(number);
        });

        background.gameObject.SetActive(true);
        return input;
    }

    /// <summary>
    /// A list that scrolls: vertical (the content) or horizontal (the tabs)
    /// </summary>
    public static RectTransform ScrollList(Transform parent, bool vertical, out ScrollRect scroll, float spacing = 6f, RectOffset padding = null) {
        var view = NewRect("Scroll", parent);
        var viewport = NewImage("Viewport", view, Color.clear);
        Stretch(viewport.rectTransform);
        viewport.gameObject.AddComponent<RectMask2D>();

        var content = NewRect("Content", viewport.transform);
        HorizontalOrVerticalLayoutGroup layout;
        var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        if (vertical) {
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        } else {
            content.anchorMin = new Vector2(0f, 0f);
            content.anchorMax = new Vector2(0f, 1f);
            content.pivot = new Vector2(0f, 0.5f);
            layout = content.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        }
        content.sizeDelta = Vector2.zero;
        layout.spacing = spacing;
        layout.padding = padding ?? new RectOffset(0, 0, 0, 0);

        scroll = view.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport.rectTransform;
        scroll.content = content;
        scroll.horizontal = !vertical;
        scroll.vertical = vertical;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;
        return content;
    }

    public static void Clear(Transform parent) {
        for (var i = parent.childCount - 1; i >= 0; i--) {
            var child = parent.GetChild(i).gameObject;
            child.SetActive(false);
            UnityEngine.Object.Destroy(child);
        }
    }

    /// <summary>
    /// A list to pick one from, over the window: "none" (value 0) first when allowed. Tapping
    /// outside or 取消 picks nothing.
    /// </summary>
    public static void Pick(RectTransform host, string title, List<KeyValuePair<string, int>> options, int current, bool allowNone, Action<int> onPick) {
        var overlay = NewImage("Picker", host, new Color(0f, 0f, 0f, 0.55f));
        Stretch(overlay.rectTransform);
        overlay.gameObject.AddComponent<Button>().onClick.AddListener(() => UnityEngine.Object.Destroy(overlay.gameObject));

        var hostSize = host.rect.size;
        var panel = NewImage("Panel", overlay.transform, PanelColor);
        // The "nothing to pick" line takes a row too
        var rows = options.Count + (allowNone ? 1 : 0) + (options.Count == 0 ? 1 : 0);
        var width = Mathf.Min(420f, hostSize.x - 40f);
        var height = Mathf.Min(hostSize.y - 30f, 96f + Mathf.Max(rows, 1) * (PICKER_ROW_HEIGHT + 4f));
        panel.rectTransform.sizeDelta = new Vector2(width, height);
        // Taps on the panel stay on it
        panel.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;

        var titleText = Text(panel.transform, title, FONT_SIZE + 1f, AccentColor, TextAlignmentOptions.Center);
        titleText.rectTransform.anchorMin = new Vector2(0f, 1f);
        titleText.rectTransform.anchorMax = new Vector2(1f, 1f);
        titleText.rectTransform.pivot = new Vector2(0.5f, 1f);
        titleText.rectTransform.sizeDelta = new Vector2(0f, 36f);

        var list = ScrollList(panel.transform, true, out var scroll, 4f, new RectOffset(10, 10, 4, 4));
        var scrollRect = scroll.transform as RectTransform;
        Stretch(scrollRect);
        scrollRect.offsetMax = new Vector2(0f, -38f);
        scrollRect.offsetMin = new Vector2(0f, 50f);

        void Choose(int value) {
            UnityEngine.Object.Destroy(overlay.gameObject);
            onPick(value);
        }

        if (allowNone) {
            var none = Button(list, "-- 未選擇 --", () => Choose(0), -1f, current == 0 ? SelectedColor : ControlColor);
            Layout(none, height: PICKER_ROW_HEIGHT);
        }
        if (options.Count == 0) {
            Label(list, "(沒有可選的項目)", FONT_SIZE, DimTextColor).alignment = TextAlignmentOptions.Center;
        }
        foreach (var option in options) {
            var value = option.Value;
            var button = Button(list, option.Key, () => Choose(value), -1f, value == current ? SelectedColor : ControlColor);
            button.GetComponentInChildren<TextMeshProUGUI>().alignment = TextAlignmentOptions.MidlineLeft;
            Layout(button, height: PICKER_ROW_HEIGHT);
        }

        var cancel = Button(panel.transform, "取消", () => UnityEngine.Object.Destroy(overlay.gameObject), 140f);
        var cancelRect = cancel.transform as RectTransform;
        cancelRect.anchorMin = cancelRect.anchorMax = new Vector2(0.5f, 0f);
        cancelRect.pivot = new Vector2(0.5f, 0f);
        cancelRect.anchoredPosition = new Vector2(0f, 8f);
        cancelRect.sizeDelta = new Vector2(140f, 36f);
    }
}
