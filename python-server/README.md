# Python Emotion Detection Server

Flask-based emotion detection server that uses DeepFace and OpenCV to analyze facial emotions in real-time via webcam.

## Features

- Real time facial emotion detection using DeepFace
- RESTful API with JSON responses
- Thread safe camera handling for macOS compatibility
- Automatic port fallback (5001 → 5002)
- Performance optimization with frame skipping
- Comprehensive error handling and logging
- Auto recovery from camera disconnections

## Installation

### 1. Create Virtual Environment
```bash
python -m venv facial_emotion_env
source facial_emotion_env/bin/activate  # On Windows: facial_emotion_env\Scripts\activate
```

### 2. Install Dependencies
```bash
pip install -r requirements.txt
```

### 3. Verify Installation
```bash
python diagnostic_test.py
```

## Usage

### Start the Server
```bash
python emotion_detection_server.py
```

Expected output:
```
2025-07-17 10:30:45 - INFO - EmotionDetectionServer initialized
2025-07-17 10:30:45 - INFO - Camera initialized successfully
2025-07-17 10:30:45 - INFO - Face cascade initialized successfully
2025-07-17 10:30:45 - INFO - Camera detection started
2025-07-17 10:30:45 - INFO - Starting Flask server on port 5001
```

### Test the API

**Check server status:**
```bash
curl http://localhost:5001/status
```

**Get current emotion:**
```bash
curl http://localhost:5001/emotion
```

**Example response:**
```json
{
  "emotion": "neutral",
  "confidence": 98.75,
  "timestamp": "2025-07-17T14:29:14.441438",
  "faces_detected": 1,
  "emotion_scores": {
    "angry": 0.40,
    "disgust": 0.00,
    "fear": 0.53,
    "happy": 0.03,
    "neutral": 98.75,
    "sad": 0.28,
    "surprise": 0.00
  }
}
```

## API Endpoints

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/emotion` | GET | Returns current detected emotion data |
| `/status` | GET | Returns server and camera status |
| `/start` | POST | Manually start emotion detection |
| `/stop` | POST | Manually stop emotion detection |

## Configuration

### Server Settings
- **Primary Port**: 5001
- **Fallback Port**: 5002
- **Frame Processing**: Every 3rd frame
- **Camera Resolution**: 640x480 @ 30fps

### Emotion Detection
- **Supported Emotions**: angry, disgust, fear, happy, neutral, sad, surprise
- **Face Detection**: Haar cascade classifier
- **Minimum Face Size**: 50x50 pixels
- **AI Model**: DeepFace with default backend

## Troubleshooting

### Common Issues

**Camera not working:**
```bash
# Test camera access
python diagnostic_test.py
```

**Port already in use (macOS AirPlay issue):**
- Server automatically tries port 5002
- Or manually kill the process: `sudo lsof -ti:5001 | xargs kill -9`

**DeepFace model download fails:**
- Ensure stable internet connection
- Models download automatically on first run
- Check firewall settings

**Low emotion confidence:**
- Ensure good lighting
- Face camera directly
- Remove glasses/masks if possible
- Minimum 50% confidence required

### Performance Issues

**High CPU usage:**
- Increase `process_every_n_frames` in code (default: 3)
- Reduce camera resolution
- Close other camera applications

**Memory leaks:**
- Restart server periodically for long sessions (< 2 hours)
- Monitor with Activity Monitor/Task Manager

## Development

### Enable Debug Mode
Edit `emotion_detection_server.py`:
```python
# Set debug level to DEBUG for verbose logging
logging.basicConfig(level=logging.DEBUG)
```

### Custom Configuration
```python
# Modify these settings in the server initialization
server = EmotionDetectionServer(
    port=5001,
    fallback_port=5002
)
server.process_every_n_frames = 5  # Process fewer frames
```

### Testing
```bash
# Run diagnostic tests
python diagnostic_test.py

# Test individual components
python -c "import cv2; print('OpenCV:', cv2.__version__)"
python -c "from deepface import DeepFace; print('DeepFace imported successfully')"
```

## Shutdown

**Graceful shutdown:**
```
Ctrl+C
```

**Force kill if needed:**
```bash
ps aux | grep emotion_detection_server
kill -9 <process_id>
```

## Logs

Server logs are written to:
- **Console**: Real-time output
- **File**: `emotion_server.log`

Log levels: INFO, WARNING, ERROR, DEBUG
