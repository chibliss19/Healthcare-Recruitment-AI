using HealthcareRecruitment.Api.Services.Ai;
using Xunit;

namespace HealthcareRecruitment.Tests;

/// <summary>
/// a file of unit tests for the AiJsonRunner.StripCodeFence method, which is used to clean up JSON responses ensuring the backend only ever see clean data ensuring our backend works 
/// Tests the small parsing step that strips the markdown code fence some providers add around
/// a JSON response despite the system prompt explicitly asking them not to.
/// </summary>
public class StripCodeFenceTests
{
    [Fact]
    public void Leaves_plain_json_unchanged()
    {
        var input = "{\"a\":1}";

        Assert.Equal(input, AiJsonRunner.StripCodeFence(input));
    }

    [Fact]
    public void Strips_a_fence_labelled_json()
    {
        // The "json" label is stripped, but the newline that followed it in the source text is
        // not separately trimmed — this is the function's real behaviour, harmless in practice
        // since System.Text.Json ignores leading whitespace, and pinned here rather than assumed.
        var input = "```json\n{\"a\":1}\n```";

        Assert.Equal("\n{\"a\":1}", AiJsonRunner.StripCodeFence(input));
    }

    [Fact]
    public void Strips_a_plain_unlabelled_fence()
    {
        var input = "```\n{\"a\":1}\n```";

        Assert.Equal("{\"a\":1}", AiJsonRunner.StripCodeFence(input));
    }
}
