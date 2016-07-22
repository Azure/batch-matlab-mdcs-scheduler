% Copyright (c) Microsoft Corporation

function batchCreatePool(poolId, numberOfComputeNodes, vmSize, interComputeNodeCommunicationEnabled, varargin)
% Creates a new pool using the specified parameters.
% Required parameters:
% poolId: string. The id of the pool to create.
% numberOfComputeNodes: integer. The desired number of compute nodes in the
%   pool.
% vmSize: The size of the virtual machines in the pool. A description of 
%   Azure VM sizes can be found here:
%   https://azure.microsoft.com/en-us/documentation/articles/cloud-services-sizes-specs/
% interComputeNodeCommunicationEnabled: boolean. Whether to enable direct
%   communication between the nodes in the pool. To support communicating
%   jobs with this Batch pool, you must pass in true.
% Optional params:
% maxTasksPerComputeNode : integer. The maximum number of tasks to schedule
%   against a Batch compute node. For communicating jobs, this value must
%   be 1. If no value is specified and interComputeNodeCommunicationEnabled
%   is true, then a default value of 1 will be used. If no value is
%   specified and interComputeNodeCommunicationEnabled is false, then the
%   value will be set to the number of cores on the specified VM size.

p = inputParser();
p.addRequired('poolId');
p.addRequired('numberOfComputeNodes', @isnumeric);
p.addRequired('vmSize');
p.addRequired('interComputeNodeCommunicationEnabled', @islogical);
p.addOptional('maxTasksPerComputeNode', @isnumeric);
p.parse(poolId, numberOfComputeNodes, vmSize, interComputeNodeCommunicationEnabled, varargin{:});

batchLib = getBatchLib();

if isnumeric(p.Results.maxTasksPerComputeNode)
    % Use the user specified value
    maxTasksPerComputeNode = p.Results.maxTasksPerComputeNode;
    
    % For a communicating job, this must be set to 1.
    if interComputeNodeCommunicationEnabled && maxTasksPerComputeNode ~= 1
        error('When interComputeNodeCommunicationEnabled is set to true, maxTasksPerComputeNode must be set to 1');
    end
else
    if interComputeNodeCommunicationEnabled
        % For a communicating job, this must be set to 1.
        maxTasksPerComputeNode = 1; 
    else   
        % Default to the core count on the specified VM size. 
        vmSizeCoreCountMap = getAzureVmSizeCoreCountMap();
   
        vmSizeLower = lower(p.Results.vmSize);
        if vmSizeCoreCountMap.isKey(vmSizeLower)
            maxTasksPerComputeNode = vmSizeCoreCountMap(vmSizeLower);
        else
            % This default value is in case the specified VM size is not recognized
            % (ex: the mapping table is out of date).
            maxTasksPerComputeNode = 4;
        end
    end
end

batchLib.CreatePool(p.Results.poolId, p.Results.vmSize, p.Results.numberOfComputeNodes, ...
    p.Results.interComputeNodeCommunicationEnabled, maxTasksPerComputeNode);

end