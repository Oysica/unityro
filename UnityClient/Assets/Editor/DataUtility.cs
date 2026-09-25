#if UNITY_EDITOR
using Assets.Scripts.Renderer.Sprite;
using ROIO;
using ROIO.Loaders;
using ROIO.Models.FileTypes;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.XR;
using static SpriteEntityViewer;
using static ROIO.Models.FileTypes.RSW;

[InitializeOnLoad]
public class DataUtility {

    private static Configuration config;

    static DataUtility() {
        config = ConfigurationLoader.Init();
        FileManager.LoadGRF(config.root, config.grf);
        Debug.Log("Done initializing grf");
    }

    public static string GENERATED_RESOURCES_PATH = Path.Combine("Assets", "_Generated", "Resources");
    public static string GENERATED_ADDRESSABLES_PATH = Path.Combine("Assets", "_Generated", "AddressablesAssets");

    /// <summary>
    /// Path prefix used by <see cref="ExtractTextures"/>. Defaults to the full
    /// texture tree, i.e. the original behaviour of that menu item. Same
    /// reasoning as <see cref="s_spriteFilter"/>: this GRF set holds 70,042
    /// texture entries, too many to extract wholesale for a bring-up test.
    /// </summary>
    private const string DEFAULT_TEXTURE_FILTER = "data/texture";
    private static string s_textureFilter = DEFAULT_TEXTURE_FILTER;

    [MenuItem("UnityRO/Utils/Extract/Textures")]
    static void ExtractTextures() {
        var textureDescriptors = FilterDescriptors(FileManager.GetFileDescriptors(), s_textureFilter).ToList();
        var shouldContinue = true;

        try {
            // This disable Unity's auto update of assets
            // Making it much faster to batch create files like we're about to do
            AssetDatabase.StartAssetEditing();

            var file = 1f;
            foreach (var descriptor in textureDescriptors) {
                try {
                    var progress = file / textureDescriptors.Count;
                    if (EditorUtility.DisplayCancelableProgressBar("UnityRO", $"Extracting texture {file} of {textureDescriptors.Count}\t\t{progress * 100}%", progress)) {
                        shouldContinue = false;
                        break;
                    }

                    var filename = Path.GetFileName(descriptor);
                    var filenameWithoutExtension = Path.GetFileNameWithoutExtension(descriptor).SanitizeForAddressables();
                    var dir = Path.GetDirectoryName(descriptor);

                    string assetPath = Path.Combine(GENERATED_RESOURCES_PATH, dir);

                    Directory.CreateDirectory(assetPath);

                    var texture = FileManager.Load(descriptor) as Texture2D;
                    if (texture != null) {
                        texture.alphaIsTransparency = true;
                        var bytes = texture.EncodeToPNG();
                        var completePath = Path.Combine(assetPath, filenameWithoutExtension + ".png");
                        File.WriteAllBytes(completePath, bytes);
                    }
                    file++;
                } catch (Exception e) {
                    Debug.LogError(e);
                }
            }
        } finally {
            AssetDatabase.StopAssetEditing();
            EditorUtility.ClearProgressBar();
            AssetDatabase.Refresh();
        }

        if (!shouldContinue) {
            return;
        }

        MakeTexturesReadable();
    }

    [MenuItem("UnityRO/Utils/Textures/Make Textures Readable")]
    private static void MakeTexturesReadable() {
        var texturesPaths = GetFilesFromDir(Path.Combine(GENERATED_RESOURCES_PATH, "data", "texture"));
        try {
            AssetDatabase.StartAssetEditing();

            for (int i = 0; i < texturesPaths.Length; i++) {
                var progress = i / texturesPaths.Length;
                if (EditorUtility.DisplayCancelableProgressBar("UnityRO", $"Processing textures {i} of {texturesPaths.Length}\t\t{progress * 100}%", progress)) {
                    break;
                }

                var path = texturesPaths[i];
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) {
                    continue;
                }

                if (path.IndexOf("�����������̽�") > -1) { //make everything under the interface path be a sprite
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    var textureSettings = new TextureImporterSettings();
                    importer.ReadTextureSettings(textureSettings);
                    textureSettings.spriteMeshType = SpriteMeshType.FullRect;
                    textureSettings.spritePixelsPerUnit = SPR.PIXELS_PER_UNIT;

                    importer.SetTextureSettings(textureSettings);
                } else {
                    importer.textureType = TextureImporterType.Default;
                    importer.isReadable = true;
                }

                AssetDatabase.ImportAsset(path);
                //importer.SaveAndReimport(); //perhaps this has better effect?
            }

        } finally {
            AssetDatabase.StopAssetEditing();
            EditorUtility.ClearProgressBar();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }
    }

    /// <summary>
    /// Path prefix used by <see cref="ExtractSprites"/>. Defaults to the full
    /// sprite tree, i.e. the original behaviour of that menu item.
    ///
    /// It exists so a caller can narrow the extraction without duplicating the
    /// 120 lines below. Extracting the whole tree is not viable on a full kRO
    /// GRF: this client's data set holds 188,547 sprite entries, and each .spr
    /// expands into an atlas texture plus one Unity Sprite per frame. That
    /// overruns Unity's graphics resource id space (observed:
    /// "Resource ID out of range in GetResource: 1114156 (max is 1048575)")
    /// and exhausts memory long before the run completes.
    ///
    /// Always restore this to <see cref="DEFAULT_SPRITE_FILTER"/> in a finally
    /// block so the stock menu items keep working.
    /// </summary>
    private const string DEFAULT_SPRITE_FILTER = "data/sprite/";
    private static string s_spriteFilter = DEFAULT_SPRITE_FILTER;

    /// <summary>
    /// Bring-up path: extract only what GameManager.Start() needs in order to
    /// reach the network layer, so the rAthena connection can be verified on
    /// its own.
    ///
    /// Covers the two things that throw before any socket is opened:
    ///   LuaInterface.LoadTable -> lua/data/luafiles514/...  (Extract/Lua Files
    ///                             is already a hand-picked whitelist, ~35 files)
    ///   CursorRenderer.Start   -> data/sprite/cursors.asset / cursors.png
    ///
    /// Roughly 1,570 files instead of 281,076. No maps, no character sprites,
    /// no models - the client will look broken, which is expected here.
    /// </summary>
    [MenuItem("UnityRO/0. Minimal Bring-up (lua + cursors)")]
    static void MinimalBringUp() {
        try {
            s_spriteFilter = "data/sprite/cursors";
            ExtractSprites();
        } finally {
            s_spriteFilter = DEFAULT_SPRITE_FILTER;
        }

        ExtractLuaFiles();

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        CreateSpritesAddressableAssets();
        CreateDataTablesAddressableAssets();

        Debug.Log("[MinimalBringUp] done - extracted cursors + lua tables only.");
    }

    /// <summary>
    /// Second, additive bring-up pass: extract just enough to render the
    /// Novice body/hair sprites, the shared shadow sprite, and the login
    /// background texture, so CharSelection stops being a wall of red
    /// errors. Runs AFTER "0. Minimal Bring-up" and AFTER "4. Rename
    /// Generated Resources folder" has already been run once (this project's
    /// _Generated/AddressablesAssets folder already exists with content).
    ///
    /// IMPORTANT - path encoding: this project does NOT use proper Unicode
    /// Korean for its Addressable keys. GrfSupport.getCString (see
    /// UnityRO.io/GRF/GrfSupport.cs) builds filenames with
    /// System.Convert.ToChar(byte) per raw CP949 byte, i.e. every Korean
    /// path segment ends up as its CP949-bytes-read-as-CP1252 mojibake form
    /// - exactly what StringExtensions.KoreanTo1252() produces on purpose,
    /// and exactly what's already hardcoded all over DBManager.cs (e.g.
    /// INTERFACE_PATH = "data/texture/À¯ÀúÀÎÅÍÆäÀÌ½º/"). The literals below
    /// were generated the same way (proper Korean -> encode CP949 -> decode
    /// CP1252) and verified byte-for-byte against DBManager.cs's existing
    /// constants before being pasted in here. Do NOT "fix" these to look
    /// like real Korean - that would break the address match.
    /// </summary>
    [MenuItem("UnityRO/0b. Bring-up: Character Basics")]
    static void BringUpCharacterBasics() {
        try {
            s_textureFilter = "data/texture/À¯ÀúÀÎÅÍÆäÀÌ½º/bgi_temp";
            ExtractTextures();
        } finally {
            s_textureFilter = DEFAULT_TEXTURE_FILTER;
        }

        string[] spriteTargets = {
            "data/sprite/ÀÎ°£Á·/¸öÅë/¿©/ÃÊº¸ÀÚ_¿©",   // body, female novice
            "data/sprite/ÀÎ°£Á·/¸öÅë/³²/ÃÊº¸ÀÚ_³²",   // body, male novice
            "data/sprite/ÀÎ°£Á·/¸Ó¸®Åë/¿©/1_¿©",       // hair style 1, female
            "data/sprite/ÀÎ°£Á·/¸Ó¸®Åë/³²/1_³²",       // hair style 1, male
            "data/sprite/shadow",                          // shared shadow sprite (plain ASCII)
        };

        foreach (var target in spriteTargets) {
            try {
                s_spriteFilter = target;
                ExtractSprites();
            } finally {
                s_spriteFilter = DEFAULT_SPRITE_FILTER;
            }
        }

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        // Tag as Addressable while the new files are still under the
        // Resources-named folder (Resources.LoadAll requires that literal
        // folder name) - same ordering the stock "1./3./4." pipeline uses.
        CreateTexturesAddressableAssets();
        CreateSpritesAddressableAssets();

        // _Generated/AddressablesAssets already exists from a previous "4."
        // run, so Directory.Move (whole-folder rename) would throw. Merge
        // the newly-extracted files into it instead, moving each file
        // together with its .meta sidecar so GUIDs (and the Addressable
        // Group entries just created above, which reference those GUIDs)
        // stay intact.
        if (Directory.Exists(GENERATED_RESOURCES_PATH)) {
            try {
                AssetDatabase.StartAssetEditing();
                MergeDirectoryInto(GENERATED_RESOURCES_PATH, GENERATED_ADDRESSABLES_PATH);
            } finally {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
        }

        Debug.Log("[BringUpCharacterBasics] done.");
    }

    /// <summary>
    /// Extracts the map textures referenced by the Prontera-area map that
    /// UnityRO's NodeProperties (3D scene-node prop meshes) requested after
    /// successfully entering the map (confirmed via Editor.log: 277
    /// InvalidKeyException hits, deduped to 273 real GRF files, verified
    /// byte-for-byte present via a one-off Python GRF-table scan before
    /// writing this method - see session scratchpad verify_map_textures.py).
    ///
    /// Prontera proper (data/texture/ÇÁ·ÐÅ×¶ó/, no trailing "³»ºÎ") is taken
    /// as one bulk folder filter: it holds 196 total GRF entries and ~190
    /// were needed, so the handful of extra files are harmless. Every other
    /// folder here is a SHARED prop/UI library used by many other maps
    /// (내부소품/외부소품/기타마을/나무잡초꽃/워터/유저인터페이스 range from
    /// 174 to 29,689 total entries each - counted via count_map_folders.py
    /// before deciding), so those stay per-file exact filters to avoid
    /// repeating the earlier full-extraction crash (see MinimalBringUp /
    /// BringUpCharacterBasics comments elsewhere in this file for that
    /// incident). Same CP1252-mojibake path convention as everywhere else
    /// in this file - do NOT "fix" these to look like real Korean.
    /// </summary>
    [MenuItem("UnityRO/0c. Bring-up: Map Textures (Prontera)")]
    static void BringUpMapTextures() {
        string[] textureTargets = {
            "data/texture/ÇÁ·ÐÅ×¶ó/",   // Prontera proper - bulk folder (196 total, ~190 needed)
            "data/texture/camp/pr_cannon01",
            "data/texture/camp/pr_cannon02",
            "data/texture/grid",
            "data/texture/pron-ch1",
            "data/texture/pron-ch2",
            "data/texture/pron-ch3",
            "data/texture/pron-ch4",
            "data/texture/pron-ch5",
            "data/texture/pron-ch6",
            "data/texture/pron-ch7",
            "data/texture/pron-ch8",
            "data/texture/pron-ch9",
            "data/texture/±âÅ¸¸¶À»/hand_01",
            "data/texture/±âÅ¸¸¶À»/hand_02",
            "data/texture/±âÅ¸¸¶À»/izld-br2",
            "data/texture/±âÅ¸¸¶À»/izld-br3",
            "data/texture/±âÅ¸¸¶À»/izld-br5",
            "data/texture/³ª¹«ÀâÃÊ²É/mo-tree-block",
            "data/texture/³ª¹«ÀâÃÊ²É/newtree_01",
            "data/texture/³ª¹«ÀâÃÊ²É/newtree_02",
            "data/texture/³»ºÎ¼ÒÇ°/box2-side",
            "data/texture/³»ºÎ¼ÒÇ°/box2",
            "data/texture/³»ºÎ¼ÒÇ°/ch-side1",
            "data/texture/³»ºÎ¼ÒÇ°/ch-side2",
            "data/texture/³»ºÎ¼ÒÇ°/ch-side3",
            "data/texture/³»ºÎ¼ÒÇ°/cha2-side1",
            "data/texture/³»ºÎ¼ÒÇ°/cha2-side2",
            "data/texture/³»ºÎ¼ÒÇ°/cha2-side3",
            "data/texture/³»ºÎ¼ÒÇ°/d-w",
            "data/texture/³»ºÎ¼ÒÇ°/drum-1",
            "data/texture/³»ºÎ¼ÒÇ°/durm-1",
            "data/texture/¿ÜºÎ¼ÒÇ°/myo-brd1",
            "data/texture/¿ÜºÎ¼ÒÇ°/myo-msign1",
            "data/texture/¿ÜºÎ¼ÒÇ°/pron-bench1",
            "data/texture/¿ÜºÎ¼ÒÇ°/pron-bench2",
            "data/texture/¿ÜºÎ¼ÒÇ°/pron-bench3",
            "data/texture/¿ÜºÎ¼ÒÇ°/pron-wag1",
            "data/texture/¿ÜºÎ¼ÒÇ°/pron-wag2",
            "data/texture/¿ÜºÎ¼ÒÇ°/pron-wag3",
            "data/texture/¿öÅÍ/water000",
            "data/texture/¿öÅÍ/water001",
            "data/texture/¿öÅÍ/water002",
            "data/texture/¿öÅÍ/water003",
            "data/texture/¿öÅÍ/water004",
            "data/texture/¿öÅÍ/water005",
            "data/texture/¿öÅÍ/water006",
            "data/texture/¿öÅÍ/water007",
            "data/texture/¿öÅÍ/water008",
            "data/texture/¿öÅÍ/water009",
            "data/texture/¿öÅÍ/water010",
            "data/texture/¿öÅÍ/water011",
            "data/texture/¿öÅÍ/water012",
            "data/texture/¿öÅÍ/water013",
            "data/texture/¿öÅÍ/water014",
            "data/texture/¿öÅÍ/water015",
            "data/texture/¿öÅÍ/water016",
            "data/texture/¿öÅÍ/water017",
            "data/texture/¿öÅÍ/water018",
            "data/texture/¿öÅÍ/water019",
            "data/texture/¿öÅÍ/water020",
            "data/texture/¿öÅÍ/water021",
            "data/texture/¿öÅÍ/water022",
            "data/texture/¿öÅÍ/water023",
            "data/texture/¿öÅÍ/water024",
            "data/texture/¿öÅÍ/water025",
            "data/texture/¿öÅÍ/water026",
            "data/texture/¿öÅÍ/water027",
            "data/texture/¿öÅÍ/water028",
            "data/texture/¿öÅÍ/water029",
            "data/texture/¿öÅÍ/water030",
            "data/texture/¿öÅÍ/water031",
            "data/texture/À¯ÀúÀÎÅÍÆäÀÌ½º/bgi_temp",
            "data/texture/À¯ÀúÀÎÅÍÆäÀÌ½º/loading06",
            "data/texture/À¯ÀúÀÎÅÍÆäÀÌ½º/make_character_ver2/img_hairstyle_doramgirl01",
            "data/texture/À¯ÀúÀÎÅÍÆäÀÌ½º/make_character_ver2/img_hairstyle_doramgirl02",
            "data/texture/À¯ÀúÀÎÅÍÆäÀÌ½º/make_character_ver2/img_hairstyle_doramgirl03",
            "data/texture/À¯ÀúÀÎÅÍÆäÀÌ½º/make_character_ver2/img_hairstyle_doramgirl04",
            "data/texture/À¯ÀúÀÎÅÍÆäÀÌ½º/make_character_ver2/img_hairstyle_doramgirl05",
            "data/texture/À¯ÀúÀÎÅÍÆäÀÌ½º/make_character_ver2/img_hairstyle_doramgirl06",
            "data/texture/À¯ÀúÀÎÅÍÆäÀÌ½º/make_character_ver2/img_hairstyle_girl01",
            "data/texture/À¯ÀúÀÎÅÍÆäÀÌ½º/make_character_ver2/img_hairstyle_girl02",
            "data/texture/À¯ÀúÀÎÅÍÆäÀÌ½º/make_character_ver2/img_hairstyle_girl03",
            "data/texture/À¯ÀúÀÎÅÍÆäÀÌ½º/make_character_ver2/img_hairstyle_girl04",
            "data/texture/À¯ÀúÀÎÅÍÆäÀÌ½º/make_character_ver2/img_hairstyle_girl05",
            "data/texture/À¯ÀúÀÎÅÍÆäÀÌ½º/make_character_ver2/img_hairstyle_girl06",
            "data/texture/À¯ÀúÀÎÅÍÆäÀÌ½º/make_character_ver2/img_hairstyle_girl07",
            "data/texture/À¯ÀúÀÎÅÍÆäÀÌ½º/make_character_ver2/img_hairstyle_girl08",
            "data/texture/À¯ÀúÀÎÅÍÆäÀÌ½º/make_character_ver2/img_hairstyle_girl09",
            "data/texture/À¯ÀúÀÎÅÍÆäÀÌ½º/make_character_ver2/img_hairstyle_girl10",
            "data/texture/À¯ÀúÀÎÅÍÆäÀÌ½º/make_character_ver2/img_hairstyle_girl11",
            "data/texture/À¯ÀúÀÎÅÍÆäÀÌ½º/make_character_ver2/img_hairstyle_girl12",
            "data/texture/À¯ÀúÀÎÅÍÆäÀÌ½º/make_character_ver2/img_hairstyle_girl13",
            "data/texture/À¯ÀúÀÎÅÍÆäÀÌ½º/make_character_ver2/img_hairstyle_girl14",
            "data/texture/À¯ÀúÀÎÅÍÆäÀÌ½º/make_character_ver2/img_hairstyle_girl15",
            "data/texture/À¯ÀúÀÎÅÍÆäÀÌ½º/make_character_ver2/img_hairstyle_girl16",
            "data/texture/À¯ÀúÀÎÅÍÆäÀÌ½º/make_character_ver2/img_hairstyle_girl17",
            "data/texture/À¯ÀúÀÎÅÍÆäÀÌ½º/make_character_ver2/img_hairstyle_girl18",
            "data/texture/À¯ÀúÀÎÅÍÆäÀÌ½º/make_character_ver2/img_hairstyle_girl19",
            "data/texture/À¯ÀúÀÎÅÍÆäÀÌ½º/make_character_ver2/img_hairstyle_girl20",
            "data/texture/À¯ÀúÀÎÅÍÆäÀÌ½º/make_character_ver2/img_hairstyle_girl21",
            "data/texture/À¯ÀúÀÎÅÍÆäÀÌ½º/make_character_ver2/img_hairstyle_girl22",
            "data/texture/À¯ÀúÀÎÅÍÆäÀÌ½º/make_character_ver2/img_hairstyle_girl23",
            "data/texture/À¯ÀúÀÎÅÍÆäÀÌ½º/map/map_arrow",
            "data/texture/ÇÁ·ÐÅ×¶ó³»ºÎ/h1-door",
            "data/texture/ÇÁ·ÐÅ×¶ó³»ºÎ/h2-door",
            "data/texture/ÇÁ·ÐÅ×¶ó³»ºÎ/h3-door",
        };

        foreach (var target in textureTargets) {
            try {
                s_textureFilter = target;
                ExtractTextures();
            } finally {
                s_textureFilter = DEFAULT_TEXTURE_FILTER;
            }
        }

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        CreateTexturesAddressableAssets();

        if (Directory.Exists(GENERATED_RESOURCES_PATH)) {
            try {
                AssetDatabase.StartAssetEditing();
                MergeDirectoryInto(GENERATED_RESOURCES_PATH, GENERATED_ADDRESSABLES_PATH);
            } finally {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
        }

        Debug.Log("[BringUpMapTextures] done.");
    }

    /// <summary>
    /// Recursively moves every file (and its .meta sidecar, if any) from
    /// sourceDir into destDir, creating destDir subfolders as needed, then
    /// removes the now-empty sourceDir. Used because System.IO.Directory.Move
    /// throws if destDir already exists, which a whole-folder rename can't
    /// handle but a per-file merge can.
    /// </summary>
    private static void MergeDirectoryInto(string sourceDir, string destDir) {
        if (!Directory.Exists(sourceDir)) {
            return;
        }
        Directory.CreateDirectory(destDir);

        foreach (var file in Directory.GetFiles(sourceDir)) {
            if (file.EndsWith(".meta")) {
                continue; // moved alongside its asset below
            }

            var destFile = Path.Combine(destDir, Path.GetFileName(file));
            if (File.Exists(destFile)) {
                File.Delete(destFile);
            }
            File.Move(file, destFile);

            var metaSrc = file + ".meta";
            var metaDest = destFile + ".meta";
            if (File.Exists(metaSrc)) {
                if (File.Exists(metaDest)) {
                    File.Delete(metaDest);
                }
                File.Move(metaSrc, metaDest);
            }
        }

        foreach (var dir in Directory.GetDirectories(sourceDir)) {
            MergeDirectoryInto(dir, Path.Combine(destDir, Path.GetFileName(dir)));
        }

        var sourceDirMeta = sourceDir + ".meta";
        if (File.Exists(sourceDirMeta)) {
            File.Delete(sourceDirMeta);
        }
        Directory.Delete(sourceDir, false);
    }

    [MenuItem("UnityRO/Utils/Extract/Sprites")]
    static void ExtractSprites() {
        try {
            var shouldContinue = true;
            var descriptors = FilterDescriptors(FileManager.GetFileDescriptors(), s_spriteFilter)
                .Select(it => it[..it.IndexOf(Path.GetExtension(it))])
                .Where(it => it.Length > 0)
                .Distinct()
                .ToList();

            /**
             * First we bulk create all the assets and the atlas textures
             * Then we tell Unity to refresh its asset database
             * After that we go over each sprite and convert its atlas texture to a proper Sprite format 
             * and slice every subsprite on the atlas
             */
            #region Extract atlases and acts
            AssetDatabase.StartAssetEditing();
            var spriteDataPaths = new List<string>();
            var spritesList = new List<List<Sprite>>();
            for (int i = 0; i < descriptors.Count; i++) {
                var progress = i * 1f / descriptors.Count;
                if (EditorUtility.DisplayCancelableProgressBar("UnityRO", $"Extracting sprites {i} of {descriptors.Count}\t\t{progress * 100}%", progress)) {
                    shouldContinue = false;
                    break;
                }

                try {
                    var descriptor = descriptors[i];
                    var sprPath = descriptor + ".spr";
                    var memoryReader = FileManager.ReadSync(descriptor + ".spr");

                    if (memoryReader == null) {
                        Debug.LogError($"Failed to extract {descriptor}");
                        continue;
                    }

                    var spr = memoryReader.ToArray();
                    var act = FileManager.Load(descriptor + ".act") as ACT;
                    var sprLoader = new CustomSpriteLoader();

                    var filename = Path.GetFileName(sprPath);
                    var filenameWithoutExtension = Path.GetFileNameWithoutExtension(sprPath);
                    var dir = sprPath.Substring(0, sprPath.IndexOf(filename));
                    string assetPath = Path.Combine(GENERATED_RESOURCES_PATH, dir);

                    Directory.CreateDirectory(assetPath);

                    var spriteData = ScriptableObject.CreateInstance<SpriteData>();
                    sprLoader.Load(spr, filename);

                    var spritePath = Path.Combine(assetPath, filenameWithoutExtension);

                    spriteData.act = act;
                    spriteData.rects = sprLoader.SpriteRects;
                    spritesList.Add(sprLoader.Sprites);

                    var atlas = sprLoader.Atlas;
                    var bytes = atlas.EncodeToPNG();
                    var atlasPath = spritePath + ".png";
                    File.WriteAllBytes(atlasPath, bytes);

                    var palette = sprLoader.Palette;
                    var paletteBytes = palette.EncodeToPNG();
                    var palettePath = spritePath + "_pal.png";
                    File.WriteAllBytes(palettePath, paletteBytes);

                    var fullAssetPath = spritePath + ".asset";
                    AssetDatabase.CreateAsset(spriteData, fullAssetPath);
                    spriteDataPaths.Add(spritePath);
                } catch (Exception ex) {
                    Debug.LogError($"Failed extracting sprites {ex}");
                }
            }

            AssetDatabase.StopAssetEditing();
            AssetDatabase.Refresh();
            #endregion

            if (!shouldContinue) {
                return;
            }

            #region Post process atlases
            AssetDatabase.StartAssetEditing();

            var dataList = new List<SpriteData>();
            for (var i = 0; i < spriteDataPaths.Count; i++) {
                var progress = i * 1f / spriteDataPaths.Count;
                if (EditorUtility.DisplayCancelableProgressBar("UnityRO", $"Post processing sprites {i} of {spriteDataPaths.Count}\t\t{progress * 100}%", progress)) {
                    break;
                }

                try {
                    var spritePath = spriteDataPaths[i];
                    var sprites = spritesList[i];

                    var spriteData = AssetDatabase.LoadAssetAtPath(spritePath + ".asset", typeof(SpriteData)) as SpriteData;
                    dataList.Add(spriteData);

                    if (spriteData != null) {
                        ProcessAtlas(spritePath + ".png", sprites);
                        ProcessPalette(spritePath + "_pal.png");
                    } else {
                        Debug.LogError($"Failed to load atlas of {spritePath}");
                    }
                } catch (Exception ex) {
                    Debug.LogError($"Failed post processing sprite {ex}");
                }
            }

            AssetDatabase.StopAssetEditing();
            AssetDatabase.Refresh();
            #endregion

            AssetDatabase.SaveAssets();
        } catch (Exception ex) {
            Debug.LogError(ex);
        } finally {
            EditorUtility.ClearProgressBar();
        }
    }

    [MenuItem("UnityRO/Utils/Extract/Palette")]
    static void ExtractPalettes() {
        var descriptors = FilterDescriptors(FileManager.GetFileDescriptors(), "data/palette/")
            .Select(it => it[..it.IndexOf(Path.GetExtension(it))])
            .Where(it => it.Length > 0)
            .Distinct()
            .ToList();

        try {
            // Bulk write all palettes to disk
            AssetDatabase.StartAssetEditing();
            var paths = new List<string>();
            for (int i = 0; i < descriptors.Count; i++) {
                var progress = i * 1f / descriptors.Count;
                if (EditorUtility.DisplayCancelableProgressBar("UnityRO", $"Extracting palettes {i} of {descriptors.Count}\t\t{progress * 100}%", progress)) {
                    break;
                }

                var descriptor = descriptors[i];
                try {
                    var sprPath = descriptor + ".pal";
                    var memoryReader = FileManager.ReadSync(descriptor + ".pal");

                    if (memoryReader == null) {
                        Debug.LogError($"Failed to extract {descriptor}");
                        continue;
                    }

                    var filename = Path.GetFileName(sprPath);
                    var filenameWithoutExtension = Path.GetFileNameWithoutExtension(sprPath);
                    var dir = sprPath.Substring(0, sprPath.IndexOf(filename));
                    string assetDirectory = Path.Combine(GENERATED_RESOURCES_PATH, dir);

                    Directory.CreateDirectory(assetDirectory);

                    var filepath = Path.Combine(assetDirectory, filenameWithoutExtension);

                    var paletteTexture = new Texture2D(256, 1, TextureFormat.RGBA32, false, true);
                    paletteTexture.alphaIsTransparency = false;
                    paletteTexture.filterMode = FilterMode.Point;
                    paletteTexture.LoadRawTextureData(memoryReader.ToArray());
                    paletteTexture.Apply();

                    var paletteBytes = paletteTexture.EncodeToPNG();
                    var palettePath = filepath + ".png";
                    File.WriteAllBytes(palettePath, paletteBytes);
                    paths.Add(palettePath);
                } catch {
                    Debug.LogError($"Couldnt extract palette {descriptor}");
                }

            }
            AssetDatabase.StopAssetEditing();
            AssetDatabase.Refresh();

            // Process all palettes
            AssetDatabase.StartAssetEditing();
            for (int i = 0; i < paths.Count; i++) {
                var progress = i * 1f / paths.Count;
                if (EditorUtility.DisplayCancelableProgressBar("UnityRO", $"Processing palettes {i} of {paths.Count}\t\t{progress * 100}%", progress)) {
                    break;
                }
                ProcessPalette(paths[i]);
            }
            AssetDatabase.StopAssetEditing();
            AssetDatabase.Refresh();
        } finally {
            AssetDatabase.StopAssetEditing();
            EditorUtility.ClearProgressBar();
        }
    }

    private static void ProcessPalette(string path) {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = false;
        importer.alphaIsTransparency = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        var textureSettings = new TextureImporterSettings();
        importer.ReadTextureSettings(textureSettings);
        importer.SetTextureSettings(textureSettings);
        importer.SaveAndReimport();
    }

    private static void ProcessAtlas(string spritePath, List<Sprite> sprites) {
        TextureImporter importer = AssetImporter.GetAtPath(spritePath) as TextureImporter;
        importer.textureType = TextureImporterType.SingleChannel;
        importer.sRGBTexture = false;
        importer.alphaIsTransparency = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        var textureSettings = new TextureImporterSettings();
        importer.ReadTextureSettings(textureSettings);
        textureSettings.singleChannelComponent = TextureImporterSingleChannelComponent.Red;
        textureSettings.textureFormat = TextureImporterFormat.R8;
        importer.SetTextureSettings(textureSettings);
        importer.SaveAndReimport();
    }

    [MenuItem("UnityRO/Utils/Fix interface textures")]
    static void FixInterfaceTextures() {
        var paths = GetFilesFromDir(Path.Combine("Assets", "_Generated", "AddressablesAssets", "data", "texture", "�����������̽�"))
            .Where(it => Path.GetExtension(it) == ".png")
            .ToList();
        Debug.Log(paths.Count);
        AssetDatabase.StartAssetEditing();

        for (var i = 0; i < paths.Count; i++) {
            var progress = i * 1f / paths.Count;
            if (EditorUtility.DisplayCancelableProgressBar("UnityRO", $"Post processing UI textures {i} of {paths.Count}\t\t{progress * 100}%", progress)) {
                break;
            }

            try {
                var texturePath = paths[i];

                TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;

                if (importer == null) {
                    continue;
                }

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                var textureSettings = new TextureImporterSettings();
                importer.ReadTextureSettings(textureSettings);
                textureSettings.spriteMeshType = SpriteMeshType.FullRect;
                textureSettings.spritePixelsPerUnit = SPR.PIXELS_PER_UNIT;

                importer.SetTextureSettings(textureSettings);
                importer.SaveAndReimport();

            } catch (Exception ex) {
                Debug.LogError($"Failed post processing sprite {ex}");
            }
        }

        AssetDatabase.StopAssetEditing();
        AssetDatabase.Refresh();
    }

    [MenuItem("UnityRO/Utils/Fix Sprites PixelsPerUnit")]
    static void FixSpritePixelsPerUnit() {
        var paths = GetFilesFromDir(Path.Combine("Assets", "_Generated", "AddressablesAssets", "data", "sprite"))
            .Where(it => Path.GetExtension(it) == ".png")
            .ToList();

        AssetDatabase.StartAssetEditing();

        var dataList = new List<SpriteData>();
        for (var i = 0; i < paths.Count; i++) {
            var progress = i * 1f / paths.Count;
            if (EditorUtility.DisplayCancelableProgressBar("UnityRO", $"Post processing sprites {i} of {paths.Count}\t\t{progress * 100}%", progress)) {
                break;
            }
            var path = paths[i];
            try {
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                var textureSettings = new TextureImporterSettings();
                importer.ReadTextureSettings(textureSettings);
                textureSettings.spritePixelsPerUnit = SPR.PIXELS_PER_UNIT;
                importer.SetTextureSettings(textureSettings);
                importer.SaveAndReimport();
            } catch (Exception ex) {
                Debug.LogError($"Failed post processing sprite {ex}");
            }
        }

        AssetDatabase.StopAssetEditing();
        AssetDatabase.Refresh();
    }

    [MenuItem("UnityRO/Utils/Extract/Lua Files")]
    static void ExtractLuaFiles() {
        /**
         * These files are the ones I could find on an experiment
         * where I put a script to log the name of the file when the OG client
         * was loading. So apparently this is the order they load and these are
         * the only files being actually used
         */
        var files = new string[] {
            "PetEvolutionCln_true.lub",
            "achievement_list.lub",
            "PrivateAirplane_true.lub",
            "CheckAttendance.lub",
            "itemInfo_true.lub",
            "tipbox.lub",
            "data/luafiles514/lua files/datainfo/changedirectorylist.lub",
            "data/luafiles514/lua files/msgstring_kr.lub",
            "data/luafiles514/lua files/datainfo/npcidentity.lub",
            "data/luafiles514/lua files/datainfo/jobname_f.lub",
            "data/luafiles514/lua files/datainfo/jobname.lub",
            "data/luafiles514/lua files/datainfo/pcjobnamegender.lub",
            "data/luafiles514/lua files/datainfo/petinfo.lub",
            "data/luafiles514/lua files/datainfo/accessoryid.lub",
            "data/luafiles514/lua files/datainfo/accname_f.lub",
            "data/luafiles514/lua files/datainfo/accname.lub",
            "data/luafiles514/lua files/skillinfoz/jobinheritlist.lub",
            "data/luafiles514/lua files/skillinfoz/skillid.lub",
            "data/luafiles514/lua files/skillinfoz/skillinfolist.lub",
            "data/luafiles514/lua files/skillinfoz/skilldescript.lub",
            "data/luafiles514/lua files/skillinfoz/skillinfo_f.lub",
            "data/luafiles514/lua files/skillinfoz/skilltreeview.lub",
            "data/luafiles514/lua files/stateicon/efstids.lub",
            "data/luafiles514/lua files/stateicon/stateiconinfo.lub",
            "data/luafiles514/lua files/stateicon/stateiconinfo_f.lub",
            "data/luafiles514/lua files/stateicon/stateiconimginfo.lub",
            "LuaFiles514/OptionInfo.lub",
            "data/luafiles514/lua files/optioninfo/cmdinfo.lub",
            "data/luafiles514/lua files/optioninfo/optioninfo_f.lub",
            "data/luafiles514/lua files/datainfo/spriterobeid.lub",
            "data/luafiles514/lua files/datainfo/spriterobename_f.lub",
            "data/luafiles514/lua files/datainfo/spriterobename.lub",
            "data/luafiles514/lua files/datainfo/npclocationradius.lub",
            "data/luafiles514/lua files/datainfo/npclocationradius_f.lub",
            "data/luafiles514/lua files/skilleffectinfo/effectid.lub",
            "data/luafiles514/lua files/skilleffectinfo/actorstate.lub",
            "data/luafiles514/lua files/skilleffectinfo/skilleffectinfolist.lub",
            "data/luafiles514/lua files/skilleffectinfo/skilleffectinfo_f.lub",
            "data/luafiles514/lua files/datainfo/kaframovemapservicelist.lub",
            "data/luafiles514/lua files/datainfo/kaframovemapservicelist_f.lub",
            "data/luafiles514/lua files/navigation/navi_f_krpri.lub",
            "data/luafiles514/lua files/navigation/navi_map_krpri.lub",
            "data/luafiles514/lua files/navigation/navi_npc_krpri.lub",
            "data/luafiles514/lua files/navigation/navi_mob_krpri.lub",
            "data/luafiles514/lua files/navigation/navi_link_krpri.lub",
            "data/luafiles514/lua files/navigation/navi_linkdistance_krpri.lub",
            "data/luafiles514/lua files/navigation/navi_npcdistance_krpri.lub",
            "data/luafiles514/lua files/navigation/navi_scroll_krpri.lub",
            "data/luafiles514/lua files/navigation/navi_picknpc_krpri.lub",
            "data/luafiles514/lua files/datainfo/helpmsgstr.lub",
            "data/luafiles514/lua files/entryqueue/entryqueuelist.lub",
            "data/luafiles514/lua files/datainfo/weapontable.lub",
            "data/luafiles514/lua files/datainfo/weapontable_f.lub",
            "data/luafiles514/lua files/datainfo/jobidentity.lub",
            "data/luafiles514/lua files/datainfo/shadowtable.lub",
            "data/luafiles514/lua files/datainfo/shadowtable_f.lub",
            "data/luafiles514/lua files/worldviewdata/worldviewdata_language.lub",
            "data/luafiles514/lua files/worldviewdata/worldviewdata_list.lub",
            "data/luafiles514/lua files/worldviewdata/worldviewdata_table.lub",
            "data/luafiles514/lua files/worldviewdata/worldviewdata_f.lub",
            "data/luafiles514/lua files/worldviewdata/worldviewdata_info.lub",
            "data/luafiles514/lua files/datainfo/enumvar.lub",
            "data/luafiles514/lua files/datainfo/addrandomoptionnametable.lub",
            "data/luafiles514/lua files/datainfo/addrandomoption_f.lub",
            "data/luafiles514/lua files/dressroom/dress_f.lub",
            "data/luafiles514/lua files/dressroom/jobdresslist.lub",
            "data/luafiles514/lua files/datainfo/titletable.lub",
            "data/luafiles514/lua files/hateffectinfo/hateffectinfo.lub",
            "data/luafiles514/lua files/signboardlist.lub",
            "data/luafiles514/lua files/effecttool/forcerendereffect.lub",
            "data/luafiles514/lua files/datainfo/lapineddukddakbox.lub",
            "data/luafiles514/lua files/datainfo/LapineUpgradeBox.lub",
            "data/luafiles514/lua files/transparentItem/transparentItem.lub",
            "data/luafiles514/lua files/transparentItem/transparentItem_f.lub",
            "data/luafiles514/lua files/service_brazil/ExternalSettings_br.lub",
            "data/luafiles514/lua files/datainfo/TB_Layer_Priority.lub",
            "data/luafiles514/lua files/datainfo/tb_cashshop_banner.lub",
            "mapInfo_true.lub",
            "Towninfo.lub",
            "data/luafiles514/lua files/datainfo/questinfo_f.lub",
            "RecommendedQuestInfoList_True.lub",
        };

        try {
            AssetDatabase.StartAssetEditing();

            foreach (var lua in files) {
                try {
                    string luaFileString;
                    if (lua.StartsWith("data/lua")) {
                        luaFileString = FileManager.ReadSync(lua, System.Text.Encoding.GetEncoding(1252)).ReadToEnd();
                    } else {
                        luaFileString = new StreamReader(Path.Combine(config.SystemPath, lua), System.Text.Encoding.GetEncoding(1252)).ReadToEnd();
                    }
                    var path = Path.Combine(GENERATED_RESOURCES_PATH, "lua", Path.GetDirectoryName(lua));
                    Directory.CreateDirectory(path);

                    File.WriteAllText(Path.Combine(path, Path.GetFileName(lua) + ".txt"), luaFileString);
                } catch (Exception e) {
                    Debug.LogError($"Couldnt load file {lua} {e}");
                }
            }
        } finally {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.Refresh();
        }
    }

    [MenuItem("UnityRO/Utils/Extract/Txt Tables")]
    static void ExtractTables() {
        try {
            var descriptors = FilterDescriptors(FileManager.GetFileDescriptors(), "data")
                .Where(it => Path.GetExtension(it) == ".txt")
                .ToList();

            AssetDatabase.StartAssetEditing();

            foreach (var descriptor in descriptors) {
                try {
                    string table = FileManager.Load(descriptor) as string;
                    var path = Path.Combine(GENERATED_RESOURCES_PATH, "txt", Path.GetDirectoryName(descriptor));
                    Directory.CreateDirectory(path);

                    File.WriteAllText(Path.Combine(path, Path.GetFileName(descriptor) + ".txt"), table);
                } catch (Exception e) {
                    Debug.LogError($"Couldnt load file {descriptor} {e}");
                }
            }

            ExtractMsgStringTableFromCsv();
        } finally {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.Refresh();
        }
    }

    // CSV line index == MSI value is verified against clif.hpp through 4137; the CSV skips IDs after that.
    private const int MaxVerifiedMsgStringId = 4137;

    /// <summary>
    /// Newer clients ship data/MsgStringTable.csv instead of data/msgstringtable.txt.
    /// Converts it into the '#'-separated format Tables.InitMsgStringTable reads.
    /// </summary>
    [MenuItem("UnityRO/Utils/Extract/MsgStringTable (from CSV)")]
    static void ExtractMsgStringTableFromCsv() {
        var target = Path.Combine(GENERATED_RESOURCES_PATH, "txt", "data", "msgstringtable.txt.txt");
        if (File.Exists(target)) {
            return;
        }

        var csv = FileManager.ReadSync("data/MsgStringTable.csv");
        if (csv == null) {
            Debug.LogWarning("data/MsgStringTable.csv not found; msgstringtable left unextracted");
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(target));
        File.WriteAllText(target, ConvertMsgStringTableCsv(csv.ToArray()));
    }

    internal static string ConvertMsgStringTableCsv(byte[] csv) {
        var messages = new List<string>();
        foreach (var line in System.Text.Encoding.UTF8.GetString(csv).Replace("\r", "").Split('\n')) {
            if (line.Length == 0) {
                continue;
            }
            if (messages.Count > MaxVerifiedMsgStringId) {
                break;
            }

            var encoded = line[(line.IndexOf(',') + 1)..];
            var text = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(encoded));

            // '#' is the entry separator and a line starting with "//" is stripped as a comment by TableLoader.
            text = text.Replace("#", "＃").Replace("\n//", "\n //");
            if (text.StartsWith("//")) {
                text = " " + text;
            }
            messages.Add(text);
        }

        return string.Join("#\n", messages) + "#\n";
    }

    [MenuItem("UnityRO/Utils/Extract/Effects")]
    static void ExtractEffects() {
        AssetDatabase.StartAssetEditing();

        try {
            var descriptors = FilterDescriptors(FileManager.GetFileDescriptors(), "data/texture/effect")
                .Where(it => Path.GetExtension(it) == ".str")
                .ToList();

            for (int i = 0; i < descriptors.Count; i++) {
                var progress = i * 1f / descriptors.Count;
                if (EditorUtility.DisplayCancelableProgressBar("UnityRO", $"Extracting effects {i} of {descriptors.Count}\t\t{progress * 100}%", progress)) {
                    break;
                }

                try {
                    var descriptor = descriptors[i];
                    var strEffect = EffectLoader.Load(FileManager.ReadSync(descriptor), Path.GetDirectoryName(descriptor).Replace("\\", "/"));

                    if (strEffect != null) {
                        var filenameWithoutExtension = Path.GetFileNameWithoutExtension(descriptor).SanitizeForAddressables();
                        var dir = Path.GetDirectoryName(descriptor);

                        string assetPath = Path.Combine(GENERATED_RESOURCES_PATH, dir);
                        Directory.CreateDirectory(assetPath);

                        var completePath = Path.Combine(assetPath, filenameWithoutExtension + ".asset");
                        AssetDatabase.CreateAsset(strEffect, completePath);
                    }
                } catch (Exception e) {
                    Debug.LogException(e);
                }
            }
        } finally {
            AssetDatabase.StopAssetEditing();
            EditorUtility.ClearProgressBar();
            AssetDatabase.Refresh();
        }
    }

    [MenuItem("UnityRO/Utils/Extract/BGM")]
    static void ExtractBGM() {
        AssetDatabase.StartAssetEditing();

        try {
            var descriptors = GetFilesFromDir(config.BgmPath).ToList();

            for (int i = 0; i < descriptors.Count; i++) {
                var progress = i * 1f / descriptors.Count;
                if (EditorUtility.DisplayCancelableProgressBar("UnityRO", $"Extracting bgm {i} of {descriptors.Count}\t\t{progress * 100}%", progress)) {
                    break;
                }

                try {
                    var descriptor = descriptors[i];
                    var bgm = File.ReadAllBytes(descriptor);

                    if (bgm != null) {
                        var filenameWithoutExtension = Path.GetFileNameWithoutExtension(descriptor).SanitizeForAddressables();

                        string assetPath = Path.Combine(GENERATED_RESOURCES_PATH, "bgm");
                        Directory.CreateDirectory(assetPath);

                        var completePath = Path.Combine(assetPath, filenameWithoutExtension + ".mp3");
                        File.WriteAllBytes(completePath, bgm);
                    }
                } catch (Exception e) {
                    Debug.LogException(e);
                }
            }
        } finally {
            AssetDatabase.StopAssetEditing();
            EditorUtility.ClearProgressBar();
            AssetDatabase.Refresh();
        }
    }

    [MenuItem("UnityRO/Utils/Extract/Wav")]
    static void ExtractWav() {
        var descriptors = FilterDescriptors(FileManager.GetFileDescriptors(), "data/wav")
            .Where(it => Path.GetExtension(it) == ".wav")
            .ToList();

        try {
            AssetDatabase.StartAssetEditing();
            for (int i = 0; i < descriptors.Count; i++) {
                var progress = i * 1f / descriptors.Count;
                if (EditorUtility.DisplayCancelableProgressBar("UnityRO", $"Extracting wavs {i} of {descriptors.Count}\t\t{progress * 100}%", progress)) {
                    break;
                }

                var descriptor = descriptors[i];
                try {
                    var audioClip = FileManager.Load(descriptor) as AudioClip;

                    if (audioClip != null) {
                        var filenameWithoutExtension = Path.GetFileNameWithoutExtension(descriptor).SanitizeForAddressables();
                        var dir = Path.GetDirectoryName(descriptor);
                        string assetPath = Path.Combine(GENERATED_RESOURCES_PATH, dir);
                        Directory.CreateDirectory(assetPath);

                        var completePath = Path.Combine(assetPath, filenameWithoutExtension + ".wav");
                        SavWav.Save(completePath, audioClip);
                    }
                } catch (Exception e) {
                    Debug.LogError($"Failed to extract {descriptor} {e}");
                }
            }
        } finally {
            AssetDatabase.StopAssetEditing();
            EditorUtility.ClearProgressBar();
            AssetDatabase.Refresh();
        }
    }

    [MenuItem("UnityRO/1. Extract Assets")]
    static void ExtractAssets() {
        EditorApplication.ExecuteMenuItem("UnityRO/Utils/Extract/Textures");
        //This has to be done here so the models can load their textures
        CreateTexturesAddressableAssets();

        EditorApplication.ExecuteMenuItem("UnityRO/Utils/Extract/Sprites");
        EditorApplication.ExecuteMenuItem("UnityRO/Utils/Extract/Palette");
        EditorApplication.ExecuteMenuItem("UnityRO/Utils/Extract/Lua Files");
        EditorApplication.ExecuteMenuItem("UnityRO/Utils/Extract/Effects");
        EditorApplication.ExecuteMenuItem("UnityRO/Utils/Extract/Wav");
        EditorApplication.ExecuteMenuItem("UnityRO/Utils/Extract/Txt Tables");

        EditorApplication.ExecuteMenuItem("UnityRO/Utils/Prepare/Models");

        // Generate map prefabs
    }

    [MenuItem("UnityRO/2. Check for missing assets")]
    static void CheckForMissingAssets() {
        AssetDatabase.Refresh();

        var descriptors = FileManager.GetFileDescriptors();

        var textureDescriptors = FilterDescriptors(descriptors, "data/texture")
            .Select(it => {
                var dir = Path.GetDirectoryName(it);
                var filenameWithoutExtension = Path.GetFileNameWithoutExtension(it);

                return Path.Combine(GENERATED_RESOURCES_PATH, dir, filenameWithoutExtension + ".png");
            }).ToList();

        var modelDescriptors = FilterDescriptors(descriptors, "data/model")
            .Where(it => Path.GetExtension(it) == ".rsm")
            .Select(it => {
                var dir = Path.GetDirectoryName(it);
                var filenameWithoutExtension = Path.GetFileNameWithoutExtension(it);

                return Path.Combine(GENERATED_RESOURCES_PATH, dir, filenameWithoutExtension + ".prefab");
            }).ToList();

        var spriteDescriptors = FilterDescriptors(descriptors, "data/sprite")
            .Select(it => {
                var dir = Path.GetDirectoryName(it);
                var filenameWithoutExtension = Path.GetFileNameWithoutExtension(it);

                return Path.Combine(GENERATED_RESOURCES_PATH, dir, filenameWithoutExtension + ".asset");
            }).ToList();

        List<string> missingTextures = new List<string>(textureDescriptors.Count);
        Parallel.ForEach(textureDescriptors, (descriptor) => {
            if (!File.Exists(descriptor)) {
                lock (missingTextures) {
                    missingTextures.Add(descriptor);
                }
            }
        });

        File.WriteAllLines("Assets/Logs/missing-textures.txt", missingTextures);
        Debug.LogError($"{missingTextures.Count} out of {textureDescriptors.Count} textures not found. Full list saved to Assets/Logs/missing-textures.txt");

        List<string> missingModels = new List<string>(modelDescriptors.Count);
        Parallel.ForEach(modelDescriptors, descriptor => {
            if (!File.Exists(descriptor)) {
                lock (missingModels) {
                    missingModels.Add(descriptor);
                }
            }
        });

        File.WriteAllLines("Assets/Logs/missing-models.txt", missingModels);
        Debug.LogError($"{missingModels.Count} out of {modelDescriptors.Count} models not found. Full list saved to Assets/Logs/missing-models.txt");

        List<string> missingSprites = new List<string>(spriteDescriptors.Count);
        Parallel.ForEach(spriteDescriptors, (descriptor) => {
            if (!File.Exists(descriptor)) {
                lock (missingSprites) {
                    missingSprites.Add(descriptor);
                }
            }
        });
        File.WriteAllLines("Assets/Logs/missing-sprites.txt", missingModels);
        Debug.LogError($"{missingSprites.Count} out of {spriteDescriptors.Count} sprites not found. Full list saved to Assets/Logs/missing-sprites.txt");
    }

    [MenuItem("UnityRO/3. Create Addressable Assets/1. All")]
    static void CreateAllAddressableAssets() {
        // textures were already assigned to addressables when you run UnityRO/1. Extract Assets
        EditorApplication.ExecuteMenuItem("UnityRO/3. Create Addressable Assets/3. Models");
        EditorApplication.ExecuteMenuItem("UnityRO/3. Create Addressable Assets/4. Sprites");
        EditorApplication.ExecuteMenuItem("UnityRO/3. Create Addressable Assets/5. Data Tables");
        EditorApplication.ExecuteMenuItem("UnityRO/3. Create Addressable Assets/6. Effects");
        EditorApplication.ExecuteMenuItem("UnityRO/3. Create Addressable Assets/7. Wav");
        EditorApplication.ExecuteMenuItem("UnityRO/3. Create Addressable Assets/8. BGM");
    }

    [MenuItem("UnityRO/3. Create Addressable Assets/2. Textures")]
    static void CreateTexturesAddressableAssets() {
        var textures = Resources.LoadAll(Path.Join("data", "texture")).ToList();
        textures.SetAddressableGroup("Textures", "Textures");
    }

    [MenuItem("UnityRO/3. Create Addressable Assets/3. Models")]
    static void CreateModelsAddressableAssets() {
        var models = Resources.LoadAll(Path.Join("data", "model")).ToList();
        models.SetAddressableGroup("Models", "Models");
    }

    [MenuItem("UnityRO/3. Create Addressable Assets/4. Sprites")]
    static void CreateSpritesAddressableAssets() {
        var sprites = Resources.LoadAll(Path.Join("data", "sprite"))
            .Where(it => it is Texture2D || it is SpriteData) // filter out the thousands of sprites we've created
            .ToList();
        sprites.SetAddressableGroup("Sprites", "Sprites", true);
    }

    [MenuItem("UnityRO/3. Create Addressable Assets/5. Data Tables")]
    static void CreateDataTablesAddressableAssets() {
        var files = Resources.LoadAll("lua").ToList();
        files.SetAddressableGroup("DataTables", "DataTables");

        var txtTables = Resources.LoadAll("txt").ToList();
        txtTables.SetAddressableGroup("DataTables", "DataTables");
    }

    [MenuItem("UnityRO/3. Create Addressable Assets/6. Effects")]
    static void CreateEffectsAddressableAssets() {
        var files = Resources.LoadAll(Path.Combine("data", "texture", "effect"))
            .Where(it => it is STR)
            .ToList();
        files.SetAddressableGroup("Effects", "Effects");
    }

    [MenuItem("UnityRO/3. Create Addressable Assets/7. Wav")]
    static void CreateWavAddressableAssets() {
        var files = Resources.LoadAll(Path.Combine("data", "wav"))
            .Where(it => it is AudioClip)
            .ToList();
        files.SetAddressableGroup("Wav", "Wav");
    }

    [MenuItem("UnityRO/3. Create Addressable Assets/8. BGM")]
    static void CreateBGMAddressableAssets() {
        var files = Resources.LoadAll("bgm")
            .Where(it => it is AudioClip)
            .ToList();
        files.SetAddressableGroup("BGM", "BGM");
    }

    [MenuItem("UnityRO/3. Create Addressable Assets/9. Palette")]
    static void CreatePaletteAddressableAssets() {
        var files = Resources.LoadAll(Path.Combine("data", "palette"))
            .Where(it => it is Texture2D)
            .ToList();
        files.SetAddressableGroup("Palettes", "Palettes", true);
    }

    [MenuItem("UnityRO/4. Rename Generated Resources folder")]
    static void RanameGeneratedResourcesFolder() {
        /**
         * This exists because Unity will pack anything under ../Resources/..
         * So we must rename the folder to any other name other than Resources
         * (That's what the addressables system does when you drag and drop a file to it)
         */
        Directory.Move(GENERATED_RESOURCES_PATH, GENERATED_ADDRESSABLES_PATH);
        AssetDatabase.Refresh();
    }

    [MenuItem("UnityRO/Utils/Fix wav addressables")]
    static void FixWavAddressables() {
        GetFilesFromDir(Path.Combine(GENERATED_ADDRESSABLES_PATH, "data", "wav"))
            .Select(it => AssetDatabase.LoadAssetAtPath<AudioClip>(it))
            .Where(it => it != null)
            .ToList()
            .SetAddressableGroup("Wav", "Wav");
        AssetDatabase.Refresh();
    }

    [MenuItem("UnityRO/Utils/Fix effects addressables")]
    static void FixEffectsAddressables() {
        GetFilesFromDir(Path.Combine(GENERATED_ADDRESSABLES_PATH, "data", "texture", "effect"))
            .Where(it => Path.GetExtension(it) == ".asset")
            .Select(it => AssetDatabase.LoadAssetAtPath<STR>(it))
            .Where(it => it != null)
            .ToList()
            .SetAddressableGroup("Effects", "Effects");
        AssetDatabase.Refresh();
    }

    [MenuItem("UnityRO/Utils/Fix texture naming case issue")]
    static void FixTexturesCaseNaming() {
        var guidList = new List<KeyValuePair<string, string>>();
        ResourceManager.ExceptionHandler = delegate (AsyncOperationHandle handle, Exception exception) {
            if (exception is InvalidKeyException invalidKey) {
                var attemptedKey = invalidKey.Key.ToString();
                if (File.Exists(Path.Combine(GENERATED_ADDRESSABLES_PATH, attemptedKey))) {
                    var guid = AssetDatabase.AssetPathToGUID(Path.Combine(GENERATED_ADDRESSABLES_PATH, attemptedKey));
                    if (guid != null) {
                        guidList.Add(new KeyValuePair<string, string>(guid, attemptedKey));
                    }
                } else {
                    Debug.Log("Not found and doesn't exist");
                }
            }
        };

        var modelTextures = FilterDescriptors(FileManager.GetFileDescriptors(), "data/model")
            .Where(it => Path.GetExtension(it) == ".rsm")
            .Select(it => {
                try {
                    return FileManager.Load(it) as RSM;
                } catch (Exception e) {
                    return null;
                }
            })
            .Where(it => it != null)
            .SelectMany(it => it.textures)
            .Distinct()
            .Select(it => Addressables.LoadAsset<Texture2D>(Path.Combine("data", "texture", Path.ChangeExtension(it, ".png")).SanitizeForAddressables()))
            .ToList();

        var settings = AddressableAssetSettingsDefaultObject.Settings;
        for (int i = 0; i < guidList.Count; i++) {
            var progress = i / guidList.Count;
            if (EditorUtility.DisplayCancelableProgressBar("UnityRO", $"Fixing addressable texture {i + 1} of {guidList.Count}\t\t{progress * 100}%", progress)) {
                break;
            }
            var guid = guidList[i];
            var entry = settings.FindAssetEntry(guid.Key);
            entry.SetAddress(guid.Value);
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, entry, false, false);
        }

        EditorUtility.ClearProgressBar();
    }

    private static string[] GetFilesFromDir(string dir) {
        return Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories)
            .Where(it => Path.HasExtension(it) && !it.Contains(".meta"))
            .Select(it => it.Replace(Application.dataPath, "Assets"))
            .ToArray();
    }

    private static List<string> FilterDescriptors(Hashtable descriptors, string filter) {
        List<string> result = new List<string>();
        foreach (DictionaryEntry entry in descriptors) {
            string path = (entry.Key as string).Trim();
            if (path.StartsWith(filter)) {
                result.Add(path);
            }
        }

        return result;
    }
}
#endif