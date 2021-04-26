using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.DynamoDBv2.DataModel;

namespace DynamoDBDemo
{
    [DynamoDBTable("MessageBoard")]
    public class MessageModel
    {
        public MessageModel()
        {

        }

        [DynamoDBHashKey]
        public string User { get; set; }

        [DynamoDBRangeKey]
        public long Ticks { get; set; }

        [DynamoDBGlobalSecondaryIndexHashKey]
        public string Thread { get; set; }

        [DynamoDBProperty]
        public string Message { get; set; }
    }

   
}
