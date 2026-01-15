using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player_Combat : MonoBehaviour
{
    public LayerMask enemyLayer;
    public Transform attackPoint;
    public StatsUI statsUI;
    public Animator anim;

    public float cooldown = 2;
    private float timer;



    private void Update()
    {
        if (timer > 0)
        {
            timer -= Time.deltaTime;
        }
    }


    public void Attack()
    {
        if (timer <= 0)
        {
            anim.SetBool("isAttacking", true);

            timer = cooldown;
        }

    }


    public void DealDamage()
    {
        // Play combat hit sound effect
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayCombatHit();
        }

        Collider2D[] enemies = Physics2D.OverlapCircleAll(attackPoint.position, StatsManager.Instance.weaponRange, enemyLayer);

        if (enemies.Length > 0)
        {
            var enemyObject = enemies[0].gameObject;
            var enemyIdentity = enemyObject.GetComponent<EnemyIdentity>();
            var enemyKnockback = enemyObject.GetComponent<Enemy_Knockback>();

            // Apply visual effects (knockback) immediately for responsive gameplay
            if (enemyKnockback != null)
            {
                enemyKnockback.Knockback(transform, StatsManager.Instance.knockbackForce, StatsManager.Instance.knockbackTime, StatsManager.Instance.stunTime);
            }

            // Report damage to server (server-authoritative HP management)
            if (enemyIdentity != null && enemyIdentity.IsInitialized && NetClient.Instance != null && NetClient.Instance.IsConnected)
            {
                int damageToDeal = StatsManager.Instance.GetDamageWithBonus();

                // Capture enemy info before async call (avoid closure capture issues)
                var capturedEnemyId = enemyIdentity.EnemyId;
                var capturedEnemyType = enemyIdentity.EnemyTypeId;
                var capturedEnemyObject = enemyObject; // Capture reference to apply immediate death

                Debug.Log($"[Player_Combat] Attacking enemy ID={capturedEnemyId} Type={capturedEnemyType}");

                // Report damage to server - HP will be synced via polling
                StartCoroutine(NetClient.Instance.ReportEnemyDamage(
                    capturedEnemyId,
                    damageToDeal,
                    res =>
                    {
                        if (res != null && res.accepted)
                        {
                            Debug.Log($"[Player_Combat] Damage accepted: {damageToDeal} dmg to {capturedEnemyId} -> HP: {res.currentHp}/{res.maxHp} (Dead: {res.isDead})");

                            // IMMEDIATELY apply death locally when server confirms kill
                            // This prevents multiple attacks being sent while waiting for polling sync
                            if (res.isDead && capturedEnemyObject != null)
                            {
                                var enemyHealth = capturedEnemyObject.GetComponent<Enemy_Health>();
                                if (enemyHealth != null && enemyHealth.currentHealth > 0)
                                {
                                    Debug.Log($"[Player_Combat] Immediately killing enemy {capturedEnemyId} (server confirmed dead)");
                                    
                                    // Set HP to 0 and destroy immediately (bypass ChangeHealth to avoid double destroy)
                                    enemyHealth.currentHealth = 0;
                                    
                                    // Trigger defeat event before destroying
                                    enemyHealth.TriggerDefeatEvent();
                                    
                                    // Destroy immediately
                                    Destroy(capturedEnemyObject);
                                }
                            }
                            else if (res != null && capturedEnemyObject != null)
                            {
                                // Update HP immediately for responsive feedback (server will sync later)
                                var enemyHealth = capturedEnemyObject.GetComponent<Enemy_Health>();
                                if (enemyHealth != null)
                                {
                                    // Only update if different to avoid unnecessary changes
                                    if (enemyHealth.currentHealth != res.currentHp)
                                    {
                                        enemyHealth.currentHealth = res.currentHp;
                                        enemyHealth.maxHealth = res.maxHp;
                                    }
                                }
                            }
                        }
                        else
                        {
                            Debug.LogWarning($"[Player_Combat] Damage not accepted for enemy {capturedEnemyId}");
                        }
                    },
                    err => Debug.LogError($"[Player_Combat] Failed to report damage: {err}")));
            }
            else
            {
                // Fallback: apply damage locally if not connected or enemy doesn't have identity (pre-placed enemies)
                if (enemyIdentity == null || !enemyIdentity.IsInitialized)
                {
                    Debug.LogWarning("[Player_Combat] Enemy doesn't have EnemyIdentity component or not initialized, applying damage locally (fallback)");
                }

                var enemyHealth = enemyObject.GetComponent<Enemy_Health>();
                if (enemyHealth != null)
                {
                    int damageToDeal = StatsManager.Instance.GetDamageWithBonus();
                    enemyHealth.ChangeHealth(-damageToDeal);
                }
            }
        }
    }




    public void FinishAttacking()
    {
        anim.SetBool("isAttacking", false);
    }
    private void OnDrawGizmosSelected()
    {
        if (attackPoint == null) return;

        // Check if StatsManager.Instance is available (might be null in Editor when not playing)
        float range = StatsManager.Instance != null
            ? StatsManager.Instance.weaponRange
            : 1.5f; // Default value for editor preview

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(attackPoint.position, range);
    }
}
