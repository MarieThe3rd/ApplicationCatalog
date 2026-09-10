<#
Appends the latest conversation turn (question, then Claude's text response)
to Documentation/claude-conversation/<yyyy-MM-dd>.md.

Wired as a Stop hook in .claude/settings.json - runs once each time Claude
finishes responding. Reads the hook's stdin JSON for `transcript_path`,
walks that session transcript backwards to find the most recent genuine
user prompt, collects every assistant `text` block written after it
(skipping thinking/tool_use/tool_result), and appends the pair.
#>

$ErrorActionPreference = 'SilentlyContinue'

$stdin = [Console]::In.ReadToEnd()
if ([string]::IsNullOrWhiteSpace($stdin)) { exit 0 }

try { $hookInput = $stdin | ConvertFrom-Json -Depth 20 } catch { exit 0 }

$transcriptPath = $hookInput.transcript_path
if (-not $transcriptPath -or -not (Test-Path -LiteralPath $transcriptPath)) { exit 0 }

$projectDir = $env:CLAUDE_PROJECT_DIR
if (-not $projectDir) { $projectDir = (Get-Location).Path }

function Test-IsGenuineUserPrompt {
    param($Entry)
    # promptSource/origin are set by Claude Code only on messages a person actually
    # typed - everything else (tool results, skill-injected content, interruption
    # markers) is also stored with type "user" but lacks these, even when its
    # content happens to be a plain string.
    if ($Entry.type -ne 'user') { return $false }
    if ($Entry.promptSource -ne 'typed') { return $false }
    if (-not $Entry.origin -or $Entry.origin.kind -ne 'human') { return $false }
    $content = $Entry.message.content
    if ($content -is [string]) { return $content.Trim().Length -gt 0 }
    if ($content -is [System.Array]) {
        return ($content | Where-Object { $_.type -eq 'text' }).Count -gt 0
    }
    return $false
}

function Get-UserPromptText {
    param($Entry)
    $content = $Entry.message.content
    if ($content -is [string]) { return $content.Trim() }
    $parts = $content | Where-Object { $_.type -eq 'text' } | ForEach-Object { $_.text }
    return ($parts -join "`n`n").Trim()
}

$entries = foreach ($line in (Get-Content -LiteralPath $transcriptPath)) {
    if ([string]::IsNullOrWhiteSpace($line)) { continue }
    try { $line | ConvertFrom-Json -Depth 30 } catch { continue }
}
$entries = @($entries)

$lastUserIndex = -1
for ($i = $entries.Count - 1; $i -ge 0; $i--) {
    if (Test-IsGenuineUserPrompt $entries[$i]) { $lastUserIndex = $i; break }
}
if ($lastUserIndex -lt 0) { exit 0 }

$question = Get-UserPromptText $entries[$lastUserIndex]
if ([string]::IsNullOrWhiteSpace($question)) { exit 0 }

$responseParts = @()
for ($i = $lastUserIndex + 1; $i -lt $entries.Count; $i++) {
    $entry = $entries[$i]
    if ($entry.type -ne 'assistant') { continue }
    $content = $entry.message.content
    if ($content -isnot [System.Array]) { continue }
    foreach ($block in $content) {
        if ($block.type -eq 'text' -and $block.text) { $responseParts += $block.text.Trim() }
    }
}
if ($responseParts.Count -eq 0) { exit 0 }
$response = ($responseParts -join "`n`n").Trim()

$logDir = Join-Path $projectDir 'Documentation/claude-conversation'
if (-not (Test-Path -LiteralPath $logDir)) {
    New-Item -ItemType Directory -Path $logDir -Force | Out-Null
}

$dateStr = Get-Date -Format 'yyyy-MM-dd'
$logFile = Join-Path $logDir "$dateStr.md"

$nextNum = 1
if (Test-Path -LiteralPath $logFile) {
    $existing = Get-Content -LiteralPath $logFile -Raw
    $numMatches = [regex]::Matches($existing, '(?m)^## Q(\d+)')
    if ($numMatches.Count -gt 0) {
        $nextNum = [int]($numMatches[$numMatches.Count - 1].Groups[1].Value) + 1
    }
    $qMatches = [regex]::Matches($existing, '(?s)\*\*You:\*\*\s*(.*?)\r?\n\r?\n\*\*Claude:\*\*')
    if ($qMatches.Count -gt 0) {
        $lastLogged = $qMatches[$qMatches.Count - 1].Groups[1].Value.Trim()
        if ($lastLogged -eq $question) { exit 0 }
    }
} else {
    $header = "# Conversation Log - $dateStr`n`nAppended automatically after each turn by the Stop hook (.claude/hooks/log-conversation.ps1).`n"
    Set-Content -LiteralPath $logFile -Value $header -Encoding utf8
}

$entryText = @"

---

## Q$nextNum

**You:** $question

**Claude:** $response
"@

Add-Content -LiteralPath $logFile -Value $entryText -Encoding utf8
exit 0
