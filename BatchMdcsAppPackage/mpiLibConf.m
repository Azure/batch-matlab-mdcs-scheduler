% Copyright (c) Microsoft Corporation

function [primaryLib, extras] = mpiLibConf
% Override MATLAB settings to force the use of MS-MPI
[primaryLib, extras] = distcomp.mpiLibConfs( 'msmpi' );
