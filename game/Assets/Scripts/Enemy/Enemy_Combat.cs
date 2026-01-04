using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Enemy_Combat : MonoBehaviour
{
    public int damage = 1;
    public Transform attackPoint;
    public float weaponRange;
    public float knockbackForce;
    public float stunTime;
    public LayerMask playerLayer;




    public void Attack()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(attackPoint.position, weaponRange, playerLayer);

        if (hits.Length > 0)
        {
            var playerGO = hits[0].gameObject;
            hits[0].GetComponent<PlayerHealth>().ChangeHealth(-damage);

            // Only apply knockback if player is still active (not dead)
            if (playerGO.activeInHierarchy)
            {
                hits[0].GetComponent<PlayerMovement>().Knockback(transform, knockbackForce, stunTime);
            }
        }
    }
}
