using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SkillWindowController : DraggableUIWindow, ISkillWindowController {

    private class SkillAllocation {
        public short skillID;
        public int level;
    }

    [SerializeField] private GridLayoutGroup GridLayout;
    [SerializeField] private ToggleGroup tabLayout;
    [SerializeField] private Toggle tabPrefab;
    [SerializeField] private TextMeshProUGUI skillPointsText;
    [SerializeField] private Material unownedSkillShader;
    [SerializeField] private Material ownedSkillShader;

    private EntityControl EntityControl;
    private List<UISkill> UISkillArray;
    private List<KeyValuePair<Skill, int>> NeededSkills = new List<KeyValuePair<Skill, int>>();
    private List<SkillAllocation> AllocatedSkillPoints = new List<SkillAllocation>();
    private Dictionary<int, Toggle> CurrentTabs = new Dictionary<int, Toggle>();
    private Toggle CurrentToggle;
    private int AvailableSkillPoints;

    private void Awake() {
        EntityControl = FindObjectOfType<EntityControl>();
    }

    // Start is called before the first frame update
    void Start() {
        InitGrid();
    }

    private void InitGrid() {
        if (UISkillArray == null) {
            UISkillArray = GridLayout.GetComponentsInChildren<UISkill>().ToList();
        };
    }

    private void ResetGrid() {
        foreach (var uis in UISkillArray) {
            uis.Reset();
            uis.SetShaders(unownedSkillShader, ownedSkillShader);
            uis.SetWindowController(this);
        }
    }

    public void UpdateSkills() {
        InitGrid();
        ResetGrid();
        ResetAllocatedPoints();
        var entity = Session.CurrentSession.Entity as Entity;
        var skillTree = entity.SkillTree;

        // Check if the current tabs are present on the current skill tree
        // we might have changed classes
        foreach (var job in CurrentTabs.Keys.Where(it => !skillTree.ClassTree.ContainsKey(it)).ToList()) {
            var tab = CurrentTabs[job];
            if (CurrentToggle == tab) {
                CurrentToggle = null;
            }
            tab.group = null;
            tabLayout.UnregisterToggle(tab);
            tab.transform.SetParent(null);
            Destroy(tab.gameObject);
            CurrentTabs.Remove(job);
        }

        // populate tabs
        foreach (var job in skillTree.ClassTree) {
            if (CurrentTabs.ContainsKey(job.Key)) {
                continue;
            }

            var tab = Instantiate(tabPrefab);
            tab.onValueChanged.AddListener(delegate {
                OnTabChanged(tab, job);
            });
            // TEMPORARY BRING-UP GUARD: JobHelper.GetJobName throws
            // KeyNotFoundException (empty job-name table, .lub bytecode
            // issue). Unhandled, this was aborting the rest of this
            // foreach - later skill tree tabs never got created.
            string tabLabel;
            try {
                tabLabel = JobHelper.GetJobName(job.Key, Session.CurrentSession.Entity.GetBaseStatus().sex);
            } catch (System.Exception) {
                tabLabel = $"Job {job.Key}";
            }
            tab
                .GetComponent<Tab>()
                .SetLabel(tabLabel);
            tab.group = tabLayout;
            // Keep the prefab's local scale: the map canvas is scaled down, so keeping the
            // world scale blew each tab up ~26x and pushed every tab after the first off screen
            tab.transform.SetParent(tabLayout.transform, false);
            tabLayout.RegisterToggle(tab);

            CurrentTabs.Add(job.Key, tab);
        }

        // Keep the tabs in tree order (novice first) when a job change adds tabs later
        var order = 0;
        foreach (var job in skillTree.ClassTree.Keys) {
            CurrentTabs[job].transform.SetSiblingIndex(order++);
        }

        // The selected tab can be gone after a job change; fall back to the first one
        if (CurrentToggle == null && CurrentTabs.Count > 0) {
            CurrentToggle = CurrentTabs[skillTree.ClassTree.Keys.First()];
        }

        if (CurrentToggle != null) {
            CurrentToggle.isOn = false;
            CurrentToggle.isOn = true;
        }
    }

    public void UpdateSkillPoints() {
        var entity = Session.CurrentSession.Entity as Entity;
        AvailableSkillPoints = (int) entity.GetBaseStatus().SkillPoints;

        skillPointsText.text = $"Skill Points: {AvailableSkillPoints}";
    }

    private void OnTabChanged(Toggle tab, KeyValuePair<int, Dictionary<int, Skill>> tree) {
        var entity = Session.CurrentSession.Entity as Entity;
        var skillTree = entity.SkillTree;

        CurrentToggle = tab;
        ResetGrid();

        foreach (var position in tree.Value.Keys) {
            UISkillArray[position]
                .SetSkill(tree.Value[position]);
        }

        foreach (var skillInfo in skillTree.OwnedSkillsInfos) {
            UISkillArray.Find(it => it.GetSkillID() == skillInfo.SkillID)?.SetSkillInfo(skillInfo);
        }
    }

    public void HighlightSkill(short skillID, int level) {
        UISkillArray
            .Find(it => it.GetSkillID() == skillID)
            .Highlight(level);
    }

    public bool HasRequiredSkill(short skillID, short level) {
        var entity = Session.CurrentSession.Entity as Entity;
        return entity.SkillTree.HasSkill(skillID, level);
    }

    public void CheckSkillRequirements(short skillID, bool isTraversing = false) {
        if (!isTraversing) {
            NeededSkills = new List<KeyValuePair<Skill, int>>();
        }

        Skill skill = SkillTable.Skills[skillID];

        if (skill.NeededSkillList == null) {
            if (NeededSkills.Count > 0) {
                NeededSkills.Reverse();
            }
            return;
        }

        foreach (var neededSkillDict in skill.NeededSkillList) {
            // Do we have the needed skill?
            if (HasRequiredSkill((short) neededSkillDict.Key, (short) neededSkillDict.Value)) {
                // Yes, just highlight it
                HighlightSkill(skillID, 0);
            } else {
                // No, recursevely lookup all the needed skills to acquire this one
                var neededSkill = SkillTable.Skills[(short) neededSkillDict.Key];
                Debug.Log($"Skill {neededSkill.SkillName}({neededSkillDict.Value}) is needed");
                HighlightSkill(neededSkill.SkillId, neededSkillDict.Value);
                CheckSkillRequirements(neededSkill.SkillId, true);
                NeededSkills.Add(new KeyValuePair<Skill, int>(neededSkill, neededSkillDict.Value));
            }
        }
    }

    public void ResetSkillRequirements() {
        NeededSkills = new List<KeyValuePair<Skill, int>>();
        UISkillArray
            .FindAll(it => it.IsHighlighted)
            .ForEach(it => it.UnHighlight());
    }

    public void ApplyAllocatedPoints() {
        var packets = new List<CZ.UPGRADE_SKILLLEVEL>();

        foreach (var allocation in AllocatedSkillPoints) {
            for (int i = 0; i < allocation.level; i++) {
                packets.Add(new CZ.UPGRADE_SKILLLEVEL {
                    SkillID = allocation.skillID
                });
            }
        }

        packets.ForEach(it => it.Send());
    }

    public void ResetAllocatedPoints() {
        AllocatedSkillPoints = new List<SkillAllocation>();
    }

    public void AllocateSkillPoints(short skillID) {
        var targetUiSkill = UISkillArray.Find(it => it.GetSkillID() == skillID);

        foreach (var skill in NeededSkills) {
            if (AllocatedSkillPoints.Any(it => it.skillID == skill.Key.SkillId)) {
                continue;
            }
            var uiSkill = UISkillArray.Find(it => it.GetSkillID() == skill.Key.SkillId);

            if (AvailableSkillPoints >= skill.Value) {
                uiSkill.AddPoints(skill.Value);

                AllocatedSkillPoints.Add(new SkillAllocation {
                    skillID = skill.Key.SkillId,
                    level = skill.Value
                });

                UpdateSkillPoints(skill.Value);
            } else {
                uiSkill.AddPoints(AvailableSkillPoints);

                AllocatedSkillPoints.Add(new SkillAllocation {
                    skillID = skill.Key.SkillId,
                    level = AvailableSkillPoints
                });

                UpdateSkillPoints(AvailableSkillPoints);
            }
        }

        if (AvailableSkillPoints > 0 && targetUiSkill.CanUpgradeCurrentSkill()) {
            targetUiSkill.AddPoints(1);
            UpdateSkillPoints(1);


            SkillAllocation allocation = AllocatedSkillPoints.Find(it => it.skillID == targetUiSkill.Skill.SkillId);
            if (allocation == null) {
                AllocatedSkillPoints.Add(new SkillAllocation {
                    skillID = targetUiSkill.Skill.SkillId,
                    level = 1
                });
            } else {
                allocation.level += 1;
            }
        }
    }

    private void UpdateSkillPoints(int value) {
        var entity = Session.CurrentSession.Entity as Entity;
        entity.GetBaseStatus().SkillPoints -= (uint) value;
        UpdateSkillPoints();
    }

    public void UseSkill(SkillInfo skillInfo, short level) {
        // Not while its shortcuts are darkened, counting down
        if (!SkillCooldowns.IsReady(skillInfo.SkillID)) {
            return;
        }
        EntityControl.UseSkill(skillInfo, level);
    }
}
