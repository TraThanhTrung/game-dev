using System.Collections;
using UnityEngine;

public class PlayerInputSender : MonoBehaviour
{
    #region Constants
    private const string AxisHorizontal = "Horizontal";
    private const string AxisVertical = "Vertical";
    private const string ButtonSlash = "Slash";
    private const string ButtonShoot = "Shoot";
    #endregion

    #region Private Fields
    [SerializeField] private float m_SendInterval = 0.1f;

    private int sequence;
    private Coroutine sendRoutine;
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

            bool attack = Input.GetButtonDown(ButtonSlash);
            bool shoot = Input.GetButtonDown(ButtonShoot);

            sequence++;

            Debug.Log($"[InputSender] send move=({moveX},{moveY}) aim=({aimX},{aimY}) atk={attack} shoot={shoot} seq={sequence}");

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

