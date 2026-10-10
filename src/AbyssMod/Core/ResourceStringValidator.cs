using System;
using MelonLoader.Preferences;

namespace AbyssMod;

/// <summary>资源配置在赋值和文件重载时统一规范化，无效输入回退默认值。</summary>
internal sealed class ResourceStringValidator : ValueValidator
{
    private readonly string _defaultValue;
    private readonly bool _httpUrl;

    internal ResourceStringValidator(string defaultValue, bool httpUrl = false)
    {
        _defaultValue = defaultValue;
        _httpUrl = httpUrl;
    }

    public override bool IsValid(object value) =>
        value is string text
        && text.Trim().Length > 0
        && (
            !_httpUrl
            || Uri.TryCreate(text.Trim(), UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
        );

    public override object EnsureValid(object value) =>
        IsValid(value) ? ((string)value).Trim() : _defaultValue;
}
