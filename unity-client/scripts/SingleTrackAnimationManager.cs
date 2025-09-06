using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Timeline;
using UnityEngine.Playables;
using UnityEngine.UI;

public class SingleTrackAnimationManager : MonoBehaviour
{
    [Header("Timeline Settings")]
    public PlayableDirector playableDirector;
    public TimelineAsset remyTimeline;
    
    [Header("Base Animation")]
    public AnimationClip talkingAnimation;
    
    [Header("Override Animations")]
    public List<AnimationClip> overrideAnimations = new List<AnimationClip>();
    
    [Header("UI Buttons - DISABLED")]
    // Keeping references but disabling functionality, feel free to test these yourself with manual buttons.
    public Button hipHopButton;
    public Button rumbaButton;
    public Button sillyButton;
    
    [Header("Timing Settings")]
    [Range(1f, 10f)]
    public float timestampInterval = 5f;
    public float blendInTime = 0.75f;
    public float blendOutTime = 0.75f;
    public float buttonPressWindow = 0.5f;
    [Range(1f, 10f)]
    public float overrideAnimationDuration = 5f;
    
    [Header("Predefined Timestamp Ranges")]
    [SerializeField] private List<TimestampRange> predefinedRanges = new List<TimestampRange>();
    [SerializeField] private float emotionDetectionWindow = 5f;
    
    [Header("Character Reference")]
    public Animator characterAnimator;
    
    // Private variables
    private AnimationTrack mainTrack;
    private TimelineClip baseTalkingClip;
    private Queue<OverrideRequest> overrideQueue = new Queue<OverrideRequest>();
    private List<double> scheduledOverrides = new List<double>();
    private List<double> emotionTriggeredRanges = new List<double>();
    private bool isInitialized = false;
    
    // Emotion detection tracking
    private bool lastEmotionWasNeutral = false;
    private float lastNeutralDetectionTime = -10f;
    
    [System.Serializable]
    public class TimestampRange
    {
        [Header("Time Range (MM:SS format)")]
        public string startTime = "0:00";
        public string endTime = "0:06";
        
        [HideInInspector] public double startSeconds;
        [HideInInspector] public double endSeconds;
        [HideInInspector] public bool isActive = true;
        
        public void CalculateSeconds()
        {
            startSeconds = ParseTimeString(startTime);
            endSeconds = ParseTimeString(endTime);
        }
        
        private double ParseTimeString(string timeStr)
        {
            try
            {
                string[] parts = timeStr.Split(':');
                if (parts.Length == 2)
                {
                    int minutes = int.Parse(parts[0]);
                    int seconds = int.Parse(parts[1]);
                    return minutes * 60.0 + seconds;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error parsing time string '{timeStr}': {e.Message}");
            }
            return 0.0;
        }
    }
    
    [System.Serializable]
    public class OverrideRequest
    {
        public AnimationClip animationClip;
        public double scheduledTime;
        
        public OverrideRequest(AnimationClip clip, double time)
        {
            animationClip = clip;
            scheduledTime = time;
        }
    }

    // Keep the enum for backward compatibility but won't be used for random selection
    // Feel free to change the names here and use it as a small testing function.
    public enum AnimationType
    {
        HipHop,
        Rumba,
        Silly
    }
    
    void Start()
    {
        InitializeTimestampRanges();
        InitializeTimeline();
        DisableButtons(); // Disable instead of setting up listeners
        StartCoroutine(TimelineUpdateLoop());
    }
    
    void InitializeTimestampRanges()
    {
        if (predefinedRanges.Count == 0)
        {
            predefinedRanges.Add(new TimestampRange { startTime = "0:06", endTime = "0:39" });
            predefinedRanges.Add(new TimestampRange { startTime = "0:40", endTime = "0:54" });
            predefinedRanges.Add(new TimestampRange { startTime = "0:55", endTime = "1:16" });
        }
        
        foreach (var range in predefinedRanges)
        {
            range.CalculateSeconds();
        }
    }
    
    void InitializeTimeline()
    {
        if (playableDirector == null)
        {
            playableDirector = GetComponent<PlayableDirector>();
            if (playableDirector == null)
            {
                playableDirector = gameObject.AddComponent<PlayableDirector>();
            }
        }
        
        if (remyTimeline == null)
        {
            remyTimeline = ScriptableObject.CreateInstance<TimelineAsset>();
            remyTimeline.name = "RemyTimeline";
        }
        
        try
        {
            var existingTracks = new List<TrackAsset>();
            foreach (var track in remyTimeline.GetRootTracks())
            {
                existingTracks.Add(track);
            }
            
            for (int i = existingTracks.Count - 1; i >= 0; i--)
            {
                if (existingTracks[i] != null)
                {
                    remyTimeline.DeleteTrack(existingTracks[i]);
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Error clearing tracks: {e.Message}");
        }
        
        mainTrack = remyTimeline.CreateTrack<AnimationTrack>(null, "Main_Animation_Track");
        
        if (talkingAnimation != null)
        {
            baseTalkingClip = mainTrack.CreateClip(talkingAnimation);
            baseTalkingClip.start = 0;
            baseTalkingClip.duration = 300;
            baseTalkingClip.displayName = "Talking_Base";
            
            var talkingAsset = baseTalkingClip.asset as AnimationPlayableAsset;
            if (talkingAsset != null)
            {
                talkingAsset.clip = talkingAnimation;
            }
        }
        
        if (characterAnimator != null)
        {
            playableDirector.SetGenericBinding(mainTrack, characterAnimator);
        }
        
        playableDirector.playableAsset = remyTimeline;
        playableDirector.Play();
        
        isInitialized = true;
    }
    
    void DisableButtons()
    {
        // Disable buttons instead of removing them completely (you can fiddle with this if want to integrate buttons later)
        if (hipHopButton != null)
            hipHopButton.interactable = false;
        
        if (rumbaButton != null)
            rumbaButton.interactable = false;
        
        if (sillyButton != null)
            sillyButton.interactable = false;
    }
    
    // Get a random animation from the override animations list
    AnimationClip GetRandomOverrideAnimation()
    {
        if (overrideAnimations == null || overrideAnimations.Count == 0)
        {
            return null;
        }
        
        // Filter out null animations
        var validAnimations = overrideAnimations.Where(clip => clip != null).ToList();
        
        if (validAnimations.Count == 0)
        {
            return null;
        }
        
        int randomIndex = Random.Range(0, validAnimations.Count);
        return validAnimations[randomIndex];
    }
    
    public void OnNeutralEmotionDetected()
    {
        lastEmotionWasNeutral = true;
        lastNeutralDetectionTime = Time.time;
        
        CheckForUpcomingTimestamps();
    }
    
    public void OnOtherEmotionDetected(string emotion)
    {
        lastEmotionWasNeutral = false;
    }
    
    void CheckForUpcomingTimestamps()
    {
        if (!isInitialized) return;
        
        double currentTime = playableDirector.time;
        
        foreach (var range in predefinedRanges)
        {
            if (!range.isActive) continue;
            
            double timeUntilRangeStart = range.startSeconds - currentTime;
            
            if (timeUntilRangeStart > 0 && timeUntilRangeStart <= emotionDetectionWindow)
            {
                if (!emotionTriggeredRanges.Contains(range.startSeconds))
                {
                    float timeSinceNeutral = Time.time - lastNeutralDetectionTime;
                    
                    if (lastEmotionWasNeutral && timeSinceNeutral <= 2f)
                    {
                        ScheduleRangeOverride(range);
                        emotionTriggeredRanges.Add(range.startSeconds);
                    }
                }
            }
        }
    }
    
    void ScheduleRangeOverride(TimestampRange range)
    {
        if (!isInitialized) return;
        
        AnimationClip randomAnimation = GetRandomOverrideAnimation();
        if (randomAnimation == null) return;
        
        InsertOverrideClip(randomAnimation, range.startSeconds, overrideAnimationDuration);
        
        if (!scheduledOverrides.Contains(range.startSeconds))
        {
            scheduledOverrides.Add(range.startSeconds);
        }
    }
    
    public void RequestOverride(AnimationType animationType = AnimationType.Rumba)
    {
        if (!isInitialized) return;
        
        AnimationClip randomAnimation = GetRandomOverrideAnimation();
        if (randomAnimation == null) return;
        
        double currentTime = playableDirector.time;
        double nextTimestamp = GetNextAvailableTimestamp(currentTime);
        double timeUntilTimestamp = nextTimestamp - currentTime;
        
        if (timeUntilTimestamp <= buttonPressWindow || IsCurrentlyPlayingOverride())
        {
            InsertOverrideClip(randomAnimation, nextTimestamp, overrideAnimationDuration);
            
            if (!scheduledOverrides.Contains(nextTimestamp))
            {
                scheduledOverrides.Add(nextTimestamp);
            }
        }
    }
    
    // Overload for direct animation clip specification (useful for testing)
    public void RequestOverride(AnimationClip specificAnimation)
    {
        if (!isInitialized || specificAnimation == null) return;
        
        double currentTime = playableDirector.time;
        double nextTimestamp = GetNextAvailableTimestamp(currentTime);
        double timeUntilTimestamp = nextTimestamp - currentTime;
        
        if (timeUntilTimestamp <= buttonPressWindow || IsCurrentlyPlayingOverride())
        {
            InsertOverrideClip(specificAnimation, nextTimestamp, overrideAnimationDuration);
            
            if (!scheduledOverrides.Contains(nextTimestamp))
            {
                scheduledOverrides.Add(nextTimestamp);
            }
        }
    }
    
    double GetNextAvailableTimestamp(double currentTime)
    {
        double nextTimestamp = System.Math.Ceiling(currentTime / timestampInterval) * timestampInterval;
        
        while (scheduledOverrides.Contains(nextTimestamp))
        {
            nextTimestamp += timestampInterval;
        }
        
        return nextTimestamp;
    }
    
    bool IsCurrentlyPlayingOverride()
    {
        double currentTime = playableDirector.time;
        
        foreach (double scheduledTime in scheduledOverrides)
        {
            if (currentTime >= scheduledTime && currentTime <= scheduledTime + overrideAnimationDuration)
            {
                return true;
            }
        }
        
        return false;
    }
    
    IEnumerator TimelineUpdateLoop()
    {
        while (true)
        {
            if (isInitialized)
            {
                CheckForTimestampHits();
                CheckForUpcomingTimestamps();
            }
            
            yield return new WaitForSeconds(0.1f);
        }
    }
    
    void CheckForTimestampHits()
    {
        double currentTime = playableDirector.time;
        
        foreach (double scheduledTime in scheduledOverrides.ToList())
        {
            if (Mathf.Abs((float)(currentTime - scheduledTime)) < 0.1f)
            {
                RefreshTimelinePlayback(currentTime);
                scheduledOverrides.Remove(scheduledTime);
                break;
            }
        }
    }
    
    void RefreshTimelinePlayback(double currentTime)
    {
        if (playableDirector != null)
        {
            double timeToResume = currentTime;
            
            playableDirector.Stop();
            playableDirector.time = timeToResume;
            playableDirector.Play();
        }
    }
    
    void InsertOverrideClip(AnimationClip animationClip, double startTime, double duration)
    {
        if (animationClip != null && mainTrack != null)
        {
            double endTime = startTime + duration;
            
            SplitBaseTalkingClip(startTime, endTime);
            
            var overrideClip = mainTrack.CreateClip(animationClip);
            overrideClip.start = startTime;
            overrideClip.duration = duration;
            overrideClip.displayName = $"{animationClip.name}_Override";
            
            overrideClip.blendInDuration = blendInTime;
            overrideClip.blendOutDuration = blendOutTime;
            
            var animationAsset = overrideClip.asset as AnimationPlayableAsset;
            if (animationAsset != null)
            {
                animationAsset.clip = animationClip;
            }
        }
    }
    
    void InsertOverrideClip(AnimationClip animationClip, double startTime)
    {
        InsertOverrideClip(animationClip, startTime, timestampInterval);
    }
    
    void SplitBaseTalkingClip(double splitStart, double splitEnd)
    {
        if (baseTalkingClip == null || mainTrack == null) return;
        
        try
        {
            double originalStart = baseTalkingClip.start;
            double originalEnd = baseTalkingClip.end;
            
            mainTrack.DeleteClip(baseTalkingClip);
            baseTalkingClip = null;
            
            if (splitStart > originalStart)
            {
                var firstPart = mainTrack.CreateClip(talkingAnimation);
                firstPart.start = originalStart;
                firstPart.duration = (splitStart - originalStart) + blendInTime;
                firstPart.displayName = "Talking_Part1";
                
                var firstPartAsset = firstPart.asset as AnimationPlayableAsset;
                if (firstPartAsset != null)
                {
                    firstPartAsset.clip = talkingAnimation;
                }
            }
            
            var secondPart = mainTrack.CreateClip(talkingAnimation);
            secondPart.start = splitEnd - blendOutTime;
            secondPart.duration = (originalEnd - splitEnd) + blendOutTime;
            secondPart.displayName = "Talking_Part2";
            
            var secondPartAsset = secondPart.asset as AnimationPlayableAsset;
            if (secondPartAsset != null)
            {
                secondPartAsset.clip = talkingAnimation;
                
                double totalElapsedTime = splitEnd - blendOutTime;
                double cycleDuration = talkingAnimation.length;
                double timeInCycle = totalElapsedTime % cycleDuration;
                
                secondPart.clipIn = timeInCycle;
            }
            
            baseTalkingClip = secondPart;
            
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error splitting talking clip: {e.Message}");
        }
    }
    
    public void AddTimestampRange(string startTime, string endTime, AnimationType animationType = AnimationType.Rumba)
    {
        var newRange = new TimestampRange 
        { 
            startTime = startTime, 
            endTime = endTime
        };
        newRange.CalculateSeconds();
        predefinedRanges.Add(newRange);
    }
    
    public void ClearTimestampRanges()
    {
        predefinedRanges.Clear();
        emotionTriggeredRanges.Clear();
    }
}