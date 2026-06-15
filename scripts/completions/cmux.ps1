# CMux PowerShell native argument completion.
# Usage:
#   . /path/to/scripts/completions/cmux.ps1
# or:
#   cmux completion powershell | Invoke-Expression

$script:CmuxCommands = @(
    'notify',
    'workspace',
    'surface',
    'split',
    'status',
    'completion',
    'help',
    'version'
)

$script:CmuxSubcommands = @{
    workspace = @('list', 'create', 'select', 'next', 'previous')
    surface = @('create', 'next', 'previous')
    split = @('right', 'down')
    completion = @('powershell')
}

$script:CmuxGlobalOptions = @('--json')
$script:CmuxCommonOptions = @(
    '--title', '--body', '--subtitle',
    '--name', '--index', '--id'
)

function New-CmuxCompletionResult {
    param(
        [Parameter(Mandatory = $true)][string]$Text,
        [string]$ToolTip = $Text
    )

    [System.Management.Automation.CompletionResult]::new(
        $Text,
        $Text,
        [System.Management.Automation.CompletionResultType]::ParameterValue,
        $ToolTip
    )
}

function Get-CmuxMatchingResults {
    param(
        [Parameter(Mandatory = $true)][string[]]$Items,
        [AllowNull()][string]$WordToComplete
    )

    $word = if ($null -eq $WordToComplete) { '' } else { $WordToComplete }
    $Items |
        Where-Object { $_ -like "$word*" } |
        Sort-Object -Unique |
        ForEach-Object { New-CmuxCompletionResult $_ }
}

Register-ArgumentCompleter -Native -CommandName cmux -ScriptBlock {
    param($wordToComplete, $commandAst, $cursorPosition)

    $words = @(
        $commandAst.CommandElements |
            ForEach-Object { $_.Extent.Text.Trim('"', "'") }
    )

    if ($words.Count -le 1) {
        Get-CmuxMatchingResults $script:CmuxCommands $wordToComplete
        return
    }

    $command = $words[1].ToLowerInvariant()

    if ($wordToComplete -like '--*') {
        Get-CmuxMatchingResults ($script:CmuxGlobalOptions + $script:CmuxCommonOptions) $wordToComplete
        return
    }

    if ($words.Count -le 2 -and $script:CmuxCommands -contains $command) {
        Get-CmuxMatchingResults $script:CmuxCommands $wordToComplete
        return
    }

    if ($script:CmuxSubcommands.ContainsKey($command) -and $words.Count -le 3) {
        Get-CmuxMatchingResults $script:CmuxSubcommands[$command] $wordToComplete
        return
    }

    Get-CmuxMatchingResults ($script:CmuxGlobalOptions + $script:CmuxCommonOptions) $wordToComplete
}
