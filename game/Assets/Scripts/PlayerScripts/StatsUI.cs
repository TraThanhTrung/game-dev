using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class StatsUI : MonoBehaviour
{
    public GameObject[] statsSlots;
    public CanvasGroup statsCanvas;

    private bool statsOpen = false;


    private void Start()
    {
        UpdateAllStats();
    }


    private void Update()
    {
        // Skip if canvas has been destroyed
        if (statsCanvas == null) return;

        if (Input.GetButtonDown("ToggleStats"))
            if (statsOpen)
            {
                Time.timeScale = 1;
                UpdateAllStats();
                statsCanvas.alpha = 0;
                statsCanvas.blocksRaycasts = false;
                statsOpen = false;
            }
            else
            {
                Time.timeScale = 0;
                UpdateAllStats();
                statsCanvas.alpha = 1;
                statsCanvas.blocksRaycasts = true;
                statsOpen = true;
            }
    }


    public void UpdateDamage()
    {
        if (statsSlots.Length > 0 && statsSlots[0] != null)
        {
            int currentDamage = StatsManager.Instance.GetDamageWithBonus();
            statsSlots[0].GetComponentInChildren<TMP_Text>().text = "Damage: " + currentDamage;
        }
    }

    public void UpdateSpeed()
    {
        if (statsSlots.Length > 1 && statsSlots[1] != null)
        {
            statsSlots[1].GetComponentInChildren<TMP_Text>().text = "Speed: " + StatsManager.Instance.speed;
        }
    }

    public void UpdateHealth()
    {
        if (statsSlots.Length > 2 && statsSlots[2] != null)
        {
            statsSlots[2].GetComponentInChildren<TMP_Text>().text = "HP: " + StatsManager.Instance.currentHealth + " / " + StatsManager.Instance.maxHealth;
        }
    }

    public void UpdateAllStats()
    {
        UpdateDamage();
        UpdateSpeed();
        UpdateHealth();
    }
}
