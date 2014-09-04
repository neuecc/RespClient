$root = Join-Path (split-path -parent $MyInvocation.MyCommand.Definition) '\..'
$version = [System.Reflection.Assembly]::LoadFile("$root\RespClient\bin\Release\RespClient.dll").GetName().Version
$versionStr = "{0}.{1}.{2}" -f ($version.Major, $version.Minor, $version.Build)

Write-Host "Setting .nuspec version tag to $versionStr" -ForegroundColor Green
$content = Get-Content $root\NuGet\RespClient.nuspec 
$content = $content -replace '\$version\$',$versionStr
$content | Out-File $root\nuget\RespClient.compiled.nuspec -Force

& $root\NuGet\NuGet.exe pack $root\nuget\RespClient.compiled.nuspec
