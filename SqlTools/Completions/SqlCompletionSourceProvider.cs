using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;
using System.Collections.Generic;
using System.ComponentModel.Composition;

namespace SqlTools.Completions
{
    [Export(typeof(IAsyncCompletionSourceProvider))]
    [Name("sql keywords dictionary completion provider")]
    [ContentType("csharp")]
    public class SqlCompletionSourceProvider : IAsyncCompletionSourceProvider
    {
        readonly IDictionary<ITextView, IAsyncCompletionSource> cache = new Dictionary<ITextView, IAsyncCompletionSource>();

        [Import]
        private readonly SqlCatalog Catalog;

        [Import]
        private readonly ITextStructureNavigatorSelectorService StructureNavigatorSelector;

        public IAsyncCompletionSource GetOrCreate(ITextView textView)
        {
            if (cache.TryGetValue(textView, out var itemSource))
                return itemSource;

            var source = new SqlCompletionSource(Catalog, StructureNavigatorSelector); // opportunity to pass in MEF parts
            textView.Closed += (o, e) => cache.Remove(textView); // clean up memory as files are closed
            cache.Add(textView, source);
            return source;
        }
    }
}
