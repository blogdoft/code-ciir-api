using BlogDoFT.Libs.ResultPattern;
using Shouldly;

namespace CodeCiir.Application.Tests.Support;

internal static class ResultAssertions
{
    /// <summary>Asserts the result is a failure and that it carries exactly the expected failure (code and message).</summary>
    /// <param name="result">The result under test.</param>
    /// <param name="expected">The failure the result must carry.</param>
    /// <typeparam name="T">The success payload type of the result.</typeparam>
    public static void ShouldBeFailure<T>(this Result<T> result, Failure expected)
    {
        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldBe(expected.Code);
        result.Failure.Message.ShouldBe(expected.Message);
    }
}
