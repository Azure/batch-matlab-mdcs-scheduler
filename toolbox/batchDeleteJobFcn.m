% Copyright (c) Microsoft Corporation

function batchDeleteJobFcn(cluster, job)

% Store the current filename for the errors, warnings and dctSchedulerMessages
currFilename = mfilename;
if ~isa(cluster, 'parallel.Cluster')
    error('AzureBatch:batchDeleteJobFcn', ...
        'The function %s is for use with clusters created using the parcluster command.', currFilename)
end
if cluster.HasSharedFilesystem
    error('AzureBatch:batchDeleteJobFcn', ...
        'The submit function %s is for use with nonshared filesystems.', currFilename)
end

 % Get the information about the actual cluster used
data = cluster.getJobClusterData(job);
if isempty(data)
    % This indicates that the job has not been submitted, so just return
    dctSchedulerMessage(1, '%s: Job cluster data was empty for job with ID %d.', currFilename, job.ID);
    return
end

batchLib = getBatchLib();
batchLib.DeleteJob(data.BatchClusterJobId, job.Username, data.JobLocation);

end

