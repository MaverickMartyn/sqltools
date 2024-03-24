using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using SqlTools.NaturalTextTaggers.CSharp;
using System;
using System.ComponentModel.Composition;

namespace SqlTools.NaturalTextTaggers
{
    [Export(typeof(ITaggerProvider))]
    [ContentType("CSharp")]
    [TagType(typeof(NaturalTextTag))]
    internal class CommentTextTaggerProvider : ITaggerProvider
    {
        [Import]
        internal IClassifierAggregatorService ClassifierAggregatorService { get; set; }

        [Import]
        internal IBufferTagAggregatorFactoryService TagAggregatorFactory { get; set; }

        public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
        {
            if (buffer == null)
                throw new ArgumentNullException("buffer");

            // Due to an issue with the built-in C# classifier, we avoid using it.
            if (buffer.ContentType.IsOfType("csharp"))
                return new CSharpCommentTextTagger(buffer) as ITagger<T>;

            var classifierAggregator = ClassifierAggregatorService.GetClassifier(buffer);

            return new CommentTextTagger(buffer, classifierAggregator) as ITagger<T>;
        }
    }
}
