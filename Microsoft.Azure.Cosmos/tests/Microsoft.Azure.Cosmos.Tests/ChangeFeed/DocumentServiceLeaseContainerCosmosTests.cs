﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;
    using Microsoft.Azure.Cosmos.Client.Core.Tests;
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    [TestCategory("ChangeFeed")]
    public class DocumentServiceLeaseContainerCosmosTests
    {
        private static DocumentServiceLeaseStoreManagerOptions leaseStoreManagerSettings = new DocumentServiceLeaseStoreManagerOptions()
        {
            ContainerNamePrefix = "prefix",
            HostName = "host"
        };

        private static List<DocumentServiceLeaseCore> allLeases = new List<DocumentServiceLeaseCore>()
        {
            new DocumentServiceLeaseCore()
            {
                LeaseId = "1",
                Owner = "someone"
            },
            new DocumentServiceLeaseCore()
            {
                LeaseId = "2",
                Owner = "host"
            }
        };

        [TestMethod]
        public async Task GetAllLeasesAsync_ReturnsAllLeaseDocuments()
        {
            DocumentServiceLeaseContainerCosmos documentServiceLeaseContainerCosmos = new DocumentServiceLeaseContainerCosmos(
                DocumentServiceLeaseContainerCosmosTests.GetMockedContainer(),
                DocumentServiceLeaseContainerCosmosTests.leaseStoreManagerSettings);

            IEnumerable<DocumentServiceLease> readLeases = await documentServiceLeaseContainerCosmos.GetAllLeasesAsync();
            CollectionAssert.AreEqual(DocumentServiceLeaseContainerCosmosTests.allLeases, readLeases.ToList());
        }

        [TestMethod]
        public async Task GetOwnedLeasesAsync_ReturnsOnlyMatched()
        {
            DocumentServiceLeaseContainerCosmos documentServiceLeaseContainerCosmos = new DocumentServiceLeaseContainerCosmos(
                DocumentServiceLeaseContainerCosmosTests.GetMockedContainer(),
                DocumentServiceLeaseContainerCosmosTests.leaseStoreManagerSettings);

            IEnumerable<DocumentServiceLease> readLeases = await documentServiceLeaseContainerCosmos.GetOwnedLeasesAsync();
            CollectionAssert.AreEqual(DocumentServiceLeaseContainerCosmosTests.allLeases.Where(l => l.Owner == DocumentServiceLeaseContainerCosmosTests.leaseStoreManagerSettings.HostName).ToList(), readLeases.ToList());
        }

        private static Container GetMockedContainer(string containerName = "myColl")
        {
            Headers headers = new Headers();
            headers.Continuation = string.Empty;

            Mock<FeedIterator<DocumentServiceLeaseCore>> mockedQuery = new Mock<FeedIterator<DocumentServiceLeaseCore>>();
            mockedQuery.Setup(q => q.ReadNextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => ReadFeedResponse<DocumentServiceLeaseCore>.CreateResponse(
                    responseMessageHeaders: headers,
                    resources: DocumentServiceLeaseContainerCosmosTests.allLeases,
                    hasMoreResults: false));
            mockedQuery.SetupSequence(q => q.HasMoreResults)
                .Returns(true)
                .Returns(false);

            Mock<Container> mockedItems = new Mock<Container>();
            mockedItems.Setup(i => i.GetItemQueryIterator<DocumentServiceLeaseCore>(
                // To make sure the SQL Query gets correctly created
                It.Is<string>(value => ("SELECT * FROM c WHERE STARTSWITH(c.id, '" + DocumentServiceLeaseContainerCosmosTests.leaseStoreManagerSettings.GetPartitionLeasePrefix() + "')").Equals(value)), 
                It.IsAny<string>(), 
                It.IsAny<QueryRequestOptions>()))
                .Returns(()=>
                {
                    return mockedQuery.Object;
                });

            return mockedItems.Object;
        }

        private static CosmosClient GetMockedClient()
        {
            DocumentClient documentClient = new MockDocumentClient();

            CosmosClientBuilder cosmosClientBuilder = new CosmosClientBuilder("http://localhost", Guid.NewGuid().ToString());

            return cosmosClientBuilder.Build(documentClient);
        }
    }
}
