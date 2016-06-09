% Copyright (c) Microsoft Corporation

function batchResizePool(poolId, newNumberOfComputeNodes)
% Resizes the specified pool.

batchLib = getBatchLib();
batchLib.ResizePool(poolId, newNumberOfComputeNodes);

end

