#-----------------------------------------------------------------------------
#
# MARK: Invoke-ADTModuleBuild
#
#-----------------------------------------------------------------------------

function Invoke-ADTModuleBuild
{
	[CmdletBinding()]
	param
	(
	)

	# Announce commencement of module build.
	Write-ADTBuildLogDivider -Character =
	Write-ADTBuildLogEntry -Message "PSAppDeployToolkit Module Build"

	# Announce finalisation of module build.
	Write-ADTBuildLogDivider -Character =
}
