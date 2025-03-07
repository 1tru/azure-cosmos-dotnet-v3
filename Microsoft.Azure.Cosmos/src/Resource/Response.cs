//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Net;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// The cosmos resource response class
    /// </summary>
    public abstract class Response<T>
    {
        /// <summary>
        /// Create an empty cosmos response for mock testing
        /// </summary>
        public Response()
        {
        }

        /// <summary>
        /// Create a Response object with the default properties set
        /// </summary>
        /// <param name="httpStatusCode">The status code of the response</param>
        /// <param name="headers">The headers of the response</param>
        /// <param name="resource">The object from the response</param>
        internal Response(
            HttpStatusCode httpStatusCode,
            Headers headers,
            T resource)
        {
            this.Headers = headers;
            this.StatusCode = httpStatusCode;
            this.Resource = resource;
        }

        /// <summary>
        /// Gets the current <see cref="ResponseMessage"/> HTTP headers.
        /// </summary>
        public virtual Headers Headers { get; }

        /// <summary>
        /// The content of the response.
        /// </summary>
        public virtual T Resource { get; protected set; }

        /// <summary>
        /// Get Resource implicitly from <see cref="Response{T}"/>
        /// </summary>
        /// <param name="response">The Azure Cosmos DB service response.</param>
        public static implicit operator T(Response<T> response)
        {
            return response.Resource;
        }

        /// <summary>
        /// Gets the request completion status code from the Azure Cosmos DB service.
        /// </summary>
        /// <value>The request completion status code</value>
        public virtual HttpStatusCode StatusCode { get; }

        /// <summary>
        /// Gets the request charge for this request from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The request charge measured in request units.
        /// </value>
        public virtual double RequestCharge => this.Headers.RequestCharge;

        /// <summary>
        /// Gets the activity ID for the request from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The activity ID for the request.
        /// </value>
        public virtual string ActivityId => this.Headers.ActivityId;

        /// <summary>
        /// Gets the entity tag associated with the resource from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The entity tag associated with the resource.
        /// </value>
        /// <remarks>
        /// ETags are used for concurrency checking when updating resources. 
        /// </remarks>
        public virtual string ETag => this.Headers.ETag;

        /// <summary>
        /// Gets the maximum size limit for this entity from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The maximum size limit for this entity. Measured in kilobytes for document resources 
        /// and in counts for other resources.
        /// </value>
        /// <remarks>
        /// To get public access to the quota information do the following
        /// cosmosResponse.Headers.GetHeaderValue("x-ms-resource-quota")
        /// </remarks>
        internal virtual string MaxResourceQuota => this.Headers.GetHeaderValue<string>(HttpConstants.HttpHeaders.MaxResourceQuota);

        /// <summary>
        /// Gets the current size of this entity from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The current size for this entity. Measured in kilobytes for document resources 
        /// and in counts for other resources.
        /// </value>
        /// <remarks>
        /// To get public access to the quota information do the following
        /// cosmosResponse.Headers.GetHeaderValue("x-ms-resource-usage")
        /// </remarks>
        internal virtual string CurrentResourceQuotaUsage => this.Headers.GetHeaderValue<string>(HttpConstants.HttpHeaders.CurrentResourceQuotaUsage);
    }
}