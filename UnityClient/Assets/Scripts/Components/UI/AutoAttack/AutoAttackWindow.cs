using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// The auto attack (內掛) window: what the official client's Gshield.dll overlay does
/// (tools/hwid_dll/src/aa_overlay.cpp), with the same tabs and the same server packets. Settings
/// are edited here and saved with 套用; the server keeps them per character and runs the bot.
/// </summary>
public class AutoAttackWindow : MonoBehaviour {

    private enum Tab { Home, Potion, BuffSkill, BuffItem, AttackSkill, Teleport, Mob, Pickup, AutoBuy, Storage, Other }

    private static readonly string[] TabLabels = {
        "主頁", "藥水", "輔助技能", "輔助道具", "攻擊技能", "瞬移", "怪物", "拾取", "自動補藥", "自動存倉", "其他"
    };

    private const float WIDTH = 660f;
    private const float HEIGHT = 450f;
    private const float TITLE_HEIGHT = 40f;
    private const float TAB_HEIGHT = 42f;
    private const float FOOTER_HEIGHT = 48f;
    private const float TAB_WIDTH = 84f;
    // The server takes a toggle, a request or an update once a second (feature.autoattack_button_cooldown)
    private const float SERVER_COOLDOWN = 1.1f;

    // What the heal slots take: AL_HEAL, AB_CHEAL, AB_HIGHNESSHEAL
    private static readonly HashSet<int> HealSkillIds = new HashSet<int> { 28, 2043, 2051 };
    // SA_AUTOSPELL asks which spell to cast
    private const int SA_AUTOSPELL = 279;
    private static readonly HashSet<int> AutoSpellIds = new HashSet<int> { 11, 13, 14, 15, 17, 19, 20 };
    // skill_db marks these Self, yet they're attacks: combo skills and area attacks around oneself
    private static readonly HashSet<int> ComboAttackIds = new HashSet<int> { 272, 273, 371, 372, 413, 415, 417, 419, 421 };
    private static readonly HashSet<int> SelfAreaAttackIds = new HashSet<int> { 254, 88, 516, 539 };

    private static readonly (int Type, string Label)[] PickupTypes = {
        (0, "治療類"), (2, "消耗品"), (3, "其他 / 素材"), (4, "防具"), (5, "武器"), (6, "卡片"), (7, "寵物蛋"),
        (8, "寵物裝備"), (10, "箭矢 / 彈藥"), (11, "延遲消耗品"), (12, "影子裝備"), (18, "商城道具"), (19, "護身符")
    };

    private static readonly string[] CureModes = {
        "不使用", "綠色藥水 (中毒/沉默/黑暗)", "萬能藥水 (上述 + 詛咒/混亂/幻覺)", "蜂膠 (同萬能, 並回 HP/SP)", "聖水 (僅解詛咒)"
    };

    public static AutoAttackWindow Instance { get; private set; }

    /// <summary>
    /// Each status the server pushes (every second)
    /// </summary>
    public static event Action<Pandas.AA_STATUS> StatusChanged;

    public Pandas.AA_STATUS Status { get; private set; }
    public bool IsRunning => Status != null && Status.State != Pandas.AA_STATUS.STATE_OFF;

    private MapUiController UI;
    private RectTransform Root;
    private RectTransform Content;
    private ScrollRect ContentScroll;
    private readonly List<Image> TabButtons = new List<Image>();
    private TextMeshProUGUI TitleStatus;
    private TextMeshProUGUI FooterText;
    private Button ApplyButton;

    private Pandas.AA_SET_SNAPSHOT Snapshot;
    // What's being edited, and the server's copy to tell whether it changed
    private AutoAttackSettings Edit;
    private byte[] SyncedBlock;
    private bool WaitingForApply;
    private Tab CurrentTab = Tab.Home;
    private float NextToggle;
    private float NextRequest;
    private float NextApply;

    // The home tab's texts, updated by each status
    private readonly Dictionary<string, TextMeshProUGUI> HomeValues = new Dictionary<string, TextMeshProUGUI>();
    private TextMeshProUGUI HomeState;
    private Button AttackButton;
    private Button SupportButton;

    public static AutoAttackWindow Create(MapUiController ui) {
        var root = AaWidgets.NewImage("Auto Attack Window", ui.transform, AaWidgets.PanelColor);
        var window = root.gameObject.AddComponent<AutoAttackWindow>();
        window.UI = ui;
        window.Root = root.rectTransform;
        window.Build();
        root.gameObject.SetActive(false);
        return window;
    }

    private void Awake() {
        Instance = this;
        var network = FindObjectOfType<NetworkClient>();
        network.HookPacket(Pandas.AA_STATUS.HEADER, OnStatus);
        network.HookPacket(Pandas.AA_SET_SNAPSHOT.HEADER, OnSnapshot);
    }

    private void OnDestroy() {
        if (Instance == this) {
            Instance = null;
        }
    }

    public void Show() {
        var canvas = (UI.transform as RectTransform).rect.size;
        Root.anchorMin = Root.anchorMax = Root.pivot = new Vector2(0.5f, 0.5f);
        Root.sizeDelta = new Vector2(Mathf.Min(WIDTH, canvas.x - 16f), Mathf.Min(HEIGHT, canvas.y - 16f));
        Root.anchoredPosition = Vector2.zero;

        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        RequestSnapshot();
        ShowTab(CurrentTab);
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

    #region Server

    private void OnStatus(ushort cmd, int size, InPacket packet) {
        if (!(packet is Pandas.AA_STATUS status) || status.Magic != Pandas.AA_STATUS.MAGIC) {
            return;
        }
        Status = status;
        StatusChanged?.Invoke(status);
        if (gameObject.activeInHierarchy) {
            RefreshStatus();
        }
    }

    private void OnSnapshot(ushort cmd, int size, InPacket packet) {
        if (!(packet is Pandas.AA_SET_SNAPSHOT snapshot) || snapshot.Magic != Pandas.AA_SET_SNAPSHOT.MAGIC) {
            return;
        }

        // Changes not applied yet stay; after 套用 the server's version wins (it drops what it rejects)
        var keepEdits = Edit != null && IsDirty && !WaitingForApply;
        Snapshot = snapshot;
        SyncedBlock = Normalized(snapshot.Settings).ToBytes();
        if (!keepEdits) {
            Edit = snapshot.Settings.Clone();
        }
        WaitingForApply = false;

        if (gameObject.activeInHierarchy) {
            ShowTab(CurrentTab, keepScroll: true);
        }
    }

    private void RequestSnapshot() {
        if (Time.unscaledTime < NextRequest) {
            return;
        }
        NextRequest = Time.unscaledTime + SERVER_COOLDOWN;
        new Pandas.AA_SET_REQUEST().Send();
    }

    private void Apply() {
        if (Edit == null || Time.unscaledTime < NextApply) {
            return;
        }
        NextApply = Time.unscaledTime + SERVER_COOLDOWN;
        WaitingForApply = true;
        new Pandas.AA_SET_UPDATE(Normalized(Edit)).Send();
        UpdateFooter();

        // The server answers at most once a second: ask again if its answer was held back
        CancelInvoke(nameof(RequestIfStillWaiting));
        Invoke(nameof(RequestIfStillWaiting), SERVER_COOLDOWN + 0.5f);
    }

    private void RequestIfStillWaiting() {
        if (WaitingForApply) {
            NextRequest = 0f;
            RequestSnapshot();
        }
    }

    private void Toggle(bool support) {
        if (Time.unscaledTime < NextToggle) {
            return;
        }
        NextToggle = Time.unscaledTime + SERVER_COOLDOWN;
        new Pandas.AA_TOGGLE(support).Send();
    }

    /// <summary>
    /// A slot is on when something is picked for it, as the official client decides (and as the
    /// server reports it back)
    /// </summary>
    private static AutoAttackSettings Normalized(AutoAttackSettings settings) {
        var s = settings.Clone();
        s.HpPotionEnabled = s.HpPotionItemId != 0;
        s.HpPotionEnabled2 = s.HpPotionItemId2 != 0;
        s.SpPotionEnabled = s.SpPotionItemId != 0;
        s.SpPotionEnabled2 = s.SpPotionItemId2 != 0;
        foreach (var slot in s.BuffSkills.Concat(s.AttackSkills)) {
            slot.Enabled = slot.SkillId != 0;
            if (slot.SkillId == 0) {
                slot.Level = 0;
                slot.SubSkillId = 0;
            }
        }
        foreach (var slot in s.BuffItems) {
            slot.Enabled = slot.ItemId != 0;
        }
        foreach (var slot in s.HealSkills) {
            slot.Enabled = slot.SkillId != 0;
        }
        return s;
    }

    private bool IsDirty => Edit != null && SyncedBlock != null && !Normalized(Edit).ToBytes().SequenceEqual(SyncedBlock);

    #endregion

    #region Building

    private void Build() {
        var title = AaWidgets.NewImage("Title", Root, AaWidgets.BarColor);
        Dock(title.rectTransform, top: true, TITLE_HEIGHT, 0f);
        var titleText = AaWidgets.Text(title.transform, "內掛系統", 19f, AaWidgets.AccentColor);
        AaWidgets.Stretch(titleText.rectTransform);
        titleText.rectTransform.offsetMin = new Vector2(14f, 0f);
        titleText.fontStyle = FontStyles.Bold;
        TitleStatus = AaWidgets.Text(title.transform, "", AaWidgets.SMALL_FONT_SIZE + 1f, AaWidgets.DimTextColor, TextAlignmentOptions.MidlineRight);
        AaWidgets.Stretch(TitleStatus.rectTransform);
        TitleStatus.rectTransform.offsetMax = new Vector2(-60f, 0f);

        var close = AaWidgets.Button(title.transform, "X", Hide, 44f);
        Place(close.transform as RectTransform, new Vector2(1f, 0.5f), new Vector2(-6f, 0f), new Vector2(44f, 32f));

        // Dragging the title moves the window
        var drag = title.gameObject.AddComponent<EventTrigger>();
        var dragEntry = new EventTrigger.Entry { eventID = EventTriggerType.Drag };
        dragEntry.callback.AddListener(data => {
            var scale = GetComponentInParent<Canvas>().scaleFactor;
            Root.anchoredPosition += ((PointerEventData) data).delta / scale;
        });
        drag.triggers.Add(dragEntry);

        var tabs = AaWidgets.ScrollList(Root, false, out var tabScroll, 4f, new RectOffset(6, 6, 5, 5));
        Dock(tabScroll.transform as RectTransform, top: true, TAB_HEIGHT, TITLE_HEIGHT);
        for (var i = 0; i < TabLabels.Length; i++) {
            var tab = (Tab) i;
            var button = AaWidgets.Button(tabs, TabLabels[i], () => ShowTab(tab), TAB_WIDTH);
            TabButtons.Add(button.targetGraphic as Image);
        }

        Content = AaWidgets.ScrollList(Root, true, out ContentScroll, 6f, new RectOffset(16, 16, 10, 14));
        var contentRect = ContentScroll.transform as RectTransform;
        AaWidgets.Stretch(contentRect);
        contentRect.offsetMax = new Vector2(0f, -(TITLE_HEIGHT + TAB_HEIGHT));
        contentRect.offsetMin = new Vector2(0f, FOOTER_HEIGHT);

        var footer = AaWidgets.NewImage("Footer", Root, AaWidgets.BarColor);
        Dock(footer.rectTransform, top: false, FOOTER_HEIGHT, 0f);
        FooterText = AaWidgets.Text(footer.transform, "", AaWidgets.SMALL_FONT_SIZE + 1f, AaWidgets.DimTextColor);
        FooterText.enableWordWrapping = true;
        AaWidgets.Stretch(FooterText.rectTransform);
        FooterText.rectTransform.offsetMin = new Vector2(14f, 0f);
        FooterText.rectTransform.offsetMax = new Vector2(-250f, 0f);

        ApplyButton = AaWidgets.Button(footer.transform, "套用", Apply, 110f, AaWidgets.SelectedColor);
        Place(ApplyButton.transform as RectTransform, new Vector2(1f, 0.5f), new Vector2(-10f, 0f), new Vector2(110f, 36f));
        var refresh = AaWidgets.Button(footer.transform, "重新讀取", () => {
            NextRequest = 0f;
            RequestSnapshot();
        }, 110f);
        Place(refresh.transform as RectTransform, new Vector2(1f, 0.5f), new Vector2(-128f, 0f), new Vector2(110f, 36f));
    }

    private static void Dock(RectTransform rect, bool top, float height, float offset) {
        rect.anchorMin = new Vector2(0f, top ? 1f : 0f);
        rect.anchorMax = new Vector2(1f, top ? 1f : 0f);
        rect.pivot = new Vector2(0.5f, top ? 1f : 0f);
        rect.sizeDelta = new Vector2(0f, height);
        rect.anchoredPosition = new Vector2(0f, top ? -offset : offset);
    }

    private static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size) {
        rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private void ShowTab(Tab tab, bool keepScroll = false) {
        var scroll = ContentScroll.verticalNormalizedPosition;
        CurrentTab = tab;
        for (var i = 0; i < TabButtons.Count; i++) {
            TabButtons[i].color = i == (int) tab ? AaWidgets.SelectedColor : AaWidgets.ControlColor;
        }

        AaWidgets.Clear(Content);
        HomeValues.Clear();
        HomeState = null;
        AttackButton = SupportButton = null;

        if (tab == Tab.Home) {
            BuildHome();
        } else if (Edit == null || Snapshot == null) {
            AaWidgets.Label(Content, "讀取設定中… 若一直停在這裡，請按「重新讀取」。", AaWidgets.FONT_SIZE, AaWidgets.DimTextColor);
        } else {
            switch (tab) {
                case Tab.Potion: BuildPotion(); break;
                case Tab.BuffSkill: BuildBuffSkills(); break;
                case Tab.BuffItem: BuildBuffItems(); break;
                case Tab.AttackSkill: BuildAttackSkills(); break;
                case Tab.Teleport: BuildTeleport(); break;
                case Tab.Mob: BuildMobs(); break;
                case Tab.Pickup: BuildPickup(); break;
                case Tab.AutoBuy: BuildAutoBuy(); break;
                case Tab.Storage: BuildStorage(); break;
                case Tab.Other: BuildOther(); break;
            }
        }

        RefreshStatus();
        UpdateFooter();

        Canvas.ForceUpdateCanvases();
        ContentScroll.verticalNormalizedPosition = keepScroll ? scroll : 1f;
    }

    /// <summary>
    /// After an edit: redraw the tab when what's shown depends on it
    /// </summary>
    private void Changed(bool redraw = false) {
        if (redraw) {
            ShowTab(CurrentTab, keepScroll: true);
        } else {
            UpdateFooter();
        }
    }

    private void UpdateFooter() {
        var dirty = IsDirty;
        if (Edit == null) {
            FooterText.text = "讀取設定中…";
            FooterText.color = AaWidgets.DimTextColor;
        } else if (WaitingForApply) {
            FooterText.text = "已送出，等待伺服器確認…";
            FooterText.color = AaWidgets.AccentColor;
        } else if (dirty) {
            FooterText.text = "有尚未套用的變更，按「套用」儲存到伺服器。";
            FooterText.color = AaWidgets.WarnColor;
        } else {
            FooterText.text = "設定已與伺服器同步。";
            FooterText.color = AaWidgets.DimTextColor;
        }
        ApplyButton.interactable = dirty && !WaitingForApply;
    }

    #endregion

    #region Names

    private string SkillName(int skillId) {
        if (SkillTable.Skills.TryGetValue((short) skillId, out var skill) && !string.IsNullOrWhiteSpace(skill.SkillName)) {
            return skill.SkillName.Trim();
        }
        var learned = Snapshot?.LearnedSkills.FirstOrDefault(s => s.SkillId == skillId);
        return learned != null && !string.IsNullOrEmpty(learned.Name) ? learned.Name : $"技能 #{skillId}";
    }

    private int LearnedLevel(int skillId) {
        var learned = Snapshot?.LearnedSkills.FirstOrDefault(s => s.SkillId == skillId);
        return learned?.Level ?? 0;
    }

    private string ItemName(uint itemId) {
        if (DBManager.ItemDB.TryGetValue((int) itemId, out var item) && !string.IsNullOrWhiteSpace(item.identifiedDisplayName)) {
            return item.identifiedDisplayName;
        }
        var inBag = Snapshot?.InventoryItems.FirstOrDefault(i => i.ItemId == itemId);
        if (inBag != null && !string.IsNullOrEmpty(inBag.Name)) {
            return inBag.Name;
        }
        var buyable = Snapshot?.BuyList.FirstOrDefault(i => i.Id == itemId);
        return buyable != null && !string.IsNullOrEmpty(buyable.Name) ? buyable.Name : $"道具 #{itemId}";
    }

    private string MobName(uint mobId) {
        var mob = Snapshot?.MapMobs.Concat(Snapshot.SavedMobs).FirstOrDefault(m => m.Id == mobId);
        return mob != null && !string.IsNullOrEmpty(mob.Name) ? $"{mob.Name} ({mobId})" : $"魔物 #{mobId}";
    }

    #endregion

    #region Pickers

    private void PickSkill(string title, int current, Func<AutoAttackLearnedSkill, bool> filter, Action<int> onPick) {
        var options = Snapshot.LearnedSkills
            .Where(filter)
            .Select(s => new KeyValuePair<string, int>($"{SkillName(s.SkillId)}  (Lv {s.Level})", s.SkillId))
            .ToList();
        AaWidgets.Pick(Root, title, options, current, true, onPick);
    }

    private void PickInventoryItem(string title, uint current, Func<AutoAttackInventoryItem, bool> filter, Action<uint> onPick) {
        var options = Snapshot.InventoryItems
            .Where(filter)
            .Select(i => new KeyValuePair<string, int>($"{ItemName(i.ItemId)}  x{i.Amount}", (int) i.ItemId))
            .ToList();
        AaWidgets.Pick(Root, title, options, (int) current, true, id => onPick((uint) id));
    }

    private static bool IsBuffSkill(AutoAttackLearnedSkill s) {
        if (ComboAttackIds.Contains(s.SkillId) || SelfAreaAttackIds.Contains(s.SkillId)) {
            return false;
        }
        return (s.Inf & (AutoAttackLearnedSkill.INF_SUPPORT | AutoAttackLearnedSkill.INF_SELF)) != 0;
    }

    private static bool IsAttackSkill(AutoAttackLearnedSkill s) {
        // Heal hurts the undead; the server only casts it on them
        if (HealSkillIds.Contains(s.SkillId) || ComboAttackIds.Contains(s.SkillId) || SelfAreaAttackIds.Contains(s.SkillId)) {
            return true;
        }
        return (s.Inf & (AutoAttackLearnedSkill.INF_ATTACK | AutoAttackLearnedSkill.INF_GROUND)) != 0
            && (s.Inf & AutoAttackLearnedSkill.DEALS_DAMAGE) != 0;
    }

    #endregion

    #region Tabs

    private void BuildHome() {
        HomeState = AaWidgets.Label(Content, "", AaWidgets.FONT_SIZE + 3f);
        HomeState.fontStyle = FontStyles.Bold;

        var buttons = AaWidgets.Row(Content, 46f);
        AttackButton = AaWidgets.Button(buttons, "啟動自動練功", () => Toggle(false), -1f, new Color(0.2f, 0.45f, 0.25f, 1f));
        SupportButton = AaWidgets.Button(buttons, "啟動自動輔助", () => Toggle(true), -1f, new Color(0.2f, 0.35f, 0.55f, 1f));
        AaWidgets.Layout(AttackButton, height: 42f);
        AaWidgets.Layout(SupportButton, height: 42f);

        AaWidgets.Label(Content, "自動練功會自己找怪打；自動輔助只幫自己補血、上狀態，可以邊操作邊用。", AaWidgets.SMALL_FONT_SIZE, AaWidgets.DimTextColor);

        foreach (var name in new[] { "職業", "累計時間", "已擊殺", "EXP / 小時", "Zeny 消耗", "背包", "地圖", "剩餘時間" }) {
            var row = AaWidgets.Row(Content, 28f);
            var label = AaWidgets.Text(row, name, AaWidgets.FONT_SIZE, AaWidgets.DimTextColor);
            AaWidgets.Layout(label, 130f);
            var value = AaWidgets.Text(row, "--");
            AaWidgets.Layout(value, flexibleWidth: 1f);
            HomeValues[name] = value;
        }

        AaWidgets.Label(Content, "在各分頁改好設定後，按下方「套用」才會存到伺服器。", AaWidgets.SMALL_FONT_SIZE, AaWidgets.DimTextColor);
    }

    private void RefreshStatus() {
        var status = Status;
        if (TitleStatus != null) {
            TitleStatus.text = status == null ? ""
                : status.State == Pandas.AA_STATUS.STATE_AUTO_ATTACK ? $"自動練功中  {FormatDuration(status.ElapsedSeconds)}"
                : status.State == Pandas.AA_STATUS.STATE_AUTO_SUPPORT ? $"自動輔助中  {FormatDuration(status.ElapsedSeconds)}"
                : "停止";
            TitleStatus.color = IsRunning ? AaWidgets.GoodColor : AaWidgets.DimTextColor;
        }

        if (HomeState == null) {
            return;
        }

        if (status == null) {
            HomeState.text = "內掛狀態：等待伺服器資料…";
            HomeState.color = AaWidgets.DimTextColor;
        } else if (status.State == Pandas.AA_STATUS.STATE_AUTO_ATTACK) {
            HomeState.text = "內掛狀態：啟動中 (自動練功)";
            HomeState.color = AaWidgets.GoodColor;
        } else if (status.State == Pandas.AA_STATUS.STATE_AUTO_SUPPORT) {
            HomeState.text = "內掛狀態：啟動中 (自動輔助)";
            HomeState.color = AaWidgets.AccentColor;
        } else {
            HomeState.text = "內掛狀態：停止";
            HomeState.color = AaWidgets.DimTextColor;
        }

        var state = status?.State ?? Pandas.AA_STATUS.STATE_OFF;
        AaWidgets.SetButtonText(AttackButton, state == Pandas.AA_STATUS.STATE_AUTO_ATTACK ? "停止自動練功" : "啟動自動練功");
        AaWidgets.SetButtonText(SupportButton, state == Pandas.AA_STATUS.STATE_AUTO_SUPPORT ? "停止自動輔助" : "啟動自動輔助");

        if (status == null) {
            return;
        }
        var self = Session.CurrentSession?.Entity as Entity;
        var baseStatus = self != null ? self.GetBaseStatus() : null;
        HomeValues["職業"].text = baseStatus != null ? $"{JobHelper.GetJobName(baseStatus.jobId, baseStatus.sex)}  Lv {status.BaseLevel}" : status.JobName;
        HomeValues["累計時間"].text = FormatDuration(status.ElapsedSeconds);
        HomeValues["已擊殺"].text = $"{status.KillCount:N0}";
        HomeValues["EXP / 小時"].text = $"{status.ExpPerHour:N0}";
        HomeValues["Zeny 消耗"].text = $"{status.ZenySpent:N0}";
        HomeValues["背包"].text = $"{status.InventoryUsed} / {status.InventoryMax}";
        HomeValues["地圖"].text = status.Map;
        var left = HomeValues["剩餘時間"];
        if (status.Unlimited) {
            left.text = "無限制";
            left.color = AaWidgets.GoodColor;
        } else if (status.DurationMsLeft == 0) {
            left.text = "已耗盡";
            left.color = AaWidgets.WarnColor;
        } else {
            left.text = FormatDuration(status.DurationMsLeft / 1000);
            left.color = AaWidgets.TextColor;
        }
    }

    private static string FormatDuration(uint seconds) {
        var days = seconds / 86400;
        var hours = seconds / 3600 % 24;
        var minutes = seconds / 60 % 60;
        var secs = seconds % 60;
        if (days > 0) {
            return $"{days}天{hours}時{minutes}分{secs}秒";
        }
        if (hours > 0) {
            return $"{hours}時{minutes}分{secs}秒";
        }
        return minutes > 0 ? $"{minutes}分{secs}秒" : $"{secs}秒";
    }

    private void BuildPotion() {
        AaWidgets.Header(Content, "HP 自動回血 (兩格各自觸發)");
        PotionSlot("第 1 格", "HP", () => Edit.HpPotionItemId, v => Edit.HpPotionItemId = v, () => Edit.HpPotionThreshold, v => Edit.HpPotionThreshold = v);
        PotionSlot("第 2 格", "HP", () => Edit.HpPotionItemId2, v => Edit.HpPotionItemId2 = v, () => Edit.HpPotionThreshold2, v => Edit.HpPotionThreshold2 = v);
        AaWidgets.Header(Content, "SP 自動回魔 (兩格各自觸發)");
        PotionSlot("第 1 格", "SP", () => Edit.SpPotionItemId, v => Edit.SpPotionItemId = v, () => Edit.SpPotionThreshold, v => Edit.SpPotionThreshold = v);
        PotionSlot("第 2 格", "SP", () => Edit.SpPotionItemId2, v => Edit.SpPotionItemId2 = v, () => Edit.SpPotionThreshold2, v => Edit.SpPotionThreshold2 = v);
        AaWidgets.Label(Content, "選擇藥水即啟用，選「未選擇」即停用。", AaWidgets.SMALL_FONT_SIZE, AaWidgets.DimTextColor);

        AaWidgets.Header(Content, "坐下回血回魔");
        AaWidgets.Toggle(Content, "啟用自動坐下回血 / 回魔", Edit.SitEnabled, v => {
            Edit.SitEnabled = v;
            if (!v) {
                Edit.SitReactAttack = Edit.SitReactTeleport = Edit.SitUseTensionRelax = false;
            }
            Changed(redraw: true);
        });
        var sit = Edit.SitEnabled;
        AaWidgets.Label(Content, "HP/SP 任一低於門檻就坐下；兩個都回到上限才站起。0 = 該項不看。", AaWidgets.SMALL_FONT_SIZE, AaWidgets.DimTextColor);
        AaWidgets.Slider(Content, "HP% 低於此值坐下", 0, 99, Edit.SitMinHp, Percent, v => { Edit.SitMinHp = (byte) v; Changed(); }, sit);
        AaWidgets.Slider(Content, "HP% 高於此值站起", 0, 100, Edit.SitMaxHp, Percent, v => { Edit.SitMaxHp = (byte) v; Changed(); }, sit);
        AaWidgets.Slider(Content, "SP% 低於此值坐下", 0, 99, Edit.SitMinSp, Percent, v => { Edit.SitMinSp = (byte) v; Changed(); }, sit);
        AaWidgets.Slider(Content, "SP% 高於此值站起", 0, 100, Edit.SitMaxSp, Percent, v => { Edit.SitMaxSp = (byte) v; Changed(); }, sit);
        AaWidgets.Label(Content, "被攻擊時 (擇一，都不勾 = 繼續坐著)", AaWidgets.FONT_SIZE, sit ? AaWidgets.TextColor : AaWidgets.DimTextColor);
        AaWidgets.Toggle(Content, "自動反擊攻擊者", Edit.SitReactAttack, v => {
            Edit.SitReactAttack = v;
            if (v) {
                Edit.SitReactTeleport = false;
            }
            Changed(redraw: true);
        }, sit);
        AaWidgets.Toggle(Content, "瞬間移動 (需在「瞬移」勾選蒼蠅翅膀或瞬移技能)", Edit.SitReactTeleport, v => {
            Edit.SitReactTeleport = v;
            if (v) {
                Edit.SitReactAttack = false;
            }
            Changed(redraw: true);
        }, sit);
        AaWidgets.Toggle(Content, "用極速回復 (LK_TENSIONRELAX) 代替坐下", Edit.SitUseTensionRelax, v => { Edit.SitUseTensionRelax = v; Changed(); }, sit);
    }

    private void PotionSlot(string label, string stat, Func<uint> item, Action<uint> setItem, Func<byte> threshold, Action<byte> setThreshold) {
        var row = AaWidgets.Row(Content);
        AaWidgets.Layout(AaWidgets.Text(row, label, AaWidgets.FONT_SIZE, AaWidgets.DimTextColor), 70f);
        AaWidgets.Select(row, item() == 0 ? "-- 未選擇 --" : ItemName(item()), () =>
            PickInventoryItem($"{stat} {label}：選擇藥水", item(), i => i.Type == 0, id => {
                setItem(id);
                Changed(redraw: true);
            }));
        AaWidgets.Slider(Content, $"{stat}% 低於此值使用", 1, 99, Math.Max((int) threshold(), 1), Percent, v => { setThreshold((byte) v); Changed(); });
    }

    private void BuildBuffSkills() {
        AaWidgets.Header(Content, "治療技能 (HP% 低於門檻時對自己施放)");
        foreach (var slot in Edit.HealSkills) {
            var row = AaWidgets.Row(Content);
            AaWidgets.Select(row, slot.SkillId == 0 ? "-- 未選擇 --" : SkillName(slot.SkillId), () =>
                PickSkill("選擇治療技能", slot.SkillId, s => HealSkillIds.Contains(s.SkillId), id => {
                    slot.SkillId = (ushort) id;
                    slot.Level = (byte) LearnedLevel(id);
                    if (slot.MinHpPercent == 0) {
                        slot.MinHpPercent = 50;
                    }
                    Changed(redraw: true);
                }));
            if (slot.SkillId != 0) {
                AaWidgets.Stepper(row, 1, Math.Max(1, LearnedLevel(slot.SkillId)), Math.Max((int) slot.Level, 1), Level, v => { slot.Level = (byte) v; Changed(); });
                AaWidgets.Slider(Content, "HP% 低於此值施放", 1, 99, Math.Max((int) slot.MinHpPercent, 1), Percent, v => { slot.MinHpPercent = (byte) v; Changed(); });
            }
        }

        AaWidgets.Header(Content, "輔助技能 (最多 5 個，狀態消失就重新施放)");
        foreach (var slot in Edit.BuffSkills) {
            SkillSlotRow("選擇輔助技能", slot, IsBuffSkill, withSubSkill: true);
        }
    }

    private void BuildAttackSkills() {
        AaWidgets.Header(Content, "攻擊技能 (依順序輪流施放)");
        foreach (var slot in Edit.AttackSkills) {
            SkillSlotRow("選擇攻擊技能", slot, IsAttackSkill, withSubSkill: false);
        }

        AaWidgets.Header(Content, "普通攻擊");
        var melee = Edit.StopMelee != 1;
        AaWidgets.Toggle(Content, "使用普通攻擊", melee, v => {
            Edit.StopMelee = (byte) (v ? 0 : 1);
            Changed(redraw: true);
        });
        AaWidgets.Toggle(Content, "SP 低於 100 才用普通攻擊", Edit.StopMelee == 2, v => {
            Edit.StopMelee = (byte) (v ? 2 : 0);
            Changed();
        }, melee);
    }

    private void SkillSlotRow(string title, AutoAttackSettings.SkillSlot slot, Func<AutoAttackLearnedSkill, bool> filter, bool withSubSkill) {
        var row = AaWidgets.Row(Content);
        AaWidgets.Select(row, slot.SkillId == 0 ? "-- 未選擇 --" : SkillName(slot.SkillId), () =>
            PickSkill(title, slot.SkillId, filter, id => {
                slot.SkillId = (ushort) id;
                slot.Level = (byte) LearnedLevel(id);
                if (id != SA_AUTOSPELL) {
                    slot.SubSkillId = 0;
                }
                Changed(redraw: true);
            }));
        if (slot.SkillId == 0) {
            return;
        }
        AaWidgets.Stepper(row, 1, Math.Max(1, LearnedLevel(slot.SkillId)), Math.Max((int) slot.Level, 1), Level, v => { slot.Level = (byte) v; Changed(); });

        if (withSubSkill && slot.SkillId == SA_AUTOSPELL && LearnedLevel(SA_AUTOSPELL) > 0) {
            var sub = AaWidgets.Row(Content);
            AaWidgets.Layout(AaWidgets.Text(sub, "　自動念咒法術", AaWidgets.FONT_SIZE, AaWidgets.DimTextColor), 150f);
            AaWidgets.Select(sub, slot.SubSkillId == 0 ? "-- 預設 --" : SkillName(slot.SubSkillId), () =>
                PickSkill("選擇自動念咒的法術", slot.SubSkillId, s => AutoSpellIds.Contains(s.SkillId), id => {
                    slot.SubSkillId = (ushort) id;
                    Changed(redraw: true);
                }));
        }
    }

    private void BuildBuffItems() {
        AaWidgets.Header(Content, "輔助道具 (卷軸、料理之類，狀態消失就重新使用)");
        for (var i = 0; i < Edit.BuffItems.Length; i++) {
            var slot = Edit.BuffItems[i];
            var row = AaWidgets.Row(Content);
            AaWidgets.Layout(AaWidgets.Text(row, $"{i + 1}.", AaWidgets.FONT_SIZE, AaWidgets.DimTextColor), 30f);
            AaWidgets.Select(row, slot.ItemId == 0 ? "-- 未選擇 --" : ItemName(slot.ItemId), () =>
                PickInventoryItem("選擇輔助道具 (伺服器允許的道具)", slot.ItemId, item => item.BuffWhitelisted, id => {
                    slot.ItemId = id;
                    Changed(redraw: true);
                }));
        }
        AaWidgets.Label(Content, "清單只列出背包裡、伺服器允許當輔助使用的道具。", AaWidgets.SMALL_FONT_SIZE, AaWidgets.DimTextColor);
    }

    private void BuildTeleport() {
        AaWidgets.Header(Content, "瞬移方式");
        AaWidgets.Toggle(Content, "使用蒼蠅翅膀", Edit.TpUseFlyWing, v => { Edit.TpUseFlyWing = v; Changed(); });
        AaWidgets.Toggle(Content, "使用瞬間移動技能", Edit.TpUseTeleportSkill, v => { Edit.TpUseTeleportSkill = v; Changed(); });

        AaWidgets.Header(Content, "何時瞬移");
        AaWidgets.Slider(Content, "HP 低於此值且被攻擊時", 0, 100, Edit.TpMinHpPercent, v => v == 0 ? "關閉" : Percent(v), v => { Edit.TpMinHpPercent = (byte) v; Changed(); });
        AaWidgets.Slider(Content, "找不到怪超過 N 秒", 0, 60, Edit.TpNoMobSeconds, v => v == 0 ? "關閉" : $"{v} 秒", v => { Edit.TpNoMobSeconds = (byte) v; Changed(); });
        AaWidgets.Slider(Content, "同一隻怪 N 秒內沒打死", 0, 30, Math.Min((int) Edit.TpKillTimeoutSeconds, 30), v => v == 0 ? "關閉" : $"{v} 秒", v => { Edit.TpKillTimeoutSeconds = (byte) v; Changed(); });
        AaWidgets.Slider(Content, "安全距離 (逃離清單與周圍怪數的判定範圍)", 1, 20, Math.Max((int) Edit.TpEscapeRange, 1), v => $"{v} 格", v => { Edit.TpEscapeRange = (byte) v; Changed(); });
        AaWidgets.Slider(Content, "周圍怪物超過 N 隻", 0, 10, Edit.TpMonsterSurround, v => v == 0 ? "關閉" : $"{v} 隻", v => { Edit.TpMonsterSurround = (byte) v; Changed(); });
        AaWidgets.Toggle(Content, "看到 MVP 立即瞬移避開", Edit.TpAvoidMvp, v => { Edit.TpAvoidMvp = v; Changed(); });
        AaWidgets.Toggle(Content, "看到 Mini Boss 立即瞬移避開", Edit.TpAvoidMiniBoss, v => { Edit.TpAvoidMiniBoss = v; Changed(); });
    }

    private void BuildMobs() {
        AaWidgets.Header(Content, "本地圖的怪物");
        if (Snapshot.MapMobs.Count == 0) {
            AaWidgets.Label(Content, "目前地圖上沒有怪物。", AaWidgets.FONT_SIZE, AaWidgets.DimTextColor);
        }
        foreach (var mob in Snapshot.MapMobs) {
            var id = mob.Id;
            var row = AaWidgets.Row(Content);
            var name = AaWidgets.Text(row, MobName(id));
            AaWidgets.Layout(name, flexibleWidth: 1f);
            MobListButton(row, Edit.MobWhitelist, id, AutoAttackSettings.MOB_SLOTS, "攻擊");
            MobListButton(row, Edit.EscapeMobList, id, AutoAttackSettings.ESCAPE_MOB_SLOTS, "逃離");
        }

        var manual = AaWidgets.Row(Content);
        AaWidgets.Layout(AaWidgets.Text(manual, "魔物 ID", AaWidgets.FONT_SIZE, AaWidgets.DimTextColor), 80f);
        var typed = 0;
        AaWidgets.NumberField(manual, 0, 0, 65535, v => typed = v, 100f);
        AaWidgets.Button(manual, "加入攻擊", () => AddMob(Edit.MobWhitelist, (uint) typed, AutoAttackSettings.MOB_SLOTS), 100f);
        AaWidgets.Button(manual, "加入逃離", () => AddMob(Edit.EscapeMobList, (uint) typed, AutoAttackSettings.ESCAPE_MOB_SLOTS), 100f);

        MobList("攻擊清單 (空清單 = 打全部怪)", Edit.MobWhitelist, AutoAttackSettings.MOB_SLOTS);
        MobList("逃離清單 (看到就瞬移避開)", Edit.EscapeMobList, AutoAttackSettings.ESCAPE_MOB_SLOTS);
    }

    private void MobListButton(Transform row, List<uint> list, uint id, int slots, string name) {
        if (list.Contains(id)) {
            AaWidgets.Button(row, $"{name} -", () => {
                list.Remove(id);
                Changed(redraw: true);
            }, 84f, AaWidgets.SelectedColor);
        } else {
            var button = AaWidgets.Button(row, $"{name} +", () => AddMob(list, id, slots), 84f);
            button.interactable = list.Count < slots;
        }
    }

    private void AddMob(List<uint> list, uint id, int slots) {
        if (id == 0 || list.Contains(id) || list.Count >= slots) {
            return;
        }
        list.Add(id);
        Changed(redraw: true);
    }

    private void MobList(string title, List<uint> list, int slots) {
        AaWidgets.Header(Content, $"{title}  {list.Count} / {slots}");
        foreach (var id in list.ToList()) {
            var row = AaWidgets.Row(Content);
            AaWidgets.Layout(AaWidgets.Text(row, MobName(id)), flexibleWidth: 1f);
            AaWidgets.Button(row, "移除", () => {
                list.Remove(id);
                Changed(redraw: true);
            }, 80f);
        }
        if (list.Count > 0) {
            ClearButton(() => list.Clear());
        }
    }

    /// <summary>
    /// Clears a list on a second tap, so one slip doesn't
    /// </summary>
    private void ClearButton(Action clear) {
        var row = AaWidgets.Row(Content);
        var armedUntil = 0f;
        Button button = null;
        button = AaWidgets.Button(row, "全部清除", () => {
            if (Time.unscaledTime < armedUntil) {
                clear();
                Changed(redraw: true);
                return;
            }
            armedUntil = Time.unscaledTime + 3f;
            AaWidgets.SetButtonText(button, "再按一次確認清除");
            button.targetGraphic.color = new Color(0.6f, 0.18f, 0.18f, 1f);
        }, 160f);
    }

    private void BuildPickup() {
        AaWidgets.Header(Content, "自動拾取");
        AaWidgets.Toggle(Content, "啟用自動拾取", Edit.PickupEnabled, v => { Edit.PickupEnabled = v; Changed(redraw: true); });
        AaWidgets.Label(Content, "關閉後內掛不會去撿東西；身上的寵物會回到原本「全部自動撿」的行為。", AaWidgets.SMALL_FONT_SIZE, AaWidgets.DimTextColor);
        var on = Edit.PickupEnabled;

        AaWidgets.Header(Content, "優先順序");
        AaWidgets.Toggle(Content, "優先戰鬥 (有目標時先打完再撿)", Edit.PickupPriority == 0, v => { Edit.PickupPriority = 0; Changed(redraw: true); }, on);
        AaWidgets.Toggle(Content, "優先拾取 (地上有東西先去撿)", Edit.PickupPriority == 1, v => { Edit.PickupPriority = 1; Changed(redraw: true); }, on);

        AaWidgets.Header(Content, "拾取種類 (都不勾 = 什麼都撿)");
        var buttons = AaWidgets.Row(Content);
        AaWidgets.Button(buttons, "全選", () => {
            Edit.PickupTypeMask = PickupTypes.Aggregate(0u, (mask, t) => mask | (1u << t.Type));
            Changed(redraw: true);
        }, 100f).interactable = on;
        AaWidgets.Button(buttons, "全部取消", () => {
            Edit.PickupTypeMask = 0;
            Changed(redraw: true);
        }, 100f).interactable = on;

        for (var i = 0; i < PickupTypes.Length; i += 2) {
            var row = AaWidgets.Row(Content);
            for (var j = i; j < Math.Min(i + 2, PickupTypes.Length); j++) {
                var bit = 1u << PickupTypes[j].Type;
                AaWidgets.Toggle(row, PickupTypes[j].Label, (Edit.PickupTypeMask & bit) != 0, v => {
                    Edit.PickupTypeMask = v ? Edit.PickupTypeMask | bit : Edit.PickupTypeMask & ~bit;
                    Changed();
                }, on);
            }
        }

        AaWidgets.Header(Content, "裝備詞條門檻");
        AaWidgets.Slider(Content, "武器 / 防具最少幾條詞條才撿", 0, 5, Edit.PickupEquipMinOptions, v => v == 0 ? "不限制" : $"{v} 條", v => { Edit.PickupEquipMinOptions = (byte) v; Changed(); }, on);
    }

    private void VipNotice() {
        if (!Snapshot.Vip) {
            AaWidgets.Label(Content, "本功能僅限 VIP 玩家使用 (已鎖定)。", AaWidgets.FONT_SIZE, AaWidgets.BadColor);
        }
    }

    private void BuildAutoBuy() {
        AaWidgets.Header(Content, "自動補藥 (到附近商店補貨)");
        VipNotice();
        var vip = Snapshot.Vip;
        for (var i = 0; i < Edit.AutoBuy.Length; i++) {
            var slot = Edit.AutoBuy[i];
            var row = AaWidgets.Row(Content);
            AaWidgets.Layout(AaWidgets.Text(row, $"{i + 1}.", AaWidgets.FONT_SIZE, AaWidgets.DimTextColor), 26f);
            var select = AaWidgets.Select(row, slot.ItemId == 0 ? "-- 未選擇 --" : ItemName(slot.ItemId), () => {
                var options = Snapshot.BuyList.Select(b => new KeyValuePair<string, int>(ItemName(b.Id), (int) b.Id)).ToList();
                AaWidgets.Pick(Root, "選擇要自動購買的道具", options, slot.ItemId, true, id => {
                    slot.ItemId = (ushort) id;
                    if (id == 0) {
                        slot.ThresholdQty = slot.BuyQty = 0;
                    }
                    Changed(redraw: true);
                });
            });
            select.interactable = vip;
            if (slot.ItemId == 0) {
                continue;
            }
            var amounts = AaWidgets.Row(Content);
            AaWidgets.Layout(AaWidgets.Text(amounts, "　剩不到", AaWidgets.FONT_SIZE, AaWidgets.DimTextColor), 80f);
            AaWidgets.NumberField(amounts, slot.ThresholdQty, 0, 30000, v => { slot.ThresholdQty = (ushort) v; Changed(); }).interactable = vip;
            AaWidgets.Layout(AaWidgets.Text(amounts, "個時買", AaWidgets.FONT_SIZE, AaWidgets.DimTextColor), 60f);
            AaWidgets.NumberField(amounts, slot.BuyQty, 0, 1000, v => { slot.BuyQty = (ushort) v; Changed(); }).interactable = vip;
            AaWidgets.Text(amounts, "個", AaWidgets.FONT_SIZE, AaWidgets.DimTextColor);
        }
        AaWidgets.Label(Content, "只能選伺服器允許的道具；錢、負重不夠時不會買。", AaWidgets.SMALL_FONT_SIZE, AaWidgets.DimTextColor);
    }

    private void BuildStorage() {
        AaWidgets.Header(Content, $"自動存倉 (負重過高時把這些道具存進倉庫)  {Edit.StorageList.Count} / {AutoAttackSettings.STORAGE_SLOTS}");
        VipNotice();
        var vip = Snapshot.Vip;

        var add = AaWidgets.Row(Content);
        AaWidgets.Button(add, "從背包加入", () => {
            var self = Session.CurrentSession?.Entity as Entity;
            var options = (self != null ? self.Inventory.ItemList : new List<ItemInfo>())
                .GroupBy(i => i.ItemID)
                .Select(g => new KeyValuePair<string, int>($"{ItemName((uint) g.Key)}  x{g.Sum(i => i.amount)}", g.Key))
                .ToList();
            AaWidgets.Pick(Root, "選擇要自動存倉的道具", options, 0, false, id => AddStorageItem((uint) id));
        }, 140f).interactable = vip;
        var typed = 0;
        AaWidgets.NumberField(add, 0, 0, 9999999, v => typed = v, 110f).interactable = vip;
        AaWidgets.Button(add, "加入 ID", () => AddStorageItem((uint) typed), 90f).interactable = vip;

        foreach (var id in Edit.StorageList.ToList()) {
            var row = AaWidgets.Row(Content);
            AaWidgets.Layout(AaWidgets.Text(row, $"{ItemName(id)} ({id})"), flexibleWidth: 1f);
            AaWidgets.Button(row, "移除", () => {
                Edit.StorageList.Remove(id);
                Changed(redraw: true);
            }, 80f).interactable = vip;
        }
        if (vip && Edit.StorageList.Count > 0) {
            ClearButton(() => Edit.StorageList.Clear());
        }
    }

    private void AddStorageItem(uint id) {
        if (id == 0 || Edit.StorageList.Contains(id) || Edit.StorageList.Count >= AutoAttackSettings.STORAGE_SLOTS) {
            return;
        }
        Edit.StorageList.Add(id);
        Changed(redraw: true);
    }

    private void BuildOther() {
        AaWidgets.Header(Content, "死亡");
        var row = AaWidgets.Row(Content);
        var token = AaWidgets.Toggle(row, "死亡時自動使用原地復活道具", Edit.TokenSiegfried > 0, v => {
            Edit.TokenSiegfried = (byte) (v ? Math.Max((int) Edit.TokenSiegfried, 1) : 0);
            Changed(redraw: true);
        });
        AaWidgets.Layout(token, flexibleWidth: 1f);
        if (Edit.TokenSiegfried > 0) {
            AaWidgets.Stepper(row, 1, 5, Edit.TokenSiegfried, v => $"{v} 次", v => { Edit.TokenSiegfried = (byte) v; Changed(); });
        }
        AaWidgets.Toggle(Content, "死亡後回儲存點 (而不是原地)", Edit.TpReturnToSavePoint, v => { Edit.TpReturnToSavePoint = v; Changed(); });

        AaWidgets.Header(Content, "異常狀態");
        var cure = AaWidgets.Row(Content);
        AaWidgets.Layout(AaWidgets.Text(cure, "自動解除", AaWidgets.FONT_SIZE, AaWidgets.DimTextColor), 90f);
        AaWidgets.Select(cure, CureModes[Math.Min((int) Edit.AutoCureMode, CureModes.Length - 1)], () => {
            var options = Enumerable.Range(1, CureModes.Length - 1).Select(i => new KeyValuePair<string, int>(CureModes[i], i)).ToList();
            AaWidgets.Pick(Root, "用什麼道具解除異常狀態", options, Edit.AutoCureMode, true, v => {
                Edit.AutoCureMode = (byte) v;
                Changed(redraw: true);
            });
        });
        AaWidgets.Toggle(Content, "用治療術 (AL_CURE) 解除黑暗 / 混亂", Edit.HealCuresStatus, v => { Edit.HealCuresStatus = v; Changed(); });

        AaWidgets.Header(Content, "PVP");
        AaWidgets.Toggle(Content, "被玩家攻擊時優先反擊", Edit.PvpCounterAttack, v => { Edit.PvpCounterAttack = v; Changed(); });
    }

    private static string Percent(int value) => $"{value} %";

    private static string Level(int value) => $"Lv {value}";

    #endregion
}
