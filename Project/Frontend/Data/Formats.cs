namespace Project.Frontend.Data
{
    public static class Formats
    {
        public static string Money(decimal value) => value.ToString("N0") + " ₫";

        /// <summary>312.500.000 -> "312,5 tr ₫"; 1.250.000.000 -> "1,25 tỷ ₫".</summary>
        public static string MoneyShort(decimal value) =>
            value >= 1_000_000_000 ? (value / 1_000_000_000m).ToString("0.##") + " tỷ ₫"
            : value >= 1_000_000 ? (value / 1_000_000m).ToString("0.#") + " tr ₫"
            : Money(value);

        public static string DayOfWeekVi(DayOfWeek d) => d switch
        {
            DayOfWeek.Monday => "Thứ 2",
            DayOfWeek.Tuesday => "Thứ 3",
            DayOfWeek.Wednesday => "Thứ 4",
            DayOfWeek.Thursday => "Thứ 5",
            DayOfWeek.Friday => "Thứ 6",
            DayOfWeek.Saturday => "Thứ 7",
            _ => "Chủ nhật"
        };

        public static string Time(TimeSpan t) => t.ToString(@"hh\:mm");
        public static string Time(DateTime t) => t.ToString("HH:mm");
    }

    public static class Text
    {
        /// <summary>Bỏ dấu + chữ thường: "Nguyễn Đức" -> "nguyen duc" (tìm kiếm không phân biệt dấu).</summary>
        public static string Fold(string? s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var normalized = s.Replace('đ', 'd').Replace('Đ', 'D').Normalize(System.Text.NormalizationForm.FormD);
            var sb = new System.Text.StringBuilder(normalized.Length);
            foreach (var ch in normalized)
                if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) != System.Globalization.UnicodeCategory.NonSpacingMark)
                    sb.Append(char.ToLowerInvariant(ch));
            return sb.ToString();
        }
    }
}
