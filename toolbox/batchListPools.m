% Copyright (c) Microsoft Corporation

function poolInfo = batchListPools()
% Lists the pools under the Batch account specified in getBatchConfigs.m

batchLib = getBatchLib();
poolInfoNet = batchLib.ListPools();

if (isempty(poolInfoNet) || poolInfoNet.Length == 0)
   poolInfo = [];
   return;
end

% Convert .NET data to more 'MATLAB-friendly' format
% Only a subset of the properties on the .NET object are selected for
% inclusion in the 'MATLAB-friendly' struct.
poolInfo(poolInfoNet.Length) = struct('Id',[],'State',[],'AllocationState',[],...
    'CurrentDedicated',0,'TargetDedicated',0,'MaxTasksPerComputeNode',0, ...
    'VirtualMachineSize',[],'InterComputeNodeCommunicationEnabled',false);
for i = 1:poolInfoNet.Length
    poolNet = poolInfoNet(i);
    pi.Id = char(poolNet.Id);
    pi.State = char(poolNet.State);
    pi.AllocationState = char(poolNet.AllocationState);
    pi.CurrentDedicated = poolNet.CurrentDedicated.Value;
    pi.TargetDedicated = poolNet.TargetDedicated.Value;
    pi.MaxTasksPerComputeNode = poolNet.MaxTasksPerComputeNode.Value;
    pi.VirtualMachineSize = char(poolNet.VirtualMachineSize);
    pi.InterComputeNodeCommunicationEnabled = logical(poolNet.InterComputeNodeCommunicationEnabled.Value);
    poolInfo(i) = pi;
end

end

