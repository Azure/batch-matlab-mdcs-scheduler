% Copyright (c) Microsoft Corporation

function batchIndependentSubmitFcn(cluster, job, props)

% Store the current filename for the errors, warnings and dctSchedulerMessages
currFilename = mfilename;
if ~isa(cluster, 'parallel.Cluster')
    
    error('AzureBatch:batchIndependentSubmitFcn', ...
        'The function %s is for use with clusters created using the parcluster command.', currFilename)
end

if cluster.HasSharedFilesystem
    error('AzureBatch:batchIndependentSubmitFcn', ...
        'The submit function %s is for use with nonshared filesystems.', currFilename)
end

if ~strcmpi(cluster.OperatingSystem, 'windows')
    error('AzureBatch:batchIndependentSubmitFcn', ...
        'The submit function %s only supports clusters with Windows OS.', currFilename)
end

batchCreateJob(cluster, job, props, 'false');

end