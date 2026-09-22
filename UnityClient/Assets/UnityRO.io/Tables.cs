using ROIO.Loaders;
using ROIO.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace ROIO {
    public static class Tables {

        public static Dictionary<string, MapTableStruct> MapTable = new Dictionary<string, MapTableStruct>();
        public static Hashtable ResNameTable = new Hashtable();
        public static Hashtable MsgStringTable = new Hashtable();

        public static void Init() {
            InitMsgStringTable();
            InitMp3NameTable();
            InitMapTable();
            InitResNameTable();

            //TODO load these tables
            //LoadTable("data/num2cardillustnametable.txt", 2);
            //LoadTable("data/cardprefixnametable.txt", 2);
            //LoadTable("data/fogparametertable.txt", 5);
        }

        // TEMPORARY BRING-UP GUARD - same pattern as LuaInterface.cs.
        // These are "async void" (fire-and-forget): Init() doesn't await them,
        // so a fault here does NOT stop DBManager.Init() from returning - it
        // only surfaces later as a logged exception on the Unity sync context.
        // Unlike LuaInterface's synchronous .WaitForCompletion() calls, this
        // file was never actually blocking GameManager.Start().
        //
        // Still worth guarding: msgstringtable.txt.txt does not exist in this
        // client's GRF set at all (only msgstringtable.CSV does, a different
        // format Tables.cs does not read) - so InitMsgStringTable() will keep
        // faulting on every run, forever, not just until an extraction step.
        private static void Guard(string what, Action action) {
            try {
                action();
            } catch (Exception e) {
                Debug.LogWarning($"[bring-up] skipped {what}: {e.Message}");
            }
        }

        private async static void InitResNameTable() {
            var data = await Addressables.LoadAssetAsync<TextAsset>($"txt/data/resnametable.txt.txt").Task;
            Guard("ResNameTable", () => {
                foreach (object[] args in TableLoader.LoadTable(data.text, 2)) {
                    ResNameTable[args[1]] = args[2];
                }
            });
        }

        private async static void InitMapTable() {
            var data = await Addressables.LoadAssetAsync<TextAsset>($"txt/data/mapnametable.txt.txt").Task;
            Guard("MapTable(name)", () => {
                foreach (object[] args in TableLoader.LoadTable(data.text, 2)) {
                    var key = Convert.ToString(args[1]);
                    if (!MapTable.ContainsKey(key)) {
                        MapTable.Add(key, new MapTableStruct());
                    }

                    MapTableStruct mts = MapTable[key];
                    mts.name = Convert.ToString(args[2]);
                    MapTable[key] = mts;
                }
            });
        }

        private async static void InitMp3NameTable() {
            var data = await Addressables.LoadAssetAsync<TextAsset>($"txt/data/mp3nametable.txt.txt").Task;
            Guard("MapTable(mp3)", () => {
                foreach (object[] args in TableLoader.LoadTable(data.text, 2)) {
                    var key = Convert.ToString(args[1]);
                    if (!MapTable.ContainsKey(key)) {
                        MapTable.Add(key, new MapTableStruct());
                    }

                    MapTableStruct mts = MapTable[key];
                    mts.mp3 = System.IO.Path.GetFileName(args[2].ToString());
                    MapTable[key] = mts;
                }
            });
        }

        private async static void InitMsgStringTable() {
            var data = await Addressables.LoadAssetAsync<TextAsset>($"txt/data/msgstringtable.txt.txt").Task;
            Guard("MsgStringTable", () => {
                foreach (object[] args in TableLoader.LoadTable(data.text, 1)) {
                    MsgStringTable[args[0]] = args[1];
                }
            });
        }
    }
}
