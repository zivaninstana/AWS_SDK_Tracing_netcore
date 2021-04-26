using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DynamoDBDemo.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DynamoDbController : ControllerBase
    {
        private readonly DynamoDBContext dynamoDBcontext;
        public DynamoDbController(IAmazonDynamoDB amazonDynamoDB)
        {
            dynamoDBcontext = new DynamoDBContext(amazonDynamoDB);
        }

        [HttpGet]
        public async Task<ActionResult> Default()
        {
            return Ok("It worked");
        }

        [HttpGet("allmessages")]
        public async Task<ActionResult> GetAllMessages()
        {
            var scanConditions = new List<ScanCondition>();
            var allDocs = await dynamoDBcontext.ScanAsync<MessageModel>(scanConditions).GetRemainingAsync();
            return Ok(allDocs);
        }

        [HttpGet("insertmessages")]
        public async Task<ActionResult> InsertMessages()
        {
            var mess = new MessageModel()
            {
                User = "Dusan",
                Ticks = DateTime.UtcNow.Ticks,
                Thread = "Basketball",
                Message = "Basketball is the best"
            };
            await dynamoDBcontext.SaveAsync(mess);

            var mess2 = new MessageModel()
            {
                User = "Silni",
                Ticks = DateTime.UtcNow.Ticks,
                Thread = "Basketball",
                Message = "Basketball is the worst"
            };
            await dynamoDBcontext.SaveAsync(mess2);

            var mess3 = new MessageModel()
            {
                User = "Lazar",
                Ticks = DateTime.UtcNow.Ticks,
                Thread = "Rugby",
                Message = "Rugby is the best"
            };
            await dynamoDBcontext.SaveAsync(mess3);

            var mess4 = new MessageModel()
            {
                User = "Jakov",
                Ticks = DateTime.UtcNow.Ticks,
                Thread = "Football",
                Message = "Football is the best"
            };
            await dynamoDBcontext.SaveAsync(mess4);

            return Ok("All ok");
        }

        [HttpGet("getusermessages/{user}")]
        public async Task<ActionResult> GetUserMessages(string user)
        {
            var userMessages = await dynamoDBcontext.QueryAsync<MessageModel>(user).GetRemainingAsync();

            return Ok(userMessages);
        }

        [HttpGet("getthreadmessages/{thread}")]
        public async Task<ActionResult> GetThreadMessages(string thread)
        {
            var config = new DynamoDBOperationConfig
            {
                IndexName = "Thread-Ticks-index",
                QueryFilter = new List<ScanCondition>(),
                BackwardQuery = true
            };

            var threadMessages = await dynamoDBcontext.QueryAsync<MessageModel>(thread, config).GetRemainingAsync();

            return Ok(threadMessages);
        }

        [HttpGet("userthreadmessages")]
        public async Task<ActionResult> GetUserThreadMessages(string user, string thread)
        {
            var conditions = new List<ScanCondition>();
            conditions.Add(new ScanCondition("Thread", Amazon.DynamoDBv2.DocumentModel.ScanOperator.Equal, thread));

            var config = new DynamoDBOperationConfig
            {
                QueryFilter = conditions,
                BackwardQuery = true
            };

            var userThreadMessages = await dynamoDBcontext.QueryAsync<MessageModel>(user, config).GetRemainingAsync();

            return Ok(userThreadMessages);
        }

        [HttpGet("deleteusermessages/{user}")]
        public async Task<ActionResult> DeleteUserMessages(string user)
        {
            var userMessages = await dynamoDBcontext.QueryAsync<MessageModel>(user).GetRemainingAsync();

            foreach(var message in userMessages)
            {
                await dynamoDBcontext.DeleteAsync<MessageModel>(message);
            }

            foreach (var message in userMessages)
            {
                await dynamoDBcontext.DeleteAsync<MessageModel>(message);
            }

            return Ok(userMessages);
        }

        private static AmazonDynamoDBClient CreateClient()
        {

            var config = new AmazonDynamoDBConfig
            {
                ServiceURL = "http://localhost:8000/"
            };
            AmazonDynamoDBConfig clientConfig = new AmazonDynamoDBConfig();
            // This client will access the US East 1 region.
            clientConfig.RegionEndpoint = RegionEndpoint.USEast2;
            AmazonDynamoDBClient client = new AmazonDynamoDBClient(clientConfig);

            return client;
        }

        private static AmazonDynamoDBClient client = CreateClient();
        private static string tableName = "ExampleTable";


        [HttpGet("show")]
        public async Task<ActionResult> Show()
        {
            try
            {
                await CreateExampleTable();
                await ListMyTables();
                await GetTableInformation();
                await UpdateExampleTable();

                await DeleteExampleTable();
                //await DeleteExampleTable();

                //Console.WriteLine("To continue, press Enter");
                //Console.ReadLine();
            }
            catch (AmazonDynamoDBException e) { Console.WriteLine(e.Message); }
            catch (Exception e) { Console.WriteLine(e.Message); }

            return Ok();
        }

        private async Task CreateExampleTable()
        {
            Console.WriteLine("\n*** Creating table ***");
            var request = new CreateTableRequest
            {
                AttributeDefinitions = new List<AttributeDefinition>()
            {
                new AttributeDefinition
                {
                    AttributeName = "Id",
                    AttributeType = "N"
                },
                new AttributeDefinition
                {
                    AttributeName = "ReplyDateTime",
                    AttributeType = "N"
                }
            },
                KeySchema = new List<KeySchemaElement>
            {
                new KeySchemaElement
                {
                    AttributeName = "Id",
                    KeyType = "HASH" //Partition key
                },
                new KeySchemaElement
                {
                    AttributeName = "ReplyDateTime",
                    KeyType = "RANGE" //Sort key
                }
            },
                ProvisionedThroughput = new ProvisionedThroughput
                {
                    ReadCapacityUnits = 5,
                    WriteCapacityUnits = 6
                },
                TableName = tableName
            };

            var response = await client.CreateTableAsync(request);

            var tableDescription = response.TableDescription;
            Console.WriteLine("{1}: {0} \t ReadsPerSec: {2} \t WritesPerSec: {3}",
                      tableDescription.TableStatus,
                      tableDescription.TableName,
                      tableDescription.ProvisionedThroughput.ReadCapacityUnits,
                      tableDescription.ProvisionedThroughput.WriteCapacityUnits);

            string status = tableDescription.TableStatus;
            Console.WriteLine(tableName + " - " + status);

            await WaitUntilTableReady(tableName);
        }

        private async Task ListMyTables()
        {
            Console.WriteLine("\n*** listing tables ***");
            string lastTableNameEvaluated = null;
            do
            {
                var request = new ListTablesRequest
                {
                    Limit = 2,
                    ExclusiveStartTableName = lastTableNameEvaluated
                };

                var response = await client.ListTablesAsync(request);
                foreach (string name in response.TableNames)
                    Console.WriteLine(name);

                lastTableNameEvaluated = response.LastEvaluatedTableName;
            } while (lastTableNameEvaluated != null);
        }

        private async Task GetTableInformation()
        {
            Console.WriteLine("\n*** Retrieving table information ***");
            var request = new DescribeTableRequest
            {
                TableName = tableName
            };

            var response = await client.DescribeTableAsync(request);

            TableDescription description = response.Table;
            Console.WriteLine("Name: {0}", description.TableName);
            Console.WriteLine("# of items: {0}", description.ItemCount);
            Console.WriteLine("Provision Throughput (reads/sec): {0}",
                      description.ProvisionedThroughput.ReadCapacityUnits);
            Console.WriteLine("Provision Throughput (writes/sec): {0}",
                      description.ProvisionedThroughput.WriteCapacityUnits);
        }

        private async Task UpdateExampleTable()
        {
            Console.WriteLine("\n*** Updating table ***");
            var request = new UpdateTableRequest()
            {
                TableName = tableName,
                ProvisionedThroughput = new ProvisionedThroughput()
                {
                    ReadCapacityUnits = 6,
                    WriteCapacityUnits = 7
                }
            };

            var response = client.UpdateTableAsync(request);

            WaitUntilTableReady(tableName);
        }

        private async Task DeleteExampleTable()
        {
            Console.WriteLine("\n*** Deleting table ***");
            var request = new DeleteTableRequest
            {
                TableName = tableName
            };

            var response = await client.DeleteTableAsync(request);

            Console.WriteLine("Table is being deleted...");
        }

        private async Task WaitUntilTableReady(string tableName)
        {
            string status = null;
            // Let us wait until table is created. Call DescribeTable.
            do
            {
                System.Threading.Thread.Sleep(5000); // Wait 5 seconds.
                try
                {
                    var res = await client.DescribeTableAsync(new DescribeTableRequest
                    {
                        TableName = tableName
                    });

                    Console.WriteLine("Table name: {0}, status: {1}",
                              res.Table.TableName,
                              res.Table.TableStatus);
                    status = res.Table.TableStatus;
                }
                catch (ResourceNotFoundException)
                {
                    // DescribeTable is eventually consistent. So you might
                    // get resource not found. So we handle the potential exception.
                }
            } while (status != "ACTIVE");
        }
    }
}
