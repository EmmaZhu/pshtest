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
using System.Linq;
using Management.Storage.ScenarioTest.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.WindowsAzure.Storage.Blob;
using MS.Test.Common.MsTestLib;
using StorageTestLib;
using System.Reflection;
using BlobType = Microsoft.WindowsAzure.Storage.Blob.BlobType;

namespace Management.Storage.ScenarioTest.Functional.Blob
{
    /// <summary>
    /// functional test for Remove-AzureStorageBlob
    /// </summary>
    [TestClass]
    public class RemoveBlob : TestBase
    {
        [ClassInitialize()]
        public static void ClassInit(TestContext testContext)
        {
            TestBase.TestClassInitialize(testContext);
        }

        [ClassCleanup()]
        public static void RemoveBlobClassCleanup()
        {
            TestBase.TestClassCleanup();
        }

        /// <summary>
        /// remove blob by pipeline 
        /// 8.13 Remove-AzureStorageBlob Positive Functional Cases
        ///     3. Remove a list of existing blobs by using pipeline
        /// </summary>
        [TestMethod()]
        [TestCategory(Tag.Function)]
        [TestCategory(PsTag.Blob)]
        [TestCategory(PsTag.RemoveBlob)]
        public void RemoveBlobByPipeline()
        {
            //TODO add more pipeline
            string containerName = Utility.GenNameString("container");
            string blobName = Utility.GenNameString("blob");
            CloudBlobContainer container = blobUtil.CreateContainer(containerName);

            try
            {
                int count = random.Next(1, 5);
                List<string> blobNames = new List<string>();
                for (int i = 0; i < count; i++)
                {
                    blobNames.Add(Utility.GenNameString("blob"));
                }

                List<CloudBlob> blobs = blobUtil.CreateRandomBlob(container, blobNames);

                string cmd = String.Format("{0} {1}", "Get-AzureStorageContainer", containerName);
                ((PowerShellAgent)CommandAgent).AddPipelineScript(cmd);
                cmd = "Get-AzureStorageBlob";
                ((PowerShellAgent)CommandAgent).AddPipelineScript(cmd);

                List<IListBlobItem> blobLists = container.ListBlobs().ToList();
                Test.Assert(blobLists.Count == blobs.Count, string.Format("container {0} should contain {1} blobs", containerName, blobs.Count));

                Test.Assert(CommandAgent.RemoveAzureStorageBlob(string.Empty, string.Empty), Utility.GenComparisonData("Get-AzureStorageContainer | Get-AzureStorageBlob | Remove-AzureStorageBlob", true));

                blobLists = container.ListBlobs().ToList();
                Test.Assert(blobLists.Count == 0, string.Format("container {0} should contain {1} blobs", containerName, 0));
            }
            finally
            {
                blobUtil.RemoveContainer(containerName);
            }
        }

        /// <summary>
        /// remove blob by pipeline
        /// 8.13 Remove-AzureStorageBlob Positive Functional Cases
        ///     4.	Remove an existing blob which has a sub directory
        /// </summary>
        [TestMethod()]
        [TestCategory(Tag.Function)]
        [TestCategory(PsTag.Blob)]
        [TestCategory(PsTag.RemoveBlob)]
        [TestCategory(CLITag.NodeJSFT)]
        [TestCategory(CLITag.RemoveBlob)]
        public void RemoveBlobInSubDirectory()
        {
            string containerName = Utility.GenNameString("container");
            string blobName = Utility.GenNameString("blob");
            string subBlobName = Utility.GenNameString(string.Format("{0}/", blobName));
            string subsubBlobName = Utility.GenNameString(string.Format("{0}/", subBlobName));

            List<string> blobNames = new List<string>
            {
                blobName, subsubBlobName
            };

            CloudBlobContainer container = blobUtil.CreateContainer(containerName);

            try
            {
                List<CloudBlob> blobs = blobUtil.CreateRandomBlob(container, blobNames);
                CloudBlob subBlob = blobUtil.CreateRandomBlob(container, subBlobName);

                List<IListBlobItem> blobLists = container.ListBlobs(string.Empty, true).ToList();
                Test.Assert(blobLists.Count == 3, string.Format("container {0} should contain {1} blobs", containerName, 3));

                Test.Assert(CommandAgent.RemoveAzureStorageBlob(subBlobName, containerName), Utility.GenComparisonData("Remove-AzureStorageBlob in subdirectory", true));
                blobLists = container.ListBlobs(string.Empty, true).ToList();
                Test.Assert(blobLists.Count == 2, string.Format("container {0} should contain {1} blobs", containerName, 2));
                bool blobFound = false, subsubBlobFound = false;
                foreach (CloudBlob blob in blobLists)
                {
                    if (blob.Name == blobName)
                    {
                        blobFound = true;
                    }
                    else if (blob.Name == subsubBlobName)
                    {
                        subsubBlobFound = true;
                    }
                }

                Test.Assert(blobFound == true, "the root blob should be contained in container");
                Test.Assert(subsubBlobFound == true, "the blob in sub sub directory should be contained in container");
            }
            finally
            {
                blobUtil.RemoveContainer(containerName);
            }
        }

        /// <summary>
        /// remove blob by pipeline
        /// 8.13 Remove-AzureStorageBlob Positive Functional Cases
        ///     5.	Remove an existing base blob that has snapthots 
        /// </summary>
        [TestMethod()]
        [TestCategory(Tag.Function)]
        [TestCategory(PsTag.Blob)]
        [TestCategory(PsTag.RemoveBlob)]
        [TestCategory(CLITag.NodeJSFT)]
        [TestCategory(CLITag.RemoveBlob)]
        [TestCategory(CLITag.BlobSnapshot)]
        public void RemoveBlobIncludeSnapshot()
        {
            string containerName = Utility.GenNameString("container");
            string blobName = Utility.GenNameString("blob");
            CloudBlobContainer container = blobUtil.CreateContainer(containerName);

            try
            {
                CloudBlob blob = blobUtil.CreateRandomBlob(container, blobName);
                List<CloudBlob> blobs = new List<CloudBlob>();
                blob.FetchAttributes();

                int count = random.Next(1, 5);
                for (int i = 0; i < count; i++)
                {
                    CloudBlob snapshot = blob.Snapshot();
                    snapshot.FetchAttributes();
                    blobs.Add(snapshot);
                }

                blobs.Add(blob);

                List<IListBlobItem> blobLists = container.ListBlobs(string.Empty, true, BlobListingDetails.All).ToList();
                Test.Assert(blobLists.Count == blobs.Count, string.Format("container {0} should contain {1} blobs, actually it contain {2} blobs", containerName, blobs.Count, blobLists.Count));
                Test.Assert(CommandAgent.RemoveAzureStorageBlob(blobName, containerName, string.Empty, onlySnapshot: false), Utility.GenComparisonData("Remove-AzureStorageBlob and snapshot", true));
                blobLists = container.ListBlobs(string.Empty, true, BlobListingDetails.All).ToList();
                Test.Assert(blobLists.Count == 0, string.Format("container {0} should contain {1} blobs", containerName, 0));
            }
            finally
            {
                blobUtil.RemoveContainer(containerName);
            }
        }

        /// <summary>
        /// remove blob by pipeline
        /// 8.13 Remove-AzureStorageBlob Positive Functional Cases
        ///     6.	Remove the snapthot only with DeleteSnap = Yes
        /// </summary>
        [TestMethod()]
        [TestCategory(Tag.Function)]
        [TestCategory(PsTag.Blob)]
        [TestCategory(PsTag.RemoveBlob)]
        [TestCategory(CLITag.NodeJSFT)]
        [TestCategory(CLITag.RemoveBlob)]
        [TestCategory(CLITag.BlobSnapshot)]
        public void RemoveSnapshot()
        {
            string containerName = Utility.GenNameString("container");
            string blobName = Utility.GenNameString("blob");
            CloudBlobContainer container = blobUtil.CreateContainer(containerName);

            try
            {
                CloudBlob blob = blobUtil.CreateRandomBlob(container, blobName);
                List<CloudBlob> blobs = new List<CloudBlob>();
                blob.FetchAttributes();

                int count = random.Next(1, 5);
                for (int i = 0; i < count; i++)
                {
                    CloudBlob snapshot = blob.Snapshot();
                    snapshot.FetchAttributes();
                    blobs.Add(snapshot);
                }

                blobs.Add(blob);
                List<IListBlobItem> blobLists = container.ListBlobs(string.Empty, true, BlobListingDetails.All).ToList();
                Test.Assert(blobLists.Count == blobs.Count, string.Format("container {0} should contain {1} blobs, and actually it contain {2} blobs", containerName, blobs.Count, blobLists.Count));
                Test.Assert(CommandAgent.RemoveAzureStorageBlob(blobName, containerName, onlySnapshot: true), Utility.GenComparisonData("Remove-AzureStorageBlob and snapshot", true));
                blobLists = container.ListBlobs(string.Empty, true, BlobListingDetails.All).ToList();
                Test.Assert(blobLists.Count == 1, string.Format("container {0} should contain {1} blobs", containerName, 1));
                CloudBlob remainBlob = blobLists[0] as CloudBlob;
                Test.Assert(blob.Name == remainBlob.Name, string.Format("Blob name should be {0}, and actually it's {1}", blob.Name, remainBlob.Name));
                Test.Assert(null == remainBlob.SnapshotTime, "snapshot time should be null");
            }
            finally
            {
                blobUtil.RemoveContainer(containerName);
            }
        }

        /// <summary>
        /// Delete a specified snapshot with �snapshot and snapshot id
        /// 8.13 Remove-AzureStorageBlob Positive Functional Cases
        ///     10. Delete a specified snapshot with �snapshot and snapshot id
        /// </summary>
        [TestMethod()]
        [TestCategory(CLITag.NodeJSFT)]
        [TestCategory(CLITag.RemoveBlob)]
        [TestCategory(CLITag.BlobSnapshot)]
        public void RemoveSpecificBlobSnapshot()
        {
            string containerName = Utility.GenNameString("container");
            string blobName = Utility.GenNameString("blob");
            CloudBlobContainer container = blobUtil.CreateContainer(containerName);

            try
            {
                CloudBlob blob = blobUtil.CreateRandomBlob(container, blobName);
                List<CloudBlob> blobs = new List<CloudBlob>();
                blob.FetchAttributes();

                string snapshotId = string.Empty;
                int count = random.Next(1, 5);
                for (int i = 0; i < count; i++)
                {
                    CloudBlob snapshot = blob.Snapshot();
                    snapshot.FetchAttributes();
                    blobs.Add(snapshot);
                    if (string.IsNullOrEmpty(snapshotId))
                    {
                        snapshotId = snapshot.SnapshotTime.Value.UtcDateTime.ToString("O");
                    }
                }

                blobs.Add(blob);
                List<IListBlobItem> blobLists = container.ListBlobs(string.Empty, true, BlobListingDetails.All).ToList();
                Test.Assert(blobLists.Count == blobs.Count, string.Format("container {0} should contain {1} blobs, and actually it contain {2} blobs", containerName, blobs.Count, blobLists.Count));
                Test.Assert(CommandAgent.RemoveAzureStorageBlob(blobName, containerName, snapshotId), Utility.GenComparisonData("Remove-AzureStorageBlob and snapshot", true));
                blobLists = container.ListBlobs(string.Empty, true, BlobListingDetails.All).ToList();
                Test.Assert(blobLists.Count == blobs.Count - 1, string.Format("container {0} should contain {1} blobs", containerName, blobs.Count - 1));
            }
            finally
            {
                blobUtil.RemoveContainer(containerName);
            }
        }

        /// <summary>
        /// remove an blob with lease
        /// 8.13 Remove-AzureStorageBlob Negative Functional Cases
        ///     4.	Remove an existing blob with lease
        /// </summary>
        [TestMethod()]
        [TestCategory(Tag.Function)]
        [TestCategory(PsTag.Blob)]
        [TestCategory(PsTag.RemoveBlob)]
        [TestCategory(CLITag.NodeJSFT)]
        [TestCategory(CLITag.RemoveBlob)]
        public void RemoveBlobWithLease()
        {
            string containerName = Utility.GenNameString("container");
            string blobName = Utility.GenNameString("blob");
            CloudBlobContainer container = blobUtil.CreateContainer(containerName);

            try
            {
                CloudBlob blob = blobUtil.CreateRandomBlob(container, blobName);
                blob.AcquireLease(null, string.Empty);

                List<IListBlobItem> blobLists = container.ListBlobs(string.Empty, true, BlobListingDetails.All).ToList();
                Test.Assert(blobLists.Count == 1, string.Format("container {0} should contain {1} blobs, and actually it contain {2} blobs", containerName, 1, blobLists.Count));
                Test.Assert(!CommandAgent.RemoveAzureStorageBlob(blobName, containerName), Utility.GenComparisonData("Remove-AzureStorageBlob with lease", false));

                CommandAgent.ValidateErrorMessage(MethodBase.GetCurrentMethod().Name);

                blobLists = container.ListBlobs(string.Empty, true, BlobListingDetails.All).ToList();
                Test.Assert(blobLists.Count == 1, string.Format("container {0} should contain {1} blobs, and actually it contain {2} blobs", containerName, 1, blobLists.Count));
                CloudBlob remainBlob = blobLists[0] as CloudBlob;
                Test.Assert(blob.Name == remainBlob.Name, string.Format("Blob name should be {0}, and actually it's {1}", blob.Name, remainBlob.Name));
                Test.Assert(remainBlob.Properties.LeaseStatus == LeaseStatus.Locked, "blob should be locked by lease");
            }
            finally
            {
                blobUtil.RemoveContainer(containerName);
            }
        }

        /// <summary>
        /// 8.9	Remove Blob 
        ///     8.	Remove a blob name with special chars
        /// </summary>
        [TestMethod()]
        [TestCategory(Tag.Function)]
        [TestCategory(PsTag.Blob)]
        [TestCategory(PsTag.SetBlobContent)]
        [TestCategory(CLITag.NodeJSFT)]
        [TestCategory(CLITag.RemoveBlob)]
        public void RemoveBlobWithSpeicialChars()
        {
            RemoveBlobWithSpeicialChars(BlobType.BlockBlob);
            RemoveBlobWithSpeicialChars(BlobType.PageBlob);
            RemoveBlobWithSpeicialChars(BlobType.AppendBlob);
        }

        public void RemoveBlobWithSpeicialChars(BlobType blobType)
        {
            CloudBlobContainer container = blobUtil.CreateContainer();
            string blobName = SpecialChars;
            CloudBlob blob = blobUtil.CreateBlob(container, blobName, blobType);

            try
            {
                Test.Assert(CommandAgent.RemoveAzureStorageBlob(blobName, container.Name), "remove blob name with special chars should succeed");
                Test.Assert(!blob.Exists(), string.Format("the specified blob '{0}' should not exist", blobName));
            }
            finally
            {
                blobUtil.RemoveContainer(container.Name);
            }
        }

        /// <summary>
        /// Remove an existing blob that has snapthots without DeleteSnap = No
        /// 8.13 Remove-AzureStorageBlob Negative Functional Cases
        ///     3.	Remove an existing blob that has snapthots without DeleteSnap = No
        /// </summary>
        [TestMethod()]
        [TestCategory(Tag.Function)]
        [TestCategory(PsTag.Blob)]
        [TestCategory(PsTag.RemoveBlob)]
        public void RemoveBlobWithSnapshotNeedComfirmation()
        {
            CloudBlobContainer container = blobUtil.CreateContainer();
            string blobName = Utility.GenNameString("blob");
            CloudBlob blob = blobUtil.CreateRandomBlob(container, blobName);
            CloudBlob snapshot = blobUtil.SnapShot(blob);

            try
            {
                Test.Assert(!CommandAgent.RemoveAzureStorageBlob(blobName, container.Name, onlySnapshot: false, force: false), "remove a blob with snapshot should throw a confirmation exception");
                ExpectedContainErrorMessage(ConfirmExceptionMessage);
                Test.Assert(blob.Exists(), string.Format("the specified blob '{0}' should exist", blob.Name));
                Test.Assert(snapshot.Exists(), "the snapshot should exist");
                Test.Assert(snapshot.SnapshotTime != null, "the snapshout time should be not null");
            }
            finally
            {
                blobUtil.RemoveContainer(container.Name);
            }
        }

        /// <summary>
        /// Delete a specified snapshot with �snapshot and a non-existing snapshot id
        /// 8.13 Remove-AzureStorageBlob Negative Functional Cases
        ///     7. Delete a specified snapshot with �snapshot and a non-existing snapshot id
        /// </summary>
        [TestMethod()]
        [TestCategory(CLITag.NodeJSFT)]
        [TestCategory(CLITag.RemoveBlob)]
        [TestCategory(CLITag.BlobSnapshot)]
        public void RemoveNonExistingBlobSnapshot()
        {
            string containerName = Utility.GenNameString("container");
            string blobName = Utility.GenNameString("blob");
            CloudBlobContainer container = blobUtil.CreateContainer(containerName);

            try
            {
                CloudBlob blob = blobUtil.CreateRandomBlob(container, blobName);
                CloudBlob snapshot = blob.Snapshot();
                string fakeSnapshotId = DateTime.UtcNow.ToString("O");
                string realSnapshotId = snapshot.SnapshotTime.Value.UtcDateTime.ToString("O");

                Test.Assert(realSnapshotId != fakeSnapshotId, string.Format("the snapshot should be {0} while passing {1}", realSnapshotId, fakeSnapshotId));
                Test.Assert(!CommandAgent.RemoveAzureStorageBlob(blobName, containerName, fakeSnapshotId), Utility.GenComparisonData("Remove-AzureStorageBlob and snapshot", false));
                CommandAgent.ValidateErrorMessage(MethodBase.GetCurrentMethod().Name, fakeSnapshotId, blobName, containerName);
            }
            finally
            {
                blobUtil.RemoveContainer(containerName);
            }
        }

        /// <summary>
        /// Delete a specified snapshot with both �snapshot and --delete-snapshot
        /// 8.13 Remove-AzureStorageBlob Negative Functional Cases
        ///     8. Delete a specified snapshot with both �snapshot and --delete-snapshot
        /// </summary>
        [TestMethod()]
        [TestCategory(CLITag.NodeJSFT)]
        [TestCategory(CLITag.RemoveBlob)]
        [TestCategory(CLITag.BlobSnapshot)]
        public void RemoveBlobSnapshotWithInvalidOption()
        {
            string containerName = Utility.GenNameString("container");
            string blobName = Utility.GenNameString("blob");
            CloudBlobContainer container = blobUtil.CreateContainer(containerName);

            try
            {
                CloudBlob blob = blobUtil.CreateRandomBlob(container, blobName);
                CloudBlob snapshot = blob.Snapshot();
                string snapshotId = snapshot.SnapshotTime.Value.UtcDateTime.ToString("O");

                Test.Assert(!CommandAgent.RemoveAzureStorageBlob(blobName, containerName, snapshotId, onlySnapshot: true), Utility.GenComparisonData("Remove-AzureStorageBlob and snapshot", false));
                CommandAgent.ValidateErrorMessage(MethodBase.GetCurrentMethod().Name, snapshotId, blobName, containerName);
            }
            finally
            {
                blobUtil.RemoveContainer(containerName);
            }
        }
    }
}
