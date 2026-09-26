using Assets.Scripts.Renderer.Sprite;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Rendering;
using static ZC.NOTIFY_VANISH;

public class EntityManager : MonoBehaviour {

    private GameObject EntityCanvasPrefab;
    private Dictionary<uint, Entity> entityCache = new Dictionary<uint, Entity>();

    private void Awake() {
        DontDestroyOnLoad(this);
        EntityCanvasPrefab = Resources.Load("Prefabs/UI/EntityCanvas") as GameObject;
    }

    public Entity Spawn(EntitySpawnData data) {
        if (data.objecttype != EntityType.PC && data.objecttype != EntityType.NPC && data.objecttype != EntityType.MOB) {
            return null;
        }

        // Known already: it's where the server says now. Not ?. / ??: a destroyed one isn't null to them
        if (entityCache.TryGetValue(data.AID, out var known)) {
            if (known != null) {
                known.gameObject.SetActive(true);
                known.Respawn(data);
                return known;
            }
            entityCache.Remove(data.AID);
        }

        switch (data.objecttype) {
            case EntityType.PC:
                return SpawnPC(data);
            case EntityType.NPC:
                return SpawnNPC(data);
            default:
                return SpawnMOB(data);
        }
    }

    public void RemoveEntity(uint AID) {
        entityCache.TryGetValue(AID, out Entity entity);
        if (entity != null) {
            Destroy(entity.gameObject);
            entityCache.Remove(AID);
        }
    }

    /// <summary>
    /// Like GetEntity, for units that may well not be in sight (party members on other maps)
    /// </summary>
    public Entity FindEntity(uint AID) {
        if (entityCache.TryGetValue(AID, out var entity) && entity != null) {
            return entity;
        }
        var self = Session.CurrentSession?.Entity as Entity;
        return self != null && (self.GetEntityGID() == AID || Session.CurrentSession.AccountID == AID) ? self : null;
    }

    public Entity GetEntity(uint AID) {
        var hasFound = entityCache.TryGetValue(AID, out var entity);
        if (hasFound) {
            return entity;
        } else if (Session.CurrentSession.Entity.GetEntityGID() == AID || Session.CurrentSession.AccountID == AID) {
            return Session.CurrentSession.Entity as Entity;
        } else {
            Debug.LogError($"No Entity found for given ID: {AID}");
            return null;
        }
    }

    public void VanishEntity(uint AID, VanishType type) {
        var entity = GetEntity(AID);
        if (entity == null) {
            entityCache.Remove(AID);
            return;
        }

        entity.Vanish(type);
        // A player who died lies there until it gets up, leaves or goes out of sight: it must still
        // be found then, or its body stays on screen for good
        if (type == VanishType.DIED && entity.Type == EntityType.PC) {
            return;
        }
        entityCache.Remove(AID);
    }

    public Entity SpawnItem(ItemSpawnInfo itemSpawnInfo) {

        Item item = DBManager.GetItem(itemSpawnInfo.AID);
        string itemPath = DBManager.GetItemPath(itemSpawnInfo.AID, itemSpawnInfo.IsIdentified);

        // Falls back to decoding the sprite from the GRF in the editor when it wasn't extracted
        var sprite = SpriteAssetLoader.Load(itemPath, itemPath.SanitizeForAddressables());

        var itemGO = new GameObject(item.identifiedDisplayName);
        itemGO.layer = LayerMask.NameToLayer("Items");
        itemGO.transform.localScale = Vector3.one;
        itemGO.transform.localPosition = itemSpawnInfo.Position;
        var entity = itemGO.AddComponent<Entity>();

        var body = new GameObject("Body");
        body.layer = LayerMask.NameToLayer("Items");
        body.transform.SetParent(itemGO.transform, false);
        body.transform.localPosition = new Vector3(0f, 0.4f, 0f);
        body.AddComponent<Billboard>();
        body.AddComponent<SortingGroup>();

        if (itemSpawnInfo.animate) {
            var animator = body.AddComponent<Animator>();
            animator.runtimeAnimatorController = Instantiate(Resources.Load("Animations/ItemDropAnimator")) as RuntimeAnimatorController;
        }

        var bodyViewer = body.AddComponent<SpriteEntityViewer>();

        entity.EntityViewer = bodyViewer;
        entity.Type = EntityType.ITEM;
        entity.ShadowSize = 0.5f;

        bodyViewer.ViewerType = ViewerType.BODY;
        bodyViewer.Entity = entity;
        bodyViewer.HeadDirection = 0;

        // Register before loading visuals: a missing item sprite used to throw here and
        // leave the drop with AID 0, so it could be neither picked up nor removed
        entity.AID = (uint) itemSpawnInfo.mapID;
        entityCache.Add(entity.AID, entity);
        if (sprite != null) {
            bodyViewer.SetPalette(sprite.Palette);
            entity.Init(sprite.Data, sprite.Atlas);
        }
        entity.SetReady(true);

        return entity;
    }

    private Entity SpawnPC(EntitySpawnData data) {
        var player = new GameObject(data.name);
        var canvas = Instantiate(EntityCanvasPrefab, player.transform).GetComponent<EntityCanvas>();
        var layer = LayerMask.NameToLayer("Characters");
        player.layer = layer;
        player.transform.localScale = Vector3.one;

        var entity = player.AddComponent<Entity>();
        entity.EntityViewerType = DBManager.GetEntityViewerType(data.job);
        entityCache.Add(data.AID, entity);
        entity.Init(data, layer, canvas);

        return entity;
    }

    private Entity SpawnNPC(EntitySpawnData data) {
        var npc = new GameObject(data.name);
        var canvas = Instantiate(EntityCanvasPrefab, npc.transform).GetComponent<EntityCanvas>();
        var layer = LayerMask.NameToLayer("NPC");
        npc.layer = layer;
        npc.transform.localScale = Vector3.one;
        var entity = npc.AddComponent<Entity>();
        entity.EntityViewerType = DBManager.GetEntityViewerType(data.job);

        entityCache.Add(data.AID, entity);
        entity.Init(data, layer, canvas);

        return entity;
    }

    private Entity SpawnMOB(EntitySpawnData data) {
        var mob = new GameObject(data.name);
        var canvas = Instantiate(EntityCanvasPrefab, mob.transform).GetComponent<EntityCanvas>();
        var layer = LayerMask.NameToLayer("Monsters");
        mob.layer = layer;
        mob.transform.localScale = Vector3.one;
        var entity = mob.AddComponent<Entity>();
        entity.EntityViewerType = DBManager.GetEntityViewerType(data.job);

        entityCache.Add(data.AID, entity);
        entity.Init(data, layer, canvas);

        return entity;
    }

    public Entity SpawnPlayer(CharacterData data) {
        var player = new GameObject(data.Name);
        var canvas = Instantiate(EntityCanvasPrefab, player.transform).GetComponent<EntityCanvas>();
        var layer = LayerMask.NameToLayer("Characters");
        player.layer = layer;
        player.transform.localScale = Vector3.one;
        var entity = player.AddComponent<Entity>();
        entity.EntityViewerType = DBManager.GetEntityViewerType(data.Job);
        entity.Init(data, layer, canvas);

        var controller = player.AddComponent<EntityControl>();
        controller.Entity = entity;

        return entity;
    }

    public void ClearEntities() {
        // Skills left on the ground go with the rest; the server sends those around the new place
        SkillUnitManager.Clear();

        // Entities of a map scene that was already unloaded are gone; the cache must not hand them out again
        foreach (var entity in entityCache.Values.Where(it => it != null)) {
            Destroy(entity.gameObject);
        }
        entityCache.Clear();

        // And any the cache lost track of, which would otherwise follow us to the next map
        var self = Session.CurrentSession?.Entity as Entity;
        foreach (var entity in FindObjectsOfType<Entity>()) {
            if (entity != self && entity.AID != 0) {
                Destroy(entity.gameObject);
            }
        }
    }
}
