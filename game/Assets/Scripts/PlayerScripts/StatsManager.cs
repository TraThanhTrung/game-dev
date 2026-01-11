using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class StatsManager : MonoBehaviour
{
    public static StatsManager Instance;
    public StatsUI statsUI;
    public TMP_Text healthText;

    [Header("Combat Stats (synced from server)")]
    public int damage;
    public float weaponRange;
    public float knockbackForce;
    public float knockbackTime;
    public float stunTime;
    [Header("Combat Modifiers")]
    public float bonusDamagePercent;
    public float damageReductionPercent;

    [Header("Movement Stats")]
    public int speed;

    [Header("Health Stats")]
    public int maxHealth;
    public int currentHealth;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    private void Start()
    {
        // Stats are synced from server via ServerStateApplier.ApplySnapshot()
        // No longer load from GameConfigLoader - all stats come from database
    }

    /// <summary>
    /// Apply all player stats from server snapshot.
    /// Called by ServerStateApplier when receiving state from server.
    /// </summary>
    public void ApplyServerStats(
        int damage, float range, float speed,
        float weaponRange, float knockbackForce, float knockbackTime, float stunTime,
        float bonusDamagePercent, float damageReductionPercent)
    {
        this.damage = damage;
        this.weaponRange = weaponRange;
        this.knockbackForce = knockbackForce;
        this.knockbackTime = knockbackTime;
        this.stunTime = stunTime;
        this.speed = (int)speed;
        this.bonusDamagePercent = bonusDamagePercent;
        this.damageReductionPercent = damageReductionPercent;

        // Update UI if available
        if (statsUI != null)
            statsUI.UpdateAllStats();
    }

    public void ApplySnapshot(int hp, int maxHp)
    {
        currentHealth = hp;
        maxHealth = maxHp;
        UpdateHealth(0);
    }

    public void UpdateMaxHealth(int amount)
    {
        maxHealth += amount;
        healthText.text = "HP: " + currentHealth + "/ " + maxHealth;
    }

    public void UpdateHealth(int amount)
    {
        currentHealth += amount;
        if (currentHealth >= maxHealth)
            currentHealth = maxHealth;

        healthText.text = "HP: " + currentHealth + "/ " + maxHealth;
    }

    public void UpdateSpeed(int amount)
    {
        speed += amount;
        statsUI.UpdateAllStats();
    }

    public int GetDamageWithBonus()
    {
        float multiplier = 1f + Mathf.Max(0f, bonusDamagePercent);
        return Mathf.Max(1, Mathf.RoundToInt(damage * multiplier));
    }

    public int CalculateDamageTaken(int rawDamage)
    {
        float reduction = Mathf.Clamp01(damageReductionPercent);
        float reducedDamage = rawDamage * (1f - reduction);
        return Mathf.Max(1, Mathf.CeilToInt(reducedDamage));
    }

    public void AddDamagePercentBonus(float amount)
    {
        bonusDamagePercent = Mathf.Max(0f, bonusDamagePercent + amount);
        statsUI.UpdateDamage();
    }

    public void AddDamageReductionPercent(float amount)
    {
        damageReductionPercent = Mathf.Clamp01(damageReductionPercent + amount);
    }

}
