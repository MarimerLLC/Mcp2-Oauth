using System.Diagnostics;
using System.Globalization;
using Mcp2Oauth.McpServer.Audit;
using Microsoft.Extensions.Options;

namespace Mcp2Oauth.McpServer.AutoApproval;

public sealed record PolicyDecision(bool AutoApprove, string Reason);

/// <summary>
/// The org's expense policy: decides whether a new expense is approved without a
/// human. The decision model only classifies the expense. The ceiling, the
/// per-category limits and the confidence bar are all applied here, in code, so
/// the model can never approve more than the policy allows. Anything the policy
/// can't clear, including a model that doesn't answer, waits for a human.
/// </summary>
public sealed class ExpensePolicy(IExpenseClassifier classifier, IOptions<ExpensePolicySettings> settings, IConfiguration configuration)
{
    /// <summary>Matches the application name, which ServiceDefaults registers as a trace source.</summary>
    public static readonly ActivitySource ActivitySource = new("Mcp2Oauth.McpServer");

    /// <summary>The <see cref="Data.Expense.ApprovedBy"/> of an auto-approved expense.</summary>
    public const string ApproverName = "policy";

    public bool IsEnabled => classifier.IsAvailable;

    /// <summary>
    /// The server, acting as itself: its own Entra app id, the same identity it uses
    /// to call the decision model.
    /// </summary>
    public AuditActor Actor => new(configuration["AzureAd:ClientId"] ?? "mcp-server", $"Expense policy ({settings.Value.Deployment})");

    public async Task<PolicyDecision> EvaluateAsync(string description, decimal amount, CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("Expense policy check");
        var decision = await DecideAsync(description, amount, activity, cancellationToken);
        activity?.SetTag("mcp2oauth.policy.auto_approve", decision.AutoApprove);
        activity?.SetTag("mcp2oauth.policy.reason", decision.Reason);
        return decision;
    }

    private async Task<PolicyDecision> DecideAsync(string description, decimal amount, Activity? activity, CancellationToken cancellationToken)
    {
        var policy = settings.Value;

        // The model is never asked about an amount the policy refuses anyway.
        if (amount > policy.Ceiling)
            return Refer($"{Usd(amount)} is over the {Usd(policy.Ceiling)} auto-approval ceiling.");

        var classification = await classifier.ClassifyAsync(description, cancellationToken);
        if (classification is null)
            return Refer("The decision model didn't answer.");

        activity?.SetTag("mcp2oauth.policy.category", classification.Category);
        activity?.SetTag("mcp2oauth.policy.category_confidence", classification.CategoryConfidence);
        activity?.SetTag("mcp2oauth.policy.exclusion_probability", classification.ExclusionProbability);

        var category = classification.Category;
        var notExcluded = 1 - classification.ExclusionProbability;
        if (notExcluded < policy.MinimumConfidence)
            return Refer(Invariant($"It may include {policy.Exclusions.Summary}, which the policy excludes ({classification.ExclusionProbability:0.00})."));
        if (classification.CategoryConfidence < policy.MinimumConfidence)
            return Refer(Invariant($"The category is unclear ({category}, {classification.CategoryConfidence:0.00})."));
        if (!policy.Categories.TryGetValue(category, out var rule) || rule.Limit is not { } limit)
            return Refer($"Expenses in the '{category}' category aren't auto-approved.");
        if (amount > limit)
            return Refer($"{Usd(amount)} is over the {Usd(limit)} limit for '{category}'.");

        return new(true, Invariant(
            $"'{category}' ({classification.CategoryConfidence:0.00}), no exclusions ({notExcluded:0.00}), {Usd(amount)} is within the {Usd(limit)} limit."));
    }

    private static PolicyDecision Refer(string reason) => new(false, reason);

    private static string Usd(decimal amount) => amount.ToString("$#,0.00", CultureInfo.InvariantCulture);

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
