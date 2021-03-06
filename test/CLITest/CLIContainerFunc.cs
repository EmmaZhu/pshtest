// ----------------------------------------------------------------------------------
//
// Copyright Microsoft Corporation
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ----------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Threading;
using Management.Storage.ScenarioTest.Common;
using Management.Storage.ScenarioTest.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.WindowsAzure.Storage.Blob;
using MS.Test.Common.MsTestLib;
using StorageTestLib;

namespace Management.Storage.ScenarioTest
{
    /// <summary>
    /// this class contains all the functional test cases for PowerShell container cmdlets
    /// </summary>
    [TestClass]
    public class CLIContainerFunc : TestBase
    {
        private static CloudBlobHelper BlobHelper;

        #region Additional test attributes

        [ClassInitialize()]
        public static void CLIBlobFuncClassInit(TestContext testContext)
        {
            TestBase.TestClassInitialize(testContext);

            //init the blob helper for blob related operations
            BlobHelper = new CloudBlobHelper(StorageAccount);
        }

        [ClassCleanup()]
        public static void CLIBlobFuncClassCleanup()
        {
            TestBase.TestClassCleanup();
        }

        #endregion

        [TestMethod]
        [TestCategory(Tag.Function)]
        [TestCategory(CLITag.NodeJSFT)]
        public void CreateInvalidContainer()
        {
            CreateInvalidContainer(CommandAgent);
        }

        [TestMethod]
        [TestCategory(Tag.Function)]
        [TestCategory(CLITag.NodeJSFT)]
        public void CreateExistingContainer()
        {
            CreateExistingContainer(CommandAgent);
        }

        [TestMethod]
        [TestCategory(Tag.Function)]
        [TestCategory(CLITag.NodeJSFT)]
        public void RootContainerOperations()
        {
            RootContainerOperations(CommandAgent);
        }

        [TestMethod]
        [TestCategory(Tag.Function)]
        [TestCategory(CLITag.NodeJSFT)]
        public void ContainerListOperations()
        {
            ContainerListOperations(CommandAgent);
        }

        [TestMethod]
        [TestCategory(Tag.Function)]
        public void GetNonExistingContainer()
        {
            GetNonExistingContainer(CommandAgent);
        }

        [TestMethod]
        [TestCategory(Tag.Function)]
        [TestCategory(CLITag.NodeJSFT)]
        public void EnumerateAllContainers()
        {
            EnumerateAllContainers(CommandAgent);
        }

        [TestMethod]
        [TestCategory(Tag.Function)]
        [TestCategory(CLITag.NodeJSFT)]
        public void RemoveNonExistingContainer()
        {
            RemoveNonExistingContainer(CommandAgent);
        }

        [TestMethod]
        [TestCategory(Tag.Function)]
        public void RemoveContainerWithoutForce()
        {
            RemoveContainerWithoutForce(CommandAgent);
        }

        /// <summary>
        /// Positive Functional Cases : get non existing container (only for nodejs)
        /// 1. Get a non-existing blob container list (Positive 6)
        /// </summary>
        [TestMethod]
        [TestCategory(CLITag.NodeJSFT)]
        public void GetNonExistingContainerNodeJS()
        {
            string ContainerName = Utility.GenNameString("nonexisting");

            // Delete the container if it exists
            CloudBlobClient blobClient = StorageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = blobClient.GetContainerReference(ContainerName);
            container.DeleteIfExists();

            //--------------Get operation--------------
            Test.Assert(CommandAgent.GetAzureStorageContainer(ContainerName), Utility.GenComparisonData("get container", true));
            // Verification for returned values
            ExpectEqual(CommandAgent.Output.Count, 0, string.Format("0 row should be returned : {0}", CommandAgent.Output.Count));
            Test.Assert(CommandAgent.ErrorMessages.Count == 0, "0 error message returned : {0}", CommandAgent.ErrorMessages.Count);
        }

        /// <summary>
        /// Functional Cases : for New-AzureStorageContainer
        /// 1. Create a list of new blob containers (Positive 2)
        /// 2. Create a list of containers that some of them already exist (Negative 4)
        /// 
        /// Functional Cases : for Get-AzureStorageContainer
        /// 2. Get a list of blob containers by using wildcards in the name (Positive 2)
        /// 3. Get a list of blob containers by using Prefix (Positive 3)
        /// 
        /// Functional Cases : for Remove-AzureStorageContainer
        /// 4.	Remove a list of existing blob containers by using pipeline (Positive 6)
        /// </summary>
        internal void ContainerListOperations(Agent agent)
        {
            string PREFIX = Utility.GenNameString("uniqueprefix-") + "-";
            string[] CONTAINER_NAMES = new string[] { Utility.GenNameString(PREFIX), Utility.GenNameString(PREFIX), Utility.GenNameString(PREFIX) };

            // PART_EXISTING_NAMES differs only the last element with CONTAINER_NAMES
            string[] PARTLY_EXISTING_NAMES = new string[CONTAINER_NAMES.Length];
            Array.Copy(CONTAINER_NAMES, PARTLY_EXISTING_NAMES, CONTAINER_NAMES.Length - 1);
            PARTLY_EXISTING_NAMES[CONTAINER_NAMES.Length - 1] = Utility.GenNameString(PREFIX);

            string[] MERGED_NAMES = CONTAINER_NAMES.Union(PARTLY_EXISTING_NAMES).ToArray();
            Array.Sort(MERGED_NAMES);

            // Generate the comparison data
            Collection<Dictionary<string, object>> comp = new Collection<Dictionary<string, object>>();
            foreach (string name in MERGED_NAMES)
                comp.Add(Utility.GenComparisonData(StorageObjectType.Container, name));

            CloudBlobClient blobClient = StorageAccount.CreateCloudBlobClient();

            // Check if all the above containers have been removed
            foreach (string name in MERGED_NAMES)
            {
                CloudBlobContainer container = blobClient.GetContainerReference(name);
                container.DeleteIfExists();
            }

            //--------------1. New operation--------------
            Test.Assert(agent.NewAzureStorageContainer(CONTAINER_NAMES), Utility.GenComparisonData("NewAzureStorageContainer", true));
            // Verification for returned values
            Test.Assert(agent.Output.Count == CONTAINER_NAMES.Count(), "3 row returned : {0}", agent.Output.Count);

            // Check if all the above containers have been created
            foreach (string name in CONTAINER_NAMES)
            {
                CloudBlobContainer container = blobClient.GetContainerReference(name);
                Test.Assert(container.Exists(), "container {0} should exist", name);
            }

            try
            {
                //--------------2. New operation--------------
                Test.Assert(!agent.NewAzureStorageContainer(CONTAINER_NAMES), Utility.GenComparisonData("NewAzureStorageContainer", false));
                // Verification for returned values
                Test.Assert(agent.Output.Count == 0, "0 row returned : {0}", agent.Output.Count);
                int i = 0;
                foreach (string name in CONTAINER_NAMES)
                {
                    string errorMsg = String.Format("Container '{0}' already exists", name);
                    Test.Assert(agent.ErrorMessages[i].Contains(errorMsg), "Expect error message {0}, actually it's: {1}", errorMsg, agent.ErrorMessages[i]);
                    ++i;
                }

                //--------------3. New operation--------------
                Test.Assert(!agent.NewAzureStorageContainer(PARTLY_EXISTING_NAMES), Utility.GenComparisonData("NewAzureStorageContainer", false));
                // Verification for returned values
                Test.Assert(agent.Output.Count == 1, "1 row returned : {0}", agent.Output.Count);

                // Check if all the above containers have been created
                foreach (string name in CONTAINER_NAMES)
                {
                    CloudBlobContainer container = blobClient.GetContainerReference(name);
                    Test.Assert(container.Exists(), "container {0} should exist", name);
                }


                //--------------4. Get operation--------------
                // use wildcards
                Test.Assert(agent.GetAzureStorageContainer("*" + PREFIX + "*"), Utility.GenComparisonData("GetAzureStorageContainer", true));
                // Verification for returned values
                agent.OutputValidation(StorageAccount.CreateCloudBlobClient().ListContainers(PREFIX, ContainerListingDetails.All));

                // use Prefix parameter
                Test.Assert(agent.GetAzureStorageContainerByPrefix(PREFIX), Utility.GenComparisonData("GetAzureStorageContainerByPrefix", true));
                // Verification for returned values
                agent.OutputValidation(StorageAccount.CreateCloudBlobClient().ListContainers(PREFIX, ContainerListingDetails.All));
            }
            finally { }

            //--------------5. Remove operation--------------
            Test.Assert(agent.RemoveAzureStorageContainer(MERGED_NAMES), Utility.GenComparisonData("RemoveAzureStorageContainer", true));
            // Check if all the above containers have been removed
            foreach (string name in CONTAINER_NAMES)
            {
                CloudBlobContainer container = blobClient.GetContainerReference(name);
                Test.Assert(!container.Exists(), "container {0} should not exist", name);
            }
        }

        /// <summary>
        /// Functional Cases:
        /// 1. Create the root container  (New-AzureStorageContainer Positive 4)
        /// 2. Get the root container (Get-AzureStorageContainer Positive 4)
        /// 3. Remove the root container  (Remove-AzureStorageContainer Positive 4)
        /// </summary>
        internal void RootContainerOperations(Agent agent)
        {
            const string ROOT_CONTAINER_NAME = "$root";
            Dictionary<string, object> dic = Utility.GenComparisonData(StorageObjectType.Container, ROOT_CONTAINER_NAME);
            Collection<Dictionary<string, object>> comp = new Collection<Dictionary<string, object>> { dic };

            // delete container if it not exists
            CloudBlobContainer container = StorageAccount.CreateCloudBlobClient().GetRootContainerReference();
            bool bExists = container.Exists();
            if (bExists)
            {
                container.Delete();
            }

            try
            {
                //--------------New operation--------------
                bool created = false;
                int retryCount = 0;
                int maxRetryCount = 60; //retry ten minutes

                do
                {
                    if (retryCount > 0)
                    {
                        int sleepInterval = 10 * 1000;
                        Test.Info("Sleep and wait for retry...");
                        Thread.Sleep(sleepInterval);
                        Test.Info(string.Format("{0}th retry to create the $root container", retryCount));
                    }

                    bool successed = agent.NewAzureStorageContainer(ROOT_CONTAINER_NAME);

                    if (successed)
                    {
                        Test.Info("Create $root container successfully");
                        created = true;
                    }
                    else
                    {
                        if (agent.ErrorMessages.Count == 0)
                        {
                            Test.AssertFail("Can not create $root container and can't get any error messages");
                            break;
                        }
                        else if (agent.ErrorMessages[0].Contains("The remote server returned an error: (409) Conflict."))
                        {
                            retryCount++;
                        }
                        else if (agent.ErrorMessages[0].Contains("The specified container is being deleted."))
                        {
                            retryCount++;
                        }
                        else
                        {
                            Test.AssertFail(string.Format("Can not create $root container. Exception: {0}", agent.ErrorMessages[0]));
                            break;
                        }
                    }
                }
                while (!created && retryCount < maxRetryCount);

                if (!created && retryCount == maxRetryCount)
                { 
                    Test.AssertFail(string.Format("Can not create $root container after {0} times retry", retryCount));
                }

                container.FetchAttributes();
                CloudBlobUtil.PackContainerCompareData(container, dic);
                // Verification for returned values
                agent.OutputValidation(comp);
                Test.Assert(container.Exists(), "container {0} should exist!", ROOT_CONTAINER_NAME);

                //--------------Get operation--------------
                Test.Assert(agent.GetAzureStorageContainer(ROOT_CONTAINER_NAME), Utility.GenComparisonData("GetAzureStorageContainer", true));
                // Verification for returned values
                agent.OutputValidation(comp);

                //--------------Remove operation--------------
                Test.Assert(agent.RemoveAzureStorageContainer(ROOT_CONTAINER_NAME), Utility.GenComparisonData("RemoveAzureStorageContainer", true));
                Test.Assert(!container.Exists(), "container {0} should not exist!", ROOT_CONTAINER_NAME);
            }
            finally
            {
                // Recover the environment
                try
                {
                    if (bExists)
                    {
                        Test.Info("Sleep for 150 seconds to wait for removing the root container");
                        System.Threading.Thread.Sleep(150000);
                        //The following statement often throw an 409 conflict exception
                        container.Create();
                    }
                }
                catch
                { }
            }
        }

        /// <summary>
        /// Negative Functional Cases : for New-AzureStorageContainer 
        /// 1. Create a blob container that already exists (Negative 3)
        /// </summary>
        internal void CreateExistingContainer(Agent agent)
        {
            string CONTAINER_NAME = Utility.GenNameString("existing");

            // create container if not exists
            CloudBlobContainer container = StorageAccount.CreateCloudBlobClient().GetContainerReference(CONTAINER_NAME);
            container.CreateIfNotExists();

            try
            {
                //--------------New operation--------------
                Test.Assert(!agent.NewAzureStorageContainer(CONTAINER_NAME), Utility.GenComparisonData("NewAzureStorageContainer", false));
                // Verification for returned values
                Test.Assert(agent.Output.Count == 0, "Only 0 row returned : {0}", agent.Output.Count);
                agent.ValidateErrorMessage(MethodBase.GetCurrentMethod().Name, CONTAINER_NAME);
            }
            finally
            {
                // Recover the environment
                container.DeleteIfExists();
            }
        }

        /// <summary>
        /// Negative Functional Cases : for New-AzureStorageContainer 
        /// 1. Create a new blob containter with an invalid blob container name (Negative 1)
        /// </summary>
        internal void CreateInvalidContainer(Agent agent)
        {
            string containerName = Utility.GenNameString("abc_");

            //--------------New operation--------------
            Test.Assert(!agent.NewAzureStorageContainer(containerName), Utility.GenComparisonData("NewAzureStorageContainer", false));
            // Verification for returned values
            Test.Assert(agent.Output.Count == 0, "Only 0 row returned : {0}", agent.Output.Count);
            agent.ValidateErrorMessage(MethodBase.GetCurrentMethod().Name, containerName);
        }


        /// <summary>
        /// Negative Functional Cases : for Get-AzureStorageContainer 
        /// 1. Get a non-existing blob container (Negative 1)
        /// </summary>
        internal void GetNonExistingContainer(Agent agent)
        {
            string CONTAINER_NAME = Utility.GenNameString("nonexisting");

            // Delete the container if it exists
            CloudBlobClient blobClient = StorageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = blobClient.GetContainerReference(CONTAINER_NAME);
            container.DeleteIfExists();

            //--------------Get operation--------------
            Test.Assert(!agent.GetAzureStorageContainer(CONTAINER_NAME), Utility.GenComparisonData("GetAzureStorageContainer", false));
            // Verification for returned values
            Test.Assert(agent.Output.Count == 0, "Only 0 row returned : {0}", agent.Output.Count);
            Test.Assert(agent.ErrorMessages[0].Contains(String.Format("Can not find the container '{0}'.", CONTAINER_NAME)), agent.ErrorMessages[0]);
        }

        /// <summary>
        /// Functional Cases : for Get-AzureStorageContainer 
        /// 1. Validate that all the containers can be enumerated (Positive 5)
        /// </summary>
        internal void EnumerateAllContainers(Agent agent)
        {
            //--------------Get operation--------------
            Test.Assert(agent.GetAzureStorageContainer(""), Utility.GenComparisonData("GetAzureStorageContainer", true));

            // Verification for returned values
            agent.OutputValidation(StorageAccount.CreateCloudBlobClient().ListContainers());
        }

        /// <summary>
        /// Negative Functional Cases : for Remove-AzureStorageContainer 
        /// 1. Remove a non-existing blob container (Negative 2)
        /// </summary>
        internal void RemoveNonExistingContainer(Agent agent)
        {
            string CONTAINER_NAME = Utility.GenNameString("nonexisting");

            // Delete the container if it exists
            CloudBlobClient blobClient = StorageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = blobClient.GetContainerReference(CONTAINER_NAME);
            container.DeleteIfExists();

            //--------------Remove operation--------------
            Test.Assert(!agent.RemoveAzureStorageContainer(CONTAINER_NAME), Utility.GenComparisonData("RemoveAzureStorageContainer", false));
            // Verification for returned values
            Test.Assert(agent.Output.Count == 0, "Only 0 row returned : {0}", agent.Output.Count);
            agent.ValidateErrorMessage(MethodBase.GetCurrentMethod().Name, CONTAINER_NAME);
        }

        /// <summary>
        /// Negative Functional Cases : for Remove-AzureStorageContainer 
        /// 1. Remove the blob container with blobs in it without by force (Negative 3)
        /// </summary>
        internal void RemoveContainerWithoutForce(Agent agent)
        {
            string CONTAINER_NAME = Utility.GenNameString("withoutforce-");

            // create container if not exists
            CloudBlobClient blobClient = StorageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = blobClient.GetContainerReference(CONTAINER_NAME);
            container.CreateIfNotExists();
            blobUtil.CreateRandomBlob(container);

            try
            {
                //--------------Remove operation--------------
                Test.Assert(!agent.RemoveAzureStorageContainer(CONTAINER_NAME, Force: false), Utility.GenComparisonData("RemoveAzureStorageContainer", false));
                // Verification for returned values
                Test.Assert(agent.Output.Count == 0, "Only 0 row returned : {0}", agent.Output.Count);
                Test.Assert(agent.ErrorMessages[0].Contains("A command that prompts the user failed because"), agent.ErrorMessages[0]);
            }
            finally
            {
                // Recover the environment
                container.DeleteIfExists();
            }
        }
    }
}
