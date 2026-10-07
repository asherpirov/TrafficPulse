$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:App__DemoMode = 'true'
dotnet run --project TrafficWeb/TrafficWeb.csproj --launch-profile TrafficWeb
