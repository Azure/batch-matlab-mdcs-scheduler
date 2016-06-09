% Copyright (c) Microsoft Corporation

function batchLib = getBatchLib()
% Gets an instance of the BatchLib class.

config = getBatchConfigs();
NET.addAssembly(config.BatchLibPath);
% This is a constructor invocation. A new instance is created each time
% this function is called because the user can modify their getBatchConfigs
% function at any time. This approach gives the user more flexibility (ex:
% they can use multiple Batch accounts and Storage accounts and frequently
% switch between them when running their scripts).
batchLib = MatlabBatchLib.BatchLib(config.BatchAccountName,config.BatchServiceUrl, ...
    config.DataStorageAccountName, config.DataShareUrl, config.DataSharePath);

end

