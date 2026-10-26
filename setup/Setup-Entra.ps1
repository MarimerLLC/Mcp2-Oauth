#Requires -Version 7
<#
.SYNOPSIS
    Creates (or updates) the Entra ID app registrations for the Mcp2Oauth demo
    and writes their settings to each project's user secrets.

.DESCRIPTION
    Three app registrations:

      Mcp2Oauth MCP Server  Protected resource (the MCP server).
                              - Delegated scope  Mcp.Tools        (agent acting for a user)
                              - App role         Mcp.Status.Read  (Application: agent acting as itself)
                              - App role         Expense.Approver (User: assigned to people)
                              - App role         Expense.Auditor  (User: assigned to people)
                              - Optional claim   idtyp            (marks app-only vs user tokens)
                            With -DecisionModelResourceId, also a confidential client with its
                            own secret, so its expense policy can call the decision model as itself.

      Mcp2Oauth Agent       The agent. A protected API (scope Agent.Chat) AND a confidential
                            client with its own secret. Has admin consent for Mcp.Tools
                            (used via on-behalf-of) and Mcp.Status.Read (client credentials).

      Mcp2Oauth Web UI      The Blazor app users sign in to. Confidential client that calls
                            the agent with the Agent.Chat scope.

    Re-running is safe: existing apps, scopes and roles are reused (matched by display
    name / value). Client secrets are only created when user secrets don't already have
    one, unless -RotateSecrets is given.

    Requires the Azure CLI, signed in to the target tenant with rights to create app
    registrations and grant admin consent:

        az login --tenant <tenant-id-or-domain> --allow-no-subscriptions

.PARAMETER AssignApproverToMe
    Assign the signed-in user the Expense.Approver role on the MCP server, so the
    approval tools show up for them.

.PARAMETER AssignAuditorToMe
    Assign the signed-in user the Expense.Auditor role on the MCP server, so the
    audit log tool shows up for them.

.PARAMETER RotateSecrets
    Always create new client secrets for the agent and web UI apps, and for the MCP server
    when -DecisionModelResourceId is given.

.PARAMETER DecisionModelResourceId
    The Azure resource id of a Foundry (AIServices) resource with a Microsoft-Decision-1
    deployment, for expense auto-approval. The MCP server gets its own client secret and the
    Cognitive Services User role on that resource, and its user secrets get the endpoint and
    deployment. Assigning the role needs Owner or User Access Administrator on the resource.
    Without it, auto-approval stays off and every expense waits for a human.

.PARAMETER DecisionModelDeployment
    The name of the Microsoft-Decision-1 deployment on that resource. Default: decision-1.
#>
[CmdletBinding()]
param(
    [string] $Prefix = "Mcp2Oauth",
    [string] $WebUiUrl = "https://localhost:7103",
    [switch] $AssignApproverToMe,
    [switch] $AssignAuditorToMe,
    [switch] $RotateSecrets,
    [string] $DecisionModelResourceId,
    [string] $DecisionModelDeployment = "decision-1"
)

$ErrorActionPreference = "Stop"
$PSNativeCommandUseErrorActionPreference = $true

$repoRoot = Split-Path $PSScriptRoot -Parent
$projects = @{
    McpServer = Join-Path $repoRoot "src/Mcp2Oauth.McpServer"
    Agent     = Join-Path $repoRoot "src/Mcp2Oauth.Agent"
    WebUI     = Join-Path $repoRoot "src/Mcp2Oauth.WebUI"
}
$graph = "https://graph.microsoft.com/v1.0"
$graphAppId = "00000003-0000-0000-c000-000000000000"
$graphScopes = @{
    openid         = "37f7f235-527c-4136-accd-4a02d197296e"
    profile        = "14dad69e-099b-42c9-810b-d002981feec1"
    offline_access = "7427e0e9-2fba-42fe-b0c0-848c9e6a8182"
    "User.Read"    = "e1fe6dd8-ba31-4d61-89e7-88639da4683d"
}

#region helpers

function Invoke-Graph {
    param(
        [Parameter(Mandatory)] [string] $Method,
        [Parameter(Mandatory)] [string] $Uri,
        [object] $Body
    )
    $arguments = @("rest", "--method", $Method, "--uri", $Uri, "--headers", "Content-Type=application/json")
    $bodyFile = $null
    if ($null -ne $Body) {
        # Pass the body through a file to avoid shell quoting problems.
        $bodyFile = New-TemporaryFile
        $Body | ConvertTo-Json -Depth 20 | Set-Content -Path $bodyFile -Encoding utf8NoBOM
        $arguments += @("--body", "@$bodyFile")
    }
    try {
        $result = az @arguments
        if ($result) { return ($result | Out-String | ConvertFrom-Json) }
    }
    finally {
        if ($bodyFile) { Remove-Item $bodyFile -ErrorAction SilentlyContinue }
    }
}

function Get-OrCreateApp {
    param([Parameter(Mandatory)] [string] $DisplayName)
    $filter = [uri]::EscapeDataString("displayName eq '$DisplayName'")
    $existing = (Invoke-Graph GET "$graph/applications?`$filter=$filter").value
    if ($existing.Count -gt 1) { throw "More than one app registration is named '$DisplayName'." }
    if ($existing.Count -eq 1) {
        Write-Host "  Found app registration '$DisplayName' ($($existing[0].appId))"
        return $existing[0]
    }
    Write-Host "  Creating app registration '$DisplayName'"
    return Invoke-Graph POST "$graph/applications" @{ displayName = $DisplayName; signInAudience = "AzureADMyOrg" }
}

function Get-OrCreateServicePrincipal {
    param([Parameter(Mandatory)] [string] $AppId)
    $filter = [uri]::EscapeDataString("appId eq '$AppId'")
    $existing = (Invoke-Graph GET "$graph/servicePrincipals?`$filter=$filter").value
    if ($existing.Count -eq 1) { return $existing[0] }
    # A new app can take a few seconds to replicate before its service principal can be created.
    for ($attempt = 1; ; $attempt++) {
        try { return Invoke-Graph POST "$graph/servicePrincipals" @{ appId = $AppId } }
        catch { if ($attempt -ge 6) { throw }; Start-Sleep -Seconds 5 }
    }
}

# Reuse the id of an existing scope/role with the same value; ids must stay stable.
function Get-StableId {
    param($ExistingItems, [string] $Value)
    $match = $ExistingItems | Where-Object value -eq $Value | Select-Object -First 1
    if ($match) { return $match.id }
    return [guid]::NewGuid().ToString()
}

function New-Scope {
    param($Existing, [string] $Value, [string] $AdminName, [string] $AdminDescription, [string] $UserName, [string] $UserDescription)
    [ordered]@{
        id                      = Get-StableId $Existing $Value
        value                   = $Value
        type                    = "User"
        isEnabled               = $true
        adminConsentDisplayName = $AdminName
        adminConsentDescription = $AdminDescription
        userConsentDisplayName  = $UserName
        userConsentDescription  = $UserDescription
    }
}

function New-AppRole {
    param($Existing, [string] $Value, [string[]] $MemberTypes, [string] $DisplayName, [string] $Description)
    [ordered]@{
        id                 = Get-StableId $Existing $Value
        value              = $Value
        allowedMemberTypes = $MemberTypes
        displayName        = $DisplayName
        description        = $Description
        isEnabled          = $true
    }
}

function Grant-DelegatedConsent {
    param([string] $ClientSpId, [string] $ResourceSpId, [string] $Scope)
    $filter = [uri]::EscapeDataString("clientId eq '$ClientSpId' and resourceId eq '$ResourceSpId' and consentType eq 'AllPrincipals'")
    $grant = (Invoke-Graph GET "$graph/oauth2PermissionGrants?`$filter=$filter").value | Select-Object -First 1
    if ($grant) {
        Invoke-Graph PATCH "$graph/oauth2PermissionGrants/$($grant.id)" @{ scope = $Scope } | Out-Null
    }
    else {
        Invoke-Graph POST "$graph/oauth2PermissionGrants" @{
            clientId = $ClientSpId; resourceId = $ResourceSpId; consentType = "AllPrincipals"; scope = $Scope
        } | Out-Null
    }
}

function Grant-AppRole {
    param([string] $PrincipalId, [string] $ResourceSpId, [string] $AppRoleId, [string] $AssignmentsUri)
    $existing = (Invoke-Graph GET $AssignmentsUri).value |
        Where-Object { $_.resourceId -eq $ResourceSpId -and $_.appRoleId -eq $AppRoleId }
    if (-not $existing) {
        Invoke-Graph POST $AssignmentsUri @{ principalId = $PrincipalId; resourceId = $ResourceSpId; appRoleId = $AppRoleId } | Out-Null
    }
}

function Get-UserSecret {
    param([string] $Project, [string] $Key)
    $line = dotnet user-secrets list --project $Project | Where-Object { $_ -like "$Key = *" } | Select-Object -First 1
    if ($line) { return $line.Substring($Key.Length + 3) }
}

function Set-UserSecrets {
    param([string] $Project, [hashtable] $Values)
    foreach ($entry in $Values.GetEnumerator()) {
        dotnet user-secrets set $entry.Key $entry.Value --project $Project | Out-Null
    }
}

function Get-ClientSecret {
    param([string] $AppObjectId, [string] $Project)
    $key = "AzureAd:ClientCredentials:0:ClientSecret"
    $current = Get-UserSecret $Project $key
    if ($current -and -not $RotateSecrets) {
        Write-Host "  Keeping the existing client secret in user secrets"
        return $current
    }
    Write-Host "  Creating a client secret (expires in 6 months)"
    $credential = Invoke-Graph POST "$graph/applications/$AppObjectId/addPassword" @{
        passwordCredential = @{ displayName = "Mcp2Oauth dev"; endDateTime = (Get-Date).AddMonths(6).ToString("o") }
    }
    return $credential.secretText
}

#endregion

$tenantId = az account show --query tenantId -o tsv
$me = Invoke-Graph GET "$graph/me?`$select=id,userPrincipalName"
Write-Host "Tenant: $tenantId"
Write-Host "Signed in as: $($me.userPrincipalName)"

#region MCP server

Write-Host "`nMCP server"
$mcpApp = Get-OrCreateApp "$Prefix MCP Server"
$mcpScope = New-Scope $mcpApp.api.oauth2PermissionScopes "Mcp.Tools" `
    "Use the MCP server's tools on behalf of the signed-in user" `
    "Allows an agent to call the Mcp2Oauth MCP server's tools on behalf of the signed-in user." `
    "Let the agent use the MCP server's tools for you" `
    "Allows the agent to call the MCP server's tools on your behalf."
$statusRole = New-AppRole $mcpApp.appRoles "Mcp.Status.Read" @("Application") `
    "Read MCP service status" "Allows an agent to read service status without a signed-in user."
$approverRole = New-AppRole $mcpApp.appRoles "Expense.Approver" @("User") `
    "Expense approver" "Can approve other people's expense reports."
$auditorRole = New-AppRole $mcpApp.appRoles "Expense.Auditor" @("User") `
    "Expense auditor" "Can read the audit log of expense submissions and approvals."

Invoke-Graph PATCH "$graph/applications/$($mcpApp.id)" @{
    identifierUris = @("api://$($mcpApp.appId)")
    api            = @{ requestedAccessTokenVersion = 2; oauth2PermissionScopes = @($mcpScope) }
    appRoles       = @($statusRole, $approverRole, $auditorRole)
    optionalClaims = @{ accessToken = @(@{ name = "idtyp"; essential = $false }) }
} | Out-Null
$mcpSp = Get-OrCreateServicePrincipal $mcpApp.appId

#endregion

#region Web UI (created before the agent so the agent can list it as a known client)

Write-Host "`nWeb UI"
$webApp = Get-OrCreateApp "$Prefix Web UI"
$webSp = Get-OrCreateServicePrincipal $webApp.appId

#endregion

#region Agent

Write-Host "`nAgent"
$agentApp = Get-OrCreateApp "$Prefix Agent"
$agentScope = New-Scope $agentApp.api.oauth2PermissionScopes "Agent.Chat" `
    "Chat with the agent" "Allows the web UI to call the agent on behalf of the signed-in user." `
    "Chat with the agent" "Allows the app to send your requests to the agent."

Invoke-Graph PATCH "$graph/applications/$($agentApp.id)" @{
    identifierUris         = @("api://$($agentApp.appId)")
    api                    = @{
        requestedAccessTokenVersion = 2
        oauth2PermissionScopes      = @($agentScope)
        knownClientApplications     = @($webApp.appId)
    }
    requiredResourceAccess = @(@{
        resourceAppId  = $mcpApp.appId
        resourceAccess = @(
            @{ id = $mcpScope.id; type = "Scope" },
            @{ id = $statusRole.id; type = "Role" }
        )
    })
} | Out-Null
$agentSp = Get-OrCreateServicePrincipal $agentApp.appId

Write-Host "  Granting admin consent: Mcp.Tools (delegated) and Mcp.Status.Read (application)"
Grant-DelegatedConsent $agentSp.id $mcpSp.id "Mcp.Tools"
Grant-AppRole $agentSp.id $mcpSp.id $statusRole.id "$graph/servicePrincipals/$($agentSp.id)/appRoleAssignments"

$agentSecret = Get-ClientSecret $agentApp.id $projects.Agent

#endregion

#region Web UI configuration

Write-Host "`nWeb UI configuration"
Invoke-Graph PATCH "$graph/applications/$($webApp.id)" @{
    web                    = @{
        redirectUris = @("$WebUiUrl/signin-oidc")
        logoutUrl    = "$WebUiUrl/signout-callback-oidc"
    }
    requiredResourceAccess = @(
        @{
            resourceAppId  = $agentApp.appId
            resourceAccess = @(@{ id = $agentScope.id; type = "Scope" })
        },
        @{
            resourceAppId  = $graphAppId
            resourceAccess = @($graphScopes.Values | ForEach-Object { @{ id = $_; type = "Scope" } })
        }
    )
} | Out-Null

$graphFilter = [uri]::EscapeDataString("appId eq '$graphAppId'")
$graphSp = (Invoke-Graph GET "$graph/servicePrincipals?`$filter=$graphFilter").value[0]
Write-Host "  Granting admin consent: Agent.Chat and $($graphScopes.Keys -join ', ')"
Grant-DelegatedConsent $webSp.id $agentSp.id "Agent.Chat"
Grant-DelegatedConsent $webSp.id $graphSp.id ($graphScopes.Keys -join " ")

$webSecret = Get-ClientSecret $webApp.id $projects.WebUI

#endregion

#region Decision model (optional): the MCP server calls Foundry as itself

if ($DecisionModelResourceId) {
    Write-Host "`nDecision model"
    $account = az resource show --ids $DecisionModelResourceId -o json | Out-String | ConvertFrom-Json
    $decisionEndpoint = $account.properties.endpoints.'AI Foundry API'
    if (-not $decisionEndpoint) { throw "Resource '$($account.name)' has no AI Foundry API endpoint." }
    Write-Host "  Foundry resource '$($account.name)': $decisionEndpoint, deployment '$DecisionModelDeployment'"

    $role = "Cognitive Services User"
    $assigned = az role assignment list --assignee $mcpSp.id --role $role --scope $DecisionModelResourceId -o json |
        Out-String | ConvertFrom-Json
    if ($assigned) {
        Write-Host "  The MCP server already has $role on the resource"
    }
    else {
        Write-Host "  Assigning $role on the resource to the MCP server (can take a few minutes to apply)"
        az role assignment create --assignee-object-id $mcpSp.id --assignee-principal-type ServicePrincipal `
            --role $role --scope $DecisionModelResourceId | Out-Null
    }

    $mcpSecret = Get-ClientSecret $mcpApp.id $projects.McpServer
}

#endregion

if ($AssignApproverToMe) {
    Write-Host "`nAssigning Expense.Approver to $($me.userPrincipalName)"
    Grant-AppRole $me.id $mcpSp.id $approverRole.id "$graph/users/$($me.id)/appRoleAssignments"
}

if ($AssignAuditorToMe) {
    Write-Host "`nAssigning Expense.Auditor to $($me.userPrincipalName)"
    Grant-AppRole $me.id $mcpSp.id $auditorRole.id "$graph/users/$($me.id)/appRoleAssignments"
}

#region user secrets

Write-Host "`nWriting user secrets"
Set-UserSecrets $projects.McpServer @{
    "AzureAd:TenantId"                           = $tenantId
    "AzureAd:ClientId"                           = $mcpApp.appId
    "AzureAd:Audience"                           = "api://$($mcpApp.appId)"
    "McpServer:AllowedAgents:$($agentApp.appId)" = "$Prefix Agent"
}
if ($DecisionModelResourceId) {
    Set-UserSecrets $projects.McpServer @{
        "AzureAd:ClientCredentials:0:SourceType"   = "ClientSecret"
        "AzureAd:ClientCredentials:0:ClientSecret" = $mcpSecret
        "ExpensePolicy:Endpoint"                   = $decisionEndpoint
        "ExpensePolicy:Deployment"                 = $DecisionModelDeployment
    }
}
Set-UserSecrets $projects.Agent @{
    "AzureAd:TenantId"                       = $tenantId
    "AzureAd:ClientId"                       = $agentApp.appId
    "AzureAd:Audience"                       = "api://$($agentApp.appId)"
    "AzureAd:ClientCredentials:0:SourceType" = "ClientSecret"
    "AzureAd:ClientCredentials:0:ClientSecret" = $agentSecret
    "Mcp:Audience"                           = "api://$($mcpApp.appId)"
}
Set-UserSecrets $projects.WebUI @{
    "AzureAd:TenantId"                         = $tenantId
    "AzureAd:ClientId"                         = $webApp.appId
    "AzureAd:ClientCredentials:0:SourceType"   = "ClientSecret"
    "AzureAd:ClientCredentials:0:ClientSecret" = $webSecret
    "Agent:Scope"                              = "api://$($agentApp.appId)/Agent.Chat"
}

#endregion

Write-Host "`nDone."
Write-Host "  MCP server app id: $($mcpApp.appId)"
Write-Host "  Agent app id:      $($agentApp.appId)"
Write-Host "  Web UI app id:     $($webApp.appId)"
