using SSAM_Server.Models;

namespace SSAM_Server.Interfaces.Repositories;

public interface IDepartmentRepository : IRepositoryBase
{
    IQueryable<Department> Departments { get; }
    IQueryable<CompanyMember> CompanyMembers { get; }
    IQueryable<WorkspaceDepartment> WorkspaceDepartments { get; }
}
