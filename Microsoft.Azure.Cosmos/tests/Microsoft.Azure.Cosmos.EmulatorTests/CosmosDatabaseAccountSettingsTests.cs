﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.SDK.EmulatorTests;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json.Linq;

    [TestClass]
    public class CosmosDatabaseAccountSettingsTests
    {
        private CosmosClient cosmosClient = null;

        [TestInitialize]
        public void TestInit()
        {
            this.cosmosClient = TestCommon.CreateCosmosClient();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            if (this.cosmosClient != null)
            {
                this.cosmosClient.Dispose();
            }
        }

        [TestMethod]
        public async Task GetCosmosDatabaseAccountSettings()
        {
            AccountProperties accountProperties = await this.cosmosClient.GetAccountPropertiesAsync();
            Assert.IsNotNull(accountProperties);
            Assert.IsNotNull(accountProperties.Id);
            Assert.IsNotNull(accountProperties.ReadableLocations);
            Assert.IsTrue(accountProperties.ReadableLocations.Count() > 0);
            Assert.IsNotNull(accountProperties.WritableLocations);
            Assert.IsTrue(accountProperties.WritableLocations.Count() > 0);
        }
    }
}
