function Get-MissingOllamaModels {
    param(
        [string[]]$ModelList,
        [string[]]$RequiredModels
    )

    # Match the full first-column model name, not arbitrary table substrings.
    $InstalledModels = @($ModelList | ForEach-Object {
        $Name = ($_.Trim() -split '\s+')[0]
        if ($Name -and $Name -ne 'NAME') { $Name }
    })
    foreach ($Required in $RequiredModels) {
        $Canonical = if (($Required -split '/')[-1] -match ':') {
            $Required
        } else {
            "${Required}:latest"
        }
        if ($InstalledModels -cnotcontains $Canonical) { $Required }
    }
}
