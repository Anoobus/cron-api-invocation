# Script must be run as an admin to create event viewer source
#Requires -RunAsAdministrator

Write-Host get date identifier
$dateId=Get-Date -Format "MMM-dd-yyyy"
$dateId="cron-svc-build-$dateId"
Write-Host create sql data dir for this build
mkdir "C:\netcore-app\$dateId\app"


if ((Test-Path -Path ".\app\appsettings.Production.json" -PathType Leaf)) {
     Write-Host copy Prod AppSettings
     Copy-Item -Path ".\app\appsettings.Production.json" -Destination "C:\netcore-app\$dateId\app\appsettings.Production.json"
 }

cd "C:\netcore-app"

# Setup the Event Viewer source
$logFileExists = [System.Diagnostics.EventLog]::SourceExists("PSS Cron Service Source");
if (! $logFileExists) {
	Write-Host "Creating event source..."
	[System.Diagnostics.EventLog]::CreateEventSource("PSS Cron Service Source", "Application")
	Write-Host "Event source created successfully"
}


Write-Host get latest source code
#set tls level so we can download the zip
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
Invoke-WebRequest -Uri "https://github.com/Anoobus/cron-api-invocation/archive/master.zip" -OutFile "pss-cron-master.zip"

Write-Host unzip the file
Expand-Archive -LiteralPath "pss-cron-master.zip" -DestinationPath "$dateId"
del  "pss-cron-master.zip"

# Need to determine where it should be installed
$destination = "c:\netcore-app\$dateId\app"

# Setup the service info
$params = @{
	Name = "pss-cron-service"
	BinaryPathName = "$destination\cron.api.invocation.exe"
	DisplayName = "PSS Cron Service"
	StartupType = "Auto"
	Description = "The PSS Cron Service (call APIs at cron schedule intervals)"
}

# If the service already exists stop it before replacing the code
$service = Get-Service -Name $params.Name -ErrorAction SilentlyContinue
if($service -ne $null) {
	Write-Host "Stopping existing service"
	Stop-Service -Name $params.Name
}

# Build and publish the Service
Write-Host "Publishing service"
dotnet publish ".\$dateId\cron-api-invocation-master\cron.api.invocation\cron.api.invocation.csproj" -c Release -o $destination



Write-Host copy install script
Copy-Item -Path ".\$dateId\cron-api-invocation-master\install-service.ps1" -Destination ".\$dateId"
Write-Host "Cleanup Sources"
Remove-Item -LiteralPath "c:\netcore-app\$dateId\cron-api-invocation-master" -Recurse


# Install the Windows Service if needed
if($service -eq $null) {
	Write-Host "Installing the service"
    New-Service @params
}

# Start the Windows Service
Write-Host "Starting the service"
Start-Service -Name $params.Name