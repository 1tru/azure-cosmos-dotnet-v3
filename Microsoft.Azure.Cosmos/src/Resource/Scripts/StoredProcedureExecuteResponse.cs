//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Scripts
{
    using System;
    using System.Net;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// The cosmos stored procedure response
    /// </summary>
    public class StoredProcedureExecuteResponse<T> : Response<T>
    {
        /// <summary>
        /// Create a <see cref="StoredProcedureExecuteResponse{T}"/> as a no-op for mock testing
        /// </summary>
        public StoredProcedureExecuteResponse() 
            : base()
        {
        }

        /// <summary>
        /// A private constructor to ensure the factory is used to create the object.
        /// This will prevent memory leaks when handling the HttpResponseMessage
        /// </summary>
        internal StoredProcedureExecuteResponse(
           HttpStatusCode httpStatusCode,
           Headers headers,
           T response) 
            : base(httpStatusCode,
               headers,
               response)
        {
        }

        /// <summary>
        /// Gets the token for use with session consistency requests from the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The token for use with session consistency requests.
        /// </value>
        public virtual string SessionToken => this.Headers.GetHeaderValue<string>(HttpConstants.HttpHeaders.SessionToken);

        /// <summary>
        /// Gets the output from stored procedure console.log() statements.
        /// </summary>
        /// <value>
        /// Output from console.log() statements in a stored procedure.
        /// </value>
        /// <seealso cref="StoredProcedureRequestOptions.EnableScriptLogging"/>
        public virtual string ScriptLog
        {
            get
            {
                string logResults = this.Headers.GetHeaderValue<string>(HttpConstants.HttpHeaders.LogResults);
                return string.IsNullOrEmpty(logResults) ? logResults : Uri.UnescapeDataString(logResults);
            }
        }
    }
}