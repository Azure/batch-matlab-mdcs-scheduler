% Copyright (c) Microsoft Corporation

function batchConfig = getBatchConfigs()
% Specify your Batch and Storage configurations here.

currentDir = fileparts(mfilename('fullpath'));
batchConfig.BatchLibPath = fullfile(currentDir, 'bin\MatlabBatchLib.dll');

batchConfig.BatchServiceUrl = 'Enter your Batch account endpoint. ex: https://mybatchaccount.westus.batch.azure.com';
batchConfig.BatchAccountName = 'Enter your Batch account name. ex: mybatchaccount';

batchConfig.DataShareUrl = 'Enter your Storage share URL. ex: https://mystorageaccount.file.core.windows.net/matlabshare/';
batchConfig.DataSharePath = 'Enter your Storage share UNC path. ex: \\mystorageaccount.file.core.windows.net\matlabshare';
batchConfig.DataStorageAccountName = 'Enter your Storage account name. ex: mystorageaccount';

batchConfig.ClusterPoolId = 'Enter your Batch pool id. ex: myPool';

end

