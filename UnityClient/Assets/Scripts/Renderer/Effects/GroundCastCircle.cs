using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The magic circle on the ground where a skill is being cast (Storm Gust, Magnus Exorcismus,
/// Sanctuary, ...), from the start of the cast until it goes off, as the official client draws it
/// (data/texture/effect/magic_target.tga). It lies over the ground, turning once as the cast goes,
/// its hand like a clock's
/// </summary>
public class GroundCastCircle : MonoBehaviour {

    private const string TEXTURE = "data/texture/effect/magic_target.tga";
    private const float STEP = 0.5f;     // how finely it follows the ground
    private const float LIFT = 0.05f;    // over the ground, not in it
    private const float FADE = 0.2f;     // seconds

    private static readonly Dictionary<uint, GroundCastCircle> ByCaster = new Dictionary<uint, GroundCastCircle>();
    // The levels of our own ground skills as last cast: the server doesn't say
    private static readonly Dictionary<int, int> CastLevels = new Dictionary<int, int>();
    private static Material Material;

    private uint Caster;
    private float Radius;
    private float Started;
    private float Seconds;
    private int Size;
    private Mesh Mesh;
    private Vector2[] Uvs;
    private Color[] Colors;

    /// <summary>
    /// The level we cast a ground skill at, for the size of its circle
    /// </summary>
    public static void Remember(int skillId, int level) {
        CastLevels[skillId] = level;
    }

    /// <param name="caster">Who casts it: a new cast of theirs takes the place of this one</param>
    /// <param name="x">The cell cast on</param>
    public static void Show(uint caster, int skillId, int x, int y, float seconds) {
        Hide(caster);
        if (seconds <= 0f) {
            return;
        }

        // As wide as the skill reaches (skill_db), at the level cast when it's ours; others' at its widest
        var mine = Session.CurrentSession != null && caster == (uint) Session.CurrentSession.AccountID;
        var level = mine && CastLevels.TryGetValue(skillId, out var cast) ? cast : 0;
        var radius = SkillAreas.Reach(skillId, level) + 0.5f;

        var pathFinder = FindObjectOfType<PathFinder>();
        var material = CircleMaterial();
        if (pathFinder == null || pathFinder.Altitude == null || material == null) {
            return;
        }

        var circle = new GameObject("Ground Cast Circle").AddComponent<GroundCastCircle>();
        circle.Caster = caster;
        circle.Radius = radius;
        circle.Seconds = seconds;
        circle.Started = Time.time;
        // The middle of the cell, as the grid under the cursor and warp portals
        circle.Build(pathFinder.Altitude, x + 0.5f, y + 0.5f, material);
        ByCaster[caster] = circle;
    }

    /// <summary>
    /// The cast was interrupted, or another one began
    /// </summary>
    public static void Hide(uint caster) {
        if (ByCaster.TryGetValue(caster, out var circle)) {
            ByCaster.Remove(caster);
            if (circle != null) {
                Destroy(circle.gameObject);
            }
        }
    }

    private static Material CircleMaterial() {
        if (Material == null) {
            var texture = TextureAssetLoader.Load(TEXTURE);
            var shader = Shader.Find("Mobile/Particles/Additive");
            if (texture == null || shader == null) {
                return null;
            }
            // Turned, the square's corners reach past the picture: its clear edge, not more runes
            texture.wrapMode = TextureWrapMode.Clamp;
            Material = new Material(shader) { mainTexture = texture };
        }
        return Material;
    }

    /// <summary>
    /// A square round the middle of the cell cast on, following the ground's height
    /// </summary>
    private void Build(Altitude altitude, float centerX, float centerZ, Material material) {
        Size = Mathf.RoundToInt(Radius * 2f / STEP) + 1;
        var points = new Vector3[Size * Size];
        Uvs = new Vector2[points.Length];
        Colors = new Color[points.Length];
        var triangles = new int[(Size - 1) * (Size - 1) * 6];

        for (var row = 0; row < Size; row++) {
            for (var column = 0; column < Size; column++) {
                var x = centerX - Radius + column * STEP;
                var z = centerZ - Radius + row * STEP;
                points[row * Size + column] = new Vector3(x, (float) altitude.GetCellHeight(x, z) + LIFT, z);
            }
        }

        var t = 0;
        for (var row = 0; row < Size - 1; row++) {
            for (var column = 0; column < Size - 1; column++) {
                var i = row * Size + column;
                triangles[t++] = i;
                triangles[t++] = i + Size;
                triangles[t++] = i + 1;
                triangles[t++] = i + 1;
                triangles[t++] = i + Size;
                triangles[t++] = i + Size + 1;
            }
        }

        Mesh = new Mesh { name = "Ground Cast Circle" };
        Mesh.vertices = points;
        Mesh.triangles = triangles;
        UpdateCircle(0f, 0f);
        Mesh.RecalculateBounds();

        gameObject.AddComponent<MeshFilter>().sharedMesh = Mesh;
        var meshRenderer = gameObject.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = material;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
    }

    private void Update() {
        var elapsed = Time.time - Started;
        if (elapsed >= Seconds) {
            Hide(Caster);
            return;
        }

        // Once round over the cast; faded in at the start and out as it goes off
        var alpha = Mathf.Min(1f, Mathf.Min(elapsed / FADE, (Seconds - elapsed) / FADE));
        UpdateCircle(elapsed / Seconds * 360f, alpha);
    }

    private void UpdateCircle(float degrees, float alpha) {
        var radians = -degrees * Mathf.Deg2Rad;
        var cos = Mathf.Cos(radians);
        var sin = Mathf.Sin(radians);
        var color = new Color(1f, 1f, 1f, alpha);

        for (var row = 0; row < Size; row++) {
            for (var column = 0; column < Size; column++) {
                var i = row * Size + column;
                // The picture turns, not the ground under it
                var u = column / (float) (Size - 1) - 0.5f;
                var v = row / (float) (Size - 1) - 0.5f;
                Uvs[i] = new Vector2(u * cos - v * sin + 0.5f, u * sin + v * cos + 0.5f);
                Colors[i] = color;
            }
        }
        Mesh.uv = Uvs;
        Mesh.colors = Colors;
    }

    private void OnDestroy() {
        if (ByCaster.TryGetValue(Caster, out var circle) && circle == this) {
            ByCaster.Remove(Caster);
        }
        if (Mesh != null) {
            Destroy(Mesh);
        }
    }
}
