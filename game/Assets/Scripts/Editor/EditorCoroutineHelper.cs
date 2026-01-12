using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Helper class để chạy coroutine trong Unity Editor.
/// Sử dụng EditorApplication.update thay vì MonoBehaviour để tránh DontDestroyOnLoad error.
/// </summary>
public static class EditorCoroutineHelper
{
    private static Dictionary<IEnumerator, bool> s_ActiveCoroutines = new Dictionary<IEnumerator, bool>();

    public static void StartCoroutine(IEnumerator coroutine)
    {
        if (coroutine == null) return;

        s_ActiveCoroutines[coroutine] = true;
        EditorApplication.update += () => UpdateCoroutine(coroutine);
    }

    private static void UpdateCoroutine(IEnumerator coroutine)
    {
        if (!s_ActiveCoroutines.ContainsKey(coroutine))
        {
            EditorApplication.update -= () => UpdateCoroutine(coroutine);
            return;
        }

        if (!coroutine.MoveNext())
        {
            // Coroutine finished
            s_ActiveCoroutines.Remove(coroutine);
            EditorApplication.update -= () => UpdateCoroutine(coroutine);
        }
    }
}


