using Amazon.SQS;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.SQS;
using Amazon.SQS.Model;

namespace DynamoDBDemo.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SQSController : ControllerBase
    {
        private readonly IAmazonSQS _amazonSQS;

        private bool useLocalSQS = true;

        public SQSController(IAmazonSQS amazonSQS)
        {
            if (!useLocalSQS)
            {
                _amazonSQS = amazonSQS;
            }
            else
            {
                _amazonSQS = InitializeSQSClient();
            }
        }

        private IAmazonSQS InitializeSQSClient()
        {
            var sqsConfig = new AmazonSQSConfig();

            sqsConfig.ServiceURL = "http://localhost:9324";
            return new AmazonSQSClient(sqsConfig);
        }

        [HttpGet("addtoqueue/{queueName}")]
        public async Task<ActionResult> AddToQueue(string queuename)
        {
            SendMessageRequest sendMessageRequest = new SendMessageRequest()
            {
                //QueueUrl = "https://sqs.us-east-2.amazonaws.com/410797082306/dotnetTestQueue",
                QueueUrl = "http://localhost:9324/queue/test",
                MessageAttributes = new Dictionary<string, MessageAttributeValue>() { {"testKey", new MessageAttributeValue() { DataType = "String", StringValue = "testValue" } } },
                MessageBody = $"Test at {DateTime.UtcNow.ToLongDateString()}"
            };

            SendMessageResponse sendMessageResponse = await _amazonSQS.SendMessageAsync(sendMessageRequest);
         
            return Ok("Hi");
        }

        [HttpGet("createqueue/{queueName}")]
        public async Task<ActionResult> CreateQueue(string queueName)
        {
            CreateQueueRequest createQueueRequest = new CreateQueueRequest(queueName);

            var response = await _amazonSQS.CreateQueueAsync(createQueueRequest);

            return Ok($"Queue {queueName} is created");
        }

        [HttpGet("deletequeue/{queueName}")]
        public async Task<ActionResult> DeleteQueue(string queueName)
        {
            GetQueueUrlRequest getQueueUrlRequest = new GetQueueUrlRequest(queueName);
            GetQueueUrlResponse getUrlResponse = await _amazonSQS.GetQueueUrlAsync(getQueueUrlRequest);

            DeleteQueueRequest deleteQueueRequest = new DeleteQueueRequest(getUrlResponse.QueueUrl);

            var response = await _amazonSQS.DeleteQueueAsync(deleteQueueRequest);

            return Ok($"Queue {queueName} is deleted");
        }


        [HttpGet("consume")]
        public async Task<ActionResult> ConsumeQueue()
        {
            ReceiveMessageRequest receiveMessageRequest = new ReceiveMessageRequest("http://localhost:9324/queue/test");
            receiveMessageRequest.MessageAttributeNames = new List<string>(2) { "All" };
            receiveMessageRequest.MaxNumberOfMessages = 10;
            ReceiveMessageResponse r = await _amazonSQS.ReceiveMessageAsync(receiveMessageRequest);

            List<DeleteMessageBatchRequestEntry> deleteReqEntries = new List<DeleteMessageBatchRequestEntry>(r.Messages.Count);
            foreach(var message in r.Messages)
            {
                Console.WriteLine("Messageid: " + message.MessageId);
                Console.WriteLine("Message body:" + message.Body);
                Console.WriteLine("Recepit: " + message.ReceiptHandle);
                Console.WriteLine("MD5Body: " + message.MD5OfBody);
                Console.WriteLine();

                deleteReqEntries.Add(new DeleteMessageBatchRequestEntry(message.MessageId, message.ReceiptHandle));
            }
                DeleteMessageBatchRequest deleteMessageBatchRequest = new DeleteMessageBatchRequest("http://localhost:9324/queue/test", deleteReqEntries);
                var response = await _amazonSQS.DeleteMessageBatchAsync(deleteMessageBatchRequest);

            return Ok();
        }
    }
}
