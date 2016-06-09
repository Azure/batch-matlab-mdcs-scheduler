% Copyright (c) Microsoft Corporation

function batchCreateJob(cluster, job, props, isCommunicating)
% Creates a job in the Batch service corresponding to an MDCS job.

% Get settings
config = getBatchConfigs();
batchLib = getBatchLib();

% Copy job data to Azure Storage
batchLib.CopyJobDataToShare(job.Username, cluster.JobStorageLocation, props.JobLocation, job.Name);
fprintf('Job data uploaded to share\n');

% Create the job
batchJobIdNetString =  batchLib.SubmitJob(config.ClusterPoolId, job.Username, num2str(job.ID), ...
    props.NumberOfTasks, props.UserToken, props.LicenseWebID, props.LicenseNumber, props.JobLocation, ...
    isCommunicating, props.TaskLocations, cluster.ClusterMatlabRoot, props.MatlabExecutable, ...
    strtrim(props.MatlabArguments));

batchJobId = char(batchJobIdNetString);
fprintf('Submitted Batch job %s\n', batchJobId);

% Store the Batch job id and job location
jobData = struct('BatchClusterJobId', batchJobId, 'JobLocation', props.JobLocation);
cluster.setJobClusterData(job, jobData);

end
