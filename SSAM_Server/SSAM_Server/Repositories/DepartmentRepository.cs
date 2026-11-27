using SSAM_Server.Data;
using SSAM_Server.Interfaces.Repositories;
using SSAM_Server.Models;

namespace SSAM_Server.Repositories;

public class DepartmentRepository : RepositoryBase, IDepartmentRepository
{
    public DepartmentRepository(AppDbContext db) : base(db) { }

    public IQueryable<Department> Departments => _db.Departments;
    public IQueryable<CompanyMember> CompanyMembers => _db.CompanyMembers;
    public IQueryable<WorkspaceDepartment> WorkspaceDepartments => _db.WorkspaceDepartments;
}
