﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    [TestClass]
    public class CosmosItemChangeFeedTests : BaseCosmosClientHelper
    {
        private ContainerCore Container = null;
        private CosmosJsonSerializerCore jsonSerializer = null;

        [TestInitialize]
        public async Task TestInitialize()
        {
            await base.TestInit();
            string PartitionKey = "/status";
            ContainerResponse response = await this.database.CreateContainerAsync(
                new ContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPath: PartitionKey),
                cancellationToken: this.cancellationToken);
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Container);
            Assert.IsNotNull(response.Resource);
            this.Container = (ContainerCore)response;
            this.jsonSerializer = new CosmosJsonSerializerCore();
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await base.TestCleanup();
        }

        /// <summary>
        /// Test to verify that StartFromBeginning works as expected by inserting 25 items, reading them all, then taking the last continuationtoken, 
        /// inserting another 25, and verifying that the iterator continues from the saved token and reads the second 25 for a total of 50 documents.
        /// </summary>
        [TestMethod]
        public async Task StandByFeedIterator()
        {
            int totalCount = 0;
            string lastcontinuation = string.Empty;
            int firstRunTotal = 25;
            int batchSize = 25;
            Documents.Routing.Range<string> previousRange = null;
            Documents.Routing.Range<string> currentRange = null;

            int pkRangesCount = (await this.Container.ClientContext.DocumentClient.ReadPartitionKeyRangeFeedAsync(this.Container.LinkUri)).Count;
            int visitedPkRanges = 0;

            await this.CreateRandomItems(batchSize, randomPartitionKey: true);
            ContainerCore itemsCore = (ContainerCore)this.Container;
            FeedIterator feedIterator = itemsCore.GetStandByFeedIterator(requestOptions: new ChangeFeedRequestOptions() { StartTime = DateTime.MinValue });

            while (feedIterator.HasMoreResults)
            {
                using (ResponseMessage responseMessage =
                    await feedIterator.ReadNextAsync(this.cancellationToken))
                {
                    lastcontinuation = responseMessage.Headers.Continuation;
                    List<CompositeContinuationToken> deserializedToken = JsonConvert.DeserializeObject<List<CompositeContinuationToken>>(lastcontinuation);
                    currentRange = deserializedToken[0].Range;
                    Assert.AreEqual(pkRangesCount, deserializedToken.Count);
                    if (responseMessage.IsSuccessStatusCode)
                    {
                        Collection<ToDoActivity> response = new CosmosJsonSerializerCore().FromStream<CosmosFeedResponseUtil<ToDoActivity>>(responseMessage.Content).Data;
                        totalCount += response.Count;
                    }

                    if (!currentRange.Equals(previousRange))
                    {
                        visitedPkRanges++;
                    }

                    if (visitedPkRanges == pkRangesCount && responseMessage.StatusCode == System.Net.HttpStatusCode.NotModified)
                    {
                        break;
                    }

                    previousRange = currentRange;
                }

            }
            Assert.AreEqual(firstRunTotal, totalCount);

            int expectedFinalCount = 50;
            previousRange = null;
            currentRange = null;
            visitedPkRanges = 0;

            // Insert another batch of 25 and use the last continuation token from the first cycle
            await this.CreateRandomItems(batchSize, randomPartitionKey: true);
            FeedIterator setIteratorNew =
                itemsCore.GetStandByFeedIterator(lastcontinuation);

            while (setIteratorNew.HasMoreResults)
            {
                using (ResponseMessage responseMessage =
                    await setIteratorNew.ReadNextAsync(this.cancellationToken))
                {
                    lastcontinuation = responseMessage.Headers.Continuation;
                    currentRange = JsonConvert.DeserializeObject<List<CompositeContinuationToken>>(lastcontinuation)[0].Range;

                    if (responseMessage.IsSuccessStatusCode)
                    {
                        Collection<ToDoActivity> response = new CosmosJsonSerializerCore().FromStream<CosmosFeedResponseUtil<ToDoActivity>>(responseMessage.Content).Data;
                        totalCount += response.Count;
                    }

                    if (!currentRange.Equals(previousRange))
                    {
                        visitedPkRanges++;
                    }

                    if (visitedPkRanges == pkRangesCount && responseMessage.StatusCode == System.Net.HttpStatusCode.NotModified)
                    {
                        break;
                    }

                    previousRange = currentRange;
                }

            }

            Assert.AreEqual(expectedFinalCount, totalCount);
        }

        /// <summary>
        /// Test that verifies that, if the token contains an invalid range, we throw.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public async Task StandByFeedIterator_WithInexistentRange()
        {
            // Add some random range, this will force the failure
            List<CompositeContinuationToken> corruptedTokens = new List<CompositeContinuationToken>
            {
                new CompositeContinuationToken()
                {
                    Range = new Documents.Routing.Range<string>("whatever", "random", true, false),
                    Token = "oops"
                }
            };

            string corruptedTokenSerialized = JsonConvert.SerializeObject(corruptedTokens);

            ContainerCore itemsCore = (ContainerCore)this.Container;
            FeedIterator setIteratorNew =
                itemsCore.GetStandByFeedIterator(corruptedTokenSerialized);

            ResponseMessage responseMessage =
                    await setIteratorNew.ReadNextAsync(this.cancellationToken);

            Assert.Fail("Should have thrown.");
        }

        /// <summary>
        /// Test that verifies that MaxItemCount is honored by checking the count of documents in the responses.
        /// </summary>
        [TestMethod]
        public async Task StandByFeedIterator_WithMaxItemCount()
        {
            await this.CreateRandomItems(2, randomPartitionKey: true);
            ContainerCore itemsCore = (ContainerCore)this.Container;
            FeedIterator feedIterator = itemsCore.GetStandByFeedIterator(maxItemCount: 1, requestOptions: new ChangeFeedRequestOptions() { StartTime = DateTime.MinValue });

            while (feedIterator.HasMoreResults)
            {
                using (ResponseMessage responseMessage =
                    await feedIterator.ReadNextAsync(this.cancellationToken))
                {
                    if (responseMessage.IsSuccessStatusCode)
                    {
                        Collection<ToDoActivity> response = new CosmosJsonSerializerCore().FromStream<CosmosFeedResponseUtil<ToDoActivity>>(responseMessage.Content).Data;
                        if (response.Count > 0)
                        {
                            Assert.AreEqual(1, response.Count);
                            return;
                        }
                    }
                }

            }

            Assert.Fail("Found no batch with size 1");
        }

        /// <summary>
        /// Test that does not use FetchNextSetAsync but creates new iterators passing along the previous one's continuationtoken.
        /// </summary>
        [TestMethod]
        public async Task StandByFeedIterator_NoFetchNext()
        {
            var pkRanges = await this.Container.ClientContext.DocumentClient.ReadPartitionKeyRangeFeedAsync(this.Container.LinkUri);

            int expected = 25;
            int iterations = 0;
            await this.CreateRandomItems(expected, randomPartitionKey: true);
            ContainerCore itemsCore = (ContainerCore)this.Container;
            string continuationToken = null;
            int count = 0;
            while (true)
            {
                ChangeFeedRequestOptions requestOptions = new ChangeFeedRequestOptions() { StartTime = DateTime.MinValue };

                FeedIterator feedIterator = itemsCore.GetStandByFeedIterator(continuationToken, requestOptions: requestOptions);
                using (ResponseMessage responseMessage =
                    await feedIterator.ReadNextAsync(this.cancellationToken))
                {
                    continuationToken = responseMessage.Headers.Continuation;
                    if (responseMessage.IsSuccessStatusCode)
                    {
                        Collection<ToDoActivity> response = new CosmosJsonSerializerCore().FromStream<CosmosFeedResponseUtil<ToDoActivity>>(responseMessage.Content).Data;
                        count += response.Count;
                    }
                }

                if (count > expected)
                {
                    Assert.Fail($"{count} does not equal {expected}");
                }

                if (count.Equals(expected))
                {
                    break;
                }

                if (iterations++ > pkRanges.Count)
                {
                    Assert.Fail("Feed does not contain all elements even after looping through PK ranges. Either the continuation is not moving forward or there is some state problem.");

                }
            }
        }

        /// <summary>
        /// Verifies that the internal delegate for PKRangeCache gets called with forceRefresh true after a split.
        /// </summary>
        [TestMethod]
        public async Task StandByFeedIterator_VerifyRefreshIsCalledOnSplit()
        {
            CosmosChangeFeedResultSetIteratorCoreMock iterator = new CosmosChangeFeedResultSetIteratorCoreMock(this.Container, "", 100, new ChangeFeedRequestOptions());
            using (ResponseMessage responseMessage =
                    await iterator.ReadNextAsync(this.cancellationToken))
            {
                Assert.IsTrue(iterator.HasCalledForceRefresh);
                Assert.IsTrue(iterator.Iteration > 1);
                Assert.AreEqual(responseMessage.StatusCode, System.Net.HttpStatusCode.NotModified);
            }
        }

        private async Task<IList<ToDoActivity>> CreateRandomItems(int pkCount, int perPKItemCount = 1, bool randomPartitionKey = true)
        {
            Assert.IsFalse(!randomPartitionKey && perPKItemCount > 1);

            List<ToDoActivity> createdList = new List<ToDoActivity>();
            for (int i = 0; i < pkCount; i++)
            {
                string pk = "TBD";
                if (randomPartitionKey)
                {
                    pk += Guid.NewGuid().ToString();
                }

                for (int j = 0; j < perPKItemCount; j++)
                {
                    ToDoActivity temp = this.CreateRandomToDoActivity(pk);

                    createdList.Add(temp);

                    await this.Container.CreateItemAsync<ToDoActivity>(item: temp);
                }
            }

            return createdList;
        }

        private ToDoActivity CreateRandomToDoActivity(string pk = null)
        {
            if (string.IsNullOrEmpty(pk))
            {
                pk = "TBD" + Guid.NewGuid().ToString();
            }

            return new ToDoActivity()
            {
                id = Guid.NewGuid().ToString(),
                description = "CreateRandomToDoActivity",
                status = pk,
                taskNum = 42,
                cost = double.MaxValue
            };
        }

        private class CosmosChangeFeedResultSetIteratorCoreMock : ChangeFeedResultSetIteratorCore
        {
            public int Iteration = 0;
            public bool HasCalledForceRefresh = false;

            internal CosmosChangeFeedResultSetIteratorCoreMock(
                ContainerCore container,
                string continuationToken,
                int? maxItemCount,
                ChangeFeedRequestOptions options) : base(
                    clientContext: container.ClientContext,
                    container: container,
                    continuationToken: continuationToken,
                    maxItemCount: maxItemCount,
                    options: options)
            {
                List<CompositeContinuationToken> compositeContinuationTokens = new List<CompositeContinuationToken>()
                {
                    new CompositeContinuationToken()
                    {
                        Token = null,
                        Range = new Documents.Routing.Range<string>("A", "B", true, false)
                    }
                };

                string serialized = JsonConvert.SerializeObject(compositeContinuationTokens);

                this.compositeContinuationToken = StandByFeedContinuationToken.CreateAsync("containerRid", serialized, (string containerRid, Documents.Routing.Range<string> ranges, bool forceRefresh) =>
                {
                    IReadOnlyList<Documents.PartitionKeyRange> filteredRanges = new List<Documents.PartitionKeyRange>()
                    {
                        new Documents.PartitionKeyRange() { MinInclusive = "A", MaxExclusive ="B", Id = "0" }
                    };

                    if (forceRefresh)
                    {
                        this.HasCalledForceRefresh = true;
                    }

                    return Task.FromResult(filteredRanges);
                }).Result;
            }

            internal override Task<ResponseMessage> NextResultSetDelegateAsync(
                string continuationToken,
                string partitionKeyRangeId,
                int? maxItemCount,
                ChangeFeedRequestOptions options,
                CancellationToken cancellationToken)
            {
                if (this.Iteration++ == 0)
                {
                    ResponseMessage httpResponse = new ResponseMessage(System.Net.HttpStatusCode.Gone);
                    httpResponse.Headers.Add(Documents.WFConstants.BackendHeaders.SubStatus, ((uint)Documents.SubStatusCodes.PartitionKeyRangeGone).ToString(CultureInfo.InvariantCulture));

                    return Task.FromResult(httpResponse);
                }

                return Task.FromResult(new ResponseMessage(System.Net.HttpStatusCode.NotModified));
            }
        }


        public class ToDoActivity
        {
            public string id { get; set; }
            public int taskNum { get; set; }
            public double cost { get; set; }
            public string description { get; set; }
            public string status { get; set; }
        }
    }
}