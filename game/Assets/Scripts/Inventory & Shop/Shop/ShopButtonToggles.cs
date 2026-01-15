using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShopButtonToggles : MonoBehaviour
{

    public void OpenItemShop()
    {
        // Play UI click sound effect
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayUIClick();
        }

        if(ShopKeeper.currentShopKeeper != null)
        {
            ShopKeeper.currentShopKeeper.OpenItemShop();
        }
    }

    public void OpenWeaponsShop()
    {
        // Play UI click sound effect
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayUIClick();
        }

        if (ShopKeeper.currentShopKeeper != null)
        {
            ShopKeeper.currentShopKeeper.OpenWeaponShop();
        }
    }


    public void OpenArmourShop()
    {
        // Play UI click sound effect
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayUIClick();
        }

        if (ShopKeeper.currentShopKeeper != null)
        {
            ShopKeeper.currentShopKeeper.OpenArmourShop();
        }
    }
}
