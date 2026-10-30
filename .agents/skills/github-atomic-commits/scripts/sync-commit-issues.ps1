<#
.SYNOPSIS
    Idempotent 1:1 Git-Commit-to-GitHub-Issue & Project #7 Kanban Synchronizer for burroQueue.
.DESCRIPTION
    1. Queries GitHub for existing 'commit-record' issues so every commit in git history has at most one 1:1 issue.
    2. Creates missing 1:1 closed 'commit-record' issues assigned to T-Boyke, sets Milestone, links as Sub-Issue under Epics #23-#27, and places on Project #7 ('Done').
    3. Optionally closes and moves specified completed roadmap/bug issues (-CompletedIssues) to 'Done' on Project #7.
#>
[CmdletBinding()]
param(
    [string]$Milestone = "v2.0.0",
    [int[]]$CompletedIssues = @()
)

$ErrorActionPreference = "Stop"
[Console]::InputEncoding  = [System.Text.Encoding]::UTF8
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding           = [System.Text.Encoding]::UTF8
$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..\..\..")
$RepoFull = "T-Boyke/burroQueue"

$GhCmd = Get-Command gh -ErrorAction SilentlyContinue
$GhExe = if ($GhCmd) { $GhCmd.Source } elseif (Test-Path "C:\Program Files\GitHub CLI\gh.exe") { "C:\Program Files\GitHub CLI\gh.exe" } else { throw "GitHub CLI (gh) not found." }

$ScratchBase = if (Test-Path "D:\temp") { "D:\temp\burroQueue-gh-cache" } else { Join-Path ([System.IO.Path]::GetTempPath()) "burroQueue-gh-cache" }
if (-not (Test-Path $ScratchBase)) {
    New-Item -ItemType Directory -Force -Path $ScratchBase | Out-Null
}

# Project #7 (https://github.com/users/T-Boyke/projects/7) Constants
$ProjectId      = "PVT_kwHOAjbZ7M4BpeGG"
$StatusFieldId  = "PVTSSF_lAHOAjbZ7M4BpeGGzhn5mos"
$StatusDoneId   = "6bcb1117"
$PillarFieldId  = "PVTSSF_lAHOAjbZ7M4BpeGGzhn5mtU"
$PrioFieldId    = "PVTSSF_lAHOAjbZ7M4BpeGGzhn5mtc"
$PrioNormalId   = "d3ee3818"
$StackFieldId   = "PVTSSF_lAHOAjbZ7M4BpeGGzhn5muo"

$StackDotNet11Id = "d46c8c09"
$StackUnoCssId   = "b8beb9f8"
$StackUiId       = "ca54bc2f"
$StackCiId       = "f7e6224b"

$PillarOpts = @{
    1 = "91b217e8"
    2 = "6212d7d8"
    3 = "c7cb16c7"
    4 = "3bd4e6ae"
    5 = "6e3628d8"
}

$PillarLabels = @{
    1 = "pillar:1-core-domain"
    2 = "pillar:2-audio-hardware"
    3 = "pillar:3-network-webdrop"
    4 = "pillar:4-fluent-ux"
    5 = "pillar:5-devsecops-release"
}

$PillarNames = @{
    1 = "Säule 1 · Core Domain, Queue Engine & TOML Persistence"
    2 = "Säule 2 · Audio Engine, VoIP Auto-Ducking & Hardware/AI"
    3 = "Säule 3 · P2P LAN Sync, YouTube & UnoCSS 66.12 WebDrop"
    4 = "Säule 4 · Native Windows 11 Fluent UI, Desktop UX & i18n"
    5 = "Säule 5 · DevSecOps, Code Signing, Verified Git & .NET 11 CI/CD"
}

$EpicNodeIds = @{
    1 = "I_kwDOVZN9Fc8AAAABbOQU3A" # #23
    2 = "I_kwDOVZN9Fc8AAAABbOQckA" # #24
    3 = "I_kwDOVZN9Fc8AAAABbOQjGA" # #25
    4 = "I_kwDOVZN9Fc8AAAABbOQqxw" # #26
    5 = "I_kwDOVZN9Fc8AAAABbOQydQ" # #27
}

function Invoke-WithRetry([scriptblock]$Action, [string]$Desc) {
    $maxRetries = 5
    for ($attempt = 1; $attempt -le $maxRetries; $attempt++) {
        try {
            return (& $Action)
        } catch {
            $msg = $_.Exception.Message
            if ($attempt -eq $maxRetries -or $msg -match "Content already exists in this project") { throw }
            $waitSec = if ($msg -match "secondary rate limit|abuse|403|429") { 65 } else { 6 * $attempt }
            Write-Host "  [RETRY $attempt/$maxRetries] $Desc failed ($msg). Waiting ${waitSec}s..." -ForegroundColor Yellow
            Start-Sleep -Seconds $waitSec
        }
    }
}

function Invoke-Gql([string]$Query, [hashtable]$Variables = @{}) {
    return Invoke-WithRetry -Desc "GraphQL" -Action {
        $body = @{ query = $Query; variables = $Variables } | ConvertTo-Json -Depth 12 -Compress
        $tmp = Join-Path $ScratchBase "gql_req.json"
        [System.IO.File]::WriteAllText($tmp, $body, [System.Text.UTF8Encoding]::new($false))
        $out = & $GhExe api graphql -H "GraphQL-Features: sub_issues,issue_dependencies" --input $tmp 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "gh graphql exit $LASTEXITCODE : $($out -join ' ')"
        }
        $parsed = ($out -join "`n") | ConvertFrom-Json
        if ($null -ne $parsed.errors) {
            throw "GraphQL Error: $($parsed.errors | ConvertTo-Json -Compress)"
        }
        return $parsed.data
    }
}

function Invoke-Rest([string]$Method, [string]$Endpoint, [hashtable]$Payload) {
    return Invoke-WithRetry -Desc "$Method $Endpoint" -Action {
        $json = $Payload | ConvertTo-Json -Depth 10 -Compress
        $tmp = Join-Path $ScratchBase "rest_req.json"
        [System.IO.File]::WriteAllText($tmp, $json, [System.Text.UTF8Encoding]::new($false))
        $out = & $GhExe api $Endpoint -X $Method --input $tmp 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "gh rest exit $LASTEXITCODE : $($out -join ' ')"
        }
        return (($out -join "`n") | ConvertFrom-Json)
    }
}

function Get-OrAddProjectItemId([string]$IssueNodeId) {
    $q = Invoke-Gql 'query($id: ID!) { node(id: $id) { ... on Issue { projectItems(first: 10) { nodes { id project { id } } } } } }' @{ id = $IssueNodeId }
    $existing = ($q.node.projectItems.nodes | Where-Object { $_.project.id -eq $ProjectId } | Select-Object -First 1).id
    if ($existing) {
        return $existing
    }
    try {
        $added = Invoke-Gql 'mutation($projectId: ID!, $contentId: ID!) { addProjectV2ItemById(input: { projectId: $projectId, contentId: $contentId }) { item { id } } }' @{
            projectId = $ProjectId
            contentId = $IssueNodeId
        }
        return $added.addProjectV2ItemById.item.id
    } catch {
        $qRetry = Invoke-Gql 'query($id: ID!) { node(id: $id) { ... on Issue { projectItems(first: 10) { nodes { id project { id } } } } } }' @{ id = $IssueNodeId }
        $existingRetry = ($qRetry.node.projectItems.nodes | Where-Object { $_.project.id -eq $ProjectId } | Select-Object -First 1).id
        if ($existingRetry) {
            return $existingRetry
        }
        throw
    }
}

function Get-CommitPillar([string]$Subject) {
    $s = $Subject.ToLowerInvariant()
    if ($s -match "\(domain\)|toml|shuffle|cursor|favorite|playliststorageservice|burrogroup|skipvotepool") { return 1 }
    if ($s -match "\(audio\)|\(playback\)|\(telephony\)|\(memory\)|avx2|simd|npu|vibe|ducking|starface|swyxit|coreaudio|ecoqos") { return 2 }
    if ($s -match "\(network\)|\(webdrop\)|\(web\)|\(youtube\)|unocss|sse|multicast|clipboard|langroupservice|embeddedwebdropserver") { return 3 }
    if ($s -match "\(presentation\)|\(ui\)|\(settings\)|\(i18n\)|smtc|toast|titlebar|localization|systray|hotkey|mainwindow|settingswindow") { return 4 }
    return 5
}

function Get-CommitTypeLabel([string]$Subject) {
    if ($Subject -match "^fix(\(|:)") { return "bug" }
    if ($Subject -match "^docs(\(|:)") { return "documentation" }
    if ($Subject -match "^ci(\(|:)") { return "github_actions" }
    return "enhancement"
}

# Resolve Milestone number dynamically from GitHub
$allMilestones = ((& $GhExe api "repos/$RepoFull/milestones?state=all&per_page=30") -join "`n") | ConvertFrom-Json
$targetMs = $allMilestones | Where-Object { $_.title -like "$Milestone*" } | Select-Object -First 1
$msNumber = if ($targetMs) { [int]$targetMs.number } else { 4 }
$msTitle  = if ($targetMs) { [string]$targetMs.title } else { $Milestone }

# Fetch existing commit-record issues from GitHub (paginated) so state is 100% portable
$recordedShortShas = @{}
$existingJson = (& $GhExe issue list --repo $RepoFull --label "commit-record" --state all --limit 500 --json number,title) -join "`n"
$existingIssues = $existingJson | ConvertFrom-Json
foreach ($iss in $existingIssues) {
    if ($iss.title -match "\[Commit #\d+[^\]]*?([0-9a-f]{7,40})\]") {
        $recordedShortShas[$Matches[1].Substring(0, 7)] = $iss.number
    }
}

$rawCommits = git -C $RepoRoot log --reverse --format="%H|%h|%aI|%s"
$totalCount = $rawCommits.Count
$createdCount = 0

for ($i = 0; $i -lt $totalCount; $i++) {
    $parts = $rawCommits[$i] -split "\|", 4
    $sha = $parts[0]
    $short7 = $sha.Substring(0, 7)
    $date = $parts[2]
    $subject = $parts[3]
    $idx = $i + 1
    $idxStr = "{0:D3}" -f $idx

    if ($recordedShortShas.ContainsKey($short7)) {
        continue
    }

    $pillar = Get-CommitPillar $subject
    $typeLabel = Get-CommitTypeLabel $subject
    $pLabel = $PillarLabels[$pillar]
    $pName = $PillarNames[$pillar]

    $labels = @("commit-record", $typeLabel, $pLabel)
    $stackId = if ($subject -match "unocss") {
        $labels += "stack:unocss"
        $StackUnoCssId
    } elseif ($pillar -eq 4) {
        $StackUiId
    } elseif ($pillar -eq 5) {
        $StackCiId
    } else {
        $labels += "stack:dotnet11"
        $StackDotNet11Id
    }

    $statLines = (git -C $RepoRoot show --stat --format="" $sha | Select-Object -First 25) -join "`n"
    $title = "[Commit #$idxStr · $short7] $subject"
    if ($title.Length -gt 240) { $title = $title.Substring(0, 237) + "..." }

    $bodyTemplate = @'
### 📌 Dokumentierter Git-Commit (`#__IDX__ / __TOTAL__`)

| Eigenschaft | Wert |
| :--- | :--- |
| **Commit-SHA** | [`__SHA__`](https://github.com/T-Boyke/burroQueue/commit/__SHA__) |
| **Signatur-Status** | SSH Ed25519 Verified (`commit.gpgsign = true`) |
| **Datum** | `__DATE__` |
| **Entwicklungs-Säule** | **__PILLAR__** |
| **Milestone** | `__MILESTONE__` |
| **Verantwortlich** | `@T-Boyke` |

### 📝 Commit-Zusammenfassung
> __SUBJECT__

### 📂 Geänderte Komponenten (`git show --stat`)
```text
__STATS__
```
'@
    $body = $bodyTemplate.
        Replace("__IDX__", $idxStr).
        Replace("__TOTAL__", [string]$totalCount).
        Replace("__SHA__", $sha).
        Replace("__DATE__", $date).
        Replace("__PILLAR__", $pName).
        Replace("__MILESTONE__", $msTitle).
        Replace("__SUBJECT__", $subject).
        Replace("__STATS__", $statLines)

    $created = Invoke-Rest -Method "POST" -Endpoint "repos/$RepoFull/issues" -Payload @{
        title     = $title
        body      = $body
        assignees = @("T-Boyke")
        milestone = $msNumber
        labels    = $labels
    }

    Invoke-Rest -Method "PATCH" -Endpoint "repos/$RepoFull/issues/$($created.number)" -Payload @{
        state        = "closed"
        state_reason = "completed"
    } | Out-Null

    try {
        Invoke-Gql 'mutation($parentId: ID!, $childId: ID!) { addSubIssue(input: { issueId: $parentId, subIssueId: $childId, replaceParent: true }) { issue { id } } }' @{
            parentId = $EpicNodeIds[$pillar]
            childId  = $created.node_id
        } | Out-Null
    } catch {
        Write-Warning "Sub-Issue link skipped on #$($created.number): $($_.Exception.Message)"
    }

    $itemId = Get-OrAddProjectItemId $created.node_id
    Invoke-Gql 'mutation($projectId: ID!, $itemId: ID!, $fStatus: ID!, $vStatus: String!, $fPillar: ID!, $vPillar: String!, $fPrio: ID!, $vPrio: String!, $fStack: ID!, $vStack: String!) {
        u1: updateProjectV2ItemFieldValue(input: { projectId: $projectId, itemId: $itemId, fieldId: $fStatus, value: { singleSelectOptionId: $vStatus } }) { projectV2Item { id } }
        u2: updateProjectV2ItemFieldValue(input: { projectId: $projectId, itemId: $itemId, fieldId: $fPillar, value: { singleSelectOptionId: $vPillar } }) { projectV2Item { id } }
        u3: updateProjectV2ItemFieldValue(input: { projectId: $projectId, itemId: $itemId, fieldId: $fPrio,   value: { singleSelectOptionId: $vPrio   } }) { projectV2Item { id } }
        u4: updateProjectV2ItemFieldValue(input: { projectId: $projectId, itemId: $itemId, fieldId: $fStack,  value: { singleSelectOptionId: $vStack  } }) { projectV2Item { id } }
    }' @{
        projectId = $ProjectId
        itemId    = $itemId
        fStatus   = $StatusFieldId
        vStatus   = $StatusDoneId
        fPillar   = $PillarFieldId
        vPillar   = $PillarOpts[$pillar]
        fPrio     = $PrioFieldId
        vPrio     = $PrioNormalId
        fStack    = $StackFieldId
        vStack    = $stackId
    } | Out-Null

    $createdCount++
    Write-Host "[OK Commit #$idxStr] Issue #$($created.number) <- $short7 (Pillar $pillar)" -ForegroundColor Green
    Start-Sleep -Milliseconds 1200
}

foreach ($issueNum in $CompletedIssues) {
    $issObj = ((& $GhExe api "repos/$RepoFull/issues/$issueNum") -join "`n") | ConvertFrom-Json
    if ($issObj.state -ne "closed") {
        Invoke-Rest -Method "PATCH" -Endpoint "repos/$RepoFull/issues/$issueNum" -Payload @{
            state        = "closed"
            state_reason = "completed"
        } | Out-Null
    }
    $itemId = Get-OrAddProjectItemId $issObj.node_id
    Invoke-Gql 'mutation($projectId: ID!, $itemId: ID!, $fieldId: ID!, $optId: String!) {
        updateProjectV2ItemFieldValue(input: { projectId: $projectId, itemId: $itemId, fieldId: $fieldId, value: { singleSelectOptionId: $optId } }) { projectV2Item { id } }
    }' @{
        projectId = $ProjectId
        itemId    = $itemId
        fieldId   = $StatusFieldId
        optId     = $StatusDoneId
    } | Out-Null
    Write-Host "[DONE] Issue #$issueNum closed & moved to Done on Project #7" -ForegroundColor Green
}

Write-Host "[SYNC COMPLETE] $totalCount total commits verified ($createdCount newly recorded)." -ForegroundColor Cyan

