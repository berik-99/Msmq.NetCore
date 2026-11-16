using Msmq.NetCore.Messaging;

#if NET5_0_OR_GREATER
[assembly: System.Runtime.Versioning.SupportedOSPlatform("windows")]
#endif

if (Environment.OSVersion.Platform != PlatformID.Win32NT)
    throw new PlatformNotSupportedException("This compatibility probe requires Windows and MSMQ.");

foreach (bool transactional in new[] { false, true })
{
    string path = $".\\private$\\msmq-compat-{Guid.NewGuid():N}";
    bool created = false;
    try
    {
        using var queue = MessageQueue.Create(path, transactional);
        created = true;
        var transactionType = transactional ? MessageQueueTransactionType.Single : MessageQueueTransactionType.None;
        queue.Formatter = new XmlMessageFormatter(new[] { typeof(string) });
        queue.Send("XML compatibility", transactionType);
        using (var message = queue.Receive(TimeSpan.FromSeconds(5), transactionType))
            Check(Equals(message.Body, "XML compatibility"), "XML round trip");

        if (transactional)
        {
            using (var transaction = new MessageQueueTransaction())
            {
                transaction.Begin();
                queue.Send("aborted", transaction);
                transaction.Abort();
            }
            try
            {
                using var unexpected = queue.Receive(TimeSpan.Zero, MessageQueueTransactionType.Single);
                throw new InvalidOperationException("Aborted message was received.");
            }
            catch (MessageQueueException error) when (error.MessageQueueErrorCode == MessageQueueErrorCode.IOTimeout)
            { Console.WriteLine("PASS: transaction abort"); }
        }
        else
        {
            queue.Formatter = new ActiveXMessageFormatter();
            queue.Send((sbyte)-1);
            queue.Send("text after number");
            using (var number = queue.Receive(TimeSpan.FromSeconds(5)))
                Check(Equals(number.Body, (sbyte)-1), "signed byte round trip");
            using (var text = queue.Receive(TimeSpan.FromSeconds(5)))
                Check(Equals(text.Body, "text after number"), "text after numeric body");

            queue.Formatter = new XmlMessageFormatter(new[] { typeof(string) });
            var pending = queue.BeginReceive(TimeSpan.FromSeconds(5));
            queue.Send("asynchronous");
            Check(pending.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(10)), "async completion");
            using var received = queue.EndReceive(pending);
            Check(Equals(received.Body, "asynchronous"), "async body");
        }
    }
    finally
    {
        if (created)
            MessageQueue.Delete(path);
    }
}

Console.WriteLine($"Compatibility checks passed on {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}.");

static void Check(bool condition, string operation)
{
    if (!condition)
        throw new InvalidOperationException(operation + " failed.");
    Console.WriteLine("PASS: " + operation);
}
