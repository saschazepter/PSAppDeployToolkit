#-----------------------------------------------------------------------------
#
# MARK: Invoke-ADTSessionCallbackOperation
#
#-----------------------------------------------------------------------------

function Invoke-ADTSessionCallbackOperation
{
    [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSReviewUnusedParameter', 'Action', Justification = "This parameter is used within delegates that PSScriptAnalyzer has no visibility of. See https://github.com/PowerShell/PSScriptAnalyzer/issues/1472 for more details.")]
    [CmdletBinding(SupportsShouldProcess = $true)]
    param
    (
        [Parameter(Mandatory = $true)]
        [ValidateSet('Starting', 'Opening', 'Closing', 'Finishing')]
        [System.String]$Type,

        [Parameter(Mandatory = $true)]
        [ValidateSet('Add', 'Remove')]
        [System.String]$Action,

        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [System.Management.Automation.CommandInfo[]]$Callback
    )

    # Cache the global callbacks and perform any required action.
    $callbacks = $Script:ADT.Callbacks.$Type
    $null = $Callback | & { process { if (($Action.Equals('Remove') -or !$callbacks.Contains($_)) -and $PSCmdlet.ShouldProcess($_, "$Type.$Action()")) { $callbacks.$Action($_) } } }
}
