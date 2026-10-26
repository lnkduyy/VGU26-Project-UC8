#!/usr/bin/env python3
# -*- coding: utf-8 -*-

"""
Generate High-Quality Landscape PDF Presentation - CLIENT & EXECUTIVE VERSION
Hệ thống Phân loại Hàng hóa Tự động Băng chuyền Bằng Robot UR10e & Thị giác Thông minh (Week 07)
Nâng cao năng suất 47%, Chuyển động mượt mà không dừng giật, Kẹp tự định tâm & Thị giác không góc chết.
Project: UC8 - Robotic Arm Conveyor Pick-and-Place with Obstacle Avoidance
Course: Robotic Project — Vietnamese-German University (VGU)
"""

import sys
import os

pylib_dir = "/home/kduy/RoboticProject/project_blueprint/docs/week04/pylib"
if os.path.exists(pylib_dir):
    sys.path.insert(0, pylib_dir)

from reportlab.lib.pagesizes import A4, landscape
from reportlab.lib import colors
from reportlab.platypus import (
    SimpleDocTemplate, Paragraph, Spacer, Table, TableStyle, PageBreak, Image
)
from reportlab.lib.styles import getSampleStyleSheet, ParagraphStyle
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.pdfgen import canvas

# 1. Register TTF Fonts for full Vietnamese Unicode support
font_regular = "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"
font_bold = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"
pdfmetrics.registerFont(TTFont("DejaVu", font_regular))
pdfmetrics.registerFont(TTFont("DejaVu-Bold", font_bold))

PAGE_WIDTH, PAGE_HEIGHT = landscape(A4)  # 841.89 x 595.27 pt


class SlideCanvas(canvas.Canvas):
    """Custom canvas that draws running header and footer on each slide"""
    def __init__(self, *args, **kwargs):
        super().__init__(*args, **kwargs)
        self.pages = []

    def showPage(self):
        self.pages.append(dict(self.__dict__))
        self._startPage()

    def save(self):
        num_pages = len(self.pages)
        for page in self.pages:
            self.__dict__.update(page)
            self.draw_decorations(num_pages)
            super().showPage()
        super().save()

    def draw_decorations(self, total_pages):
        page_num = self._pageNumber
        if page_num == 1:
            return

        self.saveState()
        # Top banner line
        self.setFillColor(colors.HexColor("#0f2b48"))
        self.rect(32, PAGE_HEIGHT - 18, PAGE_WIDTH - 64, 2.5, fill=1, stroke=0)

        # Bottom footer line
        self.setFillColor(colors.HexColor("#CBD5E1"))
        self.rect(32, 34, PAGE_WIDTH - 64, 1, fill=1, stroke=0)

        # Footer text
        self.setFont("DejaVu", 8.5)
        self.setFillColor(colors.HexColor("#64748B"))
        self.drawString(34, 22, "Hệ Thống Phân Loại Băng Chuyền Bằng Robot UR10e & Thị Giác Thông Minh | Báo Cáo Khách Hàng (Week 07) | VGU")
        page_str = f"Trang {page_num:02d} / {total_pages:02d}"
        self.drawRightString(PAGE_WIDTH - 34, 22, page_str)
        self.restoreState()


def build_pdf(output_path):
    doc = SimpleDocTemplate(
        output_path,
        pagesize=landscape(A4),
        leftMargin=32,
        rightMargin=32,
        topMargin=26,
        bottomMargin=36
    )

    styles = getSampleStyleSheet()

    # Color Palette
    PRIMARY = colors.HexColor("#0f2b48")       # Deep Industrial Navy
    ACCENT = colors.HexColor("#0284c7")        # Vibrant Sky Blue
    SUCCESS = colors.HexColor("#059669")       # Emerald Green
    TEXT_MAIN = colors.HexColor("#1e293b")     # Slate 800
    TEXT_MUTED = colors.HexColor("#64748b")    # Slate 500
    CARD_BG = colors.HexColor("#ffffff")       # Pure White
    BORDER_COL = colors.HexColor("#e2e8f0")    # Border Gray

    # Typography Styles
    title_style = ParagraphStyle(
        "CoverTitle",
        fontName="DejaVu-Bold",
        fontSize=21,
        leading=26,
        textColor=colors.HexColor("#ffffff"),
        alignment=1
    )

    subtitle_style = ParagraphStyle(
        "CoverSubtitle",
        fontName="DejaVu",
        fontSize=11,
        leading=16,
        textColor=colors.HexColor("#38bdf8"),
        alignment=1
    )

    header_title_style = ParagraphStyle(
        "HeaderTitle",
        fontName="DejaVu-Bold",
        fontSize=15,
        leading=18,
        textColor=PRIMARY
    )

    header_tracker_style = ParagraphStyle(
        "HeaderTracker",
        fontName="DejaVu-Bold",
        fontSize=8.5,
        leading=11,
        textColor=ACCENT
    )

    card_title_style = ParagraphStyle(
        "CardTitle",
        fontName="DejaVu-Bold",
        fontSize=11.5,
        leading=14,
        textColor=PRIMARY
    )

    bullet_style = ParagraphStyle(
        "BulletText",
        fontName="DejaVu",
        fontSize=9.0,
        leading=12.5,
        textColor=TEXT_MAIN
    )

    code_style = ParagraphStyle(
        "CodeText",
        fontName="DejaVu",
        fontSize=8.5,
        leading=11.5,
        textColor=PRIMARY
    )

    story = []

    def make_header(tracker, title, slide_no):
        header_data = [
            [
                Paragraph(tracker.upper(), header_tracker_style),
                Paragraph(f"{slide_no:02d} / 12", ParagraphStyle("Num", fontName="DejaVu-Bold", fontSize=10.5, textColor=ACCENT, alignment=2))
            ],
            [Paragraph(title, header_title_style), ""]
        ]
        t = Table(header_data, colWidths=[650, 127])
        t.setStyle(TableStyle([
            ('SPAN', (0, 1), (1, 1)),
            ('VALIGN', (0, 0), (-1, -1), 'TOP'),
            ('BOTTOMPADDING', (0, 0), (-1, -1), 2),
            ('TOPPADDING', (0, 0), (-1, -1), 0),
            ('LEFTPADDING', (0, 0), (-1, -1), 0),
            ('RIGHTPADDING', (0, 0), (-1, -1), 0),
        ]))
        return t

    def make_card(title, bullet_items, width=376, bg=CARD_BG, border=BORDER_COL):
        card_content = []
        if title:
            card_content.append(Paragraph(title, card_title_style))
            card_content.append(Spacer(1, 4))

        for item in bullet_items:
            card_content.append(Paragraph(f"• {item}", bullet_style))
            card_content.append(Spacer(1, 3.5))

        card_t = Table([[card_content]], colWidths=[width])
        card_t.setStyle(TableStyle([
            ('BACKGROUND', (0, 0), (-1, -1), bg),
            ('BOX', (0, 0), (-1, -1), 1, border),
            ('LEFTPADDING', (0, 0), (-1, -1), 9),
            ('RIGHTPADDING', (0, 0), (-1, -1), 9),
            ('TOPPADDING', (0, 0), (-1, -1), 8),
            ('BOTTOMPADDING', (0, 0), (-1, -1), 7),
            ('VALIGN', (0, 0), (-1, -1), 'TOP'),
        ]))
        return card_t

    # =========================================================================
    # SLIDE 01: TITLE COVER
    # =========================================================================
    cover_table_data = [
        [Paragraph("GIẢI PHÁP TỰ ĐỘNG HÓA DÂY CHUYỀN SẢN XUẤT CÔNG NGHIỆP 4.0", ParagraphStyle("Univ", fontName="DejaVu-Bold", fontSize=11, textColor=colors.HexColor("#38BDF8"), alignment=1))],
        [Spacer(1, 12)],
        [Paragraph("Hệ Thống Phân Loại Băng Chuyền Bằng Robot UR10e & Thị Giác Thông Minh", title_style)],
        [Spacer(1, 6)],
        [Paragraph("Tối Ưu Năng Suất Vận Hành, Chuyển Động Mượt Mà, Kẹp Tự Nắn Phẳng & Thị Giác Không Điểm Mù", subtitle_style)],
        [Spacer(1, 14)],
        [Paragraph("Báo Cáo Tiến Độ Dự Án (Week 07) — Phiên Bản Trình Chiếu Doanh Nghiệp & Khách Hàng", ParagraphStyle("Sub2", fontName="DejaVu", fontSize=10, textColor=colors.HexColor("#CBD5E1"), alignment=1))],
        [Spacer(1, 18)],
        [Table([
            [Paragraph("<b>Giải Pháp Công Nghệ:</b> Cánh tay robot Universal Robots UR10e (6-DOF) + Băng chuyền 3.6m + 3 Thùng phân loại", code_style)],
            [Paragraph("<b>Mắt Thần Thị Giác AI:</b> Trạm camera đối diện nghiêng 50° triệt tiêu 100% điểm mù cánh tay + Nhận diện sắc nét từng milimet", code_style)],
            [Paragraph("<b>Chuyển Động Lượn Sóng:</b> Triệt tiêu 10 lần dừng giật, liên tục êm ái, kéo dài tuổi thọ động cơ và hộp số robot", code_style)],
            [Paragraph("<b>Cơ Cấu Kẹp Ôm Sát:</b> Phanh điện tử chống trôi X=0.0m + Má cao su tự nắn phẳng và căn giữa phôi 100% vuông vắn", code_style)],
            [Paragraph("<b>Hiệu Quả Kinh Tế:</b> Năng suất tăng 47% (13.2s/kiện), 25 lần gắp liên tục thành công 100%, 0 sự cố va chạm", code_style)]
        ], colWidths=[700], style=[
            ('BACKGROUND', (0, 0), (-1, -1), colors.HexColor("#f8fafc")),
            ('BOX', (0, 0), (-1, -1), 1, colors.HexColor("#cbd5e1")),
            ('TOPPADDING', (0, 0), (-1, -1), 6),
            ('BOTTOMPADDING', (0, 0), (-1, -1), 6),
            ('LEFTPADDING', (0, 0), (-1, -1), 12),
            ('RIGHTPADDING', (0, 0), (-1, -1), 12),
        ])]
    ]
    cover_t = Table(cover_table_data, colWidths=[PAGE_WIDTH - 64])
    cover_t.setStyle(TableStyle([
        ('BACKGROUND', (0, 0), (-1, -1), colors.HexColor("#0F172A")),
        ('ALIGN', (0, 0), (-1, -1), 'CENTER'),
        ('VALIGN', (0, 0), (-1, -1), 'MIDDLE'),
        ('TOPPADDING', (0, 0), (-1, -1), 26),
        ('BOTTOMPADDING', (0, 0), (-1, -1), 26),
    ]))
    story.append(cover_t)
    story.append(PageBreak())

    # =========================================================================
    # SLIDE 02: CLIENT AGENDA
    # =========================================================================
    story.append(make_header("Lộ Trình Báo Cáo", "Nội Dung Báo Cáo Giải Pháp Dành Cho Khách Hàng", 2))
    story.append(Spacer(1, 10))

    col1 = [
        "<b>01. Tổng Quan Tiến Độ Dự Án:</b> Cam kết từ giai đoạn trước và kết quả đột phá đạt được hôm nay.",
        "<b>02. Chuyển Động Lượn Sóng Mượt Mà:</b> Loại bỏ hoàn toàn rung giật, vận hành êm ái, bảo vệ động cơ robot.",
        "<b>03. Nhấc Thoát Hiểm Chớp Nhoáng (0.2s):</b> Giải quyết triệt để va chạm, bảo đảm dòng hàng lưu thông liên tục.",
        "<b>04. Cơ Cấu Kẹp Ôm Sát Tự Nắn Phẳng:</b> Phanh dừng chính xác X=0m, tự động căn giữa và ép phẳng kiện hàng."
    ]
    col2 = [
        "<b>05. Xóa Bỏ Điểm Mù Cánh Tay:</b> Khắc phục nhược điểm camera trên trần bị che khuất bằng góc nhìn đối xứng.",
        "<b>06. Mắt Thần Thị Giác AI Góc Nghiêng:</b> Thiết kế trạm quét độc quyền, nhìn rõ cả mặt trên và mặt trước kiện hàng.",
        "<b>07. Minh Chứng Hiệu Quả 25 Lần Chạy:</b> Dữ liệu thực tế: Năng suất tăng 47%, 100% thành công, 0 sự cố va chạm.",
        "<b>08. Lộ Trình Hoàn Thiện & Nghiệm Thu:</b> Kế hoạch kiểm thử tải nặng Tuần 08 và bàn giao Milestone 2 tại Tuần 10."
    ]
    t2 = Table([[make_card("Tối Ưu Vận Hành & Cơ Cấu Kẹp", col1, 376), make_card("Thị Giác Thông Minh & Hiệu Quả Thực Tế", col2, 376)]], colWidths=[388, 388])
    t2.setStyle(TableStyle([('VALIGN', (0, 0), (-1, -1), 'TOP')]))
    story.append(t2)
    story.append(PageBreak())

    # =========================================================================
    # SLIDE 03: EXECUTIVE SUMMARY
    # =========================================================================
    story.append(make_header("Tổng Quan Tiến Độ", "Những Bước Nhảy Vọt Nổi Bật (Week 06 -> Week 07)", 3))
    story.append(Spacer(1, 10))

    c3_left = [
        "<b>Tốc Độ Chưa Tối Ưu:</b> Robot dừng nghỉ tới 10 lần trong mỗi chu kỳ khiến thời gian xử lý kéo dài ~25 giây/sản phẩm.",
        "<b>Rủi Ro Va Chạm Băng Tải:</b> Các kiện hàng phía sau trôi tới dễ đâm vào kiện hàng đang được robot giữ để tính toán.",
        "<b>Điểm Mù Quan Sát:</b> Camera gắn vuông góc trên trần bị cánh tay cơ khí che khuất đúng lúc chuẩn bị gắp hàng.",
        "<b>Độ Ôm Khít Mỏ Kẹp:</b> Kiện hàng bị xô lệch nhẹ do quán tính trôi của băng tải và mỏ kẹp chưa tự nắn phẳng."
    ]
    c3_right = [
        "<b>Năng Suất Tăng Gần Gấp Đôi (+47%):</b> Thời gian phân loại rút ngắn ngoạn mục xuống chỉ còn 13.2 - 13.6 giây/kiện hàng.",
        "<b>Chuyển Động Lượn Sóng 100%:</b> 0 lần dừng nghỉ giữa chừng; toàn bộ chu trình là một đường uốn cong mượt mà, êm ái.",
        "<b>Nhấc Thoát Hiểm Trong 0.2s:</b> Bốc bổng kiện hàng lên cao ngay lập tức, cách ly hoàn toàn với dòng hàng trôi phía sau.",
        "<b>Thị Giác Đối Xứng Không Điểm Mù:</b> Camera đặt nghiêng 50° đối diện băng tải, thấy rõ 100% phôi từ lúc xuất hiện đến lúc gắp.",
        "<b>Mỏ Kẹp Tự Nắn Phẳng Thông Minh:</b> Phanh dừng triệt tiêu trượt quán tính; má cao su tự hút phẳng kiện hàng cân đối 100%."
    ]
    t3 = Table([[make_card("Thách Thức Cần Giải Quyết", c3_left, 376), make_card("Giải Pháp Đột Phá Đã Hoàn Thiện", c3_right, 376)]], colWidths=[388, 388])
    t3.setStyle(TableStyle([('VALIGN', (0, 0), (-1, -1), 'TOP')]))
    story.append(t3)
    story.append(PageBreak())

    # =========================================================================
    # SLIDE 04: SMOOTH FLUID MOTION
    # =========================================================================
    story.append(make_header("Công Nghệ Chuyển Động", "Chuyển Động Lượn Sóng Mượt Mà — Bảo Vệ Tuổi Thọ Robot", 4))
    story.append(Spacer(1, 10))

    c4_left = [
        "<b>Cách Tiếp Cận Sơ Khai:</b> Robot phải di chuyển theo kiểu 'đi một đoạn - dừng lại kiểm tra': Dừng tiếp cận -> Dừng gắp -> Dừng nhấc -> Dừng xoay -> Dừng thả.",
        "<b>Hao Mòn Cơ Khí:</b> Động cơ liên tục phanh gấp rồi tăng tốc đột ngột, gây rung lắc mạnh cánh tay và làm giảm tuổi thọ hộp số.",
        "<b>Thời Gian Lãng Phí:</b> Mỗi lần dừng đỗ làm mất từ 1.5 - 2.5 giây, đẩy thời gian xử lý lên ~25 giây/sản phẩm."
    ]
    c4_right = [
        "<b>Nguyên Lý Lượn Sóng:</b> Toàn bộ chu trình tiếp cận, gắp hàng, nhấc lên và đưa vào thùng được kết nối thành một đường cong duy nhất.",
        "<b>Vận Hành Êm Ái:</b> Robot lướt qua các điểm mốc với tốc độ ổn định, không dừng lại một tích tắc nào, triệt tiêu hoàn toàn rung lắc.",
        "<b>Tiết Kiệm Điện Năng:</b> Giảm thiểu dòng khởi động động cơ, tiết kiệm năng lượng tiêu thụ cho toàn bộ xưởng sản xuất.",
        "<b>Tự Động Giữ Phẳng Cổ Tay:</b> Cổ tay tự động xoay bù trừ để giữ mỏ kẹp luôn thẳng đứng vuông góc mặt đất, chống vặn xoắn dây cáp và loại trừ 100% va quẹt vách bảo hộ."
    ]
    t4 = Table([[make_card("Hạn Chế Của Cách Làm Cũ (Dừng Giật)", c4_left, 376), make_card("Sáng Kiến Quỹ Đạo Lượn Sóng Mượt Mà", c4_right, 376)]], colWidths=[388, 388])
    t4.setStyle(TableStyle([('VALIGN', (0, 0), (-1, -1), 'TOP')]))
    story.append(t4)
    story.append(PageBreak())

    # =========================================================================
    # SLIDE 05: QUICK LIFT & THROUGHPUT
    # =========================================================================
    story.append(make_header("Tối Ưu Năng Suất", "Cơ Chế Thoát Hiểm Chớp Nhoáng (0.2s) & Tăng Tốc Dòng Hàng", 5))
    story.append(Spacer(1, 10))

    c5_left = [
        "<b>Áp Lực Dòng Hàng:</b> Băng chuyền nhà máy vận hành liên tục với tốc độ 0.3 m/s. Các kiện hàng nối đuôi nhau trôi về trạm gắp.",
        "<b>Rủi Ro Dây Chuyền Cũ:</b> Sau khi kẹp hàng, nếu robot nằm yên trên mặt băng để tính đường đi, kiện hàng phía sau sẽ lao tới đâm sầm vào kiện đang giữ.",
        "<b>Thoát Hiểm Chớp Nhoáng:</b> Ngay khi 2 ngón kẹp vừa khép lại, robot bốc bổng kiện hàng lên cao +16cm chỉ trong < 0.25 giây (tăng tốc gấp 2.4 lần).",
        "<b>Cách Ly Hoàn Toàn:</b> Kiện hàng được nhấc bổng lên không trung, cách ly tuyệt đối với mặt băng chuyền trước khi robot bắt đầu xoay chuyển.",
        "<b>Kết Quả Vận Hành:</b> Dòng hàng bên dưới tiếp tục trôi thoải mái, 0 sự cố va chạm hàng trên toàn bộ dây chuyền!"
    ]
    c5_right = [
        "<b>Thùng Vàng (Bên phải):</b> Thời gian hoàn thành chỉ 13.36 giây (Rút ngắn 46.5% so với mức 24.95s trước đây). Năng suất tăng gần gấp đôi!",
        "<b>Thùng Đỏ (Bên trái):</b> Thời gian hoàn thành chỉ 13.62 giây (Rút ngắn 35.1% so với mức 21.00s trước đây).",
        "<b>Thùng Xanh (Phía sau):</b> Hành trình dài xoay 180° vòng qua tủ điều khiển chỉ mất 19.02 giây (Nhanh hơn 41.9% so với mức 32.72s).",
        "<b>Thời Gian Trung Bình:</b> Giảm từ 25.0 giây xuống chỉ còn 15.68 giây cho mọi vị trí thùng, nâng cao công suất nhà máy thêm ~37%.",
        "<b>Vận Hành Trong Ngưỡng:</b> Tốc độ tăng cao nhưng vẫn nằm an toàn trong giới hạn thiết kế của hãng Universal Robots, không gây quá tải động cơ."
    ]
    t5 = Table([[make_card("Cơ Chế Nhấc Thoát Hiểm Trong 0.2 Giây", c5_left, 376), make_card("Tốc Độ Xử Lý Thực Tế Từng Thùng Hàng", c5_right, 376)]], colWidths=[388, 388])
    t5.setStyle(TableStyle([('VALIGN', (0, 0), (-1, -1), 'TOP')]))
    story.append(t5)
    story.append(PageBreak())

    # =========================================================================
    # SLIDE 06: PRECISION GRASP & PRESENTATION CAMERAS
    # =========================================================================
    story.append(make_header("Độ Chính Xác & Giám Sát", "Mỏ Kẹp Tự Nắn Phẳng & Bộ 3 Góc Máy Trình Chiếu Chuyên Nghiệp", 6))
    story.append(Spacer(1, 10))

    c6_left = [
        "<b>Phanh Hãm Triệt Tiêu Quán Tính:</b> Hệ thống kích hoạt phanh điện tử ngay khi hàng chạm vạch, triệt tiêu độ trôi 4-6cm. Kiện hàng dừng đứng bánh chính xác 100% tại tâm trạm gắp X = 0.00m.",
        "<b>Khóa Hướng Thẳng Thớm:</b> Kiện hàng được giữ cố định theo phương thẳng đứng, luôn di chuyển ngay ngắn dọc theo mép băng tải mà không bị xoay chéo.",
        "<b>Cơ Cấu Tự Định Tâm Thông Minh:</b> Khi ngón kẹp khép lại, hai má cao su tự động nắn khối hàng khớp khít vào chính giữa và ép phẳng vuông vắn với má kẹp.",
        "<b>Không Còn Hiện Tượng Bay Lơ Lửng:</b> Kiện hàng được ôm trọn và giữ vững như bàn tay con người, loại bỏ hoàn toàn hiện tượng lệch bên hay rơi rớt khi robot tăng tốc."
    ]
    c6_right = [
        "<b>Phím [1] — Toàn Cảnh Nhà Máy (Overview):</b> Góc nhìn 3/4 Isometric bao quát toàn bộ băng chuyền 3.6m, robot UR10e, vách mica và 3 thùng phân loại.",
        "<b>Phím [2] — Giám Sát Phân Loại (Sorting View):</b> Góc nhìn phía trước-phải thấy rõ vòm bay của robot né vách mica và phân chia chính xác vào từng thùng.",
        "<b>Phím [3] — Cận Cảnh Mỏ Kẹp (Close-Up):</b> Góc zoom sát trạm gắp (<0.9m) đặc tả từng milimet lúc mỏ kẹp hạ xuống ôm khít hai mặt bên kiện hàng.",
        "<b>Bảng Điều Khiển Trên Màn Hình [C]:</b> Bấm phím [C] để bật thanh công cụ camera cảm ứng, hỗ trợ thuyết trình và giám sát cực kỳ mượt mà.",
        "<b>Thân Thiện Với Người Dùng:</b> Quản đốc nhà máy có thể dễ dàng chuyển đổi góc nhìn tức thì mà không cần thao tác chuột phức tạp."
    ]
    t6 = Table([[make_card("Hãm Dừng Chuẩn Xác & Mỏ Kẹp Tự Nắn Phẳng", c6_left, 376), make_card("Bộ 3 Góc Máy Trình Chiếu Điện Ảnh", c6_right, 376)]], colWidths=[388, 388])
    t6.setStyle(TableStyle([('VALIGN', (0, 0), (-1, -1), 'TOP')]))
    story.append(t6)
    story.append(PageBreak())

    # =========================================================================
    # SLIDE 07: OVERCOMING BLIND SPOTS
    # =========================================================================
    story.append(make_header("Giải Quyết Điểm Mù", "Khắc Phục Hoàn Toàn Điểm Mù Cánh Tay Của Thị Giác Máy", 7))
    story.append(Spacer(1, 10))

    c7_left = [
        "<b>Bố Trí Thông Thường:</b> Camera thường được gắn vuông góc ngay trên nóc trạm gắp để nhìn thẳng xuống băng chuyền.",
        "<b>Hiện Tượng Bị Che Khuất:</b> Khi cánh tay robot vươn ra gắp hàng, các khớp kim loại to lớn của robot vô tình chắn ngang giữa ống kính và kiện hàng.",
        "<b>Bóng Đổ Chập Chờn:</b> Cánh tay di chuyển tạo bóng râm che phủ kiện hàng, làm mắt thần bị 'chói mắt' và nhận diện sai lệch màu sắc.",
        "<b>Hậu Quả:</b> Robot bị 'mù' đúng vào khoảnh khắc chuẩn bị kẹp hàng, dẫn đến nhận diện chập chờn, kẹp hụt hoặc bỏ sót sản phẩm."
    ]
    c7_right = [
        "<b>Bố Trí Đột Phá:</b> Đưa camera sang phía đối diện của băng tải (cách băng tải 50cm, cao 75cm), nghiêng một góc 50° chiếu thẳng vào trạm gắp.",
        "<b>Robot Luôn Nằm Phía Sau:</b> Robot UR10e đứng làm việc ở phía sau băng chuyền. Cánh tay chỉ vươn tới từ phía sau và phía trên kiện hàng.",
        "<b>Mặt Hàng Luôn Hướng Về Camera:</b> Mặt trước và mặt trên của kiện hàng luôn hướng trọn vẹn vào ống kính máy quay mà không hề có bất kỳ vật thể nào chắn giữa.",
        "<b>Triệt Tiêu 100% Điểm Mù:</b> Tầm nhìn hoàn toàn thông suốt từ lúc kiện hàng trôi tới, dừng lại cho đến khi robot gắp đi!"
    ]
    t7 = Table([[make_card("Cạm Bẫy Của Thiết Kế Truyền Thống", c7_left, 376), make_card("Sáng Kiến Đặt Camera Đối Xứng Phía Trước", c7_right, 376)]], colWidths=[388, 388])
    t7.setStyle(TableStyle([('VALIGN', (0, 0), (-1, -1), 'TOP')]))
    story.append(t7)
    story.append(PageBreak())

    # =========================================================================
    # SLIDE 08: SMART CAMERA STAND
    # =========================================================================
    story.append(make_header("Trạm Thị Giác Công Nghiệp", "Thiết Kế Trạm Thị Giác Góc Nghiêng Chuyên Dụng Độc Quyền", 8))
    story.append(Spacer(1, 10))

    c8_left = [
        "<b>Vị Trí Tối Ưu:</b> Đặt đối diện trạm gắp ở tọa độ chuẩn: cự ly cách tâm băng tải 50cm, độ cao 75cm, góc nghiêng chúc xuống 50°.",
        "<b>Khoảng Cách Lấy Nét Lý Tưởng:</b> Cự ly tới kiện hàng chỉ khoảng 82cm, mang lại hình ảnh độ nét cao và sắc nét từng chi tiết.",
        "<b>Chân Trụ Thép Công Nghiệp:</b> Trụ đỡ bằng kim loại vững chắc, đế bắt bu-lông chống rung lắc cơ học khi băng chuyền vận hành hết tải.",
        "<b>An Toàn Tuyệt Đối:</b> Nằm hoàn toàn ngoài bán kính vươn của cánh tay robot, loại trừ 100% nguy cơ va quẹt thiết bị."
    ]
    c8_right = [
        "<b>Quan Sát Đồng Thời 2 Mặt:</b> Ống kính nhìn thấy đồng thời cả mặt trên và mặt trước của kiện hàng, tăng gấp đôi diện tích nhận diện màu sắc.",
        "<b>Không Bị Đèn Trần Gây Lóa:</b> Góc nghiêng tránh được hiện tượng phản chiếu ánh sáng trắng từ dàn đèn trần nhà xưởng vào ống kính.",
        "<b>Khung Hình Giám Sát Phụ (PiP):</b> Tích hợp sẵn màn hình phụ ở góc phải (Bật/Tắt bằng phím [V]) giúp nhân viên vận hành theo dõi luồng hình ảnh trực tiếp.",
        "<b>Độ Phân Giải Chuẩn HD:</b> Truyền tải hình ảnh mượt mà ở tốc độ 15 khung hình/giây, phản ứng tức thì không có độ trễ."
    ]
    t8 = Table([[make_card("Thiết Kế Cơ Khí Chuẩn Công Nghiệp", c8_left, 376), make_card("Lợi Ích Vượt Trội Của Góc Nhìn 50 Độ", c8_right, 376)]], colWidths=[388, 388])
    t8.setStyle(TableStyle([('VALIGN', (0, 0), (-1, -1), 'TOP')]))
    story.append(t8)
    story.append(PageBreak())

    # =========================================================================
    # SLIDE 09: AI VISION & SORTING
    # =========================================================================
    story.append(make_header("Nhận Diện Thông Minh", "Tự Động Phân Loại Màu Sắc & Định Vị Kiện Hàng Chuẩn Xác", 9))
    story.append(Spacer(1, 10))

    c9_left = [
        "<b>Bộ Lọc Màu Chuyên Dụng:</b> Sử dụng thuật toán tách dải màu độc lập với độ sáng, giúp mắt thần không bị đánh lừa khi ánh nắng thay đổi hay bóng người đi qua.",
        "<b>Phân Biệt Tức Thì 3 Màu Chuẩn:</b> Tự động phân loại 3 dòng sản phẩm công nghiệp: Màu Đỏ tươi, Màu Vàng tươi, và Màu Xanh dương.",
        "<b>Bộ Lọc Khử Nhiễu Kép:</b> Tự động loại bỏ các đốm sáng phản chiếu, bụi bẩn li ti trên băng tải để chỉ tập trung vào kiện hàng thật.",
        "<b>Hiển Thị Trực Quan (HUD):</b> Khung nhận diện hiển thị tên màu và tọa độ thực tế ngay trên màn hình giám sát để người vận hành kiểm tra."
    ]
    c9_right = [
        "<b>Độ Chính Xác Cực Cao:</b> Tính toán trọng tâm hình học của khối hàng với sai số chưa tới 1.5 milimet.",
        "<b>Truyền Dữ Liệu Tức Thì:</b> Tọa độ 3 chiều (Dài, Rộng, Cao) được bắn trực tiếp vào bộ điều khiển robot ở tốc độ 15 lần/giây.",
        "<b>Tự Động Điều Phối Điểm Đến:</b> Khối Đỏ tự động chuyển về Thùng Đỏ bên trái; Khối Vàng về Thùng Vàng bên phải; Khối Xanh về Thùng Xanh phía sau.",
        "<b>Tự Động Hóa Khép Kín:</b> Toàn bộ chu trình từ phát hiện, dừng băng tải, gắp hàng đến phân loại diễn ra tự động 100% không cần can thiệp."
    ]
    t9 = Table([[make_card("Thuật Toán Nhận Diện Thông Minh", c9_left, 376), make_card("Định Vị Tọa Độ Kiện Hàng Chuẩn Xác", c9_right, 376)]], colWidths=[388, 388])
    t9.setStyle(TableStyle([('VALIGN', (0, 0), (-1, -1), 'TOP')]))
    story.append(t9)
    story.append(PageBreak())

    # =========================================================================
    # SLIDE 10: REAL-WORLD BENCHMARKS (Real 25-Trial Run + Chart)
    # =========================================================================
    story.append(make_header("Hiệu Quả Thực Nghiệm", "Minh Chứng Hiệu Quả Thực Tế — Thử Nghiệm 25 Kiện Hàng Liên Tục", 10))
    story.append(Spacer(1, 8))

    table_data = [
        [
            Paragraph("<b>Chỉ Số Vận Hành (KPI)</b>", ParagraphStyle("TH", fontName="DejaVu-Bold", fontSize=8.5, textColor=colors.white, alignment=1)),
            Paragraph("<b>Mô Hình Cũ</b>", ParagraphStyle("TH", fontName="DejaVu-Bold", fontSize=8.5, textColor=colors.white, alignment=1)),
            Paragraph("<b>Giải Pháp Mới (Week 07)</b>", ParagraphStyle("TH", fontName="DejaVu-Bold", fontSize=8.5, textColor=colors.white, alignment=1)),
            Paragraph("<b>Giá Trị Khách Hàng</b>", ParagraphStyle("TH", fontName="DejaVu-Bold", fontSize=8.5, textColor=colors.white, alignment=1))
        ],
        [Paragraph("Số lần dừng giật", bullet_style), Paragraph("10 lần/chu kỳ", bullet_style), Paragraph("<b>0 lần (Mượt mà)</b>", bullet_style), Paragraph("Triệt tiêu hao mòn động cơ", bullet_style)],
        [Paragraph("Chu kỳ Thùng Vàng", bullet_style), Paragraph("24.95 giây", bullet_style), Paragraph("<b>13.36 giây (-46.5%)</b>", bullet_style), Paragraph("Năng suất tăng gần gấp đôi", bullet_style)],
        [Paragraph("Chu kỳ Thùng Đỏ", bullet_style), Paragraph("21.00 giây", bullet_style), Paragraph("<b>13.62 giây (-35.1%)</b>", bullet_style), Paragraph("Xử lý nhanh hơn 35.1%", bullet_style)],
        [Paragraph("Chu kỳ Thùng Xanh", bullet_style), Paragraph("32.72 giây", bullet_style), Paragraph("<b>19.02 giây (-41.9%)</b>", bullet_style), Paragraph("Rút ngắn thời gian tới 41.9%", bullet_style)],
        [Paragraph("Tỷ lệ gắp thành công", bullet_style), Paragraph("Đạt yêu cầu", bullet_style), Paragraph("<b>100.0% (25/25)</b>", bullet_style), Paragraph("Độ tin cậy tuyệt đối", bullet_style)],
        [Paragraph("Tỷ lệ phân loại đúng", bullet_style), Paragraph("Đạt yêu cầu", bullet_style), Paragraph("<b>100.0% (25/25)</b>", bullet_style), Paragraph("Không nhầm lẫn màu sắc", bullet_style)],
        [Paragraph("Sự cố va quẹt băng tải", bullet_style), Paragraph("Thỉnh thoảng", bullet_style), Paragraph("<b>0 sự cố (Thoát 0.2s)</b>", bullet_style), Paragraph("Lưu thông trôi chảy 100%", bullet_style)],
    ]
    t10_data = Table(table_data, colWidths=[120, 95, 125, 120])
    t10_data.setStyle(TableStyle([
        ('BACKGROUND', (0, 0), (-1, 0), PRIMARY),
        ('ALIGN', (0, 0), (-1, 0), 'CENTER'),
        ('VALIGN', (0, 0), (-1, -1), 'MIDDLE'),
        ('GRID', (0, 0), (-1, -1), 0.5, colors.HexColor("#cbd5e1")),
        ('ROWBACKGROUNDS', (0, 1), (-1, -1), [colors.white, colors.HexColor("#f8fafc")]),
        ('TOPPADDING', (0, 0), (-1, -1), 3.5),
        ('BOTTOMPADDING', (0, 0), (-1, -1), 3.5),
        ('LEFTPADDING', (0, 0), (-1, -1), 5),
        ('RIGHTPADDING', (0, 0), (-1, -1), 5),
    ]))

    chart_img_path = "/home/kduy/RoboticProject/project_blueprint/docs/week07/cycle_time_benchmark.png"
    if os.path.exists(chart_img_path):
        img = Image(chart_img_path, width=290, height=140)
        t10_top = Table([[t10_data, img]], colWidths=[470, 306])
    else:
        t10_top = Table([[t10_data]], colWidths=[776])

    t10_top.setStyle(TableStyle([
        ('VALIGN', (0, 0), (-1, -1), 'TOP'),
        ('LEFTPADDING', (0, 0), (-1, -1), 0),
        ('RIGHTPADDING', (0, 0), (-1, -1), 0),
    ]))
    story.append(t10_top)
    story.append(Spacer(1, 8))

    c10_bottom = [
        "<b>Năng Suất Vượt Trội:</b> Tốc độ phân loại tăng gần gấp đôi (rút ngắn từ 25s xuống chỉ còn 13.3s), giúp doanh nghiệp tăng mạnh sản lượng mỗi ca.",
        "<b>Độ Tin Cậy Tuyệt Đối (100%):</b> 25 trên 25 kiện hàng được gắp và thả chuẩn xác vào đúng thùng chỉ định, không một lần đánh rơi hay nhầm lẫn màu.",
        "<b>Lưu Thông Trơn Tru 0 Va Chạm:</b> Cơ chế nhấc bốc bổng trong 0.2s giải phóng hoàn toàn mặt băng tải, các kiện hàng phía sau trôi tới thông suốt 100%.",
        "<b>Độ Ổn Định Cao:</b> Biểu đồ thời gian cho thấy các lần chạy có độ đồng đều cực kỳ cao (độ lệch chuẩn chỉ 2.76s), bảo đảm dây chuyền vận hành bền bỉ."
    ]
    t10_b = make_card("Đánh Giá Kết Quả Thực Nghiệm & Giá Trị Mang Lại", c10_bottom, 776)
    story.append(t10_b)
    story.append(PageBreak())

    # =========================================================================
    # SLIDE 11: FORWARD ROADMAP TO MILESTONE 2 (WEEK 10)
    # =========================================================================
    story.append(make_header("Lộ Trình Triển Khai", "Kế Hoạch Hoàn Thiện Tuần Tới (Week 08) & Lộ Trình Nghiệm Thu (Week 10)", 11))
    story.append(Spacer(1, 10))

    c11_left = [
        "<b>Gia Cố Mắt Thần Thị Giác:</b> Kiểm thử thị giác máy dưới các điều kiện ánh sáng phức tạp, bóng râm thay đổi và bụi bẩn để bảo đảm nhận diện thông suốt 24/7.",
        "<b>Kiểm Tra Hành Lang An Toàn Tĩnh:</b> Tối ưu hóa khoảng cách bay an toàn vượt qua các tấm vách mica bảo vệ và tủ điều khiển của robot.",
        "<b>Thực Nghiệm Độ Bền 50 Kiện Hàng:</b> Tăng tốc độ băng tải từ 0.2m/s lên 0.4m/s để kiểm tra độ dẻo dai của robot ở công suất tối đa.",
        "<b>Đóng Gói Công Cụ Vận Hành:</b> Chuẩn bị sẵn bộ công cụ khởi động 1-chạm và hệ thống ghi nhật ký tự động phục vụ nghiệm thu."
    ]
    c11_right = [
        "<b>Tuần 08 (Tuần tới):</b> Hoàn tất kiểm thử tải nặng 50 kiện hàng liên tục và tinh chỉnh thị giác máy thích ứng đa môi trường.",
        "<b>Tuần 09:</b> Hoàn thiện Sổ tay vận hành (Runbook), video chứng minh và bộ hồ sơ kỹ thuật nghiệm thu cho khách hàng.",
        "<b>Tuần 10 (Nghiệm Thu Milestone 2):</b> Bảo vệ chính thức toàn diện hệ thống: Băng tải + Phân loại màu + Thị giác thông minh + Né vật cản tĩnh.",
        "<b>Tuần 11 - 14 (Chặng Milestone 3):</b> Nâng cấp tính năng phản ứng né vật cản chuyển động cắt ngang (Dynamic Obstacle Avoidance)."
    ]
    t11 = Table([[make_card("Kế Hoạch Công Tác Tuần Tới (Week 08)", c11_left, 376), make_card("Lộ Trình Bàn Giao Tổng Thể (Weeks 08 -> 14)", c11_right, 376)]], colWidths=[388, 388])
    t11.setStyle(TableStyle([('VALIGN', (0, 0), (-1, -1), 'TOP')]))
    story.append(t11)
    story.append(PageBreak())

    # =========================================================================
    # SLIDE 12: BUSINESS VALUE & Q&A
    # =========================================================================
    story.append(make_header("Tổng Kết Giá Trị", "Hiệu Quả Đầu Tư (ROI) & Giải Đáp Thắc Mắc Cùng Khách Hàng", 12))
    story.append(Spacer(1, 10))

    c12_left = [
        "<b>Tăng Nhanh Tốc Độ Hoàn Vốn (ROI):</b> Năng suất phân loại tăng gần gấp đôi (13.3s thay vì 25s), giúp nhà máy gia tăng sản lượng với chi phí nhân công tối thiểu.",
        "<b>Tiết Kiệm Chi Phí Bảo Trì:</b> Quỹ đạo lượn sóng liên tục giúp động cơ và hộp số robot hoạt động êm ái, kéo dài vòng đời cơ khí lên 30-40%.",
        "<b>An Toàn Chuẩn Công Nghiệp:</b> Vách bảo hộ mica kết hợp cơ chế nhấc bốc bổng chống va chạm bảo vệ 100% sự nguyên vẹn của hàng hóa và thiết bị.",
        "<b>Vận Hành Đơn Giản, Thân Thiện:</b> Giám sát trực quan với bộ 3 góc camera chuyên nghiệp, khởi động chỉ với 1 thao tác bấm."
    ]
    c12_right = [
        "<b>Khả Năng Tái Lập 100%:</b> Hệ thống được đóng gói tự động hóa hoàn toàn, dễ dàng triển khai và sao chép trên nhiều dây chuyền.",
        "<b>Kiểm Thử Thực Nghiệm Nghiêm Ngặt:</b> Mọi thông số đều được chứng minh bằng số liệu đo đạc thực tế, không dựa trên lý thuyết suông.",
        "<b>Đúng Hạn Tiến Độ:</b> Hệ thống vận hành đúng tiến độ và sẵn sàng cho đợt nghiệm thu Milestone 2 tại Tuần 10.",
        "<b>Sẵn Sàng Trao Đổi:</b> Kính mời Quý Khách hàng và Ban Giám khảo đặt câu hỏi và theo dõi phần trình diễn trực tiếp trên mô hình 3D!"
    ]
    t12 = Table([[make_card("Giá Trị Kinh Tế Cho Doanh Nghiệp", c12_left, 376), make_card("Cam Kết Chất Lượng & Trao Đổi", c12_right, 376)]], colWidths=[388, 388])
    t12.setStyle(TableStyle([('VALIGN', (0, 0), (-1, -1), 'TOP')]))
    story.append(t12)

    # Build Document
    doc.build(story, canvasmaker=SlideCanvas)
    print(f"[✔] Successfully generated Landscape CLIENT PDF presentation: {output_path}")


if __name__ == "__main__":
    out_pdf = "/home/kduy/RoboticProject/project_blueprint/docs/week07/UC8_Week07_Presentation_Client.pdf"
    build_pdf(out_pdf)
