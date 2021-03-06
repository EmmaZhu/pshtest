﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using MS.Test.Common.MsTestLib;
using StorageTestLib;
using BlobType = Microsoft.WindowsAzure.Storage.Blob.BlobType;
using Management.Storage.ScenarioTest.Performance.Helper;
using Management.Storage.ScenarioTest.Util;
using Management.Storage.ScenarioTest.Common;

namespace Management.Storage.ScenarioTest
{
    [TestClass]
    public class CLIPerf_2G_N : CLIPerfBase
    {
        private const int MAX_SIZE_MB = 2048;
        private const int MAX_FILE_NUM = 4096;

        #region Additional test attributes
        // 
        //You can use the following additional attributes as you write your tests:
        //
        //Use ClassInitialize to run code before running the first test in the class
        [ClassInitialize()]
        public static void MyClassInitialize(TestContext testContext)
        {
            TestBase.TestClassInitialize(testContext);

            //delete perf files
            Helper.DeletePattern("testfile_*");

            BlobHelper = new CloudBlobHelper(StorageAccount);
            FileHelper = new CloudFileHelper(StorageAccount);

            FileName = Test.Data.Get("FileName");
            FolderName = Test.Data.Get("FolderName");

            // import module
            PowerShellAgent.ImportModules(Constants.ServiceModulePaths);

            //set the ConcurrentTaskCount field
            PowerShellAgent.ConcurrentTaskCount = Environment.ProcessorCount * 8;

            Trace.WriteLine("ClassInit");
        }

        //
        //Use ClassCleanup to run code after all tests in a class have run
        [ClassCleanup()]
        public static void MyClassCleanup()
        {
            Trace.WriteLine("ClasssCleanup");
            
            Helper.DeletePattern(FolderName + "_*");
        }

        //Use TestInitialize to run code before running each test
        [TestInitialize()]
        public void MyTestInitialize()
        {
            Trace.WriteLine("TestInit");
            Test.Start(TestContext.FullyQualifiedTestClassName, TestContext.TestName);
        }

        //Use TestCleanup to run code after each test has run
        [TestCleanup()]
        public void MyTestCleanup()
        {
            Helper.DeletePattern(FolderName + "_*");
            Trace.WriteLine("TestCleanup");
            Test.End(TestContext.FullyQualifiedTestClassName, TestContext.TestName);

        }
        #endregion

        [TestMethod]
        [TestCategory(PsTag.Perf)][Timeout(7200000)]
        public void UploadHttpBlock()
        {
            var o = new BlockBlobUploadOperation(new PowerShellAgent(), BlobHelper);
            Run(o);
        }

        [TestMethod]
        [TestCategory(PsTag.Perf)]
        public void DownloadHttpBlock()
        {
            var o = new BlockBlobDownloadOperation(new PowerShellAgent(), BlobHelper);
            var ro = new BlockBlobUploadOperation(new PowerShellAgent(), BlobHelper);
            Run(o, ro);
        }

        [TestMethod]
        [TestCategory(PsTag.Perf)]
        public void UploadHttpPage()
        {
            var o = new PageBlobUploadOperation(new PowerShellAgent(), BlobHelper);
            Run(o);
        }

        [TestMethod]
        [TestCategory(PsTag.Perf)]
        public void DownloadHttpPage()
        {
            var o = new PageBlobDownloadOperation(new PowerShellAgent(), BlobHelper);
            var ro = new PageBlobUploadOperation(new PowerShellAgent(), BlobHelper);
            Run(o, ro);
        }

        [TestMethod]
        [TestCategory(PsTag.Perf)]
        [TestCategory(PsTag.FilePerf)]
        [Timeout(7200000)]
        public void UploadFile()
        {
            var o = new FileUploadOperation(new PowerShellAgent(), FileHelper);
            Run(o);
        }

        [TestMethod]
        [TestCategory(PsTag.Perf)]
        [TestCategory(PsTag.FilePerf)]
        [Timeout(7200000)]
        public void DownloadFile()
        {
            var o = new FileDownloadOperation(new PowerShellAgent(), FileHelper);
            var ro = new FileUploadOperation(new PowerShellAgent(), FileHelper);
            Run(o, ro);
        }

        #region append blob
        [TestMethod]
        [TestCategory(PsTag.Perf)]
        [Timeout(7200000)]
        public void UploadHttpAppend()
        {
            var o = new AppendBlobUploadOperation(new PowerShellAgent(), BlobHelper);
            Run(o);
        }

        [TestMethod]
        [TestCategory(PsTag.Perf)]
        public void DownloadHttpAppend()
        {
            var o = new AppendBlobDownloadOperation(new PowerShellAgent(), BlobHelper);
            var ro = new AppendBlobUploadOperation(new PowerShellAgent(), BlobHelper);
            Run(o, ro);

        }

        #endregion

        public void Run(ICLIOperation operation, ICLIOperation reverseOperation = null)
        {
            var fileNumTime = new Dictionary<int, double>();
            var fileNumTimeSD = new Dictionary<int, double>();

            var fileNum = MAX_FILE_NUM; // 4096;       
            while (fileNum > 0)
            {
                var folder = FolderName + "-" + fileNum;
                var size = MAX_SIZE_MB * 1024 / fileNum;
                var containerName = DownloadContainerPrefix + "-" + folder;
                var localFolder = ".\\" + folder;

                if (operation.IsUploadTest)
                {
                    FileUtil.PrepareData(folder, fileNum, size);
                    containerName = UploadContainerPrefix + "-" + folder;
                }
                else if (this.GenerateDataBeforeDownload && reverseOperation != null)
                {
                    FileUtil.PrepareData(folder, fileNum, size);
                    reverseOperation.BeforeBatch(remote: containerName, local: localFolder);
                    reverseOperation.GoBatch(remote: containerName, local: localFolder);
                }

                TransferTestFiles(
                    remote: containerName,
                    local: localFolder,
                    fileNum: fileNum,
                    fileNumTime: fileNumTime,
                    fileNumTimeSD: fileNumTimeSD,
                    operation: operation);

                fileNum /= 4;
            }

            //print the results
            string sizes = string.Empty;
            string times = string.Empty;
            string sds = string.Empty;

            foreach (KeyValuePair<int, double> d in fileNumTime)
            {
                sizes += d.Key + " ";
                times += d.Value + " ";
            }
            foreach (KeyValuePair<int, double> d in fileNumTimeSD)
            {
                sds += d.Value + " ";
            }

            Test.Info("[file_number] {0}", sizes);
            Test.Info("[file_times] {0}", times);
            Test.Info("[file_timeSDs] {0}", sds);

            Helper.writePerfLog(TestContext.FullyQualifiedTestClassName + "." + TestContext.TestName);
            Helper.writePerfLog(sizes.Replace(' ', ','));
            Helper.writePerfLog(times.Replace(' ', ','));
            Helper.writePerfLog(sds.Replace(' ', ','));
        }

        public static void TransferTestFiles(string local, string remote, int fileNum,
            Dictionary<int, double> fileNumTime, Dictionary<int, double> fileNumTimeSD, ICLIOperation operation)
        {
            List<long> fileTimeList = new List<long>();
            Stopwatch sw = new Stopwatch();

            for (int j = 0; j < Constants.Iterations; j++)
            {
                operation.BeforeBatch(local, remote);

                sw.Reset(); sw.Start();

                Test.Assert(operation.GoBatch(local: local, remote: remote), operation.Name + " should succeed");

                sw.Stop();
                fileTimeList.Add(sw.ElapsedMilliseconds);
                Test.Info("file number : {0} round : {1} time(ms) : {2}", fileNum, j + 1, sw.ElapsedMilliseconds);

                var error = string.Empty;
                Test.Assert(operation.ValidateBatch(local, remote, fileNum, out error), error);
            }

            double average = fileTimeList.Average();
            var deviation = fileTimeList.Select(num => Math.Pow(num - average, 2));
            double sd = Math.Sqrt(deviation.Average());
            fileNumTime.Add(fileNum, average);
            fileNumTimeSD.Add(fileNum, sd);

            Test.Info("file number : {0} average time : {1}", fileNum, average);
            Test.Info("file number : {0} standard dev : {1}", fileNum, sd);
        }
        
        public static CloudBlobHelper BlobHelper;
        public static CloudFileHelper FileHelper;
        public static string FileName;
        public static string FolderName;
    }
}
