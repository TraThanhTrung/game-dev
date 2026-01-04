using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;


public class PlayerHealth : MonoBehaviour
{

    public TMP_Text healthText;
    public Animator healthTextAnim;


    private void Start()
    {
        healthText.text = "HP: " + StatsManager.Instance.currentHealth + " / " + StatsManager.Instance.maxHealth;
    }

    public void ChangeHealth(int amount)
    {
        int finalAmount = amount;
        if (amount < 0)
        {
            int incomingDamage = Mathf.Abs(amount);
            int reducedDamage = StatsManager.Instance.CalculateDamageTaken(incomingDamage);
            finalAmount = -reducedDamage;
        }

        StatsManager.Instance.currentHealth += finalAmount;
        healthTextAnim.Play("TextUpdate");

        healthText.text = "HP: " + StatsManager.Instance.currentHealth + " / " + StatsManager.Instance.maxHealth;

        if (StatsManager.Instance.currentHealth <= 0)
        {
            gameObject.SetActive(false);
        }
    }
}
