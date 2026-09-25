using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A modal message with an OK button, built at runtime so any scene can show one: the login
/// and server select scenes have no dialog of their own. It survives a scene load, so a caller
/// can show it and then go back to the login scene.
/// </summary>
public class SystemMessageBox : MonoBehaviour {

    private Action OnClose;

    public static void Show(string message, Action onClose = null) {
        var root = new GameObject("SystemMessageBox", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        DontDestroyOnLoad(root);
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;
        var box = root.AddComponent<SystemMessageBox>();
        box.OnClose = onClose;

        // Legacy Text with the built-in Arial, which falls back to the OS fonts for Chinese
        var font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        var blocker = CreateImage(root.transform, "Blocker", new Color(0f, 0f, 0f, 0.4f));
        Stretch(blocker.rectTransform, Vector2.zero, Vector2.one);

        var panel = CreateImage(blocker.transform, "Panel", new Color(0.97f, 0.97f, 0.97f, 1f));
        panel.rectTransform.sizeDelta = new Vector2(380f, 150f);

        var text = new GameObject("Message", typeof(RectTransform)).AddComponent<Text>();
        text.transform.SetParent(panel.transform, false);
        text.font = font;
        text.fontSize = 14;
        text.color = Color.black;
        text.alignment = TextAnchor.MiddleCenter;
        text.text = message;
        Stretch(text.rectTransform, new Vector2(0.05f, 0.35f), new Vector2(0.95f, 0.95f));

        var button = CreateImage(panel.transform, "OK", new Color(0.85f, 0.85f, 0.85f, 1f));
        button.rectTransform.anchorMin = button.rectTransform.anchorMax = new Vector2(0.5f, 0.15f);
        button.rectTransform.sizeDelta = new Vector2(90f, 26f);
        button.gameObject.AddComponent<Button>().onClick.AddListener(box.Close);

        var label = new GameObject("Label", typeof(RectTransform)).AddComponent<Text>();
        label.transform.SetParent(button.transform, false);
        label.font = font;
        label.fontSize = 13;
        label.color = Color.black;
        label.alignment = TextAnchor.MiddleCenter;
        label.text = "OK";
        Stretch(label.rectTransform, Vector2.zero, Vector2.one);
    }

    private void Update() {
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Escape)) {
            Close();
        }
    }

    private void Close() {
        Destroy(gameObject);
        OnClose?.Invoke();
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
