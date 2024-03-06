using Avalonia.Data.Core;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Markup.Xaml.MarkupExtensions.CompiledBindings;
using System;

namespace Ryujinx.Ava.Common.Locale
{
    internal class LocaleExtension : MarkupExtension
    {
        public LocaleExtension(LocaleKeys key)
        {
            Key = key;
        }

        public LocaleKeys Key { get; }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            LocaleKeys keyToUse = Key;

            var builder = new CompiledBindingPathBuilder();

            builder.SetRawSource(LocaleManager.Instance)
                .Property(new ClrPropertyInfo("Item",
                obj => (LocaleManager.Instance[keyToUse]),
                null,
                typeof(string)), (weakRef, iPropInfo) =>
                {
                    return PropertyInfoAccessorFactory.CreateInpcPropertyAccessor(weakRef, iPropInfo);
                });

            var path = builder.Build();

            var binding = new CompiledBindingExtension(path);

            return binding.ProvideValue(serviceProvider);
        }
    }
}
