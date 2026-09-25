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
        } else {
            var isActionRequested = Input.GetKeyDown(KeyCode.Mouse0) && !EventSystem.current.IsPointerOverGameObject();
            HandlePointer(Input.mousePosition, isActionRequested);
        }

        ProcessInput();
    }

    private void HandlePointer(Vector2 screenPosition, bool isActionRequested) {
        var ray = MainCamera.ScreenPointToRay(screenPosition);
        var didHitAnything = Physics.Raycast(ray, out var hit, 150, EntityMask | GroundMask);
        var didHitAnyEntity = Physics.Raycast(ray, out var entityHit, 150, EntityMask) && !IsSelf(entityHit);

        // Our own character is under the pointer a lot: what's meant then is the ground under it
        if (didHitAnything && IsSelf(hit)) {
            didHitAnything = Physics.Raycast(ray, out hit, 150, GroundMask);
        }

        if (isActionRequested && CurrentPendingAction is PendingAction.TargetSelection && !didHitAnyEntity) {
            CurrentPendingAction = new PendingAction.None();
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
                    case EntityType.WARP:
                        CursorRenderer.SetAction(CursorAction.WARP, false);
                        break;
                }
            }

            if (isActionRequested) {
                ProcessEntityClick(target.Entity);
            }
        } else if (CurrentPendingAction is PendingAction.TargetSelection) {
            CursorRenderer.SetAction(CursorAction.TARGET, false);
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
                if (PartyWindow.Instance != null) {
                    PartyWindow.Instance.ShowPlayerMenu(target);
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
                } else {
                    path = PathFinder.GetPath(Entity.transform.position, target.transform.position, Entity.GetBaseStatus().attackRange + 1);
                    actionPacket = new CZ.REQUEST_ACT2() {
                        TargetID = target.AID,
                        action = EntityActionType.CONTINUOUS_ATTACK
                    };
                }

                Action actionDelegate = delegate {
                    actionPacket.Send();
                    CurrentPendingAction = new PendingAction.None();
                };

                if (path.Count == 0) {
                    // Out of reach: a skill waiting for its target mustn't take the next attack
                    CurrentPendingAction = new PendingAction.None();
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
        if ((skillInfo.SkillType & (int) SkillTargetType.Self) > 0) {
            new CZ.USE_SKILL2() {
                SkillId = skillInfo.SkillID,
                SelectedLevel = level,
                TargetId = (int) Entity.GID
            }.Send();
        }

        if ((skillInfo.SkillType & (int) SkillTargetType.Target) > 0) {
            // A touch screen has no cursor to aim with: pick the target like the attack button
            if (MobileControls.Enabled && UseSkillOnAutomaticTarget(skillInfo, level)) {
                return;
            }
            CurrentPendingAction = new PendingAction.TargetSelection(skillInfo, level);
        }
    }

    /// <summary>
    /// Attacks a monster, walking into range first
    /// </summary>
    public void Attack(Entity target) {
        CurrentPendingAction = new PendingAction.None();
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

        if ((type & (int) SkillTargetType.Friend) > 0) {
            // Heal, Blessing, ... go on ourselves: the server knows us by account id, as it does
            // every player on the map
            new CZ.USE_SKILL2() {
                SkillId = skillInfo.SkillID,
                SelectedLevel = level,
                TargetId = (int) Session.CurrentSession.AccountID
            }.Send();
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
