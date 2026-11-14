namespace SportsCenter.BLL.AI
{
    /// <summary>Thông tin thành viên đưa vào AI để cá nhân hóa gợi ý.</summary>
    public record AiMemberProfile(
        string Name,
        string Gender,
        int? Age,
        double? HeightCm,
        double? WeightKg,
        string Level,
        string Goals,
        string HealthNotes,
        IReadOnlyList<string> RecentWorkouts,
        int SessionsLast30Days,
        IReadOnlyList<string> Classes);

    /// <summary>Một lượt trong hội thoại (IsUser = true: người dùng; false: trợ lý).</summary>
    public record AiChatTurn(bool IsUser, string Text);

    /// <summary>
    /// Trợ lý AI của trung tâm. Giao diện chỉ phụ thuộc interface này, nên có thể thay nhà cung cấp
    /// hoặc dùng bản trả lời mẫu (TemplateAiAssistant) khi chưa cấu hình API key.
    /// </summary>
    public interface IAiCoachAssistant
    {
        /// <summary>True nếu đang gọi mô hình AI thật; false nếu dùng gợi ý mẫu.</summary>
        bool IsOnline { get; }

        string ProviderName { get; }

        /// <summary>Gợi ý bài tập theo mục tiêu, trình độ và lịch sử tập (cho HLV hoặc thành viên).</summary>
        Task<string> SuggestWorkoutAsync(AiMemberProfile profile, string? extraRequest, CancellationToken ct = default);

        /// <summary>Trả lời câu hỏi về lịch tập, bài tập, dịch vụ của trung tâm.</summary>
        Task<string> AskAsync(string question, AiMemberProfile? profile, string centerInfo, IReadOnlyList<AiChatTurn> history, CancellationToken ct = default);
    }

    public static class AiAssistantFactory
    {
        /// <summary>Có biến môi trường ANTHROPIC_API_KEY thì dùng Claude, không thì dùng gợi ý mẫu.</summary>
        public static IAiCoachAssistant Create()
        {
            var key = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
            return string.IsNullOrWhiteSpace(key) ? new TemplateAiAssistant() : new ClaudeAiAssistant(new TemplateAiAssistant());
        }
    }
}
