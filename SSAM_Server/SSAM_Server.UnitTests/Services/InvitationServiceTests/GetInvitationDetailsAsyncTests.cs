using Microsoft.AspNetCore.Http;
using Moq;
using SSAM_Server.Models;
using SSAM_Server.Security;

namespace SSAM_Server.UnitTests.Services.InvitationServiceTests;

public class GetInvitationDetailsAsyncTests : InvitationServiceTestBase
{
    private CompanyInvitation CreateInvite(
        string status = "pending",
        DateTime? expiresAt = null,
        CompanyRole? role = null,
        Workspace? workspace = null)
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        return new CompanyInvitation
        {
            Id = 500,
            CompanyId = 10,
            Email = "invitee@acme.com",
            Status = status,
            ExpiresAt = expiresAt ?? now.AddDays(3),
            Company = new Company { Id = 10, Name = "Acme Tech Corp", IsActive = true },
            Workspace = workspace,
            Role = role,
            RoleName = "Member"
        };
    }

    // UTC001: A pending invitation with company, workspace, and role details returns valid details.
    [Fact]
    public async Task GetInvitationDetailsAsync_WhenPendingInviteIsValid_ReturnsDetails()
    {
        var workspace = new Workspace { Id = 100, Name = "Engineering Workspace", CompanyId = 10 };
        var role = new CompanyRole { Id = 2, Name = "Company Member", Scope = RoleScopes.Tenant };
        var invite = CreateInvite(role: role, workspace: workspace);
        InvitationRepositoryMock
            .Setup(r => r.GetInvitationWithDetailsAsync("valid-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(invite);

        var result = await InvitationService.GetInvitationDetailsAsync("valid-token", TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.True(result.Data!.Valid);
        Assert.Equal("invitee@acme.com", result.Data.Email);
        Assert.Equal("Acme Tech Corp", result.Data.CompanyName);
        Assert.Equal("Engineering Workspace", result.Data.WorkspaceName);
        Assert.Equal("Company Member", result.Data.RoleName);
        Assert.Equal(invite.ExpiresAt, result.Data.ExpiresAt);
    }

    // UTC002: A pending invitation without a loaded role falls back to the stored role name.
    [Fact]
    public async Task GetInvitationDetailsAsync_WhenRoleNavigationIsMissing_UsesStoredRoleName()
    {
        var invite = CreateInvite(role: null);
        InvitationRepositoryMock
            .Setup(r => r.GetInvitationWithDetailsAsync("fallback-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(invite);

        var result = await InvitationService.GetInvitationDetailsAsync("fallback-token", TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.True(result.Data!.Valid);
        Assert.Equal("Member", result.Data.RoleName);
        Assert.Null(result.Data.WorkspaceName);
    }

    // UTC003: A missing invitation returns an invalid NOT_FOUND result.
    [Fact]
    public async Task GetInvitationDetailsAsync_WhenInvitationIsMissing_ReturnsNotFoundReason()
    {
        InvitationRepositoryMock
            .Setup(r => r.GetInvitationWithDetailsAsync("missing-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync((CompanyInvitation?)null);

        var result = await InvitationService.GetInvitationDetailsAsync("missing-token", TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.False(result.Data!.Valid);
        Assert.Equal("NOT_FOUND", result.Data.Reason);
    }

    // UTC004: An accepted invitation returns an invalid ALREADY_USED result.
    [Fact]
    public async Task GetInvitationDetailsAsync_WhenInvitationIsAccepted_ReturnsAlreadyUsedReason()
    {
        var invite = CreateInvite(status: "accepted");
        InvitationRepositoryMock
            .Setup(r => r.GetInvitationWithDetailsAsync("accepted-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(invite);

        var result = await InvitationService.GetInvitationDetailsAsync("accepted-token", TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.False(result.Data!.Valid);
        Assert.Equal("ALREADY_USED", result.Data.Reason);
    }

    // UTC005: A non-pending invitation returns an invalid NOT_FOUND result.
    [Fact]
    public async Task GetInvitationDetailsAsync_WhenInvitationHasUnexpectedStatus_ReturnsNotFoundReason()
    {
        var invite = CreateInvite(status: "cancelled");
        InvitationRepositoryMock
            .Setup(r => r.GetInvitationWithDetailsAsync("cancelled-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(invite);

        var result = await InvitationService.GetInvitationDetailsAsync("cancelled-token", TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.False(result.Data!.Valid);
        Assert.Equal("NOT_FOUND", result.Data.Reason);
    }

    // UTC006: An expired invitation returns an invalid EXPIRED result.
    [Fact]
    public async Task GetInvitationDetailsAsync_WhenInvitationIsExpired_ReturnsExpiredReason()
    {
        var now = Clock.GetUtcNow().UtcDateTime;
        var invite = CreateInvite(expiresAt: now);
        InvitationRepositoryMock
            .Setup(r => r.GetInvitationWithDetailsAsync("expired-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(invite);

        var result = await InvitationService.GetInvitationDetailsAsync("expired-token", TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.False(result.Data!.Valid);
        Assert.Equal("EXPIRED", result.Data.Reason);
    }
}
