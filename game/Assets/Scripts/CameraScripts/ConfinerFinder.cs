using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;
using UnityEngine.SceneManagement;

public class ConfinerFinder : MonoBehaviour
{

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }


    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        CinemachineConfiner2D confiner = GetComponent<CinemachineConfiner2D>();
        if (confiner == null)
        {
            Debug.LogWarning("[ConfinerFinder] CinemachineConfiner2D component not found!");
            return;
        }

        GameObject confinerObject = GameObject.FindWithTag("Confiner");
        if (confinerObject == null)
        {
            Debug.LogWarning("[ConfinerFinder] GameObject with tag 'Confiner' not found in scene!");
            return;
        }

        PolygonCollider2D polygonCollider = confinerObject.GetComponent<PolygonCollider2D>();
        if (polygonCollider == null)
        {
            Debug.LogWarning("[ConfinerFinder] PolygonCollider2D component not found on Confiner GameObject!");
            return;
        }

        confiner.m_BoundingShape2D = polygonCollider;
    }

}
