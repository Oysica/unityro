using MoonSharp.Interpreter;
using ROIO.Utils.Extensions;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Icons and tooltips of statuses (EFST ids), from the client's stateicon tables.
/// </summary>
public static partial class StatusIconTable {

    private const string ICON_PATH = "data/texture/effect/";

    /// <summary>After the stateiconimginfo.lub priorities (PRIORITY_GOLD 0 .. PRIORITY_WHITE 4)</summary>
    public const int PRIORITY_NONE = 5;

    // GRF file names are CP949 bytes kept one char per byte
    private static readonly Encoding GrfEncoding = Encoding.GetEncoding(949);

    // efst -> (file name, priority)
    private static Dictionary<int, KeyValuePair<string, int>> ListedIcons;

    /// <returns>null when the status shows no icon</returns>
    public static Texture2D GetIcon(int efst, out int priority) {
        if (ListedIcons == null) {
            ListedIcons = LoadListedIcons();
        }

        string file;
        if (ListedIcons.TryGetValue(efst, out var listed)) {
            file = listed.Key;
            priority = listed.Value;
        } else if (ClassicIcons.TryGetValue(efst, out var classic)) {
            file = ToGrfName(classic);
            priority = PRIORITY_NONE;
        } else {
            priority = PRIORITY_NONE;
            return null;
        }

        return TextureAssetLoader.Load(ICON_PATH + file);
    }

    private static Dictionary<int, KeyValuePair<string, int>> LoadListedIcons() {
        var icons = new Dictionary<int, KeyValuePair<string, int>>();
        var list = LuaInterface.GetTable("StateIconImgList");
        if (list == null) {
            return icons;
        }

        foreach (var priority in list.Pairs) {
            foreach (var icon in priority.Value.Table.Pairs) {
                icons[(int) icon.Key.Number] = new KeyValuePair<string, int>(icon.Value.String, (int) priority.Key.Number);
            }
        }
        return icons;
    }

    private static string ToGrfName(string name) {
        var bytes = GrfEncoding.GetBytes(name);
        var chars = new char[bytes.Length];
        for (var i = 0; i < bytes.Length; i++) {
            chars[i] = (char) bytes[i];
        }
        return new string(chars);
    }

    /// <summary>
    /// Whether the tooltip shows a countdown, so the icon can warn before it runs out.
    /// </summary>
    public static bool HasTimeLimit(int efst) {
        var info = LuaInterface.GetTable("StateIconList")?.Get(efst);
        return info != null && info.Type == DataType.Table && info.Table.Get("haveTimeLimit").CastToNumber() == 1;
    }

    /// <summary>
    /// The tooltip as TextMeshPro rich text, with the time left where the table puts "%s".
    /// </summary>
    /// <returns>null when the table doesn't describe the status</returns>
    public static string GetDescription(int efst, float remainSeconds) {
        var info = LuaInterface.GetTable("StateIconList")?.Get(efst);
        if (info == null || info.Type != DataType.Table || info.Table.Get("descript").Type != DataType.Table) {
            return null;
        }

        var text = new StringBuilder();
        foreach (var line in info.Table.Get("descript").Table.Values) {
            if (line.Type != DataType.Table) {
                continue;
            }

            var content = line.Table.Get(1).CastToString()?.LuaToText();
            if (content == null) {
                continue;
            }
            if (content.Contains("%s")) {
                if (remainSeconds <= 0) {
                    continue;
                }
                content = content.Replace("%s", FormatTime(remainSeconds));
            }

            var color = line.Table.Get(2);
            if (color.Type == DataType.Table) {
                content = $"<color=#{ColorByte(color.Table, 1):X2}{ColorByte(color.Table, 2):X2}{ColorByte(color.Table, 3):X2}>{content}</color>";
            }

            if (text.Length > 0) {
                text.Append('\n');
            }
            text.Append(content);
        }
        return text.ToString();
    }

    private static int ColorByte(Table color, int index) {
        return Mathf.Clamp((int) color.Get(index).CastToNumber().GetValueOrDefault(), 0, 255);
    }

    private static string FormatTime(float seconds) {
        var total = Mathf.CeilToInt(seconds);
        var hours = total / 3600;
        var minutes = total / 60 % 60;
        var secs = total % 60;
        if (hours > 0) {
            return $"{hours} 小時 {minutes} 分";
        }
        return minutes > 0 ? $"{minutes} 分 {secs} 秒" : $"{secs} 秒";
    }
}
