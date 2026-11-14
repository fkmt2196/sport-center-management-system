using System.Text;
using Anthropic;
using Anthropic.Models.Beta;
using Anthropic.Models.Beta.Messages;

namespace SportsCenter.BLL.AI
{
    /// <summary>
    /// Trợ lý AI dùng Claude (SDK chính thức của Anthropic). API key đọc từ biến môi trường ANTHROPIC_API_KEY.
    /// Lỗi mạng/API hoặc bị từ chối trả lời thì chuyển sang bản gợi ý mẫu để ứng dụng vẫn chạy.
    /// </summary>
    public class ClaudeAiAssistant : IAiCoachAssistant
    {
        private const string Model = "claude-opus-5-5";
        private readonly AnthropicClient _client = new();
        private readonly IAiCoachAssistant _fallback;

        public ClaudeAiAssistant(IAiCoachAssistant fallback) { _fallback = fallback; }

        public bool IsOnline => true;
        public string ProviderName => "Claude (Anthropic)";

        private const string SystemPrompt = """
            Bạn là trợ lý huấn luyện của một trung tâm thể thao/fitness tại Việt Nam.
            Luôn trả lời bằng tiếng Việt, ngắn gọn, thực tế, dạng văn bản thuần (gạch đầu dòng bằng "-", không dùng markdown đậm/nghiêng/bảng).
            Khi gợi ý bài tập: nêu tên bài, số hiệp × số lần hoặc thời gian, thời gian nghỉ, và lưu ý kỹ thuật; điều chỉnh theo trình độ, mục tiêu, lịch sử tập.
            Tôn trọng lưu ý sức khỏe/chấn thương: tránh bài gây tải lên vùng bị đau và khuyên hỏi ý kiến bác sĩ khi cần. Không chẩn đoán bệnh.
            Với câu hỏi về dịch vụ, giờ mở cửa, gói tập: chỉ dùng thông tin trung tâm được cung cấp; nếu không có thông tin thì nói rõ và gợi ý liên hệ quầy lễ tân.
            """;

        public async Task<string> SuggestWorkoutAsync(AiMemberProfile profile, string? extraRequest, CancellationToken ct = default)
        {
            var prompt = $"""
                Hãy đề xuất buổi tập/kế hoạch tập tuần tới cho thành viên sau.

                {Describe(profile)}

                Yêu cầu thêm: {(string.IsNullOrWhiteSpace(extraRequest) ? "(không có)" : extraRequest)}
                """;
            return await CallAsync(new List<BetaMessageParam> { new BetaUserMessageParam { Content = prompt } }, ct)
                   ?? await _fallback.SuggestWorkoutAsync(profile, extraRequest, ct);
        }

        public async Task<string> AskAsync(string question, AiMemberProfile? profile, string centerInfo, IReadOnlyList<AiChatTurn> history, CancellationToken ct = default)
        {
            var messages = new List<BetaMessageParam>();
            var context = $"""
                THÔNG TIN TRUNG TÂM:
                {centerInfo}

                {(profile == null ? "" : "THÔNG TIN NGƯỜI HỎI:\n" + Describe(profile))}
                """;
            // Lịch sử hội thoại (bỏ lượt đầu nếu là của trợ lý để bảo đảm bắt đầu bằng user)
            var turns = history.SkipWhile(t => !t.IsUser).TakeLast(10).ToList();
            var first = true;
            foreach (var t in turns)
            {
                var text = first && t.IsUser ? context + "\n\nCÂU HỎI: " + t.Text : t.Text;
                messages.Add(t.IsUser ? new BetaUserMessageParam { Content = text } : new BetaAssistantMessageParam { Content = text });
                first = false;
            }
            messages.Add(new BetaUserMessageParam { Content = first ? context + "\n\nCÂU HỎI: " + question : question });

            return await CallAsync(messages, ct) ?? await _fallback.AskAsync(question, profile, centerInfo, history, ct);
        }

        /// <summary>Gọi Messages API; trả về null nếu lỗi hoặc bị từ chối để dùng bản mẫu.</summary>
        private async Task<string?> CallAsync(List<BetaMessageParam> messages, CancellationToken ct)
        {
            try
            {
                var response = await _client.Beta.Messages.Create(new MessageCreateParams
                {
                    Model = Model,
                    MaxTokens = 8000,
                    System = SystemPrompt,
                    Messages = messages,
                    // Nếu mô hình chính từ chối (bộ lọc an toàn), máy chủ tự chuyển sang mô hình dự phòng.
                    Betas = [AnthropicBeta.ServerSideFallback2026_07_01],
                    Fallbacks = new Default(),
                }, cancellationToken: ct);

                if (response.StopReason == "refusal") return null;
                var sb = new StringBuilder();
                foreach (var block in response.Content)
                    if (block.TryPickText(out var text)) sb.Append(text.Text);
                var result = sb.ToString().Trim();
                return result.Length == 0 ? null : result;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Claude API lỗi, dùng gợi ý mẫu: {ex.Message}");
                return null;
            }
        }

        internal static string Describe(AiMemberProfile p) => $"""
            - Họ tên: {p.Name}; giới tính: {p.Gender}; tuổi: {p.Age?.ToString() ?? "?"}
            - Chiều cao: {p.HeightCm?.ToString("0") ?? "?"} cm; cân nặng: {p.WeightKg?.ToString("0.#") ?? "?"} kg
            - Trình độ: {p.Level}
            - Mục tiêu: {(p.Goals.Length > 0 ? p.Goals : "(chưa ghi)")}
            - Lưu ý sức khỏe: {(p.HealthNotes.Length > 0 ? p.HealthNotes : "không có")}
            - Lớp đang học: {(p.Classes.Count > 0 ? string.Join(", ", p.Classes) : "chưa có")}
            - Số buổi tập 30 ngày qua: {p.SessionsLast30Days}
            - Kết quả tập gần đây: {(p.RecentWorkouts.Count > 0 ? string.Join(" | ", p.RecentWorkouts) : "chưa có")}
            """;
    }
}
