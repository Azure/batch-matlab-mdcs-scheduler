% Copyright (c) Microsoft Corporation

function batchDeletePool(poolId)
% Deletes the specified Batch pool.

batchLib = getBatchLib();
batchLib.DeletePool(poolId);

end
