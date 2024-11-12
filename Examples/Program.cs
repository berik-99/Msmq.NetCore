using NetCore.Msmq.Messaging;

string QueuePath = @".\private$\TestQueue";

if (!MessageQueue.Exists(QueuePath))
{
	MessageQueue.Create(QueuePath);
}

using MessageQueue queue = new(QueuePath);

// Sender thread
var senderTask = Task.Run(() =>
{
	int counter = 0;
	while (true)
	{
		string messageBody = $"Message {counter}";
		queue.Send(messageBody);
		Console.WriteLine($"Sent: {messageBody}");
		counter++;
		Thread.Sleep(1000);
	}
});

// Receiver thread
var receiverTask = Task.Run(() =>
{
	while (true)
	{
		try
		{
			var message = queue.Receive();
			string messageBody = (string)message.Body;
			Console.WriteLine($"Received: {messageBody}");
		}
		catch (MessageQueueException mqe)
		{
			Console.Error.WriteLine($"MessageQueueException: {mqe.Message}");
		}
		catch (Exception ex)
		{
			Console.Error.WriteLine($"Exception: {ex.Message}");
		}
	}
});

await Task.WhenAll(senderTask, receiverTask);