
# Unity Emotion Detection Client

Unity scripts that connect to Python emotion detection server and trigger random animations at predefined timestamps based on facial emotion detection.

## Requirements

* Unity 2020.3.48f1+
* Timeline package
* Python emotion detection server

## Installation

### Scripts
Copy to `Assets/Scripts/`:
* `EmotionDetectionClient.cs`
* `SingleTrackAnimationManager.cs` 
* `SimplePasteHelper.cs`

### Setup
1. Create Timeline asset with Animation Track
2. Add `SingleTrackAnimationManager` to GameObject
3. Configure:
   * **Base Animation**: Looping talking animation
   * **Override Animations**: List of clips for random selection
   * **Character Animator**: Your character's Animator component
4. Add `EmotionDetectionClient` component:
   * **Server URL**: `http://localhost:5001/emotion`
   * **Use Timestamp Mode**: Checked
   * **Animation Manager**: Reference to SingleTrackAnimationManager

## Configuration

### Timestamp Import
**Method 1 (Bulk)**: Add `SimplePasteHelper`, paste timestamps like `[0:06–0:39, 0:40–0:54]`, check "Parse Now"

**Method 2 (Manual)**: Enter ranges individually in inspector

### Settings
* **Target Emotion**: `neutral` (default)
* **Minimum Confidence**: `50%`
* **Override Duration**: `5 seconds`
* **Detection Window**: `5 seconds before timestamp`

## How It Works

1. System detects neutral emotion 5 seconds before each timestamp
2. Random animation from override list triggers at exact timestamp
3. Plays for 5 seconds then returns to talking animation

Timeline: `Talking → Random Override (5s) → Talking`

## UI Indicators

* **Green**: Animation triggered
* **Blue**: Cooldown active  
* **Yellow**: Other emotion/low confidence
* **Red**: Connection error

## Troubleshooting

**Connection Issues**:
* Verify Python server running on port 5001
* Check fallback URLs in console logs

**Animation Issues**:
* Ensure override animations list populated
* Verify timestamp mode enabled
* Check neutral emotion detected 5+ seconds before timestamps

**Performance**: Reduce poll interval from 0.5s to 1.0s if needed

## API

Server returns:
```json
{
  "emotion": "neutral",
  "confidence": 98.75,
  "faces_detected": 1
}
```

## Development

```csharp
// Change target emotion
emotionClient.SetTargetEmotion("happy");

// Manual trigger
animationManager.RequestOverride();

// Add timestamp range
animationManager.AddTimestampRange("1:30", "1:45");
```

Enable **Debug Mode** for detailed console logging.
