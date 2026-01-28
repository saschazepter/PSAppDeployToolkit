#-----------------------------------------------------------------------------
#
# MARK: Write-ADTBuildLogDivider
#
#-----------------------------------------------------------------------------

function Write-ADTBuildLogDivider
{
	[CmdletBinding()]
	param
	(
		[Parameter(Mandatory = $false)]
		[ValidateSet('=', '-')]
		[System.String]$Character = '-'
	)

	# Write out the divider directly to the console.
	$colours = @{
		'=' = [System.ConsoleColor]::DarkMagenta
		'-' = [System.ConsoleColor]::Magenta
	}
	Write-Host ($Character * 79) -ForegroundColour ($colours.$Character)
}
