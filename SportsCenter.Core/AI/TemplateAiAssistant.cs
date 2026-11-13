using System.Globalization;
using System.Text;

namespace SportsCenter.Core.AI
{
    /// <summary>
    /// Trợ lý "ngoại tuyến": sinh gợi ý mẫu theo quy tắc (mục tiêu, trình độ, chấn thương, lịch sử tập).
    /// Dùng khi chưa cấu hình ANTHROPIC_API_KEY hoặc khi gọi API thất bại.
    /// </summary>
    public class TemplateAiAssistant : IAiCoachAssistant
    {
        public bool IsOnline => false;
        public string ProviderName => "Gợi ý mẫu (chưa cấu hình ANTHROPIC_API_KEY)";

        private const string Note = "\n\n(Gợi ý mẫu tự động — cấu hình biến môi trường ANTHROPIC_API_KEY để nhận tư vấn AI chi tiết hơn.)";

        public Task<string> SuggestWorkoutAsync(AiMemberProfile p, string? extraRequest, CancellationToken ct = default)
        {
            var goal = Fold(p.Goals + " " + extraRequest);
            var level = Fold(p.Level);
            var health = Fold(p.HealthNotes);

            var (sets, reps, rest) = level.Contains("nang cao") || level.Contains("advanced") ? (4, "8–10", "60–90 giây")
                                   : level.Contains("trung binh") || level.Contains("intermediate") ? (3, "10–12", "60 giây")
                                   : (2, "12–15", "60–90 giây");

            var sb = new StringBuilder();
            sb.AppendLine($"Gợi ý cho {p.Name} (trình độ {p.Level}, {p.SessionsLast30Days} buổi trong 30 ngày qua):");
            sb.AppendLine();

            if (Has(goal, "giam can", "giam mo", "giam beo", "eo", "dot mo"))
            {
                sb.AppendLine("Mục tiêu giảm mỡ – 4 buổi/tuần, kết hợp sức mạnh toàn thân và cardio:");
                sb.AppendLine("- Khởi động: đạp xe/đi bộ nhanh 8–10 phút, xoay khớp.");
                sb.AppendLine($"- Squat với tạ đơn: {sets} hiệp × {reps} lần, nghỉ {rest}.");
                sb.AppendLine($"- Hít đất (có thể chống gối): {sets} hiệp × {reps} lần.");
                sb.AppendLine($"- Kéo cáp ngồi (Seated row): {sets} hiệp × {reps} lần.");
                sb.AppendLine("- HIIT: 30 giây chạy nhanh / 30 giây đi bộ × 8–10 vòng (máy chạy hoặc xe đạp).");
                sb.AppendLine("- Plank: 3 × 30–45 giây.");
                sb.AppendLine("- Ngoài giờ tập: đi bộ 7.000–10.000 bước/ngày, ăn thâm hụt nhẹ 300–500 kcal.");
            }
            else if (Has(goal, "tang co", "co bap", "the hinh", "tang can", "suc manh", "ngực", "nguc"))
            {
                sb.AppendLine("Mục tiêu tăng cơ – chia nhóm cơ 4 buổi/tuần (Đẩy / Kéo / Chân / Toàn thân):");
                sb.AppendLine($"- Đẩy: Đẩy ngực thanh đòn {sets}×{reps}, Đẩy vai tạ đơn {sets}×{reps}, Dip/Triceps cáp 3×12.");
                sb.AppendLine($"- Kéo: Lat pulldown {sets}×{reps}, Chèo tạ đơn {sets}×{reps}, Cuốn tạ tay 3×12.");
                sb.AppendLine($"- Chân: Squat {sets}×{reps}, Romanian deadlift {sets}×{reps}, Đạp đùi 3×12, Bắp chân 3×15.");
                sb.AppendLine("- Tăng tải 2,5–5% mỗi tuần khi hoàn thành đủ số lần với kỹ thuật tốt.");
                sb.AppendLine("- Dinh dưỡng: 1,6–2 g protein/kg cân nặng, ngủ đủ 7–8 tiếng.");
            }
            else if (Has(goal, "suc ben", "chay", "marathon", "tim mach", "the luc", "boi"))
            {
                sb.AppendLine("Mục tiêu sức bền – 3–4 buổi/tuần:");
                sb.AppendLine("- 2 buổi cardio nền: chạy/đạp xe nhịp tim vùng 2 (nói chuyện được) 30–45 phút.");
                sb.AppendLine("- 1 buổi interval: 5–6 × 3 phút nhanh / 2 phút chậm.");
                sb.AppendLine($"- 1 buổi sức mạnh bổ trợ: Lunge {sets}×{reps}, Step-up {sets}×{reps}, Plank 3×45 giây, Hip thrust {sets}×{reps}.");
                sb.AppendLine("- Tăng tổng thời lượng không quá 10%/tuần.");
            }
            else if (Has(goal, "deo", "yoga", "linh hoat", "thu gian", "stress", "tu the"))
            {
                sb.AppendLine("Mục tiêu dẻo dai & tư thế – 3 buổi/tuần:");
                sb.AppendLine("- Chuỗi chào mặt trời 5–8 vòng để khởi động.");
                sb.AppendLine("- Giữ tư thế: Chó úp mặt, Chiến binh 2, Tam giác, Bồ câu – mỗi tư thế 30–60 giây mỗi bên.");
                sb.AppendLine("- Tăng cường core: Bird-dog 3×10 mỗi bên, Dead bug 3×10, Plank nghiêng 3×20 giây.");
                sb.AppendLine("- Kết thúc: thở sâu 5 phút, giãn cơ hông và đùi sau.");
            }
            else
            {
                sb.AppendLine("Chương trình sức khỏe tổng quát – 3 buổi/tuần toàn thân:");
                sb.AppendLine($"- Goblet squat {sets}×{reps}, Hít đất {sets}×{reps}, Kéo cáp {sets}×{reps}.");
                sb.AppendLine($"- Hip hinge với tạ đơn {sets}×{reps}, Plank 3×30–45 giây.");
                sb.AppendLine("- Cardio nhẹ 15–20 phút cuối buổi.");
            }

            if (p.RecentWorkouts.Count > 0)
                sb.AppendLine($"- Dựa trên buổi gần nhất ({p.RecentWorkouts[0]}): giữ bài tập cũ và tăng nhẹ tải hoặc số lần nếu cảm thấy dễ.");

            if (Has(health, "lung", "cot song", "thoat vi"))
                sb.AppendLine("Lưu ý lưng: tránh deadlift nặng và gập bụng, ưu tiên bài ổn định core (bird-dog, plank), giữ lưng trung tính.");
            if (Has(health, "goi", "khop goi"))
                sb.AppendLine("Lưu ý đầu gối: giảm biên độ squat, tránh nhảy bật; thay chạy bằng đạp xe hoặc bơi.");
            if (Has(health, "vai"))
                sb.AppendLine("Lưu ý vai: tránh đẩy qua đầu nặng, khởi động kỹ với dây kháng lực.");
            if (Has(health, "huyet ap", "tim"))
                sb.AppendLine("Lưu ý tim mạch/huyết áp: tránh nín thở khi gắng sức, giữ cường độ vừa phải và hỏi ý kiến bác sĩ.");

            return Task.FromResult(sb.ToString().TrimEnd() + Note);
        }

        public async Task<string> AskAsync(string question, AiMemberProfile? profile, string centerInfo, IReadOnlyList<AiChatTurn> history, CancellationToken ct = default)
        {
            var q = Fold(question);
            string answer;
            // Lưu ý: khi bỏ dấu, "gối" (đầu gối) và "gói" (gói tập) đều thành "goi" -> xét chấn thương trước,
            // và chỉ nhận diện gói tập bằng cụm từ cụ thể.
            var injury = Has(q, "dau", "chan thuong", "dau goi", "khop", "bong gan", "cang co", "moi co");
            if (injury && profile != null && Has(q, "thay", "bai", "tap", "squat", "chay"))
                return InjuryAdvice(q) + "\n\n" + await SuggestWorkoutAsync(profile with { HealthNotes = profile.HealthNotes + " " + question }, question, ct);
            if (injury)
                answer = InjuryAdvice(q);
            else if (Has(q, "bai tap", "tap gi", "giao an", "ke hoach tap", "nen tap", "goi y") && profile != null)
                return await SuggestWorkoutAsync(profile, question, ct);
            else if (Has(q, "gio mo", "mo cua", "dong cua", "may gio", "gio lam"))
                answer = Extract(centerInfo, "Giờ mở cửa") ?? "Trung tâm mở cửa từ 5:30 đến 22:00 hằng ngày.";
            else if (Has(q, "goi tap", "goi thanh vien", "cac goi", "goi nao", "bang gia", "gia goi", "bao nhieu tien", "hoc phi", "gia ve", "chi phi"))
                answer = "Các gói thành viên hiện có:\n" + (Extract(centerInfo, "Gói") ?? "Vui lòng xem mục Gói thành viên hoặc liên hệ quầy lễ tân.");
            else if (Has(q, "lich", "lop", "buoi"))
                answer = profile is { Classes.Count: > 0 }
                    ? $"Bạn đang học: {string.Join(", ", profile.Classes)}. Xem chi tiết ngày giờ trong mục \"Lịch tập của tôi\"."
                    : "Bạn chưa đăng ký lớp nào. Vào mục \"Lớp học\" để xem lịch và đăng ký.";
            else if (Has(q, "an", "dinh duong", "protein", "an kieng", "che do an"))
                answer = "Gợi ý dinh dưỡng chung:\n- Mỗi bữa có 1 phần đạm (thịt nạc, cá, trứng, đậu), 1–2 phần rau, 1 phần tinh bột chậm (gạo lứt, khoai).\n"
                       + "- Uống 2–2,5 lít nước/ngày, nhiều hơn vào ngày tập.\n- Ăn nhẹ giàu đạm trong 1–2 giờ sau buổi tập.";
            else if (Has(q, "huan luyen vien", "hlv", "pt"))
                answer = "Bạn có thể xem thông tin huấn luyện viên của từng lớp trong mục \"Lịch tập của tôi\" hoặc \"Lớp học\". Để thuê PT riêng, vui lòng liên hệ quầy lễ tân.";
            else
                answer = "Mình có thể giúp về: bài tập phù hợp mục tiêu, lịch học, gói thành viên, giờ mở cửa và dinh dưỡng cơ bản. "
                       + "Bạn thử hỏi cụ thể hơn, ví dụ: \"Gợi ý bài tập giảm mỡ bụng cho người mới\".";
            return answer + Note;
        }

        private static string InjuryAdvice(string q)
        {
            var sb = new StringBuilder();
            if (Has(q, "dau goi", "goi"))
                sb.AppendLine("Khi đau đầu gối: tạm thay squat/lunge sâu bằng các bài ít tải lên gối như Glute bridge 3×15, Hip thrust 3×12, "
                            + "đạp đùi biên độ ngắn (không gập gối quá 90°), Romanian deadlift nhẹ 3×10; cardio bằng đạp xe hoặc bơi thay cho chạy/nhảy.");
            if (Has(q, "lung", "cot song"))
                sb.AppendLine("Khi đau lưng: tránh deadlift nặng, gập bụng; ưu tiên Bird-dog, Dead bug, Plank 3×20–30 giây và giữ lưng trung tính.");
            if (Has(q, "vai"))
                sb.AppendLine("Khi đau vai: tránh đẩy qua đầu và dip; dùng dây kháng lực xoay ngoài vai 3×15, kéo cáp mặt (face pull) nhẹ.");
            sb.AppendLine("Nếu đau nhói, sưng hoặc kéo dài trên 3 ngày: dừng tập vùng đó, chườm lạnh 15–20 phút và nên đi khám bác sĩ. "
                        + "Mỏi cơ thông thường sau tập (DOMS) sẽ giảm sau 2–3 ngày.");
            return sb.ToString().TrimEnd();
        }

        private static string? Extract(string info, string key)
        {
            var lines = info.Split('\n').Where(l => l.Contains(key, StringComparison.OrdinalIgnoreCase)).ToList();
            return lines.Count == 0 ? null : string.Join("\n", lines.Select(l => l.Trim()));
        }

        private static bool Has(string text, params string[] keys) => keys.Any(k => text.Contains(Fold(k)));

        private static string Fold(string? s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var n = s.Replace('đ', 'd').Replace('Đ', 'D').Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var ch in n)
                if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) sb.Append(char.ToLowerInvariant(ch));
            return sb.ToString();
        }
    }
}
