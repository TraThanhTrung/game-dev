using System.Collections;
using UnityEngine;

/// <summary>
/// Sends player input to the server at a fixed interval.
/// Captures attack/shoot button presses immediately to avoid missing inputs.
/// </summary>
public class PlayerInputSender : MonoBehaviour
{
    #region Constants
    private const string AxisHorizontal = "Horizontal";
    private const string AxisVertical = "Vertical";
    private const string ButtonSlash = "Slash";
    private const string ButtonShoot = "Shoot";
    #endregion

    #region Private Fields
    [SerializeField] private float m_SendInterval = 0.05f; // 20 Hz to match server tick

    private int sequence;
    private Coroutine sendRoutine;

    // Event-based: capture button presses immediately
    private bool pendingAttack;
    private bool pendingShoot;
    #endregion

    #region Unity Lifecycle
    private void OnEnable()
    {
        StartSending();
    }

    private void OnDisable()
    {
        StopSending();
    }

    private void Update()
    {
        // Capture button presses every frame to avoid missing inputs
        if (Input.GetButtonDown(ButtonSlash)) pendingAttack = true;
        if (Input.GetButtonDown(ButtonShoot)) pendingShoot = true;
    }
    #endregion

    #region Private Methods
    private void StartSending()
    {
        if (sendRoutine == null)
        {
            sendRoutine = StartCoroutine(SendLoop());
        }
    }

    private void StopSending()
    {
        if (sendRoutine != null)
        {
            StopCoroutine(sendRoutine);
            sendRoutine = null;
        }
    }

    private IEnumerator SendLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(m_SendInterval);

            if (NetClient.Instance == null || !NetClient.Instance.IsConnected)
                continue;

            var moveX = Input.GetAxisRaw(AxisHorizontal);
            var moveY = Input.GetAxisRaw(AxisVertical);

            var aimX = moveX;
            var aimY = moveY;

            // Use pending flags for attack/shoot (captured in Update)
            bool attack = pendingAttack;
            bool shoot = pendingShoot;
            pendingAttack = false;
            pendingShoot = false;

            sequence++;

            var payload = new InputPayload
            {
                moveX = moveX,
                moveY = moveY,
                aimX = aimX,
                aimY = aimY,
                attack = attack,
                shoot = shoot,
                sequence = sequence
            };

            yield return NetClient.Instance.SendInput(payload, err => Debug.LogWarning($"SendInput error: {err}"));
        }
    }
    #endregion
}
