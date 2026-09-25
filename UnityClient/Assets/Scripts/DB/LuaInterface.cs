using MoonSharp.Interpreter;
using UnityEngine;
using UnityEngine.AddressableAssets;

public class LuaInterface {

    public static Script Environment { get; private set; } = new Script();

    public LuaInterface() {
        InitTables();
    }

    public static Table GetTable(string name) {
        return Environment.Globals[name] as Table;
    }

    private void InitTables() {
        LoadSkillInfoZ();
        LoadJobInfo();
        LoadAccessoryInfo();
        LoadStateIconInfo();

        // Same bring-up stub as Run() below: these read the globals the .lub
        // files were supposed to define, so with compiled bytecode they get
        // null and dereference it (e.g. SkillTable.cs:16-17). Let them fail
        // individually instead of taking GameManager.Start() down with them.
        Guard("ItemTable.LoadItemDb", ItemTable.LoadItemDb);
        Guard("SkillTable.LoadSkillData", SkillTable.LoadSkillData);
        Guard("JobItentityTable.LoadTable", JobItentityTable.LoadTable);
    }

    private void Guard(string what, System.Action action) {
        try {
            action();
        } catch (System.Exception e) {
            Debug.LogWarning($"[bring-up] skipped {what}: {e.Message}");
        }
    }

    /// <summary>
    /// TEMPORARY BRING-UP STUB - remove once the .lub problem is solved.
    ///
    /// The .lub files shipped in this client's GRF are COMPILED Lua 5.1
    /// bytecode: the extracted jobinheritlist.lub.txt (29,218 bytes) starts
    /// with 1b 4c 75 61 51 ("\x1bLuaQ"). MoonSharp is a pure C# interpreter
    /// that only accepts Lua SOURCE, so DoString() throws
    /// "SyntaxErrorException: unexpected symbol near ''" on every one of them,
    /// aborting LuaInterface..ctor -> DBManager.Init -> GameManager.Start
    /// before a socket is ever opened.
    ///
    /// Swallowing the failure here lets GameManager reach the network layer so
    /// the rAthena connection can be verified in isolation. It fixes NOTHING:
    /// every table below stays unset, so LuaInterface.GetTable() returns null
    /// for SKILL_INFO_LIST, JOB_INHERIT_LIST, JobNameTable, PCJobNameTable*
    /// and AccNameTable. Anything rendering skills, job names or accessories
    /// will NullReferenceException.
    ///
    /// Real fixes, in rough order of effort:
    ///   - obtain source .lub files instead of compiled ones
    ///   - decompile with unluac/luadec as an offline build step
    ///   - swap MoonSharp for a Lua 5.1 VM that loads bytecode (NLua/KeraLua)
    /// </summary>
    /// <summary>
    /// Big5 text whose trail byte is 0x5C leaves a "\" before the next character.
    /// The client's Lua 5.1 tolerated that as an unknown escape, MoonSharp (5.2 rules)
    /// rejects it, so double such backslashes to keep the original bytes.
    /// </summary>
    internal static string EscapeStrayBackslashes(string source) {
        return System.Text.RegularExpressions.Regex.Replace(source, @"\\(\\|[^abfnrtvxz0-9""'\\\r\n])",
            m => m.Groups[1].Value == "\\" ? m.Value : "\\" + m.Value);
    }

    private void Run(string key) {
        try {
            Environment.DoString(EscapeStrayBackslashes(LoadTable(key)));
        } catch (System.Exception e) {
            Debug.LogWarning($"[bring-up] skipped lua table '{key}': {e.Message}");
        }
    }

    private void LoadSkillInfoZ() {
        Run("lua/data/luafiles514/lua files/skillinfoz/jobinheritlist.lub.txt");
        Run("lua/data/luafiles514/lua files/skillinfoz/skillid.lub.txt");
        Run("lua/data/luafiles514/lua files/skillinfoz/skilldescript.lub.txt");
        Run("lua/data/luafiles514/lua files/skillinfoz/skillinfolist.lub.txt");
        Run("lua/data/luafiles514/lua files/skillinfoz/skillinfo_f.lub.txt");
        Run("lua/data/luafiles514/lua files/skillinfoz/skilltreeview.lub.txt");
    }

    private void LoadJobInfo() {
        Run("lua/data/luafiles514/lua files/datainfo/jobidentity.lub.txt");
        Run("lua/data/luafiles514/lua files/datainfo/npcidentity.lub.txt");
        Run("lua/data/luafiles514/lua files/datainfo/jobname.lub.txt");
        //environment.DoStream(Addressables.LoadAssetAsync<TextAsset>("data/luafiles514/lua files/datainfo/pcjobnamegender_f.lub"));

        /**
         * Hack for Kagerou and Oboro
         * It seems like Gravity doesnt like to have a common ground for their scripts
         */
        var JTtbl = Environment.Globals["JTtbl"] as Table;
        Environment.Globals["pcJobTbl2"] = JTtbl;

        Run("lua/data/luafiles514/lua files/datainfo/pcjobnamegender.lub.txt");
    }

    private void LoadStateIconInfo() {
        Run("lua/data/luafiles514/lua files/stateicon/efstids.lub.txt");
        Run("lua/data/luafiles514/lua files/stateicon/stateiconimginfo.lub.txt");
        Run("lua/data/luafiles514/lua files/stateicon/stateiconinfo.lub.txt");
    }

    private void LoadAccessoryInfo() {
        Run("lua/data/luafiles514/lua files/datainfo/accessoryid.lub.txt");
        Run("lua/data/luafiles514/lua files/datainfo/accname.lub.txt");
    }

    private string LoadTable(string key) {
        return Addressables.LoadAssetAsync<TextAsset>(key).WaitForCompletion().ToString();
    }
}
