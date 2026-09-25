using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// The party (組隊): who is in it, where they are and how they're doing; making one, inviting,
/// leaving and the leader's settings. It also answers invites, shows party chat and puts party
/// members' HP bars over their heads. Built at runtime with AaWidgets, like the auto attack window.
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

    private const float WIDTH = 480f;
    private const float HEIGHT = 440f;
    private const float TITLE_HEIGHT = 40f;
    private const float REFRESH_SECONDS = 0.3f;
    private const float CONFIRM_SECONDS = 3f;
    private static readonly Color PartyChatColor = new Color32(255, 214, 140, 255);
    private static readonly Color ConfirmColor = new Color(0.6f, 0.18f, 0.18f, 1f);

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
    private TextMeshProUGUI Title;
    private GameObject InvitePopup;

    private string NewPartyName = "";
    private string InviteName = "";
    private bool NewSharePickup;
    private bool NewShareLoot;
    private float LeaveArmedUntil;
    private uint ExpelArmedFor;
    private float ExpelArmedUntil;
    private bool Dirty;
    private float NextRefresh;

    public static PartyWindow Create(MapUiController ui) {
        var root = AaWidgets.NewImage("Party Window", ui.transform, AaWidgets.PanelColor);
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
        // Not while a name is being typed in: rebuilding the list would take the field away
        if (Dirty && Time.unscaledTime >= NextRefresh && !IsTyping()) {
            Refresh();
        }
    }

    public void Show() {
        var canvas = (UI.transform as RectTransform).rect.size;
        Root.anchorMin = Root.anchorMax = Root.pivot = new Vector2(0.5f, 0.5f);
        Root.sizeDelta = new Vector2(Mathf.Min(WIDTH, canvas.x - 16f), Mathf.Min(HEIGHT, canvas.y - 16f));
        Root.anchoredPosition = Vector2.zero;
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

    private Member Me => Members.FirstOrDefault(member => IsSelf(member.AID));

    private bool IAmLeader => Me != null && Me.IsLeader;

    private Member FindMember(uint accountId) => Members.FirstOrDefault(member => member.AID == accountId);

    private void Changed() {
        Dirty = true;
    }

    private bool IsTyping() {
        if (EventSystem.current == null) {
            return false;
        }
        var selected = EventSystem.current.currentSelectedGameObject;
        if (selected == null || !selected.transform.IsChildOf(transform)) {
            return false;
        }
        var input = selected.GetComponent<TMP_InputField>();
        return input != null && input.isFocused;
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

    private void CreateParty() {
        if (string.IsNullOrWhiteSpace(NewPartyName)) {
            Say("請先輸入隊伍名稱。");
            return;
        }
        new CZ.MAKE_GROUP2(NewPartyName, NewSharePickup, NewShareLoot).Send();
    }

    public void Invite(string name) {
        if (!InParty) {
            Say("要先建立隊伍才能邀請別人。");
            Show();
            return;
        }
        if (string.IsNullOrWhiteSpace(name)) {
            Say("請先輸入要邀請的角色名稱。");
            return;
        }
        new CZ.PARTY_JOIN_REQ(name).Send();
        Say($"已邀請 {name} 加入隊伍。");
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

    private void ShowInvite(uint partyId, string partyName) {
        if (InvitePopup != null) {
            Destroy(InvitePopup);
        }

        var overlay = AaWidgets.NewImage("Party Invite", UI.transform, new Color(0f, 0f, 0f, 0.35f));
        AaWidgets.Stretch(overlay.rectTransform);
        InvitePopup = overlay.gameObject;

        var panel = AaWidgets.NewImage("Panel", overlay.transform, AaWidgets.PanelColor);
        panel.rectTransform.sizeDelta = new Vector2(380f, 170f);
        var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(18, 18, 14, 14);
        layout.spacing = 10f;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        AaWidgets.Header(panel.transform, "隊伍邀請");
        AaWidgets.Label(panel.transform, $"「{partyName}」邀請你加入隊伍。");
        var buttons = AaWidgets.Row(panel.transform, 40f);
        AaWidgets.Button(buttons, "加入", () => AnswerInvite(partyId, true), -1f, AaWidgets.SelectedColor);
        AaWidgets.Button(buttons, "拒絕", () => AnswerInvite(partyId, false));
        overlay.transform.SetAsLastSibling();
    }

    private void AnswerInvite(uint partyId, bool accept) {
        new CZ.PARTY_JOIN_REQ_ACK(partyId, accept).Send();
        if (InvitePopup != null) {
            Destroy(InvitePopup);
            InvitePopup = null;
        }
    }

    #endregion

    #region Building

    private void Build() {
        var title = AaWidgets.NewImage("Title", Root, AaWidgets.BarColor);
        var titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.sizeDelta = new Vector2(0f, TITLE_HEIGHT);
        Title = AaWidgets.Text(title.transform, "隊伍", 19f, AaWidgets.AccentColor);
        AaWidgets.Stretch(Title.rectTransform);
        Title.rectTransform.offsetMin = new Vector2(14f, 0f);
        Title.rectTransform.offsetMax = new Vector2(-60f, 0f);
        Title.fontStyle = FontStyles.Bold;

        var close = AaWidgets.Button(title.transform, "X", Hide, 44f);
        var closeRect = close.transform as RectTransform;
        closeRect.anchorMin = closeRect.anchorMax = closeRect.pivot = new Vector2(1f, 0.5f);
        closeRect.anchoredPosition = new Vector2(-6f, 0f);
        closeRect.sizeDelta = new Vector2(44f, 32f);

        // Dragging the title moves the window
        var drag = title.gameObject.AddComponent<EventTrigger>();
        var dragEntry = new EventTrigger.Entry { eventID = EventTriggerType.Drag };
        dragEntry.callback.AddListener(data => {
            Root.anchoredPosition += ((PointerEventData) data).delta / GetComponentInParent<Canvas>().scaleFactor;
        });
        drag.triggers.Add(dragEntry);

        Content = AaWidgets.ScrollList(Root, true, out ContentScroll, 6f, new RectOffset(16, 16, 10, 14));
        var contentRect = ContentScroll.transform as RectTransform;
        AaWidgets.Stretch(contentRect);
        contentRect.offsetMax = new Vector2(0f, -TITLE_HEIGHT);
    }

    private void Refresh(bool keepScroll = true) {
        Dirty = false;
        NextRefresh = Time.unscaledTime + REFRESH_SECONDS;
        if (!gameObject.activeInHierarchy) {
            return;
        }

        var scroll = ContentScroll.verticalNormalizedPosition;
        AaWidgets.Clear(Content);
        Title.text = InParty ? $"隊伍 - {PartyName}" : "隊伍";
        if (InParty) {
            BuildParty();
        } else {
            BuildNoParty();
        }
        Canvas.ForceUpdateCanvases();
        ContentScroll.verticalNormalizedPosition = keepScroll ? scroll : 1f;
    }

    private void BuildNoParty() {
        AaWidgets.Label(Content, "目前沒有加入隊伍。建立一個隊伍，或等別人邀請你。", AaWidgets.FONT_SIZE, AaWidgets.DimTextColor);

        AaWidgets.Header(Content, "建立隊伍");
        var row = AaWidgets.Row(Content);
        AaWidgets.TextField(row, NewPartyName, "隊伍名稱", 23, v => NewPartyName = v);
        AaWidgets.Button(row, "建立", CreateParty, 90f, AaWidgets.SelectedColor);
        AaWidgets.Toggle(Content, "撿到的道具由隊伍分配", NewSharePickup, v => NewSharePickup = v);
        AaWidgets.Toggle(Content, "道具平均分給隊員", NewShareLoot, v => NewShareLoot = v);

        AaWidgets.Header(Content, "邀請");
        AaWidgets.Toggle(Content, "拒絕別人的隊伍邀請", RefuseInvites, v => new CZ.PARTY_CONFIG(v).Send());
    }

    private void BuildParty() {
        var online = Members.Count(member => member.IsOnline);
        AaWidgets.Header(Content, $"隊員  {online} / {Members.Count} 人在線上");
        foreach (var member in Members.OrderByDescending(m => m.IsLeader).ThenByDescending(m => m.IsOnline)) {
            MemberRows(member);
        }

        AaWidgets.Header(Content, "邀請");
        var invite = AaWidgets.Row(Content);
        AaWidgets.TextField(invite, InviteName, "角色名稱", 23, v => InviteName = v);
        AaWidgets.Button(invite, "邀請", () => Invite(InviteName), 90f, AaWidgets.SelectedColor);
        AaWidgets.Label(Content, "也可以直接點地圖上的玩家來邀請。", AaWidgets.SMALL_FONT_SIZE, AaWidgets.DimTextColor);

        var leader = IAmLeader;
        AaWidgets.Header(Content, leader ? "設定" : "設定 (隊長才能修改)");
        var expLabel = ExpOption == 2 ? "經驗值平均分配 (等級差距太大，無法平均)" : "經驗值平均分配";
        AaWidgets.Toggle(Content, expLabel, ExpOption == 1, v => SendSettings(v, SharePickup, ShareLoot), leader && ExpOption != 2);
        AaWidgets.Toggle(Content, "撿到的道具由隊伍分配", SharePickup, v => SendSettings(ExpOption == 1, v, ShareLoot), leader);
        AaWidgets.Toggle(Content, "道具平均分給隊員", ShareLoot, v => SendSettings(ExpOption == 1, SharePickup, v), leader);
        AaWidgets.Toggle(Content, "拒絕別人的隊伍邀請", RefuseInvites, v => new CZ.PARTY_CONFIG(v).Send());
        AaWidgets.Label(Content, "隊伍聊天：在訊息前面加 % 送出，例如「%大家好」。", AaWidgets.SMALL_FONT_SIZE, AaWidgets.DimTextColor);

        // Two taps: a stray one mustn't break the party up
        var leave = AaWidgets.Row(Content);
        var leaveArmed = Time.unscaledTime < LeaveArmedUntil;
        Button leaveButton = null;
        leaveButton = AaWidgets.Button(leave, leaveArmed ? "再按一次確認離開" : "離開隊伍", () => {
            if (Time.unscaledTime < LeaveArmedUntil) {
                LeaveArmedUntil = 0f;
                new CZ.REQ_LEAVE_GROUP().Send();
                return;
            }
            LeaveArmedUntil = Time.unscaledTime + CONFIRM_SECONDS;
            AaWidgets.SetButtonText(leaveButton, "再按一次確認離開");
            leaveButton.targetGraphic.color = ConfirmColor;
        }, 180f, leaveArmed ? ConfirmColor : (Color?) null);
    }

    private void MemberRows(Member member) {
        var self = IsSelf(member.AID);
        var row = AaWidgets.Row(Content);
        var name = member.Name + (member.IsLeader ? "  (隊長)" : "") + (self ? "  (你)" : "");
        var nameText = AaWidgets.Text(row, name, AaWidgets.FONT_SIZE, member.IsOnline ? AaWidgets.TextColor : AaWidgets.DimTextColor);
        nameText.enableWordWrapping = false;
        nameText.overflowMode = TextOverflowModes.Ellipsis;
        AaWidgets.Layout(nameText, flexibleWidth: 1f);

        if (IAmLeader && !self) {
            var makeLeader = AaWidgets.Button(row, "設為隊長", () => new CZ.CHANGE_GROUP_MASTER(member.AID).Send(), 90f);
            makeLeader.interactable = member.IsOnline;

            var expelArmed = ExpelArmedFor == member.AID && Time.unscaledTime < ExpelArmedUntil;
            Button expel = null;
            expel = AaWidgets.Button(row, expelArmed ? "確認踢出" : "踢出", () => {
                if (ExpelArmedFor == member.AID && Time.unscaledTime < ExpelArmedUntil) {
                    ExpelArmedFor = 0;
                    new CZ.REQ_EXPEL_GROUP_MEMBER(member.AID, member.Name).Send();
                    return;
                }
                ExpelArmedFor = member.AID;
                ExpelArmedUntil = Time.unscaledTime + CONFIRM_SECONDS;
                AaWidgets.SetButtonText(expel, "確認踢出");
                expel.targetGraphic.color = ConfirmColor;
            }, 80f, expelArmed ? ConfirmColor : (Color?) null);
        }

        var details = $"Lv {member.BaseLevel} {JobName(member.Job)}  |  {MapName(member.Map)}";
        if (!member.IsOnline) {
            details += "  |  離線";
        } else if (member.IsDead) {
            details += "  |  死亡";
        } else if (member.MaxHp > 0) {
            details += $"  |  HP {member.Hp} / {member.MaxHp}";
        }
        var info = AaWidgets.Row(Content, 22f);
        AaWidgets.Layout(AaWidgets.Text(info, details, AaWidgets.SMALL_FONT_SIZE, AaWidgets.DimTextColor), flexibleWidth: 1f);
    }

    private static string JobName(short job) {
        try {
            return JobHelper.GetJobName(job, 1);
        } catch (KeyNotFoundException) {
            return $"Job {job}";
        }
    }

    private static string MapName(string map) {
        return string.IsNullOrEmpty(map) ? "-" : Path.GetFileNameWithoutExtension(map);
    }

    #endregion
}
