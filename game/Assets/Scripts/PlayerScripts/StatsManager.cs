using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class StatsManager : MonoBehaviour
{
    public static StatsManager Instance;
    public StatsUI statsUI;
    public TMP_Text healthText;

    [Header("Combat Stats")]
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
        // Initialize from shared config if available
        if (GameConfigLoader.Instance != null && GameConfigLoader.Instance.Config != null)
        {
            ApplyConfig(GameConfigLoader.Instance.Config.playerDefaults.stats);
        }
    }

    public void ApplyConfig(PlayerStatBlockData stats)
    {
        damage = stats.damage;
        weaponRange = stats.weaponRange;
        knockbackForce = stats.knockbackForce;
        knockbackTime = stats.knockbackTime;
        stunTime = stats.stunTime;
        speed = (int)stats.speed;
        maxHealth = stats.maxHealth;
        currentHealth = stats.currentHealth;
        bonusDamagePercent = stats.bonusDamagePercent;
        damageReductionPercent = stats.damageReductionPercent;
        UpdateHealth(0);
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
