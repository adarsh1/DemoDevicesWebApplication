using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Devices;

namespace ServiceSimulation
{
    public class SimulatedService:IDisposable
    {
        private ServiceClient client;
        private string serviceConnectionString;
        private TransportType transportType;
        private CancellationTokenSource cancelationTokenSource;
        private SemaphoreSlim serviceSemaphore;
        public ConcurrentQueue<FeedbackRecord> Feedback { get; }

        public SimulatedService(string connectionString, int transportTypeInt)
        {
            this.serviceConnectionString = connectionString;
            this.transportType = (TransportType)transportTypeInt;
            this.client = ServiceClient.CreateFromConnectionString(serviceConnectionString, this.transportType);
            this.cancelationTokenSource = new CancellationTokenSource();
            this.serviceSemaphore = new SemaphoreSlim(1, 1);
            Feedback = new ConcurrentQueue<FeedbackRecord>();
            Task.Run(() => ReceiveFeedback(this.cancelationTokenSource.Token));
        }

        public void Dispose()
        {
            this.cancelationTokenSource.Cancel();
            this.cancelationTokenSource.Dispose();
        }

        public async Task ReceiveFeedback( CancellationToken cancellationToken)
        {
            var feedbackReceiver = this.client.GetFeedbackReceiver();

            while (!cancellationToken.IsCancellationRequested)
            {
                var feedbackBatch = await feedbackReceiver.ReceiveAsync();

                if (feedbackBatch != null)
                {
                    Console.WriteLine("UserId: "+ feedbackBatch.UserId);
                    Console.WriteLine();
                    foreach (var record in feedbackBatch.Records)
                    {
                        Feedback.Enqueue(record);
                        Console.WriteLine($"Description: {record.Description}, DeviceGenerationId : {record.DeviceGenerationId}, DeviceId : {record.DeviceId}, EnqueuedTimeUtc : {record.EnqueuedTimeUtc},  OriginalMessageId : {record.OriginalMessageId}, StatusCode : {record.StatusCode}");
                    }
                    Console.WriteLine();

                    await feedbackReceiver.CompleteAsync(feedbackBatch);
                }
            }
        }

        public async Task SendMessage(string msg, string deviceId)
        {
            var commandMessage = new Message(Encoding.ASCII.GetBytes(msg))
            {
                Ack = DeliveryAcknowledgement.Full
            };
            await client.SendAsync(deviceId, commandMessage);
        }
    }
}
