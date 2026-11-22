using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using SSAM_Server.Models;

namespace SSAM_Server.Services.Common;

public static class LayoutSvgGenerator
{
    public sealed class UiBlueprint
    {
        public string AppName { get; set; } = "SSAM Workspace";
        public string ScreenTitle { get; set; } = "Screen Layout";
        public string Subtitle { get; set; } = string.Empty;
        public List<string> NavItems { get; set; } = new();
        public List<KpiItem> Kpis { get; set; } = new();
        public string FormTitle { get; set; } = "Thông tin thao tác";
        public List<FormFieldItem> Fields { get; set; } = new();
        public string PrimaryButton { get; set; } = "Xác nhận";
        public string SecondaryButton { get; set; } = "Làm mới";
        public string? AlertText { get; set; }
        public string TableTitle { get; set; } = "Danh sách dữ liệu";
        public List<string> Columns { get; set; } = new();
        public List<List<string>> Rows { get; set; } = new();
    }

    public sealed class KpiItem
    {
        public string Label { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string Hint { get; set; } = string.Empty;
    }

    public sealed class FormFieldItem
    {
        public string Label { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string Kind { get; set; } = "text"; // text | select | number
    }

    public static string BuildPrompt(string layoutTitle, DocumentNode doc, List<RequirementItem> reqs)
    {
        var reqLines = new StringBuilder();
        foreach (var r in reqs.Take(8))
        {
            reqLines.AppendLine($"- [{r.ReqUid}] {r.Title}");
            if (!string.IsNullOrWhiteSpace(r.Actor)) reqLines.AppendLine($"  Actor: {r.Actor}");
            if (!string.IsNullOrWhiteSpace(r.Preconditions)) reqLines.AppendLine($"  Preconditions: {r.Preconditions}");
            if (!string.IsNullOrWhiteSpace(r.BodyPreview)) reqLines.AppendLine($"  Details: {r.BodyPreview}");
        }
        if (reqs.Count == 0)
            reqLines.AppendLine($"Document: {doc.Title} ({doc.DocType ?? "SPEC"})");

        return $@"You are a senior UI/UX product designer. Design a realistic, domain-accurate web application screen layout based on these requirements.
Return ONLY valid JSON (no markdown fences, no commentary) matching this exact schema:
{{
  ""appName"": ""short product name"",
  ""screenTitle"": ""{layoutTitle}"",
  ""subtitle"": ""short 1-line description"",
  ""navItems"": [""5 short sidebar menu labels matching the domain""],
  ""kpis"": [
    {{ ""label"": ""metric 1"", ""value"": ""realistic value"", ""hint"": ""+4.2%"" }},
    {{ ""label"": ""metric 2"", ""value"": ""realistic value"", ""hint"": ""real-time"" }},
    {{ ""label"": ""metric 3"", ""value"": ""realistic value"", ""hint"": ""status"" }}
  ],
  ""formTitle"": ""main action/form panel title"",
  ""fields"": [
    {{ ""label"": ""field 1"", ""value"": ""realistic sample value"", ""kind"": ""select"" }},
    {{ ""label"": ""field 2"", ""value"": ""realistic sample value"", ""kind"": ""text"" }},
    {{ ""label"": ""field 3"", ""value"": ""realistic sample value"", ""kind"": ""number"" }},
    {{ ""label"": ""field 4"", ""value"": ""realistic sample value"", ""kind"": ""text"" }}
  ],
  ""primaryButton"": ""main CTA button text"",
  ""secondaryButton"": ""secondary button text"",
  ""alertText"": ""1 short validation/compliance rule from requirements"",
  ""tableTitle"": ""data grid/list title"",
  ""columns"": [""Col 1"", ""Col 2"", ""Col 3"", ""Status""],
  ""rows"": [
    [""val"", ""val"", ""val"", ""Active""],
    [""val"", ""val"", ""val"", ""Pending""],
    [""val"", ""val"", ""val"", ""Warning""],
    [""val"", ""val"", ""val"", ""Done""]
  ]
}}

Document: {doc.Title}
Requirements:
{reqLines}

Use the same language (Vietnamese/English) as the requirements. Keep labels concise so they fit neatly in a UI.";
    }

    public static UiBlueprint? TryParseBlueprint(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var match = Regex.Match(raw, @"\{[\s\S]*\}");
        if (!match.Success) return null;
        try
        {
            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var bp = JsonSerializer.Deserialize<UiBlueprint>(match.Value, opts);
            if (bp == null || string.IsNullOrWhiteSpace(bp.ScreenTitle)) return null;
            return bp;
        }
        catch
        {
            return null;
        }
    }

    public static UiBlueprint BuildFallbackBlueprint(string layoutTitle, DocumentNode doc, List<RequirementItem> reqs)
    {
        var bp = new UiBlueprint
        {
            AppName = "SSAM Workspace",
            ScreenTitle = layoutTitle,
            Subtitle = doc.Title,
            NavItems = new List<string> { "Tổng quan", "Nghiệp vụ chính", "Danh sách", "Báo cáo", "Cấu hình" },
            Kpis = new List<KpiItem>
            {
                new() { Label = "Yêu cầu liên kết", Value = $"{reqs.Count}", Hint = "Đang áp dụng" },
                new() { Label = "Trạng thái luồng", Value = "Sẵn sàng", Hint = "Real-time" },
                new() { Label = "Kiểm tra hợp lệ", Value = "100%", Hint = "Chuẩn quy tắc" }
            },
            FormTitle = reqs.FirstOrDefault()?.Title ?? layoutTitle,
            PrimaryButton = "Xác nhận",
            SecondaryButton = "Đặt lại",
            AlertText = reqs.FirstOrDefault()?.Preconditions ?? "Kiểm tra đầy đủ dữ liệu đầu vào trước khi gửi.",
            TableTitle = $"Chi tiết theo {doc.Code ?? doc.Title}",
            Columns = new List<string> { "Mã REQ", "Hạng mục", "Đối tượng", "Trạng thái" }
        };

        foreach (var r in reqs.Take(4))
        {
            bp.Fields.Add(new FormFieldItem
            {
                Label = Trunc(r.Title, 32),
                Value = string.IsNullOrWhiteSpace(r.Actor) ? "Nhập giá trị…" : r.Actor,
                Kind = "text"
            });
            bp.Rows.Add(new List<string>
            {
                r.ReqUid,
                Trunc(r.Title, 34),
                string.IsNullOrWhiteSpace(r.Actor) ? "User" : Trunc(r.Actor, 16),
                "Active"
            });
        }

        if (bp.Fields.Count == 0)
        {
            bp.Fields.Add(new FormFieldItem { Label = "Tên hạng mục", Value = doc.Title, Kind = "text" });
            bp.Fields.Add(new FormFieldItem { Label = "Phân loại", Value = doc.DocType ?? "SPEC", Kind = "select" });
            bp.Fields.Add(new FormFieldItem { Label = "Ghi chú", Value = "Mô tả chi tiết…", Kind = "text" });
        }
        if (bp.Rows.Count == 0)
        {
            bp.Rows.Add(new List<string> { "ITEM-01", Trunc(doc.Title, 34), "Admin", "Active" });
            bp.Rows.Add(new List<string> { "ITEM-02", "Kiểm tra dữ liệu", "System", "Pending" });
        }

        return bp;
    }

    public static string RenderSvg(UiBlueprint bp)
    {
        var navs = bp.NavItems.Count > 0 ? bp.NavItems.Take(6).ToList() : new List<string> { "Dashboard", "Workspace", "Orders", "Reports", "Settings" };
        var kpis = bp.Kpis.Take(3).ToList();
        while (kpis.Count < 3)
            kpis.Add(new KpiItem { Label = $"Metric {kpis.Count + 1}", Value = "—", Hint = "Active" });

        var fields = bp.Fields.Take(5).ToList();
        var cols = bp.Columns.Count > 0 ? bp.Columns.Take(4).ToList() : new List<string> { "Code", "Name", "Owner", "Status" };
        var rows = bp.Rows.Take(5).ToList();

        var sb = new StringBuilder();
        sb.AppendLine(@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 1280 860"" width=""1280"" height=""860"">");
        sb.AppendLine(@"  <rect width=""1280"" height=""860"" fill=""#f1f5f9""/>");

        // Top Header Bar
        sb.AppendLine(@"  <rect x=""0"" y=""0"" width=""1280"" height=""64"" fill=""#ffffff""/>");
        sb.AppendLine(@"  <line x1=""0"" y1=""64"" x2=""1280"" y2=""64"" stroke=""#e2e8f0"" stroke-width=""1""/>");
        sb.AppendLine(@"  <rect x=""24"" y=""16"" width=""32"" height=""32"" rx=""8"" fill=""#2563eb""/>");
        sb.AppendLine(@"  <text x=""40"" y=""37"" text-anchor=""middle"" font-family=""Inter, Arial, sans-serif"" font-size=""15"" font-weight=""700"" fill=""#ffffff"">S</text>");
        sb.AppendLine($@"  <text x=""68"" y=""37"" font-family=""Inter, Arial, sans-serif"" font-size=""15"" font-weight=""700"" fill=""#0f172a"">{Enc(Trunc(bp.AppName, 24))}</text>");

        // Search box in header
        sb.AppendLine(@"  <rect x=""300"" y=""14"" width=""380"" height=""36"" rx=""8"" fill=""#f8fafc"" stroke=""#cbd5e1""/>");
        sb.AppendLine(@"  <text x=""318"" y=""37"" font-family=""Inter, Arial, sans-serif"" font-size=""13"" fill=""#94a3b8"">Tìm kiếm nhanh (Ctrl+K)...</text>");

        // Header right controls
        sb.AppendLine(@"  <rect x=""1070"" y=""16"" width=""96"" height=""32"" rx=""8"" fill=""#eff6ff"" stroke=""#bfdbfe""/>");
        sb.AppendLine(@"  <text x=""1118"" y=""37"" text-anchor=""middle"" font-family=""Inter, Arial, sans-serif"" font-size=""12"" font-weight=""600"" fill=""#1d4ed8"">● Live</text>");
        sb.AppendLine(@"  <circle cx=""1200"" cy=""32"" r=""16"" fill=""#e2e8f0""/>");
        sb.AppendLine(@"  <text x=""1200"" y=""37"" text-anchor=""middle"" font-family=""Inter, Arial, sans-serif"" font-size=""12"" font-weight=""700"" fill=""#334155"">UI</text>");

        // Left Sidebar
        sb.AppendLine(@"  <rect x=""16"" y=""80"" width=""232"" height=""764"" rx=""12"" fill=""#ffffff"" stroke=""#e2e8f0""/>");
        sb.AppendLine(@"  <text x=""36"" y=""112"" font-family=""Inter, Arial, sans-serif"" font-size=""11"" font-weight=""700"" fill=""#94a3b8"">MENU CHÍNH</text>");
        for (int i = 0; i < navs.Count; i++)
        {
            var y = 128 + i * 44;
            var active = i == 0;
            sb.AppendLine($@"  <rect x=""28"" y=""{y}"" width=""208"" height=""36"" rx=""8"" fill=""{(active ? "#eff6ff" : "#ffffff")}"" {(active ? @"stroke=""#bfdbfe""" : "")}/>");
            sb.AppendLine($@"  <circle cx=""46"" cy=""{y + 18}"" r=""4"" fill=""{(active ? "#2563eb" : "#cbd5e1")}""/>");
            sb.AppendLine($@"  <text x=""60"" y=""{y + 23}"" font-family=""Inter, Arial, sans-serif"" font-size=""13"" font-weight=""{(active ? "600" : "500")}"" fill=""{(active ? "#1d4ed8" : "#475569")}"">{Enc(Trunc(navs[i], 22))}</text>");
        }

        // Main Header Title
        sb.AppendLine($@"  <text x=""272"" y=""110"" font-family=""Inter, Arial, sans-serif"" font-size=""22"" font-weight=""700"" fill=""#0f172a"">{Enc(Trunc(bp.ScreenTitle, 52))}</text>");
        if (!string.IsNullOrWhiteSpace(bp.Subtitle))
            sb.AppendLine($@"  <text x=""272"" y=""132"" font-family=""Inter, Arial, sans-serif"" font-size=""13"" fill=""#64748b"">{Enc(Trunc(bp.Subtitle, 85))}</text>");

        // 3 KPI Cards
        for (int i = 0; i < 3; i++)
        {
            var x = 272 + i * 334;
            var k = kpis[i];
            sb.AppendLine($@"  <rect x=""{x}"" y=""152"" width=""318"" height=""94"" rx=""12"" fill=""#ffffff"" stroke=""#e2e8f0""/>");
            sb.AppendLine($@"  <text x=""{x + 20}"" y=""180"" font-family=""Inter, Arial, sans-serif"" font-size=""12"" font-weight=""600"" fill=""#64748b"">{Enc(Trunc(k.Label, 28))}</text>");
            sb.AppendLine($@"  <text x=""{x + 20}"" y=""214"" font-family=""Inter, Arial, sans-serif"" font-size=""22"" font-weight=""700"" fill=""#0f172a"">{Enc(Trunc(k.Value, 18))}</text>");
            sb.AppendLine($@"  <rect x=""{x + 216}"" y=""194"" width=""84"" height=""24"" rx=""12"" fill=""#ecfdf5""/>");
            sb.AppendLine($@"  <text x=""{x + 258}"" y=""210"" text-anchor=""middle"" font-family=""Inter, Arial, sans-serif"" font-size=""11"" font-weight=""600"" fill=""#047857"">{Enc(Trunc(k.Hint, 12))}</text>");
        }

        // Left Form Panel (x=272, w=420, y=264, h=580)
        sb.AppendLine(@"  <rect x=""272"" y=""264"" width=""420"" height=""580"" rx=""12"" fill=""#ffffff"" stroke=""#e2e8f0""/>");
        sb.AppendLine($@"  <text x=""294"" y=""298"" font-family=""Inter, Arial, sans-serif"" font-size=""16"" font-weight=""700"" fill=""#0f172a"">{Enc(Trunc(bp.FormTitle, 38))}</text>");
        sb.AppendLine(@"  <line x1=""272"" y1=""314"" x2=""692"" y2=""314"" stroke=""#f1f5f9"" stroke-width=""1""/>");

        int fy = 334;
        foreach (var f in fields)
        {
            sb.AppendLine($@"  <text x=""294"" y=""{fy + 14}"" font-family=""Inter, Arial, sans-serif"" font-size=""12"" font-weight=""600"" fill=""#334155"">{Enc(Trunc(f.Label, 44))}</text>");
            sb.AppendLine($@"  <rect x=""294"" y=""{fy + 22}"" width=""376"" height=""36"" rx=""8"" fill=""#f8fafc"" stroke=""#cbd5e1""/>");
            sb.AppendLine($@"  <text x=""308"" y=""{fy + 45}"" font-family=""Inter, Arial, sans-serif"" font-size=""13"" fill=""#0f172a"">{Enc(Trunc(f.Value, 40))}</text>");
            if (string.Equals(f.Kind, "select", StringComparison.OrdinalIgnoreCase))
                sb.AppendLine($@"  <text x=""654"" y=""{fy + 45}"" text-anchor=""middle"" font-family=""Inter, Arial, sans-serif"" font-size=""12"" fill=""#64748b"">▾</text>");
            fy += 70;
        }

        if (!string.IsNullOrWhiteSpace(bp.AlertText))
        {
            var ay = Math.Max(fy + 4, 690);
            if (ay > 720) ay = 720;
            sb.AppendLine($@"  <rect x=""294"" y=""{ay}"" width=""376"" height=""44"" rx=""8"" fill=""#fffbeb"" stroke=""#fde68a""/>");
            sb.AppendLine($@"  <text x=""310"" y=""{ay + 27}"" font-family=""Inter, Arial, sans-serif"" font-size=""12"" fill=""#92400e"">⚠ {Enc(Trunc(bp.AlertText, 48))}</text>");
        }

        // Form Action Buttons
        sb.AppendLine(@"  <rect x=""294"" y=""784"" width=""140"" height=""40"" rx=""8"" fill=""#ffffff"" stroke=""#cbd5e1""/>");
        sb.AppendLine($@"  <text x=""364"" y=""809"" text-anchor=""middle"" font-family=""Inter, Arial, sans-serif"" font-size=""13"" font-weight=""600"" fill=""#334155"">{Enc(Trunc(bp.SecondaryButton, 16))}</text>");
        sb.AppendLine(@"  <rect x=""446"" y=""784"" width=""224"" height=""40"" rx=""8"" fill=""#2563eb""/>");
        sb.AppendLine($@"  <text x=""558"" y=""809"" text-anchor=""middle"" font-family=""Inter, Arial, sans-serif"" font-size=""13"" font-weight=""600"" fill=""#ffffff"">{Enc(Trunc(bp.PrimaryButton, 22))}</text>");

        // Right Data Table Panel (x=712, w=548, y=264, h=580)
        sb.AppendLine(@"  <rect x=""712"" y=""264"" width=""548"" height=""580"" rx=""12"" fill=""#ffffff"" stroke=""#e2e8f0""/>");
        sb.AppendLine($@"  <text x=""734"" y=""298"" font-family=""Inter, Arial, sans-serif"" font-size=""16"" font-weight=""700"" fill=""#0f172a"">{Enc(Trunc(bp.TableTitle, 44))}</text>");
        sb.AppendLine(@"  <rect x=""1140"" y=""276"" width=""98"" height=""30"" rx=""6"" fill=""#f8fafc"" stroke=""#cbd5e1""/>");
        sb.AppendLine(@"  <text x=""1189"" y=""296"" text-anchor=""middle"" font-family=""Inter, Arial, sans-serif"" font-size=""12"" font-weight=""500"" fill=""#475569"">Bộ lọc</text>");

        // Table Header
        sb.AppendLine(@"  <rect x=""732"" y=""324"" width=""508"" height=""36"" rx=""6"" fill=""#f8fafc""/>");
        int[] colX = { 746, 846, 1026, 1152 };
        for (int c = 0; c < Math.Min(4, cols.Count); c++)
        {
            sb.AppendLine($@"  <text x=""{colX[c]}"" y=""347"" font-family=""Inter, Arial, sans-serif"" font-size=""12"" font-weight=""700"" fill=""#475569"">{Enc(Trunc(cols[c], 18))}</text>");
        }

        // Table Rows
        for (int r = 0; r < rows.Count; r++)
        {
            var ry = 372 + r * 56;
            sb.AppendLine($@"  <line x1=""732"" y1=""{ry + 48}"" x2=""1240"" y2=""{ry + 48}"" stroke=""#f1f5f9""/>");
            var row = rows[r];
            for (int c = 0; c < Math.Min(4, row.Count); c++)
            {
                var cell = row[c] ?? "";
                if (c == 3)
                {
                    var isWarn = cell.Contains("Warn", StringComparison.OrdinalIgnoreCase) || cell.Contains("Cảnh", StringComparison.OrdinalIgnoreCase);
                    var pillBg = isWarn ? "#fef3c7" : "#dcfce7";
                    var pillFg = isWarn ? "#b45309" : "#15803d";
                    sb.AppendLine($@"  <rect x=""{colX[c]}"" y=""{ry + 10}"" width=""76"" height=""24"" rx=""12"" fill=""{pillBg}""/>");
                    sb.AppendLine($@"  <text x=""{colX[c] + 38}"" y=""{ry + 26}"" text-anchor=""middle"" font-family=""Inter, Arial, sans-serif"" font-size=""11"" font-weight=""600"" fill=""{pillFg}"">{Enc(Trunc(cell, 10))}</text>");
                }
                else
                {
                    var maxLen = c == 1 ? 24 : 13;
                    var weight = c == 0 ? "600" : "400";
                    var fg = c == 0 ? "#1d4ed8" : "#0f172a";
                    sb.AppendLine($@"  <text x=""{colX[c]}"" y=""{ry + 27}"" font-family=""Inter, Arial, sans-serif"" font-size=""12"" font-weight=""{weight}"" fill=""{fg}"">{Enc(Trunc(cell, maxLen))}</text>");
                }
            }
        }

        sb.AppendLine(@"</svg>");
        return sb.ToString();
    }

    private static string Enc(string s) => WebUtility.HtmlEncode(s ?? string.Empty);

    private static string Trunc(string s, int max)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        return s.Length <= max ? s : s[..(max - 1)] + "…";
    }
}
