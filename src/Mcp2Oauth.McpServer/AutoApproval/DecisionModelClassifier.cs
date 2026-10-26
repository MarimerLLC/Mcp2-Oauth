using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Abstractions;

namespace Mcp2Oauth.McpServer.AutoApproval;

/// <summary>
/// Classifies expenses with Microsoft-Decision-1 on Foundry. The server calls it
/// as itself: a client credentials token for its own Entra app registration, not
/// the user's token and not an API key. The expense description goes in the
/// request's <c>state</c>, never in the question instructions, so text in a
/// description can't rewrite the questions.
/// </summary>
public sealed class DecisionModelClassifier(
    HttpClient http,
    IAuthorizationHeaderProvider authorizationHeaders,
    IOptions<ExpensePolicySettings> settings,
    ILogger<DecisionModelClassifier> logger) : IExpenseClassifier
{
    public const string CategoryQuestion = "category";
    public const string ExclusionQuestion = "excluded";

    public bool IsAvailable => settings.Value.IsConfigured;

    public async Task<ExpenseClassification?> ClassifyAsync(string description, CancellationToken cancellationToken)
    {
        var policy = settings.Value;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(policy.TimeoutSeconds));

        try
        {
            string authorization;
            using (ExpensePolicy.ActivitySource.StartActivity("Acquire decision model token (client credentials)"))
            {
                authorization = await authorizationHeaders.CreateAuthorizationHeaderForAppAsync(
                    policy.Scope, cancellationToken: timeout.Token);
            }

            // StringContent, not JsonContent: the systemone endpoint rejects a request
            // without Content-Length, and JsonContent streams without one.
            using var request = new HttpRequestMessage(HttpMethod.Post, policy.Endpoint.TrimEnd('/') + "/" + policy.Path.TrimStart('/'))
            {
                Content = new StringContent(BuildBody(policy, description).ToJsonString(), Encoding.UTF8, "application/json"),
            };
            request.Headers.TryAddWithoutValidation("Authorization", authorization);

            using var response = await http.SendAsync(request, timeout.Token);
            var body = await response.Content.ReadAsStringAsync(timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Decision model returned HTTP {Status}: {Body}", (int)response.StatusCode, body);
                return null;
            }

            var classification = Parse(body);
            if (classification is null)
                logger.LogWarning("Decision model response didn't answer both questions: {Body}", body);
            return classification;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Decision model didn't answer within {Seconds}s", policy.TimeoutSeconds);
            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Includes failing to get a token, e.g. no AzureAd:ClientCredentials or no
            // role on the Foundry resource. The policy fails closed either way.
            logger.LogWarning(ex, "Decision model call failed");
            return null;
        }
    }

    /// <summary>The systemone request: a choice of category, and a yes/no on the exclusions.</summary>
    public static JsonObject BuildBody(ExpensePolicySettings policy, string description)
    {
        var categories = new JsonObject();
        foreach (var (label, category) in policy.Categories)
            categories[label] = category.Description;

        return new JsonObject
        {
            ["model"] = policy.Deployment,
            ["state"] = new JsonObject { ["expense_description"] = description },
            ["questions"] = new JsonObject
            {
                [CategoryQuestion] = new JsonObject
                {
                    ["type"] = "choice",
                    ["instructions"] = "Which category does this expense belong to?",
                    ["criteria"] = categories,
                },
                [ExclusionQuestion] = new JsonObject
                {
                    ["type"] = "noul",
                    ["instructions"] = $"Does this expense include {policy.Exclusions.Summary}?",
                    ["criteria"] = new JsonObject
                    {
                        ["true"] = policy.Exclusions.WhenTrue,
                        ["false"] = policy.Exclusions.WhenFalse,
                    },
                },
            },
        };
    }

    /// <summary>
    /// Reads the answers, e.g. <c>{"answers":{"category":{"choice":"meal","confidence":0.94},"excluded":{"noul":0.03}}}</c>.
    /// Null unless both questions were answered.
    /// </summary>
    public static ExpenseClassification? Parse(string body)
    {
        try
        {
            if (JsonNode.Parse(body)?["answers"] is not JsonObject answers)
                return null;
            if (answers[CategoryQuestion] is not JsonObject category
                || category["choice"] is not JsonValue choice || !choice.TryGetValue(out string? label)
                || category["confidence"] is not JsonValue confidence || !confidence.TryGetValue(out double categoryConfidence))
                return null;
            if (answers[ExclusionQuestion] is not JsonObject excluded
                || excluded["noul"] is not JsonValue noul || !noul.TryGetValue(out double exclusionProbability))
                return null;

            return new(label, Math.Clamp(categoryConfidence, 0, 1), Math.Clamp(exclusionProbability, 0, 1));
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
