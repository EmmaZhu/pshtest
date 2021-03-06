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

using Management.Storage.ScenarioTest.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.WindowsAzure.Storage.Blob;
using MS.Test.Common.MsTestLib;
using StorageTestLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.WindowsAzure.Storage.File;
using StorageFile = Microsoft.WindowsAzure.Storage.File;
using System.Threading;

namespace Management.Storage.ScenarioTest.Functional.Blob
{
    [TestClass]
    public class StopCopy : TestBase
    {
        [ClassInitialize()]
        public static void ClassInit(TestContext testContext)
        {
            TestBase.TestClassInitialize(testContext);
        }

        [ClassCleanup()]
        public static void SetBlobContentClassCleanup()
        {
            TestBase.TestClassCleanup();
        }

        /// <summary>
        /// Stop copy in root container
        /// 8.22	Stop-AzureStorageBlobCopy
        ///     1.	Stop the copy task on the blob in root container
        /// </summary>
        [TestMethod()]
        [TestCategory(Tag.Function)]
        [TestCategory(PsTag.Blob)]
        [TestCategory(PsTag.StopCopyBlob)]
        [TestCategory(CLITag.StopCopyBlob)]
        [TestCategory(CLITag.NodeJSFT)]
        public void StopCopyInRootContainerTest()
        {
            CloudBlobContainer rootContainer = blobUtil.CreateContainer("$root");
            string srcBlobName = Utility.GenNameString("src");
            //We could only use block blob to copy from external uri
            CloudBlob srcBlob = blobUtil.CreateBlockBlob(rootContainer, srcBlobName);
            string copyId = CopyBigFileToBlob(srcBlob);
            AssertStopPendingCopyOperationTest(srcBlob, lang == Language.NodeJS ? copyId : "*");
        }

        /// <summary>
        /// Stop copy using blob pipeline
        /// 8.22	Stop-AzureStorageBlobCopy
        ///   2.	Stop a list of copying blobs
        /// </summary>
        [TestMethod()]
        [TestCategory(Tag.Function)]
        [TestCategory(PsTag.Blob)]
        [TestCategory(PsTag.StopCopyBlob)]
        public void StopCopyUsingBlobPipelineTest()
        {
            CloudBlobContainer container = blobUtil.CreateContainer();
            int count = random.Next(1, 5);
            List<string> blobNames = new List<string>();
            List<CloudBlob> blobs = new List<CloudBlob>();

            for (int i = 0; i < count; i++)
            {
                //We could only use block blob to copy from external uri
                blobs.Add(blobUtil.CreateBlockBlob(container, Utility.GenNameString("blob")));
            }

            try
            {
                foreach (CloudBlob blob in blobs)
                {
                    CopyBigFileToBlob(blob);
                }

                ((PowerShellAgent)CommandAgent).AddPipelineScript(String.Format("Get-AzureStorageBlob -Container {0}", container.Name));
                string copyId = "*";
                bool force = true;
                Test.Assert(CommandAgent.StopAzureStorageBlobCopy(string.Empty, string.Empty, copyId, force), "Stop multiple copy operations using blob pipeline should be successful");
                int expectedOutputCount = blobs.Count;
                Test.Assert(CommandAgent.Output.Count == expectedOutputCount, String.Format("Should return {0} message, and actually it's {1}", expectedOutputCount, CommandAgent.Output.Count));

                for (int i = 0; i < expectedOutputCount; i++)
                {
                    blobs[i].FetchAttributes();
                    Test.Assert(blobs[i].CopyState.Status == CopyStatus.Aborted, String.Format("The copy status should be aborted, actually it's {0}", blobs[i].CopyState.Status));
                }
            }
            finally
            {
                blobUtil.RemoveContainer(container.Name);
            }
        }

        /// <summary>
        /// Unit test for invalid blob or container name.
        /// 8.20	Stop-AzureStorageBlobCopy Negative Functional Cases
        ///     1.	Stop the copy task on invalid container name and blob name
        /// </summary>
        [TestMethod()]
        [TestCategory(Tag.Function)]
        [TestCategory(PsTag.Blob)]
        [TestCategory(PsTag.StopCopyBlob)]
        [TestCategory(CLITag.StopCopyBlob)]
        [TestCategory(CLITag.NodeJSFT)]
        public void StopCopyWithInvalidNameTest()
        {
            string invalidContainerName = "Invalid";
            int maxBlobNameLength = 1024;
            string invalidBlobName = new string('a', maxBlobNameLength + 1);
            string copyId = "*";
            string invalidContainerErrorMessage;
            string invalidBlobErrorMessage;
            if (lang == Language.PowerShell)
            {
                invalidContainerErrorMessage = String.Format("Container name '{0}' is invalid.", invalidContainerName);
                invalidBlobErrorMessage = String.Format("Blob name '{0}' is invalid.", invalidBlobName);
            }
            else
            {
                invalidContainerErrorMessage = "Container name format is incorrect";
                invalidBlobErrorMessage = "Value for one of the query parameters specified in the request URI is invalid";
            }
            Test.Assert(!CommandAgent.StopAzureStorageBlobCopy(invalidContainerName, Utility.GenNameString("blob"), copyId, false), "Stop copy should failed with invalid container name");
            ExpectedContainErrorMessage(invalidContainerErrorMessage);
            Test.Assert(!CommandAgent.StopAzureStorageBlobCopy(Utility.GenNameString("container"), invalidBlobName, copyId, false), "Start copy should failed with invalid blob name");
            ExpectedContainErrorMessage(invalidBlobErrorMessage);
        }

        /// <summary>
        /// Stop the copy task on a not existing container and blob
        /// 8.22	Stop-CopyAzureStorageBlob Negative Functional Cases
        ///    2. Stop the copy task on a not existing container and blob
        /// </summary>
        [TestMethod()]
        [TestCategory(Tag.Function)]
        [TestCategory(PsTag.Blob)]
        [TestCategory(PsTag.StopCopyBlob)]
        [TestCategory(CLITag.StopCopyBlob)]
        [TestCategory(CLITag.NodeJSFT)]
        public void StopCopyWithNotExistsContainerAndBlobTest()
        {
            string srcContainerName = Utility.GenNameString("copy");
            string blobName = Utility.GenNameString("blob");
            string copyId = Guid.NewGuid().ToString();
            string errorMessage = string.Empty;
            Validator validator;
            if (lang == Language.PowerShell)
            {
                errorMessage = string.Format("Can not find blob '{0}' in container '{1}', or the blob type is unsupported.", blobName, srcContainerName);
                validator = ExpectedContainErrorMessage;
            }
            else
            {
                errorMessage = "The specified container does not exist";
                validator = ExpectedStartsWithErrorMessage;
            }

            Test.Assert(!CommandAgent.StopAzureStorageBlobCopy(srcContainerName, blobName, copyId, false), "Stop copy should failed with not existing src container");
            validator(errorMessage);

            try
            {
                CloudBlobContainer srcContainer = blobUtil.CreateContainer(srcContainerName);
                Test.Assert(!CommandAgent.StopAzureStorageBlobCopy(srcContainerName, blobName, copyId, false), "Stop copy should failed with not existing src container");
                if (lang == Language.PowerShell)
                {
                    errorMessage = string.Format("Can not find blob '{0}' in container '{1}', or the blob type is unsupported.", blobName, srcContainerName);
                }
                else
                {
                    errorMessage = "The specified blob does not exist";
                }
                validator(errorMessage);
            }
            finally
            {
                blobUtil.RemoveContainer(srcContainerName);
            }
        }

        /// <summary>
        /// Stop the copy task on a not existing container and blob
        /// 8.22	Stop-CopyAzureStorageBlob Negative Functional Cases
        ///    2. Stop the copy task on a not existing container and blob
        /// </summary>
        [TestMethod()]
        [TestCategory(Tag.Function)]
        [TestCategory(PsTag.Blob)]
        [TestCategory(PsTag.StopCopyBlob)]
        public void StopFinishedCopyFromFileTest()
        {
            string srcShareName = Utility.GenNameString("share");
            CloudFileShare srcShare = fileUtil.EnsureFileShareExists(srcShareName);

            string destContainerName = Utility.GenNameString("container");
            CloudBlobContainer destContainer = blobUtil.CreateContainer(destContainerName);

            try
            {
                string fileName = Utility.GenNameString("fileName");
                StorageFile.CloudFile file = fileUtil.CreateFile(srcShare.GetRootDirectoryReference(), fileName);

                CloudBlockBlob blob = destContainer.GetBlockBlobReference(Utility.GenNameString("destBlobName"));

                Test.Assert(CommandAgent.StartAzureStorageBlobCopy(file, destContainer.Name, blob.Name, PowerShellAgent.Context), "Start azure storage copy from file to blob should succeed.");

                while (true)
                {
                    blob.FetchAttributes();

                    if (blob.CopyState.Status != CopyStatus.Pending)
                    {
                        break;
                    }

                    Thread.Sleep(2000);
                }

                Test.Assert(!CommandAgent.StopAzureStorageBlobCopy(destContainerName, blob.Name, null, true), "Stop blob copy should fail");

                ExpectedContainErrorMessage("There is currently no pending copy operation.");
            }
            finally
            {
                fileUtil.DeleteFileShareIfExists(srcShareName);
                blobUtil.RemoveContainer(destContainerName);
            }
        }

        private void AssertStopPendingCopyOperationTest(CloudBlob blob, string copyId = "*")
        {
            Test.Assert(blob.CopyState.Status == CopyStatus.Pending, String.Format("The copy status should be pending, actually it's {0}", blob.CopyState.Status));
            bool force = true;
            Test.Assert(CommandAgent.StopAzureStorageBlobCopy(blob.Container.Name, blob.Name, copyId, force), "Stop copy operation should be successed");
            blob.FetchAttributes();
            Test.Assert(blob.CopyState.Status == CopyStatus.Aborted, String.Format("The copy status should be aborted, actually it's {0}", blob.CopyState.Status));
            int expectedOutputCount = lang == Language.PowerShell ? 1 : 0;
            Test.Assert(CommandAgent.Output.Count == expectedOutputCount, String.Format("Should return {0} message, and actually it's {1}", expectedOutputCount, CommandAgent.Output.Count));
        }

        private string CopyBigFileToBlob(CloudBlob blob)
        {
            string uri = Test.Data.Get("BigBlobUri");
            Test.Assert(!String.IsNullOrEmpty(uri), string.Format("Big file uri should be not empty, actually it's {0}", uri));
            
            if (String.IsNullOrEmpty(uri))
            {
                return string.Empty;
            }
            
            Test.Info(String.Format("Copy Big file to blob '{0}'", blob.Name));
            blob.StartCopy(new Uri(uri));
            Test.Assert(blob.CopyState.Status == CopyStatus.Pending, String.Format("The copy status should be pending, actually it's {0}", blob.CopyState.Status));

            return blob.CopyState.CopyId;
        }
    }
}
