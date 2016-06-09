# Copyright (c) Microsoft Corporation

# Create destination for MATLAB install in the task shared directory
$matlabRoot = "${env:AZ_BATCH_NODE_SHARED_DIR}\mdcs"
mkdir $matlabRoot

cmd /c "%AZ_BATCH_APP_PACKAGE_MDCS#1%\setup.exe -inputFile %AZ_BATCH_APP_PACKAGE_BATCHMDCS#1%\installer_input.txt"

Write-Output "Launched MATLAB setup, waiting up to 10 minutes for installation to complete..."
Wait-Process "setup" -Timeout 600
Write-Output "Installation completed"

# This file tells MATLAB to use MS-MPI
copy "${env:AZ_BATCH_APP_PACKAGE_BATCHMDCS#1}\mpiLibConf.m" "$matlabRoot\toolbox\distcomp\user\mpiLibConf.m"

# Write the hostname of the machine to a file so it can be used to support pool communicating jobs later
$h = hostname
$h | Out-File -FilePath "${env:AZ_BATCH_NODE_SHARED_DIR}\hostname.txt"