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
        private FeedbackReceiver<FeedbackBatch> feedbackReceiver;
        public ConcurrentQueue<FeedbackRecord> Feedback { get; }
        public ConcurrentQueue<FileNotification> FileNotification { get; }

        public SimulatedService(string connectionString, int transportTypeInt)
        {
            this.serviceConnectionString = connectionString;
            this.transportType = (TransportType)transportTypeInt;
            this.client = ServiceClient.CreateFromConnectionString(serviceConnectionString, this.transportType);
            this.cancelationTokenSource = new CancellationTokenSource();
            this.serviceSemaphore = new SemaphoreSlim(1, 1);
            Feedback = new ConcurrentQueue<FeedbackRecord>();
            FileNotification = new ConcurrentQueue<FileNotification>();
            Task.Run(() => ReceiveFeedback(this.cancelationTokenSource.Token));
            Task.Run(() => ReceiveFileNotification(this.cancelationTokenSource.Token));
        }

        public void Dispose()
        {
            this.cancelationTokenSource.Cancel();
            this.cancelationTokenSource.Dispose();
        }

        public async Task ReceiveFeedback( CancellationToken cancellationToken)
        {
            feedbackReceiver = this.client.GetFeedbackReceiver();

            while (!cancellationToken.IsCancellationRequested)
            {
                var feedbackBatch = await feedbackReceiver.ReceiveAsync();

                if (feedbackBatch != null)
                {
                    foreach (var record in feedbackBatch.Records)
                    {
                        Feedback.Enqueue(record);
                    }

                    await feedbackReceiver.CompleteAsync(feedbackBatch);
                }
            }
        }

        public async Task ReceiveFileNotification(CancellationToken cancellationToken)
        {
            var fileNotificationReceiver = this.client.GetFileNotificationReceiver();

            while (!cancellationToken.IsCancellationRequested)
            {
                var notification = await fileNotificationReceiver.ReceiveAsync();

                if (notification != null)
                {
                    FileNotification.Enqueue(notification);

                    await fileNotificationReceiver.CompleteAsync(notification);
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
