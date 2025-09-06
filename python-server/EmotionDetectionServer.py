import cv2
import json
import time
import threading
import logging
from datetime import datetime
from flask import Flask, jsonify, request
from flask_cors import CORS
from deepface import DeepFace

# Configure logging
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(levelname)s - %(message)s',
    handlers=[
        logging.FileHandler('emotion_server.log'),
        logging.StreamHandler()
    ]
)
logger = logging.getLogger(__name__)

class EmotionDetectionServer:
    def __init__(self, port=5001, fallback_port=5002):

        self.app = Flask(__name__)
        CORS(self.app)
        
        # Server configuration
        self.port = port
        self.fallback_port = fallback_port
        
        # Camera and detection variables
        self.cap = None
        self.face_cascade = None
        self.detection_running = False
        self.camera_lock = threading.Lock()
        
        # Emotion data
        self.current_emotion_data = {
            'emotion': 'unknown',
            'confidence': 0.0,
            'timestamp': datetime.now().isoformat(),
            'faces_detected': 0
        }
        self.data_lock = threading.Lock()
        
        # Performance optimization
        self.frame_skip_count = 0
        self.process_every_n_frames = 3  # Process every 3rd frame to reduce CPU load
        self.last_successful_detection = time.time()
        
        # Setup routes
        self.setup_routes()
        logger.info("EmotionDetectionServer initialized")
    
    def setup_routes(self):

        # Setup Flask routes
        @self.app.route('/emotion', methods=['GET'])
        def get_emotion():

            # Return current emotion data
            try:
                with self.data_lock:
                    return jsonify(self.current_emotion_data)
            except Exception as e:
                logger.error(f"Error in /emotion endpoint: {e}")
                return jsonify({
                    'error': 'Failed to get emotion data',
                    'emotion': 'error',
                    'confidence': 0.0,
                    'timestamp': datetime.now().isoformat()
                }), 500
        
        @self.app.route('/status', methods=['GET'])
        def get_status():

            # Return server status
            try:
                camera_status = self.cap is not None and self.cap.isOpened() if self.cap else False
                return jsonify({
                    'server_running': True,
                    'detection_running': self.detection_running,
                    'camera_connected': camera_status,
                    'last_detection': self.last_successful_detection,
                    'timestamp': datetime.now().isoformat()
                })
            except Exception as e:
                logger.error(f"Error in /status endpoint: {e}")
                return jsonify({'error': 'Failed to get status'}), 500
        
        @self.app.route('/start', methods=['POST'])
        def start_detection():

            # Start emotion detection
            try:
                if self.start_camera_detection():
                    return jsonify({'message': 'Detection started successfully'})
                else:
                    return jsonify({'error': 'Failed to start detection'}), 500
            except Exception as e:
                logger.error(f"Error starting detection: {e}")
                return jsonify({'error': str(e)}), 500
        
        @self.app.route('/stop', methods=['POST'])
        def stop_detection():

            # Stop emotion detection
            try:
                self.stop_camera_detection()
                return jsonify({'message': 'Detection stopped successfully'})
            except Exception as e:
                logger.error(f"Error stopping detection: {e}")
                return jsonify({'error': str(e)}), 500
    
    def initialize_camera(self):

        # Initialize camera with error handling
        try:
            with self.camera_lock:
                if self.cap is not None:
                    self.cap.release()
                
                self.cap = cv2.VideoCapture(0)
                if not self.cap.isOpened():
                    logger.error("Failed to open camera")
                    return False
                
                # Set camera properties for better performance
                self.cap.set(cv2.CAP_PROP_FRAME_WIDTH, 640)
                self.cap.set(cv2.CAP_PROP_FRAME_HEIGHT, 480)
                self.cap.set(cv2.CAP_PROP_FPS, 30)
                
                logger.info("Camera initialized successfully")
                return True
                
        except Exception as e:
            logger.error(f"Error initializing camera: {e}")
            return False
    
    def initialize_face_cascade(self):

        # Initialize face detection cascade
        try:
            self.face_cascade = cv2.CascadeClassifier(
                cv2.data.haarcascades + 'haarcascade_frontalface_default.xml'
            )
            if self.face_cascade.empty():
                logger.error("Failed to load face cascade classifier")
                return False
            
            logger.info("Face cascade initialized successfully")
            return True
            
        except Exception as e:
            logger.error(f"Error initializing face cascade: {e}")
            return False
    
    def start_camera_detection(self):

        # Start camera detection in a separate thread
        if self.detection_running:
            logger.warning("Detection already running")
            return True
        
        # Initialize components
        if not self.initialize_face_cascade():
            return False
        
        if not self.initialize_camera():
            return False
        
        # Start detection thread
        self.detection_running = True
        detection_thread = threading.Thread(target=self.detection_loop, daemon=True)
        detection_thread.start()
        
        logger.info("Camera detection started")
        return True
    
    def stop_camera_detection(self):

        # Stop camera detection 
        self.detection_running = False
        
        with self.camera_lock:
            if self.cap is not None:
                self.cap.release()
                self.cap = None
        
        logger.info("Camera detection stopped")
    
    def detection_loop(self):

        # Main detection loop, runs in separate thread
        logger.info("Detection loop started")
        
        while self.detection_running:
            try:
                # Skip frames for performance
                self.frame_skip_count += 1
                if self.frame_skip_count < self.process_every_n_frames:
                    time.sleep(0.033)  # ~30 FPS
                    continue
                
                self.frame_skip_count = 0
                
                # Capture frame
                with self.camera_lock:
                    if self.cap is None or not self.cap.isOpened():
                        logger.error("Camera not available")
                        break
                    
                    ret, frame = self.cap.read()
                
                if not ret:
                    logger.warning("Failed to capture frame")
                    continue
                
                # Process frame
                self.process_frame(frame)
                
            except Exception as e:
                logger.error(f"Error in detection loop: {e}")
                time.sleep(1)  # Brief pause before retrying
        
        logger.info("Detection loop ended")
    
    def convert_numpy_types(self, obj):

        # Convert numpy types to Python native types for JSON serialization
        import numpy as np
        
        if isinstance(obj, np.floating):
            return float(obj)
        elif isinstance(obj, np.integer):
            return int(obj)
        elif isinstance(obj, np.ndarray):
            return obj.tolist()
        elif isinstance(obj, dict):
            return {key: self.convert_numpy_types(value) for key, value in obj.items()}
        elif isinstance(obj, list):
            return [self.convert_numpy_types(item) for item in obj]
        else:
            return obj

    def process_frame(self, frame):

        # Process a single frame for emotion detection
        try:
            # Convert to grayscale for face detection
            gray_frame = cv2.cvtColor(frame, cv2.COLOR_BGR2GRAY)
            
            # Detect faces
            faces = self.face_cascade.detectMultiScale(
                gray_frame, 
                scaleFactor=1.1, 
                minNeighbors=5, 
                minSize=(50, 50)  # Increased minimum size for better detection
            )
            
            if len(faces) == 0:
                # No faces detected
                with self.data_lock:
                    self.current_emotion_data = {
                        'emotion': 'no_face',
                        'confidence': 0.0,
                        'timestamp': datetime.now().isoformat(),
                        'faces_detected': 0
                    }
                return
            
            # Process the largest face (most prominent)
            largest_face = max(faces, key=lambda face: face[2] * face[3])
            x, y, w, h = largest_face
            
            # Extract face ROI
            face_roi = frame[y:y+h, x:x+w]
            
            # Convert to RGB for DeepFace
            face_rgb = cv2.cvtColor(face_roi, cv2.COLOR_BGR2RGB)
            
            # Analyze emotion
            result = DeepFace.analyze(
                face_rgb, 
                actions=['emotion'], 
                enforce_detection=False,
                silent=True
            )
            
            # Extract emotion data
            if result and len(result) > 0:
                emotion_scores = result[0]['emotion']
                dominant_emotion = result[0]['dominant_emotion']
                confidence = emotion_scores[dominant_emotion]
                
                # Convert numpy types to native Python types for JSON serialization
                emotion_scores_converted = self.convert_numpy_types(emotion_scores)
                confidence_converted = self.convert_numpy_types(confidence)
                
                # Update emotion data
                with self.data_lock:
                    self.current_emotion_data = {
                        'emotion': str(dominant_emotion),
                        'confidence': confidence_converted,
                        'timestamp': datetime.now().isoformat(),
                        'faces_detected': int(len(faces)),
                        'emotion_scores': emotion_scores_converted  # Include all emotion scores
                    }
                
                self.last_successful_detection = time.time()
                logger.debug(f"Detected: {dominant_emotion} ({confidence_converted:.1f}%)")
            
        except Exception as e:
            logger.error(f"Error processing frame: {e}")
            # Set error state
            with self.data_lock:
                self.current_emotion_data = {
                    'emotion': 'error',
                    'confidence': 0.0,
                    'timestamp': datetime.now().isoformat(),
                    'faces_detected': 0,
                    'error': str(e)
                }
    
    def run_server(self):

        # Run the Flask server
        try:
            logger.info(f"Starting Flask server on port {self.port}")
            self.app.run(host='0.0.0.0', port=self.port, debug=False, threaded=True)
        except OSError as e:
            if "Address already in use" in str(e):
                logger.warning(f"Port {self.port} in use, trying fallback port {self.fallback_port}")
                try:
                    self.app.run(host='0.0.0.0', port=self.fallback_port, debug=False, threaded=True)
                except Exception as fallback_error:
                    logger.error(f"Failed to start server on fallback port: {fallback_error}")
                    raise
            else:
                raise
        except Exception as e:
            logger.error(f"Failed to start server: {e}")
            raise
    
    def shutdown(self):

        # Graceful shutdown of the server
        logger.info("Shutting down emotion detection server")
        self.stop_camera_detection()

def main():

    server = None
    try:
        server = EmotionDetectionServer()
        
        # Auto-start detection
        if server.start_camera_detection():
            logger.info("Auto-started camera detection")
        else:
            logger.warning("Failed to auto-start camera detection")
        
        # Run server
        server.run_server()
        
    except KeyboardInterrupt:
        logger.info("Received keyboard interrupt")
    except Exception as e:
        logger.error(f"Server error: {e}")
    finally:
        if server:
            server.shutdown()

if __name__ == "__main__":
    main()