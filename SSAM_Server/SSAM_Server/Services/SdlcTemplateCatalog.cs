using SSAM_Server.DTOs.Projects;
using SSAM_Server.Models;

namespace SSAM_Server.Services;

public static class SdlcTemplateCatalog
{
    private const string Gray = "#78716c";
    private const string Blue = "#2563eb";
    private const string Violet = "#7c3aed";
    private const string Green = "#079455";
    private const string Amber = "#d97706";

    private static TemplateStatusDto Status(
        string key,
        string name,
        string category,
        string color,
        string? description,
        bool isInitial = false) => new()
        {
            Key = key,
            Name = name,
            Category = category,
            Color = color,
            Description = description,
            IsInitial = isInitial
        };

    private static TemplateTransitionDto Transition(
        string from,
        string to,
        string[] allowedRoleCodes,
        string? commentHint = null) => new()
        {
            From = from,
            To = to,
            AllowedRoleCodes = allowedRoleCodes.ToList(),
            RequireComment = commentHint != null,
            CommentHint = commentHint,
            Condition = null
        };

    private static TemplatePhaseDto CodingPhase(string name, string description) => new()
    {
        Name = name,
        Description = description,
        Statuses =
        [
            Status("todo", "Open / To Do", StatusCategories.Todo, Gray, "Issue mới tạo, đang xếp hàng chờ phân công", isInitial: true),
            Status("dev", "In Development", StatusCategories.InProgress, Blue, "Lập trình viên đang xử lý code, commit lên feature branch"),
            Status("review", "Code Review & QA", StatusCategories.InProgress, Violet, "Kiểm thử chức năng QA, test tự động CI và review code"),
            Status("done", "Completed / Closed", StatusCategories.Done, Green, "Tính năng đã hoàn thành, build merge lên main")
        ],
        Transitions =
        [
            Transition("todo", "dev", [CompanyRoleCodes.Developer, CompanyRoleCodes.TechLead, CompanyRoleCodes.ProjectManager]),
            Transition("dev", "review", [CompanyRoleCodes.Developer], "Link PR / Git commit"),
            Transition("review", "done", [CompanyRoleCodes.Qa, CompanyRoleCodes.TechLead]),
            Transition("review", "dev", [CompanyRoleCodes.Qa, CompanyRoleCodes.TechLead], "Bug details & logs")
        ]
    };

    public static readonly IReadOnlyList<SdlcTemplateDto> Templates =
    [
        new SdlcTemplateDto
        {
            Method = SdlcMethods.Scrum,
            Name = "Agile Scrum",
            Description = "Sprints cố định 2 tuần, quản trị Product Backlog, Sprint Planning, Daily Standup & Retrospective.",
            PhasesEditable = false,
            Phases =
            [
                new TemplatePhaseDto
                {
                    Name = "Product Backlog",
                    Description = "Thu thập, làm rõ và ưu tiên user story",
                    Statuses =
                    [
                        Status("backlog", "Backlog", StatusCategories.Todo, Gray, "User story mới, chưa làm rõ", isInitial: true),
                        Status("refining", "Refining", StatusCategories.InProgress, Blue, "Đang làm rõ tiêu chí nghiệm thu"),
                        Status("ready", "Ready for Sprint", StatusCategories.Done, Green, "Đủ điều kiện đưa vào sprint")
                    ],
                    Transitions =
                    [
                        Transition("backlog", "refining", [CompanyRoleCodes.Ba, CompanyRoleCodes.ProjectManager]),
                        Transition("refining", "ready", [CompanyRoleCodes.ProjectManager, CompanyRoleCodes.TechLead])
                    ]
                },
                CodingPhase("Sprint Execution", "Thực thi sprint 2 tuần"),
                new TemplatePhaseDto
                {
                    Name = "Sprint Review",
                    Description = "Demo và nghiệm thu cuối sprint",
                    Statuses =
                    [
                        Status("demo", "In Review", StatusCategories.InProgress, Violet, "Đang demo cho Product Owner", isInitial: true),
                        Status("accepted", "Accepted", StatusCategories.Done, Green, "Đạt Definition of Done")
                    ],
                    Transitions =
                    [
                        Transition("demo", "accepted", [CompanyRoleCodes.ProjectManager, CompanyRoleCodes.Qa])
                    ]
                },
                new TemplatePhaseDto
                {
                    Name = "Release",
                    Description = "Đóng gói và phát hành",
                    Statuses =
                    [
                        Status("to_release", "Ready to Release", StatusCategories.Todo, Amber, null, isInitial: true),
                        Status("released", "Released", StatusCategories.Done, Green, null)
                    ],
                    Transitions =
                    [
                        Transition("to_release", "released", [CompanyRoleCodes.TechLead, CompanyRoleCodes.ProjectManager])
                    ]
                }
            ]
        },
        new SdlcTemplateDto
        {
            Method = SdlcMethods.Kanban,
            Name = "Kanban",
            Description = "Continuous flow, giới hạn công việc đang làm (WIP limits), tối ưu hoá Lead & Cycle Time.",
            PhasesEditable = false,
            Phases =
            [
                new TemplatePhaseDto
                {
                    Name = "Kanban Board",
                    Description = "Luồng liên tục — Standard Kanban Workflow",
                    Statuses =
                    [
                        Status("todo", "To Do", StatusCategories.Todo, Gray, "Issue mới tạo, chờ phân công", isInitial: true),
                        Status("doing", "In Progress", StatusCategories.InProgress, Blue, "Đang thực hiện"),
                        Status("review", "Code Review", StatusCategories.InProgress, Violet, "Chờ review / kiểm thử"),
                        Status("done", "Done", StatusCategories.Done, Green, "Hoàn thành")
                    ],
                    Transitions =
                    [
                        Transition("todo", "doing", [CompanyRoleCodes.Developer, CompanyRoleCodes.TechLead, CompanyRoleCodes.ProjectManager]),
                        Transition("doing", "review", [CompanyRoleCodes.Developer], "Link PR / Git commit"),
                        Transition("review", "done", [CompanyRoleCodes.Qa, CompanyRoleCodes.TechLead]),
                        Transition("review", "doing", [CompanyRoleCodes.Qa, CompanyRoleCodes.TechLead], "Bug details & logs")
                    ]
                }
            ]
        },
        new SdlcTemplateDto
        {
            Method = SdlcMethods.Custom,
            Name = "Custom SDLC Template",
            Description = "Tuỳ chỉnh linh hoạt thứ tự, tên gọi và điều kiện bắt buộc của từng giai đoạn bàn giao kỹ thuật.",
            PhasesEditable = true,
            Phases =
            [
                new TemplatePhaseDto
                {
                    Name = "Requirement & Analysis",
                    Description = "Thu thập và phân tích yêu cầu",
                    Statuses =
                    [
                        Status("open", "Open", StatusCategories.Todo, Gray, "Yêu cầu mới ghi nhận", isInitial: true),
                        Status("drafting", "Drafting", StatusCategories.InProgress, Blue, "BA đang soạn đặc tả"),
                        Status("approved", "Approved", StatusCategories.Done, Green, "Đặc tả đã được phê duyệt")
                    ],
                    Transitions =
                    [
                        Transition("open", "drafting", [CompanyRoleCodes.Ba, CompanyRoleCodes.ProjectManager]),
                        Transition("drafting", "approved", [CompanyRoleCodes.ProjectManager]),
                        Transition("approved", "drafting", [CompanyRoleCodes.ProjectManager], "Lý do mở lại đặc tả")
                    ]
                },
                new TemplatePhaseDto
                {
                    Name = "UI/UX Design & Architecture",
                    Description = "Thiết kế wireframe, prototype và kiến trúc",
                    Statuses =
                    [
                        Status("wireframing", "Wireframing", StatusCategories.InProgress, Blue, "Đang thiết kế", isInitial: true),
                        Status("reviewed", "Reviewed", StatusCategories.Done, Green, "Thiết kế đã được duyệt")
                    ],
                    Transitions =
                    [
                        Transition("wireframing", "reviewed", [CompanyRoleCodes.TechLead, CompanyRoleCodes.ProjectManager])
                    ]
                },
                CodingPhase("Sprint Execution & Coding", "Lập trình tính năng theo các sprint"),
                new TemplatePhaseDto
                {
                    Name = "UAT & Deployment",
                    Description = "Kiểm thử chấp nhận người dùng và triển khai",
                    Statuses =
                    [
                        Status("uat", "UAT Testing", StatusCategories.InProgress, Violet, "Người dùng đang kiểm thử chấp nhận", isInitial: true),
                        Status("deployed", "Deployed", StatusCategories.Done, Green, "Đã triển khai production")
                    ],
                    Transitions =
                    [
                        Transition("uat", "deployed", [CompanyRoleCodes.Qa, CompanyRoleCodes.ProjectManager])
                    ]
                }
            ]
        }
    ];
}
