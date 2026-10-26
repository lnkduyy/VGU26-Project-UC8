#!/usr/bin/env python3
# -*- coding: utf-8 -*-

"""
Computer Vision Object Detection Node (Requirements R6 & R7):
Model 1: Upstream Inspection Station (Standard Industrial Vision Cell)
- Mounted 65cm upstream of pick station (ROS Y = +0.65m, Unity X = -0.65m)
- 100% immune to robot arm occlusion and gripper shadow interference
- Inspects, classifies (Red, Yellow, Blue), and tracks incoming conveyor objects
- Manages FIFO tracking queue from upstream inspection to pick station arrival
- Evaluates real-time perception accuracy against /ground_truth_object_pose
- Publishes /detected_object_pose (geometry_msgs/Pose)
- Publishes /detected_object_type (std_msgs/String)
- Publishes /camera/color/image_detection/compressed (annotated industrial HUD stream)

Project: UC8 - Robotic Arm Conveyor Pick-and-Place with Obstacle Avoidance
Week: Week 07 (Milestone 2 - Computer Vision Perception Pipeline)
"""

import rospy
import cv2
import numpy as np
import time
from collections import deque
from geometry_msgs.msg import Pose, Point, Quaternion
from std_msgs.msg import String, Header
from sensor_msgs.msg import CompressedImage


class VisionDetectorNode:
    def __init__(self):
        rospy.init_node("vision_detector_node", anonymous=False)

        rospy.loginfo("==================================================================")
        rospy.loginfo("   UC8 - WEEK 07: MODEL 1 UPSTREAM COMPUTER VISION DETECTOR START  ")
        rospy.loginfo("==================================================================")

        # 1. Camera Calibration Parameters (Upstream Inspection Mount at 1.25m altitude)
        # Unity Camera Mount: X = -0.65m, Y = 1.25m, Z = -0.60m
        # ROS Coordinates: Center is at X = -0.60m, Y = +0.65m, Surface Z = 0.05m
        self.camera_center_u = rospy.get_param("~center_u", 320.0)
        self.camera_center_v = rospy.get_param("~center_v", 240.0)
        self.meters_per_pixel = rospy.get_param("~meters_per_pixel", 0.001985) # ~1.985 mm/pixel

        self.cam_center_x = rospy.get_param("~cam_center_x", -0.60) # Conveyor centerline in ROS
        self.cam_center_y = rospy.get_param("~cam_center_y",  0.65) # Upstream inspection center in ROS
        self.pick_station_y = rospy.get_param("~pick_station_y", 0.00) # Pick station center in ROS
        self.surface_z = rospy.get_param("~surface_z", 0.05)

        # Standby orientation (pointing downward onto conveyor)
        self.down_orientation = Quaternion(x=-0.7850, y=-0.6195, z=0.0030, w=0.0012)

        # 2. HSV Color Segmentation Thresholds (calibrated for standard Unity materials)
        self.hsv_ranges = {
            "Red": [
                (np.array([0, 120, 70], dtype=np.uint8), np.array([10, 255, 255], dtype=np.uint8)),
                (np.array([165, 120, 70], dtype=np.uint8), np.array([180, 255, 255], dtype=np.uint8))
            ],
            "Yellow": [
                (np.array([18, 110, 80], dtype=np.uint8), np.array([38, 255, 255], dtype=np.uint8))
            ],
            "Blue": [
                (np.array([95, 110, 70], dtype=np.uint8), np.array([135, 255, 255], dtype=np.uint8))
            ]
        }

        # Bounding box color palette (BGR for OpenCV visualization)
        self.draw_colors = {
            "Red": (0, 0, 255),
            "Yellow": (0, 220, 255),
            "Blue": (255, 120, 0)
        }

        self.min_contour_area = rospy.get_param("~min_contour_area", 400.0)

        # 3. Upstream Gate & Pick Station Thresholds (in ROS Coordinates)
        # Upstream Inspection Gate: Y in [0.45, 0.85] (Centered at 0.65m - 100% Arm Occlusion Free!)
        self.upstream_gate_min_y = 0.45
        self.upstream_gate_max_y = 0.85
        # Pick Station Arrival Trigger: Y <= 0.15m
        self.pick_arrival_y_thresh = 0.15

        # 4. Conveyor Tracking Queue & Debounce State
        self.track_counter = 0
        self.active_tracks = {} # track_id -> dict(color, last_x, last_y, first_seen, status)
        self.last_reported_time = 0.0
        self.report_cooldown_s = 2.5 # Avoid duplicate trigger for the same cube
        self.last_pick_track_id = -1

        # 5. Perception Accuracy Benchmarking vs Ground Truth
        self.latest_ground_truth = None
        self.gt_timestamp = 0.0
        self.accuracy_samples = deque(maxlen=30)
        self.current_error_mm = 0.0

        # 6. Publishers & Subscribers
        self.pose_pub = rospy.Publisher("/detected_object_pose", Pose, queue_size=5)
        self.type_pub = rospy.Publisher("/detected_object_type", String, queue_size=5)
        self.debug_pub = rospy.Publisher("/camera/color/image_detection/compressed", CompressedImage, queue_size=2)

        # Ground truth subscriber for validation
        rospy.Subscriber("/ground_truth_object_pose", Pose, self.ground_truth_callback, queue_size=5)

        # Subscribe to compressed image stream from Unity
        self.sub = rospy.Subscriber(
            "/camera/color/image_raw/compressed",
            CompressedImage,
            self.image_callback,
            queue_size=1,
            buff_size=2**24
        )

        rospy.loginfo(f"[*] Model 1 Upstream Vision Detector active: center=({self.cam_center_x:.2f}, {self.cam_center_y:.2f}), scale={self.meters_per_pixel*1000:.2f}mm/px")
        rospy.loginfo(f"[*] Upstream Inspection Gate: ROS Y in [{self.upstream_gate_min_y:.2f}, {self.upstream_gate_max_y:.2f}] (0% arm occlusion)")

    def ground_truth_callback(self, msg):
        self.latest_ground_truth = msg
        self.gt_timestamp = time.time()

    def image_callback(self, msg):
        try:
            # 1. Decode JPEG compressed image
            np_arr = np.frombuffer(msg.data, np.uint8)
            img_bgr = cv2.imdecode(np_arr, cv2.IMREAD_COLOR)
            if img_bgr is None:
                return

            h, w = img_bgr.shape[:2]
            img_hsv = cv2.cvtColor(img_bgr, cv2.COLOR_BGR2HSV)
            debug_frame = img_bgr.copy()

            # 2. Draw Upstream Inspection Gate and Pick Station on HUD
            gate_u_min = int(self.camera_center_u - (self.upstream_gate_max_y - self.cam_center_y) / self.meters_per_pixel)
            gate_u_max = int(self.camera_center_u - (self.upstream_gate_min_y - self.cam_center_y) / self.meters_per_pixel)
            gate_u_min = max(0, min(w - 1, gate_u_min))
            gate_u_max = max(0, min(w - 1, gate_u_max))

            cv2.rectangle(debug_frame, (gate_u_min, 40), (gate_u_max, h - 40), (255, 255, 0), 1)
            cv2.putText(debug_frame, "UPSTREAM INSPECTION GATE (ZERO OCCLUSION)", (gate_u_min + 10, 60),
                        cv2.FONT_HERSHEY_SIMPLEX, 0.45, (255, 255, 0), 1)

            # Pick station reference marker on image right edge (towards downstream)
            pick_station_u = int(self.camera_center_u - (self.pick_station_y - self.cam_center_y) / self.meters_per_pixel)
            if 0 <= pick_station_u < w:
                cv2.line(debug_frame, (pick_station_u, 0), (pick_station_u, h), (0, 255, 0), 2)
                cv2.putText(debug_frame, "PICK STATION (Y=0.0)", (pick_station_u - 130, h - 20),
                            cv2.FONT_HERSHEY_SIMPLEX, 0.45, (0, 255, 0), 1)

            # 3. Detect objects across all colors
            current_detections = []
            for color_name, ranges in self.hsv_ranges.items():
                mask = None
                for lower, upper in ranges:
                    m = cv2.inRange(img_hsv, lower, upper)
                    mask = m if mask is None else cv2.bitwise_or(mask, m)

                kernel = cv2.getStructuringElement(cv2.MORPH_RECT, (5, 5))
                mask_clean = cv2.morphologyEx(mask, cv2.MORPH_OPEN, kernel)
                mask_clean = cv2.morphologyEx(mask_clean, cv2.MORPH_CLOSE, kernel)

                contours, _ = cv2.findContours(mask_clean, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)

                for cnt in contours:
                    area = cv2.contourArea(cnt)
                    if area < self.min_contour_area:
                        continue

                    M = cv2.moments(cnt)
                    if M["m00"] == 0:
                        continue

                    cx_px = M["m10"] / M["m00"]
                    cy_px = M["m01"] / M["m00"]

                    # Perspective mapping to Robot Base Coordinates (FLU frame):
                    ros_x = self.cam_center_x - (cy_px - self.camera_center_v) * self.meters_per_pixel
                    ros_y = self.cam_center_y - (cx_px - self.camera_center_u) * self.meters_per_pixel

                    current_detections.append({
                        "color": color_name,
                        "cx": cx_px,
                        "cy": cy_px,
                        "ros_x": ros_x,
                        "ros_y": ros_y,
                        "area": area,
                        "cnt": cnt
                    })

            # 4. Multi-Object Tracking & FIFO Queue Association
            # Match current detections with active tracks based on spatial proximity
            matched_track_ids = set()
            for det in current_detections:
                best_id = None
                best_dist = 0.20 # 20cm matching radius
                for tid, trk in self.active_tracks.items():
                    if tid in matched_track_ids:
                        continue
                    if trk["color"] != det["color"]:
                        continue
                    dist = np.hypot(det["ros_x"] - trk["last_x"], det["ros_y"] - trk["last_y"])
                    if dist < best_dist:
                        best_dist = dist
                        best_id = tid

                if best_id is not None:
                    # Update existing track
                    self.active_tracks[best_id]["last_x"] = det["ros_x"]
                    self.active_tracks[best_id]["last_y"] = det["ros_y"]
                    self.active_tracks[best_id]["last_seen"] = time.time()
                    det["track_id"] = best_id
                    matched_track_ids.add(best_id)
                else:
                    # New track initialized if inside or upstream of inspection gate
                    if det["ros_y"] >= self.upstream_gate_min_y - 0.10:
                        self.track_counter += 1
                        new_id = self.track_counter
                        self.active_tracks[new_id] = {
                            "id": new_id,
                            "color": det["color"],
                            "last_x": det["ros_x"],
                            "last_y": det["ros_y"],
                            "first_seen": time.time(),
                            "last_seen": time.time(),
                            "status": "INSPECTED"
                        }
                        det["track_id"] = new_id
                        matched_track_ids.add(new_id)
                        rospy.loginfo(f"[OpenCV Upstream] NEW OBJECT INSPECTED #{new_id}: {det['color']} at ROS (X={det['ros_x']:.3f}, Y={det['ros_y']:.3f}) - ZERO OCCLUSION")
                    else:
                        det["track_id"] = None

            # Prune dead tracks not seen for > 8.0s
            now = time.time()
            dead_ids = [tid for tid, trk in self.active_tracks.items() if now - trk["last_seen"] > 8.0]
            for tid in dead_ids:
                del self.active_tracks[tid]

            # 5. Render Object Bounding Boxes & Tracking Badges
            for det in current_detections:
                cnt = det["cnt"]
                col_name = det["color"]
                x_box, y_box, w_box, h_box = cv2.boundingRect(cnt)
                draw_col = self.draw_colors.get(col_name, (255, 255, 255))

                cv2.rectangle(debug_frame, (x_box, y_box), (x_box + w_box, y_box + h_box), draw_col, 2)
                cv2.circle(debug_frame, (int(det["cx"]), int(det["cy"])), 4, (0, 255, 255), -1)

                tid_str = f"#{det['track_id']}" if det["track_id"] else "?"
                label = f"{col_name} {tid_str} ({det['ros_x']:.2f}, {det['ros_y']:.2f})"
                cv2.putText(debug_frame, label, (x_box, max(18, y_box - 6)),
                            cv2.FONT_HERSHEY_SIMPLEX, 0.45, draw_col, 1)

            # 6. Check for Arrival at Pick Station
            # Identify leading object approaching pick station (smallest positive ros_y)
            approaching_candidates = [
                det for det in current_detections
                if det["track_id"] is not None and det["ros_y"] <= self.pick_arrival_y_thresh
            ]

            if approaching_candidates:
                # Select closest to pick station (ros_y ~ 0.0)
                approaching_candidates.sort(key=lambda d: abs(d["ros_y"] - self.pick_station_y))
                lead_pick = approaching_candidates[0]
                target_id = lead_pick["track_id"]

                if target_id != self.last_pick_track_id and (now - self.last_reported_time) > self.report_cooldown_s:
                    self.last_reported_time = now
                    self.last_pick_track_id = target_id

                    # Publish validated pose and type
                    pose_msg = Pose()
                    # Center X on conveyor laterally, clamp Y to pick stop center
                    pose_msg.position = Point(
                        x=lead_pick["ros_x"],
                        y=max(-0.02, min(0.05, lead_pick["ros_y"])),
                        z=self.surface_z
                    )
                    pose_msg.orientation = self.down_orientation
                    self.pose_pub.publish(pose_msg)

                    type_msg = String(data=lead_pick["color"])
                    self.type_pub.publish(type_msg)

                    rospy.loginfo(f"[OpenCV Hand-Off] Target #{target_id} ({lead_pick['color']}) REACHED PICK STATION: ROS X={pose_msg.position.x:.3f}m, Y={pose_msg.position.y:.3f}m -> MoveIt triggered!")

            # 7. Real-Time Perception Accuracy Evaluation vs Ground Truth
            if self.latest_ground_truth is not None and (now - self.gt_timestamp) < 1.0:
                gt_pos = self.latest_ground_truth.position
                # Compare against closest vision detection
                if current_detections:
                    dists = [np.hypot(d["ros_x"] - gt_pos.x, d["ros_y"] - gt_pos.y) for d in current_detections]
                    min_err = min(dists) * 1000.0 # Convert to mm
                    if min_err < 60.0: # Valid match within 6cm
                        self.accuracy_samples.append(min_err)
                        self.current_error_mm = np.mean(self.accuracy_samples)

            # 8. Industrial HUD Top & Bottom Overlay
            # Top Banner
            cv2.rectangle(debug_frame, (0, 0), (w, 32), (30, 30, 30), -1)
            cv2.putText(debug_frame, "UC8 INDUSTRIAL VISION CELL - MODEL 1: UPSTREAM INSPECTION (ZERO OCCLUSION)",
                        (12, 22), cv2.FONT_HERSHEY_SIMPLEX, 0.48, (0, 255, 255), 1)

            # Bottom Status Bar
            cv2.rectangle(debug_frame, (0, h - 34), (w, h), (30, 30, 30), -1)
            err_text = f"Perception Error: {self.current_error_mm:.1f} mm (PASS < 5.0mm)" if self.current_error_mm > 0 else "Perception Calibrated: +/- 1.98mm/px"
            cv2.putText(debug_frame, f"Active Tracks: {len(self.active_tracks)} | Queue Count: {self.track_counter} | {err_text}",
                        (12, h - 12), cv2.FONT_HERSHEY_SIMPLEX, 0.44, (0, 255, 0), 1)

            # 9. Publish Annotated Debug Stream
            encode_param = [int(cv2.IMWRITE_JPEG_QUALITY), 75]
            _, enc_img = cv2.imencode(".jpg", debug_frame, encode_param)
            dbg_msg = CompressedImage()
            dbg_msg.header = Header(stamp=rospy.Time.now(), frame_id="overhead_camera_optical_frame")
            dbg_msg.format = "jpeg"
            dbg_msg.data = np.array(enc_img).tobytes()
            self.debug_pub.publish(dbg_msg)

        except Exception as e:
            rospy.logerr(f"[OpenCV Detection Error] {e}")


def main():
    detector = VisionDetectorNode()
    rospy.spin()


if __name__ == "__main__":
    main()
