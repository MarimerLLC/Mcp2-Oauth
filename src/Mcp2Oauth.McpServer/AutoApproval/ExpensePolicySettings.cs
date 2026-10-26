namespace Mcp2Oauth.McpServer.AutoApproval;

/// <summary>
/// The org's auto-approval policy. The wording and limits are policy, so they're in
/// appsettings.json. The endpoint and deployment are tenant-specific and go in user
/// secrets. Until both are set, nothing is auto-approved.
/// </summary>
public sealed class ExpensePolicySettings
{
    public const string SectionName = "ExpensePolicy";

    /// <summary>The Foundry resource endpoint, e.g. <c>https://my-resource.services.ai.azure.com</c>.</summary>
    public string Endpoint { get; set; } = "";

    /// <summary>The deployment name of <c>Microsoft-Decision-1</c>.</summary>
    public string Deployment { get; set; } = "";

    /// <summary>The decision API path on the endpoint.</summary>
    public string Path { get; set; } = "/providers/microsoft/v1/systemone";

    /// <summary>The scope of the token the server gets for Foundry with its own credential.</summary>
    public string Scope { get; set; } = "https://cognitiveservices.azure.com/.default";

    /// <summary>How long to wait for the model before leaving the expense for a human.</summary>
    public double TimeoutSeconds { get; set; } = 5;

    /// <summary>How sure the model has to be, about the category and about there being no exclusions.</summary>
    public double MinimumConfidence { get; set; } = 0.8;

    /// <summary>Nothing above this is auto-approved, whatever the model says. Checked before the model is asked.</summary>
    public decimal Ceiling { get; set; } = 250;

    /// <summary>What the model chooses between, keyed by the label it answers with.</summary>
    public Dictionary<string, ExpenseCategory> Categories { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>What's never auto-approved, whatever the amount.</summary>
    public ExpenseExclusions Exclusions { get; set; } = new();

    public bool IsConfigured => Endpoint.Length > 0 && Deployment.Length > 0;
}

public sealed class ExpenseCategory
{
    /// <summary>Tells the model what belongs in this category.</summary>
    public string Description { get; set; } = "";

    /// <summary>The most that's auto-approved in this category. Null: always needs a human.</summary>
    public decimal? Limit { get; set; }
}

public sealed class ExpenseExclusions
{
    /// <summary>A short phrase, e.g. "alcohol, a gift or entertainment". Used in the question and in reasons.</summary>
    public string Summary { get; set; } = "";

    /// <summary>Tells the model when an expense includes an exclusion.</summary>
    public string WhenTrue { get; set; } = "";

    /// <summary>Tells the model when it doesn't.</summary>
    public string WhenFalse { get; set; } = "";
}
