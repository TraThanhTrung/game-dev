using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SkillManager : MonoBehaviour
{
    public Player_Combat combat;


    private void OnEnable()
    {
        SkillSlot.OnAbilityPointSpent += HandleAbilityPointSpent;
    }

    private void OnDisable()
    {
        SkillSlot.OnAbilityPointSpent -= HandleAbilityPointSpent;
    }



    private void HandleAbilityPointSpent(SkillSlot slot)
    {
        string skillName = slot.skillSO.skillName;
        switch (skillName)
        {
            case "Max Health Boost ":
                StatsManager.Instance.UpdateMaxHealth(1);
                break;

            // Combat Skills
            case "Exp Bonus":
                // Exp bonus is applied server-side when calculating exp rewards
                Debug.Log("[SkillManager] Exp Bonus skill upgraded - server will apply bonus when awarding exp");
                break;

            case "Respawn Count":
                // NOTE: Server-side implementation needed
                // Respawn count should be tracked server-side in PlayerStats or PlayerProfile
                Debug.Log("[SkillManager] Respawn Count skill upgraded - server-side implementation needed");
                break;

            // Stats Skills
            case "Speed Boost":
                StatsManager.Instance.UpdateSpeed(1);
                break;

            case "Damage Boost":
                StatsManager.Instance.AddDamage(1);
                break;

            case "Knockback Boost":
                StatsManager.Instance.AddKnockbackForceBonus(0.5f);
                break;

            default:
                Debug.LogWarning("Unknown skill: " + skillName);
                break;
        }
    }
}
