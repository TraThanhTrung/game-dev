using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class SkillSlot : MonoBehaviour
{
    public List<SkillSlot> prerequisiteSkillSlots;
    public SkillSO skillSO;

    public int currentLevel;
    public bool isUnlocked;

    public Image skillIcon;
    public Button skillButton;
    public TMP_Text skillLevelText;

    public static event Action<SkillSlot> OnAbilityPointSpent;
    public static event Action<SkillSlot> OnSkillMaxed;


    private void OnValidate()
    {
        if (skillSO != null && skillLevelText != null)
        {
            UpdateUI();
        }
    }



    public void TryUpgradeSkill()
    {
        if (isUnlocked && currentLevel < skillSO.maxLevel)
        {
            // Call server API to upgrade skill
            if (NetClient.Instance != null && NetClient.Instance.IsConnected)
            {
                StartCoroutine(UpgradeSkillOnServer());
            }
            else
            {
                // Offline mode: upgrade locally only
                ApplyLocalUpgrade();
            }
        }
    }

    private IEnumerator UpgradeSkillOnServer()
    {
        if (skillSO == null || string.IsNullOrEmpty(skillSO.skillName))
        {
            Debug.LogError("[SkillSlot] Cannot upgrade skill: skillSO or skillName is null");
            yield break;
        }

        yield return NetClient.Instance.UpgradeSkill(
            skillSO.skillName,
            response =>
            {
                if (response.success)
                {
                    ApplyLocalUpgrade();
                    Debug.Log($"[SkillSlot] Skill {skillSO.skillName} upgraded to level {response.level}");
                }
                else
                {
                    Debug.LogWarning($"[SkillSlot] Skill upgrade failed: {response.message}");
                }
            },
            error =>
            {
                Debug.LogError($"[SkillSlot] Skill upgrade API error: {error}");
            }
        );
    }

    private void ApplyLocalUpgrade()
    {
        OnAbilityPointSpent?.Invoke(this);
        currentLevel++;
        if (currentLevel >= skillSO.maxLevel)
        {
            OnSkillMaxed?.Invoke(this);
        }
        UpdateUI();
    }


    public bool CanUnlockSkill()
    {
        foreach (SkillSlot slot in prerequisiteSkillSlots)
        {
            if (!slot.isUnlocked || slot.currentLevel < slot.skillSO.maxLevel)
            {
                return false;
            }
        }
        return true;
    }



    public void Unlock()
    {
        isUnlocked = true;
        UpdateUI();
    }


    public void SetLevel(int level)
    {
        currentLevel = level;
        UpdateUI();
    }

    private void UpdateUI()
    {
        skillIcon.sprite = skillSO.skillIcon;

        if (isUnlocked)
        {
            skillButton.interactable = true;
            skillLevelText.text = currentLevel.ToString() + "/" + skillSO.maxLevel.ToString();
            skillIcon.color = Color.white;
        }
        else
        {
            skillButton.interactable = false;
            skillLevelText.text = "Locked";
            skillIcon.color = Color.grey;
        }
    }
}
