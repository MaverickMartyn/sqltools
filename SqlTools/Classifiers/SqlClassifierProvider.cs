using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using SqlTools.NaturalTextTaggers;
using System.ComponentModel.Composition;

namespace SqlTools.Classifiers
{
    [Export(typeof(IClassifierProvider))]
    [ContentType("csharp")]
    internal class SqlClassifierProvider : IClassifierProvider
    {
#pragma warning disable 649
        [Import]
        private readonly IClassificationTypeRegistryService ClassificationRegistry;

        [Import]
        private readonly IClassificationFormatMapService ClassificationFormatMapService;

        [Import]
        private readonly IBufferTagAggregatorFactoryService TagAggregatorFactory;

#pragma warning restore 649

        public IClassifier GetClassifier(ITextBuffer buffer)
        {
            var tagAggregator = TagAggregatorFactory.CreateTagAggregator<NaturalTextTag>(buffer);
            return new SqlClassifier(tagAggregator, ClassificationRegistry, ClassificationFormatMapService);
        }
    }
}
