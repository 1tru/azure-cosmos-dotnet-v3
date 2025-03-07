//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Scripts;

    internal class CosmosResponseFactory
    {
        /// <summary>
        /// Cosmos JSON converter. This allows custom JSON parsers.
        /// </summary>
        private readonly CosmosSerializer cosmosSerializer;

        /// <summary>
        /// This is used for all meta data types
        /// </summary>
        private readonly CosmosSerializer propertiesSerializer;

        internal CosmosResponseFactory(
            CosmosSerializer defaultJsonSerializer,
            CosmosSerializer userJsonSerializer)
        {
            this.propertiesSerializer = defaultJsonSerializer;
            this.cosmosSerializer = userJsonSerializer;
        }

        internal FeedResponse<T> CreateResultSetQueryResponse<T>(
            ResponseMessage cosmosResponseMessage)
        {
            return FeedIteratorCore<T>.CreateCosmosQueryResponse(
                cosmosResponseMessage,
                this.cosmosSerializer);
        }

        internal Task<ItemResponse<T>> CreateItemResponseAsync<T>(
            Task<ResponseMessage> cosmosResponseMessageTask)
        {
            return this.ProcessMessageAsync(cosmosResponseMessageTask, (cosmosResponseMessage) =>
            {
                T item = this.ToObjectInternal<T>(cosmosResponseMessage, this.cosmosSerializer);
                return new ItemResponse<T>(
                    cosmosResponseMessage.StatusCode,
                    cosmosResponseMessage.Headers,
                    item);
            });
        }

        internal Task<ContainerResponse> CreateContainerResponseAsync(
            Container container,
            Task<ResponseMessage> cosmosResponseMessageTask)
        {
            return this.ProcessMessageAsync(cosmosResponseMessageTask, (cosmosResponseMessage) =>
            {
                ContainerProperties containerProperties = this.ToObjectInternal<ContainerProperties>(cosmosResponseMessage, this.propertiesSerializer);
                return new ContainerResponse(
                    cosmosResponseMessage.StatusCode,
                    cosmosResponseMessage.Headers,
                    containerProperties,
                    container);
            });
        }

        internal Task<DatabaseResponse> CreateDatabaseResponseAsync(
            Database database,
            Task<ResponseMessage> cosmosResponseMessageTask)
        {
            return this.ProcessMessageAsync(cosmosResponseMessageTask, (cosmosResponseMessage) =>
            {
                DatabaseProperties databaseProperties = this.ToObjectInternal<DatabaseProperties>(cosmosResponseMessage, this.propertiesSerializer);
                return new DatabaseResponse(
                    cosmosResponseMessage.StatusCode,
                    cosmosResponseMessage.Headers,
                    databaseProperties,
                    database);
            });
        }

        internal Task<ThroughputResponse> CreateThroughputResponseAsync(
            Task<ResponseMessage> cosmosResponseMessageTask)
        {
            return this.ProcessMessageAsync(cosmosResponseMessageTask, (cosmosResponseMessage) =>
            {
                ThroughputProperties throughputProperties = this.ToObjectInternal<ThroughputProperties>(cosmosResponseMessage, this.propertiesSerializer);
                return new ThroughputResponse(
                    cosmosResponseMessage.StatusCode,
                    cosmosResponseMessage.Headers,
                    throughputProperties);
            });
        }

        internal Task<StoredProcedureExecuteResponse<T>> CreateStoredProcedureExecuteResponseAsync<T>(Task<ResponseMessage> cosmosResponseMessageTask)
        {
            return this.ProcessMessageAsync(cosmosResponseMessageTask, (cosmosResponseMessage) =>
            {
                T item = this.ToObjectInternal<T>(cosmosResponseMessage, this.cosmosSerializer);
                return new StoredProcedureExecuteResponse<T>(
                    cosmosResponseMessage.StatusCode,
                    cosmosResponseMessage.Headers,
                    item);
            });
        }

        internal Task<StoredProcedureResponse> CreateStoredProcedureResponseAsync(Task<ResponseMessage> cosmosResponseMessageTask)
        {
            return this.ProcessMessageAsync(cosmosResponseMessageTask, (cosmosResponseMessage) =>
            {
                StoredProcedureProperties cosmosStoredProcedure = this.ToObjectInternal<StoredProcedureProperties>(cosmosResponseMessage, this.propertiesSerializer);
                return new StoredProcedureResponse(
                    cosmosResponseMessage.StatusCode,
                    cosmosResponseMessage.Headers,
                    cosmosStoredProcedure);
            });
        }

        internal Task<TriggerResponse> CreateTriggerResponseAsync(Task<ResponseMessage> cosmosResponseMessageTask)
        {
            return this.ProcessMessageAsync(cosmosResponseMessageTask, (cosmosResponseMessage) =>
            {
                TriggerProperties triggerProperties = this.ToObjectInternal<TriggerProperties>(cosmosResponseMessage, this.propertiesSerializer);
                return new TriggerResponse(
                    cosmosResponseMessage.StatusCode,
                    cosmosResponseMessage.Headers,
                    triggerProperties);
            });
        }

        internal Task<UserDefinedFunctionResponse> CreateUserDefinedFunctionResponseAsync(Task<ResponseMessage> cosmosResponseMessageTask)
        {
            return this.ProcessMessageAsync(cosmosResponseMessageTask, (cosmosResponseMessage) =>
            {
                UserDefinedFunctionProperties settings = this.ToObjectInternal<UserDefinedFunctionProperties>(cosmosResponseMessage, this.propertiesSerializer);
                return new UserDefinedFunctionResponse(
                    cosmosResponseMessage.StatusCode,
                    cosmosResponseMessage.Headers,
                    settings);
            });
        }

        internal async Task<T> ProcessMessageAsync<T>(Task<ResponseMessage> cosmosResponseTask, Func<ResponseMessage, T> createResponse)
        {
            using (ResponseMessage message = await cosmosResponseTask)
            {
                return createResponse(message);
            }
        }

        internal T ToObjectInternal<T>(ResponseMessage cosmosResponseMessage, CosmosSerializer jsonSerializer)
        {
            // Not finding something is part of a normal work-flow and should not be an exception.
            // This prevents the unnecessary overhead of an exception
            if (cosmosResponseMessage.StatusCode == HttpStatusCode.NotFound)
            {
                return default(T);
            }

            //Throw the exception
            cosmosResponseMessage.EnsureSuccessStatusCode();

            if (cosmosResponseMessage.Content == null)
            {
                return default(T);
            }

            return jsonSerializer.FromStream<T>(cosmosResponseMessage.Content);
        }
    }
}