% Copyright (c) Microsoft Corporation

function vmSizeCoreCounts = getAzureVmSizeCoreCountMap()
% Gets a mapping of Azure Cloud Service VM sizes to their core counts.
% Values obtained from: 
% https://azure.microsoft.com/en-us/documentation/articles/cloud-services-sizes-specs/

% There are APIs to get Azure VM sizes, but they require AAD
% authentication. For simplicity, this list is hard coded for now.
vmSizeCoreCounts = containers.Map;
% Omit 'extrasmall' since Batch does not support it.
vmSizeCoreCounts('small') = 1;
vmSizeCoreCounts('medium') = 2;
vmSizeCoreCounts('large') = 4;
vmSizeCoreCounts('extralarge') = 8;
vmSizeCoreCounts('a5') = 2;
vmSizeCoreCounts('a6') = 4;
vmSizeCoreCounts('a7') = 8;
vmSizeCoreCounts('a8') = 8;
vmSizeCoreCounts('a9') = 16;
vmSizeCoreCounts('a10') = 8;
vmSizeCoreCounts('a11') = 16;
vmSizeCoreCounts('standard_d1') = 1;
vmSizeCoreCounts('standard_d2') = 2;
vmSizeCoreCounts('standard_d3') = 4;
vmSizeCoreCounts('standard_d4') = 8;
vmSizeCoreCounts('standard_d11') = 2;
vmSizeCoreCounts('standard_d12') = 4;
vmSizeCoreCounts('standard_d13') = 8;
vmSizeCoreCounts('standard_d14') = 16;
vmSizeCoreCounts('standard_d1_v2') = 1;
vmSizeCoreCounts('standard_d2_v2') = 2;
vmSizeCoreCounts('standard_d3_v2') = 4;
vmSizeCoreCounts('standard_d4_v2') = 8;
vmSizeCoreCounts('standard_d5_v2') = 16;
vmSizeCoreCounts('standard_d11_v2') = 2;
vmSizeCoreCounts('standard_d12_v2') = 4;
vmSizeCoreCounts('standard_d13_v2') = 8;
vmSizeCoreCounts('standard_d14_v2') = 16;

end

