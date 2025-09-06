using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

[System.Serializable]
public class EmotionData
{
    public string emotion;
    public string timestamp;
    public float confidence;
    public int faces_detected;
}

public class EmotionDetectionClient : MonoBehaviour
{
    [Header("Server Settings")]
    [SerializeField] private string serverUrl = "http://localhost:5001/emotion";
    [SerializeField]
    private string[] fallbackUrls = {
        "http://localhost:5002/emotion",
        "http://127.0.0.1:5001/emotion",
        "http://127.0.0.1:5002/emotion"
    };
    [SerializeField] private float pollInterval = 0.5f;

    [Header("Emotion Trigger Settings")]
    [SerializeField] private string targetEmotion = "neutral";
    [SerializeField] private float cooldownPeriod = 6.5f;
    [SerializeField] private float minimumConfidence = 50f;

    [Header("Animation Manager Reference")]
    [SerializeField] private SingleTrackAnimationManager animationManager;

    [Header("UI Feedback (Optional)")]
    [SerializeField] private Text currentEmotionText;
    [SerializeField] private Text statusText;
    [SerializeField] private Image emotionIndicator;

    [Header("Debug")]
    [SerializeField] private bool debugMode = true;
    [SerializeField] private bool useTimestampMode = true; // Newest Toggle between old cooldown mode and new timestamp mode

    // Private variables
    private bool isPolling = false;
    private float lastTriggerTime = -10f;
    private Coroutine pollingCoroutine;
    private EmotionData lastEmotionData;
    private string currentServerUrl;
    private int connectionAttempts = 0;
    private bool connectionTestResult = false;

    // Color indicators
    private Color neutralDetectedColor = new Color(0.2f, 0.8f, 0.2f);
    private Color otherEmotionColor = new Color(0.8f, 0.8f, 0.2f);
    private Color noDetectionColor = new Color(0.8f, 0.2f, 0.2f);
    private Color cooldownColor = new Color(0.2f, 0.2f, 0.8f);

    void Start()
    {
        // Debug logging
        Debug.Log($"=== EmotionDetectionClient Starting ===");
        Debug.Log($"Target Server URL: {serverUrl}");
        Debug.Log($"Target Emotion: {targetEmotion}");
        Debug.Log($"Minimum Confidence: {minimumConfidence}%");
        Debug.Log($"Cooldown Period: {cooldownPeriod}s");
        Debug.Log($"Timestamp Mode: {(useTimestampMode ? "ENABLED" : "DISABLED")}");

        // Find animation manager if not assigned
        if (animationManager == null)
        {
            animationManager = FindObjectOfType<SingleTrackAnimationManager>();
            if (animationManager == null)
            {
                Debug.LogError("SingleTrackAnimationManager not found, Please assign it in the inspector.");
                UpdateStatusText("ERROR: Animation Manager not found");
                return;
            }
            else
            {
                Debug.Log("Found SingleTrackAnimationManager automatically");
            }
        }

        // Initialize current server URL
        currentServerUrl = serverUrl;

        // Test connection first
        StartCoroutine(TestAllConnections());
    }

    IEnumerator TestAllConnections()
    {
        Debug.Log("Testing server connections...");
        UpdateStatusText("Testing server connections...");

        // Test primary URL
        yield return StartCoroutine(TestSingleConnection(serverUrl));
        if (connectionTestResult)
        {
            Debug.Log($"Primary server connection successful: {serverUrl}");
            currentServerUrl = serverUrl;
            StartPolling();
            yield break;
        }

        // Test fallback URLs
        foreach (string fallbackUrl in fallbackUrls)
        {
            Debug.Log($"Trying fallback URL: {fallbackUrl}");
            yield return StartCoroutine(TestSingleConnection(fallbackUrl));
            if (connectionTestResult)
            {
                Debug.Log($"Fallback server connection successful: {fallbackUrl}");
                currentServerUrl = fallbackUrl;
                StartPolling();
                yield break;
            }
        }

        // No connections worked
        Debug.LogError("All server connections failed!");
        UpdateStatusText("ERROR: Cannot connect to any server");
        UpdateEmotionIndicator(noDetectionColor);
    }

    IEnumerator TestSingleConnection(string url)
    {
        connectionTestResult = false;

        using (UnityWebRequest webRequest = UnityWebRequest.Get(url))
        {
            webRequest.timeout = 3; // Short timeout for testing
            yield return webRequest.SendWebRequest();

            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"Connection test successful for {url}");
                Debug.Log($"Response: {webRequest.downloadHandler.text.Substring(0, Mathf.Min(100, webRequest.downloadHandler.text.Length))}...");
                connectionTestResult = true;
            }
            else
            {
                Debug.LogWarning($"Connection test failed for {url}: {webRequest.error}");
                connectionTestResult = false;
            }
        }
    }

    public void StartPolling()
    {
        if (!isPolling)
        {
            isPolling = true;
            pollingCoroutine = StartCoroutine(PollEmotionServer());
            UpdateStatusText($"Started polling: {currentServerUrl}");
            Debug.Log($"Started polling emotion server: {currentServerUrl}");
        }
    }

    public void StopPolling()
    {
        if (isPolling)
        {
            isPolling = false;
            if (pollingCoroutine != null)
            {
                StopCoroutine(pollingCoroutine);
                pollingCoroutine = null;
            }
            UpdateStatusText("Stopped polling emotion server");
            Debug.Log("Stopped polling emotion server");
        }
    }

    IEnumerator PollEmotionServer()
    {
        while (isPolling)
        {
            yield return StartCoroutine(FetchEmotionData());
            yield return new WaitForSeconds(pollInterval);
        }
    }

    IEnumerator FetchEmotionData()
    {
        connectionAttempts++;

        using (UnityWebRequest webRequest = UnityWebRequest.Get(currentServerUrl))
        {
            webRequest.timeout = 5;
            yield return webRequest.SendWebRequest();

            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                // Reset connection attempts on success
                connectionAttempts = 0;
                ProcessResponse(webRequest.downloadHandler.text);
            }
            else
            {
                Debug.LogWarning($"Connection error (attempt {connectionAttempts}): {webRequest.error}");
                UpdateStatusText($"Connection error: {webRequest.error}");
                UpdateEmotionIndicator(noDetectionColor);

                // Try to reconnect after several failed attempts
                if (connectionAttempts >= 5)
                {
                    Debug.Log("Too many failed attempts, trying to reconnect...");
                    connectionAttempts = 0;
                    StartCoroutine(TestAllConnections());
                }
            }
        }
    }

    void ProcessResponse(string jsonResponse)
    {
        try
        {
            if (debugMode)
            {
                Debug.Log($"Received response: {jsonResponse.Substring(0, Mathf.Min(150, jsonResponse.Length))}...");
            }

            EmotionData emotionData = JsonUtility.FromJson<EmotionData>(jsonResponse);

            if (emotionData != null)
            {
                lastEmotionData = emotionData;
                ProcessEmotionData(emotionData);
            }
            else
            {
                Debug.LogWarning("Failed to parse emotion data - emotionData is null");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error parsing emotion data: {e.Message}");
            Debug.LogError($"Raw response: {jsonResponse}");
            UpdateStatusText("Error parsing emotion data");
        }
    }

    void ProcessEmotionData(EmotionData data)
    {
        // Update UI
        UpdateEmotionDisplay(data);

        // NEW: Handle timestamp mode vs cooldown mode
        if (useTimestampMode)
        {
            ProcessEmotionForTimestamps(data);
        }
        else
        {
            ProcessEmotionForCooldown(data);
        }
    }

    // NEW METHOD: Handle emotion detection for timestamp-based triggering
    void ProcessEmotionForTimestamps(EmotionData data)
    {
        // Enhanced debug logging
        if (debugMode)
        {
            Debug.Log($"Emotion Data (Timestamp Mode): {data.emotion} ({data.confidence:F1}%), Faces: {data.faces_detected}");
        }

        if (data.emotion.ToLower() == targetEmotion.ToLower() &&
            data.confidence >= minimumConfidence)
        {
            // Notify animation manager of neutral emotion detection
            if (animationManager != null)
            {
                animationManager.OnNeutralEmotionDetected();
            }

            UpdateEmotionIndicator(neutralDetectedColor);
            UpdateStatusText($"Neutral detected - checking for upcoming timestamps...");
        }
        else
        {
            // Notify animation manager of other emotion detection
            if (animationManager != null)
            {
                animationManager.OnOtherEmotionDetected(data.emotion);
            }

            if (data.emotion.ToLower() == targetEmotion.ToLower() && data.confidence < minimumConfidence)
            {
                UpdateEmotionIndicator(otherEmotionColor);
                UpdateStatusText($"Neutral detected but confidence too low: {data.confidence:F1}%");
            }
            else
            {
                UpdateEmotionIndicator(otherEmotionColor);
                UpdateStatusText($"Waiting for {targetEmotion} emotion... (Current: {data.emotion})");
            }
        }
    }

    // ORIGINAL METHOD: Handle emotion detection for cooldown-based triggering
    void ProcessEmotionForCooldown(EmotionData data)
    {
        // Check if we should trigger animation
        bool isInCooldown = Time.time - lastTriggerTime < cooldownPeriod;

        // Enhanced debug logging
        if (debugMode)
        {
            Debug.Log($"Emotion Data (Cooldown Mode): {data.emotion} ({data.confidence:F1}%), Faces: {data.faces_detected}, Cooldown: {isInCooldown}");
        }

        if (data.emotion.ToLower() == targetEmotion.ToLower() &&
            data.confidence >= minimumConfidence &&
            !isInCooldown)
        {
            TriggerNeutralAnimation();
            UpdateEmotionIndicator(neutralDetectedColor);
        }
        else if (isInCooldown)
        {
            UpdateEmotionIndicator(cooldownColor);
            float remainingCooldown = cooldownPeriod - (Time.time - lastTriggerTime);
            UpdateStatusText($"Cooldown: {remainingCooldown:F1}s remaining");
        }
        else if (data.emotion.ToLower() == targetEmotion.ToLower() && data.confidence < minimumConfidence)
        {
            UpdateEmotionIndicator(otherEmotionColor);
            UpdateStatusText($"Neutral detected but confidence too low: {data.confidence:F1}%");
        }
        else
        {
            UpdateEmotionIndicator(otherEmotionColor);
            UpdateStatusText($"Waiting for {targetEmotion} emotion... (Current: {data.emotion})");
        }
    }

    void TriggerNeutralAnimation()
    {
        if (animationManager != null)
        {
            animationManager.RequestOverride(SingleTrackAnimationManager.AnimationType.Rumba);
            lastTriggerTime = Time.time;

            UpdateStatusText("Triggered Rumba animation for neutral emotion!");
            Debug.Log($"Triggered Rumba animation at {Time.time:F2} for neutral emotion");
        }
        else
        {
            Debug.LogError("Cannot trigger animation - animationManager is null");
            UpdateStatusText("ERROR: Animation Manager not found");
        }
    }

    void UpdateEmotionDisplay(EmotionData data)
    {
        if (currentEmotionText != null)
        {
            string modeText = useTimestampMode ? " (Timestamp Mode)" : " (Cooldown Mode)";
            currentEmotionText.text = $"Emotion: {data.emotion}\nConfidence: {data.confidence:F1}%\nFaces: {data.faces_detected}{modeText}";
        }
    }

    void UpdateStatusText(string status)
    {
        if (statusText != null)
        {
            statusText.text = $"Status: {status}";
        }
        if (debugMode)
        {
            Debug.Log($"Status: {status}");
        }
    }

    void UpdateEmotionIndicator(Color color)
    {
        if (emotionIndicator != null)
        {
            emotionIndicator.color = color;
        }
    }

    void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            StopPolling();
        }
        else if (currentServerUrl != null)
        {
            StartPolling();
        }
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
            StopPolling();
        }
        else if (currentServerUrl != null)
        {
            StartPolling();
        }
    }

    void OnDestroy()
    {
        StopPolling();
    }

    // Public methods for UI buttons
    public void SetTargetEmotion(string emotion)
    {
        targetEmotion = emotion;
        Debug.Log($"Target emotion changed to: {emotion}");
    }

    public void SetCooldownPeriod(float cooldown)
    {
        cooldownPeriod = cooldown;
        Debug.Log($"Cooldown period changed to: {cooldown}s");
    }

    // NEW: Toggle between timestamp and cooldown modes
    public void SetTimestampMode(bool enabled)
    {
        useTimestampMode = enabled;
        string mode = enabled ? "Timestamp Mode" : "Cooldown Mode";
        Debug.Log($"Switched to: {mode}");
        UpdateStatusText($"Switched to: {mode}");
    }

    // Manual connection test button
    public void TestConnection()
    {
        Debug.Log("Manual connection test requested");
        StartCoroutine(TestAllConnections());
    }

    // NEW: Methods to control timestamp ranges from UI
    public void AddTimestampRange(string startTime, string endTime)
    {
        if (animationManager != null)
        {
            animationManager.AddTimestampRange(startTime, endTime, SingleTrackAnimationManager.AnimationType.Rumba);
            Debug.Log($"Added timestamp range: {startTime}–{endTime}");
        }
    }

    public void ClearTimestampRanges()
    {
        if (animationManager != null)
        {
            animationManager.ClearTimestampRanges();
            Debug.Log("Cleared all timestamp ranges");
        }
    }

        // Add this to EmotionDetectionClient for debugging
    void Update()
    {
        if (debugMode && Time.frameCount % 300 == 0) // Every 5 seconds
        {
            Debug.Log($"Polling Status: isPolling: {isPolling}, connectionAttempts: {connectionAttempts}, currentServerUrl: {currentServerUrl}");
        }
    }
}