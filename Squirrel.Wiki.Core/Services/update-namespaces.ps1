# PowerShell script to update namespaces in moved service files

$folders = @(
    @{Path="Categories"; Namespace="Squirrel.Wiki.Core.Services.Categories"},
    @{Path="Tags"; Namespace="Squirrel.Wiki.Core.Services.Tags"},
    @{Path="Menus"; Namespace="Squirrel.Wiki.Core.Services.Menus"},
    @{Path="Users"; Namespace="Squirrel.Wiki.Core.Services.Users"},
    @{Path="Search"; Namespace="Squirrel.Wiki.Core.Services.Search"},
    @{Path="Caching"; Namespace="Squirrel.Wiki.Core.Services.Caching"},
    @{Path="Configuration"; Namespace="Squirrel.Wiki.Core.Services.Configuration"},
    @{Path="Plugins"; Namespace="Squirrel.Wiki.Core.Services.Plugins"},
    @{Path="Content"; Namespace="Squirrel.Wiki.Core.Services.Content"},
    @{Path="Infrastructure"; Namespace="Squirrel.Wiki.Core.Services.Infrastructure"}
)

foreach ($folder in $folders) {
    $path = Join-Path $PSScriptRoot $folder.Path
    $files = Get-ChildItem -Path $path -Filter "*.cs" -File
    
    foreach ($file in $files) {
        $content = Get-Content $file.FullName -Raw
        $oldNamespace = "namespace Squirrel.Wiki.Core.Services;"
        $newNamespace = "namespace $($folder.Namespace);"
        
        if ($content -match [regex]::Escape($oldNamespace)) {
            $newContent = $content -replace [regex]::Escape($oldNamespace), $newNamespace
            Set-Content -Path $file.FullName -Value $newContent -NoNewline
            Write-Host "Updated namespace in $($file.Name)"
        }
    }
}

Write-Host "Namespace update complete!"
