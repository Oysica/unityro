using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ROIO;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// The party (組隊) and friends (朋友) window, laid out as the official client's (renewalparty
/// textures), the two lists switched at its bottom. The party: each member's job, level, name
/// and map, whether they're online, and the HP of those we can see; its toolbar makes a party,
/// invites, sets it up and leaves, and tapping a member whispers, hands the lead over or expels.
/// The friends: who's online; adding, whispering, inviting to the party and removing. It also
/// answers party and friend requests, shows party chat and puts party members' HP bars over
/// their heads.
/// </summary>
public class PartyWindow : MonoBehaviour {

    public enum Tab {
        Party,
        Friends
    }

    public class Friend {
        public uint AID;
        public uint CID;
        public string Name;
        public bool IsOnline;
    }

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
    public const int MAX_FRIENDS = 40;
    private const float WIDTH = 290f;
    private const float HEIGHT = 320f;
    private const float TITLE_HEIGHT = 17f;
    private const float TOOLBAR_HEIGHT = 28f;
    // 朋友 / 隊伍 under the toolbar
    private const float TAB_HEIGHT = 20f;
    private const float ROW_HEIGHT = 36f;
    private const float FRIEND_ROW_HEIGHT = 26f;
    // Friends logging in are announced, but not the ones online when we do
    private const float FRIEND_NOTICE_DELAY = 3f;
    private const float HP_BAR_WIDTH = 60f;
    // The invite, settings, ... windows beside it
    private const float SIDE_WIDTH = 160f;
    private const float REFRESH_SECONDS = 0.3f;
    private static readonly Color PartyChatColor = new Color32(255, 214, 140, 255);
    private static readonly Color SettingsColor = new Color32(255, 255, 0, 255);
    private static readonly Color FriendChatColor = new Color32(160, 255, 160, 255);
    // The official window's colours
    private static readonly Color OnlineColor = new Color32(8, 49, 123, 255);
    private static readonly Color SelfColor = new Color32(0, 123, 123, 255);
    private static readonly Color OfflineColor = new Color32(132, 140, 165, 255);
    private const string LevelColor = "#31394A";
    private static readonly Color ToolbarColor = new Color32(247, 247, 247, 255);
    private static readonly Color PointedRowColor = new Color32(222, 231, 247, 255);

    public static PartyWindow Instance { get; private set; }

    public string PartyName { get; private set; }
    public bool InParty => PartyName != null;
    public readonly List<Member> Members = new List<Member>();
    public readonly List<Friend> Friends = new List<Friend>();
    public Tab CurrentTab { get; private set; } = Tab.Party;

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
    private GameObject EmptyParty;
    private GameObject EmptyFriends;
    private TextMeshProUGUI Title;
    private Toggle FriendsTab;
    private Toggle PartyTab;
    private TextMeshProUGUI MemberCount;
    private GameObject InvitePopup;
    private GameObject FriendRequestPopup;
    private GameObject Dialog;
    private float FriendNoticesFrom;

    private string NewPartyName = "";
    private string InviteName = "";
    private string NewFriendName = "";
    private bool NewSharePickup;
    private bool NewShareLoot;
    private bool Dirty;
    private float NextRefresh;
    private long DrawnSelfHp = -1;
    private long DrawnSelfMaxHp = -1;

    public static PartyWindow Create(MapUiController ui) {
        var root = AaWidgets.NewImage("Party Window", ui.transform, Color.white);
        root.gameObject.AddComponent<Outline>().effectColor = RoWidgets.LineColor;
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
        network.HookPacket(ZC.FRIENDS_LIST.HEADER, OnFriendsList);
        network.HookPacket(ZC.FRIENDS_STATE.HEADER, OnFriendState);
        network.HookPacket(ZC.REQ_ADD_FRIENDS.HEADER, OnFriendRequest);
        network.HookPacket(ZC.ADD_FRIENDS_LIST.HEADER, OnFriendAdded);
        network.HookPacket(ZC.DELETE_FRIENDS.HEADER, OnFriendDeleted);
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
        CloseDialog();
        gameObject.SetActive(false);
    }

    public void ToggleVisible() {
        if (gameObject.activeSelf) {
            Hide();
        } else {
            Show();
        }
    }

    /// <summary>
    /// Alt+Z (party) and Alt+H (friends): opens on that list, closes when it's already showing it
    /// </summary>
    public void ToggleTab(Tab tab) {
        if (gameObject.activeSelf && CurrentTab == tab) {
            Hide();
            return;
        }
        SwitchTab(tab);
        if (!gameObject.activeSelf) {
            Show();
        }
    }

    private void SwitchTab(Tab tab) {
        if (CurrentTab == tab) {
            return;
        }
        CurrentTab = tab;
        CloseDialog();
        if (gameObject.activeInHierarchy) {
            Refresh(keepScroll: false);
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

    private Friend FindFriend(uint accountId) => Friends.FirstOrDefault(friend => friend.AID == accountId);

    private void Changed() {
        Dirty = true;
    }

    private void Say(string text) {
        Say(text, PartyChatColor);
    }

    // The party's under the chat's 隊伍 tab
    private void Say(string text, Color color, ChatBoxController.Category category = ChatBoxController.Category.Party) {
        if (UI != null && UI.ChatBox != null) {
            UI.ChatBox.DisplayText(text, color, category);
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
            // In the chat as the official client does, each time the server sends them (logging in,
            // making or joining a party, a change)
            var exp = ExpOption == 0 ? "各自取得" : ExpOption == 1 ? "均等分配" : "無法均等分配 (等級差距過大)";
            Say($"隊伍設定 - 經驗值分配方式 : {exp}", SettingsColor);
            Say($"隊伍設定 - 道具蒐集方式 : {(SharePickup ? "隊伍隊員全體共有" : "各自取得")}", SettingsColor);
            Say($"隊伍設定 - 物品分配方式 : {(ShareLoot ? "均等分配" : "各自取得")}", SettingsColor);
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

    /// <summary>
    /// Sent on logging in: all of them offline, the ones online told right after
    /// </summary>
    private void OnFriendsList(ushort cmd, int size, InPacket packet) {
        if (!(packet is ZC.FRIENDS_LIST list)) {
            return;
        }
        Friends.Clear();
        foreach (var entry in list.FriendList) {
            Friends.Add(new Friend { AID = entry.AID, CID = entry.CID, Name = entry.Name });
        }
        FriendNoticesFrom = Time.unscaledTime + FRIEND_NOTICE_DELAY;
        Changed();
    }

    private void OnFriendState(ushort cmd, int size, InPacket packet) {
        if (!(packet is ZC.FRIENDS_STATE state) || !(FindFriend(state.AID) is Friend friend)) {
            return;
        }
        var changed = friend.IsOnline != state.IsOnline;
        friend.IsOnline = state.IsOnline;
        if (!string.IsNullOrEmpty(state.Name)) {
            friend.Name = state.Name;
        }
        if (changed && Time.unscaledTime >= FriendNoticesFrom) {
            Say(friend.IsOnline ? $"好友 {friend.Name} 上線了。" : $"好友 {friend.Name} 離線了。", FriendChatColor, ChatBoxController.Category.System);
        }
        Changed();
    }

    private void OnFriendRequest(ushort cmd, int size, InPacket packet) {
        if (packet is ZC.REQ_ADD_FRIENDS request) {
            ShowFriendRequest(request.AID, request.CID, request.Name);
        }
    }

    private void OnFriendAdded(ushort cmd, int size, InPacket packet) {
        if (!(packet is ZC.ADD_FRIENDS_LIST added)) {
            return;
        }
        var name = added.Name;
        switch (added.Result) {
            case 0:
                if (FindFriend(added.AID) == null) {
                    // Friends only when both were online for it
                    Friends.Add(new Friend { AID = added.AID, CID = added.CID, Name = name, IsOnline = true });
                }
                Say($"你和 {name} 成為了好友。", FriendChatColor, ChatBoxController.Category.System);
                break;
            case 1:
                Say($"{name} 拒絕了你的好友邀請。", FriendChatColor, ChatBoxController.Category.System);
                break;
            case 2:
                Say("你的好友名單已滿。", FriendChatColor, ChatBoxController.Category.System);
                break;
            default:
                Say($"{name} 的好友名單已滿。", FriendChatColor, ChatBoxController.Category.System);
                break;
        }
        Changed();
    }

    private void OnFriendDeleted(ushort cmd, int size, InPacket packet) {
        if (packet is ZC.DELETE_FRIENDS deleted && FindFriend(deleted.AID) is Friend friend) {
            Friends.Remove(friend);
            Say($"{friend.Name} 已從好友名單移除。", FriendChatColor, ChatBoxController.Category.System);
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
        if (FindFriend(player.AID) == null) {
            options.Add(new KeyValuePair<string, int>("加為好友", 3));
        }
        AaWidgets.Pick(UI.transform as RectTransform, name, options, 0, false, choice => {
            if (choice == 1) {
                Invite(name);
            } else if (choice == 2 && UI.ChatBox != null) {
                UI.ChatBox.StartWhisper(name);
            } else if (choice == 3) {
                AddFriend(name);
            }
        });
    }

    public bool AddFriend(string name) {
        if (string.IsNullOrWhiteSpace(name)) {
            Say("請先輸入角色名稱。", FriendChatColor, ChatBoxController.Category.System);
            return false;
        }
        if (Friends.Count >= MAX_FRIENDS) {
            Say("你的好友名單已滿。", FriendChatColor, ChatBoxController.Category.System);
            return false;
        }
        // They're asked; ZC_ADD_FRIENDS_LIST says what came of it
        new CZ.ADD_FRIENDS(name).Send();
        Say($"已向 {name} 送出好友邀請。", FriendChatColor, ChatBoxController.Category.System);
        return true;
    }

    /// <summary>
    /// A friend of the list tapped: whisper, invite to the party, remove
    /// </summary>
    private void ShowFriendMenu(Friend friend) {
        var options = new List<KeyValuePair<string, int>> {
            new KeyValuePair<string, int>("密語", 1)
        };
        if (friend.IsOnline && InParty && FindMember(friend.AID) == null) {
            options.Add(new KeyValuePair<string, int>("邀請加入隊伍", 2));
        }
        options.Add(new KeyValuePair<string, int>("刪除好友", 3));
        AaWidgets.Pick(UI.transform as RectTransform, friend.Name, options, 0, false, choice => {
            switch (choice) {
                case 1:
                    if (UI.ChatBox != null) {
                        UI.ChatBox.StartWhisper(friend.Name);
                    }
                    break;
                case 2:
                    Invite(friend.Name);
                    break;
                case 3:
                    Confirm("刪除好友", $"要把 {friend.Name} 從好友名單刪除嗎?", () => new CZ.DELETE_FRIENDS(friend.AID, friend.CID).Send());
                    break;
            }
        });
    }

    private void ShowFriendRequest(uint accountId, uint charId, string name) {
        if (FriendRequestPopup != null) {
            Destroy(FriendRequestPopup);
        }
        // The X refuses, as 拒絕
        var window = RoWidgets.Window(UI.transform, "Friend Request", "好友邀請", new Vector2(220f, 80f), () => AnswerFriendRequest(accountId, charId, false));
        FriendRequestPopup = window.gameObject;
        FitHeight(window, Message(window, $"{name} 想要加你為好友。", -24f));
        var buttons = RoWidgets.ButtonRow(window);
        RoWidgets.Button(buttons, "接受", () => AnswerFriendRequest(accountId, charId, true));
        RoWidgets.Button(buttons, "拒絕", () => AnswerFriendRequest(accountId, charId, false));
        window.SetAsLastSibling();
    }

    private void AnswerFriendRequest(uint accountId, uint charId, bool accept) {
        new CZ.ACK_REQ_ADD_FRIENDS(accountId, charId, accept).Send();
        if (FriendRequestPopup != null) {
            Destroy(FriendRequestPopup);
            FriendRequestPopup = null;
        }
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
                    Confirm("委任隊長", $"要把隊長交給 {member.Name} 嗎?", () => new CZ.CHANGE_GROUP_MASTER(member.AID).Send());
                    break;
                case 3:
                    Confirm("踢出隊伍", $"要把 {member.Name} 踢出隊伍嗎?", () => new CZ.REQ_EXPEL_GROUP_MEMBER(member.AID, member.Name).Send());
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
        // Answered one way or the other (the X declines): the server keeps the invite until then
        var window = RoWidgets.Window(UI.transform, "Party Invite", "隊伍邀請", new Vector2(220f, 80f), () => AnswerInvite(partyId, false));
        InvitePopup = window.gameObject;
        FitHeight(window, Message(window, $"「{partyName}」邀請你加入隊伍。", -24f));
        var buttons = RoWidgets.ButtonRow(window);
        RoWidgets.Button(buttons, "加入", () => AnswerInvite(partyId, true));
        RoWidgets.Button(buttons, "拒絕", () => AnswerInvite(partyId, false));
        window.SetAsLastSibling();
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
    /// A window of its own docked to the party window, right of it (left when the screen ends
    /// first), as the official client opens invite and settings; it goes with the party window
    /// </summary>
    private RectTransform OpenSideWindow(string title) {
        CloseDialog();
        var window = RoWidgets.Window(Root, "Party Dialog", title, new Vector2(SIDE_WIDTH, 100f), CloseDialog);
        Dialog = window.gameObject;
        var canvasHalfWidth = (UI.transform as RectTransform).rect.width / 2f;
        var right = Root.anchoredPosition.x + Root.rect.width / 2f + 2f + SIDE_WIDTH <= canvasHalfWidth;
        window.anchorMin = window.anchorMax = new Vector2(right ? 1f : 0f, 1f);
        window.pivot = new Vector2(right ? 0f : 1f, 1f);
        window.anchoredPosition = new Vector2(right ? 2f : -2f, 0f);
        return window;
    }

    private void CloseDialog() {
        if (Dialog != null) {
            Destroy(Dialog);
            Dialog = null;
        }
    }

    /// <summary>
    /// A line of text, wrapped, from <paramref name="y"/> down; returns where the next thing goes
    /// </summary>
    private static float Message(RectTransform window, string message, float y) {
        var width = window.sizeDelta.x - 16f;
        var text = RoWidgets.Text(window, message, 12f, RoWidgets.TextColor, new Vector2(8f, y), new Vector2(width, 16f));
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Overflow;
        var height = Mathf.Max(16f, text.GetPreferredValues(message, width, 0f).y);
        text.rectTransform.sizeDelta = new Vector2(width, height);
        return y - height - 6f;
    }

    /// <summary>
    /// A heading and its two choices, as the official settings window; returns the second one
    /// (on: shared, refused, ...)
    /// </summary>
    private static Toggle Choice(RectTransform window, string heading, string first, string second, bool secondOn, bool interactable, ref float y) {
        RoWidgets.Text(window, heading, 12f, RoWidgets.TextColor, new Vector2(8f, y), new Vector2(SIDE_WIDTH - 16f, 16f));
        y -= 18f;
        var group = new GameObject(heading, typeof(RectTransform)).AddComponent<ToggleGroup>();
        group.transform.SetParent(window, false);
        group.allowSwitchOff = false;
        RoWidgets.Radio(window, first, !secondOn, group, new Vector2(12f, y), SIDE_WIDTH - 20f, interactable);
        y -= 17f;
        var toggle = RoWidgets.Radio(window, second, secondOn, group, new Vector2(12f, y), SIDE_WIDTH - 20f, interactable);
        y -= 23f;
        return toggle;
    }

    /// <summary>
    /// The window as tall as what's in it down to <paramref name="y"/>, with the buttons under
    /// </summary>
    private static void FitHeight(RectTransform window, float y) {
        window.sizeDelta = new Vector2(window.sizeDelta.x, -y + RoWidgets.BUTTON_HEIGHT + 12f);
    }

    private void Confirm(string title, string message, Action onConfirm) {
        var window = OpenSideWindow(title);
        FitHeight(window, Message(window, message, -24f));
        var buttons = RoWidgets.ButtonRow(window);
        RoWidgets.Button(buttons, "確認", () => {
            CloseDialog();
            onConfirm();
        });
        RoWidgets.Button(buttons, "取消", CloseDialog);
    }

    private void ConfirmLeave() {
        Confirm("離開隊伍", "確定要離開隊伍嗎?", () => new CZ.REQ_LEAVE_GROUP().Send());
    }

    private void OpenCreateDialog() {
        var window = OpenSideWindow("建立隊伍");
        var y = -22f;
        RoWidgets.Text(window, "隊伍名稱", 12f, RoWidgets.TextColor, new Vector2(8f, y), new Vector2(SIDE_WIDTH - 16f, 16f));
        y -= 18f;
        var field = RoWidgets.InputField(window, NewPartyName, 23, new Vector2(8f, y), new Vector2(SIDE_WIDTH - 16f, 18f));
        y -= 26f;
        var pickup = Choice(window, "道具蒐集方式", "各自取得", "隊伍隊員全體共有", NewSharePickup, true, ref y);
        var loot = Choice(window, "物品分配方式", "各自取得", "均等分配", NewShareLoot, true, ref y);
        FitHeight(window, y);

        UnityAction create = () => {
            // The typed name even when the field hasn't been left yet
            NewPartyName = field.text.Trim();
            NewSharePickup = pickup.isOn;
            NewShareLoot = loot.isOn;
            if (CreateParty()) {
                CloseDialog();
            }
        };
        field.onSubmit.AddListener(_ => create());
        var buttons = RoWidgets.ButtonRow(window);
        RoWidgets.Button(buttons, "確認", create);
        RoWidgets.Button(buttons, "取消", CloseDialog);
        field.ActivateInputField();
    }

    private void OpenInviteDialog() {
        var window = OpenSideWindow("邀請加入隊伍");
        var y = -22f;
        RoWidgets.Text(window, "被邀請之角色名稱", 12f, RoWidgets.TextColor, new Vector2(8f, y), new Vector2(SIDE_WIDTH - 16f, 16f));
        y -= 18f;
        var field = RoWidgets.InputField(window, InviteName, 23, new Vector2(8f, y), new Vector2(SIDE_WIDTH - 16f, 18f));
        y -= 26f;
        FitHeight(window, y);

        UnityAction invite = () => {
            InviteName = field.text.Trim();
            if (Invite(InviteName)) {
                CloseDialog();
            }
        };
        field.onSubmit.AddListener(_ => invite());
        var buttons = RoWidgets.ButtonRow(window);
        RoWidgets.Button(buttons, "確認", invite);
        RoWidgets.Button(buttons, "取消", CloseDialog);
        field.ActivateInputField();
    }

    private void OpenAddFriendDialog() {
        var window = OpenSideWindow("新增好友");
        var y = -22f;
        RoWidgets.Text(window, "要加為好友的角色名稱", 12f, RoWidgets.TextColor, new Vector2(8f, y), new Vector2(SIDE_WIDTH - 16f, 16f));
        y -= 18f;
        var field = RoWidgets.InputField(window, NewFriendName, 23, new Vector2(8f, y), new Vector2(SIDE_WIDTH - 16f, 18f));
        y -= 26f;
        FitHeight(window, y);

        UnityAction add = () => {
            NewFriendName = field.text.Trim();
            if (AddFriend(NewFriendName)) {
                NewFriendName = "";
                CloseDialog();
            }
        };
        field.onSubmit.AddListener(_ => add());
        var buttons = RoWidgets.ButtonRow(window);
        RoWidgets.Button(buttons, "確認", add);
        RoWidgets.Button(buttons, "取消", CloseDialog);
        field.ActivateInputField();
    }

    /// <summary>
    /// The leader's three ways of sharing (the others see them greyed) and whether invites are
    /// taken; nothing is sent until 確認
    /// </summary>
    private void OpenSettingsDialog() {
        var window = OpenSideWindow("隊伍設定");
        var y = -22f;
        Toggle exp = null;
        Toggle pickup = null;
        Toggle loot = null;
        if (InParty) {
            var leader = IAmLeader;
            exp = Choice(window, "經驗值分配方式", "各自取得", ExpOption == 2 ? "均等分配 (等級差距過大)" : "均等分配", ExpOption != 0, leader, ref y);
            pickup = Choice(window, "道具蒐集方式", "各自取得", "隊伍隊員全體共有", SharePickup, leader, ref y);
            loot = Choice(window, "物品分配方式", "各自取得", "均等分配", ShareLoot, leader, ref y);
        }
        var refuse = Choice(window, "隊伍邀請", "接受", "拒絕", RefuseInvites, true, ref y);
        FitHeight(window, y);

        var buttons = RoWidgets.ButtonRow(window);
        RoWidgets.Button(buttons, "確認", () => {
            if (exp != null && IAmLeader && (exp.isOn != (ExpOption != 0) || pickup.isOn != SharePickup || loot.isOn != ShareLoot)) {
                SendSettings(exp.isOn, pickup.isOn, loot.isOn);
            }
            if (refuse.isOn != RefuseInvites) {
                new CZ.PARTY_CONFIG(refuse.isOn).Send();
            }
            CloseDialog();
        });
        RoWidgets.Button(buttons, "取消", CloseDialog);
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
        listRect.offsetMin = new Vector2(1f, TOOLBAR_HEIGHT + TAB_HEIGHT);
        listRect.offsetMax = new Vector2(-1f, -TITLE_HEIGHT);

        // No party: a line saying so
        var emptyParty = AaWidgets.NewRect("No Party", Root);
        AaWidgets.Stretch(emptyParty);
        emptyParty.offsetMin = listRect.offsetMin;
        emptyParty.offsetMax = listRect.offsetMax;
        EmptyParty = emptyParty.gameObject;
        var hint = AaWidgets.Text(emptyParty, "目前沒有加入隊伍", 12f, OfflineColor, TextAlignmentOptions.Center);
        hint.rectTransform.sizeDelta = new Vector2(260f, 20f);

        // No friends: the official window's porings, the angels at the top right and three at the bottom
        var emptyFriends = AaWidgets.NewRect("No Friends", Root);
        AaWidgets.Stretch(emptyFriends);
        emptyFriends.offsetMin = listRect.offsetMin;
        emptyFriends.offsetMax = listRect.offsetMax;
        EmptyFriends = emptyFriends.gameObject;
        var angels = RoWidgets.Picture(emptyFriends, "Angels", RoWidgets.Texture("renewalparty/img_friend1.bmp"), new Vector2(-6f, -8f), new Vector2(112f, 130f));
        angels.rectTransform.anchorMin = angels.rectTransform.anchorMax = angels.rectTransform.pivot = Vector2.one;
        var porings = RoWidgets.Picture(emptyFriends, "Porings", RoWidgets.Texture("renewalparty/img_friend2.bmp"), new Vector2(-4f, 4f), new Vector2(172f, 74f));
        porings.rectTransform.anchorMin = porings.rectTransform.anchorMax = porings.rectTransform.pivot = new Vector2(1f, 0f);

        BuildTabBar();

        Toolbar = AaWidgets.NewImage("Toolbar", Root, ToolbarColor).rectTransform;
        Toolbar.anchorMin = Vector2.zero;
        Toolbar.anchorMax = new Vector2(1f, 0f);
        Toolbar.pivot = new Vector2(0.5f, 0f);
        Toolbar.anchoredPosition = new Vector2(0f, TAB_HEIGHT);
        Toolbar.sizeDelta = new Vector2(0f, TOOLBAR_HEIGHT);
        var line = AaWidgets.NewImage("Line", Toolbar, RoWidgets.LineColor);
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

        var party = CurrentTab == Tab.Party;
        Title.text = party ? (InParty ? $"隊伍({PartyName})" : "隊伍") : $"朋友({Friends.Count}/{MAX_FRIENDS})";
        EmptyParty.SetActive(party && !InParty);
        EmptyFriends.SetActive(!party && Friends.Count == 0);
        ContentScroll.gameObject.SetActive(party ? InParty : Friends.Count > 0);
        var scroll = ContentScroll.verticalNormalizedPosition;
        AaWidgets.Clear(Content);
        if (party && InParty) {
            foreach (var member in Members.OrderByDescending(m => m.IsLeader).ThenByDescending(m => m.IsOnline)) {
                MemberRow(member);
            }
        } else if (!party) {
            foreach (var friend in Friends.OrderByDescending(f => f.IsOnline)) {
                FriendRow(friend);
            }
        }
        BuildToolbar();
        UpdateTabBar();
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
        if (CurrentTab == Tab.Friends) {
            RoWidgets.Button(Toolbar, "新增", OpenAddFriendDialog);
        } else if (InParty) {
            RoWidgets.Button(Toolbar, "邀請", OpenInviteDialog);
            RoWidgets.Button(Toolbar, "設定", OpenSettingsDialog);
            RoWidgets.Button(Toolbar, "離開", ConfirmLeave);
        } else {
            RoWidgets.Button(Toolbar, "建立隊伍", OpenCreateDialog);
            RoWidgets.Button(Toolbar, "設定", OpenSettingsDialog);
        }
    }

    /// <summary>
    /// ○朋友 ◉隊伍 at the bottom, with the party's head count on its tab
    /// </summary>
    private void BuildTabBar() {
        var bar = AaWidgets.NewImage("Tabs", Root, ToolbarColor).rectTransform;
        bar.anchorMin = Vector2.zero;
        bar.anchorMax = new Vector2(1f, 0f);
        bar.pivot = new Vector2(0.5f, 0f);
        bar.anchoredPosition = Vector2.zero;
        bar.sizeDelta = new Vector2(0f, TAB_HEIGHT);
        var line = AaWidgets.NewImage("Line", bar, RoWidgets.LineColor);
        line.rectTransform.anchorMin = new Vector2(0f, 1f);
        line.rectTransform.anchorMax = Vector2.one;
        line.rectTransform.pivot = new Vector2(0.5f, 1f);
        line.rectTransform.sizeDelta = new Vector2(0f, 1f);

        var group = bar.gameObject.AddComponent<ToggleGroup>();
        group.allowSwitchOff = false;
        FriendsTab = RoWidgets.Radio(bar, "朋友", CurrentTab == Tab.Friends, group, new Vector2(4f, -2f), 54f);
        PartyTab = RoWidgets.Radio(bar, "隊伍", CurrentTab == Tab.Party, group, new Vector2(62f, -2f), 54f);
        FriendsTab.onValueChanged.AddListener(on => {
            if (on) {
                SwitchTab(Tab.Friends);
            }
        });
        PartyTab.onValueChanged.AddListener(on => {
            if (on) {
                SwitchTab(Tab.Party);
            }
        });

        MemberCount = RoWidgets.Text(bar, "", 12f, RoWidgets.TextColor, new Vector2(-8f, -2f), new Vector2(100f, 16f), TextAlignmentOptions.TopRight);
        MemberCount.rectTransform.anchorMin = MemberCount.rectTransform.anchorMax = MemberCount.rectTransform.pivot = Vector2.one;
    }

    private void UpdateTabBar() {
        FriendsTab.SetIsOnWithoutNotify(CurrentTab == Tab.Friends);
        PartyTab.SetIsOnWithoutNotify(CurrentTab == Tab.Party);
        MemberCount.text = CurrentTab == Tab.Party && InParty ? $"隊員 {Members.Count}/{MAX_MEMBERS}" : "";
    }

    /// <summary>
    /// A friend's name and whether they're online (ON, OFF); the list has neither job nor level
    /// </summary>
    private void FriendRow(Friend friend) {
        var row = AaWidgets.NewImage("Friend", Content, Color.white);
        AaWidgets.Layout(row, height: FRIEND_ROW_HEIGHT);
        var button = row.gameObject.AddComponent<Button>();
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = colors.pressedColor = colors.selectedColor = PointedRowColor;
        button.colors = colors;
        button.targetGraphic = row;
        button.onClick.AddListener(() => ShowFriendMenu(friend));

        RoWidgets.Text(row.transform, $"<noparse>{friend.Name}</noparse>", 12f, friend.IsOnline ? OnlineColor : OfflineColor, new Vector2(10f, -5f), new Vector2(WIDTH - 60f, 16f));
        var state = friend.IsOnline ? "icon_party_on" : "icon_party_off";
        var badge = RoWidgets.Picture(row.transform, "State", RoWidgets.Texture($"renewalparty/{state}.bmp"), Vector2.zero, new Vector2(26f, 11f));
        badge.rectTransform.anchorMin = badge.rectTransform.anchorMax = badge.rectTransform.pivot = Vector2.one;
        badge.rectTransform.anchoredPosition = new Vector2(-8f, -7f);
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
