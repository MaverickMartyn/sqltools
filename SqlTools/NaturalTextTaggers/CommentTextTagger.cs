using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Tagging;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace SqlTools.NaturalTextTaggers
{
    internal class CommentTextTagger : ITagger<NaturalTextTag>, IDisposable
    {
        readonly ITextBuffer buffer;
        readonly IClassifier classifier;

        public CommentTextTagger(ITextBuffer buffer, IClassifier classifier)
        {
            this.buffer = buffer;
            this.classifier = classifier;

            classifier.ClassificationChanged += ClassificationChanged;
        }

        public IEnumerable<ITagSpan<NaturalTextTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            if (classifier == null || spans == null || spans.Count == 0)
                yield break;

            foreach (var snapshotSpan in spans)
            {
                Debug.Assert(snapshotSpan.Snapshot.TextBuffer == buffer);
                foreach (ClassificationSpan classificationSpan in classifier.GetClassificationSpans(snapshotSpan))
                {
                    string name = classificationSpan.ClassificationType.Classification.ToLowerInvariant();

                    if (name.Contains("string") && name.Contains("xml doc tag") == false)
                    {
                        yield return new TagSpan<NaturalTextTag>(classificationSpan.Span, new NaturalTextTag());
                    }
                }
            }
        }

        void ClassificationChanged(object sender, ClassificationChangedEventArgs e)
        {
            TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(e.ChangeSpan));
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        public void Dispose()
        {
            if (classifier != null)
                classifier.ClassificationChanged -= ClassificationChanged;
        }
    }
}
