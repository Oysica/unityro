using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A skill shortcut darkened while its skill can't be used yet, the shade winding back like a
/// clock, with the seconds left
/// </summary>
public class SkillCooldownOverlay : MonoBehaviour {

    private static Sprite Square;

    private Func<int> SkillOf;
    private Image Shade;
    private TextMeshProUGUI Seconds;
    private int ShownSeconds = -1;

    /// <param name="skillOf">The skill the shortcut holds now, 0 for none (or an item)</param>
    /// <param name="shape">The shortcut's outline for the shade (a round button's own picture), a square by default</param>
    public static SkillCooldownOverlay Add(RectTransform shortcut, Func<int> skillOf, Sprite shape = null, float fontSize = 12f) {
        var root = new GameObject("Cooldown", typeof(RectTransform));
        root.transform.SetParent(shortcut, false);
        Stretch(root.transform as RectTransform);

        var overlay = root.AddComponent<SkillCooldownOverlay>();
        overlay.SkillOf = skillOf;

        overlay.Shade = new GameObject("Shade", typeof(RectTransform)).AddComponent<Image>();
        overlay.Shade.transform.SetParent(root.transform, false);
        Stretch(overlay.Shade.rectTransform);
        overlay.Shade.sprite = shape != null ? shape : SquareSprite();
        overlay.Shade.color = new Color(0f, 0f, 0f, 0.6f);
        overlay.Shade.type = Image.Type.Filled;
        overlay.Shade.fillMethod = Image.FillMethod.Radial360;
        overlay.Shade.fillOrigin = (int) Image.Origin360.Top;
        overlay.Shade.fillClockwise = false;
        overlay.Shade.raycastTarget = false;

        overlay.Seconds = new GameObject("Seconds", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        overlay.Seconds.transform.SetParent(root.transform, false);
        Stretch(overlay.Seconds.rectTransform);
        overlay.Seconds.fontSize = fontSize;
        overlay.Seconds.fontStyle = FontStyles.Bold;
        overlay.Seconds.color = Color.white;
        overlay.Seconds.alignment = TextAlignmentOptions.Center;
        overlay.Seconds.enableWordWrapping = false;
        overlay.Seconds.raycastTarget = false;
        overlay.Seconds.outlineWidth = 0.25f;
        overlay.Seconds.outlineColor = new Color32(0, 0, 0, 220);

        overlay.Show(false);
        return overlay;
    }

    private void Update() {
        var skillId = SkillOf != null ? SkillOf() : 0;
        var share = 0f;
        var remaining = skillId > 0 ? SkillCooldowns.Remaining(skillId, out share) : 0f;

        var waiting = remaining > 0f;
        Show(waiting);
        if (!waiting) {
            return;
        }

        Shade.fillAmount = share;
        var seconds = Mathf.CeilToInt(remaining);
        if (seconds != ShownSeconds) {
            ShownSeconds = seconds;
            Seconds.text = seconds.ToString();
        }
    }

    private void Show(bool show) {
        if (Shade.gameObject.activeSelf != show) {
            Shade.gameObject.SetActive(show);
            Seconds.gameObject.SetActive(show);
        }
        if (!show) {
            ShownSeconds = -1;
        }
    }

    private static void Stretch(RectTransform rect) {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static Sprite SquareSprite() {
        if (Square == null) {
            var texture = Texture2D.whiteTexture;
            Square = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
        }
        return Square;
    }
}
