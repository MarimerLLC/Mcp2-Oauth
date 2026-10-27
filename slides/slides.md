---
theme: default
title: Guarding the Gate
info: |
  Enterprise-grade MCP servers in .NET. Rockford Lhotka, Techorama NL 2026.
author: Rockford Lhotka
layout: cover
canvasWidth: 1280
aspectRatio: 16/9
colorSchema: light
transition: fade
lineNumbers: false
fonts:
  provider: none
defaults:
  layout: content
---

# Guarding the Gate

Enterprise-grade MCP servers in .NET <span class="sep">✕</span> Rockford Lhotka

<!--
Welcome. This session is about what happens after the MCP demo works: how to expose real business capabilities to AI agents in a way your security, compliance, and operations teams can live with. Everything is C# and .NET, using the official MCP C# SDK.
-->

---
layout: dark
bg: fleur
title: Beyond the toy tool
subtitle: Every castle has a side door
---

<div class="w-[760px]">
<div class="demo-box">

````md magic-move
```csharp
// The demo: harmless, charming, useless in production
[McpServerTool, Description("Gets the weather")]
public string GetWeather(string city) => ...
```
```csharp
// Monday morning: same shape, real data
[McpServerTool, Description("Finds customers")]
public Customer[] GetCustomer(string query) => ...
```
````

</div>
<p v-click class="text-[22px] text-[#bdb5aa] mt-10">Who can call it? &nbsp;What comes back? &nbsp;Who knows it happened?</p>
<p v-click class="question">How do we expose real capabilities to agents without building an ungoverned side door?</p>
</div>

<img src="/img/lady-window.png" alt="A lady in a pointed hat looking out of a castle window" class="absolute right-[-30px] bottom-[-34px] h-[400px]">

<style>
.demo-box {
  background: #1c1c1c;
  padding: 22px 28px 24px;
  margin-top: 30px;
}
/* Slide styles are scoped; :deep reaches into the Magic Move component. */
.demo-box :deep(pre),
.demo-box :deep(.slidev-code) {
  background: transparent !important;
  padding: 0 !important;
  margin: 0 !important;
  font-family: var(--gtg-mono-font) !important;
  font-size: 21px !important;
  line-height: 1.5 !important;
}
.question {
  color: var(--gtg-rust-light);
  font-weight: 700;
  font-size: 28px;
  line-height: 1.25;
  margin-top: 18px !important;
}
</style>

<!--
Every MCP tutorial ends with get_weather or echo. Great for learning the protocol shape. Now rename it get_customer and point it at production. Same few lines of code, completely different risk profile: who is allowed to call it, what data comes back, what happens with a vague query, and does anyone know it happened? That's the gap this session fills: treating the MCP server as a production integration boundary rather than a demo.
-->

---
title: The gatehouse
subtitle: An MCP server is an integration boundary, not a shortcut into your systems
---

<div class="flex items-stretch h-[198px] mt-[20px]">
  <div class="card gh-box">
    <IconBadge icon="helmet" color="rust" />
    <h3>Agent host + LLM</h3>
    <p class="muted">Claude, Copilot, your own agent</p>
  </div>
  <div class="gh-gap boundary"><FlowArrow /></div>
  <div class="card gh-box text-white" style="background: var(--gtg-rust)">
    <IconBadge icon="castle" />
    <h3>MCP server</h3>
    <p class="text-[#f1ddd8]">Thin adapter: identity, policy, contracts, audit</p>
  </div>
  <div class="gh-gap"><FlowArrow /></div>
  <div class="card gh-box">
    <IconBadge icon="rulebook" />
    <h3>Domain layer</h3>
    <p class="muted">The same business rules your UI and APIs use</p>
  </div>
  <div class="gh-gap"><FlowArrow /></div>
  <div class="card gh-box">
    <IconBadge icon="treasure-map" />
    <h3>Data &amp; systems</h3>
    <p class="muted">Databases, ERP, line-of-business APIs</p>
  </div>
</div>

<div class="flex text-[15px] font-bold rust uppercase mt-3 tracking-wide">
  <div class="w-[284px]">Untrusted</div>
  <div>Trust boundary <span class="sep">✕</span> <span class="normal-case">policy is enforced here</span></div>
</div>

<div class="grid grid-cols-3 gap-[50px] mt-[52px]">
  <div>
    <h3>A facade, not a back door</h3>
    <p class="muted mt-3">Tools call the domain layer your UI calls. Never route around business rules.</p>
  </div>
  <div>
    <h3>All inbound input is untrusted</h3>
    <p class="muted mt-3">The model, the client, and any content the model has read along the way.</p>
  </div>
  <div>
    <h3>Transport sets the trust model</h3>
    <p class="muted mt-3">stdio inherits the local user. Remote HTTP needs real identity and authorization.</p>
  </div>
</div>

<style>
.gh-box { flex: 1; display: flex; flex-direction: column; gap: 10px; }
.gh-box h3 { margin-top: 12px; }
.gh-box p { font-size: 18px; line-height: 1.35; }
.gh-gap { width: 64px; display: flex; align-items: center; justify-content: center; position: relative; }
.boundary::before {
  content: '';
  position: absolute;
  top: -32px;
  bottom: -46px;
  left: 10px;
  border-left: 2px dashed var(--gtg-rust);
}
</style>

<!--
Architecture first. The MCP server is a thin adapter: it should not contain business logic, and it must not bypass it. It calls the same domain layer (business objects, services, rules) your UI and APIs already use, so validation and authorization rules apply equally. The dashed line is the trust boundary: everything to the left is untrusted, including the model's arguments and any text the model picked up from documents or the web. Also call out transport: a local stdio server runs as the user on their machine; a remote HTTP server is a shared service and needs real authentication.
-->

---
title: Narrow, typed tools
subtitle: Hand out a key to one door, not the master key
---

<div class="grid grid-cols-[548px_1fr] gap-[34px]">
  <div class="flex flex-col gap-[20px]">
    <div class="card flex gap-5">
      <IconBadge icon="cancel" color="rust" :size="52" />
      <div>
        <h3 class="rust">Data-shaped (avoid)</h3>
        <div class="mono text-[17.5px] mt-3 leading-[1.6]">UpdateTable(table, json)<br>Search(query)</div>
      </div>
    </div>
    <div class="card flex gap-5">
      <IconBadge icon="check" color="green" :size="52" />
      <div>
        <h3 class="green">Capability-shaped (prefer)</h3>
        <div class="mono text-[17.5px] mt-3 leading-[1.6]">GetOrderStatus(orderNumber)<br>ListOpenInvoices(customerId, dueBefore)<br>RequestRefund(orderNumber, amount, reason)</div>
      </div>
    </div>
    <ul class="bullets mt-2">
      <li>Verbs over business operations, not tables</li>
      <li>Typed parameters: IDs, enums, bounded ranges</li>
      <li>Descriptions are a contract: when to use it, and when not to</li>
    </ul>
  </div>
  <Panel label="C# ✕ MCP C# SDK" class="h-[460px]">

```csharp {all|1-2|4-5|6-7|8-11|12}
[McpServerToolType]
public sealed class OrderTools(IOrderService orders)
{
    [McpServerTool(Name = "get_order_status",
        ReadOnly = true, Idempotent = true)]
    [Description("Gets status for ONE order the " +
        "caller can access. Not for search.")]
    public Task<OrderStatusDto> GetOrderStatus(
        [Description("Order number, e.g. SO-104233")]
        string orderNumber,
        CancellationToken ct)
        => orders.GetStatusAsync(orderNumber, ct);
}
```

  </Panel>
</div>

<!--
Design tools around business capabilities, not data access. A data-shaped tool hands the model your schema and lets it compose its own operations; a capability-shaped tool encodes exactly one thing the business allows. Parameters should be typed and constrained: an order number with a known format, an enum for status, a bounded date range. Descriptions are read by the model, so write them like an API contract: what it does, when to use it, when not to. In the C# SDK this is a class with [McpServerToolType], methods with [McpServerTool] and [Description], and constructor or parameter injection for your domain services. Note the method is a one-liner: the adapter delegates to the domain layer.
-->

---
title: Here be dragons
subtitle: Anti-patterns that turn a gatehouse into a side door
frame: true
---

<div class="grid grid-cols-3 gap-x-[28px] gap-y-[24px] mt-[4px]">
  <div class="card dragon">
    <div class="dragon-head"><IconBadge icon="skull" color="rust" :size="54" /><h3>Generic query tools</h3></div>
    <p class="muted">RunSql, ExecuteOData, CallAnyEndpoint. The model now writes your access policy.</p>
  </div>
  <div class="card dragon">
    <div class="dragon-head"><IconBadge icon="secret-door" color="rust" :size="54" /><h3>Overbroad filesystem access</h3></div>
    <p class="muted">A whole drive or repo root exposed: path traversal, secrets, and source code in context.</p>
  </div>
  <div class="card dragon">
    <div class="dragon-head"><IconBadge icon="scroll" color="rust" :size="54" /><h3>Ambiguous natural-language parameters</h3></div>
    <p class="muted">“Recent orders for the big client.” The server guesses, and guesses differ between runs.</p>
  </div>
  <div class="card dragon">
    <div class="dragon-head"><IconBadge icon="dragon" color="rust" :size="54" /><h3>LLM-decided irreversible actions</h3></div>
    <p class="muted">Delete, pay, ship, or email with no policy gate and no human approval.</p>
  </div>
  <div class="card dragon">
    <div class="dragon-head"><IconBadge icon="key" color="rust" :size="54" /><h3>God-mode service accounts</h3></div>
    <p class="muted">Every call runs as an admin, and token pass-through erases who the user really was.</p>
  </div>
  <div class="card dragon">
    <div class="dragon-head"><IconBadge icon="stone-wall" color="rust" :size="54" /><h3>Tool sprawl</h3></div>
    <p class="muted">Hundreds of overlapping tools: selection accuracy drops while the attack surface grows.</p>
  </div>
</div>

<style>
.dragon { height: 214px; }
.dragon-head { display: flex; align-items: center; gap: 18px; margin-bottom: 18px; }
.dragon p { font-size: 18.5px; }
</style>

<!--
The rogues' gallery. Generic query tools are the most common: they feel flexible but effectively delegate authorization to the model. Broad filesystem access leaks secrets and invites path traversal. Natural-language parameters push interpretation into the server, so the same request behaves differently from run to run. Irreversible actions decided purely by the model, with no policy, are an incident waiting to happen. God-mode service accounts and token pass-through destroy the audit trail. And tool sprawl hurts both accuracy and security. We'll fix each of these in the next slides.
-->

---
title: Contracts in, contracts out
subtitle: Written in ink and sealed with wax
---

<div class="flex items-stretch h-[318px]">
  <div class="card flex-1">
    <div class="flex items-center gap-5 mb-6"><IconBadge icon="shield" :size="60" /><h3>Validate inputs</h3></div>
    <ul class="bullets text-[18.5px]">
      <li>Allowlists, ranges, lengths, formats</li>
      <li>Same rules as the domain layer</li>
      <li>Reject early, with a message the model can fix</li>
    </ul>
  </div>
  <div class="w-[52px] flex items-center justify-center"><FlowArrow :length="36" /></div>
  <div class="card flex-1">
    <div class="flex items-center gap-5 mb-6"><IconBadge icon="tied-scroll" :size="60" /><h3>Deterministic output</h3></div>
    <ul class="bullets text-[18.5px]">
      <li>Structured content with an output schema</li>
      <li>Stable ordering, explicit units and time zones</li>
      <li>Paginate with hard caps; no prose blobs</li>
    </ul>
  </div>
  <div class="w-[52px] flex items-center justify-center"><FlowArrow :length="36" /></div>
  <div class="card flex-1">
    <div class="flex items-center gap-5 mb-6"><IconBadge icon="wax-seal" :size="60" /><h3>Versioned contracts</h3></div>
    <ul class="bullets text-[18.5px]">
      <li>Tool names and schemas are a public API</li>
      <li>Additive changes; a new shape is a new version</li>
      <li>Deprecate deliberately; signal with <code>list_changed</code></li>
    </ul>
  </div>
</div>

<div class="banner flex items-center gap-6 mt-[28px]">
  <IconBadge icon="magnifier" :size="56" />
  <p><b>Prove it in CI.</b> Snapshot <code>tools/list</code> as a contract test, and run evals with real models to catch description drift before agents do.</p>
</div>

<!--
Treat each tool as a contract. Inbound: validate everything at the boundary, using the same rules as your domain layer, and return errors the model can act on. Outbound: return structured content with an output schema rather than formatted prose. Keep ordering stable, units explicit, and always paginate with a hard cap so one call can't dump a table into the context window. Over time: tool names and schemas are a public API. Make additive changes, introduce new versions for breaking shapes, and deprecate on purpose. Then enforce it: snapshot the tools/list output in a contract test, and run evals against real models, because a description tweak can change which tool the model picks.
-->

---
title: Safe errors and redaction
subtitle: Tell the model what to fix, never where the treasure is
---

<div class="grid grid-cols-[552px_1fr] gap-[40px]">
  <div class="flex flex-col gap-[18px]">
    <Panel label="✗ Leaky" class="h-[202px]">

```text
SqlException: Login failed for user
  'svc_orders' on PRDSQL01.corp.local
  at Contoso.Orders.Data.OrderRepo
  .GetAsync(String id) in D:\src\...
  ConnStr: Server=PRDSQL01;User=...
```

  </Panel>
  <Panel label="✓ Actionable" class="h-[232px]">

```json
{
  "isError": true,
  "code": "ORDER_NOT_FOUND",
  "message": "No accessible order SO-104233.",
  "hint": "Check the number, or call list_orders.",
  "correlationId": "7f3c2e91"
}
```

  </Panel>
  </div>
  <div class="flex flex-col gap-[14px] feature-tight">
    <Feature icon="anvil" title="Tool errors vs. protocol errors">Business failures return isError so the model can recover; protocol errors are for malformed requests.</Feature>
    <Feature icon="signpost.svg" title="Hint the next move">Tell the agent what to try instead: a corrected argument, another tool, or when to retry.</Feature>
    <Feature icon="padlock" title="Redact at serialization">Classify fields (PII, financial, secret) and mask on the way out, centrally, not tool by tool.</Feature>
    <Feature icon="torch" title="Returned data is untrusted too">Notes and documents can carry prompt injection. Label, fence, or strip it.</Feature>
    <Feature icon="quill" title="Details stay server-side">The response gets a correlation ID; your logs get the stack trace.</Feature>
  </div>
</div>

<style>
.feature-tight .feature-desc { font-size: 17.5px; margin-top: 3px; }
</style>

<!--
Two kinds of failure. Protocol errors mean the request itself is broken. Tool errors, flagged with isError, are business outcomes the model should see and recover from: not found, not permitted, invalid input. The left side shows the difference: the leaky version hands an attacker your server name, account, and code layout; the actionable version tells the model what to do next and gives ops a correlation ID. Redaction should be centralized: classify fields and mask during serialization so a new tool can't forget. And remember that data you return can itself carry prompt injection, for example a customer note that says 'ignore previous instructions'. Treat it as untrusted content.
-->

---
title: Identity and least privilege
subtitle: Not every knight gets every key
---

<div class="grid grid-cols-[600px_1fr] gap-[44px]">
  <div class="flex flex-col gap-[30px] pt-1">
    <Feature num="1" color="rust" title="Authenticate">OAuth 2.1 on the HTTP transport, protected resource metadata, Entra ID or your IdP.</Feature>
    <Feature num="2" color="rust" title="Act as the user">On-behalf-of flows, not a shared account. Never pass the client's token through to downstream APIs.</Feature>
    <Feature num="3" color="rust" title="Authorize per tool">ASP.NET Core policies and scopes on every tool. Deny by default.</Feature>
    <Feature num="4" color="rust" title="Scope the data">Tenant, region, and row-level filters enforced in the domain layer.</Feature>
  </div>
  <div class="flex flex-col gap-[24px] pt-1">
    <div class="card h-[210px]">
      <div class="flex items-center gap-5 mb-6"><IconBadge icon="key-lock" :size="58" /><h3>Effective permission</h3></div>
      <p>What the user may do <b>∩</b> what this agent is granted. Least privilege applies to both.</p>
    </div>
    <div class="card h-[210px]">
      <div class="flex items-center gap-5 mb-6"><IconBadge icon="door" :size="58" /><h3>Show only usable doors</h3></div>
      <p>Filter <code>tools/list</code> per caller, so agents never see or try tools they can't invoke.</p>
    </div>
  </div>
</div>

<!--
Four gates. First, authenticate: the MCP authorization spec builds on OAuth 2.1, with the server publishing protected resource metadata so clients can discover your identity provider, typically Entra ID in .NET shops. Second, act on behalf of the user rather than as a powerful service account, and don't forward the client's token to downstream APIs; exchange it. Third, authorize each tool with standard ASP.NET Core policies. Fourth, scope the data itself in the domain layer. The effective permission is the intersection of what the user can do and what this particular agent has been granted. And filter the tool list per caller: tools a caller can't use shouldn't even be advertised.
-->

---
title: Two principals, one request
subtitle: Know the messenger, and the lord who sent them
---

<div class="grid grid-cols-[560px_1fr] gap-[54px]">
  <div class="flex flex-col gap-[26px] pt-1">
    <Feature icon="user.svg" title="User principal">Whose rights apply: oid, tenant, roles. The user's permissions are the ceiling.</Feature>
    <Feature icon="agent.svg" title="Agent identity">Who is asking: the client app (azp) with its own consented scopes. A principal, not a proxy.</Feature>
    <Feature icon="delegation.svg" title="Delegation keeps both">On-behalf-of or token exchange carries the user as subject and the agent as client or actor.</Feature>
    <Feature icon="claims.svg" title="Claims, not tokens">ASP.NET Core validates the token; tools read claims from HttpContext.User.</Feature>
  </div>
  <Panel label="Access token claims" class="h-[456px]">

```jsonc {all|5-8|10-12}
{
  "aud": "api://orders-mcp",
  "tid": "9188040d-6c67-4c5b",

  // user principal
  "oid": "4f2a91c7-0e1d-4b8a",
  "preferred_username": "jdoe@contoso.com",
  "roles": [ "Orders.Reader" ],

  // agent identity
  "azp": "orders-assistant",
  "scp": "orders.read refunds.request"
}
```

<div class="h-5" />

```csharp
// in the tool, user = HttpContext.User
user.FindFirstValue("oid");  // who
user.FindFirstValue("azp");  // which agent
```

  </Panel>
</div>

<!--
DRAFT (the original notes on this slide were copied from the observability slide):
Every request carries two identities. The user principal (oid, tenant, roles) decides whose rights apply, and the user's permissions are the ceiling. The agent is a principal too: the client app, identified by azp, with its own consented scopes. It is not just a proxy for the user. Delegation, through on-behalf-of or token exchange, keeps both: the user as subject, the agent as client or actor. In code, ASP.NET Core validates the token and the tool reads claims from HttpContext.User. Tools never parse or forward raw tokens.
-->

---
title: Reads vs. side effects
subtitle: No royal decree without the seal
---

<div class="grid grid-cols-2 gap-[30px]">
  <div class="card h-[178px]">
    <div class="flex items-center gap-5 mb-4"><IconBadge icon="magnifier" color="green" :size="58" /><h3 class="green">Read-only tools</h3></div>
    <p>Safe to retry, broadly available, lower approval bar. Still authorized, still audited.</p>
  </div>
  <div class="card h-[178px]">
    <div class="flex items-center gap-5 mb-4"><IconBadge icon="anvil" color="rust" :size="58" /><h3 class="rust">Side-effecting tools</h3></div>
    <p>Narrow scope, policy gated, idempotent where possible. Often a separate server or deployment.</p>
  </div>
</div>

<div class="flex items-center mt-[32px]">
  <div class="flow-step">
    <h3>Model proposes</h3>
    <p class="mono">draft_refund(...)</p>
  </div>
  <div v-click="1" class="flow-gap"><FlowArrow :length="32" /></div>
  <div v-click="1" class="flow-step">
    <h3>Policy evaluates</h3>
    <p>limits, roles, risk</p>
  </div>
  <div v-click="2" class="flow-gap"><FlowArrow :length="32" /></div>
  <div v-click="2" class="flow-step">
    <h3>Human approves</h3>
    <p>when policy requires</p>
  </div>
  <div v-click="3" class="flow-gap"><FlowArrow :length="32" /></div>
  <div v-click="3" class="flow-step" style="background: var(--gtg-rust)">
    <h3>Commit</h3>
    <p>with idempotency key</p>
  </div>
</div>

<div class="card flex items-center gap-6 mt-[30px] py-[18px]">
  <IconBadge icon="wax-seal" color="rust" :size="58" />
  <p class="text-[20px]">Annotations (ReadOnly, Destructive, Idempotent) are hints to the client, not enforcement. <b class="rust">Authority comes from server-side policy, never from the model's judgment.</b></p>
</div>

<style>
.flow-step {
  flex: 1;
  background: #1c1c1c;
  color: #fff;
  border-radius: 8px;
  height: 110px;
  text-align: center;
  display: flex;
  flex-direction: column;
  justify-content: center;
  gap: 12px;
}
.flow-step p { color: var(--gtg-gold); font-size: 18px; }
.flow-gap { width: 40px; display: flex; justify-content: center; }
</style>

<!--
Separate reads from writes, by tool and often by deployment, so you can grant read access broadly and write access narrowly. The SDK lets you mark tools ReadOnly, Destructive, or Idempotent, and well-behaved clients use those hints to decide when to ask the user. But they're hints. The real control is a propose-and-commit flow: the model drafts the action, your policy engine evaluates limits and roles, a human approves when policy says so (elicitation or an approval workflow), and only then do you commit, with an idempotency key so retries don't double-refund. The model never gets to decide an irreversible action on its own.
-->

---
title: Timeouts, cancellation, and long work
subtitle: Every siege has a deadline
---

<div class="grid grid-cols-[566px_1fr] gap-[50px]">
  <div class="flex flex-col gap-[26px] pt-1">
    <Feature icon="hourglass" title="Flow the CancellationToken">From the tool method through EF Core, HttpClient, and the domain layer.</Feature>
    <Feature icon="sands" title="Per-tool time budgets">A linked token sized to the operation, not to the client's patience.</Feature>
    <Feature icon="stone-wall" title="Resilience pipelines">Rate limits, bulkheads, and retries with Microsoft.Extensions.Resilience.</Feature>
    <Feature icon="trumpet-flag" title="Agents retry aggressively">Design for duplicates: idempotency keys and safe re-reads.</Feature>
  </div>
  <div class="card h-[456px] !rounded-[22px]">
    <h3 class="mb-5">Long-running work: the job-handle pattern</h3>
    <div class="job">
      <div class="call">start_report_export(...)</div>
      <div>→ { jobId, status: "queued" }</div>
    </div>
    <div class="flex justify-center"><FlowArrow dir="down" :length="26" /></div>
    <div class="job">
      <div class="call">get_job_status(jobId)</div>
      <div>→ { status: "running", progress: 60 }</div>
    </div>
    <div class="flex justify-center"><FlowArrow dir="down" :length="26" /></div>
    <div class="job">
      <div class="call">get_job_result(jobId)</div>
      <div>→ { downloadUrl, expiresAt }</div>
    </div>
    <p class="muted italic mt-5">Send progress notifications while it runs.</p>
  </div>
</div>

<style>
.job {
  background: #1c1c1c;
  color: #d4d4d4;
  border-radius: 8px;
  padding: 12px 20px;
  font-family: var(--gtg-mono-font);
  font-size: 16.5px;
  display: flex;
  flex-direction: column;
  gap: 14px;
}
.job .call { color: #dcdcaa; }
</style>

<!--
Agents don't wait politely. Every tool should accept a CancellationToken and pass it all the way down: EF Core, HttpClient, your domain calls. Wrap it in a linked token with a per-tool time budget. Put rate limiting, bulkheads, and retries in a resilience pipeline rather than hand-rolling them. And assume duplicates: agents retry on timeouts, so side-effecting tools need idempotency keys. For work that legitimately takes minutes, don't hold the call open; return a job handle immediately and expose status and result tools, sending progress notifications while it runs.
-->

---
title: Sagas for work that takes weeks
subtitle: A campaign you can call off, one step at a time
---

<div class="card !rounded-[22px] !py-[16px]">
  <div class="flex items-center justify-between mb-3">
    <h3>One expense request, four steps, three weeks</h3>
    <div class="saga-id">sagaId: "sg-7f3c2e91"</div>
  </div>
  <div class="saga">
    <div class="t">T+0</div><div /><div class="t">T+2h</div><div /><div class="t">T+3d</div><div /><div class="t">T+2w</div>
    <div class="step"><b>Agent</b><span class="mono">reserve_budget()</span></div>
    <div class="gap"><FlowArrow :length="28" /></div>
    <div v-click="1" class="step"><b>Agent</b><span class="mono">hold_vendor_slot()</span></div>
    <div v-click="1" class="gap"><FlowArrow :length="28" /></div>
    <div v-click="2" class="step"><b>Human</b><span class="text-[#bdb5aa]">manager approves</span></div>
    <div v-click="2" class="gap"><FlowArrow :length="28" /></div>
    <div v-click="3" class="step"><b>Agent</b><span class="mono">issue_po()</span></div>
    <div class="dash" /><div /><div class="dash" /><div /><div class="dash" /><div /><div class="dash" />
    <div class="undo"><b>Undo</b><span class="mono">release_budget()</span></div>
    <div class="gap"><FlowArrow dir="left" color="#8b2a22" :length="28" /></div>
    <div class="undo"><b>Undo</b><span class="mono">release_slot()</span></div>
    <div class="gap"><FlowArrow dir="left" color="#8b2a22" :length="28" /></div>
    <div class="undo"><b>Undo</b><span class="mono">withdraw_request()</span></div>
    <div class="gap"><FlowArrow dir="left" color="#8b2a22" :length="28" /></div>
    <div class="undo"><b>Undo</b><span class="mono">void_po()</span></div>
  </div>
  <p class="mt-3 text-[17px] italic muted"><span class="mono not-italic rust">cancel_saga(sagaId)</span> stops new steps and runs the undo for each completed step, newest first</p>
</div>

<div class="grid grid-cols-3 gap-[24px] mt-[20px] saga-features">
  <Feature icon="link.svg" title="One ID, end to end" :size="62">Every tool call, approval, log line and audit entry carries the sagaId, across agents and people.</Feature>
  <Feature icon="undo.svg" title="Every step has an undo" :size="62">ACID can't span weeks, so each step ships an idempotent, retry-safe compensation.</Feature>
  <Feature icon="stop.svg" title="Cancel is a tool" :size="62">Agents and humans can call off the saga; status reports what was undone.</Feature>
</div>

<style>
.saga-id {
  background: #1c1c1c;
  color: #dcdcaa;
  font-family: var(--gtg-mono-font);
  font-size: 16px;
  border-radius: 6px;
  padding: 6px 60px;
}
.saga {
  display: grid;
  grid-template-columns: 1fr 36px 1fr 36px 1fr 36px 1fr;
  align-items: center;
}
.saga .t { font-style: italic; font-size: 15px; color: var(--gtg-muted); padding-bottom: 6px; }
.saga .step, .saga .undo {
  border-radius: 6px;
  padding: 10px 18px;
  height: 74px;
  display: flex;
  flex-direction: column;
  justify-content: center;
  gap: 2px;
  font-size: 17px;
}
.saga .step { background: #1c1c1c; color: #e8e8e8; }
.saga .step b { color: var(--gtg-amber); text-transform: uppercase; font-size: 16px; }
.saga .undo { background: #fff; border: 2px solid var(--gtg-rust); }
.saga .undo b { color: var(--gtg-rust); text-transform: uppercase; font-size: 16px; }
.saga .gap { display: flex; justify-content: center; }
.saga .dash { height: 46px; justify-self: center; border-left: 2px dashed #5a524a; }
.saga-features .feature-desc { font-size: 17.5px; }
</style>

<!--
DRAFT (the original notes on this slide were copied from the timeouts slide):
Some business processes take weeks, not seconds: reserve budget, hold a vendor slot, wait for a manager, issue a PO. You can't hold a transaction open that long, so model it as a saga. One sagaId flows through every tool call, approval, log line and audit entry. Each step ships a compensating action that is idempotent and safe to retry. And cancel is itself a tool: cancel_saga stops new steps and runs the undo for each completed step, newest first, so agents and humans can call off the campaign cleanly.
-->

---
title: Resources and prompts
subtitle: "Scrolls and spells: useful, but mind what they unlock"
---

<div class="grid grid-cols-2 gap-[30px]">
  <div class="card h-[334px] rp">
    <div class="flex items-center gap-6 mb-6">
      <IconBadge icon="scroll" :size="64" />
      <div><h3>Resources</h3><p class="muted italic text-[18px] mt-1">Application-controlled context</p></div>
    </div>
    <p><b>Helps:</b> reference data, schemas, policy docs, catalogs</p>
    <p><b class="rust">Risks:</b> context flooding, leaked sensitive data, stale content taken as truth</p>
    <p><b>Do:</b> scope, size-limit, and authorize every resource URI</p>
  </div>
  <div class="card h-[334px] rp">
    <div class="flex items-center gap-6 mb-6">
      <IconBadge icon="spellbook" :size="64" />
      <div><h3>Prompts</h3><p class="muted italic text-[18px] mt-1">User-invoked workflow templates</p></div>
    </div>
    <p><b>Helps:</b> encode known-good paths, like “triage a refund request”</p>
    <p><b class="rust">Risks:</b> prompt text that implies authority the caller doesn't have</p>
    <p><b>Do:</b> treat prompts as UX; enforce authority in tools and policy</p>
  </div>
</div>

<div class="banner text-center font-bold text-[22px] mt-[26px] !py-[26px]">
  Give the model enough context to act, not enough to roam, and never authority by suggestion.
</div>

<style>
.rp > p { margin-bottom: 12px; }
</style>

<!--
Resources and prompts get treated as magic shortcuts. They're design elements. Resources are application-controlled context: great for reference data, schemas, and policy documents, but every byte you attach costs context and may leak data, so scope them, cap their size, and authorize access to each URI just like a tool. Prompts are user-invoked templates that encode a good workflow. The risk is that a prompt reads like a grant of authority ('you may approve refunds up to 500'); the model may believe it, but the server must not. Authority lives in tools and policy.
-->

---
title: Audit and accountability
subtitle: Keep the royal chronicle
---

<div class="grid grid-cols-[566px_1fr] gap-[50px]">
  <div class="flex flex-col gap-[26px] pt-1">
    <Feature icon="compass" title="Audit every call">Who, which agent, which tool, redacted arguments, and outcome. Reads too, not just writes.</Feature>
    <Feature icon="eye-target" title="Record the decision">The policy that applied, allow or deny, and who approved. A denial is evidence too.</Feature>
    <Feature icon="quill" title="Append-only, kept apart">A separate, tamper-evident store with its own retention, not your debug logs.</Feature>
    <Feature icon="watchtower" title="Linked to the trace">The correlation ID joins each record to its trace, so an auditor can replay the request.</Feature>
  </div>
  <Panel label="Audit record" class="h-[456px]">

```json {all|4-9|10-11|3}
{
  "timestamp": "2026-11-04T14:32:07Z",
  "correlationId": "7f3c2e91",
  "user": "jdoe@contoso.com",
  "agent": "orders-assistant",
  "tool": "request_refund@v2",
  "args": { "orderNumber": "SO-104233",
            "amount": 125.00,
            "reason": "[redacted]" },
  "policy": "refund.requiresApproval",
  "decision": "pending-approval",
  "durationMs": 184
}
```

  </Panel>
</div>

<!--
If you can't see it, security won't approve it. Trace each call end to end with OpenTelemetry so an agent action can be followed into your domain layer and database. Track the metrics that reveal misuse: denial counts, error rates, and drift in which tools get called. Then keep a real audit trail, separate from operational logs, append-only, with its own retention. Each record captures the user, the agent, the tool and version, redacted arguments, the policy decision, and the outcome. This is what lets you answer 'what did the agent do on behalf of this user last Tuesday?'
-->

---
title: Logs, metrics, and traces
subtitle: Lanterns on every tower, one watch for the whole keep
---

<div class="grid grid-cols-[566px_1fr] gap-[50px]">
  <div class="flex flex-col gap-[26px] pt-1">
    <Feature icon="trace.svg" title="One span per tool call">An ActivitySource span with tool, agent, and outcome; trace context links it to the agent and the domain.</Feature>
    <Feature icon="metrics.svg" title="Meter the gate">Counters and histograms for calls, latency, errors, and denials. Tag by tool and agent, never by user.</Feature>
    <Feature icon="logs.svg" title="Structured, redacted logs">ILogger message templates exported over OTLP, stamped with the trace ID. Scrub arguments first.</Feature>
    <Feature icon="export.svg" title="One pipeline, any backend">OTLP to the Aspire dashboard in dev; Azure Monitor, Grafana, or Datadog in production.</Feature>
  </div>
  <Panel label="OpenTelemetry setup" class="h-[456px] otel">

```csharp {all|3-7,14-16|8-10,17-19|11|12}
builder.Services.AddOpenTelemetry()
  .ConfigureResource(r => r.AddService("orders-mcp"))
  .WithTracing(t => t
    .AddSource("ModelContextProtocol")
    .AddSource(McpTelemetry.Name)
    .AddAspNetCoreInstrumentation()
    .AddHttpClientInstrumentation())
  .WithMetrics(m => m
    .AddMeter(McpTelemetry.Name)
    .AddAspNetCoreInstrumentation())
  .WithLogging()
  .UseOtlpExporter();

// inside a tool
using var span = McpTelemetry.Source
  .StartActivity("get_order_status");
McpTelemetry.ToolCalls.Add(1,
  new("tool", "get_order_status"),
  new("agent", agentId));
```

  </Panel>
</div>

<style>
.otel .slidev-code { font-size: 14px !important; line-height: 1.3 !important; }
</style>

<!--
If you can't see it, security won't approve it. Trace each call end to end with OpenTelemetry so an agent action can be followed into your domain layer and database. Track the metrics that reveal misuse: denial counts, error rates, and drift in which tools get called. Then keep a real audit trail, separate from operational logs, append-only, with its own retention. Each record captures the user, the agent, the tool and version, redacted arguments, the policy decision, and the outcome. This is what lets you answer 'what did the agent do on behalf of this user last Tuesday?'
-->

---
title: Hardening one tool
subtitle: From side door to gatehouse, one click at a time
---

<div class="grid grid-cols-[1fr_330px] gap-[28px]">
<Panel label="C# ✕ one refund tool, five gates" class="h-[482px] harden">

````md magic-move
```csharp
[McpServerTool, Description("Does refunds")]
public string Refund(string request) => db.Execute(request);
```
```csharp
[McpServerTool(Name = "request_refund", Destructive = true)]
[Description("Requests a refund on ONE order the caller can access. " +
    "Large refunds wait for a human approver.")]
public async Task<RefundResult> RequestRefund(
    [Description("Order number, e.g. SO-104233")] string orderNumber,
    [Description("Amount in EUR, at most the order total")] decimal amount,
    [Description("The customer's reason")] string reason,
    CancellationToken ct)
{
    return await refunds.CommitAsync(orderNumber, amount, reason, ct);
}
```
```csharp
[McpServerTool(Name = "request_refund", Destructive = true)]
[Authorize(Policy = Policies.RefundRequester)]
[Description("Requests a refund on ONE order the caller can access. " +
    "Large refunds wait for a human approver.")]
public async Task<RefundResult> RequestRefund(
    [Description("Order number, e.g. SO-104233")] string orderNumber,
    [Description("Amount in EUR, at most the order total")] decimal amount,
    [Description("The customer's reason")] string reason,
    CancellationToken ct)
{
    return await refunds.CommitAsync(orderNumber, amount, reason, ct);
}
```
```csharp
[McpServerTool(Name = "request_refund", Destructive = true)]
[Authorize(Policy = Policies.RefundRequester)]
[Description("Requests a refund on ONE order the caller can access. " +
    "Large refunds wait for a human approver.")]
public async Task<RefundResult> RequestRefund(
    [Description("Order number, e.g. SO-104233")] string orderNumber,
    [Description("Amount in EUR, at most the order total")] decimal amount,
    [Description("The customer's reason")] string reason,
    CancellationToken ct)
{
    if (amount <= 0)
        throw new McpException("Amount must be greater than zero.");

    return await refunds.CommitAsync(orderNumber, amount, reason, ct);
}
```
```csharp
[McpServerTool(Name = "request_refund", Destructive = true)]
[Authorize(Policy = Policies.RefundRequester)]
[Description("Requests a refund on ONE order the caller can access. " +
    "Large refunds wait for a human approver.")]
public async Task<RefundResult> RequestRefund(
    [Description("Order number, e.g. SO-104233")] string orderNumber,
    [Description("Amount in EUR, at most the order total")] decimal amount,
    [Description("The customer's reason")] string reason,
    CancellationToken ct)
{
    if (amount <= 0)
        throw new McpException("Amount must be greater than zero.");

    var draft = await refunds.DraftAsync(orderNumber, amount, reason, ct);
    var decision = await policy.EvaluateAsync(draft, ct);
    if (!decision.Allowed)
        return RefundResult.Pending(draft, decision.Reason);

    return await refunds.CommitAsync(draft, ct);
}
```
```csharp
[McpServerTool(Name = "request_refund", Destructive = true)]
[Authorize(Policy = Policies.RefundRequester)]
[Description("Requests a refund on ONE order the caller can access. " +
    "Large refunds wait for a human approver.")]
public async Task<RefundResult> RequestRefund(
    [Description("Order number, e.g. SO-104233")] string orderNumber,
    [Description("Amount in EUR, at most the order total")] decimal amount,
    [Description("The customer's reason")] string reason,
    CancellationToken ct)
{
    if (amount <= 0)
        throw audit.Denied("Amount must be greater than zero.");

    var draft = await refunds.DraftAsync(orderNumber, amount, reason, ct);
    var decision = await policy.EvaluateAsync(draft, ct);
    if (!decision.Allowed)
        return audit.Referred(RefundResult.Pending(draft, decision.Reason));

    return audit.Succeeded(await refunds.CommitAsync(draft, ct));
}
```
````

</Panel>
<div class="flex flex-col gap-[10px]">
  <div class="hstep" :class="{ on: $clicks >= 1, now: $clicks === 1 }"><span class="n">1</span><div><b>Narrow, typed, described</b><p>Contracts in, contracts out</p></div></div>
  <div class="hstep" :class="{ on: $clicks >= 2, now: $clicks === 2 }"><span class="n">2</span><div><b>Authorized per tool</b><p>Identity and least privilege</p></div></div>
  <div class="hstep" :class="{ on: $clicks >= 3, now: $clicks === 3 }"><span class="n">3</span><div><b>Rejects with a fixable error</b><p>Safe errors and redaction</p></div></div>
  <div class="hstep" :class="{ on: $clicks >= 4, now: $clicks === 4 }"><span class="n">4</span><div><b>Policy decides, not the model</b><p>Reads vs. side effects</p></div></div>
  <div class="hstep" :class="{ on: $clicks >= 5, now: $clicks === 5 }"><span class="n">5</span><div><b>Every outcome on the record</b><p>Audit and accountability</p></div></div>
</div>
</div>

<style>
.harden :deep(pre),
.harden :deep(.slidev-code) {
  font-size: 14px !important;
  line-height: 1.4 !important;
}
.hstep {
  display: flex;
  gap: 16px;
  align-items: center;
  padding: 12px 16px;
  border-radius: 12px;
  opacity: 0.35;
  transition: opacity 0.3s, background-color 0.3s;
}
.hstep.on { opacity: 1; }
.hstep.now { background: var(--gtg-card); }
.hstep .n {
  width: 40px;
  height: 40px;
  border-radius: 50%;
  background: var(--gtg-amber);
  color: #fff;
  font-weight: 700;
  font-size: 18px;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}
.hstep b { font-size: 18.5px; }
.hstep p { font-size: 16px; color: var(--gtg-muted); font-style: italic; }
</style>

<!--
Let's put the last half hour into one tool. Here's the side door: a tool called Refund that takes a string and hands it to the database. Click. Now it's narrow and typed: one business operation, an order number, an amount, a reason, and a description written like a contract. Click. It's authorized per tool, so only callers who pass the refund policy can even see it. Click. Bad input comes back as an error the model can fix, not a stack trace. Click. The model proposes, the server's policy decides: large refunds come back pending for a human. Click. And every outcome, denied, referred or committed, lands in the audit trail with the user and the agent. Same tool, five gates.
-->

---
layout: dark
title: The castle, assembled
subtitle: A practical architecture for MCP servers in real .NET systems
---

<div class="flex items-stretch h-[276px]">
  <div class="card w-[212px] flex flex-col items-center justify-center text-center gap-3">
    <IconBadge icon="helmet" color="rust" :size="72" />
    <h3 class="text-white mt-2">Agent host + LLM</h3>
    <p class="muted text-[18px] leading-tight">Claude, Copilot, your own agent</p>
  </div>
  <div class="w-[42px] flex items-center justify-center"><FlowArrow color="#e8a33d" :length="32" /></div>
  <div class="flex-1 border border-[#e8a33d] rounded-[16px] bg-[#232323] px-[22px] py-[14px]">
    <div class="label">MCP server <span class="sep">✕</span> <span class="normal-case">thin adapter</span></div>
    <div class="grid grid-cols-2 gap-[10px] mt-3 castle-tiles">
      <div>Identity and per-tool authorization</div>
      <div>Validation and versioned contracts</div>
      <div>Policy gate: reads vs. side effects</div>
      <div>Redaction and safe errors</div>
      <div>Timeouts, cancellation, resilience</div>
      <div>Telemetry and audit</div>
    </div>
  </div>
  <div class="w-[42px] flex items-center justify-center"><FlowArrow color="#e8a33d" :length="32" /></div>
  <div class="card w-[258px] flex flex-col items-center justify-center text-center gap-3">
    <IconBadge icon="rulebook" color="rust" :size="72" />
    <h3 class="text-white mt-2">Domain layer</h3>
    <p class="muted text-[18px]">then data and systems</p>
  </div>
</div>

<div class="grid grid-cols-3 gap-[30px] mt-[64px] castle-features">
  <Feature icon="compass" title="Discoverable" :size="64">Narrow, typed, well-described tools agents pick correctly</Feature>
  <Feature icon="shield" title="Constrained" :size="64">Least privilege, policy-gated writes, bounded context</Feature>
  <Feature icon="eye-target" title="Observable" :size="64">Traces, metrics, and an audit trail security can trust</Feature>
</div>

<style>
.castle-tiles > div {
  background: #383838;
  border-radius: 6px;
  height: 62px;
  display: flex;
  align-items: center;
  justify-content: center;
  text-align: center;
  font-size: 17.5px;
  line-height: 1.2;
  padding: 0 12px;
}
.castle-features h3 { color: #fff; font-size: 24px; }
</style>

<!--
Putting it together. The agent talks only to the MCP server. The server is a thin adapter, but it's where the boundary concerns live: identity and per-tool authorization, validation and versioned contracts, the policy gate between reads and side effects, redaction and safe errors, time budgets and resilience, and telemetry plus audit. Behind it, the domain layer you already trust does the real work. The test for any MCP server: discoverable enough for agents to use, constrained enough for security teams to tolerate, observable enough for production operations. Monday morning: inventory your generic tools, split reads from writes, and turn on audit.
-->

---
layout: dark
title: Demo
subtitle: The gate, guarded, running live
---

<div class="grid grid-cols-[1fr_440px] gap-[44px]">
  <div class="flex flex-col gap-[18px]">
    <a class="demo-link card" href="https://localhost:7103" target="_blank">
      <IconBadge icon="laptop.svg" :size="58" />
      <div><h3 class="text-white">Web UI</h3><span class="amber mono text-[16px]">https://localhost:7103</span><p class="muted text-[17px]">Sign in with Entra ID and chat with the agent</p></div>
    </a>
    <a class="demo-link card" href="https://localhost:17049" target="_blank">
      <IconBadge icon="trace.svg" :size="58" />
      <div><h3 class="text-white">Aspire dashboard</h3><span class="amber mono text-[16px]">https://localhost:17049</span><p class="muted text-[17px]">Follow one request from UI to agent to MCP server</p></div>
    </a>
    <a class="demo-link card" href="https://localhost:7101/mcp" target="_blank">
      <IconBadge icon="castle" :size="58" />
      <div><h3 class="text-white">MCP server</h3><span class="amber mono text-[16px]">https://localhost:7101/mcp</span><p class="muted text-[17px]">Streamable HTTP, OAuth-protected, per-tool authorization</p></div>
    </a>
  </div>
  <Panel label="Start everything">

```powershell
dotnet run --project src/Mcp2Oauth.AppHost
```

  <p class="muted text-[16px] mt-4">The services keep fixed ports 7101–7103, which the Entra app registrations depend on. The dashboard login URL is printed at startup.</p>
  </Panel>
</div>

<style>
.demo-link {
  display: flex;
  gap: 22px;
  align-items: center;
  text-decoration: none;
  color: inherit;
  padding: 16px 22px !important;
  border: 1px solid transparent;
}
.demo-link:hover { border-color: var(--gtg-gold); }
.demo-link h3 { margin-bottom: 2px; }
</style>

<!--
Switch to the running sample. Sign in to the web UI, ask the agent for something it is allowed to do, then something it is not, and show the denial. Then open the Aspire dashboard and follow the same request across the UI, the agent, and the MCP server, with both principals visible on the spans.
-->

---
title: One real tool, every gate
subtitle: "From the sample: approve_expense, exactly as it ships"
---

<Panel label="src/Mcp2Oauth.McpServer/Tools/ExpenseTools.cs" class="sample">

<<< @/snippets/ExpenseTools.cs#slide-approve-expense csharp {all|2|3-4|6|7-12|14-15}

</Panel>

<div class="grid grid-cols-5 gap-[12px] mt-[16px] sample-chips">
  <div :class="{ now: $clicks === 1 }">Authorized per tool</div>
  <div :class="{ now: $clicks === 2 }">Described, typed contract</div>
  <div :class="{ now: $clicks === 3 }">Acts as the signed-in user</div>
  <div :class="{ now: $clicks === 4 }">Refusals are audited tool errors</div>
  <div :class="{ now: $clicks === 5 }">Success is audited too</div>
</div>

<style>
.sample-chips > div {
  background: var(--gtg-card);
  border-radius: 10px;
  padding: 10px 12px;
  text-align: center;
  font-weight: 700;
  font-size: 16.5px;
  line-height: 1.2;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: background-color 0.3s, color 0.3s;
}
.sample-chips > div.now {
  background: var(--gtg-rust);
  color: #fff;
}
</style>

<!--
Back from the demo: this is the real code behind what you just saw, pulled straight from the repo. One tool, every gate. The Authorize attribute names a policy: only an approver, acting through an agent on their own behalf, gets in. The description is the contract the model reads. The user comes from IUserContext, the on-behalf-of user, never the agent. Each refusal (not found, your own expense, already decided) throws an McpException, which the SDK returns as a tool error the model can explain, and audit.Denied records it first. And the approval itself is audited too, with both the user and the agent named.
-->

---
title: Only the doors you can open
subtitle: "From the sample: policies on tools, and a tools/list that honors them"
---

<div class="grid grid-cols-[786px_1fr] gap-[34px]">
  <div class="flex flex-col gap-[16px]">
    <Panel label="src/Mcp2Oauth.McpServer/Policies.cs" class="sample">

<<< @/snippets/Policies.cs#slide-policies csharp {all|1-3|5-7}

</Panel>
    <Panel label="src/Mcp2Oauth.McpServer/Program.cs" class="sample">

<<< @/snippets/Program.cs#slide-mcp-server csharp {all|6}

</Panel>
  </div>
  <div class="flex flex-col gap-[30px] pt-1">
    <Feature icon="key-lock" title="Acting for a user">The agent must call on behalf of a signed-in user, with the Mcp.Tools scope.</Feature>
    <Feature icon="wax-seal" title="Plus an Entra role">Approver adds the Expense.Approver app role on top of the on-behalf-of check.</Feature>
    <Feature icon="door" title="Hidden, not just denied">AddAuthorizationFilters() drops tools from tools/list when the caller fails their policy.</Feature>
  </div>
</div>

<!--
Here's where those policies live. OnBehalfOfUser says the agent must be calling for a signed-in user with the Mcp.Tools scope; an app-only call doesn't qualify. Approver combines that with the Expense.Approver role from Entra, so the user's own rights are the ceiling. Then one line in Program.cs: AddAuthorizationFilters. With it, the SDK applies those same policies to tools/list, so a regular user's agent never even sees approve_expense. It can't pick a tool it doesn't know exists. That's "show only usable doors", for real.
-->

---
layout: dark
title: Resources
subtitle: Code from today, and the SDKs it builds on
---

<div class="flex flex-col gap-[12px] -mt-[4px] res">
  <div class="flex gap-[24px] items-center">
    <IconBadge icon="laptop.svg" :size="66" />
    <div>
      <h3 class="text-white">Demo: OAuth-secured MCP server</h3>
      <a class="link" href="https://github.com/marimerllc/mcp2-oauth" target="_blank">github.com/marimerllc/mcp2-oauth</a>
      <p class="muted">The sample from this session: an MCP server behind OAuth with per-tool authorization</p>
    </div>
  </div>
  <div class="flex gap-[24px] items-center">
    <IconBadge icon="books.svg" :size="66" />
    <div>
      <h3 class="text-white">MCP C# SDK</h3>
      <a class="link" href="https://github.com/modelcontextprotocol/csharp-sdk" target="_blank">github.com/modelcontextprotocol/csharp-sdk</a>
      <p class="muted">The official .NET SDK for building MCP servers and clients</p>
    </div>
  </div>
  <div class="flex gap-[24px] items-center">
    <IconBadge icon="head-gears.svg" :size="66" />
    <div>
      <h3 class="text-white">Microsoft.Extensions.AI</h3>
      <a class="link" href="https://learn.microsoft.com/dotnet/ai/microsoft-extensions-ai" target="_blank">learn.microsoft.com/dotnet/ai/microsoft-extensions-ai</a>
      <p class="muted">The .NET abstractions (IChatClient) the demo agent uses to call the model and its tools</p>
    </div>
  </div>
  <div class="flex gap-[24px] items-center">
    <IconBadge icon="cloud-pc.svg" :size="66" />
    <div>
      <h3 class="text-white">Azure OpenAI client library for .NET</h3>
      <a class="link" href="https://learn.microsoft.com/dotnet/api/overview/azure/ai.openai-readme" target="_blank">learn.microsoft.com/dotnet/api/overview/azure/ai.openai-readme</a>
      <p class="muted">The chat-model client behind the demo agent</p>
    </div>
  </div>
  <div class="flex gap-[24px] items-center">
    <IconBadge icon="keys.svg" :size="66" />
    <div>
      <h3 class="text-white">Microsoft.Identity.Web</h3>
      <a class="link" href="https://github.com/AzureAD/microsoft-identity-web" target="_blank">github.com/AzureAD/microsoft-identity-web</a>
      <p class="muted">Entra ID sign-in and on-behalf-of token acquisition for the agent and web UI</p>
    </div>
  </div>
</div>

<style>
.res h3 { font-size: 21px; }
.res .link { font-size: 19px; }
.res p { font-size: 17px; }
</style>

<!--
DRAFT (the original notes on this slide were copied from the previous slide):
Everything from today is on GitHub: an MCP server behind Entra ID OAuth with per-tool authorization, an agent built on Microsoft.Extensions.AI, and a Blazor web UI. It builds on the official MCP C# SDK, Microsoft.Extensions.AI, the Azure OpenAI client library, and Microsoft.Identity.Web for sign-in and on-behalf-of.
-->

---
layout: end
---

# THANK YOU

Rockford Lhotka <span class="sep">✕</span> blog.lhotka.net

<!--
Thank you. Open for questions. Point people to the MCP C# SDK repository and your sample code.
-->
