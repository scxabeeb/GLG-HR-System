namespace HrSystemDhg.Web.Services;

public static class MenuPermissions
{
    public static readonly (string Key, string Label, string Module)[] Items =
    [
        ("people.directory", "Employee directory & details", "people"), ("people.register", "Register / import employees", "people"), ("people.reports", "Employee reports", "people"),
        ("attendance.manage", "Attendance & imports", "attendance"), ("attendance.corrections", "Attendance corrections", "attendance"), ("attendance.reports", "Attendance reports", "attendance"),
        ("leave.requests", "Leave requests & approvals", "leave"), ("leave.balances", "Leave balances", "leave"), ("leave.reports", "Leave reports", "leave"),
        ("payroll.salary", "Salary registration", "payroll"), ("payroll.salaryreports", "Salary reports", "payroll"), ("payroll.manage", "Manage payroll", "payroll"), ("payroll.reports", "Payroll reports", "payroll"),
        ("personal.workspace", "My workspace & profile", "personal"), ("personal.agenda", "My agenda", "personal"), ("personal.payslips", "My payslips", "personal"),
        ("tasks.mine", "My tasks & progress updates", "tasks"), ("tasks.manage", "Manage tasks & subtasks", "tasks"), ("tasks.reports", "Task progress reports", "tasks"),
        ("meetings.mine", "My meetings & join room", "meetings"), ("meetings.manage", "Manage meetings", "meetings"), ("meetings.reports", "Meeting reports", "meetings"), ("meetings.recordings", "Saved recordings", "meetings"),
        ("fleet.trips", "Fleet trips", "fleet"), ("fleet.register", "Vehicle register", "fleet"), ("fleet.reports", "Fleet reports & expenses", "fleet"),
        ("assets.register", "Asset register", "assets"), ("assets.edit", "Register / edit assets", "assets"), ("assets.reports", "Asset reports", "assets"),
        ("reports.hub", "Main reports", "reports")
    ];
    public static string? Report(string? source) => source?.ToLowerInvariant() switch
    {
        "attendance" => "attendance.reports", "leave" or "balances" => "leave.reports",
        "salary" => "payroll.salaryreports", "payroll" or "deductions" or "bank" => "payroll.reports",
        _ => "people.reports"
    };
    public static bool Allows(HashSet<string> denied, string? key)
    {
        if (key == null) return true;
        var module = Items.FirstOrDefault(x => x.Key == key).Module;
        return !denied.Contains(key) && ModuleAccess.Allows(denied, module);
    }
    public static string? ForUrl(string path, IQueryCollection query, string method = "GET")
    {
        path = path.TrimEnd('/').ToLowerInvariant();
        var handler = (query["handler"].FirstOrDefault() ?? "").ToLowerInvariant();
        if (path.StartsWith("/api/mobile/v2/manage/reports/")) return path.EndsWith("/tasks") ? "tasks.reports" : "meetings.reports";
        if (path.StartsWith("/api/mobile/v2/manage"))
        {
            var module = path.EndsWith("/options") ? (query["module"].FirstOrDefault() ?? "").ToLowerInvariant() : path.Contains("/meetings") ? "meetings" : "tasks";
            return module is "tasks" or "meetings" ? module + ".manage" : null;
        }
        if (path is "/api/mobile/v2/signal" or "/operations/meeting-signal") return "meetings.mine";
        if (path.StartsWith("/api/mobile/v2/tasks")) return "tasks.mine";
        if (path.StartsWith("/api/mobile/v2/meetings")) return path.EndsWith("/end") ? "meetings.manage" : "meetings.mine";
        if (path.StartsWith("/api/mobile/v2/payroll")) return "personal.payslips";
        if (path.StartsWith("/api/mobile/v2/profile")) return "personal.workspace";
        if (path.StartsWith("/api/mobile/v1/")) return path.Split('/').Last() switch { "tasks" => "tasks.mine", "meetings" => "meetings.mine", "payroll" => "personal.payslips", "profile" or "avatar" or "attendance" => "personal.workspace", _ => null };
        return path switch
        {
            "/hr/register" => "people.register",
            "/hr/employees" or "/hr/details" or "/hr/employment" or "/hr/onboarding" or "/hr/historyedit" or "/files/download" => "people.directory",
            "/hr/attendance" => "attendance.manage", "/hr/corrections" => "attendance.corrections",
            "/hr/leave" or "/me/approvals" => "leave.requests", "/hr/balances" => "leave.balances",
            "/hr/salary" => "payroll.salary", "/management/payroll" => "payroll.manage",
            "/me/profile" => "personal.workspace", "/me/agenda" => "personal.agenda", "/me/payslip" => "personal.payslips",
            "/reports" or "/reports/index" => Report(query["report"].FirstOrDefault() ?? "Employees"), "/reports/all" or "/reports/hub" => "reports.hub",
            "/operations/tasks" => handler is "save" or "import" or "sample" or "export" || query.ContainsKey("edit") || query["view"].FirstOrDefault() == "created" ? "tasks.manage" : "tasks.mine",
            "/operations/taskreport" => "tasks.reports",
            "/operations/meetings" => handler is "save" or "import" or "end" or "cancel" or "sample" or "export" || query.ContainsKey("edit") || query["mode"].FirstOrDefault() == "manage" ? "meetings.manage" : "meetings.mine",
            "/operations/meetingreport" => "meetings.reports", "/operations/meetingrecordings" => "meetings.recordings",
            "/operations/meetingdetails" => method == "POST" && handler is "" or "document" or "followup" ? "meetings.manage" : "meetings.mine",
            "/operations/meetingroom" => "meetings.mine",
            "/operations/fleet" => "fleet.register", "/operations/fleettrips" => "fleet.trips", "/operations/fleetreport" => "fleet.reports",
            "/assets" or "/assets/index" => bool.TryParse(query["report"].FirstOrDefault(), out var report) && report ? "assets.reports" : "assets.register", "/assets/edit" => handler == "photo" ? "assets.register" : "assets.edit",
            _ => null
        };
    }
}
