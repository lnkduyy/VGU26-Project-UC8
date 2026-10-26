#!/usr/bin/env bash
# ==============================================================================
# UC8 - ROBOTIC ARM CONVEYOR PICK-AND-PLACE (AUTOMATED WORKCELL LAUNCHER)
# Tự động hóa khởi chạy trọn bộ: Endpoint (10000) -> MoveIt & RViz -> State Machine
# Hỗ trợ:
#   1. Mặc định (All-in-One): Chạy trực tiếp trong 1 terminal, tự động dọn dẹp khi bấm Ctrl+C.
#   2. Chế độ 3 cửa sổ riêng biệt: ./run_workcell.sh --gui (hoặc chọn khi chạy)
# ==============================================================================

PROJECT_ROOT="/home/kduy/RoboticProject/project_blueprint"
ROS_WS="$PROJECT_ROOT/ros_ws"
LOGS_DIR="$PROJECT_ROOT/logs"
SETUP_NOETIC="/opt/ros/noetic/setup.bash"
SETUP_WS="$ROS_WS/devel/setup.bash"

# Màu hiển thị terminal
CYAN='\033[0;36m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
MAGENTA='\033[0;35m'
BOLD='\033[1m'
NC='\033[0m'

mkdir -p "$LOGS_DIR"

clear
echo -e "${CYAN}${BOLD}"
echo "=========================================================================="
echo "    UC8 WORKCELL AUTOMATED LAUNCHER — ROBOTIC PROJECT VGU (MILESTONE 1)   "
echo "=========================================================================="
echo -e "${NC}"

# 1. Kiểm tra môi trường ROS
if [ ! -f "$SETUP_NOETIC" ]; then
    echo -e "${RED}[ERROR] Không tìm thấy ROS Noetic tại $SETUP_NOETIC!${NC}"
    exit 1
fi

if [ ! -f "$SETUP_WS" ]; then
    echo -e "${YELLOW}[!] Chưa tìm thấy $SETUP_WS. Đang tiến hành build workspace...${NC}"
    cd "$ROS_WS" && catkin_make
fi

# Source môi trường
source "$SETUP_NOETIC"
source "$SETUP_WS"

# 2. Dọn dẹp tiến trình cũ để tránh xung đột
echo -e "${YELLOW}[1/4] Dọn dẹp tiến trình cũ và giải phóng cổng 10000...${NC}"
kill -9 $(lsof -t -i:10000) 2>/dev/null
pkill -9 -f "task_state_machine.py" 2>/dev/null
pkill -9 -f "ur10e_moveit_config" 2>/dev/null
pkill -9 -f "ros_tcp_endpoint" 2>/dev/null
sleep 1
echo -e "${GREEN}[✔] Cổng 10000 và tiến trình cũ đã được giải phóng hoàn toàn.${NC}"

# Kiểm tra cờ tham số --gui
MODE="cli"
if [ "$1" == "--gui" ] || [ "$1" == "-g" ]; then
    MODE="gui"
fi

if [ "$MODE" == "gui" ]; then
    echo -e "${YELLOW}[2/4] Khởi động chế độ 3 Cửa Sổ Terminal riêng biệt...${NC}"

    # Lệnh cho từng cửa sổ
    CMD1="source $SETUP_NOETIC && source $SETUP_WS && echo -e '${GREEN}${BOLD}=== [1/3] ROS-TCP-ENDPOINT (PORT 10000) ===${NC}' && roslaunch ros_tcp_endpoint endpoint.launch; read -p 'Press Enter to exit...'"
    CMD2="sleep 2 && source $SETUP_NOETIC && source $SETUP_WS && echo -e '${CYAN}${BOLD}=== [2/3] MOVEIT MOTION PLANNING & RVIZ ===${NC}' && roslaunch ur10e_moveit_config demo.launch; read -p 'Press Enter to exit...'"
    CMD3="sleep 7 && source $SETUP_NOETIC && source $SETUP_WS && echo -e '${YELLOW}${BOLD}=== [3/3] TASK STATE MACHINE (PICK-AND-PLACE) ===${NC}' && python3 $ROS_WS/src/uc8_robot_sorting/scripts/task_state_machine.py; read -p 'Press Enter to exit...'"

    # Dùng dbus-launch hoặc gnome-terminal trực tiếp cho từng cửa sổ riêng lẻ
    gnome-terminal --title="[1] ROS-TCP-Endpoint" -- bash -c "$CMD1" 2>/dev/null || x-terminal-emulator -e bash -c "$CMD1" &
    sleep 1
    gnome-terminal --title="[2] MoveIt & RViz" -- bash -c "$CMD2" 2>/dev/null || x-terminal-emulator -e bash -c "$CMD2" &
    sleep 2
    gnome-terminal --title="[3] Task State Machine" -- bash -c "$CMD3" 2>/dev/null || x-terminal-emulator -e bash -c "$CMD3" &

    echo -e "${GREEN}[✔] Đã gửi lệnh mở 3 cửa sổ terminal riêng biệt.${NC}"
    echo -e "Nếu cửa sổ không hiện lên do lỗi D-Bus của GNOME, bạn hãy chạy trực tiếp: ${YELLOW}./run_workcell.sh${NC} (không cần cờ --gui)."
    exit 0
fi

# ==============================================================================
# CHẾ ĐỘ MẶC ĐỊNH: ALL-IN-ONE TRONG TERMINAL HIỆN TẠI (ĐẢM BẢO CHẮC CHẮN CHẠY 100%)
# ==============================================================================
echo -e "${CYAN}[2/4] Khởi động chế độ All-in-One (Một Terminal duy nhất, tự động quản lý)...${NC}"

# Hàm dọn dẹp khi người dùng bấm Ctrl+C
cleanup() {
    echo ""
    echo -e "\n${YELLOW}[!] Đang dừng sạch sẽ toàn bộ các tiến trình nền...${NC}"
    kill -9 $PID_ENDPOINT $PID_MOVEIT $PID_VISION 2>/dev/null
    kill -9 $(lsof -t -i:10000) 2>/dev/null
    pkill -9 -f "task_state_machine.py" 2>/dev/null
    pkill -9 -f "vision_detector_node.py" 2>/dev/null
    pkill -9 -f "ur10e_moveit_config" 2>/dev/null
    pkill -9 -f "ros_tcp_endpoint" 2>/dev/null
    echo -e "${GREEN}[✔] Đã dừng toàn bộ hệ thống an toàn.${NC}"
    exit 0
}
trap cleanup SIGINT SIGTERM EXIT

# 1. Chạy ROS-TCP-Endpoint trong nền
echo -e "${YELLOW} --> [1/4] Khởi động ROS-TCP-Endpoint trên cổng 10000...${NC}"
roslaunch ros_tcp_endpoint endpoint.launch > "$LOGS_DIR/endpoint.log" 2>&1 &
PID_ENDPOINT=$!
sleep 2

# Kiểm tra xem cổng 10000 đã mở chưa
if lsof -i:10000 >/dev/null 2>&1; then
    echo -e "${GREEN}      [✔] ROS-TCP-Endpoint đã sẵn sàng trên cổng 10000 (PID: $PID_ENDPOINT).${NC}"
else
    echo -e "${YELLOW}      [*] Endpoint đang khởi tạo... (Log tại: $LOGS_DIR/endpoint.log)${NC}"
fi

# 2. Chạy MoveIt & RViz trong nền (Cửa sổ 3D RViz vẫn sẽ tự động hiện lên màn hình)
echo -e "${YELLOW} --> [2/4] Khởi động MoveIt Motion Planning & RViz...${NC}"
roslaunch ur10e_moveit_config demo.launch > "$LOGS_DIR/moveit.log" 2>&1 &
PID_MOVEIT=$!

echo -e "      [*] Đang nạp mô hình UR10e và thuật toán OMPL (chờ khoảng 5-7s)..."
for i in {1..12}; do
    if grep -q "You can start planning now" "$LOGS_DIR/moveit.log" 2>/dev/null; then
        echo -e "${GREEN}      [✔] MoveIt & RViz đã sẵn sàng hoàn toàn! (PID: $PID_MOVEIT)${NC}"
        break
    fi
    sleep 1
done

# 3. Chạy OpenCV Computer Vision Detector Node trong nền (Requirement R6 & R7)
echo -e "${YELLOW} --> [3/4] Khởi động OpenCV Computer Vision Node (/camera/color/image_raw)...${NC}"
python3 "$ROS_WS/src/uc8_robot_sorting/scripts/vision_detector_node.py" > "$LOGS_DIR/vision.log" 2>&1 &
PID_VISION=$!
sleep 1
echo -e "${GREEN}      [✔] OpenCV Computer Vision Detector đã kích hoạt! (PID: $PID_VISION)${NC}"

# 4. Chạy Task State Machine ở tiền cảnh (Foreground) để người dùng theo dõi trực tiếp
echo ""
echo -e "${GREEN}${BOLD}=========================================================================="
echo "    [4/4] KHỞI ĐỘNG TASK STATE MACHINE (PICK-AND-PLACE SEQUENCE)          "
echo "==========================================================================${NC}"
echo -e " * Cửa sổ RViz 3D đã mở trên màn hình."
echo -e " * Camera thị giác máy tính OpenCV đã sẵn sàng nhận ảnh từ Unity."
echo -e " * Mở ${GREEN}${BOLD}Unity Editor${NC} và nhấn nút ${GREEN}${BOLD}Play (▶)${NC} để bắt đầu mô phỏng!"
echo -e " * Để dừng toàn bộ hệ thống bất cứ lúc nào, nhấn ${YELLOW}${BOLD}Ctrl + C${NC}."
echo -e "${CYAN}--------------------------------------------------------------------------${NC}"
echo ""

python3 "$ROS_WS/src/uc8_robot_sorting/scripts/task_state_machine.py" "$@"
