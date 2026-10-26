using Mcp2Oauth.McpServer.Audit;
using Mcp2Oauth.McpServer.Data;
using ModelContextProtocol;

namespace Mcp2Oauth.McpServer.Tests;

/// <summary>
/// The audit trail, exercised through the MCP tools the way an agent calls them.
/// </summary>
public sealed class AuditLogTests
{
    private static readonly AuditActor Agent = new(TestMcpServer.AgentAppId, TestMcpServer.AgentName);

    [Fact]
    public async Task Starts_with_history_for_the_seeded_expenses()
    {
        await using var server = new TestMcpServer();

        var trail = await AuditTrailAsync(server);

        Assert.Equal([1L, 2L, 3L, 4L], trail.Select(e => e.Sequence));
        Assert.All(trail, e => Assert.Equal(AuditOutcome.Succeeded, e.Outcome));
        Assert.Equal(
            [(AuditAction.SubmitExpense, 3), (AuditAction.ApproveExpense, 3)],
            trail.Where(e => e.ExpenseId == 3).Select(e => (e.Action, e.ExpenseId!.Value)));
    }

    [Fact]
    public async Task Submitting_an_expense_records_the_user_and_the_agent()
    {
        await using var server = new TestMcpServer();
        await using var employee = await server.ConnectAsync(TestToken.OnBehalfOf(TestUser.Employee));

        var expense = await employee.CallAsync<Expense>("submit_expense", new() { ["description"] = "Team lunch", ["amount"] = 45m });

        var entry = Assert.Single(await AuditTrailAsync(server, expense.Id));
        Assert.Equal(5, entry.Sequence);
        Assert.Equal(AuditAction.SubmitExpense, entry.Action);
        Assert.Equal(AuditOutcome.Succeeded, entry.Outcome);
        Assert.Equal(45m, entry.Amount);
        Assert.Equal(TestUser.Employee.Name, entry.ExpenseOwner);
        Assert.Equal(new AuditActor(TestUser.Employee.ObjectId, TestUser.Employee.UserName), entry.User);
        Assert.Equal(Agent, entry.Agent);
        Assert.Null(entry.Reason);
        Assert.False(string.IsNullOrEmpty(entry.TraceId));
    }

    [Theory]
    [InlineData("", 45, "A description is required.")]
    [InlineData("Team lunch", 0, "Amount must be between 0 and 10,000 USD.")]
    [InlineData("Company car", 25_000, "Amount must be between 0 and 10,000 USD.")]
    public async Task A_refused_submission_is_audited_and_creates_no_expense(string description, decimal amount, string reason)
    {
        await using var server = new TestMcpServer();
        await using var employee = await server.ConnectAsync(TestToken.OnBehalfOf(TestUser.Employee));

        var error = await employee.CallExpectingErrorAsync("submit_expense", new() { ["description"] = description, ["amount"] = amount });

        Assert.Contains(reason, error);
        Assert.Empty(await employee.CallAsync<List<Expense>>("list_my_expenses"));

        var entry = (await AuditTrailAsync(server)).Last();
        Assert.Equal(AuditAction.SubmitExpense, entry.Action);
        Assert.Equal(AuditOutcome.Denied, entry.Outcome);
        Assert.Null(entry.ExpenseId);
        Assert.Equal(amount, entry.Amount);
        Assert.Equal(reason, entry.Reason);
        Assert.Equal(TestUser.Employee.ObjectId, entry.User.Id);
        Assert.Equal(Agent, entry.Agent);
    }

    [Fact]
    public async Task Approving_someone_elses_expense_is_audited()
    {
        await using var server = new TestMcpServer();
        await using var approver = await server.ConnectAsync(TestToken.OnBehalfOf(TestUser.Approver));

        var approved = await approver.CallAsync<Expense>("approve_expense", new() { ["id"] = 2 });
        Assert.Equal(ExpenseStatus.Approved, approved.Status);

        var trail = await AuditTrailAsync(server, 2);
        Assert.Equal([AuditAction.SubmitExpense, AuditAction.ApproveExpense], trail.Select(e => e.Action));

        var entry = trail[^1];
        Assert.Equal(AuditOutcome.Succeeded, entry.Outcome);
        Assert.Equal(142.50m, entry.Amount);
        Assert.Equal("Bob Example", entry.ExpenseOwner);
        Assert.Equal(new AuditActor(TestUser.Approver.ObjectId, TestUser.Approver.UserName), entry.User);
        Assert.Equal(Agent, entry.Agent);
    }

    [Fact]
    public async Task Approving_your_own_expense_is_refused_and_audited()
    {
        await using var server = new TestMcpServer();
        await using var approver = await server.ConnectAsync(TestToken.OnBehalfOf(TestUser.Approver));
        var expense = await approver.CallAsync<Expense>("submit_expense", new() { ["description"] = "Hotel", ["amount"] = 310m });

        var error = await approver.CallExpectingErrorAsync("approve_expense", new() { ["id"] = expense.Id });

        Assert.Contains("You can't approve your own expense.", error);
        Assert.Equal(ExpenseStatus.Pending, Assert.Single(await approver.CallAsync<List<Expense>>("list_my_expenses")).Status);

        var trail = await AuditTrailAsync(server, expense.Id);
        Assert.Equal(
            [(AuditAction.SubmitExpense, AuditOutcome.Succeeded), (AuditAction.ApproveExpense, AuditOutcome.Denied)],
            trail.Select(e => (e.Action, e.Outcome)));
        Assert.Equal("You can't approve your own expense.", trail[^1].Reason);
        Assert.Equal(310m, trail[^1].Amount);
        Assert.Equal(TestUser.Approver.ObjectId, trail[^1].User.Id);
    }

    [Fact]
    public async Task Approving_an_expense_twice_is_refused_and_audited()
    {
        await using var server = new TestMcpServer();
        await using var approver = await server.ConnectAsync(TestToken.OnBehalfOf(TestUser.Approver));

        // Expense 3 is seeded as already approved.
        var error = await approver.CallExpectingErrorAsync("approve_expense", new() { ["id"] = 3 });

        Assert.Contains("Expense 3 is already Approved.", error);
        var entry = (await AuditTrailAsync(server, 3))[^1];
        Assert.Equal(AuditAction.ApproveExpense, entry.Action);
        Assert.Equal(AuditOutcome.Denied, entry.Outcome);
        Assert.Equal(TestUser.Approver.ObjectId, entry.User.Id);
    }

    [Fact]
    public async Task Approving_a_missing_expense_is_refused_and_audited()
    {
        await using var server = new TestMcpServer();
        await using var approver = await server.ConnectAsync(TestToken.OnBehalfOf(TestUser.Approver));

        var error = await approver.CallExpectingErrorAsync("approve_expense", new() { ["id"] = 999 });

        Assert.Contains("Expense 999 was not found.", error);
        var entry = Assert.Single(await AuditTrailAsync(server, 999));
        Assert.Equal(AuditOutcome.Denied, entry.Outcome);
        Assert.Null(entry.Amount);
        Assert.Null(entry.ExpenseOwner);
    }

    [Fact]
    public async Task Read_only_tools_are_not_audited()
    {
        await using var server = new TestMcpServer();
        await using var approver = await server.ConnectAsync(TestToken.OnBehalfOf(TestUser.Approver));

        await approver.CallAsync<List<Expense>>("list_my_expenses");
        await approver.CallAsync<List<Expense>>("list_pending_approvals");

        Assert.Equal(4, (await AuditTrailAsync(server)).Count);
    }

    [Theory]
    [InlineData(nameof(TestUser.Employee), false)]
    [InlineData(nameof(TestUser.Approver), false)]
    [InlineData(nameof(TestUser.Auditor), true)]
    public async Task Only_auditors_can_see_the_audit_log(string userName, bool visible)
    {
        var user = userName switch
        {
            nameof(TestUser.Employee) => TestUser.Employee,
            nameof(TestUser.Approver) => TestUser.Approver,
            _ => TestUser.Auditor,
        };
        await using var server = new TestMcpServer();
        await using var client = await server.ConnectAsync(TestToken.OnBehalfOf(user));

        Assert.Equal(visible, (await client.ToolNamesAsync()).Contains("get_audit_log"));
    }

    [Fact]
    public async Task An_app_only_agent_cannot_see_the_audit_log()
    {
        await using var server = new TestMcpServer();
        await using var agent = await server.ConnectAsync(TestToken.AppOnly());

        Assert.DoesNotContain("get_audit_log", await agent.ToolNamesAsync());
    }

    [Fact]
    public async Task A_non_auditor_cannot_call_the_audit_log_directly()
    {
        await using var server = new TestMcpServer();
        await using var approver = await server.ConnectAsync(TestToken.OnBehalfOf(TestUser.Approver));

        // The authorization filter rejects the request itself, before the tool runs.
        var error = await Assert.ThrowsAsync<McpProtocolException>(() =>
            approver.CallToolAsync("get_audit_log", cancellationToken: TestContext.Current.CancellationToken).AsTask());
        Assert.Contains("Access forbidden", error.Message);
    }

    private static async Task<List<AuditEntry>> AuditTrailAsync(TestMcpServer server, int? expenseId = null)
    {
        await using var auditor = await server.ConnectAsync(TestToken.OnBehalfOf(TestUser.Auditor));
        return await auditor.CallAsync<List<AuditEntry>>("get_audit_log", new() { ["expenseId"] = expenseId });
    }
}
