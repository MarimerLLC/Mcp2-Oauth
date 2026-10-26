namespace Mcp2Oauth.McpServer;

public sealed class McpServerSettings
{
    public const string SectionName = "McpServer";

    /// <summary>The public URL of the MCP endpoint, used as the protected resource identifier.</summary>
    public string PublicUrl { get; set; } = "https://localhost:7101/mcp";

    /// <summary>
    /// Agents (Entra app ids) allowed to call this server, mapped to a display name.
    /// Matched against the token's <c>azp</c>/<c>appid</c> claim.
    /// </summary>
    public Dictionary<string, string> AllowedAgents { get; set; } = [];
}
