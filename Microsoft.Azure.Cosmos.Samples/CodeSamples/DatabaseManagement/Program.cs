﻿namespace Cosmos.Samples.Shared
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Extensions.Configuration;

    internal class Program
    {
        //Read configuration
        private static readonly string databaseId = "samples";

        // Async main requires c# 7.1 which is set in the csproj with the LangVersion attribute 
        public static async Task Main(string[] args)
        {
            try
            {
                IConfigurationRoot configuration = new ConfigurationBuilder()
                    .AddJsonFile("appSettings.json")
                    .Build();

                string endpoint = configuration["EndPointUrl"];
                if (string.IsNullOrEmpty(endpoint))
                {
                    throw new ArgumentNullException("Please specify a valid endpoint in the appSettings.json");
                }

                string authKey = configuration["AuthorizationKey"];
                if (string.IsNullOrEmpty(authKey) || string.Equals(authKey, "Super secret key"))
                {
                    throw new ArgumentException("Please specify a valid AuthorizationKey in the appSettings.json");
                }

                //Read the Cosmos endpointUrl and authorisationKeys from configuration
                //These values are available from the Azure Management Portal on the Cosmos Account Blade under "Keys"
                //NB > Keep these values in a safe & secure location. Together they provide Administrative access to your Cosmos account
                using (CosmosClient client = new CosmosClient(endpoint, authKey))
                {
                    await Program.RunDatabaseDemo(client);
                }
            }
            catch (CosmosException cre)
            {
                Console.WriteLine(cre.ToString());
            }
            catch (Exception e)
            {
                Exception baseException = e.GetBaseException();
                Console.WriteLine("Error: {0}, Message: {1}", e.Message, baseException.Message);
            }
            finally
            {
                Console.WriteLine("End of demo, press any key to exit.");
                Console.ReadKey();
            }
        }

        /// <summary>
        /// Run basic database meta data operations as a console application.
        /// </summary>
        /// <returns></returns>
        private static async Task RunDatabaseDemo(CosmosClient client)
        {
            // An object containing relevant information about the response
            DatabaseResponse databaseResponse = await client.CreateDatabaseIfNotExistsAsync(databaseId, 10000);

            // A client side reference object that allows additional operations like ReadAsync
            CosmosDatabase database = databaseResponse;

            // The response from Azure Cosmos
            CosmosDatabaseSettings settings = databaseResponse;

            Console.WriteLine($"\n1. Create a database resource with id: {settings.Id} and last modified time stamp: {settings.LastModified}");
            Console.WriteLine($"\n2. Create a database resource request charge: {databaseResponse.RequestCharge} and Activity Id: {databaseResponse.ActivityId}");

            // Read the database from Azure Cosmos
            DatabaseResponse readResponse = await database.ReadAsync();
            Console.WriteLine($"\n3. Read a database: {readResponse.Resource.Id}");

            await readResponse.Database.CreateContainerAsync("testContainer", "/pk");

            // Get the current throughput for the database
            int? throughput = await database.ReadProvisionedThroughputAsync();
            if (throughput.HasValue)
            {
                Console.WriteLine($"\n4. Read a database throughput: {throughput.Value}");

                // Update the current throughput for the database
                await database.ReplaceProvisionedThroughputAsync(11000);
            }

            Console.WriteLine("\n5. Reading all databases resources for an account");
            FeedIterator<CosmosDatabaseSettings> iterator = client.GetDatabasesIterator();
            do
            {
                foreach (CosmosDatabaseSettings db in await iterator.FetchNextSetAsync())
                {
                    Console.WriteLine(db.Id);
                }
            } while (iterator.HasMoreResults);

            // Delete the database from Azure Cosmos.
            await database.DeleteAsync();
            Console.WriteLine($"\n6. Database {database.Id} deleted.");
        }
    }
}
