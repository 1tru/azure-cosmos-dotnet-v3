﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// The default cosmos request options
    /// </summary>
    public class RequestOptions
    {
        internal IDictionary<string, object> Properties { get; set; }

        /// <summary>
        /// Gets or sets the If-Match (ETag) associated with the request in the Azure Cosmos DB service.
        /// </summary>
        /// <remarks>
        /// Most commonly used with the Delete* and Replace* methods of <see cref="Container"/> such as <see cref="Container.ReplaceItemAsync{T}(T, string, PartitionKey, ItemRequestOptions, System.Threading.CancellationToken)"/>
        /// but can be used with other methods like <see cref="Container.ReadItemAsync{T}(string, PartitionKey, ItemRequestOptions, System.Threading.CancellationToken)"/> for caching scenarios.
        /// </remarks>
        public virtual string IfMatchEtag { get; set; }

        /// <summary>
        /// Gets or sets the If-None-Match (ETag) associated with the request in the Azure Cosmos DB service.
        /// </summary>
        /// <remarks>
        /// Most commonly used to detect changes to the resource
        /// </remarks>
        public virtual string IfNoneMatchEtag { get; set; }

        /// <summary>
        /// Fill the CosmosRequestMessage headers with the set properties
        /// </summary>
        /// <param name="request">The <see cref="RequestMessage"/></param>
        internal virtual void PopulateRequestOptions(RequestMessage request)
        {
            if (this.Properties != null)
            {
                foreach (KeyValuePair<string, object> property in this.Properties)
                {
                    request.Properties[property.Key] = property.Value;
                }
            }

            if (this.IfMatchEtag != null)
            {
                request.Headers.Add(HttpConstants.HttpHeaders.IfMatch, this.IfMatchEtag);
            }

            if (this.IfNoneMatchEtag != null)
            {
                request.Headers.Add(HttpConstants.HttpHeaders.IfNoneMatch, this.IfNoneMatchEtag);
            }
        }

        /// <summary>
        /// Gets the resource URI passed in as a request option. This is used by MongoDB and Cassandra implementation for performance reasons.
        /// </summary>
        /// <param name="resourceUri">The URI passed in from the request options</param>
        /// <returns>True if the object exists in the request options. False if the value was not passed in as a request option</returns>
        internal bool TryGetResourceUri(out Uri resourceUri)
        {
            if (this.Properties != null && this.Properties.TryGetValue(HandlerConstants.ResourceUri, out var requestOptesourceUri))
            {
                Uri uri = requestOptesourceUri as Uri;
                if (uri == null || uri.IsAbsoluteUri)
                {
                    throw new ArgumentException(HandlerConstants.ResourceUri + " must be a relative Uri of type System.Uri");
                }

                resourceUri = uri;
                return true;
            }

            resourceUri = null;
            return false;
        }

        /// <summary>
        /// Set the consistency level
        /// </summary>
        /// <param name="request">The current request.</param>
        /// <param name="consistencyLevel">The desired Consistency level.</param>
        protected static void SetConsistencyLevel(RequestMessage request, ConsistencyLevel? consistencyLevel)
        {
            if (consistencyLevel != null && consistencyLevel.HasValue)
            {
                // ConsistencyLevel compatibility with back-end configuration will be done by RequestInvokeHandler
                request.Headers.Add(HttpConstants.HttpHeaders.ConsistencyLevel, consistencyLevel.ToString());
            }
        }

        /// <summary>
        /// Set the session token
        /// </summary>
        /// <param name="request">The current request.</param>
        /// <param name="sessionToken">The current session token.</param>
        protected static void SetSessionToken(RequestMessage request, string sessionToken)
        {
            if (!string.IsNullOrWhiteSpace(sessionToken))
            {
                request.Headers.Add(HttpConstants.HttpHeaders.SessionToken, sessionToken);
            }
        }
    }
}
