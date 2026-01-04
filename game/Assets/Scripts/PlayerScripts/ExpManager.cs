using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class ExpManager : MonoBehaviour
{
    #region Events
    public static event Action<int> OnLevelUp;
    #endregion

    #region Public Fields
    public int level;
    public int currentExp;
    public int expToLevel = 10;
    public Slider expSlider;
    public TMP_Text currentLevelText;
    #endregion

    private void Start()
    {
        UpdateUI();
    }

    public void SyncFromServer(int newLevel, int newExp, int newExpToLevel)
    {
        int oldLevel = level;
        level = newLevel;
        currentExp = newExp;
        expToLevel = newExpToLevel;

        // Fire level up event if level increased
        if (level > oldLevel)
        {
            OnLevelUp?.Invoke(level - oldLevel);
        }

        UpdateUI();
    }

    public void UpdateUI()
    {
        if (expSlider != null)
        {
            expSlider.maxValue = expToLevel;
            expSlider.value = currentExp;
        }

        if (currentLevelText != null)
        {
            currentLevelText.text = "Level: " + level;
        }
    }
}
