using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Asks how many of a stack to move, like the official client does when storing or taking items.
/// Built at runtime in the style of <see cref="SystemMessageBox"/>; the shop's NumberInput only
/// exists inside the shop prefab.
/// </summary>
public class AmountInputBox : MonoBehaviour {

    private InputField Input;
    private int Max;
    private Action<int> OnConfirm;

    public static void Show(string label, int max, Action<int> onConfirm) {
        var root = new GameObject("AmountInputBox", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;
        var box = root.AddComponent<AmountInputBox>();
        box.Max = max;
        box.OnConfirm = onConfirm;

        var font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        var blocker = CreateImage(root.transform, "Blocker", new Color(0f, 0f, 0f, 0.25f));
        Stretch(blocker.rectTransform, Vector2.zero, Vector2.one);

        var panel = CreateImage(blocker.transform, "Panel", new Color(0.97f, 0.97f, 0.97f, 1f));
        panel.rectTransform.sizeDelta = new Vector2(300f, 120f);

        var text = CreateText(panel.transform, font, label, 14, TextAnchor.MiddleCenter);
        Stretch(text.rectTransform, new Vector2(0.05f, 0.68f), new Vector2(0.95f, 0.95f));

        var field = CreateImage(panel.transform, "Amount", Color.white);
        Stretch(field.rectTransform, new Vector2(0.3f, 0.4f), new Vector2(0.7f, 0.62f));
        var fieldText = CreateText(field.transform, font, "", 14, TextAnchor.MiddleCenter);
        Stretch(fieldText.rectTransform, Vector2.zero, Vector2.one);
        fieldText.supportRichText = false;
        box.Input = field.gameObject.AddComponent<InputField>();
        box.Input.textComponent = fieldText;
        box.Input.contentType = InputField.ContentType.IntegerNumber;
        box.Input.text = $"{max}";

        CreateButton(panel.transform, font, "OK", new Vector2(0.3f, 0.17f), box.Confirm);
        CreateButton(panel.transform, font, "Cancel", new Vector2(0.7f, 0.17f), box.Close);

        box.Input.Select();
        box.Input.ActivateInputField();
    }

    private void Update() {
        if (UnityEngine.Input.GetKeyDown(KeyCode.Return) || UnityEngine.Input.GetKeyDown(KeyCode.KeypadEnter)) {
            Confirm();
        } else if (UnityEngine.Input.GetKeyDown(KeyCode.Escape)) {
            Close();
        }
    }

    private void Confirm() {
        if (int.TryParse(Input.text, out var amount) && amount > 0) {
            OnConfirm?.Invoke(Mathf.Min(amount, Max));
        }
        Close();
    }

    private void Close() {
        Destroy(gameObject);
    }

    private static void CreateButton(Transform parent, Font font, string label, Vector2 anchor, UnityEngine.Events.UnityAction onClick) {
        var button = CreateImage(parent, label, new Color(0.85f, 0.85f, 0.85f, 1f));
        button.rectTransform.anchorMin = button.rectTransform.anchorMax = anchor;
        button.rectTransform.sizeDelta = new Vector2(80f, 24f);
        button.gameObject.AddComponent<Button>().onClick.AddListener(onClick);

        var text = CreateText(button.transform, font, label, 13, TextAnchor.MiddleCenter);
        Stretch(text.rectTransform, Vector2.zero, Vector2.one);
    }

    private static Text CreateText(Transform parent, Font font, string content, int size, TextAnchor alignment) {
        var text = new GameObject("Text", typeof(RectTransform)).AddComponent<Text>();
        text.transform.SetParent(parent, false);
        text.font = font;
        text.fontSize = size;
        text.color = Color.black;
        text.alignment = alignment;
        text.text = content;
        return text;
    }

    private static Image CreateImage(Transform parent, string name, Color color) {
        var image = new GameObject(name, typeof(RectTransform)).AddComponent<Image>();
        image.transform.SetParent(parent, false);
        image.color = color;
        return image;
    }

    private static void Stretch(RectTransform rect, Vector2 min, Vector2 max) {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
