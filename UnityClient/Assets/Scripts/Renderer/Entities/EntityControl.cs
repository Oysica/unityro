using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class EntityControl : MonoBehaviour {

    private LayerMask GroundMask;
    private LayerMask EntityMask;
    private Camera MainCamera;

    private PendingAction CurrentPendingAction = new PendingAction.None();

    public Entity Entity;

    private CursorRenderer CursorRenderer;
    private GridRenderer GridRenderer;
    private PathFinder PathFinder;

    // A player the mouse went down on, waiting to be let go (a click) or held (their menu)
    private const float PRESS_MOVE_PIXELS = 12f;
    private Entity PressedPlayer;
    private float PressedAt;
    private Vector2 PressedWhere;


    private void Awake() {
        CursorRenderer = FindObjectOfType<CursorRenderer>();
        GridRenderer = FindObjectOfType<GridRenderer>();
        PathFinder = FindObjectOfType<PathFinder>();
        MainCamera = Camera.main;
    }

    void Start() {
        GroundMask = LayerMask.GetMask("Ground");
        // Characters: other players can be tapped (party invites, skills on them)
        EntityMask = LayerMask.GetMask("NPC", "Monsters", "Items", "Characters");
    }

    // Update is called once per frame
    void Update() {
        if (GridRenderer == null) {
            GridRenderer = FindObjectOfType<GridRenderer>();
        }
        if (MainCamera == null) {
            MainCamera = Camera.main;
        }
        if (PathFinder == null) {
            PathFinder = FindObjectOfType<PathFinder>();
        }

        if (ScreenInput.UsesTouch) {
            // No cursor to follow: act where the world is tapped
            foreach (var tap in ScreenInput.WorldTaps) {
                HandlePointer(tap, true);
            }
            // Held on a player: their menu (whisper, party, friends, trade)
            foreach (var press in ScreenInput.WorldLongPresses) {
                var player = PlayerUnder(press);
                if (player != null) {
                    ShowPlayerMenu(player);
                }
            }
        } else {
            var isActionRequested = Input.GetKeyDown(KeyCode.Mouse0) && !EventSystem.current.IsPointerOverGameObject();
            // A player pressed on waits: let go soon it's a click (locking on), held it's their menu
            if (isActionRequested && !(CurrentPendingAction is PendingAction.TargetSelection)) {
                var player = PlayerUnder(Input.mousePosition);
                if (player != null) {
                    PressedPlayer = player;
                    PressedAt = Time.unscaledTime;
                    PressedWhere = Input.mousePosition;
                    isActionRequested = false;
                }
            }
            HandlePointer(Input.mousePosition, isActionRequested);
            HandlePlayerPress();

            // A right click lets a skill waiting for its target go, as in the official client
            if (Input.GetKeyDown(KeyCode.Mouse1) && !EventSystem.current.IsPointerOverGameObject()) {
                EndTargetSelection();
            }
        }

        ProcessInput();
    }

    private void OnDisable() {
        EndTargetSelection();
    }

    private void HandlePointer(Vector2 screenPosition, bool isActionRequested) {
        var ray = MainCamera.ScreenPointToRay(screenPosition);

        // A skill waiting for where it goes takes the click
        if (CurrentPendingAction is PendingAction.TargetSelection selection) {
            HandleTargetSelection(selection, ray, isActionRequested);
            return;
        }

        var didHitAnything = Physics.Raycast(ray, out var hit, 150, EntityMask | GroundMask);

        // Our own character is under the pointer a lot: what's meant then is the ground under it
        if (didHitAnything && IsSelf(hit)) {
            didHitAnything = Physics.Raycast(ray, out hit, 150, GroundMask);
        }

        if (!didHitAnything) {
            return;
        }

        hit.collider.gameObject.TryGetComponent<SpriteEntityViewer>(out var target);

        if (target != null) {
            if (CurrentPendingAction is PendingAction.None) {
                switch (target.Entity.Type) {
                    case EntityType.NPC:
                        CursorRenderer.SetAction(CursorAction.TALK, false);
                        break;
                    case EntityType.ITEM:
                        CursorRenderer.SetAction(CursorAction.PICK, true);
                        break;
                    case EntityType.MOB:
                        CursorRenderer.SetAction(CursorAction.ATTACK, false);
                        break;
                    case EntityType.PC when MapRules.IsEnemy(target.Entity):
                        CursorRenderer.SetAction(CursorAction.ATTACK, false);
                        break;
                    case EntityType.WARP:
                        CursorRenderer.SetAction(CursorAction.WARP, false);
                        break;
                }
            }

            if (isActionRequested) {
                ProcessEntityClick(target.Entity);
            }
        } else {
            CursorRenderer.SetAction(CursorAction.DEFAULT, true);

            if (!GridRenderer.IsCurrentPositionValid) {
                CursorRenderer.SetAction(CursorAction.INVALID, false);
            }
            if (isActionRequested) {
                Entity.RequestMove(Mathf.FloorToInt(hit.point.x), Mathf.FloorToInt(hit.point.z), 0);
            }
        }
    }

    private void ProcessInput() {
        if (Event.current == null)
            return;

        if (!Event.current.isKey || Event.current.keyCode == KeyCode.None)
            return;

        switch (Event.current.type) {
            case EventType.KeyUp:
                switch(Event.current.keyCode) {
                    case KeyCode.Insert:
                        RequestSitStand();
                        break;
                    default:
                        break;
                };
                break;
            default:
                break;
        }

        if (Event.current.keyCode == KeyCode.Insert) {
            
        }
    }

    public void RequestSitStand() {
        new CZ.REQUEST_ACT2() {
            action = Entity.EntityViewer.State == SpriteState.Sit ? EntityActionType.STAND : EntityActionType.SIT,
            TargetID = Entity.GID
        }.Send();
    }

    private bool IsSelf(RaycastHit hit) {
        return hit.collider != null && hit.collider.TryGetComponent<SpriteEntityViewer>(out var viewer) && viewer.Entity == Entity;
    }

    /// <summary>
    /// Another player right under the pointer (not behind something else), or null
    /// </summary>
    private Entity PlayerUnder(Vector2 screenPosition) {
        var ray = MainCamera.ScreenPointToRay(screenPosition);
        if (!Physics.Raycast(ray, out var hit, 150, EntityMask | GroundMask) || IsSelf(hit)) {
            return null;
        }
        return hit.collider.TryGetComponent<SpriteEntityViewer>(out var viewer)
            && viewer.Entity != null
            && viewer.Entity.Type == EntityType.PC
            ? viewer.Entity
            : null;
    }

    /// <summary>
    /// A mouse press on a player: let go soon, a click (locking on, attacking an enemy); held,
    /// their menu; dragged away, nothing
    /// </summary>
    private void HandlePlayerPress() {
        if (PressedPlayer == null) {
            return;
        }

        var player = PressedPlayer;
        if (((Vector2) Input.mousePosition - PressedWhere).magnitude > PRESS_MOVE_PIXELS) {
            PressedPlayer = null;
        } else if (Time.unscaledTime - PressedAt >= ScreenInput.LONG_PRESS_SECONDS) {
            PressedPlayer = null;
            ShowPlayerMenu(player);
        } else if (!Input.GetKey(KeyCode.Mouse0)) {
            PressedPlayer = null;
            ProcessEntityClick(player);
        }
    }

    private void ShowPlayerMenu(Entity player) {
        if (PartyWindow.Instance != null) {
            PartyWindow.Instance.ShowPlayerMenu(player);
        }
    }

    private void ProcessEntityClick(Entity target) {
        switch (target.Type) {
            case EntityType.PC:
                if (target == Entity) {
                    break;
                }
                // A skill waiting for its target goes on them (Heal, Blessing, ...)
                if (CurrentPendingAction is PendingAction.TargetSelection) {
                    goto case EntityType.MOB;
                }
                // Locked on, as a monster is (support skills go on them), and attacked where
                // players fight; their menu is a long press
                MobileControls.Target = target;
                if (MapRules.IsEnemy(target)) {
                    goto case EntityType.MOB;
                }
                break;
            case EntityType.NPC:
                new CZ.CONTACTNPC() {
                    NAID = target.AID,
                    Type = 1
                }.Send();
                break;
            case EntityType.ITEM:
                CursorRenderer.SetAction(CursorAction.PICK, false, 2);

                OutPacket pickPacket = new CZ.ITEM_PICKUP2() { ID = (int) target.AID };
                // Compare on the ground plane: dropped items sit above the cell while their drop animation plays
                var toItem = target.transform.position - transform.position;
                if (new Vector2(toItem.x, toItem.z).magnitude > 2) {
                    Entity.AfterMoveAction = delegate {
                        pickPacket.Send();
                    };

                    new CZ.REQUEST_MOVE2() {
                        x = (short) target.transform.position.x,
                        y = (short) target.transform.position.z,
                        dir = 0
                    }.Send();

                    break;
                }

                pickPacket.Send();
                Entity.LookTo(target.transform.position);
                break;
            case EntityType.MOB:
                // TODO render lock arrow
                // What the attack button and attack skills go for on a touch screen
                if (target.Type == EntityType.MOB) {
                    MobileControls.Target = target;
                }

                List<PathNode> path;
                OutPacket actionPacket;

                if (CurrentPendingAction is PendingAction.TargetSelection TargetSelection) {
                    path = PathFinder.GetPath(Entity.transform.position, target.transform.position, TargetSelection.SkillInfo.AttackRange + 1);
                    actionPacket = new CZ.USE_SKILL2() {
                        SkillId = TargetSelection.SkillInfo.SkillID,
                        SelectedLevel = TargetSelection.Level,
                        TargetId = (int) target.AID
                    };
                    // Chosen: the next click is a click again, even while walking into range
                    EndTargetSelection();
                } else {
                    path = PathFinder.GetPath(Entity.transform.position, target.transform.position, Entity.GetBaseStatus().attackRange + 1);
                    actionPacket = new CZ.REQUEST_ACT2() {
                        TargetID = target.AID,
                        action = EntityActionType.CONTINUOUS_ATTACK
                    };
                }

                Action actionDelegate = delegate {
                    actionPacket.Send();
                };

                if (path.Count == 0) {
                    // Out of reach
                    return;
                } else if (path.Count <= 1) {
                    actionDelegate.Invoke();
                } else {
                    PathNode endNode = path[path.Count - 1];

                    Entity.AfterMoveAction = actionDelegate;

                    new CZ.REQUEST_MOVE2() {
                        x = (short) endNode.x,
                        y = (short) endNode.z,
                        dir = (byte) Entity.Direction
                    }.Send();
                }

                break;
            case EntityType.WARP:
                break;
        }

    }

    internal void UseSkill(SkillInfo skillInfo, short level) {
        var type = skillInfo.SkillType;
        if ((type & (int) SkillTargetType.Self) > 0) {
            EndTargetSelection();
            new CZ.USE_SKILL2() {
                SkillId = skillInfo.SkillID,
                SelectedLevel = level,
                TargetId = (int) Entity.GID
            }.Send();
        }

        if ((type & (int) SkillTargetType.Target) > 0) {
            // The same skill pressed again while it waits: a support skill goes on ourselves
            // (Heal, Blessing, ...), anything else is let go
            if (CurrentPendingAction is PendingAction.TargetSelection waiting && waiting.SkillInfo.SkillID == skillInfo.SkillID) {
                EndTargetSelection();
                if ((type & (int) SkillTargetType.Friend) > 0) {
                    UseSkillOnSelf(skillInfo, level);
                }
                return;
            }

            // A touch screen has no cursor to aim with: attack and ground skills go where the
            // attack button would
            if (MobileControls.Enabled && UseSkillOnAutomaticTarget(skillInfo, level)) {
                return;
            }
            BeginTargetSelection(skillInfo, level);
        }
    }

    /// <summary>
    /// A support skill waiting for its target, used on a player picked elsewhere (the party
    /// list): walking to them when in sight, else the server says whether they're in reach
    /// </summary>
    public bool UseSelectedSkillOnPlayer(uint accountId) {
        if (!(CurrentPendingAction is PendingAction.TargetSelection selection) || (selection.SkillInfo.SkillType & (int) SkillTargetType.Friend) == 0) {
            return false;
        }

        var entityManager = FindObjectOfType<EntityManager>();
        var target = entityManager != null ? entityManager.FindEntity(accountId) : null;
        if (target != null) {
            UseSelectedSkillOn(selection, target);
        } else {
            EndTargetSelection();
            new CZ.USE_SKILL2() {
                SkillId = selection.SkillInfo.SkillID,
                SelectedLevel = selection.Level,
                TargetId = (int) accountId
            }.Send();
        }
        return true;
    }

    /// <summary>
    /// Where a skill waiting for its target goes: on who's clicked if it may go on them (support
    /// skills on ourselves too), on the ground for ground skills; anywhere else lets it go
    /// </summary>
    private void HandleTargetSelection(PendingAction.TargetSelection selection, Ray ray, bool isActionRequested) {
        CursorRenderer.SetAction(CursorAction.TARGET, false);
        if (!isActionRequested) {
            return;
        }

        var type = selection.SkillInfo.SkillType;
        var target = TargetUnder(ray, type);
        if (target != null) {
            UseSelectedSkillOn(selection, target);
        } else if ((type & (int) SkillTargetType.Place) > 0 && Physics.Raycast(ray, out var ground, 150, GroundMask)) {
            EndTargetSelection();
            // The cell clicked, as walking takes it
            UseSkillAt(selection.SkillInfo, selection.Level, new Vector3(Mathf.Floor(ground.point.x), 0f, Mathf.Floor(ground.point.z)));
        } else {
            EndTargetSelection();
        }
    }

    /// <summary>
    /// The nearest one under the pointer the skill may go on: monsters, other players, and
    /// ourselves for support skills and ground skills
    /// </summary>
    private Entity TargetUnder(Ray ray, int type) {
        var hits = Physics.RaycastAll(ray, 150, EntityMask);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var hit in hits) {
            if (!hit.collider.TryGetComponent<SpriteEntityViewer>(out var viewer) || viewer.Entity == null) {
                continue;
            }

            var entity = viewer.Entity;
            if (entity.Type == EntityType.MOB || (entity.Type == EntityType.PC && entity != Entity)) {
                return entity;
            }
            if (entity == Entity && (type & (int) (SkillTargetType.Friend | SkillTargetType.Place)) > 0) {
                return entity;
            }
        }
        return null;
    }

    private void UseSelectedSkillOn(PendingAction.TargetSelection selection, Entity target) {
        var skillInfo = selection.SkillInfo;
        var type = skillInfo.SkillType;
        EndTargetSelection();

        if ((type & (int) (SkillTargetType.Enemy | SkillTargetType.Friend)) == 0) {
            // A ground skill clicked on someone goes on the cell they stand on
            UseSkillAt(skillInfo, selection.Level, target.transform.position);
        } else if (target == Entity) {
            UseSkillOnSelf(skillInfo, selection.Level);
        } else {
            UseSkillOn(skillInfo, selection.Level, target);
        }
    }

    private void UseSkillOnSelf(SkillInfo skillInfo, short level) {
        // The server knows us by account id, as it does every player on the map
        new CZ.USE_SKILL2() {
            SkillId = skillInfo.SkillID,
            SelectedLevel = level,
            TargetId = (int) Session.CurrentSession.AccountID
        }.Send();
    }

    private void BeginTargetSelection(SkillInfo skillInfo, short level) {
        CurrentPendingAction = new PendingAction.TargetSelection(skillInfo, level);
        SkillTargetHint.Show(TargetHintText(skillInfo));
    }

    private void EndTargetSelection() {
        if (CurrentPendingAction is PendingAction.TargetSelection) {
            CurrentPendingAction = new PendingAction.None();
        }
        SkillTargetHint.Hide();
    }

    private static string TargetHintText(SkillInfo skillInfo) {
        // The client's names end in a space
        var name = (SkillTable.Skills.TryGetValue(skillInfo.SkillID, out var skill) && !string.IsNullOrWhiteSpace(skill.SkillName)
            ? skill.SkillName
            : skillInfo.SkillName ?? "").Trim();
        var type = skillInfo.SkillType;
        var mouse = !ScreenInput.UsesTouch;

        if ((type & (int) SkillTargetType.Friend) > 0) {
            return $"【{name}】點自己、其他玩家、隊伍名單或怪物　再按一次技能：對自己施放　{(mouse ? "點空地或右鍵取消" : "點空地取消")}";
        }
        if ((type & (int) SkillTargetType.Enemy) > 0) {
            return $"【{name}】點要施放的目標　{(mouse ? "點空地或右鍵取消" : "點空地取消")}";
        }
        return $"【{name}】點要施放的位置　{(mouse ? "右鍵或再按一次技能取消" : "再按一次技能取消")}";
    }

    /// <summary>
    /// Attacks a monster, walking into range first
    /// </summary>
    public void Attack(Entity target) {
        EndTargetSelection();
        ProcessEntityClick(target);
    }

    /// <summary>
    /// Picks an item up, walking to it first
    /// </summary>
    public void PickUp(Entity item) {
        ProcessEntityClick(item);
    }

    /// <summary>
    /// Uses a skill on a monster, walking into the skill's range first
    /// </summary>
    public void UseSkillOn(SkillInfo skillInfo, short level, Entity target) {
        CurrentPendingAction = new PendingAction.TargetSelection(skillInfo, level);
        ProcessEntityClick(target);
    }

    /// <summary>
    /// Uses a skill on a cell, walking into the skill's range first
    /// </summary>
    public void UseSkillAt(SkillInfo skillInfo, short level, Vector3 position) {
        var x = Mathf.RoundToInt(position.x);
        var y = Mathf.RoundToInt(position.z);
        OutPacket packet = new CZ.USE_SKILL_TOGROUND2(skillInfo.SkillID, level, x, y);
        // Its magic circle is as wide as this level reaches
        GroundCastCircle.Remember(skillInfo.SkillID, level);

        var cell = Entity.transform.position;
        var path = x == Mathf.RoundToInt(cell.x) && y == Mathf.RoundToInt(cell.z)
            ? new List<PathNode>()
            : PathFinder.GetPath(cell, new Vector3(x, 0, y), skillInfo.AttackRange + 1);
        if (path.Count <= 1) {
            // In range, or no way there: the server says whether it can be used
            packet.Send();
            return;
        }

        var endNode = path[path.Count - 1];
        Entity.AfterMoveAction = delegate {
            packet.Send();
        };
        new CZ.REQUEST_MOVE2() {
            x = (short) endNode.x,
            y = (short) endNode.z,
            dir = (byte) Entity.Direction
        }.Send();
    }

    private bool UseSkillOnAutomaticTarget(SkillInfo skillInfo, short level) {
        var type = skillInfo.SkillType;

        if ((type & (int) SkillTargetType.Enemy) > 0) {
            var target = MobileControls.FindTarget(Entity);
            if (target == null) {
                return false;
            }
            UseSkillOn(skillInfo, level, target);
            return true;
        }

        if ((type & (int) SkillTargetType.Place) > 0) {
            // On the target, or where we stand (Pneuma, Sanctuary, ...)
            var target = MobileControls.FindTarget(Entity);
            UseSkillAt(skillInfo, level, target != null ? target.transform.position : Entity.transform.position);
            return true;
        }

        // Support skills (Heal, Blessing, ...) go on a friendly player locked on; else they wait for
        // who they go on: a player tapped, one in the party list, or ourselves pressed again
        var locked = MobileControls.Target;
        if ((type & (int) SkillTargetType.Friend) > 0 && MobileControls.IsSelected(locked, Entity) && !MapRules.IsEnemy(locked)) {
            UseSkillOn(skillInfo, level, locked);
            return true;
        }
        return false;
    }

    public partial class PendingAction {

        public class None : PendingAction { }

        public class TargetSelection : PendingAction {
            public SkillInfo SkillInfo;
            public short Level;

            public TargetSelection(SkillInfo skillInfo, short level) {
                SkillInfo = skillInfo;
                Level = level;
            }
        }

    }

}
