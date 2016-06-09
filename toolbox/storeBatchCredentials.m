% Copyright (c) Microsoft Corporation

function storeBatchCredentials()
% Stores credentials for the Batch and Storage accounts specified in
% getBatchConfigs.m. The credentials are stored in the Windows Credential
% Manager on the local machine. This function must be executed before any
% Batch service operations can be performed.

config = getBatchConfigs();
NET.addAssembly(config.BatchLibPath);

batchAccountKey = input(['Enter the key for Batch account ', config.BatchAccountName, ': '], 's');
MatlabBatchLib.BatchLib.StoreCredential(config.BatchAccountName, batchAccountKey);

dataStorageAccountKey = input(['Enter the key for Storage account ', config.DataStorageAccountName, ': '], 's');
MatlabBatchLib.BatchLib.StoreCredential(config.DataStorageAccountName, dataStorageAccountKey);

end

