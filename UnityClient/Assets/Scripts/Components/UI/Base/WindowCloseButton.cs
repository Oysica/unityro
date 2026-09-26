using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// The X at the right end of a window's title bar (basic_interface/sys_close_*), as in the
/// official client.
/// </summary>
public static class WindowCloseButton {

    private const float ICON_SIZE = 12f;
    // Larger than the picture, which is too small to hit with a finger
    private const float HIT_SIZE = 24f;

    /// <summary>
    /// Adds it to the window's "Title Bar" (the window itself when it has none); once only
    /// </summary>
    public static Button Add(Transform window, UnityAction onClick) {
        var titleBar = window.GetComponentsInChildren<Transform>(true).FirstOrDefault(it => it.name.StartsWith("Title Bar"));
        if (titleBar == null) {
            titleBar = window;
        }
        var existing = titleBar.Find("Close");
        if (existing != null) {
            return existing.GetComponent<Button>();
        }

        var hit = new GameObject("Close", typeof(RectTransform)).AddComponent<Image>();
        hit.transform.SetParent(titleBar, false);
        hit.color = Color.clear;
        var hitRect = hit.rectTransform;
        hitRect.anchorMin = hitRect.anchorMax = hitRect.pivot = new Vector2(1f, 0.5f);
        hitRect.anchoredPosition = Vector2.zero;
        hitRect.sizeDelta = new Vector2(HIT_SIZE, HIT_SIZE);

        var off = TextureAssetLoader.Load($"{DBManager.INTERFACE_PATH}basic_interface/sys_close_off.png");
        var on = TextureAssetLoader.Load($"{DBManager.INTERFACE_PATH}basic_interface/sys_close_on.png");
        var icon = new GameObject("Icon", typeof(RectTransform)).AddComponent<RawImage>();
        icon.transform.SetParent(hitRect, false);
        icon.raycastTarget = false;
        var iconRect = icon.rectTransform;
        iconRect.anchorMin = iconRect.anchorMax = iconRect.pivot = new Vector2(1f, 0.5f);
        iconRect.anchoredPosition = new Vector2(-3f, 0f);
        iconRect.sizeDelta = new Vector2(ICON_SIZE, ICON_SIZE);
        if (off != null) {
            icon.texture = off;
        } else {
            // No picture to be had: a plain X
            icon.color = Color.clear;
            var x = new GameObject("X", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            x.transform.SetParent(iconRect, false);
            x.text = "X";
            x.fontSize = 11f;
            x.alignment = TextAlignmentOptions.Center;
            x.raycastTarget = false;
            x.rectTransform.anchorMin = Vector2.zero;
            x.rectTransform.anchorMax = Vector2.one;
            x.rectTransform.offsetMin = x.rectTransform.offsetMax = Vector2.zero;
        }

        // Lit while the pointer is over it
        if (off != null && on != null) {
            var hover = hit.gameObject.AddComponent<Hover>();
            hover.Icon = icon;
            hover.Off = off;
            hover.On = on;
        }

        var button = hit.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = hit;
        button.onClick.AddListener(onClick);
        return button;
    }

    // Only enter and exit: a drag starting on the X still moves the window
    private class Hover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler {
        public RawImage Icon;
        public Texture2D Off;
        public Texture2D On;

        public void OnPointerEnter(PointerEventData eventData) {
            Icon.texture = On;
        }

        public void OnPointerExit(PointerEventData eventData) {
            Icon.texture = Off;
        }

        private void OnDisable() {
            // Closing leaves the pointer over it: it would open lit
            if (Icon != null) {
                Icon.texture = Off;
            }
        }
    }
}
