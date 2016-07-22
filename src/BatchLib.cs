// Copyright (c) Microsoft Corporation

using Microsoft.Azure.Batch;
using Microsoft.Azure.Batch.Auth;
using Microsoft.Azure.Batch.Common;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.DataMovement;
using Microsoft.WindowsAzure.Storage.File;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace MatlabBatchLib
{
    /// <summary>
    /// A helper library for interacting with the Batch service in MATLAB.
    /// </summary>
    public class BatchLib
    {
        private const string MatlabLocalShareRootDrive = "M:\\";
        private const string MountShareAndExecuteCommandScript = "${env:AZ_BATCH_APP_PACKAGE_BATCHMDCS#1}\\mountShareAndExecuteCommand.ps1";
        private const string SetupMatlabCommandLine = "powershell.exe -executionpolicy unrestricted -command \"& ${env:AZ_BATCH_APP_PACKAGE_BATCHMDCS#1}\\BatchPoolInstallMDCS.ps1\"";
        private const string MdcsAppPackageId = "mdcs";
        private const string MdcsAppPackageVersion = "1";
        private const string BatchMdcsAppPackageId = "batchmdcs";
        private const string BatchMdcsAppPackageVersion = "1";
        private const string NodeMatlabRoot = @"%AZ_BATCH_NODE_SHARED_DIR%\mdcs";
        private const string NodeMpiDir = NodeMatlabRoot + @"\bin\win64\msmpi";
        private const string NodeMpiExecPath = NodeMpiDir + @"\mpiexec.exe";
        private const string NodeSmpdPath = NodeMpiDir + @"\smpd.exe";
        private const string MpiPort = "28350";
        private const string NodeHostNameFile = "shared\\hostname.txt";
        private const string HostsFileName = "hosts";
        private const string MatlabJobActiveState = "running";
        private const string MatlabJobFinishedState = "finished";

        private BatchClient batchClient;

        private string dataStorageAccountName;
        private string dataStorageAccountKey;
        private string dataShareUrl;
        private string dataSharePath;

        /// <summary>
        /// Initializes an instance of BatchLib.
        /// </summary>
        /// <param name="batchAccountName">The name of the Batch account to use.</param>
        /// <param name="batchServiceUrl">The endpoint of the Batch account to use.</param>
        /// <param name="dataStorageAccountName">The name of the Storage account used for MATLAB job data.</param>
        /// <param name="dataShareUrl">The url of the Storage file share used for MATLAB job data.</param>
        /// <param name="dataSharePath">The UNC path of the Storage file share used for MATLAB job data.</param>
        public BatchLib(string batchAccountName, string batchServiceUrl, string dataStorageAccountName, string dataShareUrl, string dataSharePath)
        {
            if (string.IsNullOrEmpty(batchAccountName))
            {
                throw new ArgumentNullException("batchAccountName");
            }
            if (string.IsNullOrEmpty(batchServiceUrl))
            {
                throw new ArgumentNullException("batchServiceUrl");
            }
            if (string.IsNullOrEmpty(dataStorageAccountName))
            {
                throw new ArgumentNullException("dataStorageAccountName");
            }
            if (string.IsNullOrEmpty(dataShareUrl))
            {
                throw new ArgumentNullException("dataShareUrl");
            }
            if (string.IsNullOrEmpty(dataSharePath))
            {
                throw new ArgumentNullException("dataSharePath");
            }

            string batchAccountKey = Credentials.GetStoredKey(batchAccountName);
            this.dataStorageAccountKey = Credentials.GetStoredKey(dataStorageAccountName);

            BatchSharedKeyCredentials batchCreds = new BatchSharedKeyCredentials(batchServiceUrl, batchAccountName, batchAccountKey);
            this.batchClient = BatchClient.Open(batchCreds);
            
            // Set a default retry policy
            LinearRetry defaultRetryPolicy = new LinearRetry(TimeSpan.FromSeconds(10), 5);
            this.batchClient.CustomBehaviors.Add(new RetryPolicyProvider(defaultRetryPolicy));

            this.dataStorageAccountName = dataStorageAccountName;

            string trailingSlashShareUrl = dataShareUrl;
            if (!dataShareUrl.EndsWith("/"))
            {
                trailingSlashShareUrl = dataShareUrl + "/";
            }
            this.dataShareUrl = trailingSlashShareUrl;

            this.dataSharePath = dataSharePath;

            // Recommended settings for Data Movement Lib
            int parallelOps = Environment.ProcessorCount * 8;
            ServicePointManager.DefaultConnectionLimit = parallelOps;
            TransferManager.Configurations.ParallelOperations = parallelOps;
        }

        /// <summary>
        /// Stores a credential in the Windows Credential Manager on the local machine.
        /// </summary>
        /// <param name="target">The target name of the credential.</param>
        /// <param name="key">The credential key.</param>
        /// <remarks>This is the public method used by MATLAB, since it cannot directly access the internal Credentials class.</remarks>
        public static void StoreCredential(string target, string key)
        {
            Credentials.StoreCredential(target, key);
        }

        # region Storage methods

        /// <summary>
        /// Copies MATLAB job data from the local machine to the Storage file share.
        /// </summary>
        /// <param name="jobUser">The user who iniated the MATLAB job.</param>
        /// <param name="localJobDataRoot">The root path on the local machine where MATLAB job data is stored.</param>
        /// <param name="jobDataDirectory">The directory containing the files for the job.</param>
        public void CopyJobDataToShare(string jobUser, string localJobDataRoot, string jobDataDirectory)
        {
            CloudFileDirectory userCloudDir = GetUserShareDirectory(jobUser, shouldCreateDirectories: true);

            // MATLAB job directories can be reused, so clean the job files before uploading anything to avoid
            // issues from leftover files of old tasks.
            CleanCloudDirectory(userCloudDir, jobDataDirectory);

            // Upload the job files in the job data directory root
            // The names of files associated with the job in the root directory will all begin with "<jobDataDirectory>."
            UploadDirectoryOptions jobFilesInRootOptions = new UploadDirectoryOptions()
            {
                SearchPattern = jobDataDirectory + ".*",
                Recursive = false
            };
            Task jobFilesTask = TransferManager.UploadDirectoryAsync(localJobDataRoot, userCloudDir, jobFilesInRootOptions, null);
            
            // Upload the metadata file in the job data directory root
            const string matlabMetadataFile = "matlab_metadata.mat";
            string matlabMetadataLocalPath = string.Join("\\", localJobDataRoot, matlabMetadataFile);
            CloudFile metadataCloudFile = userCloudDir.GetFileReference(matlabMetadataFile);
            Task metadataTask = metadataCloudFile.UploadFromFileAsync(matlabMetadataLocalPath, FileMode.Open);

            // Upload the job directory
            string jobDirLocalPath = string.Join("\\", localJobDataRoot, jobDataDirectory);
            CloudFileDirectory jobCloudDirectory = userCloudDir.GetDirectoryReference(jobDataDirectory);
            Task jobDirTask = TransferManager.UploadDirectoryAsync(jobDirLocalPath, jobCloudDirectory);

            Task.WaitAll(jobFilesTask, metadataTask, jobDirTask);
        }

        /// <summary>
        /// Gets a CloudFileShare instance representing the job data share.
        /// </summary>
        /// <returns>A CloudFileShare instance representing the job data share.</returns>
        private CloudFileShare GetJobFileShare()
        {
            bool useHTTPS = true;
            StorageCredentials creds = new StorageCredentials(this.dataStorageAccountName, this.dataStorageAccountKey);
            CloudStorageAccount storageAccount = new CloudStorageAccount(creds, useHTTPS);
            CloudFileShare share = new CloudFileShare(new Uri(this.dataShareUrl), creds);
            return share;
        }

        /// <summary>
        /// Gets a CloudFileDirectory instance representing a user's directory on the data share.
        /// </summary>
        /// <param name="userDirectory">The name of the user's directory on the share.</param>
        /// <param name="shouldCreateDirectories">Whether ot not the directories should be created.</param>
        /// <returns>A CloudFileDirectory instance representing a user's directory on the data share.</returns>
        private CloudFileDirectory GetUserShareDirectory(string userDirectory, bool shouldCreateDirectories)
        {
            CloudFileShare share = GetJobFileShare();
            if (shouldCreateDirectories)
            {
                share.CreateIfNotExists();
            }

            CloudFileDirectory rootCloudDir = share.GetRootDirectoryReference();
            CloudFileDirectory userCloudDir = rootCloudDir.GetDirectoryReference(userDirectory);
            if (shouldCreateDirectories)
            {
                userCloudDir.CreateIfNotExists();
            }

            return userCloudDir;
        }

        /// <summary>
        /// Recursively deletes a CloudFileDirectory's contents, optionally scoped to only a single job's files.
        /// </summary>
        /// <param name="dir">The CloudFileDirectory to delete the contents of.</param>
        /// <param name="jobDataDirectory">If specified, only the specified job data directory and files associated 
        /// with the job in the root directory will be deleted. Otherwise, all directories will be deleted.
        /// </param>
        private void CleanCloudDirectory(CloudFileDirectory dir, string jobDataDirectory)
        {
            if (!dir.Exists())
            {
                return;
            }

            List<Task> fileDeleteTasks = new List<Task>();
            foreach (IListFileItem fileItem in dir.ListFilesAndDirectories())
            {
                if (fileItem is CloudFile)
                {
                    CloudFile cloudFile = (CloudFile)fileItem;
                    // The names of files associated with the job in the root directory will all begin with "<jobDataDirectory>."
                    // The "." is added to the job name to ensure that we don't delete Job10's files when the user passes in "Job1"
                    if (string.IsNullOrEmpty(jobDataDirectory) || cloudFile.Name.StartsWith(jobDataDirectory + ".", StringComparison.OrdinalIgnoreCase))
                    {
                        fileDeleteTasks.Add(cloudFile.DeleteAsync());
                    }
                }
                else if (fileItem is CloudFileDirectory)
                {
                    CloudFileDirectory cloudDir = ((CloudFileDirectory)fileItem);
                    if (string.IsNullOrEmpty(jobDataDirectory) || cloudDir.Name.Equals(jobDataDirectory, StringComparison.OrdinalIgnoreCase))
                    {
                        // Directory must be empty before it can be deleted, so we recursively delete all subdirectories.
                        CleanCloudDirectory(cloudDir, null);
                        cloudDir.Delete();
                    }
                }
            }

            Task.WaitAll(fileDeleteTasks.ToArray());
        }

        /// <summary>
        /// Copies MATLAB job data from a Storage share to the local machine.
        /// </summary>
        /// <param name="jobUser">The user who initiated the MATLAB job.</param>
        /// <param name="localJobDataRoot">The root path on the local machine where MATLAB job data is stored.</param>
        /// <param name="jobDataDirectory">The directory containing the files for the job.</param>
        public void CopyJobDataFromShare(string jobUser, string localJobDataRoot, string jobDataDirectory)
        {
            CloudFileDirectory userCloudDir = GetUserShareDirectory(jobUser, shouldCreateDirectories: false);
            CloudFileDirectory jobCloudDirectory = userCloudDir.GetDirectoryReference(jobDataDirectory);

            // Ideally, we could specify a search pattern like "Job1.*" and target the whole directory, but the Storage library only supports
            // exact matches for downloading a CloudFileDirectory.  Therefore, we copy specific job output files by name.
            // The names of files associated with the job in the root directory will all begin with "<jobDataDirectory>."
            string jobOutputFileName = jobDataDirectory + ".out.mat";
            string jobCommonFileName = jobDataDirectory + ".common.mat";
            string jobOutputLocalPath = string.Join("\\", localJobDataRoot, jobOutputFileName);
            string jobCommonLocalPath = string.Join("\\", localJobDataRoot, jobCommonFileName);

            CloudFile outCloudFile = userCloudDir.GetFileReference(jobOutputFileName);
            CloudFile commonCloudFile = userCloudDir.GetFileReference(jobCommonFileName);
            FileRequestOptions noMd5Check = new FileRequestOptions()
            {
                // Azure Storage will store the MD5 hash for a file when it is uploaded, but it is the user's responsibility
                // to update the stored MD5 hash whenever the file is modified. Each compute node's MATLAB instance will modify
                // multiple files on the file share, and we don't keep track of these changes, so the MD5 validation needs to
                // be disabled to avoid exceptions on download. In order to enable the MD5 validation, the MD5 hashes of each
                // file in the share would need to be recalculated sometime between the completion of the MATLAB worker tasks
                // and this file download.
                DisableContentMD5Validation = true
            };
            AccessCondition emptyAccessCondition = AccessCondition.GenerateEmptyCondition();
            Task jobOutputDownloadTask = outCloudFile.DownloadToFileAsync(jobOutputLocalPath, FileMode.Create, emptyAccessCondition, noMd5Check, null);
            Task jobCommonDownloadTask = commonCloudFile.DownloadToFileAsync(jobCommonLocalPath, FileMode.Create, emptyAccessCondition, noMd5Check, null);

            // Download the job files in the job directory
            string jobDirLocalPath = string.Join("\\", localJobDataRoot, jobDataDirectory);
            DownloadDirectoryOptions downloadOptions = new DownloadDirectoryOptions
            {
                Recursive = true,
                DisableContentMD5Validation = true
            };
            TransferContext alwaysOverwrite = new TransferContext()
            {
                OverwriteCallback = (source, dest) => { return true; }
            };
            Task jobDirectoryDownloadTask = TransferManager.DownloadDirectoryAsync(jobCloudDirectory, jobDirLocalPath, downloadOptions, alwaysOverwrite);

            Task.WaitAll(jobOutputDownloadTask, jobCommonDownloadTask, jobDirectoryDownloadTask);
        }

        # endregion

        # region Batch Job/Task methods 

        /// <summary>
        /// Submits a job to the Batch service corresponding to a MATLAB job.
        /// </summary>
        /// <param name="poolId">The pool to schedule the job against.</param>
        /// <param name="jobUser">The user who initiated the MATLAB job.</param>
        /// <param name="matlabJobId">The MATLAB job id.</param>
        /// <param name="numTasks">The number of tasks to create under the job.</param>
        /// <param name="licenseUserToken">The MATLAB license manager user token.</param>
        /// <param name="licenseWebId">The MATLAB license manager web id.</param>
        /// <param name="licenseNumber">The MDCS license number.</param>
        /// <param name="jobDataDirectory">The MATLAB job's data directory.</param>
        /// <param name="isCommunicatingStr">Whether the MATLAB job is a communicating job. Valid values are "true" or "false".</param>
        /// <param name="taskLocations">The MATLAB tasks' data directories.</param>
        /// <param name="localMatlabRoot">The MATLAB cluster root on the local machine.</param>
        /// <param name="localMatlabExe">The path to the MATLAB exe on the local machine.</param>
        /// <param name="matlabArgs">The arguments to pass to the MATLAB executable.</param>
        /// <returns>The id of the job created in the Batch service.</returns>
        public string SubmitJob(
            string poolId,
            string jobUser,
            string matlabJobId,
            int numTasks,
            string licenseUserToken,
            string licenseWebId,
            string licenseNumber,
            string jobDataDirectory,
            string isCommunicatingStr,
            string[] taskLocations,
            string localMatlabRoot,
            string localMatlabExe,
            string matlabArgs)
        {
            // We could use a container class/struct to wrap up all these parameters and create a simpler method signature.
            // The MATLAB script would need to initialize the object first and pass it into the .NET method call.

            bool isCommunicating = bool.Parse(isCommunicatingStr);

            string nodeUserShare = MatlabLocalShareRootDrive + jobUser;
            Uri uri = new Uri(this.dataSharePath);
            string shareHost = uri.Host;

            JobOperations jobOps = this.batchClient.JobOperations;
            string batchJobId = string.Format("{0}-{1}-{2}", jobUser, matlabJobId, DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            CloudJob unboundJob = jobOps.CreateJob(batchJobId, new PoolInformation() { PoolId = poolId });

            // Set the environment variables MATLAB expects.
            List<EnvironmentSetting> jobEnvironmentSettings = new List<EnvironmentSetting>();
            jobEnvironmentSettings.Add(new EnvironmentSetting("MDCE_STORAGE_LOCATION", nodeUserShare));
            jobEnvironmentSettings.Add(new EnvironmentSetting("MDCE_JOB_LOCATION", jobDataDirectory));
            jobEnvironmentSettings.Add(new EnvironmentSetting("MLM_WEB_USER_CRED", licenseUserToken));
            jobEnvironmentSettings.Add(new EnvironmentSetting("MLM_WEB_ID", licenseWebId));
            jobEnvironmentSettings.Add(new EnvironmentSetting("MDCE_LICENSE_NUMBER", licenseNumber));

            jobEnvironmentSettings.Add(new EnvironmentSetting("MDCE_STORAGE_CONSTRUCTOR", "makeFileStorageObject"));
            jobEnvironmentSettings.Add(new EnvironmentSetting("MLM_WEB_LICENSE", "true"));
            jobEnvironmentSettings.Add(new EnvironmentSetting("MDCE_DEBUG", "true"));

            if (isCommunicating)
            {
                jobEnvironmentSettings.Add(new EnvironmentSetting("MDCE_DECODE_FUNCTION", "parallel.cluster.generic.communicatingDecodeFcn"));
                jobEnvironmentSettings.Add(new EnvironmentSetting("MDCE_TOTAL_TASKS", numTasks.ToString()));
                jobEnvironmentSettings.Add(new EnvironmentSetting("MDCE_CMR", NodeMatlabRoot));
            }
            else
            {
                jobEnvironmentSettings.Add(new EnvironmentSetting("MDCE_DECODE_FUNCTION", "parallel.cluster.generic.independentDecodeFcn"));
            }

            unboundJob.CommonEnvironmentSettings = jobEnvironmentSettings;

            if (isCommunicating)
            {
                // MATLAB pool communicating jobs use hostnames to communicate with each other, and resolution doesn't seem to work by default 
                // in Batch pools, so we use a job preparation task to overwrite each node's hosts file with the nodes' current IP addresses.
                // There may be network settings or JVM settings (MATLAB uses Java methods for communication between nodes) to  enable hostname 
                // resolution in Batch pool. This code should also be revisited if virtual networks are supported in Batch. With VNets, we could
                // support parpool interactive scenarios by joining the user's local machine to the virtual network.
                UploadHostsFile(poolId, jobUser, jobDataDirectory);

                string copyCommand = string.Format("copy {0}\\{1}\\{2} %WinDir%\\System32\\drivers\\etc\\{2} /Y", nodeUserShare, jobDataDirectory, HostsFileName);
                JobPreparationTask jobPrepTask = new JobPreparationTask()
                {
                    Id = "copyHostsFile",
                    RunElevated = true,
                    WaitForSuccess = true,
                    CommandLine = 
                        string.Format("powershell.exe -executionpolicy unrestricted -command \"& {0} -shareHost '{1}' -account '{2}' -key '{3}' -sharePath '{4}' -command '{5}'\"",
                        MountShareAndExecuteCommandScript, shareHost, this.dataStorageAccountName, this.dataStorageAccountKey, this.dataSharePath, copyCommand)
                };
                unboundJob.JobPreparationTask = jobPrepTask;
            }

            unboundJob.Commit();

            // MATLAB will give us the MATLAB root and MATLAB exe path on this machine, which goes under C:\Program Files by default. 
            // We install MATLAB to a custom location on the node, so we have to tweak the values that the local MATLAB installation
            // gives us.
            string nodeMatlabExe = localMatlabExe.Replace(localMatlabRoot, NodeMatlabRoot);
            string nodeMatlabCommand = string.Format("{0} {1}", nodeMatlabExe, matlabArgs);

            if (isCommunicating)
            {
                // Communicating jobs require MPI
                string mpiTaskCommandLine = string.Format("cmd /c {0} -p {1} -wdir %AZ_BATCH_TASK_SHARED_DIR% {2}", NodeMpiExecPath, MpiPort, nodeMatlabCommand);
                CloudTask multiInstanceTask = new CloudTask("1", mpiTaskCommandLine);
                MultiInstanceSettings multiInstanceSettings = new MultiInstanceSettings(numTasks);
                string smpdCommand = string.Format("{0} -p {1} -d", NodeSmpdPath, MpiPort);
                string coordinatingCommand = 
                    string.Format("powershell.exe -executionpolicy unrestricted -command \"& {0} -shareHost '{1}' -account '{2}' -key '{3}' -sharePath '{4}' -command '{5}'\"",
                    MountShareAndExecuteCommandScript, shareHost, this.dataStorageAccountName, this.dataStorageAccountKey, this.dataSharePath, smpdCommand);
                multiInstanceSettings.CoordinationCommandLine = string.Format("cmd /c start {0}", coordinatingCommand);
                multiInstanceTask.MultiInstanceSettings = multiInstanceSettings;

                jobOps.AddTask(batchJobId, multiInstanceTask);
            }
            else
            {
                string taskCommand = 
                    string.Format("powershell.exe -executionpolicy unrestricted -command \"& {0} -shareHost '{1}' -account '{2}' -key '{3}' -sharePath '{4}' -command '{5}'\"",
                    MountShareAndExecuteCommandScript, shareHost, this.dataStorageAccountName, this.dataStorageAccountKey, this.dataSharePath, nodeMatlabCommand);

                List<CloudTask> tasksToAdd = new List<CloudTask>();
                for (int t = 1; t <= numTasks; t++)
                {
                    string taskId = t.ToString();
                    CloudTask mlTask = new CloudTask(taskId, taskCommand);
                    mlTask.EnvironmentSettings = new List<EnvironmentSetting>()
                    {
                        new EnvironmentSetting("MDCE_TASK_LOCATION", taskLocations[t - 1])
                    };
                    tasksToAdd.Add(mlTask);
                }

                jobOps.AddTask(batchJobId, tasksToAdd);
            }

            return batchJobId;
        }

        /// <summary>
        /// Upload a hosts file containing each VM's host name and IP Address to Storage.
        /// <param name="poolId">The id of the pool</param>
        /// <param name="jobUser">The job user</param>
        /// <param name="jobDataDirectory">The job data directory</param>
        /// </summary>
        private void UploadHostsFile(string poolId, string jobUser, string jobDataDirectory)
        {
            PoolOperations poolOps = this.batchClient.PoolOperations;

            ODATADetailLevel detailLevel = new ODATADetailLevel()
            {
                SelectClause = "id,ipAddress"
            };

            // Compose the contents of the hosts file. Each line follows the format "<IP Address> <Host name>"
            StringBuilder hostsFileContent = new StringBuilder();
            try
            {
                foreach (ComputeNode node in poolOps.ListComputeNodes(poolId, detailLevel))
                {
                    // In the start task, each node writes its hostname to a file, so we read this file from every node to 
                    // compose the hosts file.
                    // This approach was chosen for simplicity, but alternate approaches could be considered to minimize 
                    // the number of calls to the service. For example, the nodes could write the files to the Storage share.
                    NodeFile nodeHostNameFile = node.GetNodeFile(NodeHostNameFile);
                    string nodeHostName = nodeHostNameFile.ReadAsString();

                    hostsFileContent.AppendFormat("{0} {1}", node.IPAddress, nodeHostName);
                    hostsFileContent.AppendLine();
                }
            }
            catch (BatchException ex)
            {
                if (ex?.RequestInformation?.BatchError?.Code == BatchErrorCodeStrings.FileNotFound)
                {
                    throw new InvalidOperationException("Each Batch node must successfully complete its start task before a communicating job can be scheduled.");
                }
                throw;
            }

            // Upload the hosts file to the job's data directory in Storage.
            CloudFileDirectory userCloudDir = GetUserShareDirectory(jobUser, shouldCreateDirectories: false);
            CloudFileDirectory jobCloudDirectory = userCloudDir.GetDirectoryReference(jobDataDirectory);
            CloudFile hostsFile = jobCloudDirectory.GetFileReference(HostsFileName);
            hostsFile.UploadText(hostsFileContent.ToString());
        }

        /// <summary>
        /// Gets the status of a job in the Batch service.
        /// </summary>
        /// <param name="batchJobId">The id of the Batch service job.</param>
        /// <returns>A string representation of the job status. The first element in the array will be "Active" if there are still tasks executing, 
        /// or "Completed" if all tasks have completed. For a completed job, the second element in the array will indicate whether or not any jobs
        /// completed with a non-zero exit code.
        /// </returns>
        public string[] GetJobStatus(string batchJobId)
        {
            JobOperations jobOps = this.batchClient.JobOperations;
            ODATADetailLevel jobDetailLevel = new ODATADetailLevel()
            {
                SelectClause = "id,state"
            };

            // Check for job already being in the Completed or Terminating states. 
            CloudJob job = jobOps.GetJob(batchJobId, detailLevel: jobDetailLevel);
            if (job.State == JobState.Completed || job.State == JobState.Terminating)
            {
                return new string[] { MatlabJobFinishedState };
            }

            // Get state of all tasks associated with the job
            ODATADetailLevel taskDetailLevel = new ODATADetailLevel()
            {
                SelectClause = "id,state,executionInfo"
            };

            List<string> nonZeroExitCodeTasks = new List<string>();
            int totalTasks = 0;
            int completedTasks = 0;
            foreach (CloudTask t in jobOps.ListTasks(batchJobId, detailLevel: taskDetailLevel))
            {
                totalTasks++;
                if (t.State == TaskState.Completed) 
                {
                    completedTasks++;
                    if (t?.ExecutionInformation.ExitCode != 0)
                    {
                        nonZeroExitCodeTasks.Add(t.Id);
                    }
                }
            }

            // If all tasks have finished then terminate the job
            if (completedTasks == totalTasks)
            {
                job.Terminate();

                string exitCodeString = string.Empty;
                if (nonZeroExitCodeTasks.Count > 0)
                {
                    exitCodeString = "WARNING! The following tasks completed with a non-zero exit code: " + string.Join(", ", nonZeroExitCodeTasks);
                }
                else
                {
                    exitCodeString = "All tasks completed with an exit code of 0.";
                }

                return new string[] { MatlabJobFinishedState, exitCodeString };
            }
            else
            {
                return new string[] { MatlabJobActiveState };
            }
        }

        /// <summary>
        /// Deletes the specified Azure Batch job along with its data in Azure Storage.
        /// </summary>
        /// <param name="jobId">The id of the Batch job to delete.</param>
        /// <param name="jobUser">The job user.</param>
        /// <param name="jobDataDirectory">The job data directory.</param>
        public void DeleteJob(string jobId, string jobUser, string jobDataDirectory)
        {
            // Delete the job data in Azure Storage
            CloudFileDirectory userCloudDir = GetUserShareDirectory(jobUser, shouldCreateDirectories: false);

            CleanCloudDirectory(userCloudDir, jobDataDirectory);

            // Delete the job in the Batch service
            JobOperations jobOps = this.batchClient.JobOperations;
            jobOps.DeleteJob(jobId);
        }

        # endregion

        #region Batch Pool methods

        /// <summary>
        /// Creates a pool in the Batch service.
        /// </summary>
        /// <param name="poolId">The id of the pool to create.</param>
        /// <param name="vmSize">The Azure VM size of the compute nodes in the pool.</param>
        /// <param name="targetDedicated">The target number of dedicated compute nodes in the pool.</param>
        /// <param name="interNodeCommunicationEnabled">Whether communication between the nodes in the pool is enabled.</param>
        /// <param name="maxTasksPerComputeNode">The maximum number of tasks that can be scheduled on each compute node.</param>
        public void CreatePool(string poolId, string vmSize, int targetDedicated, bool interNodeCommunicationEnabled, int maxTasksPerComputeNode)
        {
            PoolOperations poolOps = this.batchClient.PoolOperations;

            // Create the Pool
            CloudServiceConfiguration cloudServiceConfig = new CloudServiceConfiguration("4");
            CloudPool pool = poolOps.CreatePool(poolId, vmSize, cloudServiceConfig, targetDedicated);

            // Initialize the pool nodes by copying the application packages
            List<ApplicationPackageReference> appPackageList = new List<ApplicationPackageReference>();
            ApplicationPackageReference mdcsAppPackage = new ApplicationPackageReference()
            {
                ApplicationId = MdcsAppPackageId,
                Version = MdcsAppPackageVersion
            };
            appPackageList.Add(mdcsAppPackage);
            ApplicationPackageReference batchMdcsAppPackage = new ApplicationPackageReference()
            {
                ApplicationId = BatchMdcsAppPackageId,
                Version = BatchMdcsAppPackageVersion
            };
            appPackageList.Add(batchMdcsAppPackage);
            pool.ApplicationPackageReferences = appPackageList;

            // The start task will install MDCS
            pool.StartTask = new StartTask()
            {
                CommandLine = SetupMatlabCommandLine,
                WaitForSuccess = true,
                RunElevated = true
            };

            // Set other properties for the pool
            pool.InterComputeNodeCommunicationEnabled = interNodeCommunicationEnabled;
            pool.MaxTasksPerComputeNode = maxTasksPerComputeNode;

            pool.Commit();
        }

        /// <summary>
        /// Resizes the specified pool.
        /// </summary>
        /// <param name="poolId">The id of the pool to resize.</param>
        /// <param name="newTargetDedicated">The new target number of dedicated compute nodes.</param>
        public void ResizePool(string poolId, int newTargetDedicated)
        {
            this.batchClient.PoolOperations.ResizePool(poolId, newTargetDedicated);
        }

        /// <summary>
        /// Deletes the specified pool.
        /// </summary>
        /// <param name="poolId">The id of the pool to delete.</param>
        public void DeletePool(string poolId)
        {
            this.batchClient.PoolOperations.DeletePool(poolId);
        }

        /// <summary>
        /// Lists the pools under the Batch account.
        /// </summary>
        /// <returns>An array of CloudPool instaces representing all pools under the Batch account.</returns>
        public CloudPool[] ListPools()
        {
            PoolOperations poolOps = this.batchClient.PoolOperations;
            ODATADetailLevel detailLevel = new ODATADetailLevel();

            CloudPool[] pools = poolOps.ListPools(detailLevel).ToArray();
            return pools;
        }

        # endregion
    }
}
