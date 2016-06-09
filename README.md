# batch-matlab-mdcs-scheduler

This project provides an implementation of the MDCS Generic Scheduler using the Azure Batch service.

### Setup and Usage

#### Azure Setup

1. Get your MATLAB installation files. These steps are accurate as of May 2016, but the [MathWorks website](http://www.mathworks.com/) may change its layout in the future.
    * Log into your MathWorks account, go to the **My Account** page, and click the **Download Products** button.
    * Choose the version of MATLAB you want, and select the **Windows (64-bit)** option.
    * Select the **MATLAB Distributed Computing Server** tab, and click the button to download the installer.
    * Open the installer, and choose the **Log in with a MathWorks account** option.
    * Accept the license agreement and enter your MathWorks account credentials.
    * Select the **Download Only** option. Select the **Windows (64-bit)** option, and choose a path to download the installation files to.
    * Create a .zip file called MDCS.zip to contain the downloaded files. Make sure your .zip does not have a top level folder. setup.exe should be at the top level.  

2. Supply your file installation key and create the Batch setup package.
    * Log into your MathWorks account, go to the **My Account** page, and click the **View My Licenses** button.
    * Select your MATLAB Distributed Computing Server license, navigate to the **Activation and Installation** tab, and click the **Get File Installation Key** button.
    * Select your product version, and a long string of numbers should appear. Copy it to your clipboard.
    * In the BatchMdcsAppPackage folder in this package, open installer_input.txt.
    * In the second line of the file, paste your file installation key after the "=" character. Ensure there are no spaces between the "=" character and the beginning of your file installation key.
    * Put all files in a .zip file called BatchMdcs.zip. The .zip should just contain the files without a top level folder.

3. Create an Azure Batch account following the steps here: https://azure.microsoft.com/documentation/articles/batch-account-create-portal/

4. Create an Azure Storage account following the steps here: https://azure.microsoft.com/documentation/articles/storage-create-storage-account/#create-a-storage-account  
   This Storage account will be used to store the MATLAB job data.
    * Create a file share under this account.	

5. Create an Azure Storage account to house the MATLAB installation and setup files.
    * Log into https://portal.azure.com and create a new Storage account.
    * Navigate to the Batch account you created in step 3 and link it to this new Storage account.

6. Upload 2 application packages. For more information on application packages, see the following article: https://azure.microsoft.com/documentation/articles/batch-application-packages/
    * Add a new application to your Batch account:
        * Application Id: mdcs
        * Version: 1
        * Application Package: Enter the path to the MDCS.zip you created in step 1.
    * Add another new application:
        * Application Id: batchmdcs
        * Version: 1
        * Application Package: Enter the path to the BatchMdcs.zip you created in step 2.

#### Building and Installing the Toolbox

1. Install Visual Studio 2015. You can download the free Community edition here: https://www.visualstudio.com/products/visual-studio-community-vs

2. Install MATLAB and the [Parallel Computing Toolbox](http://www.mathworks.com/products/parallel-computing/). MATLAB R2015a and Parallel Computing Toolbox 6.6 have been verified to work with this project.

3. Open src\MatlabBatchLib.sln in Visual Studio.

4. Build the solution. If you see errors, please ensure that the required NuGet packages are downloaded.

5. Create and install the MATLAB toolbox
    * Open MATLAB on your local machine.
    * Under the toolbar's **Home** tab, go to the Resources section and select **Add-Ons > Package Toolbox**.
    * In the **Toolbox Folder** section of the toolbar, click the + button and select this project's "toolbox" folder.
    * Name the package **Batch MDCS Scheduler**.
    * Click the Package button.
    * Double click the .mltbx file that was created and install the toolbox.
	
#### Batch Cluster Setup

1. Create a Generic Cluster Profile in MATLAB.
    * Open MATLAB on your local machine.
    * In the "Home" tab, select **Parallel > Manage Cluster Profiles**.
    * Click the **Import** button, and select the BatchProfile.settings file at the root of this project.
    * Once imported, edit the top section's JobStorageLocation, NumWorkers, and License Number properties as appropriate for your setup. Also edit the WORKERS section as appropriate for your setup.

2. Update getBatchConfigs.m
    * In MATLAB, navigate to the directory where you installed the toolbox (ex: `C:\Users\johndoe\Documents\MATLAB\Toolboxes\Batch MDCS Scheduler`) and open getBatchConfigs.m.
    * Fill in your Batch account information.
    * Fill in your Storage account information.

3. Execute the storeBatchCredentials function.
    * From the Azure portal, get the keys for your data Storage account and your Batch account.  You will be prompted for these keys by the storeBatchCredentials function.
    * The keys you supply will be stored in the Windows Credential Manager on your local machine. All future interactions with the Batch service will use the stored credentials.
    * If the keys to your Batch and Storage account are regenerated, simply rerun storeBatchCredentials and supply the new values.

4. Use the pool helper functions to manage pools in the Batch service. See each function's help text for more information.
    * batchCreatePool
    * batchListPools
    * batchResizePool
    * batchDeletePool

5. Choose which Batch pool to use with your cluster, and set the ClusterPoolId property in getBatchConfigs.m.

6. Run your MATLAB workflow against the Batch cluster profile. 

#### Known Issues:
* Creating an interactive session with parpool is not currently supported.
* The Start Task does not validate that the MATLAB installation was successful.

### Testing

To verify your Batch cluster setup, go to the **Home** tab of the MATLAB toolbar and select **Parallel > Manage Cluster Profiles**. Select your Batch Profile, and click the **Validate** button. MATLAB will run some test jobs.  

NOTE: The final job will fail because it attempts to create an interactive parpool session. This is not yet supported.  

NOTE: The validation suite includes 2 communicating jobs, so the Batch pool you use for validation must have the MaxTasksPerComputeNode property set to 1.