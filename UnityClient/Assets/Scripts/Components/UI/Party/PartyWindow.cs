using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ROIO;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// The party (組隊) window, laid out as the official client's (renewalparty textures): each
/// member's job, level, name and map, whether they're online, and the HP of those we can see.
/// Its toolbar makes a party, invites, sets it up and leaves; tapping a member whispers, hands
/// the lead over or expels. It also answers invites, shows party chat and puts party members'
/// HP bars over their heads.
/// </summary>
public class PartyWindow : MonoBehaviour {

    public class Member {
        public uint AID;
        public uint GID;
        public string Name;
        public string Map;
        public bool IsLeader;
        public bool IsOnline;
        public bool IsDead;
        public short Job;
        public short BaseLevel;
        public int Hp = -1;
        public int MaxHp = -1;
        // The cell they're on, when on our map
        public int X = -1;
        public int Y = -1;
    }

    public const int MAX_MEMBERS = 12;
    private const float WIDTH = 290f;
    private const float HEIGHT = 300f;
    private const float TITLE_HEIGHT = 17f;
    private const float TOOLBAR_HEIGHT = 28f;
    private const float ROW_HEIGHT = 36f;
    private const float HP_BAR_WIDTH = 60f;
    private const float REFRESH_SECONDS = 0.3f;
    private static readonly Color PartyChatColor = new Color32(255, 214, 140, 255);
    private static readonly Color ConfirmColor = new Color(0.6f, 0.18f, 0.18f, 1f);
    // The official window's colours
    private static readonly Color OnlineColor = new Color32(8, 49, 123, 255);
    private static readonly Color SelfColor = new Color32(0, 123, 123, 255);
    private static readonly Color OfflineColor = new Color32(132, 140, 165, 255);
    private const string LevelColor = "#31394A";
    private static readonly Color ToolbarColor = new Color32(247, 247, 247, 255);
    private static readonly Color LineColor = new Color32(198, 198, 206, 255);
    private static readonly Color PointedRowColor = new Color32(222, 231, 247, 255);

    public static PartyWindow Instance { get; private set; }

    public string PartyName { get; private set; }
    public bool InParty => PartyName != null;
    public readonly List<Member> Members = new List<Member>();

    // 0 each keeps their own, 1 shared, 2 can't be shared (levels too far apart)
    private int ExpOption;
    private bool SharePickup;
    private bool ShareLoot;
    private bool RefuseInvites;

    private MapUiController UI;
    private EntityManager EntityManager;
    private RectTransform Root;
    private RectTransform Content;
    private ScrollRect ContentScroll;
    private RectTransform Toolbar;
    private GameObject Empty;
    private TextMeshProUGUI Title;
    private GameObject InvitePopup;
    private GameObject Dialog;

    private string NewPartyName = "";
    private string InviteName = "";
    private bool NewSharePickup;
    private bool NewShareLoot;
    private bool Dirty;
    private float NextRefresh;
    private long DrawnSelfHp = -1;
    private long DrawnSelfMaxHp = -1;

    public static PartyWindow Create(MapUiController ui) {
        var root = AaWidgets.NewImage("Party Window", ui.transform, Color.white);
        root.gameObject.AddComponent<Outline>().effectColor = LineColor;
        var window = root.gameObject.AddComponent<PartyWindow>();
        window.UI = ui;
        window.Root = root.rectTransform;
        window.Build();
        root.gameObject.SetActive(false);

        // The shortcut menu's party icon opens it
        var partyIcon = ui.transform.Find("SystemShortcuts/Panel/Party");
        if (partyIcon != null && partyIcon.TryGetComponent<Button>(out var button)) {
            button.onClick.AddListener(window.ToggleVisible);
        }
        return window;
    }

    private void Awake() {
        Instance = this;
        EntityManager = FindObjectOfType<EntityManager>();
        var network = FindObjectOfType<NetworkClient>();
        network.HookPacket(ZC.GROUP_LIST.HEADER, OnGroupList);
        network.HookPacket(ZC.ADD_MEMBER_TO_GROUP.HEADER, OnAddMember);
        network.HookPacket(ZC.DELETE_MEMBER_FROM_GROUP.HEADER, OnDeleteMember);
        network.HookPacket(ZC.NOTIFY_HP_TO_GROUPM.HEADER, OnMemberHp);
        network.HookPacket(ZC.NOTIFY_POSITION_TO_GROUPM.HEADER, OnMemberPosition);
        network.HookPacket(ZC.PARTY_CONFIG.HEADER, OnPartyConfig);
        network.HookPacket(ZC.REQ_GROUPINFO_CHANGE_V2.HEADER, OnGroupInfo);
        network.HookPacket(ZC.ACK_MAKE_GROUP.HEADER, OnMakeGroup);
        network.HookPacket(ZC.PARTY_JOIN_REQ.HEADER, OnInvited);
        network.HookPacket(ZC.PARTY_JOIN_REQ_ACK.HEADER, OnInviteAnswered);
        network.HookPacket(ZC.NOTIFY_CHAT_PARTY.HEADER, OnPartyChat);
        network.HookPacket(ZC.CHANGE_GROUP_MASTER.HEADER, OnLeaderChanged);
        network.HookPacket(ZC.GROUP_ISALIVE.HEADER, OnMemberAlive);
        network.HookPacket(ZC.NOTIFY_MEMBERINFO_TO_GROUPM.HEADER, OnMemberInfo);
    }

    private void OnDestroy() {
        if (Instance == this) {
            Instance = null;
        }
    }

    private void Update() {
        // Our own HP isn't sent as a party member's: redraw our bar when it changes
        if (InParty) {
            var status = SelfStatus();
            if (status != null && (status.hp != DrawnSelfHp || status.max_hp != DrawnSelfMaxHp)) {
                DrawnSelfHp = status.hp;
                DrawnSelfMaxHp = status.max_hp;
                Changed();
            }
        }
        if (Dirty && Time.unscaledTime >= NextRefresh) {
            Refresh();
        }
    }

    public void Show() {
        // Where it was left, but inside the screen
        var canvas = (UI.transform as RectTransform).rect.size;
        Root.sizeDelta = new Vector2(Mathf.Min(WIDTH, canvas.x - 16f), Mathf.Min(HEIGHT, canvas.y - 16f));
        var room = (canvas - Root.sizeDelta) / 2f;
        Root.anchoredPosition = new Vector2(Mathf.Clamp(Root.anchoredPosition.x, -room.x, room.x), Mathf.Clamp(Root.anchoredPosition.y, -room.y, room.y));
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        Refresh(keepScroll: false);
    }

    public void Hide() {
        gameObject.SetActive(false);
    }

    public void ToggleVisible() {
        if (gameObject.activeSelf) {
            Hide();
        } else {
            Show();
        }
    }

    #region State

    private static bool IsSelf(uint accountId) => Session.CurrentSession != null && accountId == Session.CurrentSession.AccountID;

    private static EntityBaseStatus SelfStatus() {
        var self = Session.CurrentSession?.Entity as Entity;
        return self != null ? self.GetBaseStatus() : null;
    }

    private Member Me => Members.FirstOrDefault(member => IsSelf(member.AID));

    private bool IAmLeader => Me != null && Me.IsLeader;

    private Member FindMember(uint accountId) => Members.FirstOrDefault(member => member.AID == accountId);

    private void Changed() {
        Dirty = true;
    }

    private void Say(string text) {
        if (UI != null && UI.ChatBox != null) {
            UI.ChatBox.DisplayText(text, PartyChatColor);
        }
    }

    private void LeaveParty() {
        foreach (var member in Members) {
            ShowHitPoints(member, false);
        }
        Members.Clear();
        PartyName = null;
        ExpOption = 0;
        Changed();
    }

    /// <summary>
    /// Party members in sight have their HP bar over their head
    /// </summary>
    private void ShowHitPoints(Member member, bool show) {
        if (member == null || IsSelf(member.AID) || EntityManager == null) {
            return;
        }
        var entity = EntityManager.FindEntity(member.AID);
        if (entity == null) {
            return;
        }
        if (!show) {
            entity.HidePartyHitPoints();
        } else if (member.MaxHp > 0) {
            entity.ShowPartyHitPoints(member.Hp, member.MaxHp);
        }
    }

    #endregion

    #region Server

    private void OnGroupList(ushort cmd, int size, InPacket packet) {
        if (!(packet is ZC.GROUP_LIST list)) {
            return;
        }
        var known = Members.ToDictionary(member => member.AID);
        Members.Clear();
        PartyName = list.PartyName;
        foreach (var entry in list.Members) {
            known.TryGetValue(entry.AID, out var before);
            Members.Add(new Member {
                AID = entry.AID,
                GID = entry.GID,
                Name = entry.Name,
                Map = entry.Map,
                IsLeader = entry.IsLeader,
                IsOnline = entry.IsOnline,
                Job = entry.Job,
                BaseLevel = entry.BaseLevel,
                IsDead = before != null && before.IsDead,
                Hp = before != null ? before.Hp : -1,
                MaxHp = before != null ? before.MaxHp : -1,
                X = before != null ? before.X : -1,
                Y = before != null ? before.Y : -1
            });
        }
        Changed();
    }

    private void OnAddMember(ushort cmd, int size, InPacket packet) {
        if (!(packet is ZC.ADD_MEMBER_TO_GROUP add)) {
            return;
        }
        var wasInParty = InParty;
        PartyName = add.PartyName;
        SharePickup = add.SharePickup;
        ShareLoot = add.ShareLoot;

        var member = FindMember(add.AID);
        if (member == null) {
            member = new Member { AID = add.AID };
            Members.Add(member);
            if (wasInParty && !IsSelf(add.AID)) {
                Say($"{add.Name} 加入了隊伍。");
            }
        }
        member.GID = add.GID;
        member.Name = add.Name;
        member.Map = add.Map;
        member.IsLeader = add.IsLeader;
        member.IsOnline = add.IsOnline;
        member.Job = add.Job;
        member.BaseLevel = add.BaseLevel;
        member.X = add.X;
        member.Y = add.Y;
        Changed();
    }

    private void OnMemberPosition(ushort cmd, int size, InPacket packet) {
        // Where members on our map are, for the minimap; nothing in the window changes
        if (packet is ZC.NOTIFY_POSITION_TO_GROUPM position && FindMember(position.AID) is Member member) {
            member.X = position.X;
            member.Y = position.Y;
        }
    }

    private void OnDeleteMember(ushort cmd, int size, InPacket packet) {
        if (!(packet is ZC.DELETE_MEMBER_FROM_GROUP delete)) {
            return;
        }
        var self = IsSelf(delete.AID);
        switch (delete.Result) {
            case 0:
                Say(self ? "你離開了隊伍。" : $"{delete.Name} 離開了隊伍。");
                break;
            case 1:
                Say(self ? "你被踢出了隊伍。" : $"{delete.Name} 被踢出了隊伍。");
                break;
            case 2:
                Say("這張地圖無法離開隊伍。");
                return;
            default:
                Say("這張地圖無法踢出隊員。");
                return;
        }

        if (self) {
            LeaveParty();
            return;
        }
        var member = FindMember(delete.AID);
        if (member != null) {
            ShowHitPoints(member, false);
            Members.Remove(member);
        }
        Changed();
    }

    private void OnMemberHp(ushort cmd, int size, InPacket packet) {
        if (!(packet is ZC.NOTIFY_HP_TO_GROUPM hp)) {
            return;
        }
        var member = FindMember(hp.AID);
        if (member == null) {
            return;
        }
        member.Hp = hp.Hp;
        member.MaxHp = hp.MaxHp;
        ShowHitPoints(member, true);
        Changed();
    }

    private void OnPartyConfig(ushort cmd, int size, InPacket packet) {
        if (packet is ZC.PARTY_CONFIG config) {
            RefuseInvites = config.RefuseInvites;
            Changed();
        }
    }

    private void OnGroupInfo(ushort cmd, int size, InPacket packet) {
        if (packet is ZC.REQ_GROUPINFO_CHANGE_V2 info) {
            ExpOption = info.ExpOption;
            SharePickup = info.SharePickup;
            ShareLoot = info.ShareLoot;
            Changed();
        }
    }

    private void OnMakeGroup(ushort cmd, int size, InPacket packet) {
        if (!(packet is ZC.ACK_MAKE_GROUP made)) {
            return;
        }
        switch (made.Result) {
            case 0:
                Say("隊伍建立成功。");
                NewPartyName = "";
                break;
            case 1:
                Say("已經有同名的隊伍了。");
                break;
            case 2:
                Say("你已經在隊伍裡了。");
                break;
            default:
                Say("這張地圖無法建立隊伍。");
                break;
        }
        Changed();
    }

    private void OnInvited(ushort cmd, int size, InPacket packet) {
        if (packet is ZC.PARTY_JOIN_REQ invite) {
            ShowInvite(invite.PartyId, invite.PartyName);
        }
    }

    private void OnInviteAnswered(ushort cmd, int size, InPacket packet) {
        if (!(packet is ZC.PARTY_JOIN_REQ_ACK answer)) {
            return;
        }
        // clif.hpp e_party_invite_reply
        var name = answer.Name;
        Say(answer.Result switch {
            0 => $"{name} 已經有隊伍了。",
            1 => $"{name} 拒絕了隊伍邀請。",
            2 => $"{name} 加入了隊伍。",
            3 => "隊伍人數已滿。",
            4 => "同一個帳號的角色已經在隊伍裡。",
            5 => $"{name} 設定了拒絕隊伍邀請。",
            7 => $"{name} 不在線上，或沒有這個角色。",
            8 => $"{name} 所在的地圖無法組隊。",
            9 => "這張地圖無法加入隊伍。",
            10 => "在副本裡無法邀請或離開隊伍。",
            _ => $"邀請 {name} 失敗 ({answer.Result})。"
        });
    }

    private void OnPartyChat(ushort cmd, int size, InPacket packet) {
        if (packet is ZC.NOTIFY_CHAT_PARTY chat) {
            Say(chat.Message);
        }
    }

    private void OnLeaderChanged(ushort cmd, int size, InPacket packet) {
        if (!(packet is ZC.CHANGE_GROUP_MASTER change)) {
            return;
        }
        foreach (var member in Members) {
            member.IsLeader = member.AID == change.NewLeaderAID;
        }
        var leader = FindMember(change.NewLeaderAID);
        if (leader != null) {
            Say(IsSelf(leader.AID) ? "你成為了隊長。" : $"隊長換成了 {leader.Name}。");
        }
        Changed();
    }

    private void OnMemberAlive(ushort cmd, int size, InPacket packet) {
        if (packet is ZC.GROUP_ISALIVE alive && FindMember(alive.AID) is Member member) {
            member.IsDead = alive.IsDead;
            Changed();
        }
    }

    private void OnMemberInfo(ushort cmd, int size, InPacket packet) {
        if (packet is ZC.NOTIFY_MEMBERINFO_TO_GROUPM info && FindMember(info.AID) is Member member) {
            member.Job = info.Job;
            member.BaseLevel = info.BaseLevel;
            Changed();
        }
    }

    #endregion

    #region Requests

    private bool CreateParty() {
        if (string.IsNullOrWhiteSpace(NewPartyName)) {
            Say("請先輸入隊伍名稱。");
            return false;
        }
        new CZ.MAKE_GROUP2(NewPartyName, NewSharePickup, NewShareLoot).Send();
        return true;
    }

    public bool Invite(string name) {
        if (!InParty) {
            Say("要先建立隊伍才能邀請別人。");
            Show();
            return false;
        }
        if (string.IsNullOrWhiteSpace(name)) {
            Say("請先輸入要邀請的角色名稱。");
            return false;
        }
        new CZ.PARTY_JOIN_REQ(name).Send();
        Say($"已邀請 {name} 加入隊伍。");
        return true;
    }

    private void SendSettings(bool shareExp, bool sharePickup, bool shareLoot) {
        new CZ.GROUPINFO_CHANGE_V2(shareExp, sharePickup, shareLoot).Send();
    }

    /// <summary>
    /// What can be done with another player tapped on the map
    /// </summary>
    public void ShowPlayerMenu(Entity player) {
        var name = player.GetBaseStatus().name;
        var options = new List<KeyValuePair<string, int>> {
            new KeyValuePair<string, int>("密語", 2)
        };
        if (FindMember(player.AID) == null) {
            options.Add(new KeyValuePair<string, int>(InParty ? "邀請加入隊伍" : "邀請加入隊伍 (要先建立隊伍)", 1));
        }
        AaWidgets.Pick(UI.transform as RectTransform, name, options, 0, false, choice => {
            if (choice == 1) {
                Invite(name);
            } else if (choice == 2 && UI.ChatBox != null) {
                UI.ChatBox.StartWhisper(name);
            }
        });
    }

    /// <summary>
    /// A member of the list tapped: whisper; the leader also hands the lead over or expels
    /// </summary>
    private void ShowMemberMenu(Member member) {
        var self = IsSelf(member.AID);
        var options = new List<KeyValuePair<string, int>>();
        if (!self) {
            options.Add(new KeyValuePair<string, int>("密語", 1));
            if (IAmLeader) {
                if (member.IsOnline) {
                    options.Add(new KeyValuePair<string, int>("委任隊長", 2));
                }
                options.Add(new KeyValuePair<string, int>("踢出隊伍", 3));
            }
        } else {
            options.Add(new KeyValuePair<string, int>("離開隊伍", 4));
        }
        AaWidgets.Pick(UI.transform as RectTransform, member.Name, options, 0, false, choice => {
            switch (choice) {
                case 1:
                    if (UI.ChatBox != null) {
                        UI.ChatBox.StartWhisper(member.Name);
                    }
                    break;
                case 2:
                    Confirm($"要把隊長交給 {member.Name} 嗎?", "委任", () => new CZ.CHANGE_GROUP_MASTER(member.AID).Send());
                    break;
                case 3:
                    Confirm($"要把 {member.Name} 踢出隊伍嗎?", "踢出", () => new CZ.REQ_EXPEL_GROUP_MEMBER(member.AID, member.Name).Send());
                    break;
                case 4:
                    ConfirmLeave();
                    break;
            }
        });
    }

    private void ShowInvite(uint partyId, string partyName) {
        if (InvitePopup != null) {
            Destroy(InvitePopup);
        }
        // Answered one way or the other: the server keeps the invite until then
        var panel = OpenDialog("隊伍邀請", false, out InvitePopup);
        AaWidgets.Label(panel, $"「{partyName}」邀請你加入隊伍。");
        var buttons = AaWidgets.Row(panel, 40f);
        AaWidgets.Button(buttons, "加入", () => AnswerInvite(partyId, true), -1f, AaWidgets.SelectedColor);
        AaWidgets.Button(buttons, "拒絕", () => AnswerInvite(partyId, false));
    }

    private void AnswerInvite(uint partyId, bool accept) {
        new CZ.PARTY_JOIN_REQ_ACK(partyId, accept).Send();
        if (InvitePopup != null) {
            Destroy(InvitePopup);
            InvitePopup = null;
        }
    }

    #endregion

    #region Dialogs

    /// <summary>
    /// A dialog over the screen, like the auto attack window's pickers; tapping outside closes it
    /// when <paramref name="dismissable"/>
    /// </summary>
    private RectTransform OpenDialog(string title, bool dismissable, out GameObject overlayObject) {
        var overlay = AaWidgets.NewImage("Party Dialog", UI.transform, new Color(0f, 0f, 0f, 0.35f));
        AaWidgets.Stretch(overlay.rectTransform);
        var created = overlay.gameObject;
        overlayObject = created;
        if (dismissable) {
            overlay.gameObject.AddComponent<Button>().onClick.AddListener(() => Destroy(created));
        }

        var panel = AaWidgets.NewImage("Panel", overlay.transform, AaWidgets.PanelColor);
        // Taps on the panel stay on it
        panel.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;
        panel.rectTransform.sizeDelta = new Vector2(Mathf.Min(380f, (UI.transform as RectTransform).rect.width - 32f), 0f);
        var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(18, 18, 14, 14);
        layout.spacing = 8f;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        AaWidgets.Header(panel.transform, title);
        overlay.transform.SetAsLastSibling();
        return panel.rectTransform;
    }

    private RectTransform OpenDialog(string title) {
        CloseDialog();
        var panel = OpenDialog(title, true, out var overlay);
        Dialog = overlay;
        return panel;
    }

    private void CloseDialog() {
        if (Dialog != null) {
            Destroy(Dialog);
            Dialog = null;
        }
    }

    private void Confirm(string message, string action, Action onConfirm) {
        var panel = OpenDialog("確認");
        AaWidgets.Label(panel, message);
        var buttons = AaWidgets.Row(panel, 40f);
        AaWidgets.Button(buttons, action, () => {
            CloseDialog();
            onConfirm();
        }, -1f, ConfirmColor);
        AaWidgets.Button(buttons, "取消", CloseDialog);
    }

    private void ConfirmLeave() {
        Confirm("確定要離開隊伍嗎?", "離開", () => new CZ.REQ_LEAVE_GROUP().Send());
    }

    private void OpenCreateDialog() {
        var panel = OpenDialog("建立隊伍");
        var row = AaWidgets.Row(panel);
        var field = AaWidgets.TextField(row, NewPartyName, "隊伍名稱", 23, v => NewPartyName = v);
        AaWidgets.Toggle(panel, "撿到的道具由隊伍分配", NewSharePickup, v => NewSharePickup = v);
        AaWidgets.Toggle(panel, "道具平均分給隊員", NewShareLoot, v => NewShareLoot = v);
        var buttons = AaWidgets.Row(panel, 40f);
        AaWidgets.Button(buttons, "建立", () => {
            // The typed name even when the field hasn't been left yet
            NewPartyName = field.text.Trim();
            if (CreateParty()) {
                CloseDialog();
            }
        }, -1f, AaWidgets.SelectedColor);
        AaWidgets.Button(buttons, "取消", CloseDialog);
    }

    private void OpenInviteDialog() {
        var panel = OpenDialog("邀請加入隊伍");
        var row = AaWidgets.Row(panel);
        var field = AaWidgets.TextField(row, InviteName, "角色名稱", 23, v => InviteName = v);
        AaWidgets.Label(panel, "也可以直接點地圖上的玩家來邀請。", AaWidgets.SMALL_FONT_SIZE, AaWidgets.DimTextColor);
        var buttons = AaWidgets.Row(panel, 40f);
        AaWidgets.Button(buttons, "邀請", () => {
            InviteName = field.text.Trim();
            if (Invite(InviteName)) {
                CloseDialog();
            }
        }, -1f, AaWidgets.SelectedColor);
        AaWidgets.Button(buttons, "取消", CloseDialog);
    }

    private void OpenSettingsDialog() {
        var panel = OpenDialog("隊伍設定");
        if (InParty) {
            var leader = IAmLeader;
            if (!leader) {
                AaWidgets.Label(panel, "只有隊長可以修改分配方式。", AaWidgets.SMALL_FONT_SIZE, AaWidgets.DimTextColor);
            }
            var expLabel = ExpOption == 2 ? "經驗值平均分配 (等級差距太大，無法平均)" : "經驗值平均分配";
            AaWidgets.Toggle(panel, expLabel, ExpOption == 1, v => SendSettings(v, SharePickup, ShareLoot), leader && ExpOption != 2);
            AaWidgets.Toggle(panel, "撿到的道具由隊伍分配", SharePickup, v => SendSettings(ExpOption == 1, v, ShareLoot), leader);
            AaWidgets.Toggle(panel, "道具平均分給隊員", ShareLoot, v => SendSettings(ExpOption == 1, SharePickup, v), leader);
        }
        AaWidgets.Toggle(panel, "拒絕別人的隊伍邀請", RefuseInvites, v => new CZ.PARTY_CONFIG(v).Send());
        AaWidgets.Label(panel, "隊伍聊天：在訊息前面加 % 送出，例如「%大家好」。", AaWidgets.SMALL_FONT_SIZE, AaWidgets.DimTextColor);
        var buttons = AaWidgets.Row(panel, 40f);
        AaWidgets.Button(buttons, "關閉", CloseDialog);
    }

    #endregion

    #region Building

    private void Build() {
        Root.anchorMin = Root.anchorMax = Root.pivot = new Vector2(0.5f, 0.5f);
        Root.sizeDelta = new Vector2(WIDTH, HEIGHT);

        Title = RoWidgets.TitleBar(Root, "隊伍");
        WindowCloseButton.Add(Root, Hide);
        // Dragging the title moves the window
        var drag = Title.transform.parent.gameObject.AddComponent<EventTrigger>();
        var dragEntry = new EventTrigger.Entry { eventID = EventTriggerType.Drag };
        dragEntry.callback.AddListener(data => {
            Root.anchoredPosition += ((PointerEventData) data).delta / GetComponentInParent<Canvas>().scaleFactor;
        });
        drag.triggers.Add(dragEntry);

        Content = AaWidgets.ScrollList(Root, true, out ContentScroll, 0f, new RectOffset(0, 0, 2, 2));
        var listRect = ContentScroll.transform as RectTransform;
        AaWidgets.Stretch(listRect);
        listRect.offsetMin = new Vector2(1f, TOOLBAR_HEIGHT);
        listRect.offsetMax = new Vector2(-1f, -TITLE_HEIGHT);

        // No party: the friends window's picture
        var empty = AaWidgets.NewRect("Empty", Root);
        AaWidgets.Stretch(empty);
        empty.offsetMin = listRect.offsetMin;
        empty.offsetMax = listRect.offsetMax;
        Empty = empty.gameObject;
        var picture = RoWidgets.Texture("renewalparty/img_friend2.bmp");
        var image = new GameObject("Picture", typeof(RectTransform)).AddComponent<RawImage>();
        image.transform.SetParent(empty, false);
        image.texture = picture;
        image.color = picture != null ? Color.white : Color.clear;
        image.raycastTarget = false;
        image.rectTransform.anchoredPosition = new Vector2(0f, 14f);
        image.rectTransform.sizeDelta = new Vector2(172f, 74f);
        var hint = AaWidgets.Text(empty, "目前沒有加入隊伍", 12f, OfflineColor, TextAlignmentOptions.Center);
        hint.rectTransform.anchoredPosition = new Vector2(0f, -40f);
        hint.rectTransform.sizeDelta = new Vector2(260f, 20f);

        Toolbar = AaWidgets.NewImage("Toolbar", Root, ToolbarColor).rectTransform;
        Toolbar.anchorMin = Vector2.zero;
        Toolbar.anchorMax = new Vector2(1f, 0f);
        Toolbar.pivot = new Vector2(0.5f, 0f);
        Toolbar.anchoredPosition = Vector2.zero;
        Toolbar.sizeDelta = new Vector2(0f, TOOLBAR_HEIGHT);
        var line = AaWidgets.NewImage("Line", Toolbar, LineColor);
        line.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        line.rectTransform.anchorMin = new Vector2(0f, 1f);
        line.rectTransform.anchorMax = Vector2.one;
        line.rectTransform.pivot = new Vector2(0.5f, 1f);
        line.rectTransform.sizeDelta = new Vector2(0f, 1f);
        var layout = Toolbar.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(6, 8, 4, 4);
        layout.spacing = 4f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
    }

    private void Refresh(bool keepScroll = true) {
        Dirty = false;
        NextRefresh = Time.unscaledTime + REFRESH_SECONDS;
        if (!gameObject.activeInHierarchy) {
            return;
        }

        Title.text = InParty ? $"隊伍({PartyName})" : "隊伍";
        Empty.SetActive(!InParty);
        ContentScroll.gameObject.SetActive(InParty);
        var scroll = ContentScroll.verticalNormalizedPosition;
        AaWidgets.Clear(Content);
        if (InParty) {
            foreach (var member in Members.OrderByDescending(m => m.IsLeader).ThenByDescending(m => m.IsOnline)) {
                MemberRow(member);
            }
        }
        BuildToolbar();
        Canvas.ForceUpdateCanvases();
        ContentScroll.verticalNormalizedPosition = keepScroll ? scroll : 1f;
    }

    private void BuildToolbar() {
        for (var i = Toolbar.childCount - 1; i >= 0; i--) {
            var child = Toolbar.GetChild(i).gameObject;
            if (child.name != "Line") {
                child.SetActive(false);
                Destroy(child);
            }
        }
        if (InParty) {
            RoWidgets.Button(Toolbar, "邀請", OpenInviteDialog);
            RoWidgets.Button(Toolbar, "設定", OpenSettingsDialog);
            RoWidgets.Button(Toolbar, "離開", ConfirmLeave);
        } else {
            RoWidgets.Button(Toolbar, "建立隊伍", OpenCreateDialog);
            RoWidgets.Button(Toolbar, "設定", OpenSettingsDialog);
        }
        AaWidgets.Layout(AaWidgets.NewRect("Spacer", Toolbar), flexibleWidth: 1f);
        if (InParty) {
            var count = AaWidgets.Text(Toolbar, $"隊員 {Members.Count}/{MAX_MEMBERS}", 12f, RoWidgets.TextColor, TextAlignmentOptions.MidlineRight);
            AaWidgets.Layout(count, 72f);
        }
    }

    /// <summary>
    /// Job icon, level, name and map, whether online (ON, ME, OFF), and the HP bar when known
    /// </summary>
    private void MemberRow(Member member) {
        var self = IsSelf(member.AID);
        var row = AaWidgets.NewImage("Member", Content, Color.white);
        AaWidgets.Layout(row, height: ROW_HEIGHT);
        var button = row.gameObject.AddComponent<Button>();
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = colors.pressedColor = colors.selectedColor = PointedRowColor;
        button.colors = colors;
        button.targetGraphic = row;
        button.onClick.AddListener(() => ShowMemberMenu(member));

        var icon = RoWidgets.Picture(row.transform, "Job", JobIcon(member), new Vector2(6f, -6f), new Vector2(25f, 25f));
        if (!member.IsOnline) {
            icon.color = new Color(1f, 1f, 1f, 0.45f);
        }
        if (member.IsLeader) {
            RoWidgets.Picture(row.transform, "Crown", RoWidgets.Texture("renewalparty/ico_partycrown.bmp"), new Vector2(8f, -1f), new Vector2(21f, 10f));
        }

        var color = !member.IsOnline ? OfflineColor : self ? SelfColor : OnlineColor;
        var level = member.IsOnline ? $"<color={LevelColor}>Lv.{member.BaseLevel}</color>" : $"Lv.{member.BaseLevel}";
        var map = member.IsOnline ? $"({MapDisplayName(member.Map)})" : "";
        RoWidgets.Text(row.transform, $"{level} <noparse>{member.Name}</noparse>{map}", 12f, color, new Vector2(38f, -4f), new Vector2(WIDTH - 38f - 40f, 16f));

        var state = self ? "icon_party_me" : member.IsOnline ? "icon_party_on" : "icon_party_off";
        var badge = RoWidgets.Picture(row.transform, "State", RoWidgets.Texture($"renewalparty/{state}.bmp"), Vector2.zero, new Vector2(26f, 11f));
        badge.rectTransform.anchorMin = badge.rectTransform.anchorMax = badge.rectTransform.pivot = Vector2.one;
        badge.rectTransform.anchoredPosition = new Vector2(-8f, -6f);

        long hp = member.Hp;
        long maxHp = member.MaxHp;
        var status = self ? SelfStatus() : null;
        if (status != null) {
            hp = status.hp;
            maxHp = status.max_hp;
        }
        if (!member.IsOnline || maxHp <= 0) {
            return;
        }
        var dead = member.IsDead || hp <= 0;
        var bar = RoWidgets.Picture(row.transform, "HP", RoWidgets.Texture(dead ? "renewalparty/img_hpbar_mini_die.bmp" : "renewalparty/img_hpbar_mini_logout.bmp"),
            new Vector2(38f, -23f), new Vector2(HP_BAR_WIDTH, 6f));
        if (!dead) {
            var ratio = Mathf.Clamp01((float) hp / maxHp);
            var fill = RoWidgets.Picture(bar.transform, "Fill", RoWidgets.Texture("renewalparty/img_hpbar_mini.bmp"), Vector2.zero, new Vector2(HP_BAR_WIDTH * ratio, 6f));
            fill.uvRect = new Rect(0f, 0f, ratio, 1f);
        }
        RoWidgets.Text(row.transform, $"{Math.Max(hp, 0)}/{maxHp}", 10f, RoWidgets.TextColor, new Vector2(38f + HP_BAR_WIDTH + 4f, -20f), new Vector2(100f, 12f));
    }

    private static Texture2D JobIcon(Member member) {
        var dead = member.IsDead ? "_die" : "";
        var icon = RoWidgets.Texture($"renewalparty/icon_jobs_{member.Job}{dead}.bmp");
        return icon != null ? icon : RoWidgets.Texture($"renewalparty/icon_jobs_0{dead}.bmp");
    }

    /// <summary>
    /// The map's name in the client's language (mapnametable), its file name when it has none
    /// </summary>
    private static string MapDisplayName(string map) {
        if (string.IsNullOrEmpty(map)) {
            return "-";
        }
        var id = Path.GetFileNameWithoutExtension(map);
        if (Tables.MapTable.TryGetValue(id + ".rsw", out var entry) && !string.IsNullOrWhiteSpace(entry.name) && entry.name.IndexOf('�') < 0) {
            return entry.name.Trim();
        }
        return id;
    }

    #endregion
}
