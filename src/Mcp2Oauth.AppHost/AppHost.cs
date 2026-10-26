var builder = DistributedApplication.CreateBuilder(args);

// Each project keeps the https port from its launch profile (7101-7103). The
// Entra registrations use those URLs, e.g. the web UI's redirect URI.
var mcpServer = builder.AddProject<Projects.Mcp2Oauth_McpServer>("mcpserver");
var mcpEndpoint = ReferenceExpression.Create($"{mcpServer.GetEndpoint("https")}/mcp");
mcpServer.WithEnvironment("McpServer__PublicUrl", mcpEndpoint);

var agent = builder.AddProject<Projects.Mcp2Oauth_Agent>("agent")
    .WithReference(mcpServer)
    .WaitFor(mcpServer)
    .WithEnvironment("Mcp__Endpoint", mcpEndpoint);

builder.AddProject<Projects.Mcp2Oauth_WebUI>("webui")
    .WithReference(agent)
    .WaitFor(agent)
    .WithEnvironment("Agent__BaseUrl", agent.GetEndpoint("https"))
    .WithExternalHttpEndpoints();

builder.Build().Run();
