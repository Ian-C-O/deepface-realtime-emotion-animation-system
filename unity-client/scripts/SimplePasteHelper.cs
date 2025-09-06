using UnityEngine;
using System.Text.RegularExpressions;

public class SimplePasteHelper : MonoBehaviour
{
    [Header("Just Paste Your List Here!")]
    [TextArea(3, 10)]
    [SerializeField] private string pastedList = "[0:00–0:06, 0:07–0:39, 0:40–0:54, 0:55–1:16]";
    
    [Header("Animation Settings")]
    [SerializeField] private SingleTrackAnimationManager.AnimationType animationType = SingleTrackAnimationManager.AnimationType.Rumba;
    
    [Header("Auto-Apply")]
    [SerializeField] private SingleTrackAnimationManager animationManager;
    
    [Space]
    [Button("Parse List")]
    public bool parseNow = false;
    
    void OnValidate()
    {
        if (parseNow)
        {
            parseNow = false;
            ParsePastedList();
        }
    }
    
    [ContextMenu("Parse Pasted List")]
    public void ParsePastedList()
    {
        if (animationManager == null)
        {
            animationManager = GetComponent<SingleTrackAnimationManager>();
            if (animationManager == null)
            {
                Debug.LogError("No SingleTrackAnimationManager found!");
                return;
            }
        }
        
        Debug.Log($"Parsing list: {pastedList}");
        
        // Clear existing ranges
        animationManager.ClearTimestampRanges();
        
        // Use regex to find all time ranges in format "X:XX–X:XX" or "X:XX-X:XX"
        // This handles: 0:00–0:06, 0:07–0:39, etc.
        string pattern = @"(\d+:\d+)[–\-](\d+:\d+)";
        MatchCollection matches = Regex.Matches(pastedList, pattern);
        
        int addedCount = 0;
        
        foreach (Match match in matches)
        {
            if (match.Groups.Count >= 3)
            {
                string startTime = match.Groups[1].Value;
                string endTime = match.Groups[2].Value;
                
                animationManager.AddTimestampRange(startTime, endTime, animationType);
                addedCount++;
                
                Debug.Log($"Added: {startTime}–{endTime}");
            }
        }
        
        if (addedCount > 0)
        {
            Debug.Log($"Successfully parsed {addedCount} timestamp ranges!");
        }
        else
        {
            Debug.LogWarning("No valid timestamp ranges found. Make sure format is like: 0:06–0:39");
            Debug.LogWarning("Supported formats: 0:06–0:39 or 0:06-0:39");
        }
    }
    
    // Quick test examples
    [ContextMenu("Test Example 1")]
    void TestExample1()
    {
        pastedList = "[0:00–0:06, 0:07–0:39, 0:40–0:54, 0:55–1:16]";
        ParsePastedList();
    }
    
    [ContextMenu("Test Example 2")]
    void TestExample2()
    {
        pastedList = "0:06-0:39, 0:40-0:54, 0:55-1:16, 1:30-1:45";
        ParsePastedList();
    }
    
    [ContextMenu("Test Example 3")]
    void TestExample3()
    {
        pastedList = "Timestamps: [0:15–0:30, 1:00–1:20, 2:30–2:50] for dance moves";
        ParsePastedList();
    }
}

// Custom attribute to make button look better in inspector
public class ButtonAttribute : PropertyAttribute
{
    public string MethodName { get; }
    
    public ButtonAttribute(string methodName)
    {
        MethodName = methodName;
    }
}