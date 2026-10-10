using Xunit;

namespace AbyssMod.Tests
{
    public sealed class ResourceStringValidatorTests
    {
        [Theory]
        [InlineData(" https://example.test/translations ", "https://example.test/translations")]
        [InlineData("http://localhost:12315", "http://localhost:12315")]
        [InlineData("ftp://example.test/translations", "https://default.test")]
        [InlineData("", "https://default.test")]
        [InlineData("   ", "https://default.test")]
        public void UrlInputIsTrimmedAndInvalidValuesUseTheDefault(string input, string expected)
        {
            var validator = new ResourceStringValidator("https://default.test", httpUrl: true);
            Assert.Equal(expected, validator.EnsureValid(input));
        }

        [Theory]
        [InlineData("  Game/font  ", "Game/font")]
        [InlineData("   ", "Game/default")]
        public void ResourcePathsKeepTheirLayoutAndCannotBeEmpty(string input, string expected)
        {
            var validator = new ResourceStringValidator("Game/default");
            Assert.Equal(expected, validator.EnsureValid(input));
        }
    }
}

namespace MelonLoader.Preferences
{
    // 只隔离加载器基类，验证执行实际配置规范化代码。
    public abstract class ValueValidator
    {
        public abstract bool IsValid(object value);
        public abstract object EnsureValid(object value);
    }
}
