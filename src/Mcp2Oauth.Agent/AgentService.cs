using System.ClientModel;
using System.Text.Json;
using Mcp2Oauth.Contracts;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Protocol;

namespace Mcp2Oauth.Agent;

/// <summary>
/// The agent: an LLM that picks and calls MCP tools, using whichever identity
/// the caller asked for.
/// </summary>
public sealed class AgentService(McpConnector connector, IServiceProvider services)
{
    private const string SystemPrompt =
        """
        You are an expense assistant. Use the available tools to answer questions about
        the user's expense reports, submit new ones, handle approvals, and review the
        audit log. If a tool
        isn't available or fails with an authorization error, say so plainly; don't guess.
        When asked who you are or who the user is, call the whoami tool.
        """;

    public async Task<ProbeResponse> ProbeAsync(CallMode mode, CancellationToken cancellationToken)
    {
        await using var session = await connector.ConnectAsync(mode, cancellationToken);
        var tools = await session.Client.ListToolsAsync(cancellationToken: cancellationToken);

        var result = await session.Client.CallToolAsync("whoami", cancellationToken: cancellationToken);
        if (result.IsError == true)
            return new ProbeResponse(null, TextOf(result), ToolNames(tools), session.Token);

        var identity = result.StructuredContent is { } structured
            ? structured.Deserialize<IdentityReport>(JsonSerializerOptions.Web)
            : JsonSerializer.Deserialize<IdentityReport>(TextOf(result), JsonSerializerOptions.Web);

        return new ProbeResponse(identity, null, ToolNames(tools), session.Token);
    }

    public async Task<AgentChatResponse> ChatAsync(AgentChatRequest request, CancellationToken cancellationToken)
    {
        var chatClient = services.GetService<IChatClient>()
            ?? throw new InvalidOperationException(
                "Azure OpenAI isn't configured. Set AzureOpenAI:Endpoint and AzureOpenAI:Deployment for the agent.");

        await using var session = await connector.ConnectAsync(request.Mode, cancellationToken);

        // tools/list is filtered by the server's authorization policies, so an
        // app-only agent never even sees the per-user tools.
        var tools = await session.Client.ListToolsAsync(cancellationToken: cancellationToken);

        List<ChatMessage> messages = [new(ChatRole.System, SystemPrompt)];
        messages.AddRange(request.Messages.Select(m =>
            new ChatMessage(m.Role == "assistant" ? ChatRole.Assistant : ChatRole.User, m.Text)));

        // Record which tools actually run, so a refused request can say whether
        // anything happened before the refusal.
        var called = new List<string>();
        Microsoft.Extensions.AI.ChatResponse response;
        try
        {
            response = await chatClient.GetResponseAsync(
                messages,
                new ChatOptions { Tools = [.. tools.Select(t => new RecordingTool(t, called))] },
                cancellationToken);
        }
        catch (ClientResultException ex) when (IsContentFilter(ex))
        {
            return new AgentChatResponse(BlockedReply(called), [], ToolNames(tools), session.Token, Blocked: true);
        }

        return new AgentChatResponse(response.Text, TraceToolCalls(response), ToolNames(tools), session.Token);
    }

    /// <summary>
    /// Azure OpenAI's content filter, including its jailbreak (prompt injection)
    /// detection, refuses a request with HTTP 400 and the code content_filter.
    /// </summary>
    private static bool IsContentFilter(ClientResultException ex) =>
        ex.Status == 400 && ex.Message.Contains("content_filter", StringComparison.Ordinal);

    private static string BlockedReply(List<string> called)
    {
        lock (called)
        {
            return called.Count == 0
                ? "Azure OpenAI's content filter refused that message as a likely prompt injection, so I didn't act on it "
                  + "and no tools were called. Nothing was submitted. Try again without the instructions aimed at the system."
                : "Azure OpenAI's content filter refused this conversation as a likely prompt injection, after I had already called "
                  + string.Join(", ", called) + ". Ask me to list your expenses to see where things stand.";
        }
    }

    /// <summary>An MCP tool that records its name each time the model calls it.</summary>
    private sealed class RecordingTool(AIFunction tool, List<string> called) : DelegatingAIFunction(tool)
    {
        protected override ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
        {
            lock (called) called.Add(Name);
            return base.InvokeCoreAsync(arguments, cancellationToken);
        }
    }

    private static List<ToolCallTrace> TraceToolCalls(Microsoft.Extensions.AI.ChatResponse response)
    {
        var contents = response.Messages.SelectMany(m => m.Contents).ToList();
        var results = contents.OfType<FunctionResultContent>().ToDictionary(r => r.CallId);

        return contents.OfType<FunctionCallContent>()
            .Select(call => new ToolCallTrace(
                call.Name,
                JsonSerializer.Serialize(call.Arguments),
                results.TryGetValue(call.CallId, out var result) ? Describe(result.Result) : "(no result)"))
            .ToList();
    }

    private static string Describe(object? result) => result switch
    {
        null => "(null)",
        string s => s,
        JsonElement e => e.GetRawText(),
        _ => JsonSerializer.Serialize(result),
    };

    private static List<string> ToolNames(IEnumerable<ModelContextProtocol.Client.McpClientTool> tools) =>
        tools.Select(t => t.Name).ToList();

    private static string TextOf(CallToolResult result) =>
        string.Concat(result.Content.OfType<TextContentBlock>().Select(t => t.Text));
}
