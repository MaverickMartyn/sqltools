﻿using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using SqlTools.ClassExtensions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SqlTools.NaturalTextTaggers.CSharp
{
    internal class CSharpCommentTextTagger : ITagger<NaturalTextTag>, IDisposable
    {
        private readonly ITextBuffer buffer;

        private ITextSnapshot lineCacheSnapshot;

        private readonly List<State> lineCache;

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        public CSharpCommentTextTagger(ITextBuffer buffer)
        {
            this.buffer = buffer;
            ITextSnapshot currentSnapshot = this.buffer.CurrentSnapshot;
            lineCache = new List<State>(currentSnapshot.LineCount);
            lineCache.AddRange(Enumerable.Repeat(State.Default, currentSnapshot.LineCount));
            RescanLines(currentSnapshot, 0, currentSnapshot.LineCount - 1);
            lineCacheSnapshot = currentSnapshot;
            this.buffer.Changed += OnTextBufferChanged;
        }

        public void Dispose()
        {
            buffer.Changed -= OnTextBufferChanged;
        }

        public IEnumerable<ITagSpan<NaturalTextTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            foreach (SnapshotSpan span2 in spans)
            {
                SnapshotSpan span = span2;
                if (span.Snapshot != lineCacheSnapshot)
                {
                    yield break;
                }
                SnapshotPoint val = span.Start;
                while (val < span.End)
                {
                    ITextSnapshotLine line = val.GetContainingLine();
                    State state = State.Default;
                    if (line.LineNumber > 0)
                    {
                        // If previous line is multiline string, assume this is the same type, and then scan the line.
                        var prevLineState = lineCache[line.LineNumber - 1];
                        if (prevLineState.IsMultiLine())
                            state = prevLineState;
                    }

                    List<SnapshotSpan> list = new List<SnapshotSpan>();
                    ScanLine(state, line, list);
                    foreach (SnapshotSpan item in list)
                    {
                        SnapshotSpan current = item;
                        if (current.IntersectsWith(span))
                        {
                            yield return new TagSpan<NaturalTextTag>(current, new NaturalTextTag() { State = state });
                        }
                    }
                    val = line.EndIncludingLineBreak;
                }
            }
        }

        private void OnTextBufferChanged(object sender, TextContentChangedEventArgs e)
        {
            ITextSnapshot snapshot = e.After;
            foreach (ITextChange item in e.Changes)
            {
                if (item.LineCountDelta > 0)
                {
                    int lineNumber = snapshot.GetLineFromPosition(item.NewPosition).LineNumber;
                    lineCache.InsertRange(lineNumber, Enumerable.Repeat(State.Default, item.LineCountDelta));
                }
                else if (item.LineCountDelta < 0)
                {
                    int lineNumber2 = snapshot.GetLineFromPosition(item.NewPosition).LineNumber;
                    lineCache.RemoveRange(lineNumber2, -item.LineCountDelta);
                }
            }
            List<SnapshotSpan> list = (from change in e.Changes
                                       let startLine = snapshot.GetLineFromPosition(change.NewPosition)
                                       let endLine = snapshot.GetLineFromPosition(change.NewPosition)
                                       let lastUpdatedLine = RescanLines(snapshot, startLine.LineNumber, endLine.LineNumber)
                                       select new SnapshotSpan(startLine.Start, snapshot.GetLineFromLineNumber(lastUpdatedLine).End)).ToList();
            lineCacheSnapshot = snapshot;
            EventHandler<SnapshotSpanEventArgs> tagsChanged = TagsChanged;
            if (tagsChanged != null)
            {
                foreach (SnapshotSpan item2 in list)
                {
                    tagsChanged(this, (SnapshotSpanEventArgs)(object)new SnapshotSpanEventArgs(item2));
                }
            }
        }

        private int RescanLines(ITextSnapshot snapshot, int startLine, int lastDirtyLine)
        {
            int i = startLine;
            bool flag = true;
            State state = State.Default;
            if (i > 0)
            {
                // If previous line is multiline string, assume this is the same type, and then scan the line.
                var prevLineState = lineCache[i - 1];
                if (prevLineState.IsMultiLine())
                    state = prevLineState;
            }
            for (; i < lastDirtyLine || (flag && i < snapshot.LineCount); i++)
            {
                ITextSnapshotLine lineFromLineNumber = snapshot.GetLineFromLineNumber(i);
                state = ScanLine(state, lineFromLineNumber);
                if (i < snapshot.LineCount)
                {
                    flag = (state != lineCache[i]);
                    lineCache[i] = state;
                }
            }
            return i - 1;
        }

        private State ScanLine(State state, ITextSnapshotLine line, List<SnapshotSpan> naturalTextSpans = null)
        {
            LineProgress lineProgress = new LineProgress(line, state, naturalTextSpans);
            while (!lineProgress.EndOfLine)
            {
                if (lineProgress.State == State.Default)
                {
                    ScanDefault(lineProgress);
                }
                else if (lineProgress.State.IsMultiLine())
                {
                    ScanMultiLineString(lineProgress);
                }
            }
            return lineProgress.State;
        }

        private void ScanDefault(LineProgress p)
        {
            while (!p.EndOfLine)
            {
                if (p.Char() == '"' && p.NextChar() == '"' && p.NextNextChar() == '"')
                {
                    p.Advance(3);
                    p.State = State.RawString;
                    ScanMultiLineString(p);
                }
                else if (p.Char() == '@' && p.NextChar() == '"')
                {
                    p.Advance(2);
                    p.State = State.MultiLineString;
                    ScanMultiLineString(p);
                }
                else if (p.Char() == '"')
                {
                    p.Advance();
                    p.State = State.String;
                    ScanString(p);
                }
                else
                {
                    p.Advance();
                }
            }
        }

        private void ScanString(LineProgress p)
        {
            p.StartNaturalText();
            while (!p.EndOfLine)
            {
                if (p.Char() == '\\')
                {
                    p.Advance(2);
                    continue;
                }
                if (p.Char() == '"')
                {
                    p.EndNaturalText();
                    p.Advance();
                    p.State = State.Default;
                    return;
                }
                p.Advance();
            }
            p.EndNaturalText();
            p.State = State.Default;
        }

        private void ScanMultiLineString(LineProgress p)
        {
            p.StartNaturalText();
            while (!p.EndOfLine)
            {
                if (p.Char() == '"' && p.NextChar() == '"' && p.NextNextChar() == '"' && p.State == State.RawString)
                {
                    // End of the raw string.
                    p.EndNaturalText();
                    p.Advance(3);
                    p.State = State.Default;
                    return;
                }
                else if (p.Char() == '"' && p.NextChar() == '"' && p.State == State.MultiLineString)
                    p.Advance(2); // This is an escaped quote ("").
                else if (p.Char() == '"' && p.State != State.RawString)
                {
                    // End of string, unless it is a raw string, where quotes are allowed.
                    p.EndNaturalText();
                    p.Advance();
                    p.State = State.Default;
                    return;
                }
                else
                    p.Advance();
            }
            p.EndNaturalText();
        }
    }
}