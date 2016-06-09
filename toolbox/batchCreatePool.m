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
%   communication between the nodes in the pool. For communicating jobs, you
%   must pass in true.
% Optional params:
% maxTasksPerComputeNode : integer. If none specified, will default to the
%   number of cores on the specified VM size. For communicating jobs, you
%   must specify a value of 1.

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

batchLib.CreatePool(p.Results.poolId, p.Results.vmSize, p.Results.numberOfComputeNodes, ...
    p.Results.interComputeNodeCommunicationEnabled, maxTasksPerComputeNode);

end