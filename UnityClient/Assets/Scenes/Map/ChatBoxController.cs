using ROIO;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The chat: tabs at its top left to show one kind of message only (公開, 隊伍, 公會, 聯盟,
/// 代表, 密語, 系統), and at the right of the input the channel to speak on (公開, 隊伍, 公會,
/// 公會聯盟, 代表公會) and 發送, as in the official client. A name in the box left of the
/// message whispers to them.
/// </summary>
public class ChatBoxController : MonoBehaviour {

    /// <summary>
    /// What a line of the chat is, for the tabs
    /// </summary>
    public enum Category {
        Public,
        Party,
        Guild,
        Whisper,
        System,
        // 公會聯盟 and 代表公會 (clan)
        Ally,
        Clan
    }

    // In the order of the channel menu
    private enum Channel {
        Public,
        Party,
        Guild,
        Ally,
        Clan
    }

    private const float TAB_WIDTH = 38f;
    private const float TAB_HEIGHT = 17f;
    private const float INPUT_BUTTON_WIDTH = 40f;
    private const float CHANNEL_ROW_HEIGHT = 18f;

    private static readonly Color WhisperColor = Color.yellow;
    private static readonly Color GuildColor = new Color32(180, 255, 180, 255);
    private static readonly Color AllyColor = new Color32(140, 210, 255, 255);
    private static readonly Color ClanColor = new Color32(255, 180, 230, 255);
    private static readonly Color ChosenRowColor = new Color32(165, 189, 231, 255);
    private static readonly Color PointedRowColor = new Color32(222, 231, 247, 255);
    private static readonly string[] TabLabels = { "全部", "公開", "隊伍", "公會", "聯盟", "代表", "密語", "系統" };
    private static readonly Category?[] TabFilters = {
        null, Category.Public, Category.Party, Category.Guild, Category.Ally, Category.Clan, Category.Whisper, Category.System
    };
    private static readonly string[] ChannelLabels = { "公開", "隊伍", "公會", "聯盟", "代表" };
    private static readonly string[] ChannelMenuLabels = { "公開發言", "隊伍發言頻道", "公會發言頻道", "公會聯盟發言頻道", "代表公會發言頻道" };

    [SerializeField] private TMP_InputField MessageInput;
    [SerializeField] private TMP_InputField PMInput;
    [SerializeField] private GameObject LinearLayout;
    [SerializeField] private GameObject TextLinePrefab;
    [SerializeField] private ToggleGroup tabLayout;

    private NetworkClient NetworkClient;
    private EntityManager EntityManager;
    private string LastWhisperTarget;

    private Category? Filter;
    private Channel SendChannel = Channel.Public;
    private readonly List<Button> TabButtons = new List<Button>();
    private Button ChannelButton;
    private Button SendButton;
    private GameObject ChannelMenu;
    private ScrollRect Scroll;

    private void Awake() {
        NetworkClient = FindObjectOfType<NetworkClient>();
        EntityManager = FindObjectOfType<EntityManager>();

        NetworkClient.HookPacket(ZC.NOTIFY_PLAYERCHAT.HEADER, OnMessageRecieved);
        NetworkClient.HookPacket(ZC.NOTIFY_CHAT.HEADER, OnMessageRecieved);
        NetworkClient.HookPacket(ZC.MSG.HEADER, OnMessageRecieved);
        NetworkClient.HookPacket(ZC.NPC_CHAT.HEADER, OnMessageRecieved);
        NetworkClient.HookPacket(ZC.WHISPER02.HEADER, OnWhisper);
        NetworkClient.HookPacket(ZC.ACK_WHISPER02.HEADER, OnWhisperAnswered);
        NetworkClient.HookPacket(ZC.GUILD_CHAT.HEADER, OnGuildChat);
        NetworkClient.HookPacket(ZC.ALLY_CHAT.HEADER, OnAllyChat);
        NetworkClient.HookPacket(ZC.NOTIFY_CLAN_CHAT.HEADER, OnClanChat);

        // A phone's keyboard has no Return key press to catch: it submits the input instead
        if (Application.isMobilePlatform) {
            MessageInput.onSubmit.AddListener(delegate { SendChatMessage(); });
        }

        // The box left of the message is who it is whispered to
        if (PMInput != null && PMInput.placeholder is TMP_Text hint) {
            hint.text = "密語對象";
        }

        Scroll = GetComponentInChildren<ScrollRect>(true);
        BuildTabs();
        BuildInputButtons();
    }

    private void Start() {
        Relayout();
    }

    #region Layout

    private void BuildTabs() {
        var strip = new GameObject("Channel Tabs", typeof(RectTransform)).GetComponent<RectTransform>();
        strip.SetParent(transform, false);
        strip.anchorMin = strip.anchorMax = strip.pivot = new Vector2(0f, 1f);
        strip.anchoredPosition = new Vector2(2f, -1f);
        var layout = strip.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 1f;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        var fitter = strip.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        for (var i = 0; i < TabLabels.Length; i++) {
            var index = i;
            TabButtons.Add(RoWidgets.Tab(strip, TabLabels[i], () => ShowTab(index), TAB_WIDTH, TAB_HEIGHT, i == 0));
        }
    }

    private void BuildInputButtons() {
        var panel = transform.Find("Panel");
        if (panel == null) {
            return;
        }
        ChannelButton = RoWidgets.Button(panel, ChannelLabels[(int) SendChannel], ToggleChannelMenu, INPUT_BUTTON_WIDTH);
        ChannelButton.name = "Channel";
        SendButton = RoWidgets.Button(panel, "發送", SendChatMessage, INPUT_BUTTON_WIDTH);
        SendButton.name = "Send";
    }

    /// <summary>
    /// Fits the messages under the tabs and the input row to the box (which the mobile controls
    /// resize): the message box takes what the other controls leave.
    /// </summary>
    public void Relayout(float inputHeight = -1f) {
        var scroll = Scroll != null ? Scroll.transform as RectTransform : null;
        if (scroll != null) {
            scroll.offsetMax = new Vector2(scroll.offsetMax.x, -(TAB_HEIGHT + 2f));
        }

        var panel = transform.Find("Panel") as RectTransform;
        var message = MessageInput.transform as RectTransform;
        if (panel == null || ChannelButton == null) {
            return;
        }
        var height = inputHeight > 0f ? inputHeight : message.sizeDelta.y;
        var buttonHeight = Mathf.Max(height + 3f, 18f);
        foreach (var button in new[] { ChannelButton, SendButton }) {
            (button.transform as RectTransform).sizeDelta = new Vector2(INPUT_BUTTON_WIDTH, buttonHeight);
        }

        var layout = panel.GetComponent<HorizontalLayoutGroup>();
        var used = layout != null ? layout.padding.left + layout.padding.right : 0f;
        var others = 0;
        foreach (RectTransform child in panel) {
            if (child == message || !child.gameObject.activeSelf) {
                continue;
            }
            used += child.sizeDelta.x;
            others++;
        }
        if (layout != null) {
            used += layout.spacing * others;
        }
        message.sizeDelta = new Vector2(Mathf.Max(60f, panel.rect.width - used - 4f), message.sizeDelta.y);
    }

    #endregion

    #region Tabs

    private void ShowTab(int index) {
        Filter = TabFilters[index];
        for (var i = 0; i < TabButtons.Count; i++) {
            RoWidgets.SetTabSelected(TabButtons[i], i == index);
        }
        foreach (Transform child in LinearLayout.transform) {
            var line = child.GetComponent<Line>();
            child.gameObject.SetActive(line == null || Shows(line.Category));
        }
        ScrollToEnd();
    }

    private bool Shows(Category category) => Filter == null || Filter == category;

    private void ScrollToEnd() {
        if (Scroll != null) {
            Canvas.ForceUpdateCanvases();
            Scroll.verticalNormalizedPosition = 0f;
        }
    }

    // What kind of message a line of the chat is
    private class Line : MonoBehaviour {
        public Category Category;
    }

    #endregion

    #region Channel

    /// <summary>
    /// 公開發言 / 隊伍發言頻道 / 公會發言頻道, over the channel button as in the official client
    /// </summary>
    private void ToggleChannelMenu() {
        if (ChannelMenu != null) {
            CloseChannelMenu();
            return;
        }
        var ui = MapUiController.Instance != null ? MapUiController.Instance.transform : transform.parent;
        var overlay = new GameObject("Chat Channel Menu", typeof(RectTransform)).AddComponent<Image>();
        overlay.transform.SetParent(ui, false);
        overlay.color = Color.clear;
        overlay.rectTransform.anchorMin = Vector2.zero;
        overlay.rectTransform.anchorMax = Vector2.one;
        overlay.rectTransform.offsetMin = overlay.rectTransform.offsetMax = Vector2.zero;
        // Tapping anywhere else closes it
        overlay.gameObject.AddComponent<Button>().onClick.AddListener(CloseChannelMenu);
        ChannelMenu = overlay.gameObject;

        var list = new GameObject("List", typeof(RectTransform)).AddComponent<Image>();
        list.transform.SetParent(overlay.transform, false);
        list.color = Color.white;
        list.gameObject.AddComponent<Outline>().effectColor = RoWidgets.LineColor;
        var rect = list.rectTransform;
        rect.sizeDelta = new Vector2(124f, ChannelMenuLabels.Length * CHANNEL_ROW_HEIGHT + 4f);
        // Over the channel button, its right edge on the button's
        var corners = new Vector3[4];
        (ChannelButton.transform as RectTransform).GetWorldCorners(corners);
        rect.pivot = new Vector2(1f, 0f);
        rect.position = corners[2];

        for (var i = 0; i < ChannelMenuLabels.Length; i++) {
            var channel = (Channel) i;
            var row = new GameObject(ChannelMenuLabels[i], typeof(RectTransform)).AddComponent<Image>();
            row.transform.SetParent(rect, false);
            row.color = channel == SendChannel ? ChosenRowColor : Color.white;
            var rowRect = row.rectTransform;
            rowRect.anchorMin = new Vector2(0f, 1f);
            rowRect.anchorMax = Vector2.one;
            rowRect.pivot = new Vector2(0.5f, 1f);
            rowRect.sizeDelta = new Vector2(-4f, CHANNEL_ROW_HEIGHT);
            rowRect.anchoredPosition = new Vector2(0f, -2f - i * CHANNEL_ROW_HEIGHT);
            var button = row.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = colors.pressedColor = colors.selectedColor = channel == SendChannel ? Color.white : PointedRowColor;
            button.colors = colors;
            button.targetGraphic = row;
            button.onClick.AddListener(() => {
                SetChannel(channel);
                CloseChannelMenu();
            });
            RoWidgets.Text(rowRect, ChannelMenuLabels[i], 12f, RoWidgets.TextColor, new Vector2(6f, -1f), new Vector2(100f, 16f));
        }
        overlay.transform.SetAsLastSibling();
    }

    private void CloseChannelMenu() {
        if (ChannelMenu != null) {
            Destroy(ChannelMenu);
            ChannelMenu = null;
        }
    }

    private void SetChannel(Channel channel) {
        SendChannel = channel;
        RoWidgets.SetButtonText(ChannelButton, ChannelLabels[(int) channel]);
    }

    #endregion

    /// <summary>
    /// What's typed next is whispered to them
    /// </summary>
    public void StartWhisper(string name) {
        if (PMInput != null) {
            PMInput.text = name;
        }
        MessageInput.ActivateInputField();
        MessageInput.Select();
    }

    private void OnWhisper(ushort cmd, int size, InPacket packet) {
        if (!(packet is ZC.WHISPER02 whisper)) {
            return;
        }
        var line = DisplayText($"(來自 {whisper.Sender}) : {whisper.Message}", WhisperColor, Category.Whisper);
        // Tapping it answers them
        var sender = whisper.Sender;
        var text = line.GetComponentInChildren<TextMeshProUGUI>(true);
        text.raycastTarget = true;
        var button = line.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = text;
        button.onClick.AddListener(() => StartWhisper(sender));
    }

    private void OnWhisperAnswered(ushort cmd, int size, InPacket packet) {
        if (!(packet is ZC.ACK_WHISPER02 answer) || answer.Result == 0) {
            return;
        }
        var name = LastWhisperTarget ?? "";
        DisplayText(answer.Result switch {
            1 => $"{name} 不在線上，或沒有這個角色。",
            2 => $"{name} 拒絕了你的密語。",
            3 => $"{name} 拒絕所有密語。",
            _ => $"密語 {name} 失敗 ({answer.Result})。"
        }, Color.red, Category.Whisper);
    }

    private void OnGuildChat(ushort cmd, int size, InPacket packet) {
        if (packet is ZC.GUILD_CHAT chat) {
            DisplayText(chat.Message, GuildColor, Category.Guild);
        }
    }

    private void OnAllyChat(ushort cmd, int size, InPacket packet) {
        if (packet is ZC.ALLY_CHAT chat) {
            DisplayText(chat.Message, AllyColor, Category.Ally);
        }
    }

    private void OnClanChat(ushort cmd, int size, InPacket packet) {
        if (packet is ZC.NOTIFY_CLAN_CHAT chat) {
            DisplayText(chat.Message, ClanColor, Category.Clan);
        }
    }

    private void OnMessageRecieved(ushort cmd, int size, InPacket packet) {
        if (packet is ZC.NOTIFY_PLAYERCHAT) {
            var pkt = packet as ZC.NOTIFY_PLAYERCHAT;
            DisplayText(pkt.Message, Color.green, Category.Public);

            // Only what we said ("name : message") goes over our head; the server's notices
            // (channels, commands, ...) come in this packet too
            var self = Session.CurrentSession.Entity as Entity;
            if (self != null && pkt.Message.StartsWith(self.GetBaseStatus().name + " : ")) {
                self.DisplayChatBubble(pkt.Message);
            }
        } else if (packet is ZC.NOTIFY_CHAT) {
            var pkt = packet as ZC.NOTIFY_CHAT;
            DisplayText(pkt.Message, Color.green, Category.Public);

            EntityManager.GetEntity(pkt.GID).DisplayChatBubble(pkt.Message);
        } else if (packet is ZC.MSG) {
            var pkt = packet as ZC.MSG;
            DisplayText((string) Tables.MsgStringTable[$"{pkt.MessageID}"] ?? $"{pkt.MessageID}", Color.white, Category.System);
        } else if (packet is ZC.NPC_CHAT NPC_CHAT) {
            // 0x00BBGGRR; NPCs talking around us, as players do
            var color = new Color32((byte) NPC_CHAT.Color, (byte) (NPC_CHAT.Color >> 8), (byte) (NPC_CHAT.Color >> 16), 255);
            DisplayText(NPC_CHAT.Message, color, Category.Public);
        }
    }

    public void SendChatMessage() {
        var message = MessageInput.text;
        if (message.Length == 0)
            return;

        // "/ho", "/!", ... show an emotion instead of being said
        if (message.StartsWith("/") && EmotionTable.TryGetCommand(message.Substring(1).Trim(), out var emotion)) {
            new CZ.SEND_EMOTE(emotion).Send();
        } else if (message.StartsWith("%") && message.Length > 1) {
            // "%message" goes to the party and "$message" to the guild, as in the official client
            new CZ.REQUEST_CHAT_PARTY(message.Substring(1).TrimStart()).Send();
        } else if (message.StartsWith("$") && message.Length > 1) {
            new CZ.GUILD_CHAT(message.Substring(1).TrimStart()).Send();
        } else if (PMInput != null && !string.IsNullOrWhiteSpace(PMInput.text)) {
            // A name in the box left of the message: it's whispered to them
            var target = PMInput.text.Trim();
            LastWhisperTarget = target;
            new CZ.WHISPER(target, message).Send();
            DisplayText($"(給 {target}) : {message}", WhisperColor, Category.Whisper);
        } else if (SendChannel == Channel.Party) {
            new CZ.REQUEST_CHAT_PARTY(message).Send();
        } else if (SendChannel == Channel.Guild) {
            new CZ.GUILD_CHAT(message).Send();
        } else if (SendChannel == Channel.Ally) {
            new CZ.ALLY_CHAT(message).Send();
        } else if (SendChannel == Channel.Clan) {
            new CZ.CLAN_CHAT(message).Send();
        } else {
            new CZ.REQUEST_CHAT(message).Send();
        }
        MessageInput.text = "";
        // On a phone that would bring the keyboard straight back over the game
        if (!Application.isMobilePlatform) {
            MessageInput.ActivateInputField();
            MessageInput.Select();
        }
    }

    public void DisplayText(string text, ChatMessageType messageType) {
        DisplayText(text, GetTextColor(messageType), CategoryOf(messageType));
    }

    public GameObject DisplayText(string text, Color color, Category category = Category.System) {
        var line = Instantiate(TextLinePrefab);
        var uiText = line.GetComponentInChildren<TextMeshProUGUI>();

        uiText.text = text;
        uiText.color = color;

        line.transform.SetParent(LinearLayout.transform, false);
        line.AddComponent<Line>().Category = category;
        // Not on the tab shown: kept for when its tab is
        line.SetActive(Shows(category));
        return line;
    }

    public void DisplayMessage(int messageID, ChatMessageType messageType) {
        var text = (string) Tables.MsgStringTable[$"{messageID}"] ?? $"{messageID}";
        DisplayText(text, GetTextColor(messageType), CategoryOf(messageType));
    }

    public void DisplayMessage(int messageID, ChatMessageType messageType, params KeyValuePair<string, string>[] replacePairs) {
        var text = (string) Tables.MsgStringTable[$"{messageID}"] ?? $"{messageID}";

        foreach(var pair in replacePairs) {
            text = text.Replace(pair.Key, pair.Value);
        }

        DisplayText(text, GetTextColor(messageType), CategoryOf(messageType));
    }

    private static Category CategoryOf(ChatMessageType messageType) {
        if ((messageType & ChatMessageType.PARTY) > 0) {
            return Category.Party;
        }
        if ((messageType & ChatMessageType.GUILD) > 0) {
            return Category.Guild;
        }
        if ((messageType & ChatMessageType.PRIVATE) > 0) {
            return Category.Whisper;
        }
        if ((messageType & ChatMessageType.PUBLIC) > 0) {
            return Category.Public;
        }
        return Category.System;
    }

    private Color32 GetTextColor(ChatMessageType messageType) {
        var intMessageType = (int) messageType;
        Color color;

        if ((messageType & ChatMessageType.PUBLIC) > 0 && (messageType & ChatMessageType.SELF) > 0) {
            color = Color.green;
        } else if ((messageType & ChatMessageType.PARTY) > 0) {
            color = (messageType & ChatMessageType.SELF) > 0 ? Color.yellow : Color.white;
        } else if ((messageType & ChatMessageType.GUILD) > 0) {
            ColorUtility.TryParseHtmlString("#B4FFB4", out color);
        } else if ((messageType & ChatMessageType.PRIVATE) > 0) {
            color = Color.yellow;
        } else if ((messageType & ChatMessageType.ERROR) > 0) {
            color = Color.red;
        } else if ((messageType & ChatMessageType.INFO) > 0) {
            color = Color.yellow;
        } else if ((messageType & ChatMessageType.BLUE) > 0) {
            color = Color.cyan;
        } else if ((messageType & ChatMessageType.ADMIN) > 0) {
            color = Color.yellow;
        } else {
            color = Color.white;
        }

        return color;
    }

    // Update is called once per frame
    void Update() {
        if (Input.GetKeyDown(KeyCode.Return)) {
            SendChatMessage();
        }
    }
}
