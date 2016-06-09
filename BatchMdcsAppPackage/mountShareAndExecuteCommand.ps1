# Copyright (c) Microsoft Corporation

param([string]$shareHost, [string]$account, [string]$key, [string]$sharePath, [string]$command)

cmdkey /add:$shareHost /user:$account /pass:$key

# If the service could universally support one user for all tasks, then we'd
# only have to do the net use command once.  Currently, each task runs
# under a different user account and needs to run its own net use command.
# It appears that running multiple net use commands simultaneously can 
# sometimes return system error 58, so a retry mechanism is added to 
# support pools with MaxTasksPerComputeNode > 1.

# The default seed is set based on the clock, but if multiple tasks start
# around the same time, they can be seeded with the same value, so the 
# process id is used to seed the RNG instead here.
Get-Random -SetSeed $pid

$retry = 0
$isShareMapped = $false

while (!$isShareMapped -and $retry -le 10) 
{
    Write-Output "$([DateTime]::UtcNow.ToString()): Mapping share to local drive."
    net use m: $sharePath

    if ($LASTEXITCODE -ne 0)
    {
        # Sleep for a variable amount of time
        $sleepTime = Get-Random -Minimum 1 -Maximum 20
        Write-Output "$([DateTime]::UtcNow.ToString()): Failed to map share to local drive. Sleeping $sleepTime seconds"
        Start-Sleep -Seconds $sleepTime
        $retry++
    }
    else
    {
        Write-Output "$([DateTime]::UtcNow.ToString()): Mapped share to local drive."
        $isShareMapped = $true
    }
}
if (!$isShareMapped)
{
    Write-Error "Failed to map share. Aborting"
    exit 1
}

CMD /C $command

if ($LASTEXITCODE -ne 0)
{
    Write-Error "MATLAB worker encountered an error, exiting."
    net use m: /delete
    exit 1
}

net use m: /delete
exit 0