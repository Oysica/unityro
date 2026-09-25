using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;

public class StatsWindowController : DraggableUIWindow {

    [SerializeField] private TextMeshProUGUI Str;
    [SerializeField] private TextMeshProUGUI StrNeed;

    [SerializeField] private TextMeshProUGUI Agi;
    [SerializeField] private TextMeshProUGUI AgiNeed;

    [SerializeField] private TextMeshProUGUI Vit;
    [SerializeField] private TextMeshProUGUI VitNeed;

    [SerializeField] private TextMeshProUGUI Int;
    [SerializeField] private TextMeshProUGUI IntNeed;

    [SerializeField] private TextMeshProUGUI Dex;
    [SerializeField] private TextMeshProUGUI DexNeed;

    [SerializeField] private TextMeshProUGUI Luk;
    [SerializeField] private TextMeshProUGUI LukNeed;

    [SerializeField] private TextMeshProUGUI Atk;
    [SerializeField] private TextMeshProUGUI Def;
    [SerializeField] private TextMeshProUGUI MAtk;
    [SerializeField] private TextMeshProUGUI MDef;
    [SerializeField] private TextMeshProUGUI Hit;
    [SerializeField] private TextMeshProUGUI Flee;
    [SerializeField] private TextMeshProUGUI Crit;
    [SerializeField] private TextMeshProUGUI Aspd;
    [SerializeField] private TextMeshProUGUI Points;
    [SerializeField] private TextMeshProUGUI Guild;

    // The last full status, kept up to date by the single values the server sends afterwards
    private ZC.STATUS Stats;
    private readonly Dictionary<EntityStatus, Button> IncreaseButtons = new Dictionary<EntityStatus, Button>();

    private void Awake() {
        // Each stat row holds its value label and its increase button
        var values = new[] { Str, Agi, Vit, Int, Dex, Luk };
        for (var i = 0; i < values.Length; i++) {
            var status = EntityStatus.SP_STR + i;
            var button = values[i].transform.parent.GetComponentInChildren<Button>(true);
            button.onClick.AddListener(() => new CZ.STATUS_CHANGE(status).Send());
            IncreaseButtons[status] = button;
        }

        UpdateIncreaseButtons();
    }

    public void UpdateData(ZC.STATUS stats) {
        Stats = stats;

        Str.text = stats.str.ToString();
        StrNeed.text = stats.needStr.ToString();

        Agi.text = stats.agi.ToString();
        AgiNeed.text = stats.needAgi.ToString();

        Vit.text = stats.vit.ToString();
        VitNeed.text = stats.needVit.ToString();

        Int.text = stats.inte.ToString();
        IntNeed.text = stats.needInte.ToString();

        Dex.text = stats.dex.ToString();
        DexNeed.text = stats.needDex.ToString();

        Luk.text = stats.luk.ToString();
        LukNeed.text = stats.needLuk.ToString();

        Points.text = $"{stats.stpoint}";
        UpdateDerivedStats();
        UpdateIncreaseButtons();
    }

    /// <summary>
    /// A single value sent after spending points, equipping or a status change
    /// (clif.cpp clif_updatestatus). Base stats come with their bonus instead, see UpdateData.
    /// </summary>
    public void UpdateParameter(EntityStatus status, int value) {
        if (Stats == null) {
            return;
        }

        switch (status) {
            case EntityStatus.SP_STATUSPOINT:
                Stats.stpoint = (short) value;
                Points.text = $"{value}";
                break;
            case EntityStatus.SP_USTR:
                Stats.needStr = value;
                StrNeed.text = $"{value}";
                break;
            case EntityStatus.SP_UAGI:
                Stats.needAgi = value;
                AgiNeed.text = $"{value}";
                break;
            case EntityStatus.SP_UVIT:
                Stats.needVit = value;
                VitNeed.text = $"{value}";
                break;
            case EntityStatus.SP_UINT:
                Stats.needInte = value;
                IntNeed.text = $"{value}";
                break;
            case EntityStatus.SP_UDEX:
                Stats.needDex = value;
                DexNeed.text = $"{value}";
                break;
            case EntityStatus.SP_ULUK:
                Stats.needLuk = value;
                LukNeed.text = $"{value}";
                break;
            // Same sides as ZC_STATUS (clif.cpp clif_initialstatus)
            case EntityStatus.SP_ATK1: Stats.atk = (short) value; break;
            case EntityStatus.SP_ATK2: Stats.atk2 = (short) value; break;
            case EntityStatus.SP_MATK1: Stats.matkMin = (short) value; break;
            case EntityStatus.SP_MATK2: Stats.matkMax = (short) value; break;
            case EntityStatus.SP_DEF1: Stats.def = (short) value; break;
            case EntityStatus.SP_DEF2: Stats.def2 = (short) value; break;
            case EntityStatus.SP_MDEF1: Stats.mdef = (short) value; break;
            case EntityStatus.SP_MDEF2: Stats.mdef2 = (short) value; break;
            case EntityStatus.SP_HIT: Stats.hit = (short) value; break;
            case EntityStatus.SP_FLEE1: Stats.flee = (short) value; break;
            case EntityStatus.SP_FLEE2: Stats.flee2 = (short) value; break;
            case EntityStatus.SP_CRITICAL: Stats.crit = (short) value; break;
            case EntityStatus.SP_ASPD: Stats.aspd = (short) value; break;
            default:
                return;
        }

        UpdateDerivedStats();
        UpdateIncreaseButtons();
    }

    private void UpdateDerivedStats() {
        Atk.text = $"{Stats.atk} ~ {Stats.atk2}";
        Def.text = $"{Stats.def} ~ {Stats.def2}";
        MAtk.text = $"{Stats.matkMin} ~ {Stats.matkMax}";
        MDef.text = $"{Stats.mdef} ~ {Stats.mdef2}";
        Hit.text = $"{Stats.hit}";
        Flee.text = $"{Stats.flee} ~ {Stats.flee2}";
        Crit.text = $"{Stats.crit}";
        Aspd.text = $"{(2000 - Stats.aspd) / 10}";
    }

    /// <summary>
    /// A stat can be raised while the points cover its cost; the server reports a cost of 0 once it is maxed.
    /// </summary>
    private void UpdateIncreaseButtons() {
        if (Stats == null) {
            return;
        }

        var needs = new Dictionary<EntityStatus, int> {
            { EntityStatus.SP_STR, Stats.needStr },
            { EntityStatus.SP_AGI, Stats.needAgi },
            { EntityStatus.SP_VIT, Stats.needVit },
            { EntityStatus.SP_INT, Stats.needInte },
            { EntityStatus.SP_DEX, Stats.needDex },
            { EntityStatus.SP_LUK, Stats.needLuk },
        };
        foreach (var button in IncreaseButtons) {
            var need = needs[button.Key];
            button.Value.interactable = need > 0 && Stats.stpoint >= need;
        }
    }

    public void UpdateData(string value, EntityStatus? status) {
        if (status == null) return;

        switch (status) {
            case EntityStatus.SP_STR:
                Str.text = value;
                break;
            case EntityStatus.SP_AGI:
                Agi.text = value;
                break;
            case EntityStatus.SP_VIT:
                Vit.text = value;
                break;
            case EntityStatus.SP_INT:
                Int.text = value;
                break;
            case EntityStatus.SP_DEX:
                Dex.text = value;
                break;
            case EntityStatus.SP_LUK:
                Luk.text = value;
                break;
        }
    }
}