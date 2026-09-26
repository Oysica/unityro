using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The line over the game while a skill waits for where it goes: what to click or tap, and how
/// to let it go
/// </summary>
public static class SkillTargetHint {

    private static GameObject Banner;
    private static TextMeshProUGUI Label;

    public static void Show(string text) {
        var ui = MapUiController.Instance;
        if (ui == null) {
            return;
        }

        // Made again with a new map UI
        if (Banner == null) {
            Build(ui.transform);
        }
        Label.text = text;
        Banner.transform.SetAsLastSibling();
        Banner.SetActive(true);
    }

    public static void Hide() {
        if (Banner != null) {
            Banner.SetActive(false);
        }
    }

    private static void Build(Transform parent) {
        var background = new GameObject("Skill Target Hint", typeof(RectTransform)).AddComponent<Image>();
        background.transform.SetParent(parent, false);
        background.color = new Color(0f, 0f, 0f, 0.65f);
        background.raycastTarget = false;

        var rect = background.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        // Under the shortcut bar
        rect.anchoredPosition = new Vector2(0f, -40f);

        var layout = background.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 4, 5);
        layout.childControlWidth = layout.childControlHeight = true;
        var fitter = background.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Label = new GameObject("Label", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        Label.transform.SetParent(background.transform, false);
        Label.fontSize = 14f;
        Label.color = new Color(1f, 0.92f, 0.55f);
        Label.alignment = TextAlignmentOptions.Center;
        Label.enableWordWrapping = false;
        Label.raycastTarget = false;
        Label.outlineWidth = 0.15f;
        Label.outlineColor = new Color32(0, 0, 0, 200);

        Banner = background.gameObject;
    }
}
