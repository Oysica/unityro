using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// The touch screen controls over the map (see MobileControls): a joystick bottom left, the attack
/// button bottom right with the first shortcut bar slots around it, and buttons to pick a target,
/// pick up and sit. Built at runtime like the status icons; shown while MobileControls is on.
/// </summary>
public class MobileControlsController : MonoBehaviour {

    private const int SKILL_BUTTONS = 5;

    // Walk this many cells ahead and ask again when this few are left, so the walk never stops
    // in between; each request takes effect once the current step is done
    private const int WALK_AHEAD_CELLS = 5;
    private const int WALK_AGAIN_CELLS = 2;
    private const float WALK_RETRY_SECONDS = 0.5f;
    private const float JOYSTICK_DEAD_ZONE = 0.3f;

    // How often auto lock looks for a monster, and a held attack button for the next one
    private const float AUTO_LOCK_SECONDS = 0.25f;
    private const float ATTACK_HOLD_SECONDS = 0.3f;
    // The ring under the target spans this many cells
    private const float TARGET_RING_CELLS = 2.2f;

    // Layout, in canvas units from the corners
    private static readonly Vector2 BottomLeft = new Vector2(0f, 0f);
    private static readonly Vector2 BottomRight = new Vector2(1f, 0f);
    private static readonly Vector2 JoystickCenter = new Vector2(170f, 165f);
    private const float JOYSTICK_SIZE = 200f;
    private const float KNOB_SIZE = 90f;
    private static readonly Vector2 AttackCenter = new Vector2(-130f, 130f);
    private const float ATTACK_SIZE = 140f;
    private const float SKILL_SIZE = 78f;
    private const float SKILL_ARC_RADIUS = 178f;
    private const float SKILL_ARC_START = 90f;
    private const float SKILL_ARC_STEP = 28f;
    private const float SKILL_ICON_SIZE = 46f;
    private const float UTILITY_SIZE = 62f;
    private const float UTILITY_ROW_Y = 392f;
    private static readonly float[] UtilityColumnsX = { -75f, -145f, -215f, -285f, -355f };
    // Share of a utility button its picture takes up
    private const float ICON_FILL = 0.56f;
    private const float CHAT_MAX_WIDTH = 440f;
    private const float CHAT_MIN_WIDTH = 300f;
    private const float CHAT_SIDE_MARGIN = 350f;
    private const float CHAT_HEIGHT = 170f;
    private const float CHAT_INPUT_HEIGHT = 28f;
    private const float CHAT_PM_WIDTH = 80f;
    private const float CHAT_INPUT_POINT_SIZE = 16f;

    // Light, so the stick shows on dark ground as well
    private static readonly Color BaseColor = new Color(1f, 1f, 1f, 0.2f);
    private static readonly Color KnobColor = new Color(1f, 1f, 1f, 0.55f);
    private static readonly Color ButtonColor = new Color(0.08f, 0.08f, 0.1f, 0.55f);
    private static readonly Color AttackColor = new Color(0.6f, 0.12f, 0.1f, 0.7f);
    private static readonly Color AutoLockOnColor = new Color(0.15f, 0.5f, 0.2f, 0.75f);
    private static readonly Color TargetRingColor = new Color(1f, 0.25f, 0.15f, 0.9f);

    // Art for the controls (PixelLab), one sprite per piece; a plain tinted circle stands in for
    // any that's missing
    private const string ART_PATH = "Textures/MobileControls/";
    private static readonly Color ArtColor = new Color(1f, 1f, 1f, 0.92f);

    private static Sprite CircleSprite;
    private static Sprite RingSprite;
    private static MobileControlsController Instance;

    private MapUiController UI;
    private HotkeyBarController HotkeyBar;
    private VirtualJoystick Joystick;
    private readonly RawImage[] SkillIcons = new RawImage[SKILL_BUTTONS];
    private readonly HashSet<GameObject> SkillButtons = new HashSet<GameObject>();

    private Image AutoLockButton;
    private Image AutoAttackButton;
    private RectTransform TargetRing;
    private bool AttackHeld;
    private Entity AttackedTarget;
    private float NextAttackCheck;
    private float NextAutoLock;

    private Vector2Int WalkStep;
    private Vector2Int? WalkTarget;
    private float LastWalkRequest;
    private bool IsWalking;

    private readonly Dictionary<RectTransform, RectState> DesktopChat = new Dictionary<RectTransform, RectState>();
    private float DesktopChatPointSize;
    private float ChatLaidOutWidth;

    public static MobileControlsController Create(MapUiController ui) {
        var root = new GameObject("Mobile Controls", typeof(RectTransform));
        var rect = root.transform as RectTransform;
        rect.SetParent(ui.transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        // Under every window and dialog
        rect.SetSiblingIndex(ui.ChatBox.transform.GetSiblingIndex() + 1);

        var controller = root.AddComponent<MobileControlsController>();
        controller.UI = ui;
        controller.Build();
        controller.OnMobileControlsChanged(MobileControls.Enabled);
        return controller;
    }

    /// <summary>
    /// A shortcut dropped here went to its slot: the slot it came from must not clear itself
    /// </summary>
    public static bool IsHotkeyButton(GameObject target) {
        if (Instance == null || target == null) {
            return false;
        }
        for (var transform = target.transform; transform != null; transform = transform.parent) {
            if (Instance.SkillButtons.Contains(transform.gameObject)) {
                return true;
            }
        }
        return false;
    }

    private void Awake() {
        Instance = this;
        MobileControls.Changed += OnMobileControlsChanged;
        AutoAttackWindow.StatusChanged += OnAutoAttackStatus;
    }

    private void OnDestroy() {
        MobileControls.Changed -= OnMobileControlsChanged;
        AutoAttackWindow.StatusChanged -= OnAutoAttackStatus;
        if (Instance == this) {
            Instance = null;
        }
    }

    private void OnMobileControlsChanged(bool enabled) {
        gameObject.SetActive(enabled);
        LayoutChat(enabled);
    }

    private void Update() {
        // The canvas is sized after it's created, and again when the screen changes
        var canvasWidth = (UI.transform as RectTransform).rect.width;
        if (!Mathf.Approximately(canvasWidth, ChatLaidOutWidth)) {
            LayoutChat(true);
        }

        RefreshSkillIcons();
        Walk();
        KeepTarget();
    }

    private void LateUpdate() {
        PlaceTargetRing();
    }

    #region Building

    private void Build() {
        HotkeyBar = UI.GetComponentInChildren<HotkeyBarController>(true);

        // Under the buttons, on the target's feet
        var ring = new GameObject("Target Ring", typeof(RectTransform)).AddComponent<Image>();
        ring.transform.SetParent(transform, false);
        ring.sprite = GetRingSprite();
        ring.color = TargetRingColor;
        ring.raycastTarget = false;
        TargetRing = ring.rectTransform;
        TargetRing.anchorMin = TargetRing.anchorMax = TargetRing.pivot = new Vector2(0.5f, 0.5f);
        ring.gameObject.SetActive(false);

        var joystick = CreateCircle(transform, "Joystick", BottomLeft, JoystickCenter, JOYSTICK_SIZE, BaseColor, "joystick_base");
        var knob = CreateCircle(joystick.transform, "Knob", new Vector2(0.5f, 0.5f), Vector2.zero, KNOB_SIZE, KnobColor, "knob");
        knob.raycastTarget = false;
        // The stick sits over the game: let it show through
        if (joystick.sprite != CircleSprite) {
            joystick.color = new Color(1f, 1f, 1f, 0.8f);
        }
        Joystick = joystick.gameObject.AddComponent<VirtualJoystick>();
        Joystick.Knob = knob.rectTransform;
        Joystick.Radius = (JOYSTICK_SIZE - KNOB_SIZE) / 2f + 10f;

        // Attacks as soon as it's touched; held, it goes on with the next monster
        var attack = CreateButton("Attack", AttackCenter, ATTACK_SIZE, AttackColor, "攻擊", null, "attack");
        var attackTrigger = attack.gameObject.AddComponent<EventTrigger>();
        var press = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
        press.callback.AddListener(delegate { OnAttackPressed(); });
        attackTrigger.triggers.Add(press);
        var release = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
        release.callback.AddListener(delegate { AttackHeld = false; });
        attackTrigger.triggers.Add(release);

        for (var i = 0; i < SKILL_BUTTONS; i++) {
            var angle = (SKILL_ARC_START + i * SKILL_ARC_STEP) * Mathf.Deg2Rad;
            var position = AttackCenter + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * SKILL_ARC_RADIUS;
            var slot = i;
            var button = CreateButton($"Shortcut {i + 1}", position, SKILL_SIZE, ButtonColor, null, () => UseShortcut(slot), "skill_slot");

            var icon = new GameObject("Icon", typeof(RectTransform)).AddComponent<RawImage>();
            icon.transform.SetParent(button.transform, false);
            icon.rectTransform.sizeDelta = new Vector2(SKILL_ICON_SIZE, SKILL_ICON_SIZE);
            icon.raycastTarget = false;
            icon.enabled = false;
            SkillIcons[i] = icon;

            // Skills and items dragged here go to the slot, as on the shortcut bar
            var trigger = button.gameObject.AddComponent<EventTrigger>();
            var drop = new EventTrigger.Entry { eventID = EventTriggerType.Drop };
            drop.callback.AddListener(data => OnShortcutDrop(slot, data as PointerEventData));
            trigger.triggers.Add(drop);
            SkillButtons.Add(button.gameObject);
        }

        CreateButton("Target", new Vector2(UtilityColumnsX[0], UTILITY_ROW_Y), UTILITY_SIZE, ButtonColor, "目標", OnNextTarget, "small_button", "icon_target");
        var autoLock = CreateButton("Auto Lock", new Vector2(UtilityColumnsX[1], UTILITY_ROW_Y), UTILITY_SIZE, ButtonColor, "自動\n鎖定", OnAutoLock, "small_button", "icon_autolock");
        AutoLockButton = autoLock.targetGraphic as Image;
        RefreshAutoLockButton();
        CreateButton("Pick Up", new Vector2(UtilityColumnsX[2], UTILITY_ROW_Y), UTILITY_SIZE, ButtonColor, "撿取", OnPickUp, "small_button", "icon_pickup");
        CreateButton("Sit", new Vector2(UtilityColumnsX[3], UTILITY_ROW_Y), UTILITY_SIZE, ButtonColor, "坐下", OnSitStand, "small_button", "icon_sit");
        var autoAttack = CreateButton("Auto Attack", new Vector2(UtilityColumnsX[4], UTILITY_ROW_Y), UTILITY_SIZE, ButtonColor, "內掛", OnAutoAttack, "small_button", "icon_autoattack");
        AutoAttackButton = autoAttack.targetGraphic as Image;
    }

    private Button CreateButton(string name, Vector2 position, float size, Color color, string label, UnityAction onClick, string art = null, string icon = null) {
        var image = CreateCircle(transform, name, BottomRight, position, size, color, art);
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        if (onClick != null) {
            button.onClick.AddListener(onClick);
        }

        // A picture in place of the words; the words only if the picture is missing
        var iconSprite = icon != null ? LoadArt(icon) : null;
        if (iconSprite != null) {
            var picture = new GameObject("Icon", typeof(RectTransform)).AddComponent<Image>();
            picture.transform.SetParent(image.transform, false);
            picture.sprite = iconSprite;
            picture.preserveAspect = true;
            picture.raycastTarget = false;
            picture.rectTransform.sizeDelta = new Vector2(size * ICON_FILL, size * ICON_FILL);
            label = null;
        }

        if (label != null) {
            var text = new GameObject("Label", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            text.transform.SetParent(image.transform, false);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
            text.text = label;
            text.fontSize = size * (label.Contains("\n") ? 0.21f : 0.26f);
            text.lineSpacing = -20f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.enableWordWrapping = false;
            text.raycastTarget = false;
            // Readable over the button art
            text.outlineWidth = 0.2f;
            text.outlineColor = new Color32(0, 0, 0, 220);
        }
        return button;
    }

    private static Image CreateCircle(Transform parent, string name, Vector2 anchor, Vector2 position, float size, Color color, string art = null) {
        var image = new GameObject(name, typeof(RectTransform)).AddComponent<Image>();
        image.transform.SetParent(parent, false);
        var sprite = art != null ? LoadArt(art) : null;
        image.sprite = sprite != null ? sprite : GetCircleSprite();
        image.color = sprite != null ? ArtColor : color;
        image.preserveAspect = true;

        var rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(size, size);
        return image;
    }

    /// <summary>
    /// A white disc with a brighter rim, tinted per button
    /// </summary>
    private static Sprite GetCircleSprite() {
        if (CircleSprite != null) {
            return CircleSprite;
        }

        const int size = 128;
        const float rim = 5f;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) {
            name = "ui@MobileControlsCircle",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        var pixels = new Color32[size * size];
        var center = (size - 1) / 2f;
        var radius = size / 2f - 1f;
        for (var y = 0; y < size; y++) {
            for (var x = 0; x < size; x++) {
                var distance = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center));
                var alpha = Mathf.Clamp01(radius - distance + 0.5f);
                var onRim = distance > radius - rim;
                var shade = onRim ? 1f : 0.8f;
                pixels[y * size + x] = new Color(shade, shade, shade, alpha * (onRim ? 1f : 0.85f));
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply();

        CircleSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return CircleSprite;
    }

    /// <summary>
    /// A white ring, drawn squashed on the ground under the target
    /// </summary>
    private static Sprite GetRingSprite() {
        if (RingSprite != null) {
            return RingSprite;
        }

        const int size = 128;
        const float thickness = 9f;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) {
            name = "ui@MobileControlsRing",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        var pixels = new Color32[size * size];
        var center = (size - 1) / 2f;
        var outer = size / 2f - 1f;
        var inner = outer - thickness;
        for (var y = 0; y < size; y++) {
            for (var x = 0; x < size; x++) {
                var distance = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center));
                var alpha = Mathf.Clamp01(outer - distance + 0.5f) * Mathf.Clamp01(distance - inner + 0.5f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply();

        RingSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return RingSprite;
    }

    #endregion

    #region Chat

    private struct RectState {
        public Vector2 AnchorMin, AnchorMax, Pivot, AnchoredPosition, SizeDelta;

        public static RectState Of(RectTransform rect) {
            return new RectState {
                AnchorMin = rect.anchorMin,
                AnchorMax = rect.anchorMax,
                Pivot = rect.pivot,
                AnchoredPosition = rect.anchoredPosition,
                SizeDelta = rect.sizeDelta
            };
        }

        public void ApplyTo(RectTransform rect) {
            rect.anchorMin = AnchorMin;
            rect.anchorMax = AnchorMax;
            rect.pivot = Pivot;
            rect.anchoredPosition = AnchoredPosition;
            rect.sizeDelta = SizeDelta;
        }
    }

    /// <summary>
    /// The chat box leaves the bottom left to the joystick: it goes between the two, with an input
    /// big enough to tap
    /// </summary>
    private void LayoutChat(bool mobile) {
        var chat = UI.ChatBox.transform as RectTransform;
        var panel = chat.Find("Panel") as RectTransform;
        var scroll = chat.Find("Scroll View") as RectTransform;
        var pmInput = panel != null ? panel.Find("PM Input") as RectTransform : null;
        var button = panel != null ? panel.Find("Button") as RectTransform : null;
        var messageInput = panel != null ? panel.Find("Message Input") as RectTransform : null;
        var parts = new[] { chat, panel, scroll, pmInput, button, messageInput };
        var messageField = messageInput != null ? messageInput.GetComponent<TMP_InputField>() : null;

        if (DesktopChat.Count == 0) {
            foreach (var part in parts.Where(part => part != null)) {
                DesktopChat[part] = RectState.Of(part);
            }
            DesktopChatPointSize = messageField != null ? messageField.pointSize : 0f;
        }

        if (!mobile) {
            foreach (var saved in DesktopChat) {
                saved.Value.ApplyTo(saved.Key);
            }
            if (messageField != null) {
                messageField.pointSize = DesktopChatPointSize;
            }
            ChatLaidOutWidth = 0f;
            // Its tabs, channel and 發送 button
            UI.ChatBox.Relayout();
            return;
        }

        var canvasWidth = (UI.transform as RectTransform).rect.width;
        ChatLaidOutWidth = canvasWidth;
        var width = Mathf.Clamp(canvasWidth - CHAT_SIDE_MARGIN * 2f, CHAT_MIN_WIDTH, CHAT_MAX_WIDTH);
        chat.anchorMin = chat.anchorMax = chat.pivot = new Vector2(0.5f, 0f);
        chat.anchoredPosition = Vector2.zero;
        chat.sizeDelta = new Vector2(width, CHAT_HEIGHT);

        if (panel == null) {
            return;
        }
        panel.anchorMin = new Vector2(0f, 0f);
        panel.anchorMax = new Vector2(1f, 0f);
        panel.pivot = new Vector2(0.5f, 0f);
        panel.anchoredPosition = Vector2.zero;
        panel.sizeDelta = new Vector2(0f, CHAT_INPUT_HEIGHT + 8f);

        var used = 4f;
        if (pmInput != null) {
            pmInput.sizeDelta = new Vector2(CHAT_PM_WIDTH, CHAT_INPUT_HEIGHT);
            used += CHAT_PM_WIDTH + 2f;
        }
        if (button != null) {
            button.sizeDelta = new Vector2(button.sizeDelta.x, CHAT_INPUT_HEIGHT);
            used += button.sizeDelta.x + 2f;
        }
        if (messageInput != null) {
            messageInput.sizeDelta = new Vector2(width - used - 4f, CHAT_INPUT_HEIGHT);
        }
        if (messageField != null) {
            messageField.pointSize = CHAT_INPUT_POINT_SIZE;
        }
        if (scroll != null) {
            scroll.anchorMin = new Vector2(scroll.anchorMin.x, (CHAT_INPUT_HEIGHT + 10f) / CHAT_HEIGHT);
        }
        // Its tabs, channel and 發送 button, the message box taking what they leave
        UI.ChatBox.Relayout(CHAT_INPUT_HEIGHT);
    }

    #endregion

    #region Actions

    private Entity Self => Session.CurrentSession?.Entity as Entity;

    private EntityControl Control {
        get {
            var self = Self;
            return self != null ? self.GetComponent<EntityControl>() : null;
        }
    }

    private void OnAttackPressed() {
        AttackHeld = true;
        NextAttackCheck = Time.unscaledTime + ATTACK_HOLD_SECONDS;
        Attack(true);
    }

    /// <summary>
    /// Goes for the target, or the nearest monster when there's none
    /// </summary>
    private void Attack(bool sayWhenNone) {
        var self = Self;
        var control = Control;
        if (control == null) {
            return;
        }

        var target = MobileControls.FindTarget(self);
        AttackedTarget = target;
        if (target == null) {
            if (sayWhenNone) {
                UI.ChatBox.DisplayText("附近沒有可攻擊的目標", ChatMessageType.INFO);
            }
            return;
        }
        control.Attack(target);
    }

    /// <summary>
    /// Auto lock picks the nearest monster when there's no target; a held attack button goes on
    /// with it once the one being hit is gone
    /// </summary>
    private void KeepTarget() {
        var self = Self;
        if (self == null) {
            return;
        }

        var now = Time.unscaledTime;
        if (AttackHeld) {
            if (now >= NextAttackCheck) {
                NextAttackCheck = now + ATTACK_HOLD_SECONDS;
                if (!MobileControls.IsValidTarget(AttackedTarget, self)) {
                    Attack(false);
                }
            }
        } else if (MobileControls.AutoLock && now >= NextAutoLock) {
            NextAutoLock = now + AUTO_LOCK_SECONDS;
            MobileControls.FindTarget(self);
        }
    }

    /// <summary>
    /// The red ring on the ground under the target, sized to it as the camera zooms
    /// </summary>
    private void PlaceTargetRing() {
        var self = Self;
        var target = MobileControls.Target;
        var camera = Camera.main;
        var show = self != null && camera != null && MobileControls.IsValidTarget(target, self);

        if (show) {
            var feet = target.transform.position;
            var screen = camera.WorldToScreenPoint(feet);
            var edge = camera.WorldToScreenPoint(feet + camera.transform.right * (TARGET_RING_CELLS / 2f));
            var canvas = GetComponentInParent<Canvas>();
            var canvasCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            var scale = canvas != null ? canvas.scaleFactor : 1f;

            var local = Vector2.zero;
            show = screen.z > 0f && RectTransformUtility.ScreenPointToLocalPointInRectangle(transform as RectTransform, screen, canvasCamera, out local);
            if (show) {
                var width = Mathf.Abs(edge.x - screen.x) * 2f / scale;
                TargetRing.anchoredPosition = local;
                TargetRing.sizeDelta = new Vector2(width, width * 0.45f);
                TargetRing.localScale = Vector3.one * (1f + 0.06f * Mathf.Sin(Time.unscaledTime * 6f));
            }
        }

        if (TargetRing.gameObject.activeSelf != show) {
            TargetRing.gameObject.SetActive(show);
        }
    }

    private void OnAutoLock() {
        MobileControls.AutoLock = !MobileControls.AutoLock;
        RefreshAutoLockButton();
        UI.ChatBox.DisplayText(MobileControls.AutoLock ? "自動鎖定：開" : "自動鎖定：關", ChatMessageType.INFO);
    }

    private void RefreshAutoLockButton() {
        if (AutoLockButton == null) {
            return;
        }

        var art = LoadArt(MobileControls.AutoLock ? "small_button_on" : "small_button");
        if (art != null) {
            AutoLockButton.sprite = art;
            AutoLockButton.color = ArtColor;
        } else {
            AutoLockButton.color = MobileControls.AutoLock ? AutoLockOnColor : ButtonColor;
        }
    }

    private static Sprite LoadArt(string name) {
        return Resources.Load<Sprite>(ART_PATH + name);
    }

    private void OnNextTarget() {
        var self = Self;
        if (self != null && MobileControls.NextTarget(self) == null) {
            UI.ChatBox.DisplayText("附近沒有可攻擊的目標", ChatMessageType.INFO);
        }
    }

    private void OnPickUp() {
        var self = Self;
        var control = Control;
        if (control == null) {
            return;
        }

        var item = MobileControls.FindNearby(self, EntityType.ITEM, MobileControls.PICKUP_RANGE).FirstOrDefault();
        if (item == null) {
            UI.ChatBox.DisplayText("附近沒有可撿取的物品", ChatMessageType.INFO);
            return;
        }
        control.PickUp(item);
    }

    private void OnAutoAttack() {
        if (AutoAttackWindow.Instance != null) {
            AutoAttackWindow.Instance.ToggleVisible();
        }
    }

    /// <summary>
    /// The 內掛 button lights up while the bot runs
    /// </summary>
    private void OnAutoAttackStatus(Pandas.AA_STATUS status) {
        if (AutoAttackButton == null) {
            return;
        }
        var running = status.State != Pandas.AA_STATUS.STATE_OFF;
        var art = LoadArt(running ? "small_button_on" : "small_button");
        if (art != null) {
            if (AutoAttackButton.sprite != art) {
                AutoAttackButton.sprite = art;
            }
        } else {
            AutoAttackButton.color = running ? AutoLockOnColor : ButtonColor;
        }
    }

    private void OnSitStand() {
        var control = Control;
        if (control != null) {
            control.RequestSitStand();
        }
    }

    private void UseShortcut(int slot) {
        var container = HotkeyBar != null ? HotkeyBar.GetSlot(slot) : null;
        if (container != null) {
            container.Use();
        }
    }

    private void OnShortcutDrop(int slot, PointerEventData eventData) {
        var container = HotkeyBar != null ? HotkeyBar.GetSlot(slot) : null;
        if (container != null && eventData != null) {
            container.OnDrop(eventData);
        }
    }

    private void RefreshSkillIcons() {
        for (var i = 0; i < SKILL_BUTTONS; i++) {
            var container = HotkeyBar != null ? HotkeyBar.GetSlot(i) : null;
            var texture = container != null ? container.Icon : null;
            if (SkillIcons[i].texture != texture) {
                SkillIcons[i].texture = texture;
                SkillIcons[i].enabled = texture != null;
            }
        }
    }

    #endregion

    #region Walking

    /// <summary>
    /// The server walks to cells: head for a cell a few steps ahead in the stick's direction, as
    /// the camera sees it, and ask for the next one before getting there
    /// </summary>
    private void Walk() {
        var self = Self;
        if (self == null || Joystick == null) {
            return;
        }

        if (Joystick.Direction.magnitude < JOYSTICK_DEAD_ZONE) {
            if (IsWalking) {
                StopWalking(self);
            }
            return;
        }

        var step = GetStep(Joystick.Direction);
        var walk = self.GetComponent<EntityWalk>();
        var position = self.transform.position;
        var now = Time.unscaledTime;
        if (IsWalking && step == WalkStep && WalkTarget.HasValue) {
            var target = new Vector3(WalkTarget.Value.x, position.y, WalkTarget.Value.y);
            var closeToTarget = MobileControls.CellDistance(position, target) <= WALK_AGAIN_CELLS;
            var notMoving = (walk == null || !walk.IsWalking) && now - LastWalkRequest > WALK_RETRY_SECONDS;
            if (!closeToTarget && !notMoving) {
                return;
            }
        }

        var pathFinder = FindObjectOfType<PathFinder>();
        if (pathFinder == null) {
            return;
        }
        var cell = new Vector2Int(Mathf.RoundToInt(position.x), Mathf.RoundToInt(position.z));
        var next = FindWalkTarget(pathFinder, cell, step);
        if (!next.HasValue) {
            return;
        }
        // Against a wall or the map's edge there's no further to ask for: only try again now and then
        if (next == WalkTarget && now - LastWalkRequest < WALK_RETRY_SECONDS) {
            return;
        }

        if (self.EntityViewer != null && self.EntityViewer.State == SpriteState.Sit) {
            Control?.RequestSitStand();
        }
        new CZ.REQUEST_MOVE2(next.Value.x, next.Value.y, 0).Send();
        WalkStep = step;
        WalkTarget = next;
        LastWalkRequest = now;
        IsWalking = true;
    }

    private void StopWalking(Entity self) {
        IsWalking = false;
        WalkTarget = null;
        WalkStep = Vector2Int.zero;

        // Stop on the cell being walked into: the server takes it once that step is done
        var walk = self.GetComponent<EntityWalk>();
        var next = walk != null ? walk.NextCell : null;
        if (!next.HasValue) {
            var position = self.transform.position;
            next = new Vector2Int(Mathf.RoundToInt(position.x), Mathf.RoundToInt(position.z));
        }
        new CZ.REQUEST_MOVE2(next.Value.x, next.Value.y, 0).Send();
    }

    /// <summary>
    /// One of the 8 cell directions, where the stick points on screen
    /// </summary>
    private static Vector2Int GetStep(Vector2 direction) {
        var camera = Camera.main;
        var forward = camera != null ? Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up).normalized : Vector3.forward;
        var right = camera != null ? Vector3.ProjectOnPlane(camera.transform.right, Vector3.up).normalized : Vector3.right;
        var world = forward * direction.y + right * direction.x;

        var octant = Mathf.RoundToInt(Mathf.Atan2(world.z, world.x) / (Mathf.PI / 4f));
        var angle = octant * Mathf.PI / 4f;
        return new Vector2Int(Mathf.RoundToInt(Mathf.Cos(angle)), Mathf.RoundToInt(Mathf.Sin(angle)));
    }

    /// <summary>
    /// The furthest walkable cell in a straight line, up to WALK_AHEAD_CELLS; going into a wall at
    /// an angle slides along it
    /// </summary>
    private static Vector2Int? FindWalkTarget(PathFinder pathFinder, Vector2Int from, Vector2Int step) {
        Vector2Int? target = null;
        for (var i = 1; i <= WALK_AHEAD_CELLS; i++) {
            var cell = from + step * i;
            if (!IsWalkable(pathFinder, cell)) {
                break;
            }
            target = cell;
        }

        if (!target.HasValue && step.x != 0 && step.y != 0) {
            return FindWalkTarget(pathFinder, from, new Vector2Int(step.x, 0))
                ?? FindWalkTarget(pathFinder, from, new Vector2Int(0, step.y));
        }
        return target;
    }

    private static bool IsWalkable(PathFinder pathFinder, Vector2Int cell) {
        try {
            return pathFinder.IsWalkable(cell.x, cell.y);
        } catch (System.Exception) {
            // Off the map
            return false;
        }
    }

    #endregion
}
