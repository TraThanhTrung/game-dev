using UnityEngine;

/// <summary>
/// ScriptableObject để quản lý cấu hình server URL tập trung.
/// Tạo asset từ menu: Create > Config > Server Config
/// </summary>
[CreateAssetMenu(fileName = "ServerConfig", menuName = "Config/Server Config", order = 1)]
public class ServerConfig : ScriptableObject
{
    #region Constants
    private const string c_DefaultBaseUrl = "http://localhost:5220";
    #endregion

    #region Private Fields
    [SerializeField] private string m_BaseUrl = c_DefaultBaseUrl;
    #endregion

    #region Public Properties
    /// <summary>
    /// Base URL của server (ví dụ: "http://localhost:5220").
    /// </summary>
    public string BaseUrl
    {
        get
        {
            if (string.IsNullOrWhiteSpace(m_BaseUrl))
            {
                return c_DefaultBaseUrl;
            }
            return m_BaseUrl.TrimEnd('/');
        }
        set
        {
            m_BaseUrl = string.IsNullOrWhiteSpace(value) ? c_DefaultBaseUrl : value.TrimEnd('/');
        }
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Lấy base URL từ config, fallback về default nếu chưa được assign.
    /// </summary>
    public string GetBaseUrl()
    {
        return BaseUrl;
    }
    #endregion
}





