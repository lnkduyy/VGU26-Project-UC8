#!/usr/bin/env python3
# -*- coding: utf-8 -*-

"""
Computer Vision Object Detection Node (Requirements R6 & R7):
Subscribes to overhead RGB camera (/camera/color/image_raw/compressed),
performs HSV color segmentation (Red, Yellow, Blue),
calculates contour 3D centroid via calibrated perspective projection,
and publishes detected object pose & classification to MoveIt task state machine:
- Publishes /detected_object_pose (geometry_msgs/Pose)
- Publishes /detected_object_type (std_msgs/String)
- Publishes /camera/color/image_detection/compressed (annotated debug stream)

Project: UC8 - Robotic Arm Conveyor Pick-and-Place with Obstacle Avoidance
Week: Week 07 (Milestone 2 - Computer Vision Perception Pipeline)
"""

import rospy
import cv2
import numpy as np
import time
from geometry_msgs.msg import Pose, Point, Quaternion
from std_msgs.msg import String, Header
from sensor_msgs.msg import CompressedImage


class VisionDetectorNode:
    def __init__(self):
        rospy.init_node("vision_detector_node", anonymous=False)

        rospy.loginfo("==================================================")
        rospy.loginfo("   UC8 - WEEK 07: COMPUTER VISION DETECTOR START  ")
        rospy.loginfo("==================================================")

        # 1. Camera calibration parameters (Overhead Camera mounted at 1.25m altitude)
        # Conveyor pick station center is at ROS: X = -0.60m, Y = 0.0m, Z = 0.05m
        self.camera_center_u = rospy.get_param("~center_u", 320.0)
        self.camera_center_v = rospy.get_param("~center_v", 240.0)
        # Scale factor (meters per pixel at conveyor height): ~1.98 mm/pixel
        self.meters_per_pixel = rospy.get_param("~meters_per_pixel", 0.001985)
        self.base_pick_x = rospy.get_param("~pick_center_x", -0.60)
        self.base_pick_y = rospy.get_param("~pick_center_y",  0.00)
        self.base_pick_z = rospy.get_param("~pick_surface_z", 0.05)

        # Standby orientation (pointing downward onto conveyor)
        self.down_orientation = Quaternion(x=-0.7850, y=-0.6195, z=0.0030, w=0.0012)

        # 2. HSV Color Segmentation Thresholds (calibrated for standard Unity materials)
        # Format: (lower_bound, upper_bound)
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

        # Minimum contour area to ignore background speckles
        self.min_contour_area = rospy.get_param("~min_contour_area", 400.0)

        # 3. Detection gating and rate limiting
        self.pick_tolerance_x = 0.18 # Pick window +/-18cm along conveyor
        self.pick_tolerance_y = 0.18 # Pick window +/-18cm across conveyor
        self.last_reported_time = 0.0
        self.report_cooldown_s = 2.0 # Minimum cooldown before reporting same target
        self.last_detected_color = None

        # 4. Publishers & Subscribers
        self.pose_pub = rospy.Publisher("/detected_object_pose", Pose, queue_size=5)
        self.type_pub = rospy.Publisher("/detected_object_type", String, queue_size=5)
        self.debug_pub = rospy.Publisher("/camera/color/image_detection/compressed", CompressedImage, queue_size=2)

        # Subscribe to compressed image stream
        self.sub = rospy.Subscriber(
            "/camera/color/image_raw/compressed",
            CompressedImage,
            self.image_callback,
            queue_size=1,
            buff_size=2**24
        )

        rospy.loginfo(f"[*] Vision Detector initialized: scale={self.meters_per_pixel*1000:.2f}mm/px, pick_center=({self.base_pick_x:.2f}, {self.base_pick_y:.2f})")

    def image_callback(self, msg):
        try:
            # 1. Decode JPEG compressed image to BGR numpy array
            np_arr = np.frombuffer(msg.data, np.uint8)
            img_bgr = cv2.imdecode(np_arr, cv2.IMREAD_COLOR)
            if img_bgr is None:
                return

            h, w = img_bgr.shape[:2]

            # 2. Convert to HSV color space
            img_hsv = cv2.cvtColor(img_bgr, cv2.COLOR_BGR2HSV)

            # Debug annotation frame
            debug_frame = img_bgr.copy()

            # Draw pick target zone on debug frame
            center_px = int(self.camera_center_u)
            center_py = int(self.camera_center_v)
            cv2.drawMarker(debug_frame, (center_px, center_py), (0, 255, 0), cv2.MARKER_CROSS, 24, 2)
            cv2.putText(debug_frame, "PICK STATION", (center_px - 45, center_py - 16), cv2.FONT_HERSHEY_SIMPLEX, 0.45, (0, 255, 0), 1)

            detected_objects = []

            # 3. Process each color category
            for color_name, ranges in self.hsv_ranges.items():
                # Merge color masks
                mask = None
                for lower, upper in ranges:
                    m = cv2.inRange(img_hsv, lower, upper)
                    mask = m if mask is None else cv2.bitwise_or(mask, m)

                # Morphological noise removal (Opening & Closing)
                kernel = cv2.getStructuringElement(cv2.MORPH_RECT, (5, 5))
                mask_clean = cv2.morphologyEx(mask, cv2.MORPH_OPEN, kernel)
                mask_clean = cv2.morphologyEx(mask_clean, cv2.MORPH_CLOSE, kernel)

                # Find contours
                contours, _ = cv2.findContours(mask_clean, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)

                for cnt in contours:
                    area = cv2.contourArea(cnt)
                    if area < self.min_contour_area:
                        continue

                    # Calculate spatial moments for sub-pixel centroid
                    M = cv2.moments(cnt)
                    if M["m00"] == 0:
                        continue

                    cx_px = M["m10"] / M["m00"]
                    cy_px = M["m01"] / M["m00"]

                    # Perspective mapping to Robot Base Coordinates (FLU frame):
                    # - Pixel displacement in horizontal axis (cx - center_u) maps to ROS -Y
                    # - Pixel displacement in vertical axis (cy - center_v) maps to ROS -X
                    ros_x = self.base_pick_x - (cy_px - self.camera_center_v) * self.meters_per_pixel
                    ros_y = self.base_pick_y - (cx_px - self.camera_center_u) * self.meters_per_pixel
                    ros_z = self.base_pick_z

                    detected_objects.append({
                        "color": color_name,
                        "cx": cx_px,
                        "cy": cy_px,
                        "ros_x": ros_x,
                        "ros_y": ros_y,
                        "ros_z": ros_z,
                        "area": area,
                        "contour": cnt
                    })

                    # Draw visualization bounding box & centroid
                    x_box, y_box, w_box, h_box = cv2.boundingRect(cnt)
                    draw_col = self.draw_colors.get(color_name, (255, 255, 255))
                    cv2.rectangle(debug_frame, (x_box, y_box), (x_box + w_box, y_box + h_box), draw_col, 2)
                    cv2.circle(debug_frame, (int(cx_px), int(cy_px)), 4, (0, 255, 255), -1)

                    label = f"{color_name} ({ros_x:.3f}, {ros_y:.3f})"
                    cv2.putText(debug_frame, label, (x_box, max(15, y_box - 8)), cv2.FONT_HERSHEY_SIMPLEX, 0.45, draw_col, 1)

            # 4. Filter objects currently inside the Pick Target Zone
            valid_picks = []
            for obj in detected_objects:
                dist_x = abs(obj["ros_x"] - self.base_pick_x)
                dist_y = abs(obj["ros_y"] - self.base_pick_y)
                if dist_x <= self.pick_tolerance_x and dist_y <= self.pick_tolerance_y:
                    valid_picks.append(obj)

            # 5. Publish leading pick object to task state machine
            if valid_picks:
                # Sort by distance to pick station center
                valid_picks.sort(key=lambda o: (o["ros_x"] - self.base_pick_x)**2 + (o["ros_y"] - self.base_pick_y)**2)
                target = valid_picks[0]

                now = time.time()
                if (now - self.last_reported_time) > self.report_cooldown_s:
                    self.last_reported_time = now
                    self.last_detected_color = target["color"]

                    # Publish Pose
                    pose_msg = Pose()
                    pose_msg.position = Point(x=target["ros_x"], y=target["ros_y"], z=target["ros_z"])
                    pose_msg.orientation = self.down_orientation
                    self.pose_pub.publish(pose_msg)

                    # Publish Type
                    type_msg = String(data=target["color"])
                    self.type_pub.publish(type_msg)

                    rospy.loginfo(f"[OpenCV Detection] Pick Target: {target['color']} at ROS X={target['ros_x']:.3f}m, Y={target['ros_y']:.3f}m, Z={target['ros_z']:.3f}m (Area: {target['area']:.0f}px)")

            # 6. Publish annotated debug stream for RViz / rqt_image_view
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
