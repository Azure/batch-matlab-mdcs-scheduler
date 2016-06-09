% Copyright (c) Microsoft Corporation

function state = batchGetJobStateFcn(cluster, job, state)

% Store the current filename for the errors, warnings and dctSchedulerMessages
currFilename = mfilename;
if ~isa(cluster, 'parallel.Cluster')
    error('AzureBatch:batchGetJobStateFcn', ...
        'The function %s is for use with clusters created using the parcluster command.', currFilename)
end
if cluster.HasSharedFilesystem
    error('AzureBatch:batchGetJobStateFcn', ...
        'The submit function %s is for use with nonshared filesystems.', currFilename)
end

% Get the information about the actual cluster used
data = cluster.getJobClusterData(job);
if isempty(data)
    % This indicates that the job has not been submitted, so just return
    dctSchedulerMessage(1, '%s: Job cluster data was empty for job with ID %d.', currFilename, job.ID);
    return
end

% Shortcut if the job state is already finished or failed
if (strcmp(state, 'finished') || strcmp(state, 'failed'))
    return;
end;

batchLib = getBatchLib();

jobStateNetStrings = batchLib.GetJobStatus(data.BatchClusterJobId);
jobState = char(jobStateNetStrings(1));
fprintf('Job state: %s\n', jobState);

if strcmpi(jobState, 'finished')
    if (jobStateNetStrings.Length > 1)
        exitCodeMessage = char(jobStateNetStrings(2));
        fprintf('%s\n', exitCodeMessage);
    end
    
    % Because of the short circuit before the .NET call above, we should avoid redundant downloads.
    fprintf('Downloading job data...\n');
    batchLib.CopyJobDataFromShare(job.Username, cluster.JobStorageLocation, data.JobLocation, job.Name);
    fprintf('Downloaded job data from share.\n');
end

state = jobState;

end