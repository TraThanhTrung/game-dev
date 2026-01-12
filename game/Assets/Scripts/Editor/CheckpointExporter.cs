using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Unity Editor tool để export CheckpointMarkers từ scene vào database.
/// Menu: Tools > Export Checkpoints
/// </summary>
public class CheckpointExporter : EditorWindow
{
    #region Constants
    private const string c_LogPrefix = "[CheckpointExporter]";
    private const string c_DefaultServerUrl = "http://localhost:5220";
    private const string c_DefaultExportPath = "Assets/ExportedCheckpoints";
    #endregion

    #region Private Fields
    private string m_ServerUrl = c_DefaultServerUrl;
    private string m_ExportPath = c_DefaultExportPath;
    private bool m_ExportToServer = true;
    private bool m_ExportToFile = true;
    private bool m_RequireAuth = true;
    private string m_AuthToken = "";
    private Vector2 m_ScrollPosition;
    private List<CheckpointMarker> m_FoundCheckpoints = new List<CheckpointMarker>();
    
    // Export progress
    private bool m_IsExporting = false;
    private int m_ExportedCount = 0;
    private int m_TotalCount = 0;
    private string m_ExportStatus = "";
    
    // Duplicate handling
    private enum DuplicateMode { CreateNew, UpdateExisting, Skip }
    private DuplicateMode m_DuplicateMode = DuplicateMode.UpdateExisting;
    #endregion

    #region Menu Item
    [MenuItem("Tools/Export Checkpoints to Database")]
    public static void ShowWindow()
    {
        var window = GetWindow<CheckpointExporter>("Checkpoint Exporter");
        window.m_IsExporting = false;
        window.m_ExportStatus = "";
    }
    #endregion

    #region Unity Editor GUI
    private void OnGUI()
    {
        EditorGUILayout.LabelField("Checkpoint Exporter", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // Server Settings
        EditorGUILayout.LabelField("Server Settings", EditorStyles.boldLabel);
        m_ServerUrl = EditorGUILayout.TextField("Server URL", m_ServerUrl);
        m_RequireAuth = EditorGUILayout.Toggle("Require Authentication", m_RequireAuth);
        if (m_RequireAuth)
        {
            m_AuthToken = EditorGUILayout.TextField("Auth Token", m_AuthToken);
        }
        EditorGUILayout.Space();

        // Export Options
        EditorGUILayout.LabelField("Export Options", EditorStyles.boldLabel);
        m_ExportToServer = EditorGUILayout.Toggle("Export to Server (API)", m_ExportToServer);
        m_ExportToFile = EditorGUILayout.Toggle("Export to File (JSON/SQL)", m_ExportToFile);
        if (m_ExportToFile)
        {
            m_ExportPath = EditorGUILayout.TextField("Export Path", m_ExportPath);
        }
        EditorGUILayout.Space();
        
        // Duplicate Handling
        EditorGUILayout.LabelField("Duplicate Handling", EditorStyles.boldLabel);
        m_DuplicateMode = (DuplicateMode)EditorGUILayout.EnumPopup("If Checkpoint Exists:", m_DuplicateMode);
        EditorGUILayout.HelpBox(
            m_DuplicateMode == DuplicateMode.CreateNew ? "Create new checkpoint (may fail if name exists)" :
            m_DuplicateMode == DuplicateMode.UpdateExisting ? "Update existing checkpoint by name" :
            "Skip if checkpoint name already exists",
            MessageType.Info);
        EditorGUILayout.Space();

        // Scan Scene
        EditorGUILayout.LabelField("Scene Checkpoints", EditorStyles.boldLabel);
        if (GUILayout.Button("Scan Scene for Checkpoints"))
        {
            ScanScene();
        }

        if (m_FoundCheckpoints.Count > 0)
        {
            EditorGUILayout.LabelField($"Found {m_FoundCheckpoints.Count} checkpoints:", EditorStyles.boldLabel);
            m_ScrollPosition = EditorGUILayout.BeginScrollView(m_ScrollPosition, GUILayout.Height(200));
            foreach (var checkpoint in m_FoundCheckpoints)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"• {checkpoint.checkpointName}", GUILayout.Width(200));
                EditorGUILayout.LabelField($"Section {checkpoint.sectionId}", GUILayout.Width(100));
                EditorGUILayout.LabelField($"({checkpoint.GetPosition().x:F1}, {checkpoint.GetPosition().y:F1})", GUILayout.Width(150));
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
        }
        else
        {
            EditorGUILayout.HelpBox("No CheckpointMarker components found in scene. Add CheckpointMarker component to GameObjects to mark them as checkpoints.", MessageType.Info);
        }

        EditorGUILayout.Space();

        // Export Status
        if (m_IsExporting)
        {
            EditorGUILayout.HelpBox($"Exporting... {m_ExportedCount}/{m_TotalCount}\n{m_ExportStatus}", MessageType.Info);
        }
        else if (!string.IsNullOrEmpty(m_ExportStatus))
        {
            EditorGUILayout.HelpBox(m_ExportStatus, MessageType.Info);
        }

        // Export Button
        GUI.enabled = m_FoundCheckpoints.Count > 0 && !m_IsExporting;
        if (GUILayout.Button(m_IsExporting ? "Exporting..." : "Export Checkpoints", GUILayout.Height(30)))
        {
            ExportCheckpoints();
        }
        GUI.enabled = true;

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("1. Add CheckpointMarker component to GameObjects in scene\n2. Configure checkpoint settings\n3. Click 'Scan Scene' to find all checkpoints\n4. Click 'Export Checkpoints' to export to database", MessageType.Info);
    }
    #endregion

    #region Private Methods
    private void ScanScene()
    {
        m_FoundCheckpoints.Clear();
        var markers = FindObjectsOfType<CheckpointMarker>();
        m_FoundCheckpoints.AddRange(markers);
        Debug.Log($"{c_LogPrefix} Found {m_FoundCheckpoints.Count} checkpoints in scene");
    }

    private void ExportCheckpoints()
    {
        if (m_FoundCheckpoints.Count == 0)
        {
            EditorUtility.DisplayDialog("Error", "No checkpoints found in scene. Please scan scene first.", "OK");
            return;
        }

        // Validate all checkpoints
        var validCheckpoints = new List<CheckpointMarker>();
        var invalidCheckpoints = new List<string>();

        foreach (var checkpoint in m_FoundCheckpoints)
        {
            if (checkpoint.Validate(out string error))
            {
                validCheckpoints.Add(checkpoint);
            }
            else
            {
                invalidCheckpoints.Add($"{checkpoint.checkpointName}: {error}");
            }
        }

        if (invalidCheckpoints.Count > 0)
        {
            var message = "Some checkpoints are invalid:\n\n" + string.Join("\n", invalidCheckpoints);
            EditorUtility.DisplayDialog("Validation Error", message, "OK");
            return;
        }

        // Export to file
        if (m_ExportToFile)
        {
            ExportToFile(validCheckpoints);
        }

        // Export to server
        if (m_ExportToServer)
        {
            ExportToServer(validCheckpoints);
        }

        EditorUtility.DisplayDialog("Success", $"Exported {validCheckpoints.Count} checkpoints successfully!", "OK");
    }

    private void ExportToFile(List<CheckpointMarker> checkpoints)
    {
        // Create export directory
        if (!Directory.Exists(m_ExportPath))
        {
            Directory.CreateDirectory(m_ExportPath);
        }

        // Export as JSON
        var checkpointList = new List<CheckpointData>();
        foreach (var marker in checkpoints)
        {
            var pos = marker.GetPosition();
            checkpointList.Add(new CheckpointData
            {
                checkpointName = marker.checkpointName,
                sectionId = marker.sectionId,
                x = pos.x,
                y = pos.y,
                enemyPool = marker.enemyPool,
                maxEnemies = marker.maxEnemies,
                isActive = marker.isActive
            });
        }

        var jsonData = new CheckpointExportData
        {
            checkpoints = checkpointList.ToArray()
        };

        var jsonPath = Path.Combine(m_ExportPath, "checkpoints_export.json");
        var json = JsonUtility.ToJson(jsonData, true);
        File.WriteAllText(jsonPath, json);
        Debug.Log($"{c_LogPrefix} Exported JSON to {jsonPath}");

        // Export as SQL
        var sqlPath = Path.Combine(m_ExportPath, "checkpoints_export.sql");
        var sql = GenerateSQL(checkpoints);
        File.WriteAllText(sqlPath, sql);
        Debug.Log($"{c_LogPrefix} Exported SQL to {sqlPath}");

        AssetDatabase.Refresh();
    }

    private void ExportToServer(List<CheckpointMarker> checkpoints)
    {
        if (string.IsNullOrEmpty(m_ServerUrl))
        {
            EditorUtility.DisplayDialog("Error", "Server URL is required for server export.", "OK");
            return;
        }

        m_IsExporting = true;
        m_ExportedCount = 0;
        m_TotalCount = checkpoints.Count;
        m_ExportStatus = "Starting export...";

        // Start export using EditorApplication.update
        StartServerExport(checkpoints);
    }

    private List<CheckpointMarker> m_PendingCheckpoints;
    private UnityWebRequest m_CurrentRequest;
    private int m_CurrentIndex = 0;
    private int m_SuccessCount = 0;
    private int m_FailCount = 0;
    private int m_SkippedCount = 0;
    private List<string> m_ExportErrors = new List<string>();
    private Dictionary<string, int> m_CheckpointNameToId = new Dictionary<string, int>(); // Cache for lookup

    private void StartServerExport(List<CheckpointMarker> checkpoints)
    {
        m_PendingCheckpoints = new List<CheckpointMarker>(checkpoints);
        m_CurrentIndex = 0;
        m_SuccessCount = 0;
        m_FailCount = 0;
        m_SkippedCount = 0;
        m_ExportErrors.Clear();
        m_CurrentRequest = null;
        m_CheckpointNameToId.Clear();

        // Pre-load existing checkpoints if UpdateExisting mode
        if (m_DuplicateMode == DuplicateMode.UpdateExisting)
        {
            EditorApplication.update += LoadExistingCheckpoints;
        }
        else
        {
            EditorApplication.update += ProcessServerExport;
            ProcessServerExport(); // Start immediately
        }
    }

    private UnityWebRequest m_LoadRequest = null;
    private void LoadExistingCheckpoints()
    {
        if (m_LoadRequest != null)
        {
            // Check if request is done
            if (m_LoadRequest.isDone)
            {
                HandleLoadCheckpointsComplete();
                return;
            }
            // Still loading
            Repaint();
            return;
        }

        // Start loading
        m_ExportStatus = "Loading existing checkpoints...";
        Repaint();

        var url = $"{m_ServerUrl.TrimEnd('/')}/api/checkpoints";
        m_LoadRequest = UnityWebRequest.Get(url);
        m_LoadRequest.SetRequestHeader("Content-Type", "application/json");

        if (m_RequireAuth && !string.IsNullOrEmpty(m_AuthToken))
        {
            m_LoadRequest.SetRequestHeader("Authorization", $"Bearer {m_AuthToken}");
        }

        m_LoadRequest.SendWebRequest();
        Repaint();
    }

    private void HandleLoadCheckpointsComplete()
    {
        if (m_LoadRequest == null) return;

        if (m_LoadRequest.result == UnityWebRequest.Result.Success)
        {
            try
            {
                var json = m_LoadRequest.downloadHandler.text;
                // Server returns List<Checkpoint> directly, but JsonUtility doesn't support arrays
                // Wrap it manually
                var wrappedJson = "{\"checkpoints\":" + json + "}";
                var response = JsonUtility.FromJson<CheckpointListResponse>(wrappedJson);
                if (response != null && response.checkpoints != null)
                {
                    foreach (var cp in response.checkpoints)
                    {
                        m_CheckpointNameToId[cp.checkpointName] = cp.checkpointId;
                    }
                    Debug.Log($"{c_LogPrefix} Loaded {m_CheckpointNameToId.Count} existing checkpoints");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{c_LogPrefix} Failed to parse existing checkpoints: {ex.Message}");
            }
        }
        else
        {
            Debug.LogWarning($"{c_LogPrefix} Failed to load existing checkpoints: {m_LoadRequest.error}");
        }

        m_LoadRequest.Dispose();
        m_LoadRequest = null;

        // Start export after loading
        EditorApplication.update -= LoadExistingCheckpoints;
        EditorApplication.update += ProcessServerExport;
        ProcessServerExport();
    }

    private void ProcessServerExport()
    {
        if (m_PendingCheckpoints == null || m_CurrentIndex >= m_PendingCheckpoints.Count)
        {
            // All done
            EditorApplication.update -= ProcessServerExport;
            FinishServerExport();
            return;
        }

        // If we have a pending request, check if it's done
        if (m_CurrentRequest != null)
        {
            if (m_CurrentRequest.isDone)
            {
                HandleRequestComplete();
                m_CurrentRequest = null;
            }
            else
            {
                // Still waiting
                Repaint();
                return;
            }
        }

        // Start next request
        if (m_CurrentIndex < m_PendingCheckpoints.Count)
        {
            var marker = m_PendingCheckpoints[m_CurrentIndex];
            var pos = marker.GetPosition();
            m_ExportStatus = $"Exporting {marker.checkpointName}... ({m_CurrentIndex + 1}/{m_TotalCount})";
            Repaint();

            // Check if checkpoint exists
            bool checkpointExists = m_CheckpointNameToId.ContainsKey(marker.checkpointName);

            if (checkpointExists && m_DuplicateMode == DuplicateMode.Skip)
            {
                // Skip this checkpoint
                m_SkippedCount++;
                m_ExportedCount++;
                m_CurrentIndex++;
                Debug.Log($"{c_LogPrefix} Skipped existing checkpoint: {marker.checkpointName}");
                return; // Continue to next
            }

            // Create checkpoint data
            var checkpointData = new CheckpointApiData
            {
                CheckpointName = marker.checkpointName,
                SectionId = marker.sectionId,
                X = pos.x,
                Y = pos.y,
                EnemyPool = marker.enemyPool,
                MaxEnemies = marker.maxEnemies,
                IsActive = marker.isActive
            };

            // Convert to JSON
            var json = JsonUtility.ToJson(checkpointData);
            var jsonBytes = Encoding.UTF8.GetBytes(json);

            // Determine URL and method
            string url;
            string method;
            if (checkpointExists && m_DuplicateMode == DuplicateMode.UpdateExisting)
            {
                // Update existing checkpoint
                int checkpointId = m_CheckpointNameToId[marker.checkpointName];
                url = $"{m_ServerUrl.TrimEnd('/')}/api/checkpoints/{checkpointId}";
                method = "PUT";
            }
            else
            {
                // Create new checkpoint
                url = $"{m_ServerUrl.TrimEnd('/')}/api/checkpoints";
                method = "POST";
            }

            // Create request
            m_CurrentRequest = new UnityWebRequest(url, method);
            m_CurrentRequest.uploadHandler = new UploadHandlerRaw(jsonBytes);
            m_CurrentRequest.downloadHandler = new DownloadHandlerBuffer();
            m_CurrentRequest.SetRequestHeader("Content-Type", "application/json");

            // Add auth header if required
            if (m_RequireAuth && !string.IsNullOrEmpty(m_AuthToken))
            {
                m_CurrentRequest.SetRequestHeader("Authorization", $"Bearer {m_AuthToken}");
            }

            // Send request
            m_CurrentRequest.SendWebRequest();
            m_CurrentIndex++;
        }

        Repaint();
    }

    private void HandleRequestComplete()
    {
        if (m_CurrentRequest == null) return;

        var marker = m_PendingCheckpoints[m_CurrentIndex - 1];

        if (m_CurrentRequest.result == UnityWebRequest.Result.Success)
        {
            m_SuccessCount++;
            Debug.Log($"{c_LogPrefix} Successfully exported checkpoint: {marker.checkpointName} (HTTP {m_CurrentRequest.responseCode})");
        }
        else
        {
            m_FailCount++;
            var errorMsg = $"Failed to export {marker.checkpointName}: {m_CurrentRequest.error}";
            if (m_CurrentRequest.responseCode > 0)
            {
                errorMsg += $" (HTTP {m_CurrentRequest.responseCode})";
                if (!string.IsNullOrEmpty(m_CurrentRequest.downloadHandler.text))
                {
                    errorMsg += $"\nResponse: {m_CurrentRequest.downloadHandler.text}";
                }
            }
            m_ExportErrors.Add(errorMsg);
            Debug.LogError($"{c_LogPrefix} {errorMsg}");
        }

        m_ExportedCount++;
        m_CurrentRequest.Dispose();
        m_CurrentRequest = null;
    }

    private void FinishServerExport()
    {
        m_IsExporting = false;
        m_ExportStatus = "";

        if (m_FailCount == 0)
        {
            var message = $"Successfully exported to server!\n\n" +
                         $"Created/Updated: {m_SuccessCount}";
            if (m_SkippedCount > 0)
            {
                message += $"\nSkipped: {m_SkippedCount}";
            }
            message += $"\n\nServer: {m_ServerUrl}";
            
            EditorUtility.DisplayDialog("Success", message, "OK");
            Debug.Log($"{c_LogPrefix} Successfully exported {m_SuccessCount} checkpoints to server (skipped: {m_SkippedCount})");
        }
        else
        {
            var message = $"Export completed with errors:\n\n" +
                         $"Success: {m_SuccessCount}";
            if (m_SkippedCount > 0)
            {
                message += $"\nSkipped: {m_SkippedCount}";
            }
            message += $"\nFailed: {m_FailCount}\n\n" +
                      $"Errors:\n{string.Join("\n", m_ExportErrors.Take(5))}";
            if (m_ExportErrors.Count > 5)
            {
                message += $"\n... and {m_ExportErrors.Count - 5} more errors";
            }

            EditorUtility.DisplayDialog("Export Completed with Errors", message, "OK");
            Debug.LogWarning($"{c_LogPrefix} Export completed: {m_SuccessCount} success, {m_SkippedCount} skipped, {m_FailCount} failed");
        }

        Repaint();
    }

    private string GenerateSQL(List<CheckpointMarker> checkpoints)
    {
        var sb = new StringBuilder();
        sb.AppendLine("-- ============================================");
        sb.AppendLine("-- Auto-generated Checkpoint Export SQL");
        sb.AppendLine($"-- Generated: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"-- Total Checkpoints: {checkpoints.Count}");
        sb.AppendLine("-- ============================================");
        sb.AppendLine();

        foreach (var marker in checkpoints)
        {
            var pos = marker.GetPosition();
            sb.AppendLine($"INSERT INTO Checkpoints (CheckpointName, SectionId, X, Y, EnemyPool, MaxEnemies, IsActive, CreatedAt)");
            sb.AppendLine($"VALUES (");
            sb.AppendLine($"    '{marker.checkpointName.Replace("'", "''")}',");
            sb.AppendLine($"    {marker.sectionId},");
            sb.AppendLine($"    {pos.x:F2},");
            sb.AppendLine($"    {pos.y:F2},");
            sb.AppendLine($"    '{marker.enemyPool}',");
            sb.AppendLine($"    {marker.maxEnemies},");
            sb.AppendLine($"    {(marker.isActive ? 1 : 0)},");
            sb.AppendLine($"    datetime('now')");
            sb.AppendLine($");");
            sb.AppendLine();
        }

        return sb.ToString();
    }
    #endregion

    #region Data Classes
    [System.Serializable]
    private class CheckpointExportData
    {
        public CheckpointData[] checkpoints;
    }

    [System.Serializable]
    private class CheckpointData
    {
        public string checkpointName;
        public int sectionId;
        public float x;
        public float y;
        public string enemyPool;
        public int maxEnemies;
        public bool isActive;
    }

    [System.Serializable]
    private class CheckpointApiData
    {
        public string CheckpointName;
        public int SectionId;
        public float X;
        public float Y;
        public string EnemyPool;
        public int MaxEnemies;
        public bool IsActive;
    }

    [System.Serializable]
    private class CheckpointListResponse
    {
        public CheckpointResponse[] checkpoints;
    }

    [System.Serializable]
    private class CheckpointResponse
    {
        public int checkpointId;
        public string checkpointName;
        public int? sectionId;
        public float x;
        public float y;
        public string enemyPool;
        public int maxEnemies;
        public bool isActive;
    }
    #endregion
}

