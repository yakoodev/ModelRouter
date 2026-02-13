using MultiLlm.Core.Instructions;

namespace MultiLlm.Core.Tests;

public class InstructionLayersTests
{
    [Fact]
    public void OrderedByPriority_ReturnsRequestFirst()
    {
        var layers = new InstructionLayers(
            System: "system",
            Developer: "developer",
            Session: "session",
            Request: "request");

        var ordered = layers.OrderedByPriority().ToArray();

        Assert.Equal(["request", "session", "developer", "system"], ordered);
    }
}
