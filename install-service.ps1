# Script must be run as an admin to create event viewer source
#Requires -RunAsAdministrator

# Setup the Event Viewer source
$logFileExists = [System.Diagnostics.EventLog]::SourceExists("Orion Service Source");
if (! $logFileExists) {
	Write-Host "Creating event source..."
	[System.Diagnostics.EventLog]::CreateEventSource("Orion Service Source", "Application")
	Write-Host "Event source created successfully"
}

# Do we need to pull down the code or will that already be done with the main app?

# Need to determine where it should be installed
$destination = 'c:\orion-service'

# Setup the service info
$params = @{
	Name = "orion.service"
	BinaryPathName = "$destination\orion.service.exe"
	DisplayName = "Orion Service"
	StartupType = "Auto"
	Description = "The Orion Service"
}

# If the service already exists stop it before replacing the code
$service = Get-Service -Name $params.Name -ErrorAction SilentlyContinue
if($service -ne $null) {
	Write-Host "Stopping existing service"
	Stop-Service -Name $params.Name
}

# Build and publish the Service
Write-Host "Publishing service"
dotnet publish ".\orion.service.csproj" -c Release -o $destination

# Install the Windows Service if needed
if($service -eq $null) {
	Write-Host "Installing the service"
    New-Service @params
}

# Start the Windows Service
Write-Host "Starting the service"
Start-Service -Name $params.Name