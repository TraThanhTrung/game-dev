using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class SkillTreeManager : MonoBehaviour
{
    public SkillSlot[] skillSlots;
    public TMP_Text pointsText;
    public int availablePoints;



    private void OnEnable()
    {
        SkillSlot.OnAbilityPointSpent += HandleAbilityPointsSpent;
        SkillSlot.OnSkillMaxed += HandleSkillMaxed;
        ExpManager.OnLevelUp += UpdateAbilityPoints;
    }


    private void OnDisable()
    {
        SkillSlot.OnAbilityPointSpent -= HandleAbilityPointsSpent;
        SkillSlot.OnSkillMaxed -= HandleSkillMaxed;
        ExpManager.OnLevelUp -= UpdateAbilityPoints;
    }



    private void Start()
    {
        foreach (SkillSlot slot in skillSlots)
        {
            slot.skillButton.onClick.AddListener(() => CheckAvailablePoints(slot));
        }
        UpdateAbilityPoints(0);

        // Load skills from server when starting (for rejoin support)
        if (NetClient.Instance != null && NetClient.Instance.IsConnected)
        {
            StartCoroutine(LoadSkillsFromServer());
        }
    }

    /// <summary>
    /// Load skill levels from server and sync UI.
    /// Called when joining/rejoining session.
    /// </summary>
    public IEnumerator LoadSkillsFromServer()
    {
        if (NetClient.Instance == null || !NetClient.Instance.IsConnected)
        {
            Debug.LogWarning("[SkillTreeManager] Cannot load skills: Not connected");
            yield break;
        }

        yield return NetClient.Instance.GetSkills(
            response =>
            {
                if (response != null && response.skills != null)
                {
                    SyncSkillLevels(response.skills);
                    Debug.Log($"[SkillTreeManager] Loaded {response.skills.Count} skills from server");
                }
                else
                {
                    Debug.Log("[SkillTreeManager] No skills found on server (new session)");
                }
            },
            error =>
            {
                Debug.LogWarning($"[SkillTreeManager] Failed to load skills from server: {error}");
            }
        );
    }

    /// <summary>
    /// Sync skill levels from server to UI.
    /// </summary>
    public void SyncSkillLevels(List<SkillInfo> serverSkills)
    {
        if (serverSkills == null || skillSlots == null) return;

        // Create dictionary for quick lookup
        var skillDict = new Dictionary<string, int>();
        foreach (var skill in serverSkills)
        {
            skillDict[skill.skillId] = skill.level;
        }

        // Update each skill slot
        foreach (var slot in skillSlots)
        {
            if (slot.skillSO != null && skillDict.TryGetValue(slot.skillSO.skillName, out int level))
            {
                slot.SetLevel(level);
            }
        }
    }



    private void CheckAvailablePoints(SkillSlot slot)
    {
        if(availablePoints > 0)
        {
            slot.TryUpgradeSkill();
        }
    }


    private void HandleAbilityPointsSpent(SkillSlot skillSlot)
    {
        if(availablePoints > 0)
        {
            UpdateAbilityPoints(-1);
        }
    }


    private void HandleSkillMaxed(SkillSlot skillSlot)
    {
        foreach (SkillSlot slot in skillSlots)
        {
            if (!slot.isUnlocked && slot.CanUnlockSkill())
            {
                slot.Unlock();
            }
        }
    }



    public void UpdateAbilityPoints(int amount)
    {
        availablePoints += amount;
        pointsText.text = "Points: " + availablePoints;
    }
}
