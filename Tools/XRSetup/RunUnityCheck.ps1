param([string]$Method = 'CampusEmergency.Editor.CampusXRSceneBuilder.Build', [string]$LogName = 'XRTeleportBuild.log', [switch]$PlayMode)
$ErrorActionPreference = 'Stop'
$project = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$env:SystemRoot = 'C:\Windows'
$env:WINDIR = $env:SystemRoot
$env:SystemDrive = 'C:'
$env:ComSpec = 'C:\Windows\System32\cmd.exe'
$env:APPDATA = Join-Path $env:USERPROFILE 'AppData\Roaming'
$env:LOCALAPPDATA = Join-Path $env:USERPROFILE 'AppData\Local'
$env:ProgramData = 'C:\ProgramData'
$env:ALLUSERSPROFILE = $env:ProgramData
$env:ProgramFiles = 'C:\Program Files'
$env:OS = 'Windows_NT'
$env:PATH = 'C:\Windows\System32;C:\Windows;' + $env:PATH
$editor = 'C:\Program Files\Unity 2022.3.62f3\Editor\Unity.exe'
$arguments = @('-batchmode','-nographics','-quit','-projectPath',('"'+$project+'"'),'-buildTarget','Win64','-executeMethod',$Method,'-logFile',('"'+(Join-Path $project ('Logs\'+$LogName))+'"'))
if ($PlayMode) { $arguments = @($arguments | Where-Object { $_ -ne '-quit' }) }
$process = Start-Process -FilePath $editor -ArgumentList $arguments -WindowStyle Hidden -PassThru
Write-Output ('Unity process: ' + $process.Id)