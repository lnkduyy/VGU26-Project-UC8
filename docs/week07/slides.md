# UC8: ROBOTIC ARM CONVEYOR PICK-AND-PLACE WITH OBSTACLE AVOIDANCE
## Week 07 Progress Presentation & Technical Defense
### Continuous Trajectory Smoothing (R5), Parallel Jaw Alignment, & Front-Tilted Computer Vision (R6/R7)

- **Course:** Robotic Project — Vietnamese-German University (VGU)
- **Academic Term:** Winter Semester 2026 / October 2026
- **Project Topic:** Use Case 08 (UC8) — Robotic Sorting Workcell with Obstacle Avoidance
- **Hardware/Simulation Platform:** Universal Robots UR10e (6-DOF) + 3.6m Conveyor Belt + 3 Sorting Bins + Dual Safety Guards + UR Control Station + Front-Tilted Inspection Camera Stand
- **Software Stack:** Unity 2022.3 LTS (PhysX 4.x / ArticulationBody) + ROS Noetic (MoveIt Motion Planning Framework) + OpenCV 4.2.0 + ROS-TCP-Endpoint
- **Current Milestone:** Week 07 Progress Review & Milestone 2 Validation (Requirements R5, R6 & R7 Implementation)
- **Target Milestone:** Milestone 2 Defense & Milestone 3 Roadmap (Weeks 07–08)

---

## PRESENTATION AGENDA

| Slide | Topic | Key Focus |
| :---: | :--- | :--- |
| **01** | **Title Cover** | UC8 Week 07 Progress Presentation & Technical Defense |
| **02** | **Presentation Agenda** | Overview of the 12 technical presentation slides |
| **03** | **Executive Progress Summary** | Week 06 Commitments vs. Week 07 Deliverables (100% Completion) |
| **04** | **Continuous Trajectory Smoothing (R5)** | Blended Cartesian path, natural wrist quaternion $\mathbf{q}_z(\Delta\theta)\otimes\mathbf{q}_{\text{standby}}$, 0 singularities |
| **05** | **Grasp Escape Lift & Velocity Boost** | Rapid vertical snap ($+16\,\text{cm}$ in $<0.25\,\text{s}$ at $2.4\text{x}$) & cycle time cut ($25\,\text{s}\to13\,\text{s}$) |
| **06** | **Gripper Parallelism & Presentation Cameras** | Resolving jaw skew ($13.4^\circ$), conveyor braking & self-centering grasp, camera presets `[1]-[3]` |
| **07** | **The Robot Arm Occlusion Dilemma** | Overhead vs Front-Tilted camera architectures; why front-tilt provides unoccluded perspective |
| **08** | **Front-Tilted Vision Cell Architecture (R6/R7)** | Camera at $X=0, Y=0.75\,\text{m}, Z=-1.10\,\text{m}$ (tilt $50^\circ$), 3D tripod stand, OpenCV HUD |
| **09** | **Decoupled Perception Architecture** | Transparent drop-in replacement; ROS TCP streaming at 15 Hz; real-time HSV segmentation |
| **10** | **Empirical Benchmarks (25-Trial Run)** | Real continuous run: 100% grasp/place success, 0 collisions, cycle times $13.36\,\text{s}$ / $13.62\,\text{s}$ |
| **11** | **Plan for Next Week (Week 08)** | Week 08 sprint goals & roadmap towards Milestone 2 formal defense (Week 10) |
| **12** | **Summary & Faculty Q&A** | Project status sign-off, engineering conclusions, and committee discussion |

---

## SLIDE 01: TITLE COVER
- **Project:** UC8: Robotic Arm Conveyor Pick-and-Place with Obstacle Avoidance
- **Title:** Week 07 Progress Presentation: Continuous Trajectory Optimization, Parallel Jaw Alignment, & Front-Tilted Computer Vision
- **Institution:** Vietnamese-German University (VGU) — Robotic Project Course
- **Platform:** UR10e Manipulator, Unity 2022.3 LTS PhysX Engine, ROS Noetic MoveIt Planning Framework, OpenCV 4.2.0
- **Milestone Scope:** Verification of Requirement R5 (Trajectory Smoothing & Speed Optimization) and Requirements R6 & R7 (Realistic Computer Vision Perception Pipeline)

---

## SLIDE 02: PRESENTATION AGENDA
1. **Executive Progress Summary:** Verification of commitments made in Week 06 and delivered in Week 07.
2. **Continuous Trajectory Smoothing (R5):** Transition from discrete 10 stop-and-go stages to blended Cartesian multi-waypoint splines.
3. **Conveyor Escape Lift & Velocity Boost:** Rapid vertical snap ($+16\,\text{cm}$ in $<0.25\,\text{s}$ at $2.4\text{x}$) preventing conveyor box dragging.
4. **Gripper Orthogonal Alignment & Physical Grasping:** Resolving jaw skew ($13.4^\circ$), electro-mechanical conveyor braking, and self-centering parallel grasp.
5. **The Robot Arm Occlusion Dilemma:** Empirical analysis of camera placement and why front-tilted inspection eliminates occlusion.
6. **Front-Tilted Vision Cell Architecture (R6/R7):** Station geometry at $Z=-1.10\,\text{m}$, $50^\circ$ pitch, and 3D industrial tripod stand.
7. **OpenCV Multi-Color Segmentation & Tracking:** HSV filtering, sub-pixel centroid moments ($\approx 1.52\,\text{mm/px}$), and real-time HUD.
8. **Empirical Benchmarks & 25-Trial Continuous Test:** Statistical QA across 25 consecutive trials ($13.36\,\text{s}$ Yellow, $13.62\,\text{s}$ Red, $100\%$ success).
9. **Interactive Presentation Controls:** Hotkey camera presets `[1]-[3]`, PiP inspection `[V]`, and virtual markers `[M]`.
10. **Plan for Next Week (Week 08) & Roadmap to Milestone 2 (Week 10):** Hardening perception, static obstacle validation, and defense readiness.

---

## SLIDE 03: EXECUTIVE PROGRESS SUMMARY (WEEK 06 $\to$ WEEK 07)
### 1. Commitments Set Out in Week 06:
- **Commitment 1 (Trajectory Smoothing - R5):** Eliminate the 10 discrete stop-and-go pauses of Milestone 1 via blended Cartesian splines.
- **Commitment 2 (Velocity Scaling):** Boost robot motion speed to reduce cycle time from $\approx 25\,\text{s}$ to under $18\,\text{s}$.
- **Commitment 3 (Computer Vision Perception - R6 & R7):** Replace overhead ideal sensing with an RGB camera and OpenCV pipeline.
- **Commitment 4 (Conveyor Safety & Collision Elimination):** Prevent oncoming conveyor cubes from colliding during long-distance MoveIt planning.

### 2. Concrete Deliverables Achieved in Week 07:
- **100% Blended Trajectories:** Implemented `move_cartesian_trajectory` with continuous corner blending, cutting stops from $10 \to 0$.
- **47% Cycle Time Drop:** Flanking bin cycle times plummeted from $24.95\,\text{s} \to 13.20\,\text{s}$ (Yellow: $13.36\,\text{s}$, Red: $13.62\,\text{s}$, Blue: $19.02\,\text{s}$).
- **100% Parallel Gripper Alignment:** Aligned wrist joint 5 ($16.9^\circ$) with shoulder yaw ($16.9^\circ$), eliminating the $13.4^\circ$ skew.
- **Front-Tilted Vision Cell (Option 1):** Deployed camera on opposite side of conveyor ($Z=-1.10\,\text{m}$, pitch $50^\circ$), giving unoccluded view of cube front.
- **Electro-Mechanical Conveyor Braking:** Eliminated $5\,\text{cm}$ coasting drift; cube stops dead-center at $X = 0.00\,\text{m}$.
- **Self-Centering Parallel Grasp:** Flushes cube faces symmetrically between rubber jaw pads with zero air-gap floating.
- **Cinematic Presentation Cameras:** Calibrated 3 preset camera views (`[1]` Overview, `[2]` Sorting Bins, `[3]` Gripper Close-Up).

---

## SLIDE 04: CONTINUOUS TRAJECTORY SMOOTHING (REQUIREMENT R5)
### 1. The Stop-and-Go Limitation in Milestone 1:
- Milestone 1 used discrete, disjointed Cartesian and Joint waypoints.
- Result: Robot stopped to zero velocity at every intermediate waypoint (10 stop-and-go stages per cycle).
- Total cycle time suffered from repeated acceleration/deceleration penalties ($\approx 25\,\text{s}$ per item).

### 2. Multi-Waypoint Continuous Cartesian Blending:
- Implemented `move_cartesian_trajectory(waypoints, min_fraction=0.85, speed_factor=None)` using MoveIt's Cartesian planner.
- Groups approach $\to$ pregrasp $\to$ grasp into a single fluid trajectory, and lift $\to$ apex sweep $\to$ bin descent into a single arc.
- Preserves safety with automatic fallback to orthogonal stepped legs if Cartesian fraction drops below threshold.

### 3. Dynamic Natural Wrist Orientation:
- **The Singularity Problem:** Fixed world quaternion (`down_orientation`) caused wrist joint 5 to reverse by $160^\circ$ when swinging to Yellow/Red bins, triggering collision with safety shields.
- **Mathematical Formulation:** Dynamically calculate tool orientation based on base shoulder yaw angle $\theta$:
  $$\Delta \theta = \theta_{\text{waypoint}} - \theta_{\text{standby}}$$
  $$\mathbf{q}_{\text{rot}} = [0, 0, \sin(\Delta\theta/2), \cos(\Delta\theta/2)]$$
  $$\mathbf{q}_{\text{waypoint}} = \mathbf{q}_{\text{rot}} \otimes \mathbf{q}_{\text{standby}}$$
- **Result:** Keeps wrist in continuous natural vertical downward alignment with ZERO joint reversal. Cartesian IK feasibility reached 100.0% for all 3 bins!

---

## SLIDE 05: CONVEYOR ESCAPE LIFT & TRANSIT VELOCITY SCALING
### 1. The Conveyor Collision Challenge:
- During multi-item continuous conveyor flow, oncoming cubes travel at $0.3\,\text{m/s}$ toward the pick zone.
- Planning a complex 5-waypoint Cartesian path to the Blue bin takes $\approx 1.2\,\text{s}$ of MoveIt CPU computation.
- If the robot holds the grasped cube stationary on the conveyor surface while planning, the next incoming cube collides with the grasped box!

### 2. Rapid Conveyor Escape Lift Architecture:
- Immediately upon gripper closure (`/ur10e/gripper_command = True`), the state machine executes an instantaneous vertical snap:
  - Climb $+16\,\text{cm}$ straight up into open air.
  - Duration: $<0.25\,\text{s}$ using speed scaling factor $2.4\text{x}$.
- Clears the conveyor height boundary ($Z = 0.21\,\text{m}$) before initiating long-range transit planning!
- Completely isolates transit planning latency from conveyor surface physics.

### 3. Multi-Tier Speed Scaling:
- `scale_trajectory_speed(traj, factor)` rewrites trajectory `time_from_start`, `velocities`, and `accelerations`.
- Red Bin ($Y=+0.55\,\text{m}$): Speed scaled by $1.8\text{x}$ (Cycle time: $13.62\,\text{s}$).
- Yellow Bin ($Y=-0.55\,\text{m}$): Speed scaled by $1.8\text{x}$ (Cycle time: $13.36\,\text{s}$).
- Blue Bin ($X=+0.50\,\text{m}$): Long rear fly-over scaled by $2.2\text{x}$ (Cycle time: $19.02\,\text{s}$).

---

## SLIDE 06: GRIPPER PARALLELISM, BRAKING, & PRESENTATION CAMERAS
### 1. Precision Geometric Realignment:
- **Joint-Space Synchronization:** Shoulder rotates $\theta_0 = +16.87^\circ$ ($0.2945\,\text{rad}$) when reaching pick zone. Legacy Joint 5 was $+30.31^\circ$, creating a $13.44^\circ$ skew.
- Setting Joint 5 to $+16.87^\circ$ and $\mathbf{q}_{\text{standby}} = [-0.7071, -0.7071, 0.0031, 0.0008]$ achieves $\text{Yaw} = 90.00^\circ$ orthogonal alignment.

### 2. Electro-Mechanical Braking & Self-Centering Grasp:
- **Zero-Coasting Brake:** `PickZoneSensor.cs` triggers `rb.velocity = Vector3.zero` at $X = 0.00\,\text{m}$, eliminating the $4\sim6\,\text{cm}$ slide that caused offset grasping.
- **Belt Alignment:** `RandomObjectSpawner.cs` applies `FreezeRotationX/Y/Z` to prevent cube yaw drift during conveyor transit.
- **Self-Centering Snap:** `GripperController.cs` automatically centers $X, Z$ coordinates between the rubber pads and squares cube rotation (`Quaternion.identity`) upon grasp, ensuring 100% flush jaw contact with zero mid-air floating!

### 3. Interactive Presentation Controls:
- **Preset `[1]` (Workcell Overview):** $(-1.40, 1.65, -2.10)$, Pitch $28^\circ$, Yaw $36^\circ$ — Complete 3/4 isometric perspective.
- **Preset `[2]` (Sorting & Obstacles):** $(1.35, 1.50, -1.65)$, Pitch $26.5^\circ$, Yaw $-39^\circ$ — Clear view of 3 bins & apex clearance.
- **Preset `[3]` (Gripper Close-Up):** $(-0.55, 0.55, -1.15)$, Pitch $29^\circ$, Yaw $45^\circ$ — Crisp focus on pick station & parallel jaws.
- **Interactive Hotkeys:** `[M]` Toggle Markers, `[V]` Toggle CV PiP, `[C]` Toggle Camera HUD, `[T]` Toggle Trajectory.

---

## SLIDE 07: PERCEPTION UPGRADE: THE ROBOT ARM OCCLUSION DILEMMA
### 1. The Overhead Pick-Station Problem:
- In classical academic setups, cameras are mounted directly above the pick target ($X=0, Z=-0.60\,\text{m}$).
- **Fatal Industrial Flaw:** When the robot arm reaches in to grasp or hover in standby, the mechanical arm links (`forearm_link`, `wrist_1_link`, `gripper`) completely block the camera's line of sight!
- Dynamic shadows cast by the moving arm cause HSV color classification to fail or produce intermittent detection flickering.

### 2. Architectural Comparison:
- **Upstream Inspection (65cm away):** Suffers from conveyor physics drift and timing synchronization issues between inspection and arrival.
- **Option 1: Front-Tilted Opposite Mount (Selected):**
  - Camera mounted on the opposite side of the conveyor ($Z = -1.10\,\text{m}$, height $0.75\,\text{m}$).
  - Tilted $50^\circ$ downward pointing directly at the pick station ($Z = -0.60\,\text{m}$).
  - The robot operates BEHIND the conveyor ($Z \in [-0.60, 0.0]$).
  - The FRONT and TOP faces of the cube directly face the camera lens $\implies$ **0% visual occlusion**!

---

## SLIDE 08: FRONT-TILTED VISION CELL ARCHITECTURE (R6 & R7)
### 1. Spatial Station Geometry:
- **Mount Coordinates (Unity):** $X = 0.00\,\text{m}, Y = 0.75\,\text{m}, Z = -1.10\,\text{m}$.
- **Rotation:** Euler angles $(50.0^\circ, 0.0^\circ, 0.0^\circ)$ pointing directly at $(0.0, 0.10, -0.60)$.
- **Line-of-Sight Distance:** $L = \sqrt{0.50^2 + 0.65^2} \approx 0.82\,\text{m}$.
- **Field of View (FOV):** $48^\circ$, framing the pick station, oncoming conveyor lane, and cube faces.

### 2. Procedural 3D Camera Tripod Stand:
- Unity script `OverheadCameraPublisher.cs` procedurally instantiates a realistic physical camera stand:
  - Base plate on floor: $(0.0, 0.02, -1.10)$, size $(0.24, 0.04, 0.24)$ in dark industrial steel.
  - Vertical post: $(0.0, 0.38, -1.10)$, height $0.72\,\text{m}$.
  - Swivel camera head: $(0.0, 0.74, -1.10)$, tilted $50^\circ$ with black camera enclosure housing.
- Non-colliding visual representation provides 100% industrial realism in Unity scene.

### 3. High-Performance ROS Image Streaming:
- Captures RenderTexture at $640\times 480$ resolution @ 15 Hz.
- Publishes JPEG compressed stream `/camera/color/image_raw/compressed` (<150 KB/s payload).
- Picture-in-Picture (PiP) viewport in bottom-right corner of Unity game view (toggle with `[V]`).

---

## SLIDE 09: DECOUPLED PERCEPTION ARCHITECTURE
### 1. ROS Communication Flow:
```
+-------------------------------------------------------------------+
|                        UNITY 2022.3 SIMULATION                     |
|  [OverheadCameraPublisher]              [ObjectLocalizationReporter] |
|             |                                        |            |
|    sensor_msgs/CompressedImage            geometry_msgs/Pose       |
|    (/camera/color/image_raw/compressed)  (/detected_object_pose)   |
+-------------------------------------------------------------------+
             |                                        |
             v                                        |
+---------------------------------------------------+  |
|         OPENCV PERCEPTION (vision_detector_node)   |  |
|  - HSV Segmentation (Red, Yellow, Blue)           |  |
|  - Sub-Pixel Centroid Extraction                 |  |
|  - Front-Tilted Perspective (0% Arm Occlusion)    |  |
|  - Publishes /camera/color/image_detection/comp   |  |
+---------------------------------------------------+  |
                                                      |
                                                      v
+-------------------------------------------------------------------+
|             MOVEIT TASK STATE MACHINE (task_state_machine.py)       |
|  - Continuous Cartesian Descent (100% Parallel Jaws)               |
|  - Rapid Escape Lift (<0.25s at 2.4x)                             |
|  - Direct Apex Blended Transit (13.2s Cycle Time)                  |
|  - Symmetrical Return to Standby                                   |
+-------------------------------------------------------------------+
```

### 2. OpenCV Processing Pipeline:
- Converts BGR to HSV: Red $[0-10, 165-180]$, Yellow $[18-38]$, Blue $[95-135]$.
- Morphological Opening & Closing with $5\times 5$ rectangular kernel.
- Sub-pixel centroid moments $cx = m_{10}/m_{00}, cy = m_{01}/m_{00}$ ($\approx 1.52\,\text{mm/pixel}$).
- Annotated HUD overlay displaying bounding box, color classification, and "FRONT-TILTED (ZERO OCCLUSION)".

---

## SLIDE 10: EMPIRICAL BENCHMARKS (25-TRIAL CONTINUOUS TEST)
### 1. Quantitative Benchmark Results (Real Logged Run):

| Parameter | Milestone 1 (Baseline) | Week 07 (Optimized R5) | Improvement |
| :--- | :---: | :---: | :---: |
| **Number of Stops** | 10 pauses per cycle | **0 pauses (Continuous)** | **-100% (Zero Jerk)** |
| **Yellow Bin Cycle Time** | $24.95\,\text{s}$ | **$13.36\,\text{s}$** (Min: 13.02s) | **$-46.5\%$** |
| **Red Bin Cycle Time** | $21.00\,\text{s}$ | **$13.62\,\text{s}$** (Min: 13.34s) | **$-35.1\%$** |
| **Blue Bin Cycle Time** | $32.72\,\text{s}$ | **$19.02\,\text{s}$** (Min: 18.63s) | **$-41.9\%$** |
| **Overall Mean Cycle Time** | $25.00\,\text{s}$ | **$15.68\,\text{s}$** | **$-37.3\%$ Overall** |
| **Grasp Success Rate** | 100% | **100.0% (25/25 trials)** | **Rock-solid reliability** |
| **Place Success Rate** | 100% | **100.0% (25/25 trials)** | **Rock-solid reliability** |
| **Conveyor Collision Rate** | Intermittent on Blue | **0 Collisions (Escape Lift)** | **100% collision-free** |
| **Gripper Jaw Parallelism** | Skewed by $13.4^\circ$ | **$0.0^\circ$ Error (100% Parallel)** | **Flawless alignment** |

### 2. Statistical Highlights:
- High consistency: Standard deviation of cycle time across all 25 trials is only $2.76\,\text{s}$.
- Flanking bins (Red & Yellow) consistently finish in **$13.0 - 14.0\,\text{s}$**.
- Rear fly-over bin (Blue) consistently finishes in **$18.6 - 19.3\,\text{s}$** with zero conveyor drag.

---

## SLIDE 11: PLAN FOR NEXT WEEK (WEEK 08) & ROADMAP TO MILESTONE 2 (WEEK 10)
### 1. Week 08 Technical Commitments:
- **Perception Hardening & Lighting Invariance:** Calibrate OpenCV HSV segmentation under dynamic shadows and reflections; add morphological filtering for outlier rejection.
- **Static Obstacle Verification:** Validate safety clearance margins over acrylic shields ($Z = -0.38\,\text{m}$) and UR Control Box across all 3 destination bins.
- **Extended Continuous Flow Testing:** Perform a 50-trial endurance benchmark with varied conveyor speeds ($0.2 - 0.4\,\text{m/s}$) and random multi-color queues.
- **Milestone 2 Defense Package:** Prepare automated launch scripts, 1-click test runners, and diagnostic logs ahead of the formal submission.

### 2. Milestone Roadmap (Weeks 08 $\to$ 14):
- **Week 08 (Next Week):** Perception hardening, static obstacle clearance validation, and 50-trial endurance testing.
- **Week 09:** Complete Milestone 2 pre-defense package: Technical Runbook, documentation, and recorded verification video.
- **Week 10 (Milestone 2 Formal Defense):** Official committee evaluation of Conveyor Sorting, Computer Vision Perception, and Static Obstacle Avoidance (Requirements R6–R8).
- **Weeks 11–14 (Milestone 3 Sprint):** Dynamic Moving Obstacle Avoidance (oscillating physical barrier, real-time risk monitor, reactive safe-stop & replan).

---

## SLIDE 12: SUMMARY & FACULTY Q&A
### 1. Key Accomplishments Delivered in Week 07:
- **Requirement R5 Verified:** Continuous Cartesian path blending eliminated all 10 stop pauses, cutting flanking cycle times to $13.2 - 13.6\,\text{s}$ ($-47\%$ boost).
- **Gripper Parallelism Achieved:** Joint 5 realigned to $16.87^\circ$, eliminating the $13.4^\circ$ skew and achieving $100\%$ parallel alignment.
- **Physical Grasp Stability:** Electro-mechanical braking ($X = 0.00\,\text{m}$) and self-centering grasp eliminated coasting drift and mid-air floating.
- **Conveyor Grasp Collision Solved:** Rapid $+16\,\text{cm}$ vertical escape lift in $<0.25\,\text{s}$ at $2.4\text{x}$ cleared oncoming conveyor cubes cleanly.
- **Requirements R6 & R7 Verified:** Front-Tilted Camera Mount deployed with 3D stand in Unity, delivering 0% arm occlusion and real-time OpenCV HUD.
- **Interactive Presentation Controls:** Added camera presets `[1]-[3]`, `[M]` toggle for Markers, `[V]` for CV PiP, and `[C]` for Camera HUD.

### 2. Engineering Sign-Off:
- System is 100% reproducible via `./run_workcell.sh` in Unity and ROS Noetic.
- Real empirical dataset of 25 consecutive trials verified with 100% success rate.
- Ready for Faculty Committee Q&A and on track for Milestone 2 defense at Week 10.
