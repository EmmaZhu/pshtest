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

namespace Management.Storage.ScenarioTest.BVT.HTTP
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using HTTPSAnonymousBVT = Management.Storage.ScenarioTest.BVT.HTTPS.AnonymousBVT;

    /// <summary>
    /// bvt cases for anonymous storage account
    /// </summary>
    [TestClass]
    public class AnonymousBVT : HTTPSAnonymousBVT
    {
        [ClassInitialize()]
        public static void AnonymousHTTPBVTClassInitialize(TestContext testContext)
        {
            useHttps = false;
            HTTPSAnonymousBVT.Initialize(testContext, useHttps);
        }

        [ClassCleanup()]
        public static void AnonymousHTTPBVTClassCleanup()
        {
            HTTPSAnonymousBVT.AnonymousBVTClassCleanup();
        }
    }
}
