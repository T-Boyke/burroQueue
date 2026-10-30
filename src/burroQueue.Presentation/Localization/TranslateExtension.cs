using System.Windows.Data;
using System.Windows.Markup;

namespace burroQueue.Presentation.Localization;

[MarkupExtensionReturnType(typeof(BindingExpression))]
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public sealed class TranslateExtension : MarkupExtension
{
    public string Key { get; set; } = string.Empty;

    public TranslateExtension()
    {
    }

    public TranslateExtension(string key)
    {
        Key = key;
    }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        System.Windows.Data.Binding binding = new($"[{Key}]")
        {
            Source = LocalizationManager.Instance,
            Mode = BindingMode.OneWay
        };
        return binding.ProvideValue(serviceProvider);
    }
}

