using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using SSAM_Server.Models;

namespace SSAM_Server.Services.Common;

public static class LayoutSvgGenerator
{
    public sealed class UiBlueprint
    {
        public string Shell { get; set; } = "SIDEBAR_TOPBAR"; // SIDEBAR_TOPBAR | TOPBAR_ONLY | SPLIT_DARK_LEFT | CENTERED_MINIMAL
        public string HeaderStyle { get; set; } = "STANDARD"; // STANDARD | SEARCH_FILTERS | TABS_BAR | STEPPER | BREADCRUMB_ACTIONS | HERO_BANNER
        public string AppName { get; set; } = "Workspace";
        public string ScreenTitle { get; set; } = "Main Screen";
        public string Subtitle { get; set; } = "";
        public List<string> NavItems { get; set; } = new();
        public List<string> HeaderItems { get; set; } = new(); // tabs, filter pills, or steps
        public string PrimaryButton { get; set; } = "Confirm";
        public string SecondaryButton { get; set; } = "Cancel";
        public List<LayoutRow> Rows { get; set; } = new();
    }

    public sealed class LayoutRow
    {
        public List<LayoutBlock> Blocks { get; set; } = new();
    }

    public sealed class LayoutBlock
    {
        // KPI_ROW | FORM | TABLE | CATALOG_GRID | CART_SUMMARY | KANBAN | CHAT_THREAD | CALENDAR_SCHEDULE | CHARTS_ANALYTICS | TIMELINE_FEED | SETTINGS_TOGGLES | DETAIL_KEY_VALUE | ALERT_CHECKLIST | MAP_TRACKING
        public string Type { get; set; } = "FORM";
        public int ColSpan { get; set; } = 12; // 1..12
        public string Title { get; set; } = "";
        public string Badge { get; set; } = "";
        public List<string> Columns { get; set; } = new();
        public List<List<string>> TableRows { get; set; } = new();
        public List<BlockItem> Items { get; set; } = new();
        public string Note { get; set; } = "";
        public string TotalLabel { get; set; } = "";
        public string TotalValue { get; set; } = "";
    }

    public sealed class BlockItem
    {
        public string Label { get; set; } = "";
        public string Value { get; set; } = "";
        public string Sub { get; set; } = "";
        public string Tag { get; set; } = "";
        public bool Active { get; set; } = true;
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string BuildPrompt(
        string layoutTitle,
        DocumentNode doc,
        List<RequirementItem> targetReqs,
        string? userNotes = null,
        bool hasReferenceImage = false)
    {
        var reqList = string.Join("\n", targetReqs.Select(r =>
            $"- [{r.ReqUid}] {r.Title}: {Trunc(r.BodyPreview, 180)}"));
        var notesSection = string.IsNullOrWhiteSpace(userNotes)
            ? ""
            : $"\nUser Design Notes / Layout Instructions (HIGHEST PRIORITY):\n{userNotes.Trim()}\n";
        var refImageSection = hasReferenceImage
            ? "\nReference Layout Image Attached: YES.\nCarefully inspect the attached reference UI image's visual structure (navigation shell, header style, columns, proportions, card/table/form/chart/kanban/catalog arrangement) and replicate that layout structure using our 12-column block system, populated with the domain data from the Document and Requirements.\n"
            : "";

        return $$"""
You are a Senior Product Designer. Analyze the Document and Requirements below and compose the MOST APPROPRIATE UI layout using our flexible 12-column block system. Do NOT force everything into a dashboard — pick the exact shell, headerStyle, and block types that truly fit the user's requirements! Return ONLY valid JSON (no markdown, no explanation).

Available `shell` options:
- "SIDEBAR_TOPBAR": Full app with left navigation + top bar.
- "TOPBAR_ONLY": Full-width workspace (great for Kanban, POS, Maps, Calendar, Analytics).
- "SPLIT_DARK_LEFT": Dark branding/stepper left panel + focused right workspace (great for Login, Register, Onboarding, Wizards).
- "CENTERED_MINIMAL": Clean centered single-column workspace (great for Settings, Checkout, Forms, Profiles).

Available `headerStyle` options:
- "STANDARD", "SEARCH_FILTERS", "TABS_BAR", "STEPPER", "BREADCRUMB_ACTIONS", "HERO_BANNER"

Available `block.type` options (compose 1 to 3 rows, each row's colSpan summing to 12):
- "KPI_ROW": 2-4 metric cards (items: label, value, tag).
- "FORM": Input fields & CTAs (items: label, value, tag=text|select|number; note=validation rule).
- "TABLE": Data table (columns: [...], tableRows: [[...]], items unused).
- "CATALOG_GRID": 4-6 product/menu/media cards (items: label=name, value=price/metric, sub=category, tag=badge).
- "CART_SUMMARY": Order/checkout summary (items: label=item, value=price, sub=qty; totalLabel, totalValue).
- "KANBAN": 3-4 workflow columns (columns: ["To Do","Doing","Review","Done"], items: label=title, sub=code, tag=priority).
- "CHAT_THREAD": Messaging/Support/AI inbox (items: label=sender, value=message, sub=time, active=true if outgoing).
- "CALENDAR_SCHEDULE": Time slots / booking calendar (columns: ["Mon","Tue","Wed","Thu","Fri"], items: label=event, sub=time, tag=status).
- "CHARTS_ANALYTICS": Bar/trend chart + breakdown (items: label=metric/period, value=number 10-100, sub=display value).
- "TIMELINE_FEED": Audit log / activity / notification stream (items: label=actor/title, value=description, sub=time, tag=badge).
- "SETTINGS_TOGGLES": Settings/permissions rows with toggle switches (items: label=setting, sub=description, active=true|false).
- "DETAIL_KEY_VALUE": Structured record details (items: label=field, value=val, tag=badge).
- "ALERT_CHECKLIST": Business rules / acceptance criteria checklist (items: label=rule, active=true|false).
- "MAP_TRACKING": Live map / route / branch locator + status cards (items: label=stop/location, value=status, sub=eta).
- "PRICING_PLANS": 3 subscription/pricing tier cards (items: label=plan name, value=price, sub=features, tag=badge, active=true if recommended).
- "LANDING_HERO": Marketing/portal hero banner + feature highlights (items: label=feature, value=description; note=hero headline).
- "PROFILE_HEADER": User/merchant/patient profile banner with avatar, stats, and quick actions (items: label=stat, value=val).
- "MEDIA_GALLERY": 2x3 image/document/video asset grid (items: label=file/media name, sub=type/size, tag=badge).
- "FILE_UPLOADER": Drag-and-drop upload zone + file queue with progress bars (items: label=filename, value=percent 10-100, sub=size).
- "NOTIFICATIONS_INBOX": Notification center with unread indicators & timestamps (items: label=title, value=body, sub=time, active=true if unread).
- "INVOICE_RECEIPT": Billing/payment receipt with line items & total (items: label=item, value=amount, sub=qty; totalLabel, totalValue).
- "DONUT_BREAKDOWN": Ring/donut allocation chart + legend (items: label=category, value=percent 10-100, sub=display amount).
- "COMPARISON_MATRIX": Side-by-side feature/option comparison table (columns: ["Feature","Opt A","Opt B","Opt C"], tableRows: [[...]]).
- "EMPTY_OR_CONFIRM_MODAL": Centered confirmation/success/empty state card (note=message, items: label=detail, value=val).

Document Title: {{doc.Title}}
Screen Title: {{layoutTitle}}{{notesSection}}{{refImageSection}}
Requirements:
{{reqList}}

Return JSON matching this structure:
{
  "shell": "TOPBAR_ONLY",
  "headerStyle": "SEARCH_FILTERS",
  "appName": "Short Name",
  "screenTitle": "{{layoutTitle}}",
  "subtitle": "1-line screen summary",
  "navItems": ["Nav 1", "Nav 2", "Nav 3", "Nav 4"],
  "headerItems": ["Tab/Filter/Step 1", "Tab/Filter/Step 2", "Tab/Filter/Step 3"],
  "primaryButton": "Primary CTA",
  "secondaryButton": "Secondary",
  "rows": [
    {
      "blocks": [
        {
          "type": "BLOCK_TYPE",
          "colSpan": 8,
          "title": "Block Title",
          "badge": "Live",
          "columns": ["Col 1", "Col 2", "Col 3", "Status"],
          "tableRows": [["A1", "B1", "C1", "Active"]],
          "items": [
            {"label": "Item 1", "value": "Value 1", "sub": "Sub 1", "tag": "Tag", "active": true}
          ],
          "note": "Optional rule or helper text",
          "totalLabel": "Total",
          "totalValue": "250.000đ"
        }
      ]
    }
  ]
}
""";
    }

    public static UiBlueprint? TryParseBlueprint(string rawResponse)
    {
        if (string.IsNullOrWhiteSpace(rawResponse)) return null;
        var clean = rawResponse.Trim();
        var firstBrace = clean.IndexOf('{');
        var lastBrace = clean.LastIndexOf('}');
        if (firstBrace >= 0 && lastBrace > firstBrace)
            clean = clean.Substring(firstBrace, lastBrace - firstBrace + 1);

        try
        {
            var bp = JsonSerializer.Deserialize<UiBlueprint>(clean, JsonOpts);
            if (bp == null || string.IsNullOrWhiteSpace(bp.ScreenTitle) || bp.Rows.Count == 0)
                return null;
            return bp;
        }
        catch
        {
            return null;
        }
    }

    public static UiBlueprint BuildFallbackBlueprint(string layoutTitle, DocumentNode doc, List<RequirementItem> targetReqs, string? userNotes = null)
    {
        var corpus = $"{userNotes} {layoutTitle} {doc.Title} {string.Join(" ", targetReqs.Select(r => $"{r.Title} {r.BodyPreview}"))}".ToLowerInvariant();
        var firstReq = targetReqs.FirstOrDefault();

        var bp = new UiBlueprint
        {
            AppName = Trunc(doc.Title, 18),
            ScreenTitle = Trunc(layoutTitle, 44),
            Subtitle = Trunc(firstReq?.Title ?? doc.Title, 70),
            NavItems = new List<string> { "Overview", "Workspace", "Requests", "Reports", "Settings" },
            HeaderItems = new List<string> { "All", "Active", "Pending", "Completed" },
            PrimaryButton = "Confirm",
            SecondaryButton = "Cancel"
        };

        if (ContainsAny(corpus, "chat", "tin nhắn", "message", "hỗ trợ", "support", "ticket", "chatbot", "tư vấn"))
        {
            bp.Shell = "SIDEBAR_TOPBAR";
            bp.HeaderStyle = "BREADCRUMB_ACTIONS";
            bp.Rows.Add(new LayoutRow
            {
                Blocks = new List<LayoutBlock>
                {
                    new()
                    {
                        Type = "CHAT_THREAD", ColSpan = 8, Title = "Hội thoại & Xử lý yêu cầu", Badge = "Online",
                        Items = targetReqs.Take(4).Select((r, i) => new BlockItem
                        {
                            Label = i % 2 == 0 ? (r.Actor ?? "Khách hàng") : "Hệ thống / CSKH",
                            Value = Trunc(r.Title, 48),
                            Sub = $"{10 + i}:0{i}",
                            Active = i % 2 == 1
                        }).ToList()
                    },
                    new()
                    {
                        Type = "DETAIL_KEY_VALUE", ColSpan = 4, Title = "Thông tin ngữ cảnh",
                        Items = targetReqs.Take(4).Select(r => new BlockItem
                        {
                            Label = Trunc(r.ReqUid, 16),
                            Value = Trunc(r.Title, 24),
                            Tag = "Active"
                        }).ToList()
                    }
                }
            });
            return bp;
        }

        if (ContainsAny(corpus, "lịch", "calendar", "schedule", "booking", "đặt lịch", "hẹn", "ca làm", "shift", "timetable"))
        {
            bp.Shell = "TOPBAR_ONLY";
            bp.HeaderStyle = "SEARCH_FILTERS";
            bp.HeaderItems = new List<string> { "Tuần này", "Hôm nay", "Còn trống", "Đã xác nhận" };
            bp.Rows.Add(new LayoutRow
            {
                Blocks = new List<LayoutBlock>
                {
                    new()
                    {
                        Type = "CALENDAR_SCHEDULE", ColSpan = 8, Title = "Lịch trình & Khung giờ", Badge = "Tuần 47",
                        Columns = new List<string> { "Thứ 2", "Thứ 3", "Thứ 4", "Thứ 5", "Thứ 6" },
                        Items = targetReqs.Take(5).Select((r, i) => new BlockItem
                        {
                            Label = Trunc(r.Title, 20),
                            Sub = $"{9 + i * 2}:00 - {10 + i * 2}:30",
                            Tag = i % 2 == 0 ? "Confirmed" : "Pending"
                        }).ToList()
                    },
                    new()
                    {
                        Type = "FORM", ColSpan = 4, Title = "Đặt lịch nhanh",
                        Items = targetReqs.Take(4).Select((r, i) => new BlockItem
                        {
                            Label = Trunc(r.ReqUid, 16),
                            Value = Trunc(r.Title, 24),
                            Tag = i == 1 ? "select" : "text"
                        }).ToList(),
                        Note = Trunc(firstReq?.BodyPreview ?? "Kiểm tra khung giờ trống trước khi xác nhận.", 56)
                    }
                }
            });
            return bp;
        }

        if (ContainsAny(corpus, "bản đồ", "map", "giao hàng", "delivery", "tracking", "vận chuyển", "ship", "logistics", "vị trí", "chi nhánh"))
        {
            bp.Shell = "TOPBAR_ONLY";
            bp.HeaderStyle = "BREADCRUMB_ACTIONS";
            bp.Rows.Add(new LayoutRow
            {
                Blocks = new List<LayoutBlock>
                {
                    new()
                    {
                        Type = "MAP_TRACKING", ColSpan = 7, Title = "Theo dõi lộ trình trực tiếp", Badge = "Live GPS",
                        Items = targetReqs.Take(3).Select((r, i) => new BlockItem
                        {
                            Label = Trunc(r.Title, 24),
                            Value = i == 0 ? "Đang giao" : "Điểm dừng",
                            Sub = $"ETA {12 + i * 8} phút"
                        }).ToList()
                    },
                    new()
                    {
                        Type = "TIMELINE_FEED", ColSpan = 5, Title = "Trạng thái vận đơn",
                        Items = targetReqs.Take(4).Select((r, i) => new BlockItem
                        {
                            Label = Trunc(r.ReqUid, 14),
                            Value = Trunc(r.Title, 32),
                            Sub = $"{i * 15 + 5}m trước",
                            Tag = i == 0 ? "Live" : "Done"
                        }).ToList()
                    }
                }
            });
            return bp;
        }

        if (ContainsAny(corpus, "cài đặt", "settings", "phân quyền", "permission", "role", "cấu hình", "config", "bảo mật", "security"))
        {
            bp.Shell = "SIDEBAR_TOPBAR";
            bp.HeaderStyle = "TABS_BAR";
            bp.HeaderItems = new List<string> { "Chung", "Phân quyền", "Thông báo", "Tích hợp API" };
            bp.Rows.Add(new LayoutRow
            {
                Blocks = new List<LayoutBlock>
                {
                    new()
                    {
                        Type = "SETTINGS_TOGGLES", ColSpan = 8, Title = "Cấu hình & Quyền truy cập",
                        Items = targetReqs.Take(5).Select((r, i) => new BlockItem
                        {
                            Label = Trunc(r.Title, 34),
                            Sub = Trunc(r.BodyPreview, 52),
                            Active = i % 2 == 0
                        }).ToList()
                    },
                    new()
                    {
                        Type = "ALERT_CHECKLIST", ColSpan = 4, Title = "Quy tắc bảo mật",
                        Items = targetReqs.Take(4).Select((r, i) => new BlockItem
                        {
                            Label = Trunc(r.Title, 28),
                            Active = i < 3
                        }).ToList()
                    }
                }
            });
            return bp;
        }

        if (ContainsAny(corpus, "biểu đồ", "chart", "thống kê", "analytics", "doanh thu", "báo cáo", "report", "kpi"))
        {
            bp.Shell = "SIDEBAR_TOPBAR";
            bp.HeaderStyle = "SEARCH_FILTERS";
            bp.HeaderItems = new List<string> { "7 ngày", "30 ngày", "Quý này", "Xuất báo cáo" };
            bp.Rows.Add(new LayoutRow
            {
                Blocks = new List<LayoutBlock>
                {
                    new()
                    {
                        Type = "KPI_ROW", ColSpan = 12,
                        Items = new List<BlockItem>
                        {
                            new() { Label = "Tổng lượt xử lý", Value = "24,890", Tag = "+14.2%" },
                            new() { Label = "Tỷ lệ đạt chuẩn", Value = "97.8%", Tag = "Optimal" },
                            new() { Label = "Thời gian TB", Value = "1.4s", Tag = "-18%" }
                        }
                    }
                }
            });
            bp.Rows.Add(new LayoutRow
            {
                Blocks = new List<LayoutBlock>
                {
                    new()
                    {
                        Type = "CHARTS_ANALYTICS", ColSpan = 7, Title = "Biểu đồ hiệu suất theo ngày", Badge = "Realtime",
                        Items = new List<BlockItem>
                        {
                            new() { Label = "T2", Value = "55", Sub = "550" },
                            new() { Label = "T3", Value = "72", Sub = "720" },
                            new() { Label = "T4", Value = "64", Sub = "640" },
                            new() { Label = "T5", Value = "88", Sub = "880" },
                            new() { Label = "T6", Value = "94", Sub = "940" },
                            new() { Label = "T7", Value = "78", Sub = "780" }
                        }
                    },
                    new()
                    {
                        Type = "TIMELINE_FEED", ColSpan = 5, Title = "Điểm nhấn & Cảnh báo",
                        Items = targetReqs.Take(4).Select(r => new BlockItem
                        {
                            Label = Trunc(r.ReqUid, 14),
                            Value = Trunc(r.Title, 30),
                            Sub = "Hôm nay",
                            Tag = "Metric"
                        }).ToList()
                    }
                }
            });
            return bp;
        }

        if (ContainsAny(corpus, "gọi món", "thực đơn", "menu", "cart", "giỏ hàng", "đặt món", "pos", "checkout", "e-commerce", "ecommerce", "sản phẩm", "catalog", "mua hàng", "shop"))
        {
            bp.Shell = "TOPBAR_ONLY";
            bp.HeaderStyle = "SEARCH_FILTERS";
            bp.HeaderItems = new List<string> { "Tất cả", "Món chính", "Đồ uống", "Combo", "Bán chạy" };
            bp.Rows.Add(new LayoutRow
            {
                Blocks = new List<LayoutBlock>
                {
                    new()
                    {
                        Type = "CATALOG_GRID", ColSpan = 8, Title = "Danh mục sản phẩm / Thực đơn",
                        Items = Enumerable.Range(0, 6).Select(i =>
                        {
                            var r = i < targetReqs.Count ? targetReqs[i] : null;
                            return new BlockItem
                            {
                                Label = Trunc(r?.Title ?? $"Mục #{i + 1}", 22),
                                Sub = i < 2 ? "Nổi bật" : "Tiêu chuẩn",
                                Value = $"{(i + 2) * 25}.000đ",
                                Tag = i == 0 ? "Hot" : i == 1 ? "New" : ""
                            };
                        }).ToList()
                    },
                    new()
                    {
                        Type = "CART_SUMMARY", ColSpan = 4, Title = "Đơn hàng hiện tại", Badge = "Bàn #12",
                        Items = targetReqs.Take(3).Select((r, i) => new BlockItem
                        {
                            Label = Trunc(r.Title, 20),
                            Sub = $"x{i + 1}",
                            Value = $"{(i + 1) * 45}.000đ"
                        }).ToList(),
                        TotalLabel = "Tổng thanh toán",
                        TotalValue = "270.000đ"
                    }
                }
            });
            return bp;
        }

        if (ContainsAny(corpus, "đăng nhập", "login", "đăng ký", "register", "auth", "onboarding", "wizard", "step", "bước", "quên mật khẩu", "password"))
        {
            bp.Shell = "SPLIT_DARK_LEFT";
            bp.HeaderStyle = "STEPPER";
            bp.HeaderItems = new List<string> { "Thông tin cơ bản", "Xác thực quy tắc", "Hoàn tất" };
            bp.Rows.Add(new LayoutRow
            {
                Blocks = new List<LayoutBlock>
                {
                    new()
                    {
                        Type = "FORM", ColSpan = 12, Title = Trunc(firstReq?.Title ?? "Thông tin biểu mẫu", 34),
                        Items = targetReqs.Take(4).Select((r, i) => new BlockItem
                        {
                            Label = Trunc(r.ReqUid, 18),
                            Value = Trunc(r.Title, 28),
                            Tag = i == 1 ? "select" : "text"
                        }).ToList(),
                        Note = Trunc(firstReq?.BodyPreview ?? "Vui lòng kiểm tra kỹ thông tin trước khi xác nhận.", 64)
                    }
                }
            });
            return bp;
        }

        if (ContainsAny(corpus, "kanban", "board", "trạng thái", "workflow", "sprint", "pipeline", "task", "bếp", "tiến độ"))
        {
            bp.Shell = "TOPBAR_ONLY";
            bp.HeaderStyle = "BREADCRUMB_ACTIONS";
            bp.Rows.Add(new LayoutRow
            {
                Blocks = new List<LayoutBlock>
                {
                    new()
                    {
                        Type = "KANBAN", ColSpan = 12, Title = "Bảng điều phối quy trình",
                        Columns = new List<string> { "To Do", "In Progress", "Review", "Done" },
                        Items = targetReqs.Take(6).Select((r, i) => new BlockItem
                        {
                            Label = Trunc(r.Title, 26),
                            Sub = Trunc(r.ReqUid, 12),
                            Tag = i % 2 == 0 ? "High" : "Normal"
                        }).ToList()
                    }
                }
            });
            return bp;
        }

        if (ContainsAny(corpus, "gói", "pricing", "plan", "subscription", "bảng giá", "nâng cấp", "tier"))
        {
            bp.Shell = "TOPBAR_ONLY";
            bp.HeaderStyle = "HERO_BANNER";
            bp.Rows.Add(new LayoutRow
            {
                Blocks = new List<LayoutBlock>
                {
                    new()
                    {
                        Type = "PRICING_PLANS", ColSpan = 12, Title = "Các gói dịch vụ & Tính năng",
                        Items = new List<BlockItem>
                        {
                            new() { Label = "Starter", Value = "299.000đ/th", Sub = "5 thành viên • 10GB", Tag = "Basic", Active = false },
                            new() { Label = "Professional", Value = "799.000đ/th", Sub = "Không giới hạn • AI Pro", Tag = "Phổ biến", Active = true },
                            new() { Label = "Enterprise", Value = "Liên hệ", Sub = "SLA 99.9% • Dedicated", Tag = "Custom", Active = false }
                        }
                    }
                }
            });
            return bp;
        }

        if (ContainsAny(corpus, "tải lên", "upload", "import", "nhập file", "excel", "csv", "đính kèm"))
        {
            bp.Shell = "CENTERED_MINIMAL";
            bp.HeaderStyle = "STEPPER";
            bp.HeaderItems = new List<string> { "Chọn tệp", "Kiểm tra dữ liệu", "Hoàn tất" };
            bp.Rows.Add(new LayoutRow
            {
                Blocks = new List<LayoutBlock>
                {
                    new()
                    {
                        Type = "FILE_UPLOADER", ColSpan = 12, Title = "Tải lên & Kiểm tra tệp",
                        Items = targetReqs.Take(3).Select((r, i) => new BlockItem
                        {
                            Label = Trunc($"{r.ReqUid}.xlsx", 26),
                            Value = i == 0 ? "100" : i == 1 ? "68" : "35",
                            Sub = $"{(i + 1) * 1.4:0.0} MB"
                        }).ToList()
                    }
                }
            });
            return bp;
        }

        if (ContainsAny(corpus, "hóa đơn", "invoice", "thanh toán", "payment", "billing", "biên lai", "receipt"))
        {
            bp.Shell = "CENTERED_MINIMAL";
            bp.HeaderStyle = "BREADCRUMB_ACTIONS";
            bp.Rows.Add(new LayoutRow
            {
                Blocks = new List<LayoutBlock>
                {
                    new()
                    {
                        Type = "INVOICE_RECEIPT", ColSpan = 8, Title = "Hóa đơn thanh toán", Badge = "Chờ thanh toán",
                        Items = targetReqs.Take(4).Select((r, i) => new BlockItem
                        {
                            Label = Trunc(r.Title, 28),
                            Sub = $"x{i + 1}",
                            Value = $"{(i + 1) * 120}.000đ"
                        }).ToList(),
                        TotalLabel = "Tổng thanh toán",
                        TotalValue = "1.200.000đ"
                    },
                    new()
                    {
                        Type = "DONUT_BREAKDOWN", ColSpan = 4, Title = "Phân bổ chi phí",
                        Items = new List<BlockItem>
                        {
                            new() { Label = "Dịch vụ chính", Value = "65", Sub = "780.000đ" },
                            new() { Label = "Phụ phí / VAT", Value = "25", Sub = "300.000đ" },
                            new() { Label = "Khác", Value = "10", Sub = "120.000đ" }
                        }
                    }
                }
            });
            return bp;
        }

        if (ContainsAny(corpus, "thông báo", "notification", "inbox", "hộp thư", "cảnh báo", "alert"))
        {
            bp.Shell = "SIDEBAR_TOPBAR";
            bp.HeaderStyle = "SEARCH_FILTERS";
            bp.HeaderItems = new List<string> { "Tất cả", "Chưa đọc", "Hệ thống", "Nhắc việc" };
            bp.Rows.Add(new LayoutRow
            {
                Blocks = new List<LayoutBlock>
                {
                    new()
                    {
                        Type = "NOTIFICATIONS_INBOX", ColSpan = 12, Title = "Trung tâm thông báo",
                        Items = targetReqs.Take(6).Select((r, i) => new BlockItem
                        {
                            Label = Trunc(r.Title, 38),
                            Value = Trunc(r.BodyPreview, 58),
                            Sub = $"{i * 5 + 2} phút trước",
                            Active = i < 3
                        }).ToList()
                    }
                }
            });
            return bp;
        }

        if (ContainsAny(corpus, "so sánh", "compare", "matrix", "ma trận", "đối chiếu"))
        {
            bp.Shell = "TOPBAR_ONLY";
            bp.HeaderStyle = "STANDARD";
            bp.Rows.Add(new LayoutRow
            {
                Blocks = new List<LayoutBlock>
                {
                    new()
                    {
                        Type = "COMPARISON_MATRIX", ColSpan = 12, Title = "Bảng so sánh phương án",
                        Columns = new List<string> { "Tiêu chí", "Phương án A", "Phương án B", "Phương án C" },
                        TableRows = targetReqs.Take(5).Select(r => new List<string> { Trunc(r.Title, 24), "✓", "✓", "—" }).ToList()
                    }
                }
            });
            return bp;
        }

        if (ContainsAny(corpus, "chi tiết", "detail", "hồ sơ", "profile", "audit", "duyệt", "approval", "review"))
        {
            bp.Shell = "SIDEBAR_TOPBAR";
            bp.HeaderStyle = "TABS_BAR";
            bp.HeaderItems = new List<string> { "Tổng quan", "Đặc tả chi tiết", "Lịch sử duyệt", "Tài liệu" };
            bp.Rows.Add(new LayoutRow
            {
                Blocks = new List<LayoutBlock>
                {
                    new()
                    {
                        Type = "DETAIL_KEY_VALUE", ColSpan = 8, Title = "Chi tiết yêu cầu & Thông số",
                        Items = targetReqs.Take(5).Select(r => new BlockItem
                        {
                            Label = Trunc(r.ReqUid, 18),
                            Value = Trunc(r.Title, 36),
                            Tag = "Verified"
                        }).ToList()
                    },
                    new()
                    {
                        Type = "TIMELINE_FEED", ColSpan = 4, Title = "Lịch sử hoạt động",
                        Items = targetReqs.Take(4).Select(r => new BlockItem
                        {
                            Label = Trunc(r.Actor ?? "Analyst", 18),
                            Value = Trunc(r.Title, 28),
                            Sub = "Vừa xong",
                            Tag = "Log"
                        }).ToList()
                    }
                }
            });
            return bp;
        }

        // Default: Balanced Dashboard (KPIs + Form + Table)
        bp.Shell = "SIDEBAR_TOPBAR";
        bp.HeaderStyle = "STANDARD";
        bp.Rows.Add(new LayoutRow
        {
            Blocks = new List<LayoutBlock>
            {
                new()
                {
                    Type = "KPI_ROW", ColSpan = 12,
                    Items = new List<BlockItem>
                    {
                        new() { Label = "Tổng mục", Value = "1,284", Tag = "+8.4%" },
                        new() { Label = "Đang xử lý", Value = "42", Tag = "Live" },
                        new() { Label = "Hoàn thành", Value = "94.2%", Tag = "On Track" }
                    }
                }
            }
        });
        bp.Rows.Add(new LayoutRow
        {
            Blocks = new List<LayoutBlock>
            {
                new()
                {
                    Type = "FORM", ColSpan = 5, Title = Trunc(firstReq?.Title ?? "Thao tác nhanh", 30),
                    Items = targetReqs.Take(4).Select((r, i) => new BlockItem
                    {
                        Label = Trunc(r.ReqUid, 16),
                        Value = Trunc(r.Title, 24),
                        Tag = i == 1 ? "select" : "text"
                    }).ToList(),
                    Note = Trunc(firstReq?.BodyPreview ?? "Kiểm tra điều kiện hợp lệ trước khi lưu.", 52)
                },
                new()
                {
                    Type = "TABLE", ColSpan = 7, Title = "Danh sách bản ghi",
                    Columns = new List<string> { "Mã", "Tiêu đề", "Phụ trách", "Trạng thái" },
                    TableRows = targetReqs.Take(5).Select((r, i) => new List<string>
                    {
                        Trunc(r.ReqUid, 12),
                        Trunc(r.Title, 22),
                        Trunc(r.Actor ?? "System", 12),
                        i % 3 == 0 ? "Active" : i % 3 == 1 ? "Pending" : "Done"
                    }).ToList()
                }
            }
        });
        return bp;
    }

    private static bool ContainsAny(string text, params string[] keywords) =>
        keywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));

    public static string RenderSvg(UiBlueprint bp)
    {
        var sb = new StringBuilder(20480);
        sb.AppendLine("""<svg xmlns="http://www.w3.org/2000/svg" width="1280" height="860" viewBox="0 0 1280 860" fill="none">""");
        sb.AppendLine("""  <rect width="1280" height="860" fill="#F8FAFC"/>""");

        var shell = (bp.Shell ?? "SIDEBAR_TOPBAR").Trim().ToUpperInvariant();
        int cx, cy, cw, cBottom = 832;

        if (shell == "SPLIT_DARK_LEFT")
        {
            DrawSplitDarkLeft(sb, bp);
            cx = 500; cy = 48; cw = 740; cBottom = 812;
        }
        else if (shell == "CENTERED_MINIMAL")
        {
            DrawTopHeader(sb, bp.AppName, "Tìm kiếm...", bp.HeaderStyle);
            cx = 200; cy = 88; cw = 880;
            cy = DrawContentHeader(sb, bp, cx, cy, cw);
        }
        else if (shell == "TOPBAR_ONLY")
        {
            DrawTopHeader(sb, bp.AppName, "Tìm kiếm nhanh (Ctrl+K)...", bp.HeaderStyle);
            cx = 32; cy = 88; cw = 1216;
            cy = DrawContentHeader(sb, bp, cx, cy, cw);
        }
        else // SIDEBAR_TOPBAR
        {
            DrawTopHeader(sb, bp.AppName, "Tìm kiếm nhanh (Ctrl+K)...", bp.HeaderStyle);
            DrawSidebar(sb, bp.NavItems, 228, 64, 796);
            cx = 256; cy = 88; cw = 992;
            cy = DrawContentHeader(sb, bp, cx, cy, cw);
        }

        // Render Composable Rows
        var rows = bp.Rows.Take(3).ToList();
        if (rows.Count == 0)
        {
            rows.Add(new LayoutRow { Blocks = new List<LayoutBlock> { new() { Type = "FORM", ColSpan = 12, Title = bp.ScreenTitle } } });
        }

        const int rowGap = 18;
        int availH = Math.Max(200, cBottom - cy);
        var weights = rows.Select(GetRowWeight).ToList();
        double totalWeight = weights.Sum();
        int curY = cy;

        for (int r = 0; r < rows.Count; r++)
        {
            int rowH = r == rows.Count - 1
                ? Math.Max(90, cBottom - curY)
                : (int)Math.Round((availH - rowGap * (rows.Count - 1)) * (weights[r] / totalWeight));

            var blocks = rows[r].Blocks.Take(3).ToList();
            if (blocks.Count > 0)
            {
                const int colGap = 18;
                int totalSpan = Math.Max(1, blocks.Sum(b => Math.Clamp(b.ColSpan, 1, 12)));
                int curX = cx;
                int usableW = cw - colGap * (blocks.Count - 1);

                for (int b = 0; b < blocks.Count; b++)
                {
                    int span = Math.Clamp(blocks[b].ColSpan, 1, 12);
                    int bw = b == blocks.Count - 1
                        ? (cx + cw) - curX
                        : (int)Math.Round(usableW * (span / (double)totalSpan));

                    RenderBlock(sb, bp, blocks[b], curX, curY, bw, rowH);
                    curX += bw + colGap;
                }
            }

            curY += rowH + rowGap;
        }

        sb.AppendLine("</svg>");
        return sb.ToString();
    }

    private static double GetRowWeight(LayoutRow row)
    {
        if (row.Blocks.Count == 0) return 1.0;
        var max = row.Blocks.Max(b => (b.Type ?? "").ToUpperInvariant() switch
        {
            "KPI_ROW" or "PROFILE_HEADER" => 1.2,
            "ALERT_CHECKLIST" => 1.8,
            "KANBAN" or "CHAT_THREAD" or "CATALOG_GRID" or "CALENDAR_SCHEDULE" or "PRICING_PLANS" or "MEDIA_GALLERY" or "LANDING_HERO" => 4.2,
            _ => 3.0
        });
        return max;
    }

    private static int DrawContentHeader(StringBuilder sb, UiBlueprint bp, int x, int y, int w)
    {
        sb.AppendLine($"""  <text x="{x}" y="{y + 18}" font-family="Inter, sans-serif" font-size="22" font-weight="700" fill="#0F172A">{Esc(Trunc(bp.ScreenTitle, 48))}</text>""");
        sb.AppendLine($"""  <text x="{x}" y="{y + 42}" font-family="Inter, sans-serif" font-size="13" fill="#64748B">{Esc(Trunc(bp.Subtitle, 78))}</text>""");

        var style = (bp.HeaderStyle ?? "STANDARD").Trim().ToUpperInvariant();
        if (style == "STANDARD") return y + 60;

        int hy = y + 58;
        if (style == "SEARCH_FILTERS")
        {
            var pills = bp.HeaderItems.Take(5).ToList();
            if (pills.Count == 0) pills = new List<string> { "Tất cả", "Đang mở", "Hoàn tất" };
            int px = x;
            for (int i = 0; i < pills.Count; i++)
            {
                bool active = i == 0;
                int pw = Math.Clamp(pills[i].Length * 8 + 30, 76, 146);
                sb.AppendLine($"""  <rect x="{px}" y="{hy}" width="{pw}" height="32" rx="16" fill="{(active ? "#2563EB" : "#FFFFFF")}" stroke="{(active ? "#2563EB" : "#E2E8F0")}"/>""");
                sb.AppendLine($"""  <text x="{px + pw / 2}" y="{hy + 20}" text-anchor="middle" font-family="Inter, sans-serif" font-size="12" font-weight="600" fill="{(active ? "#FFFFFF" : "#475569")}">{Esc(Trunc(pills[i], 14))}</text>""");
                px += pw + 10;
            }
            return hy + 48;
        }

        if (style == "TABS_BAR")
        {
            var tabs = bp.HeaderItems.Take(5).ToList();
            if (tabs.Count == 0) tabs = new List<string> { "Overview", "Details", "History" };
            sb.AppendLine($"""  <line x1="{x}" y1="{hy + 32}" x2="{x + w}" y2="{hy + 32}" stroke="#E2E8F0"/>""");
            int tx = x;
            for (int i = 0; i < tabs.Count; i++)
            {
                bool active = i == 0;
                sb.AppendLine($"""  <text x="{tx}" y="{hy + 20}" font-family="Inter, sans-serif" font-size="13" font-weight="{(active ? "700" : "500")}" fill="{(active ? "#2563EB" : "#64748B")}">{Esc(Trunc(tabs[i], 18))}</text>""");
                if (active) sb.AppendLine($"""  <rect x="{tx}" y="{hy + 30}" width="78" height="3" rx="1.5" fill="#2563EB"/>""");
                tx += 126;
            }
            return hy + 48;
        }

        if (style == "STEPPER")
        {
            var steps = bp.HeaderItems.Take(4).ToList();
            if (steps.Count == 0) steps = new List<string> { "Step 1", "Step 2", "Step 3" };
            int sx = x;
            for (int i = 0; i < steps.Count; i++)
            {
                bool active = i == 0;
                sb.AppendLine($"""  <circle cx="{sx + 14}" cy="{hy + 16}" r="13" fill="{(active ? "#2563EB" : "#E2E8F0")}"/>""");
                sb.AppendLine($"""  <text x="{sx + 14}" y="{hy + 20}" text-anchor="middle" font-family="Inter, sans-serif" font-size="11" font-weight="700" fill="{(active ? "#FFFFFF" : "#475569")}">{i + 1}</text>""");
                sb.AppendLine($"""  <text x="{sx + 34}" y="{hy + 20}" font-family="Inter, sans-serif" font-size="13" font-weight="{(active ? "700" : "500")}" fill="{(active ? "#0F172A" : "#64748B")}">{Esc(Trunc(steps[i], 18))}</text>""");
                sx += 200;
            }
            return hy + 48;
        }

        if (style == "HERO_BANNER")
        {
            sb.AppendLine($"""  <rect x="{x}" y="{hy}" width="{w}" height="44" rx="10" fill="#EFF6FF" stroke="#BFDBFE"/>""");
            sb.AppendLine($"""  <text x="{x + 16}" y="{hy + 27}" font-family="Inter, sans-serif" font-size="13" font-weight="600" fill="#1E40AF">{Esc(Trunc(bp.Subtitle, 62))}</text>""");
            DrawButton(sb, x + w - 148, hy + 6, 136, 32, bp.PrimaryButton, true);
            return hy + 58;
        }

        // BREADCRUMB_ACTIONS: put buttons top-right of header
        DrawButton(sb, x + w - 284, y + 6, 124, 36, bp.SecondaryButton, false);
        DrawButton(sb, x + w - 146, y + 6, 146, 36, bp.PrimaryButton, true);
        return y + 60;
    }

    private static void DrawSplitDarkLeft(StringBuilder sb, UiBlueprint bp)
    {
        sb.AppendLine("""  <rect width="440" height="860" fill="#0F172A"/>""");
        sb.AppendLine("""  <rect x="40" y="42" width="36" height="36" rx="9" fill="#2563EB"/>""");
        sb.AppendLine($"""  <text x="58" y="66" text-anchor="middle" font-family="Inter, sans-serif" font-size="16" font-weight="700" fill="#FFFFFF">{Esc(bp.AppName.Length > 0 ? bp.AppName[..1].ToUpperInvariant() : "A")}</text>""");
        sb.AppendLine($"""  <text x="90" y="66" font-family="Inter, sans-serif" font-size="17" font-weight="700" fill="#FFFFFF">{Esc(Trunc(bp.AppName, 22))}</text>""");
        sb.AppendLine($"""  <text x="40" y="150" font-family="Inter, sans-serif" font-size="25" font-weight="700" fill="#FFFFFF">{Esc(Trunc(bp.ScreenTitle, 28))}</text>""");
        sb.AppendLine($"""  <text x="40" y="182" font-family="Inter, sans-serif" font-size="13" fill="#94A3B8">{Esc(Trunc(bp.Subtitle, 48))}</text>""");

        var steps = bp.HeaderItems.Take(4).ToList();
        if (steps.Count == 0) steps = new List<string> { "Thông tin cơ bản", "Xác thực quy tắc", "Hoàn tất" };
        for (int i = 0; i < steps.Count; i++)
        {
            int sy = 256 + i * 74;
            bool active = i == 0;
            sb.AppendLine($"""  <circle cx="58" cy="{sy}" r="15" fill="{(active ? "#2563EB" : "#1E293B")}" stroke="{(active ? "#60A5FA" : "#334155")}"/>""");
            sb.AppendLine($"""  <text x="58" y="{sy + 5}" text-anchor="middle" font-family="Inter, sans-serif" font-size="12" font-weight="700" fill="#FFFFFF">{i + 1}</text>""");
            sb.AppendLine($"""  <text x="88" y="{sy + 4}" font-family="Inter, sans-serif" font-size="14" font-weight="600" fill="{(active ? "#FFFFFF" : "#94A3B8")}">{Esc(Trunc(steps[i], 28))}</text>""");
            if (i < steps.Count - 1)
                sb.AppendLine($"""  <line x1="58" y1="{sy + 19}" x2="58" y2="{sy + 55}" stroke="#334155" stroke-width="2"/>""");
        }
    }

    // -------------------------------------------------------------------------
    // BLOCK RENDERERS (14 DISTINCT COMPOSABLE BLOCKS)
    // -------------------------------------------------------------------------
    private static void RenderBlock(StringBuilder sb, UiBlueprint bp, LayoutBlock block, int x, int y, int w, int h)
    {
        var type = (block.Type ?? "FORM").Trim().ToUpperInvariant();
        if (type == "KPI_ROW")
        {
            RenderKpiRow(sb, block, x, y, w, h);
            return;
        }

        DrawCard(sb, x, y, w, h);
        if (!string.IsNullOrWhiteSpace(block.Title))
        {
            sb.AppendLine($"""  <text x="{x + 20}" y="{y + 30}" font-family="Inter, sans-serif" font-size="15" font-weight="700" fill="#0F172A">{Esc(Trunc(block.Title, 38))}</text>""");
        }
        if (!string.IsNullOrWhiteSpace(block.Badge))
        {
            DrawStatusPill(sb, x + w - 98, y + 14, block.Badge, 0);
        }

        switch (type)
        {
            case "FORM": RenderFormBlock(sb, bp, block, x, y, w, h); break;
            case "TABLE": RenderTableBlock(sb, block, x, y, w, h); break;
            case "CATALOG_GRID": RenderCatalogGrid(sb, block, x, y, w, h); break;
            case "CART_SUMMARY": RenderCartSummary(sb, bp, block, x, y, w, h); break;
            case "KANBAN": RenderKanbanBlock(sb, block, x, y, w, h); break;
            case "CHAT_THREAD": RenderChatBlock(sb, block, x, y, w, h); break;
            case "CALENDAR_SCHEDULE": RenderCalendarBlock(sb, block, x, y, w, h); break;
            case "CHARTS_ANALYTICS": RenderChartsBlock(sb, block, x, y, w, h); break;
            case "TIMELINE_FEED": RenderTimelineBlock(sb, block, x, y, w, h); break;
            case "SETTINGS_TOGGLES": RenderSettingsBlock(sb, block, x, y, w, h); break;
            case "DETAIL_KEY_VALUE": RenderDetailKvBlock(sb, block, x, y, w, h); break;
            case "ALERT_CHECKLIST": RenderChecklistBlock(sb, block, x, y, w, h); break;
            case "MAP_TRACKING": RenderMapBlock(sb, block, x, y, w, h); break;
            case "PRICING_PLANS": RenderPricingPlansBlock(sb, block, x, y, w, h); break;
            case "LANDING_HERO": RenderLandingHeroBlock(sb, bp, block, x, y, w, h); break;
            case "PROFILE_HEADER": RenderProfileHeaderBlock(sb, bp, block, x, y, w, h); break;
            case "MEDIA_GALLERY": RenderMediaGalleryBlock(sb, block, x, y, w, h); break;
            case "FILE_UPLOADER": RenderFileUploaderBlock(sb, block, x, y, w, h); break;
            case "NOTIFICATIONS_INBOX": RenderNotificationsBlock(sb, block, x, y, w, h); break;
            case "INVOICE_RECEIPT": RenderInvoiceReceiptBlock(sb, bp, block, x, y, w, h); break;
            case "DONUT_BREAKDOWN": RenderDonutBreakdownBlock(sb, block, x, y, w, h); break;
            case "COMPARISON_MATRIX": RenderComparisonMatrixBlock(sb, block, x, y, w, h); break;
            case "EMPTY_OR_CONFIRM_MODAL": RenderConfirmModalBlock(sb, bp, block, x, y, w, h); break;
            default: RenderDetailKvBlock(sb, block, x, y, w, h); break;
        }
    }

    private static void RenderKpiRow(StringBuilder sb, LayoutBlock block, int x, int y, int w, int h)
    {
        var items = block.Items.Take(4).ToList();
        if (items.Count == 0) items = new List<BlockItem> { new() { Label = "Total", Value = "1,240", Tag = "+12%" }, new() { Label = "Active", Value = "86", Tag = "Live" }, new() { Label = "Done", Value = "94%", Tag = "OK" } };
        const int gap = 16;
        int cardW = (w - gap * (items.Count - 1)) / items.Count;
        int cardH = Math.Min(h, 96);
        for (int i = 0; i < items.Count; i++)
        {
            int kx = x + i * (cardW + gap);
            DrawCard(sb, kx, y, cardW, cardH);
            sb.AppendLine($"""  <text x="{kx + 18}" y="{y + 28}" font-family="Inter, sans-serif" font-size="12" font-weight="500" fill="#64748B">{Esc(Trunc(items[i].Label, 24))}</text>""");
            sb.AppendLine($"""  <text x="{kx + 18}" y="{y + 66}" font-family="Inter, sans-serif" font-size="24" font-weight="700" fill="#0F172A">{Esc(Trunc(items[i].Value, 16))}</text>""");
            if (!string.IsNullOrWhiteSpace(items[i].Tag))
                DrawStatusPill(sb, kx + cardW - 88, y + 44, items[i].Tag, 2);
        }
    }

    private static void RenderFormBlock(StringBuilder sb, UiBlueprint bp, LayoutBlock block, int x, int y, int w, int h)
    {
        int fy = y + 54;
        int maxFields = Math.Clamp((h - 160) / 72, 1, 5);
        var items = block.Items.Take(maxFields).ToList();
        for (int i = 0; i < items.Count; i++)
        {
            DrawField(sb, x + 20, fy + i * 72, w - 40, items[i].Label, items[i].Value, items[i].Tag);
        }

        if (!string.IsNullOrWhiteSpace(block.Note) && h > 220)
            DrawAlertBanner(sb, x + 20, y + h - 114, w - 40, block.Note);

        int btnY = y + h - 56;
        int secW = Math.Min(130, (w - 54) / 3);
        DrawButton(sb, x + 20, btnY, secW, 38, bp.SecondaryButton, false);
        DrawButton(sb, x + 32 + secW, btnY, w - 52 - secW, 38, bp.PrimaryButton, true);
    }

    private static void RenderTableBlock(StringBuilder sb, LayoutBlock block, int x, int y, int w, int h)
    {
        var cols = block.Columns.Take(4).ToList();
        while (cols.Count < 4) cols.Add($"Col {cols.Count + 1}");
        int ty = y + 52;
        int tw = w - 40;
        sb.AppendLine($"""  <rect x="{x + 20}" y="{ty}" width="{tw}" height="36" rx="6" fill="#F8FAFC"/>""");
        int cw = tw / 4;
        for (int c = 0; c < 4; c++)
            sb.AppendLine($"""  <text x="{x + 32 + c * cw}" y="{ty + 23}" font-family="Inter, sans-serif" font-size="12" font-weight="700" fill="#475569">{Esc(Trunc(cols[c], 14))}</text>""");

        int maxRows = Math.Clamp((h - 108) / 50, 1, 8);
        var rows = block.TableRows.Take(maxRows).ToList();
        for (int r = 0; r < rows.Count; r++)
        {
            int ry = ty + 44 + r * 50;
            sb.AppendLine($"""  <line x1="{x + 20}" y1="{ry + 40}" x2="{x + 20 + tw}" y2="{ry + 40}" stroke="#F1F5F9"/>""");
            for (int c = 0; c < 4; c++)
            {
                var cell = c < rows[r].Count ? rows[r][c] : "—";
                if (c == 3)
                    DrawStatusPill(sb, x + 32 + c * cw, ry + 8, cell, r);
                else
                    sb.AppendLine($"""  <text x="{x + 32 + c * cw}" y="{ry + 24}" font-family="Inter, sans-serif" font-size="13" font-weight="{(c == 0 ? "600" : "400")}" fill="{(c == 0 ? "#2563EB" : "#0F172A")}">{Esc(Trunc(cell, 16))}</text>""");
            }
        }
    }

    private static void RenderCatalogGrid(StringBuilder sb, LayoutBlock block, int x, int y, int w, int h)
    {
        var items = block.Items.Take(6).ToList();
        int cols = w >= 680 ? 3 : 2;
        int rows = Math.Max(1, (int)Math.Ceiling(items.Count / (double)cols));
        int gap = 14;
        int cw = (w - 40 - gap * (cols - 1)) / cols;
        int ch = Math.Min(260, (h - 68 - gap * (rows - 1)) / rows);

        for (int i = 0; i < items.Count; i++)
        {
            int c = i % cols, r = i / cols;
            int ix = x + 20 + c * (cw + gap);
            int iy = y + 50 + r * (ch + gap);
            sb.AppendLine($"""  <rect x="{ix}" y="{iy}" width="{cw}" height="{ch}" rx="10" fill="#F8FAFC" stroke="#E2E8F0"/>""");
            int imgH = Math.Max(56, ch - 116);
            sb.AppendLine($"""  <rect x="{ix + 10}" y="{iy + 10}" width="{cw - 20}" height="{imgH}" rx="6" fill="#E2E8F0"/>""");
            if (!string.IsNullOrWhiteSpace(items[i].Tag))
                DrawStatusPill(sb, ix + 18, iy + 18, items[i].Tag, 1);
            sb.AppendLine($"""  <text x="{ix + 12}" y="{iy + imgH + 30}" font-family="Inter, sans-serif" font-size="11" fill="#64748B">{Esc(Trunc(items[i].Sub, 18))}</text>""");
            sb.AppendLine($"""  <text x="{ix + 12}" y="{iy + imgH + 50}" font-family="Inter, sans-serif" font-size="14" font-weight="700" fill="#0F172A">{Esc(Trunc(items[i].Label, 20))}</text>""");
            sb.AppendLine($"""  <text x="{ix + 12}" y="{iy + imgH + 74}" font-family="Inter, sans-serif" font-size="14" font-weight="700" fill="#2563EB">{Esc(Trunc(items[i].Value, 14))}</text>""");
            DrawButton(sb, ix + 12, iy + ch - 38, cw - 24, 28, "+ Chọn", false);
        }
    }

    private static void RenderCartSummary(StringBuilder sb, UiBlueprint bp, LayoutBlock block, int x, int y, int w, int h)
    {
        int cy = y + 54;
        int maxItems = Math.Clamp((h - 190) / 58, 1, 5);
        foreach (var item in block.Items.Take(maxItems))
        {
            sb.AppendLine($"""  <rect x="{x + 20}" y="{cy}" width="{w - 40}" height="50" rx="8" fill="#F8FAFC" stroke="#E2E8F0"/>""");
            sb.AppendLine($"""  <text x="{x + 32}" y="{cy + 21}" font-family="Inter, sans-serif" font-size="13" font-weight="600" fill="#0F172A">{Esc(Trunc(item.Label, 22))}</text>""");
            sb.AppendLine($"""  <text x="{x + 32}" y="{cy + 39}" font-family="Inter, sans-serif" font-size="11" fill="#64748B">{Esc(item.Sub)}</text>""");
            sb.AppendLine($"""  <text x="{x + w - 32}" y="{cy + 30}" text-anchor="end" font-family="Inter, sans-serif" font-size="13" font-weight="700" fill="#0F172A">{Esc(item.Value)}</text>""");
            cy += 58;
        }

        sb.AppendLine($"""  <line x1="{x + 20}" y1="{y + h - 114}" x2="{x + w - 20}" y2="{y + h - 114}" stroke="#E2E8F0"/>""");
        sb.AppendLine($"""  <text x="{x + 20}" y="{y + h - 84}" font-family="Inter, sans-serif" font-size="14" font-weight="700" fill="#0F172A">{Esc(string.IsNullOrWhiteSpace(block.TotalLabel) ? "Tổng cộng" : block.TotalLabel)}</text>""");
        sb.AppendLine($"""  <text x="{x + w - 20}" y="{y + h - 84}" text-anchor="end" font-family="Inter, sans-serif" font-size="18" font-weight="700" fill="#2563EB">{Esc(string.IsNullOrWhiteSpace(block.TotalValue) ? "250.000đ" : block.TotalValue)}</text>""");
        DrawButton(sb, x + 20, y + h - 56, w - 40, 38, bp.PrimaryButton, true);
    }

    private static void RenderKanbanBlock(StringBuilder sb, LayoutBlock block, int x, int y, int w, int h)
    {
        var cols = block.Columns.Take(4).ToList();
        if (cols.Count == 0) cols = new List<string> { "To Do", "In Progress", "Review", "Done" };
        int gap = 14;
        int cw = (w - 40 - gap * (cols.Count - 1)) / cols.Count;
        int colH = h - 68;

        for (int c = 0; c < cols.Count; c++)
        {
            int kx = x + 20 + c * (cw + gap);
            int ky = y + 48;
            sb.AppendLine($"""  <rect x="{kx}" y="{ky}" width="{cw}" height="{colH}" rx="10" fill="#F1F5F9"/>""");
            sb.AppendLine($"""  <text x="{kx + 14}" y="{ky + 26}" font-family="Inter, sans-serif" font-size="13" font-weight="700" fill="#0F172A">{Esc(Trunc(cols[c], 16))}</text>""");

            var colCards = block.Items.Where((_, idx) => idx % cols.Count == c).Take(3).ToList();
            for (int k = 0; k < colCards.Count; k++)
            {
                int cardY = ky + 42 + k * 116;
                if (cardY + 104 > ky + colH) break;
                DrawCard(sb, kx + 10, cardY, cw - 20, 104);
                sb.AppendLine($"""  <text x="{kx + 22}" y="{cardY + 24}" font-family="Inter, sans-serif" font-size="11" font-weight="700" fill="#2563EB">{Esc(Trunc(colCards[k].Sub, 14))}</text>""");
                DrawStatusPill(sb, kx + cw - 92, cardY + 10, colCards[k].Tag, c);
                sb.AppendLine($"""  <text x="{kx + 22}" y="{cardY + 54}" font-family="Inter, sans-serif" font-size="13" font-weight="600" fill="#0F172A">{Esc(Trunc(colCards[k].Label, 22))}</text>""");
                sb.AppendLine($"""  <circle cx="{kx + 32}" cy="{cardY + 82}" r="10" fill="#DBEAFE"/>""");
            }
        }
    }

    private static void RenderChatBlock(StringBuilder sb, LayoutBlock block, int x, int y, int w, int h)
    {
        int listW = w >= 620 ? 220 : 0;
        if (listW > 0)
        {
            sb.AppendLine($"""  <line x1="{x + listW}" y1="{y + 46}" x2="{x + listW}" y2="{y + h}" stroke="#E2E8F0"/>""");
            for (int i = 0; i < 4; i++)
            {
                int ly = y + 54 + i * 62;
                if (ly + 52 > y + h) break;
                sb.AppendLine($"""  <rect x="{x + 12}" y="{ly}" width="{listW - 24}" height="52" rx="8" fill="{(i == 0 ? "#EFF6FF" : "#FFFFFF")}"/>""");
                sb.AppendLine($"""  <circle cx="{x + 34}" cy="{ly + 26}" r="14" fill="#DBEAFE"/>""");
                sb.AppendLine($"""  <text x="{x + 56}" y="{ly + 23}" font-family="Inter, sans-serif" font-size="12" font-weight="700" fill="#0F172A">Thread #{i + 1}</text>""");
                sb.AppendLine($"""  <text x="{x + 56}" y="{ly + 40}" font-family="Inter, sans-serif" font-size="11" fill="#64748B">Active conversation</text>""");
            }
        }

        int msgX = x + listW + 20;
        int msgW = w - listW - 40;
        int my = y + 56;
        foreach (var msg in block.Items.Take(4))
        {
            if (my + 66 > y + h - 68) break;
            int bw = Math.Min(msgW - 60, 360);
            int bx = msg.Active ? msgX + msgW - bw : msgX;
            sb.AppendLine($"""  <rect x="{bx}" y="{my}" width="{bw}" height="56" rx="10" fill="{(msg.Active ? "#2563EB" : "#F1F5F9")}"/>""");
            sb.AppendLine($"""  <text x="{bx + 14}" y="{my + 22}" font-family="Inter, sans-serif" font-size="11" font-weight="700" fill="{(msg.Active ? "#BFDBFE" : "#64748B")}">{Esc(Trunc(msg.Label, 20))} · {Esc(msg.Sub)}</text>""");
            sb.AppendLine($"""  <text x="{bx + 14}" y="{my + 42}" font-family="Inter, sans-serif" font-size="13" fill="{(msg.Active ? "#FFFFFF" : "#0F172A")}">{Esc(Trunc(msg.Value, 36))}</text>""");
            my += 70;
        }

        sb.AppendLine($"""  <rect x="{msgX}" y="{y + h - 54}" width="{msgW - 96}" height="38" rx="8" fill="#F8FAFC" stroke="#CBD5E1"/>""");
        sb.AppendLine($"""  <text x="{msgX + 14}" y="{y + h - 30}" font-family="Inter, sans-serif" font-size="12" fill="#94A3B8">Nhập tin nhắn...</text>""");
        DrawButton(sb, msgX + msgW - 86, y + h - 54, 86, 38, "Gửi", true);
    }

    private static void RenderCalendarBlock(StringBuilder sb, LayoutBlock block, int x, int y, int w, int h)
    {
        var days = block.Columns.Take(5).ToList();
        if (days.Count == 0) days = new List<string> { "Mon", "Tue", "Wed", "Thu", "Fri" };
        int cw = (w - 40) / days.Count;
        int gy = y + 52;
        int gh = h - 72;

        for (int d = 0; d < days.Count; d++)
        {
            int dx = x + 20 + d * cw;
            sb.AppendLine($"""  <rect x="{dx}" y="{gy}" width="{cw - 6}" height="32" rx="6" fill="#F1F5F9"/>""");
            sb.AppendLine($"""  <text x="{dx + (cw - 6) / 2}" y="{gy + 21}" text-anchor="middle" font-family="Inter, sans-serif" font-size="12" font-weight="700" fill="#334155">{Esc(Trunc(days[d], 10))}</text>""");
            sb.AppendLine($"""  <rect x="{dx}" y="{gy + 38}" width="{cw - 6}" height="{gh - 38}" rx="6" fill="#F8FAFC" stroke="#E2E8F0"/>""");
        }

        var events = block.Items.Take(5).ToList();
        for (int i = 0; i < events.Count; i++)
        {
            int d = i % days.Count;
            int ex = x + 24 + d * cw;
            int ey = gy + 48 + (i % 3) * 76;
            if (ey + 64 > gy + gh) continue;
            sb.AppendLine($"""  <rect x="{ex}" y="{ey}" width="{cw - 14}" height="64" rx="8" fill="#EFF6FF" stroke="#93C5FD"/>""");
            sb.AppendLine($"""  <text x="{ex + 10}" y="{ey + 22}" font-family="Inter, sans-serif" font-size="11" font-weight="700" fill="#1D4ED8">{Esc(Trunc(events[i].Sub, 14))}</text>""");
            sb.AppendLine($"""  <text x="{ex + 10}" y="{ey + 42}" font-family="Inter, sans-serif" font-size="12" font-weight="600" fill="#0F172A">{Esc(Trunc(events[i].Label, 14))}</text>""");
        }
    }

    private static void RenderChartsBlock(StringBuilder sb, LayoutBlock block, int x, int y, int w, int h)
    {
        var bars = block.Items.Take(7).ToList();
        if (bars.Count == 0) bars = new List<BlockItem> { new() { Label = "M", Value = "60" }, new() { Label = "T", Value = "80" }, new() { Label = "W", Value = "45" }, new() { Label = "T", Value = "90" }, new() { Label = "F", Value = "75" } };
        int chartX = x + 28, chartY = y + 60, chartW = w - 56, chartH = h - 104;
        sb.AppendLine($"""  <line x1="{chartX}" y1="{chartY + chartH}" x2="{chartX + chartW}" y2="{chartY + chartH}" stroke="#CBD5E1"/>""");
        int slotW = chartW / bars.Count;
        int barW = Math.Min(42, slotW - 16);

        for (int i = 0; i < bars.Count; i++)
        {
            int pct = int.TryParse(bars[i].Value, out var v) ? Math.Clamp(v, 10, 100) : 60;
            int bh = (int)(chartH * (pct / 100.0));
            int bx = chartX + i * slotW + (slotW - barW) / 2;
            int by = chartY + chartH - bh;
            sb.AppendLine($"""  <rect x="{bx}" y="{by}" width="{barW}" height="{bh}" rx="6" fill="#2563EB"/>""");
            sb.AppendLine($"""  <text x="{bx + barW / 2}" y="{chartY + chartH + 20}" text-anchor="middle" font-family="Inter, sans-serif" font-size="11" fill="#64748B">{Esc(Trunc(bars[i].Label, 8))}</text>""");
        }
    }

    private static void RenderTimelineBlock(StringBuilder sb, LayoutBlock block, int x, int y, int w, int h)
    {
        int ty = y + 58;
        int max = Math.Clamp((h - 70) / 64, 1, 6);
        foreach (var item in block.Items.Take(max))
        {
            sb.AppendLine($"""  <circle cx="{x + 30}" cy="{ty + 8}" r="6" fill="#2563EB"/>""");
            sb.AppendLine($"""  <text x="{x + 48}" y="{ty + 12}" font-family="Inter, sans-serif" font-size="13" font-weight="600" fill="#0F172A">{Esc(Trunc(item.Label, 22))}</text>""");
            sb.AppendLine($"""  <text x="{x + w - 20}" y="{ty + 12}" text-anchor="end" font-family="Inter, sans-serif" font-size="11" fill="#94A3B8">{Esc(Trunc(item.Sub, 12))}</text>""");
            sb.AppendLine($"""  <text x="{x + 48}" y="{ty + 32}" font-family="Inter, sans-serif" font-size="12" fill="#64748B">{Esc(Trunc(item.Value, 38))}</text>""");
            ty += 64;
        }
    }

    private static void RenderSettingsBlock(StringBuilder sb, LayoutBlock block, int x, int y, int w, int h)
    {
        int sy = y + 54;
        int max = Math.Clamp((h - 70) / 68, 1, 6);
        foreach (var item in block.Items.Take(max))
        {
            sb.AppendLine($"""  <line x1="{x + 20}" y1="{sy + 58}" x2="{x + w - 20}" y2="{sy + 58}" stroke="#F1F5F9"/>""");
            sb.AppendLine($"""  <text x="{x + 20}" y="{sy + 20}" font-family="Inter, sans-serif" font-size="14" font-weight="600" fill="#0F172A">{Esc(Trunc(item.Label, 36))}</text>""");
            sb.AppendLine($"""  <text x="{x + 20}" y="{sy + 40}" font-family="Inter, sans-serif" font-size="12" fill="#64748B">{Esc(Trunc(item.Sub, 56))}</text>""");
            int tx = x + w - 64;
            sb.AppendLine($"""  <rect x="{tx}" y="{sy + 14}" width="44" height="24" rx="12" fill="{(item.Active ? "#2563EB" : "#CBD5E1")}"/>""");
            sb.AppendLine($"""  <circle cx="{(item.Active ? tx + 32 : tx + 12)}" cy="{sy + 26}" r="9" fill="#FFFFFF"/>""");
            sy += 68;
        }
    }

    private static void RenderDetailKvBlock(StringBuilder sb, LayoutBlock block, int x, int y, int w, int h)
    {
        int ky = y + 54;
        int max = Math.Clamp((h - 70) / 56, 1, 7);
        foreach (var item in block.Items.Take(max))
        {
            sb.AppendLine($"""  <rect x="{x + 20}" y="{ky}" width="{w - 40}" height="46" rx="8" fill="#F8FAFC" stroke="#E2E8F0"/>""");
            sb.AppendLine($"""  <text x="{x + 34}" y="{ky + 28}" font-family="Inter, sans-serif" font-size="12" font-weight="600" fill="#64748B">{Esc(Trunc(item.Label, 20))}</text>""");
            sb.AppendLine($"""  <text x="{x + 190}" y="{ky + 28}" font-family="Inter, sans-serif" font-size="13" font-weight="600" fill="#0F172A">{Esc(Trunc(item.Value, 34))}</text>""");
            if (!string.IsNullOrWhiteSpace(item.Tag))
                DrawStatusPill(sb, x + w - 106, ky + 11, item.Tag, 2);
            ky += 56;
        }
    }

    private static void RenderChecklistBlock(StringBuilder sb, LayoutBlock block, int x, int y, int w, int h)
    {
        int cy = y + 54;
        int max = Math.Clamp((h - 70) / 48, 1, 7);
        foreach (var item in block.Items.Take(max))
        {
            sb.AppendLine($"""  <rect x="{x + 20}" y="{cy + 6}" width="18" height="18" rx="4" fill="{(item.Active ? "#2563EB" : "#FFFFFF")}" stroke="{(item.Active ? "#2563EB" : "#CBD5E1")}"/>""");
            sb.AppendLine($"""  <text x="{x + 48}" y="{cy + 20}" font-family="Inter, sans-serif" font-size="13" fill="#0F172A">{Esc(Trunc(item.Label, 40))}</text>""");
            cy += 48;
        }
    }

    private static void RenderMapBlock(StringBuilder sb, LayoutBlock block, int x, int y, int w, int h)
    {
        int mx = x + 20, my = y + 50, mw = w - 40, mh = h - 70;
        sb.AppendLine($"""  <rect x="{mx}" y="{my}" width="{mw}" height="{mh}" rx="10" fill="#E2E8F0"/>""");
        // Stylized roads
        sb.AppendLine($"""  <line x1="{mx}" y1="{my + mh / 3}" x2="{mx + mw}" y2="{my + mh / 2}" stroke="#FFFFFF" stroke-width="6"/>""");
        sb.AppendLine($"""  <line x1="{mx + mw / 3}" y1="{my}" x2="{mx + mw / 2}" y2="{my + mh}" stroke="#FFFFFF" stroke-width="6"/>""");
        // Route line & pins
        sb.AppendLine($"""  <line x1="{mx + 90}" y1="{my + mh - 90}" x2="{mx + mw - 100}" y2="{my + 90}" stroke="#2563EB" stroke-width="4" stroke-dasharray="8 6"/>""");
        sb.AppendLine($"""  <circle cx="{mx + 90}" cy="{my + mh - 90}" r="12" fill="#2563EB" stroke="#FFFFFF" stroke-width="3"/>""");
        sb.AppendLine($"""  <circle cx="{mx + mw - 100}" cy="{my + 90}" r="12" fill="#16A34A" stroke="#FFFFFF" stroke-width="3"/>""");
        if (block.Items.Count > 0)
        {
            sb.AppendLine($"""  <rect x="{mx + 16}" y="{my + 16}" width="240" height="54" rx="8" fill="#FFFFFF" stroke="#CBD5E1"/>""");
            sb.AppendLine($"""  <text x="{mx + 28}" y="{my + 38}" font-family="Inter, sans-serif" font-size="12" font-weight="700" fill="#0F172A">{Esc(Trunc(block.Items[0].Label, 24))}</text>""");
            sb.AppendLine($"""  <text x="{mx + 28}" y="{my + 56}" font-family="Inter, sans-serif" font-size="11" fill="#2563EB">{Esc(Trunc(block.Items[0].Sub, 24))}</text>""");
        }
    }

    private static void RenderPricingPlansBlock(StringBuilder sb, LayoutBlock block, int x, int y, int w, int h)
    {
        var plans = block.Items.Take(3).ToList();
        if (plans.Count == 0)
        {
            plans = new List<BlockItem>
            {
                new() { Label = "Basic", Value = "199.000đ", Sub = "Core features", Tag = "Starter" },
                new() { Label = "Pro", Value = "499.000đ", Sub = "Unlimited + AI", Tag = "Popular", Active = true },
                new() { Label = "Enterprise", Value = "Custom", Sub = "Dedicated SLA", Tag = "Scale" }
            };
        }
        int gap = 18;
        int cw = (w - 40 - gap * (plans.Count - 1)) / plans.Count;
        int ch = h - 72;
        for (int i = 0; i < plans.Count; i++)
        {
            int px = x + 20 + i * (cw + gap);
            int py = y + 52;
            bool rec = plans[i].Active;
            sb.AppendLine($"""  <rect x="{px}" y="{py}" width="{cw}" height="{ch}" rx="12" fill="{(rec ? "#EFF6FF" : "#F8FAFC")}" stroke="{(rec ? "#2563EB" : "#E2E8F0")}" stroke-width="{(rec ? "2" : "1")}"/>""");
            if (!string.IsNullOrWhiteSpace(plans[i].Tag))
                DrawStatusPill(sb, px + cw - 92, py + 16, plans[i].Tag, rec ? 0 : 3);
            sb.AppendLine($"""  <text x="{px + 20}" y="{py + 36}" font-family="Inter, sans-serif" font-size="16" font-weight="700" fill="#0F172A">{Esc(Trunc(plans[i].Label, 20))}</text>""");
            sb.AppendLine($"""  <text x="{px + 20}" y="{py + 80}" font-family="Inter, sans-serif" font-size="24" font-weight="700" fill="#2563EB">{Esc(Trunc(plans[i].Value, 16))}</text>""");
            sb.AppendLine($"""  <text x="{px + 20}" y="{py + 112}" font-family="Inter, sans-serif" font-size="13" fill="#475569">{Esc(Trunc(plans[i].Sub, 32))}</text>""");
            for (int f = 0; f < 3; f++)
            {
                int fy = py + 148 + f * 32;
                if (fy + 56 > py + ch) break;
                sb.AppendLine($"""  <circle cx="{px + 28}" cy="{fy}" r="4" fill="#2563EB"/>""");
                sb.AppendLine($"""  <rect x="{px + 42}" y="{fy - 5}" width="{Math.Max(80, cw - 72)}" height="10" rx="5" fill="#CBD5E1"/>""");
            }
            DrawButton(sb, px + 20, py + ch - 54, cw - 40, 38, "Chọn gói", rec);
        }
    }

    private static void RenderLandingHeroBlock(StringBuilder sb, UiBlueprint bp, LayoutBlock block, int x, int y, int w, int h)
    {
        int heroH = Math.Min(220, h / 2);
        sb.AppendLine($"""  <rect x="{x + 20}" y="{y + 48}" width="{w - 40}" height="{heroH}" rx="12" fill="#0F172A"/>""");
        sb.AppendLine($"""  <text x="{x + 48}" y="{y + 104}" font-family="Inter, sans-serif" font-size="24" font-weight="700" fill="#FFFFFF">{Esc(Trunc(string.IsNullOrWhiteSpace(block.Note) ? bp.ScreenTitle : block.Note, 44))}</text>""");
        sb.AppendLine($"""  <text x="{x + 48}" y="{y + 136}" font-family="Inter, sans-serif" font-size="14" fill="#94A3B8">{Esc(Trunc(bp.Subtitle, 66))}</text>""");
        DrawButton(sb, x + 48, y + heroH - 14, 164, 38, bp.PrimaryButton, true);

        var feats = block.Items.Take(3).ToList();
        if (feats.Count > 0)
        {
            int fy = y + 64 + heroH;
            int fh = Math.Max(80, y + h - fy - 20);
            int gap = 16;
            int fw = (w - 40 - gap * (feats.Count - 1)) / feats.Count;
            for (int i = 0; i < feats.Count; i++)
            {
                int fx = x + 20 + i * (fw + gap);
                sb.AppendLine($"""  <rect x="{fx}" y="{fy}" width="{fw}" height="{fh}" rx="10" fill="#F8FAFC" stroke="#E2E8F0"/>""");
                sb.AppendLine($"""  <circle cx="{fx + 28}" cy="{fy + 28}" r="12" fill="#DBEAFE"/>""");
                sb.AppendLine($"""  <text x="{fx + 20}" y="{fy + 62}" font-family="Inter, sans-serif" font-size="14" font-weight="700" fill="#0F172A">{Esc(Trunc(feats[i].Label, 22))}</text>""");
                sb.AppendLine($"""  <text x="{fx + 20}" y="{fy + 84}" font-family="Inter, sans-serif" font-size="12" fill="#64748B">{Esc(Trunc(feats[i].Value, 28))}</text>""");
            }
        }
    }

    private static void RenderProfileHeaderBlock(StringBuilder sb, UiBlueprint bp, LayoutBlock block, int x, int y, int w, int h)
    {
        sb.AppendLine($"""  <circle cx="{x + 60}" cy="{y + h / 2}" r="30" fill="#DBEAFE"/>""");
        sb.AppendLine($"""  <text x="{x + 60}" y="{y + h / 2 + 6}" text-anchor="middle" font-family="Inter, sans-serif" font-size="18" font-weight="700" fill="#1D4ED8">U</text>""");
        sb.AppendLine($"""  <text x="{x + 106}" y="{y + h / 2 - 6}" font-family="Inter, sans-serif" font-size="17" font-weight="700" fill="#0F172A">{Esc(Trunc(bp.ScreenTitle, 32))}</text>""");
        sb.AppendLine($"""  <text x="{x + 106}" y="{y + h / 2 + 16}" font-family="Inter, sans-serif" font-size="12" fill="#64748B">{Esc(Trunc(bp.Subtitle, 48))}</text>""");
        int sx = x + w - 340;
        foreach (var stat in block.Items.Take(2))
        {
            sb.AppendLine($"""  <text x="{sx}" y="{y + h / 2 - 4}" font-family="Inter, sans-serif" font-size="16" font-weight="700" fill="#0F172A">{Esc(Trunc(stat.Value, 12))}</text>""");
            sb.AppendLine($"""  <text x="{sx}" y="{y + h / 2 + 16}" font-family="Inter, sans-serif" font-size="11" fill="#64748B">{Esc(Trunc(stat.Label, 14))}</text>""");
            sx += 110;
        }
        DrawButton(sb, x + w - 120, y + h / 2 - 18, 100, 36, bp.PrimaryButton, true);
    }

    private static void RenderMediaGalleryBlock(StringBuilder sb, LayoutBlock block, int x, int y, int w, int h)
    {
        var items = block.Items.Take(6).ToList();
        int cols = w >= 680 ? 3 : 2;
        int rows = Math.Max(1, (int)Math.Ceiling(items.Count / (double)cols));
        int gap = 14;
        int cw = (w - 40 - gap * (cols - 1)) / cols;
        int ch = Math.Min(210, (h - 68 - gap * (rows - 1)) / rows);
        for (int i = 0; i < items.Count; i++)
        {
            int c = i % cols, r = i / cols;
            int mx = x + 20 + c * (cw + gap);
            int my = y + 50 + r * (ch + gap);
            sb.AppendLine($"""  <rect x="{mx}" y="{my}" width="{cw}" height="{ch}" rx="10" fill="#F1F5F9" stroke="#E2E8F0"/>""");
            sb.AppendLine($"""  <circle cx="{mx + cw / 2}" cy="{my + ch / 2 - 12}" r="18" fill="#CBD5E1"/>""");
            sb.AppendLine($"""  <text x="{mx + 14}" y="{my + ch - 26}" font-family="Inter, sans-serif" font-size="13" font-weight="600" fill="#0F172A">{Esc(Trunc(items[i].Label, 22))}</text>""");
            sb.AppendLine($"""  <text x="{mx + 14}" y="{my + ch - 10}" font-family="Inter, sans-serif" font-size="11" fill="#64748B">{Esc(Trunc(items[i].Sub, 18))}</text>""");
        }
    }

    private static void RenderFileUploaderBlock(StringBuilder sb, LayoutBlock block, int x, int y, int w, int h)
    {
        int dzH = Math.Min(150, h / 3);
        sb.AppendLine($"""  <rect x="{x + 20}" y="{y + 52}" width="{w - 40}" height="{dzH}" rx="12" fill="#EFF6FF" stroke="#3B82F6" stroke-dasharray="6 4"/>""");
        sb.AppendLine($"""  <text x="{x + w / 2}" y="{y + 52 + dzH / 2 - 4}" text-anchor="middle" font-family="Inter, sans-serif" font-size="15" font-weight="700" fill="#1D4ED8">Kéo thả tệp vào đây hoặc bấm để chọn</text>""");
        sb.AppendLine($"""  <text x="{x + w / 2}" y="{y + 52 + dzH / 2 + 18}" text-anchor="middle" font-family="Inter, sans-serif" font-size="12" fill="#64748B">Hỗ trợ XLSX, CSV, PDF, PNG (tối đa 25MB)</text>""");

        int qy = y + 70 + dzH;
        foreach (var f in block.Items.Take(4))
        {
            if (qy + 56 > y + h - 16) break;
            int pct = int.TryParse(f.Value, out var v) ? Math.Clamp(v, 10, 100) : 70;
            sb.AppendLine($"""  <rect x="{x + 20}" y="{qy}" width="{w - 40}" height="48" rx="8" fill="#F8FAFC" stroke="#E2E8F0"/>""");
            sb.AppendLine($"""  <text x="{x + 34}" y="{qy + 22}" font-family="Inter, sans-serif" font-size="13" font-weight="600" fill="#0F172A">{Esc(Trunc(f.Label, 34))}</text>""");
            sb.AppendLine($"""  <text x="{x + w - 34}" y="{qy + 22}" text-anchor="end" font-family="Inter, sans-serif" font-size="11" fill="#64748B">{Esc(f.Sub)} · {pct}%</text>""");
            int barW = w - 68;
            sb.AppendLine($"""  <rect x="{x + 34}" y="{qy + 32}" width="{barW}" height="6" rx="3" fill="#E2E8F0"/>""");
            sb.AppendLine($"""  <rect x="{x + 34}" y="{qy + 32}" width="{(int)(barW * (pct / 100.0))}" height="6" rx="3" fill="#2563EB"/>""");
            qy += 58;
        }
    }

    private static void RenderNotificationsBlock(StringBuilder sb, LayoutBlock block, int x, int y, int w, int h)
    {
        int ny = y + 54;
        int max = Math.Clamp((h - 70) / 64, 1, 7);
        foreach (var n in block.Items.Take(max))
        {
            sb.AppendLine($"""  <rect x="{x + 20}" y="{ny}" width="{w - 40}" height="56" rx="8" fill="{(n.Active ? "#EFF6FF" : "#FFFFFF")}" stroke="#E2E8F0"/>""");
            if (n.Active)
                sb.AppendLine($"""  <circle cx="{x + 36}" cy="{ny + 28}" r="5" fill="#2563EB"/>""");
            sb.AppendLine($"""  <text x="{x + 52}" y="{ny + 24}" font-family="Inter, sans-serif" font-size="13" font-weight="700" fill="#0F172A">{Esc(Trunc(n.Label, 40))}</text>""");
            sb.AppendLine($"""  <text x="{x + 52}" y="{ny + 42}" font-family="Inter, sans-serif" font-size="12" fill="#64748B">{Esc(Trunc(n.Value, 58))}</text>""");
            sb.AppendLine($"""  <text x="{x + w - 34}" y="{ny + 24}" text-anchor="end" font-family="Inter, sans-serif" font-size="11" fill="#94A3B8">{Esc(Trunc(n.Sub, 16))}</text>""");
            ny += 64;
        }
    }

    private static void RenderInvoiceReceiptBlock(StringBuilder sb, UiBlueprint bp, LayoutBlock block, int x, int y, int w, int h)
    {
        RenderCartSummary(sb, bp, block, x, y, w, h);
    }

    private static void RenderDonutBreakdownBlock(StringBuilder sb, LayoutBlock block, int x, int y, int w, int h)
    {
        int cx = x + w / 2, cy = y + 138;
        sb.AppendLine($"""  <circle cx="{cx}" cy="{cy}" r="56" fill="none" stroke="#E2E8F0" stroke-width="18"/>""");
        sb.AppendLine($"""  <circle cx="{cx}" cy="{cy}" r="56" fill="none" stroke="#2563EB" stroke-width="18" stroke-dasharray="220 360"/>""");
        sb.AppendLine($"""  <text x="{cx}" y="{cy + 6}" text-anchor="middle" font-family="Inter, sans-serif" font-size="18" font-weight="700" fill="#0F172A">100%</text>""");

        int ly = y + 226;
        foreach (var item in block.Items.Take(4))
        {
            if (ly + 24 > y + h - 12) break;
            sb.AppendLine($"""  <circle cx="{x + 28}" cy="{ly - 4}" r="5" fill="#2563EB"/>""");
            sb.AppendLine($"""  <text x="{x + 42}" y="{ly}" font-family="Inter, sans-serif" font-size="12" fill="#334155">{Esc(Trunc(item.Label, 20))}</text>""");
            sb.AppendLine($"""  <text x="{x + w - 24}" y="{ly}" text-anchor="end" font-family="Inter, sans-serif" font-size="12" font-weight="700" fill="#0F172A">{Esc(Trunc(item.Sub, 14))}</text>""");
            ly += 28;
        }
    }

    private static void RenderComparisonMatrixBlock(StringBuilder sb, LayoutBlock block, int x, int y, int w, int h)
    {
        RenderTableBlock(sb, block, x, y, w, h);
    }

    private static void RenderConfirmModalBlock(StringBuilder sb, UiBlueprint bp, LayoutBlock block, int x, int y, int w, int h)
    {
        int cx = x + w / 2, cy = y + h / 2 - 40;
        sb.AppendLine($"""  <circle cx="{cx}" cy="{cy - 48}" r="28" fill="#DCFCE7"/>""");
        sb.AppendLine($"""  <text x="{cx}" y="{cy - 41}" text-anchor="middle" font-family="Inter, sans-serif" font-size="22" font-weight="700" fill="#15803D">✓</text>""");
        sb.AppendLine($"""  <text x="{cx}" y="{cy}" text-anchor="middle" font-family="Inter, sans-serif" font-size="18" font-weight="700" fill="#0F172A">{Esc(Trunc(block.Title, 40))}</text>""");
        sb.AppendLine($"""  <text x="{cx}" y="{cy + 28}" text-anchor="middle" font-family="Inter, sans-serif" font-size="13" fill="#64748B">{Esc(Trunc(block.Note, 64))}</text>""");
        DrawButton(sb, cx - 140, cy + 56, 130, 38, bp.SecondaryButton, false);
        DrawButton(sb, cx + 10, cy + 56, 130, 38, bp.PrimaryButton, true);
    }

    // -------------------------------------------------------------------------
    // SHARED SVG PRIMITIVES
    // -------------------------------------------------------------------------
    private static void DrawTopHeader(StringBuilder sb, string appName, string searchPlaceholder, string badgeText)
    {
        sb.AppendLine("""  <rect width="1280" height="64" fill="#FFFFFF"/>""");
        sb.AppendLine("""  <line x1="0" y1="64" x2="1280" y2="64" stroke="#E2E8F0"/>""");
        sb.AppendLine("""  <rect x="24" y="16" width="32" height="32" rx="8" fill="#2563EB"/>""");
        sb.AppendLine($"""  <text x="40" y="38" text-anchor="middle" font-family="Inter, sans-serif" font-size="15" font-weight="700" fill="#FFFFFF">{Esc(appName.Length > 0 ? appName[..1].ToUpperInvariant() : "S")}</text>""");
        sb.AppendLine($"""  <text x="68" y="38" font-family="Inter, sans-serif" font-size="16" font-weight="700" fill="#0F172A">{Esc(Trunc(appName, 20))}</text>""");

        sb.AppendLine("""  <rect x="280" y="14" width="380" height="36" rx="8" fill="#F8FAFC" stroke="#E2E8F0"/>""");
        sb.AppendLine($"""  <text x="300" y="37" font-family="Inter, sans-serif" font-size="13" fill="#94A3B8">{Esc(searchPlaceholder)}</text>""");

        sb.AppendLine("""  <rect x="1030" y="17" width="150" height="30" rx="15" fill="#EFF6FF"/>""");
        sb.AppendLine($"""  <text x="1105" y="36" text-anchor="middle" font-family="Inter, sans-serif" font-size="11" font-weight="600" fill="#1D4ED8">{Esc(Trunc(badgeText.Replace('_', ' '), 18))}</text>""");
        sb.AppendLine("""  <circle cx="1220" cy="32" r="16" fill="#E2E8F0"/>""");
        sb.AppendLine("""  <text x="1220" y="37" text-anchor="middle" font-family="Inter, sans-serif" font-size="12" font-weight="700" fill="#475569">UI</text>""");
    }

    private static void DrawSidebar(StringBuilder sb, List<string> navItems, int w, int y, int h)
    {
        sb.AppendLine($"""  <rect x="0" y="{y}" width="{w}" height="{h}" fill="#FFFFFF"/>""");
        sb.AppendLine($"""  <line x1="{w}" y1="{y}" x2="{w}" y2="{y + h}" stroke="#E2E8F0"/>""");
        sb.AppendLine($"""  <text x="24" y="{y + 38}" font-family="Inter, sans-serif" font-size="11" font-weight="700" fill="#94A3B8">MENU CHÍNH</text>""");
        var items = navItems.Take(6).ToList();
        if (items.Count == 0) items = new List<string> { "Overview", "Workspace", "Reports", "Settings" };
        for (int i = 0; i < items.Count; i++)
        {
            int iy = y + 58 + i * 44;
            bool active = i == 0;
            if (active)
                sb.AppendLine($"""  <rect x="16" y="{iy - 20}" width="{w - 32}" height="36" rx="8" fill="#EFF6FF"/>""");
            sb.AppendLine($"""  <circle cx="34" cy="{iy - 2}" r="3.5" fill="{(active ? "#2563EB" : "#94A3B8")}"/>""");
            sb.AppendLine($"""  <text x="48" y="{iy + 2}" font-family="Inter, sans-serif" font-size="13" font-weight="{(active ? "600" : "500")}" fill="{(active ? "#1D4ED8" : "#475569")}">{Esc(Trunc(items[i], 20))}</text>""");
        }
    }

    private static void DrawCard(StringBuilder sb, int x, int y, int w, int h) =>
        sb.AppendLine($"""  <rect x="{x}" y="{y}" width="{w}" height="{h}" rx="12" fill="#FFFFFF" stroke="#E2E8F0"/>""");

    private static void DrawField(StringBuilder sb, int x, int y, int w, string label, string value, string type)
    {
        sb.AppendLine($"""  <text x="{x}" y="{y}" font-family="Inter, sans-serif" font-size="12" font-weight="600" fill="#334155">{Esc(Trunc(label, 28))}</text>""");
        sb.AppendLine($"""  <rect x="{x}" y="{y + 10}" width="{w}" height="38" rx="8" fill="#F8FAFC" stroke="#CBD5E1"/>""");
        sb.AppendLine($"""  <text x="{x + 14}" y="{y + 34}" font-family="Inter, sans-serif" font-size="13" fill="#0F172A">{Esc(Trunc(value, 38))}</text>""");
        if (string.Equals(type, "select", StringComparison.OrdinalIgnoreCase))
            sb.AppendLine($"""  <text x="{x + w - 18}" y="{y + 34}" text-anchor="end" font-family="Inter, sans-serif" font-size="12" fill="#64748B">▾</text>""");
    }

    private static void DrawButton(StringBuilder sb, int x, int y, int w, int h, string label, bool primary)
    {
        sb.AppendLine($"""  <rect x="{x}" y="{y}" width="{w}" height="{h}" rx="8" fill="{(primary ? "#2563EB" : "#FFFFFF")}" stroke="{(primary ? "#2563EB" : "#CBD5E1")}"/>""");
        sb.AppendLine($"""  <text x="{x + w / 2}" y="{y + h / 2 + 5}" text-anchor="middle" font-family="Inter, sans-serif" font-size="13" font-weight="600" fill="{(primary ? "#FFFFFF" : "#334155")}">{Esc(Trunc(label, 22))}</text>""");
    }

    private static void DrawAlertBanner(StringBuilder sb, int x, int y, int w, string text)
    {
        sb.AppendLine($"""  <rect x="{x}" y="{y}" width="{w}" height="42" rx="8" fill="#FEF9C3" stroke="#FDE047"/>""");
        sb.AppendLine($"""  <text x="{x + 14}" y="{y + 26}" font-family="Inter, sans-serif" font-size="12" font-weight="500" fill="#854D0E">⚠ {Esc(Trunc(text, 52))}</text>""");
    }

    private static void DrawStatusPill(StringBuilder sb, int x, int y, string text, int tone)
    {
        var (bg, fg) = (tone % 4) switch
        {
            0 => ("#DBEAFE", "#1D4ED8"),
            1 => ("#FEF3C7", "#B45309"),
            2 => ("#DCFCE7", "#15803D"),
            _ => ("#F1F5F9", "#475569")
        };
        sb.AppendLine($"""  <rect x="{x}" y="{y}" width="76" height="24" rx="12" fill="{bg}"/>""");
        sb.AppendLine($"""  <text x="{x + 38}" y="{y + 16}" text-anchor="middle" font-family="Inter, sans-serif" font-size="11" font-weight="600" fill="{fg}">{Esc(Trunc(text, 10))}</text>""");
    }

    private static string Esc(string? s) => WebUtility.HtmlEncode(s ?? "");

    private static string Trunc(string? s, int max)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        var clean = s.Replace("\r", " ").Replace("\n", " ").Trim();
        return clean.Length <= max ? clean : clean[..(max - 1)] + "…";
    }
}
