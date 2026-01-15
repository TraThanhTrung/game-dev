using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class HomeUiRaycastLogger : MonoBehaviour
{
    #region Constants
    private static readonly string[] c_TargetButtons =
    {
        "SwordBtn",
        "SpearBtn",
        "ArcherBtn",
        "MagicBtn"
    };
    #endregion

    #region Private Fields
    private GraphicRaycaster[] m_AllRaycasters;
    #endregion

    #region Unity Lifecycle
    private void Start()
    {
        m_AllRaycasters = FindObjectsOfType<GraphicRaycaster>(includeInactive: true);
        LogEventSystem();
        LogCanvasAndRaycasters();
        LogTargetsState();
        StartCoroutine(LogRaycastResultsForTargets());
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            LogRaycastAtScreenPoint(Input.mousePosition, "[MouseClick]");
        }
    }
    #endregion

    #region Private Methods
    private void LogEventSystem()
    {
        var es = EventSystem.current;
        Debug.Log($"[UI-Dbg] EventSystem present: {(es != null ? "YES" : "NO")}");
    }

    private void LogCanvasAndRaycasters()
    {
        var canvases = FindObjectsOfType<Canvas>(includeInactive: true);
        foreach (var canvas in canvases)
        {
            var gr = canvas.GetComponent<GraphicRaycaster>();
            Debug.Log($"[UI-Dbg] Canvas='{canvas.name}', enabled={canvas.enabled}, renderMode={canvas.renderMode}, receivesEvents={(canvas as object != null ? "N/A" : "N/A")}, hasGraphicRaycaster={(gr != null ? "YES" : "NO")}");
            if (gr != null)
            {
                Debug.Log($"[UI-Dbg]   GraphicRaycaster on '{canvas.name}': enabled={gr.enabled}, blockingObjects={gr.blockingObjects}");
            }
        }
    }

    private void LogTargetsState()
    {
        foreach (var name in c_TargetButtons)
        {
            var go = GameObject.Find(name);
            if (go == null)
            {
                Debug.LogWarning($"[UI-Dbg] Target '{name}' NOT FOUND");
                continue;
            }

            var btn = go.GetComponent<Button>();
            var img = go.GetComponent<Image>();
            var rt = go.GetComponent<RectTransform>();
            var groups = go.GetComponentsInParent<CanvasGroup>(includeInactive: true);

            Debug.Log($"[UI-Dbg] '{name}': activeInHierarchy={go.activeInHierarchy}, Button={(btn != null ? "YES" : "NO")}, Interactable={(btn != null && btn.interactable)}, Image={(img != null ? "YES" : "NO")}, RaycastTarget={(img != null && img.raycastTarget)}");

            if (rt != null)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, Vector2.zero, null, out var _);
                var worldPos = rt.position;
                Debug.Log($"[UI-Dbg]   RectTransform pos={worldPos}, sizeDelta={rt.sizeDelta}, anchors=({rt.anchorMin} -> {rt.anchorMax})");
            }

            foreach (var g in groups)
            {
                Debug.Log($"[UI-Dbg]   CanvasGroup '{g.name}': alpha={g.alpha}, interactable={g.interactable}, blocksRaycasts={g.blocksRaycasts}");
            }
        }
    }

    private IEnumerator LogRaycastResultsForTargets()
    {
        // wait a frame so layout is ready
        yield return null;

        foreach (var name in c_TargetButtons)
        {
            var go = GameObject.Find(name);
            if (go == null) continue;
            var rt = go.GetComponent<RectTransform>();
            if (rt == null) continue;

            var screenPoint = RectTransformUtility.WorldToScreenPoint(null, rt.position);
            LogRaycastAtScreenPoint(screenPoint, $"[AutoTest:{name}]");
        }
    }

    private void LogRaycastAtScreenPoint(Vector2 screenPoint, string tag)
    {
        if (EventSystem.current == null)
        {
            Debug.LogWarning("[UI-Dbg] No EventSystem to raycast.");
            return;
        }

        var data = new PointerEventData(EventSystem.current)
        {
            position = screenPoint
        };

        var sb = new StringBuilder();
        sb.Append($"{tag} Raycast at {screenPoint}:");

        int totalHits = 0;
        foreach (var gr in m_AllRaycasters)
        {
            if (gr == null || !gr.isActiveAndEnabled) continue;
            var results = new List<RaycastResult>();
            gr.Raycast(data, results);
            if (results.Count == 0) continue;

            foreach (var r in results)
            {
                totalHits++;
                sb.Append($"\n  - Canvas='{gr.name}', hit='{r.gameObject.name}', depth={r.depth}, sortingOrder={r.sortingOrder}, distance={r.distance}");
            }
        }

        if (totalHits == 0)
        {
            sb.Append(" no hits");
        }

        Debug.Log(sb.ToString());
    }
    #endregion
}

















