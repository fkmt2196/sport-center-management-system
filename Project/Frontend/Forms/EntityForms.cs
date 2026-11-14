using SportsCenter.BLL;
using System.Text.RegularExpressions;
using Project.Frontend.Controls;
using Project.Frontend.Data;
using Project.Frontend.Shell;
using SportsCenter.DAL.Models;
using SportsCenter.BLL.Services;

namespace Project.Frontend.Forms
{
    /// <summary>Quy tắc kiểm tra dữ liệu nhập và danh sách lựa chọn dùng chung.</summary>
    internal static class Rules
    {
        public static string? Email(FormDialog _, object? v) =>
            v is string s && !Regex.IsMatch(s, @"^[^@\s]+@[^@\s]+\.[^@\s]+$") ? "Email không hợp lệ (vd. ten@gmail.com)." : null;

        public static string? Phone(FormDialog _, object? v) =>
            v is string s && !Regex.IsMatch(s, @"^0\d{9}$") ? "Số điện thoại gồm 10 chữ số và bắt đầu bằng 0." : null;

        public static string? Username(FormDialog _, object? v) =>
            v is string s && !Regex.IsMatch(s, @"^[a-zA-Z0-9._]{3,50}$") ? "3–50 ký tự: chữ không dấu, số, dấu chấm hoặc gạch dưới." : null;

        public static string? Password(FormDialog _, object? v) =>
            v is string s && s.Length < 6 ? "Mật khẩu phải có ít nhất 6 ký tự." : null;

        public static Func<FormDialog, object?, string?> SameAs(string key) =>
            (f, v) => v is string s && s != f.Text(key) ? "Mật khẩu nhập lại không khớp." : null;

        public static Func<FormDialog, object?, string?> Range(int min, int max) =>
            (_, v) => v is int n && (n < min || n > max) ? $"Giá trị từ {min} đến {max}." : null;

        public static IEnumerable<(string, object)> Enum<T>(string kind) where T : struct, System.Enum =>
            System.Enum.GetValues<T>().Select(v => (StatusBadge.Describe(kind, v.ToString()).Label, (object)v));

        public static IEnumerable<(string, object)> Genders() =>
            System.Enum.GetValues<Gender>().Select(g => (Labels.Gender(g), (object)g));

        public static IEnumerable<(string, object)> Methods(bool includeOnline = false) =>
            System.Enum.GetValues<PaymentMethod>().Where(m => includeOnline || m != PaymentMethod.Online)
                .Select(m => (PaymentService.MethodName(m), (object)m));

        public static readonly DayOfWeek[] Week =
            { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday };

        public static async Task<List<(string, object)>> MemberOptions() =>
            (await ServiceScope.Run(s => s.Members.ListAsync()))
                .Select(m => ($"{m.User!.FullName} ({m.User.Username} · {m.User.Phone})", (object)m.Id)).ToList();

        public static async Task<List<(string, object)>> CoachOptions(bool withNone)
        {
            var list = (await ServiceScope.Run(s => s.Coaches.ListAsync()))
                .Where(c => c.User!.IsActive)
                .Select(c => ($"{c.User!.FullName} · {c.Specialty}{(c.IsAvailable ? "" : " (tạm ngưng)")}", (object)c.Id)).ToList();
            if (withNone) list.Insert(0, ("(Chưa phân công)", 0));
            return list;
        }

        /// <summary>Lỗi nghiệp vụ trong form: ném ValidationException để FormDialog hiển thị đúng chỗ.</summary>
        public static int Id(object? v) => v is int i ? i : 0;
    }

    // =====================================================================
    // TÀI KHOẢN
    // =====================================================================
    public static class AccountForms
    {
        public static async Task<bool> EditAsync(User? existing)
        {
            var isNew = existing == null;
            var fields = new List<FormField>
            {
                new("FullName", "Họ và tên") { Required = true, MaxLength = 150, Value = existing?.FullName },
                new("Username", "Tên đăng nhập") { Required = isNew, ReadOnly = !isNew, MaxLength = 50, Value = existing?.Username, Validate = Rules.Username },
            };
            if (isNew) fields.Add(new("Password", "Mật khẩu", FieldKind.Password) { Required = true, Validate = Rules.Password, Hint = "Tối thiểu 6 ký tự." });
            fields.Add(new("Phone", "Số điện thoại") { MaxLength = 20, Value = existing?.Phone, Placeholder = "09xxxxxxxx", Validate = Rules.Phone });
            fields.Add(new("Email", "Email") { MaxLength = 150, Value = existing?.Email, Validate = Rules.Email });
            fields.Add(new("Role", "Vai trò (phân quyền)", FieldKind.Combo) { Required = true, Value = existing?.Role ?? UserRole.Receptionist, Options = Rules.Enum<UserRole>("role"),
                Hint = "Mỗi vai trò chỉ thấy các chức năng của mình." });
            if (!isNew) fields.Add(new("IsActive", "Tài khoản đang hoạt động", FieldKind.Check) { Value = existing!.IsActive });

            var dialog = new FormDialog(isNew ? "Tạo tài khoản" : "Sửa tài khoản & phân quyền", isNew ? "Tạo tài khoản cho nhân viên, HLV hoặc thành viên." : existing!.Username,
                Icons.Contact, fields, async f =>
                {
                    await ServiceScope.Run(async s =>
                    {
                        if (isNew) await s.Accounts.CreateAsync(f.Text("Username"), f.Text("Password"), f.Text("FullName"), f.Text("Phone"), f.Text("Email"), (UserRole)f.Get("Role")!);
                        else await s.Accounts.UpdateAsync(existing!.Id, f.Text("FullName"), f.Text("Phone"), f.Text("Email"), (UserRole)f.Get("Role")!, f.Get("IsActive") is true);
                    });
                    return true;
                }, isNew ? "Tạo tài khoản" : "Lưu thay đổi");
            if (!await UiHost.ShowDialogAsync(dialog)) return false;
            UiHost.Toast(isNew ? $"Đã tạo tài khoản \"{dialog.Text("Username")}\"." : "Đã cập nhật tài khoản.");
            return true;
        }

        public static async Task<bool> ResetPasswordAsync(User user)
        {
            var dialog = new FormDialog("Đặt lại mật khẩu", user.FullName, Icons.Contact, new[]
            {
                new FormField("Password", "Mật khẩu mới", FieldKind.Password) { Required = true, Validate = Rules.Password },
                new FormField("Confirm", "Nhập lại mật khẩu", FieldKind.Password) { Required = true, Validate = Rules.SameAs("Password") },
            }, async f => { await ServiceScope.Run(s => s.Accounts.ResetPasswordAsync(user.Id, f.Text("Password"))); return true; }, "Đặt lại");
            if (!await UiHost.ShowDialogAsync(dialog)) return false;
            UiHost.Toast($"Đã đặt lại mật khẩu cho {user.Username}.");
            return true;
        }

        public static async Task<bool> ChangeOwnPasswordAsync(int userId)
        {
            var dialog = new FormDialog("Đổi mật khẩu", "Mật khẩu mới tối thiểu 6 ký tự.", Icons.Contact, new[]
            {
                new FormField("Current", "Mật khẩu hiện tại", FieldKind.Password) { Required = true, Wide = true },
                new FormField("Password", "Mật khẩu mới", FieldKind.Password) { Required = true, Validate = Rules.Password },
                new FormField("Confirm", "Nhập lại mật khẩu mới", FieldKind.Password) { Required = true, Validate = Rules.SameAs("Password") },
            }, async f => { await ServiceScope.Run(s => s.Auth.ChangePasswordAsync(userId, f.Text("Current"), f.Text("Password"))); return true; }, "Đổi mật khẩu");
            if (!await UiHost.ShowDialogAsync(dialog)) return false;
            UiHost.Toast("Đã đổi mật khẩu.");
            return true;
        }
    }

    // =====================================================================
    // THÀNH VIÊN & HLV
    // =====================================================================
    public static class PeopleForms
    {
        /// <summary>Thêm (tại quầy) hoặc sửa hồ sơ thành viên. selfService: thành viên tự cập nhật.</summary>
        public static async Task<bool> MemberAsync(Member? existing, bool selfService = false)
        {
            var isNew = existing == null;
            var u = existing?.User;
            var fields = new List<FormField>
            {
                new("FullName", "Họ và tên") { Required = true, MaxLength = 150, Value = u?.FullName, Placeholder = "vd. Nguyễn Văn An" },
            };
            if (isNew)
            {
                fields.Add(new("Username", "Tên đăng nhập") { Required = true, MaxLength = 50, Validate = Rules.Username, Placeholder = "vd. hv061" });
                fields.Add(new("Password", "Mật khẩu", FieldKind.Password) { Required = true, Validate = Rules.Password, Hint = "Thành viên dùng để đăng nhập ứng dụng." });
            }
            else fields.Add(new("Username", "Tên đăng nhập") { ReadOnly = true, Value = u!.Username });
            fields.AddRange(new[]
            {
                new FormField("Phone", "Số điện thoại") { Required = true, MaxLength = 20, Value = u?.Phone, Placeholder = "09xxxxxxxx", Validate = Rules.Phone },
                new FormField("Email", "Email") { MaxLength = 150, Value = u?.Email, Validate = Rules.Email },
                new FormField("Gender", "Giới tính", FieldKind.Combo) { Required = true, Value = existing?.Gender ?? Gender.Male, Options = Rules.Genders() },
                new FormField("Dob", "Ngày sinh", FieldKind.Date) { Value = existing?.DateOfBirth,
                    Validate = (_, v) => v is DateTime d && (d > DateTime.Today.AddYears(-6) || d < DateTime.Today.AddYears(-100)) ? "Ngày sinh không hợp lệ." : null },
                new FormField("Height", "Chiều cao (cm)", FieldKind.Decimal) { Value = existing?.HeightCm, Validate = (_, v) => v is double h && (h < 80 || h > 250) ? "Chiều cao từ 80 đến 250 cm." : null },
                new FormField("Weight", "Cân nặng (kg)", FieldKind.Decimal) { Value = existing?.WeightKg, Validate = (_, v) => v is double w && (w < 20 || w > 300) ? "Cân nặng từ 20 đến 300 kg." : null },
                new FormField("Level", "Trình độ", FieldKind.Combo) { Required = true, Value = existing?.FitnessLevel ?? FitnessLevel.Beginner, Options = Rules.Enum<FitnessLevel>("level") },
                new FormField("Goals", "Mục tiêu tập luyện", FieldKind.Multiline) { Wide = true, MaxLength = 500, Value = existing?.Goals, Placeholder = "vd. Giảm 5 kg trong 3 tháng, tăng sức bền" },
                new FormField("Health", "Lưu ý sức khỏe / chấn thương", FieldKind.Multiline) { Wide = true, MaxLength = 500, Value = existing?.HealthNotes, Placeholder = "Không bắt buộc" },
            });

            var dialog = new FormDialog(
                isNew ? "Đăng ký thành viên mới" : selfService ? "Cập nhật hồ sơ của tôi" : "Sửa hồ sơ thành viên",
                isNew ? "Tạo tài khoản và hồ sơ thành viên tại quầy." : u!.FullName,
                Icons.People, fields, async f =>
                {
                    var input = new MemberInput(f.Text("FullName"), f.Text("Phone"), f.Text("Email"), (Gender)f.Get("Gender")!, f.Value<DateTime>("Dob"),
                        f.Value<double>("Height"), f.Value<double>("Weight"), (FitnessLevel)f.Get("Level")!, f.Text("Goals"), f.Text("Health"));
                    await ServiceScope.Run(async s =>
                    {
                        if (isNew) await s.Members.RegisterAsync(f.Text("Username"), f.Text("Password"), input);
                        else await s.Members.UpdateAsync(existing!.Id, input);
                    });
                    return true;
                }, isNew ? "Đăng ký" : "Lưu thay đổi");
            if (!await UiHost.ShowDialogAsync(dialog)) return false;
            UiHost.Toast(isNew ? $"Đã đăng ký thành viên \"{dialog.Text("FullName")}\". Hãy đăng ký gói để bắt đầu tập." : "Đã cập nhật hồ sơ.");
            return true;
        }

        public static async Task<bool> CoachAsync(Coach? existing, bool selfService = false)
        {
            var isNew = existing == null;
            var u = existing?.User;
            var fields = new List<FormField> { new("FullName", "Họ và tên") { Required = true, MaxLength = 150, Value = u?.FullName } };
            if (isNew)
            {
                fields.Add(new("Username", "Tên đăng nhập") { Required = true, MaxLength = 50, Validate = Rules.Username, Placeholder = "vd. hlv09" });
                fields.Add(new("Password", "Mật khẩu", FieldKind.Password) { Required = true, Validate = Rules.Password });
            }
            fields.AddRange(new[]
            {
                new FormField("Phone", "Số điện thoại") { MaxLength = 20, Value = u?.Phone, Validate = Rules.Phone },
                new FormField("Email", "Email") { MaxLength = 150, Value = u?.Email, Validate = Rules.Email },
                new FormField("Specialty", "Chuyên môn") { Required = true, MaxLength = 150, Value = existing?.Specialty, Placeholder = "vd. Yoga, Gym & Thể hình" },
                new FormField("Years", "Số năm kinh nghiệm", FieldKind.Number) { Value = existing?.YearsExperience ?? 0, Validate = Rules.Range(0, 60) },
                new FormField("Certs", "Bằng cấp, chứng chỉ", FieldKind.Multiline) { Wide = true, MaxLength = 500, Value = existing?.Certifications },
                new FormField("Bio", "Giới thiệu", FieldKind.Multiline) { Wide = true, MaxLength = 1000, Value = existing?.Bio },
                new FormField("Available", "Đang nhận lớp mới", FieldKind.Check) { Value = existing?.IsAvailable ?? true },
            });
            var dialog = new FormDialog(isNew ? "Thêm huấn luyện viên" : selfService ? "Cập nhật hồ sơ của tôi" : "Sửa hồ sơ huấn luyện viên",
                isNew ? "Tạo tài khoản và hồ sơ HLV." : u!.FullName, Icons.Education, fields, async f =>
                {
                    var input = new CoachInput(f.Text("FullName"), f.Text("Phone"), f.Text("Email"), f.Text("Specialty"), f.Text("Certs"), f.Text("Bio"),
                        f.Value<int>("Years") ?? 0, f.Get("Available") is true);
                    await ServiceScope.Run(async s =>
                    {
                        if (isNew) await s.Coaches.CreateAsync(f.Text("Username"), f.Text("Password"), input);
                        else await s.Coaches.UpdateAsync(existing!.Id, input);
                    });
                    return true;
                }, isNew ? "Thêm HLV" : "Lưu thay đổi");
            if (!await UiHost.ShowDialogAsync(dialog)) return false;
            UiHost.Toast(isNew ? $"Đã thêm huấn luyện viên \"{dialog.Text("FullName")}\"." : "Đã cập nhật hồ sơ HLV.");
            return true;
        }
    }

    // =====================================================================
    // BỘ MÔN, PHÒNG, GÓI
    // =====================================================================
    public static class CatalogForms
    {
        public static async Task<bool> SportAsync(Sport? existing)
        {
            var dialog = new FormDialog(existing == null ? "Thêm bộ môn" : "Sửa bộ môn", "Bộ môn dùng để phân loại lớp học.", Icons.Library, new[]
            {
                new FormField("Name", "Tên bộ môn") { Required = true, MaxLength = 100, Value = existing?.Name, Placeholder = "vd. Kickboxing" },
                new FormField("Active", "Đang hoạt động", FieldKind.Check) { Value = existing?.IsActive ?? true },
                new FormField("Desc", "Mô tả", FieldKind.Multiline) { Wide = true, MaxLength = 500, Value = existing?.Description },
            }, async f =>
            {
                await ServiceScope.Run(s => s.Facilities.SaveSportAsync(new Sport { Id = existing?.Id ?? 0, Name = f.Text("Name"), Description = f.Text("Desc"), IsActive = f.Get("Active") is true }));
                return true;
            });
            if (!await UiHost.ShowDialogAsync(dialog)) return false;
            UiHost.Toast($"Đã lưu bộ môn \"{dialog.Text("Name")}\".");
            return true;
        }

        public static async Task<bool> RoomAsync(Room? existing)
        {
            var dialog = new FormDialog(existing == null ? "Thêm phòng tập" : "Sửa phòng tập", "Phòng/khu vực dùng để xếp ca học.", Icons.Location, new[]
            {
                new FormField("Name", "Tên phòng") { Required = true, MaxLength = 100, Value = existing?.Name },
                new FormField("Capacity", "Sức chứa (người)", FieldKind.Number) { Required = true, Value = existing?.Capacity, Validate = Rules.Range(1, 500) },
                new FormField("Location", "Vị trí") { MaxLength = 200, Value = existing?.Location, Placeholder = "vd. Tầng 2" },
                new FormField("Active", "Đang sử dụng", FieldKind.Check) { Value = existing?.IsActive ?? true },
                new FormField("Equipment", "Trang thiết bị", FieldKind.Multiline) { Wide = true, MaxLength = 500, Value = existing?.Equipment },
            }, async f =>
            {
                await ServiceScope.Run(s => s.Facilities.SaveRoomAsync(new Room
                {
                    Id = existing?.Id ?? 0, Name = f.Text("Name"), Capacity = f.Value<int>("Capacity") ?? 0, Location = f.Text("Location"),
                    Equipment = f.Text("Equipment"), IsActive = f.Get("Active") is true
                }));
                return true;
            });
            if (!await UiHost.ShowDialogAsync(dialog)) return false;
            UiHost.Toast($"Đã lưu phòng \"{dialog.Text("Name")}\".");
            return true;
        }

        public static async Task<bool> PlanAsync(MembershipPlan? existing)
        {
            var dialog = new FormDialog(existing == null ? "Thêm gói thành viên" : "Sửa gói thành viên", "Giá, thời hạn và quyền lợi của gói.", Icons.Money, new[]
            {
                new FormField("Name", "Tên gói") { Required = true, Wide = true, MaxLength = 100, Value = existing?.Name, Placeholder = "vd. Gói 3 tháng" },
                new FormField("Price", "Giá (VNĐ)", FieldKind.Money) { Required = true, Value = existing?.Price,
                    Validate = (_, v) => v is decimal d && d > 100_000_000 ? "Giá không vượt quá 100 triệu." : null },
                new FormField("Days", "Thời hạn (ngày)", FieldKind.Number) { Required = true, Value = existing?.DurationDays, Validate = Rules.Range(1, 1095) },
                new FormField("Classes", "Gồm các lớp nhóm (học phí 0 ₫)", FieldKind.Check) { Value = existing?.IncludesGroupClasses ?? true },
                new FormField("Active", "Đang bán", FieldKind.Check) { Value = existing?.IsActive ?? true },
                new FormField("Desc", "Mô tả quyền lợi", FieldKind.Multiline) { Wide = true, MaxLength = 500, Value = existing?.Description },
            }, async f =>
            {
                await ServiceScope.Run(s => s.Memberships.SavePlanAsync(new MembershipPlan
                {
                    Id = existing?.Id ?? 0, Name = f.Text("Name"), Price = f.Value<decimal>("Price") ?? 0, DurationDays = f.Value<int>("Days") ?? 0,
                    IncludesGroupClasses = f.Get("Classes") is true, IsActive = f.Get("Active") is true, Description = f.Text("Desc")
                }));
                return true;
            });
            if (!await UiHost.ShowDialogAsync(dialog)) return false;
            UiHost.Toast($"Đã lưu gói \"{dialog.Text("Name")}\".");
            return true;
        }
    }

    // =====================================================================
    // LỚP & CA HỌC
    // =====================================================================
    public static class ClassForms
    {
        public static async Task<bool> ClassAsync(TrainingClass? existing, bool assignOnly = false)
        {
            var sports = (await ServiceScope.Run(s => s.Facilities.SportsAsync())).Where(s => s.IsActive || s.Id == existing?.SportId).Select(s => (s.Name, (object)s.Id)).ToList();
            var coaches = await Rules.CoachOptions(withNone: true);
            var isNew = existing == null;
            var fields = new List<FormField>();
            if (!assignOnly)
                fields.AddRange(new[]
                {
                    new FormField("Name", "Tên lớp") { Required = true, Wide = true, MaxLength = 150, Value = existing?.Name, Placeholder = "vd. Yoga cơ bản tối 2-4-6" },
                    new FormField("Sport", "Bộ môn", FieldKind.Combo) { Required = true, Options = sports, Value = existing?.SportId },
                });
            fields.Add(new("Coach", "Huấn luyện viên phụ trách", FieldKind.Combo) { Required = true, Options = coaches, Value = existing?.CoachId ?? 0,
                Hint = "HLV được phân công sẽ nhận thông báo.", Wide = assignOnly });
            if (!assignOnly)
                fields.AddRange(new[]
                {
                    new FormField("Level", "Trình độ", FieldKind.Combo) { Required = true, Value = existing?.Level ?? FitnessLevel.Beginner, Options = Rules.Enum<FitnessLevel>("level") },
                    new FormField("Capacity", "Sĩ số tối đa", FieldKind.Number) { Required = true, Value = existing?.Capacity, Validate = Rules.Range(1, 200) },
                    new FormField("Fee", "Học phí riêng (VNĐ)", FieldKind.Money) { Value = existing?.Fee ?? 0, Hint = "Để 0 nếu lớp đã gồm trong gói thành viên." },
                    new FormField("Start", "Ngày bắt đầu", FieldKind.Date) { Required = true, Value = existing?.StartDate ?? DateTime.Today },
                    new FormField("End", "Ngày kết thúc", FieldKind.Date) { Value = existing?.EndDate,
                        Validate = (f, v) => v is DateTime e && f.Value<DateTime>("Start") is DateTime s && e < s ? "Ngày kết thúc phải sau ngày bắt đầu." : null },
                    new FormField("Status", "Trạng thái", FieldKind.Combo) { Required = true, Value = existing?.Status ?? ClassStatus.Open, Options = Rules.Enum<ClassStatus>("class") },
                    new FormField("Desc", "Mô tả", FieldKind.Multiline) { Wide = true, MaxLength = 500, Value = existing?.Description },
                });

            var dialog = new FormDialog(assignOnly ? "Phân công huấn luyện viên" : isNew ? "Thêm lớp học" : "Sửa lớp học", existing?.Name ?? "Lớp tập theo nhóm thuộc một bộ môn.",
                Icons.Library, fields, async f =>
                {
                    var coachId = Rules.Id(f.Get("Coach"));
                    var cls = assignOnly ? existing! : new TrainingClass
                    {
                        Id = existing?.Id ?? 0, Name = f.Text("Name"), SportId = Rules.Id(f.Get("Sport")), Level = (FitnessLevel)f.Get("Level")!,
                        Capacity = f.Value<int>("Capacity") ?? 0, Fee = f.Value<decimal>("Fee") ?? 0, StartDate = f.Value<DateTime>("Start")!.Value,
                        EndDate = f.Value<DateTime>("End"), Status = (ClassStatus)f.Get("Status")!, Description = f.Text("Desc")
                    };
                    cls.CoachId = coachId == 0 ? null : coachId;
                    await ServiceScope.Run(s => s.Classes.SaveAsync(cls));
                    return true;
                }, assignOnly ? "Phân công" : isNew ? "Thêm lớp" : "Lưu thay đổi");
            if (!await UiHost.ShowDialogAsync(dialog)) return false;
            UiHost.Toast(assignOnly ? "Đã phân công huấn luyện viên." : $"Đã lưu lớp \"{dialog.Text("Name")}\".");
            return true;
        }

        public static async Task<bool> GenerateSessionsAsync(TrainingClass cls)
        {
            var rooms = (await ServiceScope.Run(s => s.Facilities.RoomsAsync())).Where(r => r.IsActive).Select(r => ($"{r.Name} ({r.Capacity} người)", (object)r.Id)).ToList();
            var from = cls.StartDate > DateTime.Today ? cls.StartDate : DateTime.Today;
            var dialog = new FormDialog("Xếp lịch theo tuần", cls.Name, Icons.Calendar, new[]
            {
                new FormField("Room", "Phòng tập", FieldKind.Combo) { Required = true, Wide = true, Options = rooms },
                new FormField("Days", "Các ngày trong tuần", FieldKind.MultiCheck) { Required = true, Wide = true, Options = Rules.Week.Select(d => (Formats.DayOfWeekVi(d), (object)d)) },
                new FormField("Start", "Giờ bắt đầu", FieldKind.Time) { Required = true },
                new FormField("End", "Giờ kết thúc", FieldKind.Time) { Required = true,
                    Validate = (f, v) => v is TimeSpan e && f.Value<TimeSpan>("Start") is TimeSpan s && e <= s ? "Giờ kết thúc phải sau giờ bắt đầu." : null },
                new FormField("From", "Từ ngày", FieldKind.Date) { Required = true, Value = from },
                new FormField("To", "Đến ngày", FieldKind.Date) { Required = true, Value = cls.EndDate ?? from.AddDays(27),
                    Validate = (f, v) => v is DateTime t && f.Value<DateTime>("From") is DateTime fr && (t < fr || (t - fr).TotalDays > 370) ? "Khoảng thời gian không hợp lệ (tối đa 1 năm)." : null },
            }, async f =>
            {
                var days = ((List<object>)f.Get("Days")!).Cast<DayOfWeek>().ToList();
                var (created, skipped) = await ServiceScope.Run(s => s.Classes.GenerateSessionsAsync(cls.Id, Rules.Id(f.Get("Room")), days,
                    f.Value<TimeSpan>("Start")!.Value, f.Value<TimeSpan>("End")!.Value, f.Value<DateTime>("From")!.Value, f.Value<DateTime>("To")!.Value));
                UiHost.Toast(skipped == 0 ? $"Đã tạo {created} ca học." : $"Đã tạo {created} ca học, bỏ qua {skipped} ca trùng phòng.", skipped == 0 ? ToastKind.Success : ToastKind.Warning);
                return true;
            }, "Tạo lịch");
            return await UiHost.ShowDialogAsync(dialog);
        }

        public static async Task<bool> SessionAsync(SessionRow? existing)
        {
            var rooms = (await ServiceScope.Run(s => s.Facilities.RoomsAsync())).Where(r => r.IsActive || r.Id == existing?.Entity.RoomId).Select(r => (r.Name, (object)r.Id)).ToList();
            var classes = existing == null
                ? (await ServiceScope.Run(s => s.Classes.ListAsync())).Where(c => c.Class.Status == ClassStatus.Open).Select(c => (c.Class.Name, (object)c.Class.Id)).ToList()
                : new List<(string, object)>();
            var e = existing?.Entity;
            var fields = new List<FormField>();
            if (existing == null) fields.Add(new("Class", "Lớp", FieldKind.Combo) { Required = true, Wide = true, Options = classes });
            fields.AddRange(new[]
            {
                new FormField("Room", "Phòng tập", FieldKind.Combo) { Required = true, Options = rooms, Value = e?.RoomId },
                new FormField("Date", "Ngày", FieldKind.Date) { Required = true, Value = e?.StartTime.Date ?? DateTime.Today.AddDays(1) },
                new FormField("Start", "Giờ bắt đầu", FieldKind.Time) { Required = true, Value = e?.StartTime.TimeOfDay },
                new FormField("End", "Giờ kết thúc", FieldKind.Time) { Required = true, Value = e?.EndTime.TimeOfDay,
                    Validate = (f, v) => v is TimeSpan en && f.Value<TimeSpan>("Start") is TimeSpan st && en <= st ? "Giờ kết thúc phải sau giờ bắt đầu." : null },
                new FormField("Note", "Ghi chú", FieldKind.Multiline) { Wide = true, MaxLength = 300, Value = e?.Note },
            });
            var dialog = new FormDialog(existing == null ? "Thêm ca học" : "Đổi lịch ca học", existing?.ClassName ?? "Một buổi tập cụ thể của lớp.", Icons.Calendar, fields, async f =>
            {
                var date = f.Value<DateTime>("Date")!.Value.Date;
                await ServiceScope.Run(s => s.Classes.SaveSessionAsync(new ClassSession
                {
                    Id = e?.Id ?? 0, ClassId = e?.ClassId ?? Rules.Id(f.Get("Class")), RoomId = Rules.Id(f.Get("Room")),
                    StartTime = date + f.Value<TimeSpan>("Start")!.Value, EndTime = date + f.Value<TimeSpan>("End")!.Value,
                    Note = f.Text("Note"), Status = e?.Status ?? SessionStatus.Scheduled
                }));
                return true;
            }, existing == null ? "Thêm ca" : "Lưu & thông báo");
            if (!await UiHost.ShowDialogAsync(dialog)) return false;
            UiHost.Toast(existing == null ? "Đã thêm ca học." : "Đã cập nhật lịch. Thành viên của lớp đã được thông báo nếu lịch thay đổi.");
            return true;
        }

        public static async Task<bool> CancelSessionAsync(SessionRow row)
        {
            var dialog = new FormDialog("Hủy ca học", $"{row.ClassName} · {row.DateText} {row.TimeText}", Icons.Warning, new[]
            {
                new FormField("Reason", "Lý do hủy", FieldKind.Multiline) { Required = true, Wide = true, MaxLength = 300, Placeholder = "vd. HLV ốm, bảo trì thiết bị",
                    Hint = "Lý do sẽ được gửi kèm thông báo tới thành viên của lớp." },
            }, async f => { await ServiceScope.Run(s => s.Classes.CancelSessionAsync(row.Id, f.Text("Reason"))); return true; }, "Hủy ca & thông báo");
            if (!await UiHost.ShowDialogAsync(dialog)) return false;
            UiHost.Toast("Đã hủy ca học và gửi thông báo cho thành viên.");
            return true;
        }
    }

    // =====================================================================
    // GÓI, ĐĂNG KÝ LỚP, THANH TOÁN, HỖ TRỢ
    // =====================================================================
    public static class SalesForms
    {
        /// <summary>Đăng ký/gia hạn gói. memberId null: chọn thành viên. selfService: thành viên tự mua (online).</summary>
        public static async Task<Payment?> SubscribeAsync(int? memberId, bool selfService = false, int? planId = null)
        {
            var plans = (await ServiceScope.Run(s => s.Memberships.PlansAsync(activeOnly: true)))
                .Select(p => ($"{p.Name} – {p.Price:N0} ₫ / {p.DurationDays} ngày", (object)p.Id)).ToList();
            var members = memberId == null ? await Rules.MemberOptions() : null;
            string? hint = null;
            if (memberId is int mid)
            {
                var m = await ServiceScope.Run(s => s.Members.GetAsync(mid));
                var end = m == null ? null : MemberService.MembershipEnd(m);
                hint = end.HasValue && end >= DateTime.Today ? $"Gói hiện tại còn hạn đến {end:dd/MM/yyyy}: gói mới sẽ nối tiếp từ {end.Value.AddDays(1):dd/MM/yyyy}." : "Gói mới bắt đầu từ hôm nay.";
            }
            var fields = new List<FormField>();
            if (members != null) fields.Add(new("Member", "Thành viên", FieldKind.Combo) { Required = true, Wide = true, Options = members });
            fields.Add(new("Plan", "Gói thành viên", FieldKind.Combo) { Required = true, Wide = true, Options = plans, Value = planId, Hint = hint });
            if (!selfService)
            {
                fields.Add(new("Method", "Hình thức thanh toán", FieldKind.Combo) { Required = true, Options = Rules.Methods(), Value = PaymentMethod.Cash });
                fields.Add(new("Note", "Ghi chú") { MaxLength = 300 });
            }
            Payment? payment = null;
            var dialog = new FormDialog(selfService ? "Đăng ký / gia hạn gói" : "Đăng ký / gia hạn gói cho thành viên",
                selfService ? "Thanh toán online – hóa đơn được lưu trong lịch sử của bạn." : "Hệ thống tự tạo hóa đơn sau khi ghi nhận.", Icons.Money, fields, async f =>
                {
                    var id = memberId ?? Rules.Id(f.Get("Member"));
                    var method = selfService ? PaymentMethod.Online : (PaymentMethod)f.Get("Method")!;
                    (_, payment) = await ServiceScope.Run(s => s.Memberships.SubscribeAsync(id, Rules.Id(f.Get("Plan")), method, selfService ? "" : f.Text("Note")));
                    return true;
                }, selfService ? "Thanh toán" : "Ghi nhận & lập hóa đơn");
            if (!await UiHost.ShowDialogAsync(dialog)) return null;
            UiHost.Toast($"Đăng ký gói thành công. Hóa đơn {payment!.InvoiceNo} – {payment.Amount:N0} ₫.");
            return payment;
        }

        /// <summary>Đăng ký lớp (tự đăng ký hoặc đăng ký hộ). Lớp có học phí cần chọn hình thức thanh toán.</summary>
        public static async Task<bool> EnrollAsync(int? memberId, int? classId, bool selfService = false)
        {
            var classes = (await ServiceScope.Run(s => s.Classes.ListAsync()))
                .Where(c => c.Class.Status == ClassStatus.Open && (c.Class.EndDate == null || c.Class.EndDate >= DateTime.Today))
                .OrderBy(c => c.Class.Name)
                .Select(c => ($"{c.Class.Name} · {(c.Class.Fee == 0 ? "gồm trong gói" : $"{c.Class.Fee:N0} ₫")} · còn {Math.Max(0, c.Class.Capacity - c.Enrolled)} chỗ", (object)c.Class.Id)).ToList();
            var fields = new List<FormField>();
            if (memberId == null) fields.Add(new("Member", "Thành viên", FieldKind.Combo) { Required = true, Wide = true, Options = await Rules.MemberOptions() });
            fields.Add(new("Class", "Lớp học", FieldKind.Combo) { Required = true, Wide = true, Options = classes, Value = classId });
            fields.Add(new("Method", "Thanh toán học phí (nếu có)", FieldKind.Combo)
            {
                Options = selfService ? Rules.Methods(true).Where(m => (PaymentMethod)m.Item2 == PaymentMethod.Online) : Rules.Methods(),
                Value = selfService ? PaymentMethod.Online : PaymentMethod.Cash, Wide = true,
                Hint = "Chỉ áp dụng cho lớp có học phí riêng; lớp gồm trong gói không thu thêm."
            });
            var dialog = new FormDialog(selfService ? "Đăng ký lớp" : "Đăng ký lớp cho thành viên", "Cần có gói thành viên còn hạn.", Icons.Library, fields, async f =>
            {
                var id = memberId ?? Rules.Id(f.Get("Member"));
                await ServiceScope.Run(s => s.Classes.EnrollAsync(id, Rules.Id(f.Get("Class")), (PaymentMethod?)f.Get("Method")));
                return true;
            }, "Đăng ký");
            if (!await UiHost.ShowDialogAsync(dialog)) return false;
            UiHost.Toast("Đăng ký lớp thành công.");
            return true;
        }

        public static async Task<Payment?> PaymentAsync(int? memberId)
        {
            var fields = new List<FormField>();
            if (memberId == null) fields.Add(new("Member", "Thành viên", FieldKind.Combo) { Required = true, Wide = true, Options = await Rules.MemberOptions() });
            fields.Add(new("Amount", "Số tiền (VNĐ)", FieldKind.Money) { Required = true,
                Validate = (_, v) => v is decimal d && (d <= 0 || d > 100_000_000) ? "Số tiền từ 1 đến 100 triệu." : null });
            fields.Add(new("Method", "Hình thức", FieldKind.Combo) { Required = true, Options = Rules.Methods(), Value = PaymentMethod.Cash });
            fields.Add(new("Note", "Nội dung thu", FieldKind.Multiline) { Required = true, Wide = true, MaxLength = 300, Placeholder = "vd. Thuê PT 4 buổi, phí làm lại thẻ" });
            Payment? payment = null;
            var dialog = new FormDialog("Ghi nhận thanh toán", "Khoản thu khác ngoài gói và học phí lớp.", Icons.Money, fields, async f =>
            {
                payment = await ServiceScope.Run(s => s.Payments.RecordAsync(memberId ?? Rules.Id(f.Get("Member")), f.Value<decimal>("Amount")!.Value, (PaymentMethod)f.Get("Method")!, f.Text("Note")));
                return true;
            }, "Ghi nhận");
            if (!await UiHost.ShowDialogAsync(dialog)) return null;
            UiHost.Toast($"Đã ghi nhận hóa đơn {payment!.InvoiceNo}.");
            return payment;
        }

        public static async Task<bool> SupportAsync(SupportRequest? existing)
        {
            var members = await Rules.MemberOptions();
            members.Insert(0, ("(Khách vãng lai / không xác định)", 0));
            var dialog = new FormDialog(existing == null ? "Ghi nhận yêu cầu hỗ trợ" : "Xử lý yêu cầu hỗ trợ", existing?.Subject ?? "Yêu cầu từ thành viên tại quầy.", Icons.Info, new[]
            {
                new FormField("Member", "Thành viên", FieldKind.Combo) { Wide = true, Options = members, Value = existing?.MemberId ?? 0 },
                new FormField("Subject", "Tiêu đề", FieldKind.Text) { Required = true, Wide = true, MaxLength = 200, Value = existing?.Subject },
                new FormField("Desc", "Nội dung", FieldKind.Multiline) { Required = true, Wide = true, MaxLength = 2000, Value = existing?.Description },
                new FormField("Status", "Trạng thái", FieldKind.Combo) { Required = true, Value = existing?.Status ?? SupportStatus.Open, Options = Rules.Enum<SupportStatus>("support") },
                new FormField("Response", "Phản hồi / cách xử lý", FieldKind.Multiline) { Wide = true, MaxLength = 1000, Value = existing?.Response },
            }, async f =>
            {
                var memberIdValue = Rules.Id(f.Get("Member"));
                await ServiceScope.Run(s => s.Support.SaveAsync(new SupportRequest
                {
                    Id = existing?.Id ?? 0, MemberId = memberIdValue == 0 ? null : memberIdValue, Subject = f.Text("Subject"),
                    Description = f.Text("Desc"), Status = (SupportStatus)f.Get("Status")!, Response = f.Text("Response"),
                    CreatedByUserId = existing?.CreatedByUserId ?? Me.User.Id
                }));
                return true;
            });
            if (!await UiHost.ShowDialogAsync(dialog)) return false;
            UiHost.Toast("Đã lưu yêu cầu hỗ trợ.");
            return true;
        }
    }

    // =====================================================================
    // TẬP LUYỆN (HLV)
    // =====================================================================
    public static class TrainingForms
    {
        private static async Task<(List<(string, object)> Members, List<(string, object)> Classes)> CoachTargets(int coachId)
        {
            var members = (await ServiceScope.Run(s => s.Coaches.MyMembersAsync(coachId)))
                .Select(x => ($"{x.Member.User!.FullName} ({string.Join(", ", x.Classes)})", (object)x.Member.Id)).ToList();
            var classes = (await ServiceScope.Run(s => s.Classes.ListAsync(coachId))).Select(c => (c.Class.Name, (object)c.Class.Id)).ToList();
            return (members, classes);
        }

        public static async Task<bool> PlanAsync(int coachId, TrainingPlan? existing, int? memberId = null, string? content = null, string? goal = null)
        {
            var (members, classes) = await CoachTargets(coachId);
            members.Insert(0, ("(Không – kế hoạch cho cả lớp)", 0));
            classes.Insert(0, ("(Không – kế hoạch cá nhân)", 0));
            var dialog = new FormDialog(existing == null ? "Tạo kế hoạch tập" : "Sửa kế hoạch tập", "Áp dụng cho một học viên hoặc cả lớp.", Icons.Edit, new[]
            {
                new FormField("Member", "Học viên (cá nhân)", FieldKind.Combo) { Options = members, Value = existing?.MemberId ?? memberId ?? 0 },
                new FormField("Class", "Lớp (cả lớp)", FieldKind.Combo) { Options = classes, Value = existing?.ClassId ?? 0,
                    Validate = (f, v) => (Rules.Id(f.Get("Member")) == 0) == (Rules.Id(v) == 0) ? "Chọn đúng một: học viên HOẶC lớp." : null },
                new FormField("Title", "Tiêu đề", FieldKind.Text) { Required = true, Wide = true, MaxLength = 200, Value = existing?.Title, Placeholder = "vd. Giảm mỡ 8 tuần – giai đoạn 1" },
                new FormField("Goal", "Mục tiêu", FieldKind.Text) { Wide = true, MaxLength = 300, Value = existing?.Goal ?? goal },
                new FormField("Start", "Từ ngày", FieldKind.Date) { Required = true, Value = existing?.StartDate ?? DateTime.Today },
                new FormField("End", "Đến ngày", FieldKind.Date) { Value = existing?.EndDate ?? DateTime.Today.AddDays(28),
                    Validate = (f, v) => v is DateTime e && f.Value<DateTime>("Start") is DateTime s && e < s ? "Ngày kết thúc phải sau ngày bắt đầu." : null },
                new FormField("Content", "Nội dung (bài tập, hiệp × lần, nghỉ...)", FieldKind.Multiline) { Required = true, Wide = true, MaxLength = 4000, Value = existing?.Content ?? content,
                    Hint = "Mẹo: dùng Trợ lý AI để có gợi ý theo mục tiêu và trình độ học viên." },
            }, async f =>
            {
                var m = Rules.Id(f.Get("Member")); var c = Rules.Id(f.Get("Class"));
                await ServiceScope.Run(s => s.Training.SavePlanAsync(new TrainingPlan
                {
                    Id = existing?.Id ?? 0, CoachId = existing?.CoachId ?? coachId, MemberId = m == 0 ? null : m, ClassId = c == 0 ? null : c,
                    Title = f.Text("Title"), Goal = f.Text("Goal"), Content = f.Text("Content"), StartDate = f.Value<DateTime>("Start")!.Value, EndDate = f.Value<DateTime>("End")
                }));
                return true;
            }, "Lưu kế hoạch");
            if (!await UiHost.ShowDialogAsync(dialog)) return false;
            UiHost.Toast("Đã lưu kế hoạch tập.");
            return true;
        }

        public static async Task<bool> LogAsync(int coachId, WorkoutLog? existing, int? memberId = null, int? sessionId = null, DateTime? date = null)
        {
            var (members, _) = await CoachTargets(coachId);
            var dialog = new FormDialog(existing == null ? "Ghi kết quả buổi tập" : "Sửa kết quả buổi tập", "Kết quả hiển thị cho học viên trong mục Kết quả tập.", Icons.Checklist, new[]
            {
                new FormField("Member", "Học viên", FieldKind.Combo) { Required = true, Wide = true, Options = members, Value = existing?.MemberId ?? memberId },
                new FormField("Date", "Ngày tập", FieldKind.Date) { Required = true, Value = existing?.Date ?? date ?? DateTime.Today,
                    Validate = (_, v) => v is DateTime d && d > DateTime.Today ? "Không ghi kết quả cho ngày tương lai." : null },
                new FormField("Duration", "Thời lượng (phút)", FieldKind.Number) { Value = existing?.DurationMinutes ?? 60, Validate = Rules.Range(5, 300) },
                new FormField("Exercises", "Bài tập & kết quả", FieldKind.Multiline) { Required = true, Wide = true, MaxLength = 2000, Value = existing?.Exercises,
                    Placeholder = "vd. Squat 60 kg 4×8; Plank 3×60 giây; chạy 3 km 18 phút" },
                new FormField("Weight", "Cân nặng (kg)", FieldKind.Decimal) { Value = existing?.WeightKg },
                new FormField("Notes", "Nhận xét nhanh", FieldKind.Text) { MaxLength = 500, Value = existing?.Notes },
            }, async f =>
            {
                await ServiceScope.Run(s => s.Training.SaveLogAsync(new WorkoutLog
                {
                    Id = existing?.Id ?? 0, CoachId = existing?.CoachId ?? coachId, MemberId = Rules.Id(f.Get("Member")), SessionId = existing?.SessionId ?? sessionId,
                    Date = f.Value<DateTime>("Date")!.Value, DurationMinutes = f.Value<int>("Duration"), Exercises = f.Text("Exercises"),
                    WeightKg = f.Value<double>("Weight"), Notes = f.Text("Notes")
                }));
                return true;
            }, "Lưu kết quả");
            if (!await UiHost.ShowDialogAsync(dialog)) return false;
            UiHost.Toast("Đã ghi kết quả buổi tập.");
            return true;
        }

        public static async Task<bool> ReviewAsync(int coachId, ProgressReview? existing, int? memberId = null)
        {
            var (members, _) = await CoachTargets(coachId);
            var dialog = new FormDialog(existing == null ? "Đánh giá tiến độ" : "Sửa đánh giá", "Học viên sẽ nhận thông báo khi có nhận xét mới.", Icons.Success, new[]
            {
                new FormField("Member", "Học viên", FieldKind.Combo) { Required = true, Wide = true, Options = members, Value = existing?.MemberId ?? memberId },
                new FormField("Date", "Ngày đánh giá", FieldKind.Date) { Required = true, Value = existing?.Date ?? DateTime.Today },
                new FormField("Score", "Điểm tiến độ (1–10)", FieldKind.Number) { Required = true, Value = existing?.Score, Validate = Rules.Range(1, 10) },
                new FormField("Comment", "Nhận xét", FieldKind.Multiline) { Required = true, Wide = true, MaxLength = 1000, Value = existing?.Comment,
                    Placeholder = "Điểm mạnh, điểm cần cải thiện, mục tiêu tiếp theo" },
            }, async f =>
            {
                await ServiceScope.Run(s => s.Training.SaveReviewAsync(new ProgressReview
                {
                    Id = existing?.Id ?? 0, CoachId = existing?.CoachId ?? coachId, MemberId = Rules.Id(f.Get("Member")),
                    Date = f.Value<DateTime>("Date")!.Value, Score = f.Value<int>("Score") ?? 0, Comment = f.Text("Comment")
                }));
                return true;
            }, "Lưu đánh giá");
            if (!await UiHost.ShowDialogAsync(dialog)) return false;
            UiHost.Toast("Đã lưu đánh giá tiến độ.");
            return true;
        }

        /// <summary>Gửi thông báo / bài tập về nhà tới cả lớp hoặc một học viên.</summary>
        public static async Task<bool> SendNoticeAsync(int? coachId, int? presetMemberId = null, int? presetClassId = null, NotificationType type = NotificationType.Homework)
        {
            var targets = new List<(string, object)>();
            var classes = await ServiceScope.Run(s => s.Classes.ListAsync(coachId));
            targets.AddRange(classes.Where(c => c.Class.Status == ClassStatus.Open).Select(c => ($"Cả lớp: {c.Class.Name} ({c.Enrolled} học viên)", (object)$"c:{c.Class.Id}")));
            if (coachId is int cid)
                targets.AddRange((await ServiceScope.Run(s => s.Coaches.MyMembersAsync(cid))).Select(x => ($"Học viên: {x.Member.User!.FullName}", (object)$"m:{x.Member.Id}")));
            else
                targets.AddRange((await Rules.MemberOptions()).Select(m => ($"Thành viên: {m.Item1}", (object)$"m:{m.Item2}")));
            var preset = presetMemberId != null ? $"m:{presetMemberId}" : presetClassId != null ? $"c:{presetClassId}" : null;

            var dialog = new FormDialog("Gửi thông báo / bài tập", "Người nhận xem trong mục Thông báo.", Icons.Info, new[]
            {
                new FormField("Target", "Gửi tới", FieldKind.Combo) { Required = true, Wide = true, Options = targets, Value = preset },
                new FormField("Type", "Loại", FieldKind.Combo) { Required = true, Value = type,
                    Options = new[] { NotificationType.Homework, NotificationType.General, NotificationType.ScheduleChange }.Select(t => (StatusBadge.Describe("notify", t.ToString()).Label, (object)t)) },
                new FormField("Title", "Tiêu đề", FieldKind.Text) { Required = true, MaxLength = 200, Placeholder = "vd. Bài tập về nhà tuần này" },
                new FormField("Body", "Nội dung", FieldKind.Multiline) { Required = true, Wide = true, MaxLength = 2000 },
            }, async f =>
            {
                var target = (string)f.Get("Target")!;
                var id = int.Parse(target[2..]);
                var t = (NotificationType)f.Get("Type")!;
                var count = await ServiceScope.Run(async s =>
                {
                    if (target.StartsWith("c:")) return await s.Notifications.SendToClassAsync(id, f.Text("Title"), f.Text("Body"), t);
                    await s.Notifications.SendToMemberAsync(id, f.Text("Title"), f.Text("Body"), t);
                    return 1;
                });
                UiHost.Toast($"Đã gửi tới {count} người.");
                return true;
            }, "Gửi");
            return await UiHost.ShowDialogAsync(dialog);
        }
    }
}
