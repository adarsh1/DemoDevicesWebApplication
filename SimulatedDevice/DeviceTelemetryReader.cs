// ---------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ---------------------------------------------------------------

namespace SimulatedDevice
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.EventHubs;
    using Newtonsoft.Json.Linq;
    using NJsonSchema;

    public class DeviceTelemetryReader : IDisposable
    {
        private static EventHubClient eventHubClient;
        private readonly CancellationTokenSource cts;
        private IList<Task> tasks;
        ConcurrentQueue<KeyValuePair<string, string>> messageQueue;

        public DeviceTelemetryReader(string eventhubCompatibleConnectionString, string hubName)
        {
            var connectionString = new EventHubsConnectionStringBuilder(eventhubCompatibleConnectionString)
            {
                EntityPath = hubName
            };

            eventHubClient = EventHubClient.CreateFromConnectionString(connectionString.ToString());
            cts = new CancellationTokenSource();
            tasks = new List<Task>();
            messageQueue = new ConcurrentQueue<KeyValuePair<string, string>>();
        }

        public Task Start(string consumerGroup)
        {
            return this.Start(consumerGroup, "{}");
        }

        public async Task Start(string consumerGroup, string messageJsonSchema)
        {
            
            var schema = await JsonSchema4.FromJsonAsync(messageJsonSchema);
            var runtimeInfo = await eventHubClient.GetRuntimeInformationAsync();
            var d2cPartitions = runtimeInfo.PartitionIds;

            foreach (string partition in d2cPartitions)
            {
                tasks.Add(Task.Run(()=>ReceiveMessagesFromDeviceAsync(consumerGroup, partition, schema, cts.Token)));
            }

        }

        public IEnumerable<KeyValuePair<string,string>> GetMessageBatch(int max)
        {
            int count = 0;
            while(count++ < max && messageQueue.TryDequeue(out KeyValuePair<string, string> result))
            {
                yield return result;
            }
        }

        private async Task ReceiveMessagesFromDeviceAsync(string consumerGroup, string partition, JsonSchema4 schema, CancellationToken ct)
        {
            // Create the receiver using the default consumer group.
            // For the purposes of this sample, read only messages sent since 
            // the time the receiver is created. Typically, you don't want to skip any messages.
            var eventHubReceiver = eventHubClient.CreateReceiver(consumerGroup, partition, EventPosition.FromEnqueuedTime(DateTime.Now));
            Console.WriteLine("Create receiver on partition: " + partition);
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    Console.WriteLine("Listening for messages on: " + partition);
                    // Check for EventData - this methods times out if there is nothing to retrieve.
                    var events = await eventHubReceiver.ReceiveAsync(100);

                    // If there is data in the batch, process it.
                    if (events == null) continue;

                    foreach (EventData eventData in events)
                    {
                        if (eventData.SystemProperties.Keys.Contains("iothub-message-source") && (string)eventData.SystemProperties["iothub-message-source"] != "Telemetry")
                        {
                            continue;
                        }

                        string data = Encoding.UTF8.GetString(eventData.Body.Array);

                        var token = JToken.Parse(data);
                        IEnumerable<string> elements = new string[0];

                        if (token is JArray)
                        {
                            var array = token as JArray;
                            elements = array.Select(x => (x as JValue)?.Value as string);
                        }
                        else if (token is JObject)
                        {
                            elements = new string[] { data };
                        }

                        foreach (string element in elements)
                        {
                            if (element != null && !schema.Validate(element).Any())
                            {
                                var key = "";
                                if (eventData.SystemProperties.Keys.Contains("iothub-connection-device-id") && eventData.SystemProperties["iothub-connection-device-id"] != null)
                                {
                                    key = (string)eventData.SystemProperties["iothub-connection-device-id"];
                                }

                                messageQueue.Enqueue(new KeyValuePair<string, string>(key, element));
                            }
                        }

                    }
                }
                catch (Exception) { }
            }
        }

        public void Dispose()
        {
            this.cts?.Cancel();
            this.cts?.Dispose();
        }
    }
}
