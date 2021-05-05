using Amazon.SQS;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.SQS;
using Amazon.SQS.Model;
using Instana.Tracing.Sdk.Spans;
using Instana.Tracing.Api;
using System.Threading;

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
                //MessageAttributes = new Dictionary<string, MessageAttributeValue>() { {"testKey", new MessageAttributeValue() { DataType = "String", StringValue = "testValue" } } },
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
            //receiveMessageRequest.MessageAttributeNames = new List<string>(2) { "X-INSTANA-T", "X-INSTANA-ST" };
            receiveMessageRequest.MaxNumberOfMessages = 10;

            Message correlationMessage = null;
            ReceiveMessageResponse r = await _amazonSQS.ReceiveMessageAsync(receiveMessageRequest);
            correlationMessage = r.Messages.FirstOrDefault();
                List<DeleteMessageBatchRequestEntry> deleteReqEntries = new List<DeleteMessageBatchRequestEntry>(r.Messages.Count);

            using (var span = CustomSpan.Create()
                       .AsAWSSQSMessageReceive(receiveMessageRequest.QueueUrl))
            {
                span.WrapAction(() =>
                {
                    foreach (var message in r.Messages)
                    {
                        Console.WriteLine("Messageid: " + message.MessageId);
                        Console.WriteLine("Message body:" + message.Body);
                        Console.WriteLine("Recepit: " + message.ReceiptHandle);
                        Console.WriteLine("MD5Body: " + message.MD5OfBody);
                        Console.WriteLine();
                        Thread.Sleep(2000);

                        deleteReqEntries.Add(new DeleteMessageBatchRequestEntry(message.MessageId, message.ReceiptHandle));
                    }


                }, true);

                span.AsChildOf(() => GetDisInfo(correlationMessage));

            }
            DeleteMessageBatchRequest deleteMessageBatchRequest = new DeleteMessageBatchRequest("http://localhost:9324/queue/test", deleteReqEntries);
            var response = await _amazonSQS.DeleteMessageBatchAsync(deleteMessageBatchRequest);

            return Ok();
        }

        [HttpGet("odlconsume")]
        public async Task<ActionResult> ConsumeQueueOld()
        {
            ReceiveMessageRequest receiveMessageRequest = new ReceiveMessageRequest("http://localhost:9324/queue/test");
            //receiveMessageRequest.MessageAttributeNames = new List<string>(2) { "X-INSTANA-T", "X-INSTANA-ST" };
            receiveMessageRequest.MaxNumberOfMessages = 10;

                    ReceiveMessageResponse r = _amazonSQS.ReceiveMessageAsync(receiveMessageRequest).Result;
                    

                    List<DeleteMessageBatchRequestEntry> deleteReqEntries = new List<DeleteMessageBatchRequestEntry>(r.Messages.Count);
                    foreach (var message in r.Messages)
                    {
                        Console.WriteLine("Messageid: " + message.MessageId);
                        Console.WriteLine("Message body:" + message.Body);
                        Console.WriteLine("Recepit: " + message.ReceiptHandle);
                        Console.WriteLine("MD5Body: " + message.MD5OfBody);
                        Console.WriteLine();
                        Thread.Sleep(2000);

                        deleteReqEntries.Add(new DeleteMessageBatchRequestEntry(message.MessageId, message.ReceiptHandle));
                    }

                    DeleteMessageBatchRequest deleteMessageBatchRequest = new DeleteMessageBatchRequest("http://localhost:9324/queue/test", deleteReqEntries);
                    var response = _amazonSQS.DeleteMessageBatchAsync(deleteMessageBatchRequest).Result;

            return Ok();
        }

        public static DistributedTraceInformation GetDisInfo(Message msg)
        {
            DistributedTraceInformation disInfo = new DistributedTraceInformation();

            if (msg == null) return disInfo;

            if (msg.MessageAttributes.TryGetValue(TracingConstants.ExternalTraceIdHeader, out MessageAttributeValue traceIdAttributeValue))
            {
                disInfo.TraceId = TraceIdUtil.GetLongFromHex(traceIdAttributeValue.StringValue);
            }

            if (msg.MessageAttributes.TryGetValue(TracingConstants.ExternalParentSpanIdHeader, out MessageAttributeValue parentIdAttributeValue))
            {
                disInfo.ParentSpanId = TraceIdUtil.GetLongFromHex(parentIdAttributeValue.StringValue);
            }

            return disInfo;
        }
    }
}
