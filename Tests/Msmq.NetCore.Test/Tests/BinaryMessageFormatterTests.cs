using System.Drawing;
using Msmq.NetCore.Messaging;

namespace Msmq.NetCore.Test.Tests;

public class BinaryMessageFormatterTests : IAsyncLifetime
{
    private const string TEST_IMAGE = "assets/ironing a kitten.jpg";
    private readonly QueueFixture _fixture = new();

    public async Task InitializeAsync()
    {
        await _fixture.InitializeAsync();

        if (!MessageQueue.Exists(TestCommon.TEST_PRIVATE_NONTRANSACTIONAL_QUEUE))
        {
            MessageQueue.Create(TestCommon.TEST_PRIVATE_NONTRANSACTIONAL_QUEUE);
        }
    }

    public async Task DisposeAsync()
    {
        _fixture.PurgeQueue(TestCommon.TEST_PRIVATE_NONTRANSACTIONAL_QUEUE);
        await _fixture.DisposeAsync();
    }

    [Fact]
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public void BinaryMessageFormatterTest()
    {
        if (!OperatingSystem.IsWindows())
            return;

#if NET9_0_OR_GREATER
        // BinaryFormatter has been removed in .NET 9+, so this test cannot run on this runtime.
        return;
#endif
        Assert.True(File.Exists(TEST_IMAGE), "Can't find binary file to test.");

        Image thisImage = Image.FromFile(TEST_IMAGE);
        using var q = new MessageQueue(TestCommon.TEST_PRIVATE_NONTRANSACTIONAL_QUEUE);
        using var m = new Message(thisImage, new BinaryMessageFormatter());
        q.Formatter = new BinaryMessageFormatter();

        q.Send(m);
        string msgId = m.Id;
        var recMsg = q.ReceiveById(msgId, TimeSpan.FromSeconds(10.0));

        // Verify that the message was received
        Assert.NotNull(recMsg);
        Assert.Equal(msgId, recMsg.Id);
        recMsg.Dispose();
        thisImage.Dispose();
    }

    [Fact]
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public void BinaryMessageFormatterWithStringBodyTest()
    {
        if (!OperatingSystem.IsWindows())
            return;

#if NET9_0_OR_GREATER
        return;
#endif
        using var q = new MessageQueue(TestCommon.TEST_PRIVATE_NONTRANSACTIONAL_QUEUE);
        q.Formatter = new BinaryMessageFormatter();

        const string testBody = "Test Body String";
        using var msg = new Message(testBody, new BinaryMessageFormatter());

        q.Send(msg);
        var receivedMsg = q.ReceiveById(msg.Id, TimeSpan.FromSeconds(5));

        Assert.NotNull(receivedMsg);
        Assert.Equal(testBody, (string)receivedMsg.Body);
        receivedMsg.Dispose();
    }

    [Fact]
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public void BinaryMessageFormatterWithObjectBodyTest()
    {
        if (!OperatingSystem.IsWindows())
            return;

#if NET9_0_OR_GREATER
        return;
#endif
        using var q = new MessageQueue(TestCommon.TEST_PRIVATE_NONTRANSACTIONAL_QUEUE);
        q.Formatter = new BinaryMessageFormatter();

        var person = TestCommon.CreateTestPerson();
        using var msg = new Message(person, new BinaryMessageFormatter());

        q.Send(msg);
        var receivedMsg = q.ReceiveById(msg.Id, TimeSpan.FromSeconds(5));

        Assert.NotNull(receivedMsg);
        var receivedPerson = (Person)receivedMsg.Body;
        Assert.Equal(person.FirstName, receivedPerson.FirstName);
        Assert.Equal(person.LastName, receivedPerson.LastName);
        receivedMsg.Dispose();
    }

    internal static byte[] ConvertStreamToByteArray(Stream input)
    {
        byte[] buffer = new byte[16 * 1024];

        using MemoryStream ms = new();
        int chunk;

        while ((chunk = input.Read(buffer, 0, buffer.Length)) > 0)
        {
            ms.Write(buffer, 0, chunk);
        }

        return ms.ToArray();
    }
}
