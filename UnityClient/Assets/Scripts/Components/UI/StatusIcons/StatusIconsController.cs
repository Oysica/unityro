using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// The column of status icons (buffs, debuffs, toggles) of the player's character at the right of
/// the screen, with the status' description on hover. The server ends statuses itself; the local
/// countdown only drives the tooltip and the blinking before one runs out.
/// </summary>
public class StatusIconsController : MonoBehaviour {

    private const float ICON_SIZE = 24f;
    private const float BLINK_BELOW_SECONDS = 10f;

    private class StatusIcon {
        public int Efst;
        public int Priority;
        public int Order;
        public bool HasTimeLimit;
        public float EndTime;
        public RawImage Image;

        public float RemainSeconds => HasTimeLimit ? Mathf.Max(0f, EndTime - Time.realtimeSinceStartup) : 0f;
    }

    private readonly Dictionary<int, StatusIcon> Icons = new Dictionary<int, StatusIcon>();
    private int NextOrder;

    // The icon under the mouse, whose tooltip counts down while it stays there
    private int HoveredEfst = -1;
    private int ShownSeconds;

    /// <summary>
    /// Built at runtime: the map UI prefab has no place for it.
    /// </summary>
    public static StatusIconsController Create(Transform parent) {
        var root = new GameObject("Status Icons", typeof(RectTransform));
        root.transform.SetParent(parent, false);

        // Top right, under the minimap
        var rect = root.transform as RectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-8f, -150f);
        rect.sizeDelta = new Vector2(ICON_SIZE, 0f);

        var layout = root.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 2f;
        layout.childAlignment = TextAnchor.UpperRight;
        layout.childControlWidth = layout.childControlHeight = false;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;

        return root.AddComponent<StatusIconsController>();
    }

    private void Awake() {
        var networkClient = FindObjectOfType<NetworkClient>();
        networkClient.HookPacket(ZC.MSG_STATE_CHANGE3.HEADER, OnStateChange);
        networkClient.HookPacket(ZC.MSG_STATE_CHANGE.HEADER, OnStateChange);
        networkClient.HookPacket(ZC.EFST_SET_ENTER.HEADER, OnStateChange);
    }

    private void OnStateChange(ushort cmd, int size, InPacket packet) {
        switch (packet) {
            case ZC.MSG_STATE_CHANGE3 change when IsMine(change.AID):
                if (change.State == 1) {
                    Show(change.Type, change.RemainMs);
                } else {
                    Hide(change.Type);
                }
                break;
            case ZC.MSG_STATE_CHANGE change when IsMine(change.AID):
                if (change.State == 1) {
                    Show(change.Type, 0);
                } else {
                    Hide(change.Type);
                }
                break;
            case ZC.EFST_SET_ENTER enter when IsMine(enter.AID):
                Show(enter.Type, enter.RemainMs);
                break;
        }
    }

    private static bool IsMine(uint id) {
        return Session.CurrentSession != null && id == (uint) Session.CurrentSession.AccountID;
    }

    private void Show(int efst, int remainMs) {
        var texture = StatusIconTable.GetIcon(efst, out var priority);
        if (texture == null) {
            return;
        }

        if (!Icons.TryGetValue(efst, out var icon)) {
            icon = new StatusIcon { Efst = efst, Priority = priority, Order = NextOrder++, Image = CreateImage(efst) };
            Icons[efst] = icon;
        }

        icon.Image.texture = texture;
        icon.HasTimeLimit = remainMs > 0 && StatusIconTable.HasTimeLimit(efst);
        icon.EndTime = Time.realtimeSinceStartup + remainMs / 1000f;
        Sort();
    }

    private void Hide(int efst) {
        if (Icons.TryGetValue(efst, out var icon)) {
            Destroy(icon.Image.gameObject);
            Icons.Remove(efst);
            if (HoveredEfst == efst) {
                HoveredEfst = -1;
                MapUiController.Instance.HideTooltip();
            }
        }
    }

    private RawImage CreateImage(int efst) {
        var image = new GameObject($"EFST {efst}", typeof(RectTransform)).AddComponent<RawImage>();
        image.transform.SetParent(transform, false);
        image.rectTransform.sizeDelta = new Vector2(ICON_SIZE, ICON_SIZE);

        var trigger = image.gameObject.AddComponent<EventTrigger>();
        AddTrigger(trigger, EventTriggerType.PointerEnter, () => {
            HoveredEfst = efst;
            ShowTooltip(efst);
        });
        AddTrigger(trigger, EventTriggerType.PointerExit, () => {
            HoveredEfst = -1;
            MapUiController.Instance.HideTooltip();
        });
        return image;
    }

    private static void AddTrigger(EventTrigger trigger, EventTriggerType type, System.Action action) {
        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(_ => action());
        trigger.triggers.Add(entry);
    }

    private void ShowTooltip(int efst) {
        if (!Icons.TryGetValue(efst, out var icon)) {
            return;
        }

        ShownSeconds = Mathf.CeilToInt(icon.RemainSeconds);
        var text = StatusIconTable.GetDescription(efst, icon.RemainSeconds);
        if (string.IsNullOrEmpty(text)) {
            return;
        }

        // To the left of the column, its top right corner at the icon's top left
        var rect = icon.Image.rectTransform;
        var corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        MapUiController.Instance.DisplayTooltip(text, corners[1], new Vector2(1f, 1f));
    }

    private void Sort() {
        var index = 0;
        foreach (var icon in Icons.Values.OrderBy(it => it.Priority).ThenBy(it => it.Order)) {
            icon.Image.transform.SetSiblingIndex(index++);
        }
    }

    private void Update() {
        // Blink while a timed status is about to run out
        foreach (var icon in Icons.Values) {
            var alpha = 1f;
            if (icon.HasTimeLimit && icon.RemainSeconds < BLINK_BELOW_SECONDS) {
                alpha = 0.35f + 0.65f * Mathf.Abs(Mathf.Cos(Time.realtimeSinceStartup * Mathf.PI));
            }
            icon.Image.color = new Color(1f, 1f, 1f, alpha);
        }

        // Count the tooltip down while the mouse stays on its icon
        if (HoveredEfst >= 0 && Icons.TryGetValue(HoveredEfst, out var hovered) && hovered.HasTimeLimit
            && Mathf.CeilToInt(hovered.RemainSeconds) != ShownSeconds) {
            ShowTooltip(HoveredEfst);
        }
    }
}
