"""Finger poses for the Rocketbox Biped hand (exec'd by bake_clip.py and pose_clips.py) (WP05).

finger_locals(arm, {"L": pose_name, "R": pose_name}) -> {bone name: Quaternion} local basis rotations (relative to the
bind pose) for the finger bones. Curl axes are built from the hand's anatomy in the bind pose, so the same angles
work for both hands and any wrist orientation:
  F   = segment direction (bone head -> child head)
  K   = index base - pinky base ("knuckle line")
  N   = palm normal = (K x F_hand) * (+1 right, -1 left)
  curl axis = normalize(F x N): a positive angle bends the segment toward the palm.
Angles (degrees) per finger: [proximal, middle, distal]; thumb: [base curl, middle, distal, roll]
(roll = twist of the thumb base about its own axis; negative turns the pad toward the fingers). Thumb values were
chosen by a search that puts the thumb tip in front of / around the curled fingers without intersecting them.
"""
import math
from mathutils import Quaternion, Vector

POSES = {
    # right hand on a pistol grip, index straight along the trigger guard; left hand wrapped around a handguard
    "RifleGrip": {
        "R": {"index": [18, 22, 10], "middle": [72, 82, 48], "ring": [76, 84, 50], "pinky": [80, 82, 50], "thumb": [20, 40, 70, -20]},
        "L": {"index": [48, 58, 36], "middle": [58, 66, 40], "ring": [62, 70, 42], "pinky": [66, 70, 42], "thumb": [20, 30, 70, -20]},
    },
    "PistolGrip": {
        "R": {"index": [18, 22, 10], "middle": [72, 82, 48], "ring": [76, 84, 50], "pinky": [80, 82, 50], "thumb": [20, 40, 70, -20]},
        "L": {"index": [56, 62, 36], "middle": [62, 70, 42], "ring": [66, 72, 44], "pinky": [70, 72, 44], "thumb": [20, 30, 60, -20]},
    },
    "Fist": {
        s: {"index": [86, 100, 62], "middle": [90, 102, 64], "ring": [92, 102, 64], "pinky": [94, 100, 62], "thumb": [10, 50, 60, -20]}
        for s in "LR"
    },
    "Relaxed": {
        s: {"index": [14, 18, 10], "middle": [20, 24, 12], "ring": [26, 28, 14], "pinky": [32, 30, 16], "thumb": [5, 15, 15, 0]}
        for s in "LR"
    },
    "FlatHands": {
        s: {"index": [0, 0, 0], "middle": [0, 0, 0], "ring": [0, 0, 0], "pinky": [0, 0, 0], "thumb": [0, 0, 0, 0]}
        for s in "LR"
    },
}
CHAIN = {"thumb": "0", "index": "1", "middle": "2", "ring": "3", "pinky": "4"}
# THUMB_PLANE: bend direction of the thumb joints in degrees from Kp (toward the little finger) to N (palm normal).
# 90 (bend toward the palm like the fingers) measured best for every grip on the Rocketbox hand; per-pose override
# with "thumb_plane". The old version rolled the thumb about the hand axis (opposition 38-48 deg), which pushed the
# thumb out sideways ('thumbs-up' fists); the roll is now about the thumb's own axis.
THUMB_PLANE = 90.0  # measured best for every grip on the Rocketbox hand (2026-10-16 search, see WP05 handoff)


def _w(arm, bn):
    return arm.matrix_world @ arm.data.bones[bn].matrix_local


def _pos(arm, bn):
    return _w(arm, bn).translation


def finger_locals(arm, sides):
    out = {}
    for side, pose in sides.items():
        if pose in (None, "rest"):
            continue
        P = POSES[pose][side]
        pre = f"Bip01 {side} "
        hand = pre + "Hand"
        if hand not in arm.data.bones:
            continue
        H = _pos(arm, hand)
        F_hand = (_pos(arm, pre + "Finger2") - H).normalized()
        K = (_pos(arm, pre + "Finger1") - _pos(arm, pre + "Finger4")).normalized()
        N = K.cross(F_hand).normalized() * (1 if side == "R" else -1)
        for finger, c in CHAIN.items():
            names = [pre + f"Finger{c}", pre + f"Finger{c}1", pre + f"Finger{c}2"]
            names = [n for n in names if n in arm.data.bones]
            ang = P[finger]
            for j, bn in enumerate(names):
                b = arm.data.bones[bn]
                head = _pos(arm, bn)
                if b.children:
                    F = (_pos(arm, b.children[0].name) - head).normalized()
                else:  # distal: continue the previous segment
                    F = (head - _pos(arm, b.parent.name)).normalized()
                bend = N
                if finger == "thumb":
                    Kp = N.cross(F_hand).normalized()  # toward the little finger
                    t = math.radians(P.get("thumb_plane", THUMB_PLANE))
                    bend = (Kp * math.cos(t) + N * math.sin(t)).normalized()
                axis = F.cross(bend)
                if axis.length < 1e-6:
                    continue
                axis.normalize()
                Rw = Quaternion(axis, math.radians(ang[j]))
                if finger == "thumb" and j == 0 and len(ang) > 3 and ang[3]:
                    # opposition: roll the thumb base about its own direction so the pad turns toward the palm
                    Rw = Rw @ Quaternion(F * (1 if side == "R" else -1), math.radians(ang[3]))
                Rb = _w(arm, bn).to_quaternion()
                out[bn] = Rb.inverted() @ Rw @ Rb
    return out
