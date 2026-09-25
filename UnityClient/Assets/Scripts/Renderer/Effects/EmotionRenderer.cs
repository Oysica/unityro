using Assets.Scripts.Renderer.Sprite;
using ROIO.Models.FileTypes;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// An emotion (ZC_EMOTION, ZC_RECEIVE_EMOTE) over a unit's head: one run of its action in
/// data/sprite/이팩트/emotion, drawn like the cursor draws its ACT, then gone.
/// </summary>
public class EmotionRenderer : MonoBehaviour {

    // Where roBrowser draws head attachments: 100 sprite pixels above the feet, in the plane the
    // sprites are drawn in (the entity viewer faces the camera, so this is not straight up)
    private static readonly Vector3 HEAD_OFFSET = new Vector3(0f, 100f / SPR.PIXELS_PER_UNIT, 0f);

    private static SpriteAssetLoader.LoadedSprite Loaded;
    private static UnityEngine.Sprite[] Sprites;
    private static Material SpriteMaterial;
    private static readonly Dictionary<ACT.Frame, Mesh> MeshCache = new Dictionary<ACT.Frame, Mesh>();

    private ACT.Action Action;
    private MeshFilter MeshFilter;
    private int Frame = -1;
    private float Started;

    public static void Show(Entity entity, int emotion) {
        var actionIndex = EmotionTable.GetSpriteAction(emotion);
        if (entity == null || actionIndex < 0 || !Load() || actionIndex >= Loaded.Data.act.actions.Length) {
            return;
        }

        // A new emotion replaces the one still showing
        foreach (var shown in entity.GetComponentsInChildren<EmotionRenderer>()) {
            Destroy(shown.gameObject);
        }

        var emotionObject = new GameObject("Emotion");
        emotionObject.layer = entity.gameObject.layer;
        if (entity.EntityViewer != null) {
            // Turns with the viewer's billboard
            emotionObject.transform.SetParent(entity.EntityViewer.transform, false);
        } else {
            emotionObject.transform.SetParent(entity.transform, false);
            emotionObject.AddComponent<Billboard>();
        }
        emotionObject.transform.localPosition = HEAD_OFFSET;
        emotionObject.AddComponent<SortingGroup>().sortingOrder = 10;

        var renderer = emotionObject.AddComponent<EmotionRenderer>();
        renderer.Init(Loaded.Data.act.actions[actionIndex]);
    }

    private static bool Load() {
        // Loaded again when its atlas went with an unloaded scene
        if (Loaded == null || Loaded.Atlas == null) {
            // Falls back to decoding the sprite from the GRF in the editor when it wasn't extracted
            Loaded = SpriteAssetLoader.Load(EmotionTable.SPRITE_PATH);
            if (Loaded == null) {
                return false;
            }
            Sprites = Loaded.Data.GetSprites(Loaded.Atlas);
            SpriteMaterial = new Material(Resources.Load("Materials/Sprites/SpriteMaterial") as Material) {
                mainTexture = Loaded.Atlas
            };
            if (Loaded.Palette != null) {
                SpriteMaterial.SetTexture("_PaletteTex", Loaded.Palette);
            }
            foreach (var mesh in MeshCache.Values) {
                Destroy(mesh);
            }
            MeshCache.Clear();
        }
        return true;
    }

    private void Init(ACT.Action action) {
        Action = action;
        Started = Time.time;

        MeshFilter = gameObject.AddComponent<MeshFilter>();
        var meshRenderer = gameObject.AddComponent<MeshRenderer>();
        meshRenderer.receiveShadows = false;
        meshRenderer.lightProbeUsage = LightProbeUsage.Off;
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.sharedMaterial = SpriteMaterial;
        Update();
    }

    private void Update() {
        // Same pace as the cursor's ACT (CursorRenderer): delay per frame in ms, a bit slower
        var frame = (int) ((Time.time - Started) * 1000f / (Action.delay * 1.15f));
        if (frame >= Action.frames.Length) {
            Destroy(gameObject);
            return;
        }
        if (frame == Frame) {
            return;
        }

        Frame = frame;
        var actFrame = Action.frames[frame];
        if (!MeshCache.TryGetValue(actFrame, out var mesh)) {
            mesh = SpriteMeshBuilder.BuildSpriteMesh(actFrame, Sprites);
            MeshCache[actFrame] = mesh;
        }
        MeshFilter.sharedMesh = mesh;
    }
}
