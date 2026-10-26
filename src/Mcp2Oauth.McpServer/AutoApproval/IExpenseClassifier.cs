namespace Mcp2Oauth.McpServer.AutoApproval;

/// <summary>
/// What the decision model says an expense is: its category, and how likely it is
/// to include something the policy excludes. The model never sees the amount;
/// <see cref="ExpensePolicy"/> applies the limits in code.
/// </summary>
public sealed record ExpenseClassification(string Category, double CategoryConfidence, double ExclusionProbability);

public interface IExpenseClassifier
{
    /// <summary>False when no model is configured, so the policy is off.</summary>
    bool IsAvailable { get; }

    /// <summary>Classifies an expense description. Null when the model didn't answer.</summary>
    Task<ExpenseClassification?> ClassifyAsync(string description, CancellationToken cancellationToken);
}
