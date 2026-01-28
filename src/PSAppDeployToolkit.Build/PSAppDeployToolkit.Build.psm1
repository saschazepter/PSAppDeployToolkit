<#

.SYNOPSIS
PSAppDeployToolkit.Build - This module script contains all the necessary logic to build PSAppDeployToolkit from source.

.DESCRIPTION
This module is designed to facilitate the local building of PSAppDeployToolkit into a release state. It is not designed to be operated outside of this repository.

PSAppDeployToolkit is licensed under the GNU LGPLv3 License - © 2026 PSAppDeployToolkit Team (Sean Lillis, Dan Cunningham, Muhammad Mashwani, Mitch Richters, Dan Gough).

This program is free software: you can redistribute it and/or modify it under the terms of the GNU Lesser General Public License as published by the
Free Software Foundation, either version 3 of the License, or any later version. This program is distributed in the hope that it will be useful, but
WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU Lesser General Public License
for more details. You should have received a copy of the GNU Lesser General Public License along with this program. If not, see <http://www.gnu.org/licenses/>.

.LINK
https://psappdeploytoolkit.com

#>

#-----------------------------------------------------------------------------
#
# MARK: Module Initialization Code
#
#-----------------------------------------------------------------------------

# Throw if this psm1 file isn't being imported via our manifest.
if (!([System.Environment]::StackTrace.Split("`n") -like '*Microsoft.PowerShell.Commands.ModuleCmdletBase.LoadModuleManifest(*'))
{
    throw [System.Management.Automation.ErrorRecord]::new(
        [System.InvalidOperationException]::new("This module must be imported via its .psd1 file, which is recommended for all modules that supply them."),
        'ModuleImportError',
        [System.Management.Automation.ErrorCategory]::InvalidOperation,
        $MyInvocation.MyCommand.ScriptBlock.Module
    )
}

# Initialise the module as required.
try
{
    # Set required variables to ensure module functionality.
    New-Variable -Name ErrorActionPreference -Value ([System.Management.Automation.ActionPreference]::Stop) -Option Constant -Force
    New-Variable -Name InformationPreference -Value ([System.Management.Automation.ActionPreference]::Continue) -Option Constant -Force
    New-Variable -Name ProgressPreference -Value ([System.Management.Automation.ActionPreference]::SilentlyContinue) -Option Constant -Force

    # Ensure module operates under the strictest of conditions.
    Set-StrictMode -Version 3

    # Import all necessary functions.
    New-Variable -Name ModuleFiles -Option Constant -Value ([System.Collections.ObjectModel.ReadOnlyCollection[System.IO.FileInfo]]::new([System.IO.FileInfo[]]$([System.IO.Directory]::GetFiles((Join-Path -Path $PSScriptRoot -ChildPath Private)); [System.IO.Directory]::GetFiles((Join-Path -Path $PSScriptRoot -ChildPath Public)))))
    $FunctionPaths = [System.Collections.Generic.List[System.String]]::new()
    $PrivateFuncs = [System.Collections.Generic.List[System.String]]::new()
    $ModuleFiles | & {
        process
        {
            if ([System.IO.Path]::GetDirectoryName($_.FullName).EndsWith('Private'))
            {
                $PrivateFuncs.Add($_.BaseName)
            }
            $FunctionPaths.Add("Microsoft.PowerShell.Core\Function::$($_.BaseName)")
        }
    }
    New-Variable -Name FunctionPaths -Option Constant -Value $FunctionPaths.AsReadOnly() -Force
    New-Variable -Name PrivateFuncs -Option Constant -Value $PrivateFuncs.AsReadOnly() -Force
    Remove-Item -LiteralPath $FunctionPaths -Force -ErrorAction Ignore
    $ModuleFiles.FullName | . { process { . $_ } }
    Set-Item -LiteralPath $FunctionPaths -Options ReadOnly
}
catch
{
    # Rethrowing caught exceptions makes the error output from Import-Module look better.
    throw
}
