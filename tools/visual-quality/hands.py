"""Optional local anatomy signal. Counts detected hands, never proves anatomy correct."""
import argparse
import json
from pathlib import Path
import sys

parser = argparse.ArgumentParser()
parser.add_argument('--packages', required=True)
parser.add_argument('--model', required=True)
parser.add_argument('--image', required=True)
args = parser.parse_args()
sys.path.insert(0, str(Path(args.packages).resolve(strict=True)))
import mediapipe as mp
from mediapipe.tasks.python import vision
from mediapipe.tasks.python.core.base_options import BaseOptions

options = vision.HandLandmarkerOptions(
    base_options=BaseOptions(model_asset_path=str(Path(args.model).resolve(strict=True))),
    num_hands=12, min_hand_detection_confidence=.5, min_hand_presence_confidence=.5)
with vision.HandLandmarker.create_from_options(options) as detector:
    result = detector.detect(mp.Image.create_from_file(str(Path(args.image).resolve(strict=True))))
    print(json.dumps({'Count': len(result.hand_landmarks), 'Hands': [
        {'X': sum(p.x for p in hand) / len(hand), 'Y': sum(p.y for p in hand) / len(hand)}
        for hand in result.hand_landmarks]}))
