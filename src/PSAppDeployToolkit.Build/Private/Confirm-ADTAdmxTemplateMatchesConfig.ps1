#-----------------------------------------------------------------------------
#
# MARK: Confirm-ADTAdmxTemplateMatchesConfig
#
#-----------------------------------------------------------------------------

function Confirm-ADTAdmxTemplateMatchesConfig
{
    [CmdletBinding()]
    [OutputType([System.Boolean])]
    param
    (
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [System.String]$ConfigPath,

        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [System.String]$AdmxPath
    )

    # Internal worker function for processing each hashtable.
    function Confirm-ADTAdmxCategoryMatchesConfigSection
    {
        [CmdletBinding()]
        param
        (
            [Parameter(Mandatory = $true)]
            [ValidateNotNullOrEmpty()]
            [System.String]$Category,

            [Parameter(Mandatory = $true)]
            [ValidateNotNullOrEmpty()]
            [System.Collections.Hashtable]$Section
        )

        # Recursively process subsections that are hashtables.
        $sectionProps = foreach ($kvp in $Section.GetEnumerator())
        {
            if ($kvp.Value -is [System.Collections.Hashtable])
            {
                Confirm-ADTAdmxCategoryMatchesConfigSection -Category $kvp.Key -Section $kvp.Value
            }
            else
            {
                $kvp.Key
            }
        }

        # Test our collected session properties.
        $admxProps = $admxData.policyDefinitions.policies.policy | & { process { if ($_.parentCategory.ref.Equals($Category)) { return $_.Name.Split('_')[0] } } }
        if ($missing = $sectionProps | & { process { if ($admxProps -notcontains $_) { return $_ } } })
        {
            throw "The ADMX category [$Category] is missing the following config options: ['$([System.String]::Join("', '", $missing))']."
        }
        if ($extras = $admxProps | & { process { if ($sectionProps -notcontains $_) { return $_ } } })
        {
            throw "The ADMX category [$Category] has the following extra config options: ['$([System.String]::Join("', '", $extras))']."
        }
    }

    # Import config and XML as required.
    $adtConfig = Import-PowerShellDataFile -LiteralPath $ConfigPath
    $admxData = [System.Xml.XmlDocument]::new()
    $admxData.Load($AdmxPath)

    # Process the hashtable. We assume that each initial section is a hashtable.
    foreach ($kvp in $adtConfig.GetEnumerator())
    {
        Confirm-ADTAdmxCategoryMatchesConfigSection -Category $kvp.Key -Section $kvp.Value
    }
    return $true
}
