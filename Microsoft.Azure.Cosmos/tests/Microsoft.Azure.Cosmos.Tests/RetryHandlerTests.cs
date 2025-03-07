﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Client.Core.Tests;
    using Microsoft.Azure.Cosmos.Handlers;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class RetryHandlerTests
    {
        private static readonly Uri TestUri = new Uri("https://dummy.documents.azure.com:443/dbs");

        [TestMethod]
        public async Task RetryHandlerDoesNotRetryOnSuccess()
        {
            CosmosClient client = MockCosmosUtil.CreateMockCosmosClient();

            RetryHandler retryHandler = new RetryHandler(client.DocumentClient.ResetSessionTokenRetryPolicy);
            int handlerCalls = 0;
            int expectedHandlerCalls = 1;
            TestHandler testHandler = new TestHandler((request, cancellationToken) => {
                handlerCalls++;
                return TestHandler.ReturnSuccess();
            });

            retryHandler.InnerHandler = testHandler;
            RequestInvokerHandler invoker = new RequestInvokerHandler(client);
            invoker.InnerHandler = retryHandler;
            RequestMessage requestMessage = new RequestMessage(HttpMethod.Delete, RetryHandlerTests.TestUri);
            requestMessage.Headers.Add(HttpConstants.HttpHeaders.PartitionKey, "[]");
            requestMessage.ResourceType = ResourceType.Document;
            requestMessage.OperationType = OperationType.Read;
            await invoker.SendAsync(requestMessage, new CancellationToken());
            Assert.AreEqual(expectedHandlerCalls, handlerCalls);
        }

        [TestMethod]
        public async Task RetryHandlerRetriesOn429()
        {
            CosmosClient client = MockCosmosUtil.CreateMockCosmosClient();

            RetryHandler retryHandler = new RetryHandler(client.DocumentClient.ResetSessionTokenRetryPolicy);
            int handlerCalls = 0;
            int expectedHandlerCalls = 2;
            TestHandler testHandler = new TestHandler((request, cancellationToken) => {
                if (handlerCalls == 0)
                {
                    handlerCalls++;
                    return TestHandler.ReturnStatusCode((HttpStatusCode)StatusCodes.TooManyRequests);
                }

                handlerCalls++;
                return TestHandler.ReturnSuccess();
            });

            retryHandler.InnerHandler = testHandler;
            RequestInvokerHandler invoker = new RequestInvokerHandler(client);
            invoker.InnerHandler = retryHandler;
            RequestMessage requestMessage = new RequestMessage(HttpMethod.Delete, RetryHandlerTests.TestUri);
            requestMessage.Headers.Add(HttpConstants.HttpHeaders.PartitionKey, "[]");
            requestMessage.ResourceType = ResourceType.Document;
            requestMessage.OperationType =OperationType.Read;
            await invoker.SendAsync(requestMessage, new CancellationToken());
            Assert.AreEqual(expectedHandlerCalls, handlerCalls);
        }

        [TestMethod]
        [ExpectedException(typeof(Exception))]
        public async Task RetryHandlerDoesNotRetryOnException()
        {
            CosmosClient client = MockCosmosUtil.CreateMockCosmosClient();

            RetryHandler retryHandler = new RetryHandler(client.DocumentClient.ResetSessionTokenRetryPolicy);
            int handlerCalls = 0;
            int expectedHandlerCalls = 2;
            TestHandler testHandler = new TestHandler((request, cancellationToken) => {
                handlerCalls++;
                if (handlerCalls == expectedHandlerCalls)
                {
                    Assert.Fail("Should not retry on exception.");
                }

                throw new Exception("You shall not retry.");
            });

            retryHandler.InnerHandler = testHandler;
            RequestInvokerHandler invoker = new RequestInvokerHandler(client);
            invoker.InnerHandler = retryHandler;
            RequestMessage requestMessage = new RequestMessage(HttpMethod.Get, new System.Uri("https://dummy.documents.azure.com:443/dbs"));
            requestMessage.Headers.Add(HttpConstants.HttpHeaders.PartitionKey, "[]");
            requestMessage.ResourceType = ResourceType.Document;
            requestMessage.OperationType = OperationType.Read;
            await invoker.SendAsync(requestMessage, new CancellationToken());
        }

        [TestMethod]
        public async Task RetryHandlerHttpClientExceptionRefreshesLocations()
        {
            DocumentClient dc = new MockDocumentClient(RetryHandlerTests.TestUri, "test");
            CosmosClient client = new CosmosClient(
                RetryHandlerTests.TestUri.OriginalString, 
                Guid.NewGuid().ToString(), 
                new CosmosClientOptions(), 
                dc);

            Mock<IDocumentClientRetryPolicy> mockClientRetryPolicy = new Mock<IDocumentClientRetryPolicy>();

            mockClientRetryPolicy.Setup(m => m.ShouldRetryAsync(It.IsAny<Exception>(), It.IsAny<CancellationToken>()))
                .Returns<Exception, CancellationToken>((ex, tooken) => Task.FromResult(ShouldRetryResult.RetryAfter(TimeSpan.FromMilliseconds(1))));

            Mock<IRetryPolicyFactory> mockRetryPolicy = new Mock<IRetryPolicyFactory>();
            mockRetryPolicy.Setup(m => m.GetRequestPolicy())
                .Returns(() => mockClientRetryPolicy.Object);

            RetryHandler retryHandler = new RetryHandler(mockRetryPolicy.Object);
            int handlerCalls = 0;
            int expectedHandlerCalls = 2;
            TestHandler testHandler = new TestHandler((request, response) => {
                handlerCalls++;
                if (handlerCalls == expectedHandlerCalls)
                {
                    return TestHandler.ReturnSuccess();
                }

                throw new HttpRequestException("DNS or some other network issue");
            });

            retryHandler.InnerHandler = testHandler;
            RequestInvokerHandler invoker = new RequestInvokerHandler(client);
            invoker.InnerHandler = retryHandler;
            RequestMessage requestMessage = new RequestMessage(HttpMethod.Get, new System.Uri("https://dummy.documents.azure.com:443/dbs"));
            requestMessage.Headers.Add(HttpConstants.HttpHeaders.PartitionKey, "[]");
            requestMessage.ResourceType = ResourceType.Document;
            requestMessage.OperationType = OperationType.Read;
            await invoker.SendAsync(requestMessage, new CancellationToken());
            Assert.AreEqual(expectedHandlerCalls, handlerCalls);
        }

        [TestMethod]
        public async Task RetryHandlerNoRetryOnAuthError()
        {
            await this.RetryHandlerDontRetryOnStatusCode(HttpStatusCode.Unauthorized);
        }

        [TestMethod]
        public async Task RetryHandlerNoRetryOnWriteForbidden()
        {
            await this.RetryHandlerDontRetryOnStatusCode(HttpStatusCode.Forbidden, SubStatusCodes.WriteForbidden);
        }

        [TestMethod]
        public async Task RetryHandlerNoRetryOnSessionNotAvailable()
        {
            await this.RetryHandlerDontRetryOnStatusCode(HttpStatusCode.NotFound, SubStatusCodes.ReadSessionNotAvailable);
        }

        [TestMethod]
        public async Task RetryHandlerNoRetryOnDatabaseAccountNotFound()
        {
            await this.RetryHandlerDontRetryOnStatusCode(HttpStatusCode.Forbidden, SubStatusCodes.DatabaseAccountNotFound);
        }

        private async Task RetryHandlerDontRetryOnStatusCode(
                HttpStatusCode statusCode,
                SubStatusCodes subStatusCode = SubStatusCodes.Unknown)
        {
            int handlerCalls = 0;
            TestHandler testHandler = new TestHandler((request, response) => {
                handlerCalls++;

                if (handlerCalls == 0)
                {
                    return TestHandler.ReturnStatusCode(statusCode, subStatusCode);
                }

                return TestHandler.ReturnSuccess();
            });

            CosmosClient client = MockCosmosUtil.CreateMockCosmosClient();
            RetryHandler retryHandler = new RetryHandler(client.DocumentClient.ResetSessionTokenRetryPolicy);
            retryHandler.InnerHandler = testHandler;

            RequestInvokerHandler invoker = new RequestInvokerHandler(client);
            invoker.InnerHandler = retryHandler;
            RequestMessage requestMessage = new RequestMessage(HttpMethod.Delete, RetryHandlerTests.TestUri);
            requestMessage.Headers.Add(HttpConstants.HttpHeaders.PartitionKey, "[]");
            requestMessage.ResourceType = ResourceType.Document;
            requestMessage.OperationType = OperationType.Read;
            await invoker.SendAsync(requestMessage, new CancellationToken());

            int expectedHandlerCalls = 1;
            Assert.AreEqual(expectedHandlerCalls, handlerCalls);
        }

        [TestMethod]
        public async Task InvalidPartitionExceptionRetryHandlerDoesNotRetryOnSuccess()
        {
            CosmosClient client = MockCosmosUtil.CreateMockCosmosClient();

            NamedCacheRetryHandler retryHandler = new NamedCacheRetryHandler(client);
            int handlerCalls = 0;
            int expectedHandlerCalls = 1;
            TestHandler testHandler = new TestHandler((request, cancellationToken) => {
                handlerCalls++;
                return TestHandler.ReturnSuccess();
            });

            retryHandler.InnerHandler = testHandler;
            RequestInvokerHandler invoker = new RequestInvokerHandler(client);
            invoker.InnerHandler = retryHandler;
            RequestMessage requestMessage = new RequestMessage(HttpMethod.Get, new Uri("https://dummy.documents.azure.com:443/dbs"));
            await invoker.SendAsync(requestMessage, new CancellationToken());
            Assert.AreEqual(expectedHandlerCalls, handlerCalls);
        }

        [TestMethod]
        public async Task InvalidPartitionExceptionRetryHandlerDoesNotRetryOn410()
        {
            CosmosClient client = MockCosmosUtil.CreateMockCosmosClient();

            NamedCacheRetryHandler retryHandler = new NamedCacheRetryHandler(client);
            int handlerCalls = 0;
            int expectedHandlerCalls = 2;
            TestHandler testHandler = new TestHandler((request, cancellationToken) => {
                if (handlerCalls == 0)
                {
                    handlerCalls++;
                    return TestHandler.ReturnStatusCode((HttpStatusCode)StatusCodes.Gone, SubStatusCodes.NameCacheIsStale);
                }

                handlerCalls++;
                return TestHandler.ReturnSuccess();
            });

            retryHandler.InnerHandler = testHandler;
            RequestInvokerHandler invoker = new RequestInvokerHandler(client);
            invoker.InnerHandler = retryHandler;
            RequestMessage requestMessage = new RequestMessage(HttpMethod.Get, new Uri("https://dummy.documents.azure.com:443/dbs"));
            await invoker.SendAsync(requestMessage, new CancellationToken());
            Assert.AreEqual(expectedHandlerCalls, handlerCalls);
        }

        [TestMethod]
        public async Task PartitionKeyRangeGoneRetryHandlerOnSuccess()
        {
            CosmosClient client = MockCosmosUtil.CreateMockCosmosClient();

            PartitionKeyRangeGoneRetryHandler retryHandler = new PartitionKeyRangeGoneRetryHandler(client);
            int handlerCalls = 0;
            int expectedHandlerCalls = 1;
            TestHandler testHandler = new TestHandler((request, cancellationToken) => {
                handlerCalls++;
                return TestHandler.ReturnSuccess();
            });

            retryHandler.InnerHandler = testHandler;
            RequestInvokerHandler invoker = new RequestInvokerHandler(client);
            invoker.InnerHandler = retryHandler;
            RequestMessage requestMessage = new RequestMessage(HttpMethod.Get, new Uri("https://dummy.documents.azure.com:443/dbs"));
            await invoker.SendAsync(requestMessage, new CancellationToken());
            Assert.AreEqual(expectedHandlerCalls, handlerCalls);
        }

        [TestMethod]
        public async Task PartitionKeyRangeGoneRetryHandlerOn410()
        {
            CosmosClient client = MockCosmosUtil.CreateMockCosmosClient();

            int handlerCalls = 0;
            TestHandler testHandler = new TestHandler((request, response) => {
                if (handlerCalls == 0)
                {
                    handlerCalls++;
                    return TestHandler.ReturnStatusCode((HttpStatusCode)StatusCodes.Gone, SubStatusCodes.PartitionKeyRangeGone);
                }

                handlerCalls++;
                return TestHandler.ReturnSuccess();
            });

            PartitionKeyRangeGoneRetryHandler retryHandler = new PartitionKeyRangeGoneRetryHandler(client);
            retryHandler.InnerHandler = testHandler;

            RequestInvokerHandler invoker = new RequestInvokerHandler(client);
            invoker.InnerHandler = retryHandler;
            RequestMessage requestMessage = new RequestMessage(HttpMethod.Get, new Uri("https://localhost/dbs/db1/colls/col1/docs/doc1"));
            await invoker.SendAsync(requestMessage, new CancellationToken());

            int expectedHandlerCalls = 2;
            Assert.AreEqual(expectedHandlerCalls, handlerCalls);
        }
    }
}
