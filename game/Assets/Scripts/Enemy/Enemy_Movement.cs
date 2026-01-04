using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Enemy_Movement : MonoBehaviour
{
    #region Private Fields
    [SerializeField] private Transform m_DetectionPoint;
    [SerializeField] private LayerMask m_PlayerLayer;

    private float m_Speed = 2f;
    private float m_AttackRange = 1.2f;
    private float m_AttackCooldown = 2f;
    private float m_PlayerDetectRange = 6f;

    private float m_AttackCooldownTimer;
    private int m_FacingDirection = -1;
    private EnemyState m_EnemyState;

    private Rigidbody2D m_Rb;
    private Transform m_Player;
    private Animator m_Anim;
    private Enemy_Health m_EnemyHealth;
    #endregion

    #region Public Properties
    public Transform detectionPoint => m_DetectionPoint;
    public LayerMask playerLayer => m_PlayerLayer;
    #endregion



    #region Unity Lifecycle
    private void Start()
    {
        m_Rb = GetComponent<Rigidbody2D>();
        m_Anim = GetComponent<Animator>();
        m_EnemyHealth = GetComponent<Enemy_Health>();

        LoadFromConfig();
        ChangeState(EnemyState.Idle);
    }
    #endregion

    #region Private Methods
    private void LoadFromConfig()
    {
        // Ensure GameConfigLoader exists
        GameConfigLoader.EnsureInstance();

        if (GameConfigLoader.Instance == null)
        {
            Debug.LogWarning("[Enemy_Movement] GameConfigLoader not found. Using defaults.");
            return;
        }

        // Get typeId from Enemy_Health component
        string typeId = "slime"; // default
        if (m_EnemyHealth != null)
        {
            typeId = m_EnemyHealth.EnemyTypeId;
        }

        var enemyConfig = GameConfigLoader.Instance.GetEnemyConfig(typeId);
        if (enemyConfig != null)
        {
            m_Speed = enemyConfig.speed;
            m_AttackRange = enemyConfig.attackRange;
            m_AttackCooldown = enemyConfig.attackCooldown;
            m_PlayerDetectRange = enemyConfig.detectRange;
            Debug.Log($"[Enemy_Movement] Loaded config for {typeId}: speed={m_Speed}, attackRange={m_AttackRange}");
        }
        else
        {
            Debug.LogWarning($"[Enemy_Movement] Config not found for typeId={typeId}. Using defaults.");
        }
    }
    #endregion




    private void Update()
    {
        if (m_EnemyState != EnemyState.Knockback)
        {
            CheckForPlayer();

            if (m_AttackCooldownTimer > 0)
            {
                m_AttackCooldownTimer -= Time.deltaTime;
            }

            if (m_EnemyState == EnemyState.Chasing)
            {
                Chase();
            }
            else if (m_EnemyState == EnemyState.Attacking)
            {
                //Do Attacky Stuff
                m_Rb.velocity = Vector2.zero;
            }
        }
    }



    private void Chase()
    {
        if (m_Player == null) return;

        if (m_Player.position.x > transform.position.x && m_FacingDirection == -1 ||
            m_Player.position.x < transform.position.x && m_FacingDirection == 1)
        {
            Flip();
        }

        Vector2 direction = (m_Player.position - transform.position).normalized;
        m_Rb.velocity = direction * m_Speed;
    }




    private void Flip()
    {
        m_FacingDirection *= -1;
        transform.localScale = new Vector3(transform.localScale.x * -1, transform.localScale.y, transform.localScale.z);
    }



    private void CheckForPlayer()
    {
        if (m_DetectionPoint == null) return;

        Collider2D[] hits = Physics2D.OverlapCircleAll(m_DetectionPoint.position, m_PlayerDetectRange, m_PlayerLayer);

        if (hits.Length > 0)
        {
            m_Player = hits[0].transform;

            //if the player is in attack range AND cooldown is ready
            if (Vector2.Distance(transform.position, m_Player.position) < m_AttackRange && m_AttackCooldownTimer <= 0)
            {
                m_AttackCooldownTimer = m_AttackCooldown;
                ChangeState(EnemyState.Attacking);
            }
            else if (Vector2.Distance(transform.position, m_Player.position) > m_AttackRange && m_EnemyState != EnemyState.Attacking)
            {
                ChangeState(EnemyState.Chasing);
            }
        }
        else
        {
            m_Rb.velocity = Vector2.zero;
            ChangeState(EnemyState.Idle);
        }
    }




    public void ChangeState(EnemyState newState)
    {
        if (m_Anim == null) return;

        //Exit the current animation
        if (m_EnemyState == EnemyState.Idle)
            m_Anim.SetBool("isIdle", false);
        else if (m_EnemyState == EnemyState.Chasing)
            m_Anim.SetBool("isChasing", false);
        else if (m_EnemyState == EnemyState.Attacking)
            m_Anim.SetBool("isAttacking", false);

        //Update our current state
        m_EnemyState = newState;

        //Update the new animation
        if (m_EnemyState == EnemyState.Idle)
            m_Anim.SetBool("isIdle", true);
        else if (m_EnemyState == EnemyState.Chasing)
            m_Anim.SetBool("isChasing", true);
        else if (m_EnemyState == EnemyState.Attacking)
            m_Anim.SetBool("isAttacking", true);
    }


    private void OnDrawGizmosSelected()
    {
        if (m_DetectionPoint == null) return;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(m_DetectionPoint.position, m_PlayerDetectRange);
    }
}


public enum EnemyState
{
    Idle,
    Chasing,
    Attacking,
    Knockback
}
