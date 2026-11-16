using System.Xml.Linq;
using Msmq.NetCore.Messaging;

namespace Msmq.NetCore.Test.Tests;

public class MessageTests
{
    [Fact]
    public void MessageTest()
    {
        using var q = new Message();
        Assert.NotNull(q);
        Assert.IsType<Message>(q);
    }

    [Fact]
    public void MessageTestBody()
    {
        const string messageBody = "Whatever";
        using var q = new Message(messageBody);
        Assert.NotNull(q);
        Assert.IsType<Message>(q);
        Assert.True((string)q.Body == messageBody);
    }

    [Fact]
    public void MessageTestBodyFormatter()
    {
        XElement xmlBody = new("Root",
            new XElement("Child1", 1),
            new XElement("Child2", 2),
            new XElement("Child3", 3),
            new XElement("Child4", 4),
            new XElement("Child5", 5),
            new XElement("Child6", 6)
        );
        using var q = new Message(xmlBody, new XmlMessageFormatter());
        Assert.NotNull(q);
        Assert.IsType<Message>(q);
    }

    [Fact]
    public void MessageTest_BodyWithEmptyString()
    {
        using var msg = new Message(string.Empty);
        Assert.NotNull(msg);
        Assert.Equal(string.Empty, (string)msg.Body);
    }

    [Fact]
    public void MessageTest_BodyWithLongString()
    {
        string longBody = new('A', 10000);
        using var msg = new Message(longBody);
        Assert.NotNull(msg);
        Assert.Equal(longBody, (string)msg.Body);
    }

    [Fact]
    public void MessageTest_BodyWithSpecialCharacters()
    {
        const string body = "Special chars: é à ü ñ 中文 🎉";
        using var msg = new Message(body);
        Assert.NotNull(msg);
        Assert.Equal(body, (string)msg.Body);
    }

    [Fact]
    public void MessageTest_WithLabel()
    {
        using var msg = new Message("Body")
        {
            Label = "Test Label"
        };
        Assert.Equal("Test Label", msg.Label);
    }

    [Fact]
    public void MessageTest_WithPriority()
    {
        using var msg = new Message("Body")
        {
            Priority = MessagePriority.High
        };
        Assert.Equal(MessagePriority.High, msg.Priority);
    }

    [Fact]
    public void MessageTest_WithTimeToBeReceived()
    {
        using var msg = new Message("Body");
        var timeToBeReceived = TimeSpan.FromMinutes(5);
        msg.TimeToBeReceived = timeToBeReceived;
        Assert.Equal(timeToBeReceived, msg.TimeToBeReceived);
    }

    [Fact]
    public void MessageTest_IdIsEmptyBeforeSend()
    {
        using var msg = new Message("Test Body");
        Assert.Equal("00000000-0000-0000-0000-000000000000\\0", msg.Id);
    }

    [Fact]
    public void MessageTest_SentTimeIsUnavailableBeforeSend()
    {
        using var msg = new Message("Test Body");
        Assert.Throws<InvalidOperationException>(() => msg.SentTime);
    }

    [Fact]
    public void MessageTest_AcknowledgementType()
    {
        using var msg = new Message("Body")
        {
            AcknowledgeType = AcknowledgeTypes.PositiveArrival | AcknowledgeTypes.PositiveReceive
        };
        Assert.Equal(AcknowledgeTypes.PositiveArrival | AcknowledgeTypes.PositiveReceive, msg.AcknowledgeType);
    }

    [Fact]
    public void MessageTest_MultipleMessagesWithDifferentBodies()
    {
        string[] bodies = ["Body1", "Body2", "Body3"];
        var messages = new List<Message>();

        try
        {
            foreach (string body in bodies)
            {
                messages.Add(new Message(body));
            }

            Assert.Equal(3, messages.Count);
            for (int i = 0; i < bodies.Length; i++)
            {
                Assert.Equal(bodies[i], (string)messages[i].Body);
            }
        }
        finally
        {
            foreach (var msg in messages)
            {
                msg?.Dispose();
            }
        }
    }

    [Fact]
    public void MessageTest_WrongFormatterType()
    {
        var xmlBody = new XElement("Root", new XElement("Item", "value"));
        using var msg = new Message(xmlBody, new XmlMessageFormatter());
        Assert.NotNull(msg);
        // The formatter will be used at deserialization time
    }

    [Fact]
    public void MessageTest_NullBody()
    {
        using var msg = new Message((object)null);
        Assert.NotNull(msg);
        // Null body is valid in MSMQ
    }

    [Fact]
    public void MessageTest_ObjectBody()
    {
        var person = TestCommon.CreateTestPerson();
        using var msg = new Message(person, new XmlMessageFormatter());
        Assert.NotNull(msg);
        Assert.IsType<Message>(msg);
    }
}
