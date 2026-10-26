# Mcp2Oauth: agent identity + on-behalf-of user identity with MCP

A .NET 10 demo of an MCP server (protocol revision **2026-07-28**, C# SDK 2.3) that sees
**two identities** on every request:

- **The agent**, the software calling the server, authenticated by Entra ID with its
  own credential.
- **The user** the agent is acting on behalf of (OBO).

The server's HTTP middleware sees both. The tools see only the OBO user, and they
rely on that user's claims (`oid`, `roles`) for data access and authorization.

```
 Browser ──OIDC──► Web UI (Blazor Server)          Entra ID (MarimerLLC tenant)
                    │  token: aud=Agent, scp=Agent.Chat       ▲   ▲
                    ▼                                         │   │
                  Agent API (Azure OpenAI + MCP client) ──────┘   │
                    │  OBO:  user token + agent secret ──► token: aud=MCP, azp=Agent, oid=user, scp=Mcp.Tools
                    │  App:  agent secret only          ──► token: aud=MCP, azp=Agent, roles=Mcp.Status.Read
                    ▼
                  MCP Server
                    ├─ CallerIdentityMiddleware: agent (azp) + user (oid) → allowlist, logging
                    ├─ Authorization policies per tool (also filter tools/list)
                    └─ Tools: IUserContext only (the OBO user)
```

| Project | URL | Role |
|---|---|---|
| `Mcp2Oauth.WebUI` | https://localhost:7103 | Blazor Web App, InteractiveServer. Users sign in here. |
| `Mcp2Oauth.Agent` | https://localhost:7102 | The agent. Protected API + confidential client. Runs the LLM tool loop. |
| `Mcp2Oauth.McpServer` | https://localhost:7101/mcp | MCP server (Streamable HTTP, stateless). |
| `Mcp2Oauth.Contracts` | | Shared DTOs. |
| `Mcp2Oauth.AppHost` | dashboard URL printed at startup | Aspire AppHost: runs all three services and the dashboard. |
| `Mcp2Oauth.ServiceDefaults` | | OpenTelemetry, health checks and resilience shared by the services. |

## How the agent authenticates (and why not a PAT)

A PAT or API key is a static shared secret that Entra knows nothing about. The MCP
server would have to store and check it, and nothing ties it to a user. Here the agent
is an **Entra confidential client** with its own credential: a client secret for
development, ideally a certificate, managed identity or federated credential in
production. Entra checks that credential whenever the agent asks for a token, and
records the result in the token:

- **On-behalf-of** ([OAuth 2.0 OBO](https://learn.microsoft.com/entra/identity-platform/v2-oauth2-on-behalf-of-flow)):
  the agent sends the user's token (audience = agent) plus its own credential, and
  gets back a token for the MCP server. That token contains the user (`oid`, `name`,
  `preferred_username`, `scp`, user `roles`) and the agent (`azp`). Entra only puts
  `azp` there after authenticating the agent, so the server can trust it.
- **App-only** (client credentials, the MCP
  [`io.modelcontextprotocol/oauth-client-credentials`](https://modelcontextprotocol.io/extensions/auth/oauth-client-credentials)
  extension): just the agent's credential. The token has `azp` and application `roles`,
  and no user.

Compare that with `clientInfo`, which the 2026-07-28 protocol has the client send in
every request's `_meta`. It's self-asserted. The `whoami` tool returns it next to the
attested `azp` so you can see the difference.

## What the demo shows

1. **Middleware sees both identities.** `CallerIdentityMiddleware` reads `azp` and the
   user claims, rejects agents that aren't on `McpServer:AllowedAgents` (403), and logs
   lines like:
   `tools/call list_my_expenses: agent Mcp2Oauth Agent (…) on behalf of rocky@… (…)`.
   It reads the new `Mcp-Method`/`Mcp-Name` headers (SEP-2243), so it never parses the
   JSON-RPC body.
2. **Tools see only the user.** `ExpenseTools` depends on `IUserContext`. Expenses are
   keyed by the user's `oid`, approval requires the user's `Expense.Approver` app role,
   and users can't approve their own expenses.
3. **Policies filter `tools/list`.** With `AddAuthorizationFilters()`, `[Authorize]` on a
   tool hides it from callers that fail the policy. In agent-only mode only `whoami` and
   `get_service_status` appear.
4. **Spec discovery.** An unauthenticated request gets
   `401` + `WWW-Authenticate: Bearer resource_metadata="…/.well-known/oauth-protected-resource/mcp"`,
   and that document names the Entra tenant as the authorization server.

## Setup

### 1. Entra app registrations

You need rights to create app registrations and grant admin consent in the tenant.

```powershell
az login --tenant <your-tenant-id-or-domain> --allow-no-subscriptions
./setup/Setup-Entra.ps1 -AssignApproverToMe
```

The script is idempotent. It creates three registrations (`Mcp2Oauth MCP Server`,
`Mcp2Oauth Agent`, `Mcp2Oauth Web UI`), configures v2 tokens, scopes, app roles and the
`idtyp` optional claim, grants admin consent, and writes all ids and secrets to each
project's **user secrets**. Nothing tenant-specific is committed. `-AssignApproverToMe`
gives you the `Expense.Approver` role so the approval tools appear for you.

### 2. Azure OpenAI (for chat)

The **Probe identity** button works without this. Chat needs a chat-model deployment:

```powershell
cd src/Mcp2Oauth.Agent
dotnet user-secrets set AzureOpenAI:Endpoint   https://<resource>.openai.azure.com/
dotnet user-secrets set AzureOpenAI:Deployment <deployment-name>
# Either an API key...
dotnet user-secrets set AzureOpenAI:ApiKey     <key>
# ...or leave ApiKey empty to use DefaultAzureCredential (az login / Visual Studio).
# Your account then needs the "Cognitive Services OpenAI User" role on the resource.
```

### 3. Run

Start everything with Aspire, either the `Mcp2Oauth.AppHost` project in Visual Studio or:

```powershell
dotnet run --project src/Mcp2Oauth.AppHost
```

The console prints the Aspire dashboard's login URL. The services keep their fixed ports
(7101-7103), which the Entra registrations depend on. You can still run each project on its
own with `dotnet run --launch-profile https`. Telemetry is then simply not exported.

Open https://localhost:7103, sign in, and:

- **Probe identity** in each mode. Compare the token claims, what the middleware saw,
  and which tools are visible.
- Chat in **OBO** mode: "List my expenses", "Submit a $45 expense for a team lunch",
  "What's waiting for my approval?".
- Switch to **Agent only** and ask "List my expenses". The agent can't see that tool,
  and says so. "What's the service status?" still works.
- Watch the identity flow in the Aspire dashboard (see below).

## Observability (Aspire + OpenTelemetry)

`Mcp2Oauth.ServiceDefaults` configures OpenTelemetry logs, traces and metrics for every
service and exports them to the Aspire dashboard over OTLP.

| Dashboard view | What to look for |
|---|---|
| **Structured logs** (`mcpserver`) | The middleware's `… agent {AgentName} on behalf of {UserName}` entries, with `AgentAppId`, `UserObjectId`, `McpMethod` and `McpName` as filterable properties. |
| **Traces** | One trace per chat message: `webui` → `agent` `POST /api/chat` → `Acquire MCP token (on-behalf-of)` or `(client credentials)` → MCP calls (`Experimental.ModelContextProtocol`) → `mcpserver` → Azure OpenAI `chat` spans (`Experimental.Microsoft.Extensions.AI`, which include prompts and tool arguments in Development). The `mcpserver` request spans carry `mcp2oauth.agent.*`, `mcp2oauth.token_kind`, `user.id` and `user.name` tags. |
| **Metrics** (`mcpserver`) | `mcp2oauth.mcp.requests`, by agent, token kind, MCP method, tool name and outcome (`allowed` or `rejected_agent`). The user is deliberately left off metric tags so the series count stays low. |
| **Metrics** (`agent`, `webui`) | `MicrosoftIdentityClient_Common_Meter`: MSAL token acquisitions and durations, showing cache hits versus calls to Entra. Plus the MCP SDK and gen_ai client metrics. |

Retries are disabled for POSTs in the shared resilience handler, so a tool call like
`submit_expense` is never sent twice. The web UI's call to the agent opts out of the
standard handler and uses a longer timeout, because one chat request covers a whole LLM
tool loop.

## Notes

- **Blazor 10 static assets.** The UI uses `MapStaticAssets()`, `<ResourcePreloader />`,
  `@Assets["…"]` for CSS, the favicon and `_framework/blazor.web.js`, and `<ImportMap />`.
  Authentication runs before `UseAntiforgery()`, and static asset endpoints don't
  require authorization.
- **Tokens in a Blazor circuit.** An interactive circuit has no current `HttpContext`,
  so `AgentClient` takes the user from `AuthenticationStateProvider` and passes it to
  MSAL explicitly. The token cache is in memory, so after a restart the cookie is
  rejected (`RejectSessionCookieWhenAccountNotInCacheEvents`) and you sign in again.
- **Client registration.** Entra doesn't support Client ID Metadata Documents or
  Dynamic Client Registration (now deprecated). The agent here is pre-registered, which
  is the normal enterprise setup, so it gets MCP server details from configuration
  rather than by discovery.
- **Production.** Replace client secrets with certificates, managed identity or
  federated identity credentials. Replace the in-memory token caches with a
  distributed cache. Consider
  [Microsoft Entra Agent ID](https://learn.microsoft.com/entra/agent-id/) for
  first-class agent identities.

## License

[MIT](LICENSE)
