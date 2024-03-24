using Microsoft.VisualStudio.Text;
using System.Collections.Generic;

namespace SqlTools.NaturalTextTaggers.CSharp
{
    // <summary>
    /// This class is used to track line progress while parsing C# code for natural text regions
    /// </summary>
    /// <remarks>This is used in place of the normal classifier to work around issues in how the default
    /// classifier works.</remarks>
    class LineProgress
    {
        private readonly ITextSnapshotLine snapshotLine;

        private readonly List<SnapshotSpan> naturalTextSpans;

        private readonly string lineText;

        private int naturalTextStart = -1;

        private int linePosition;

        public State State
        {
            get;
            set;
        }

        public bool EndOfLine => linePosition >= snapshotLine.Length;

        public LineProgress(ITextSnapshotLine line, State state, List<SnapshotSpan> naturalTextSpans)
        {
            snapshotLine = line;
            lineText = line.GetText();
            linePosition = 0;
            this.naturalTextSpans = naturalTextSpans;
            State = state;
        }

        public char Char()
        {
            return lineText[linePosition];
        }

        public char NextChar()
        {
            if (linePosition >= snapshotLine.Length - 1)
            {
                return '\0';
            }
            return lineText[linePosition + 1];
        }

        public char NextNextChar()
        {
            if (linePosition >= snapshotLine.Length - 2)
            {
                return '\0';
            }
            return lineText[linePosition + 2];
        }

        public void Advance(int count = 1)
        {
            linePosition += count;
        }

        public void AdvanceToEndOfLine()
        {
            linePosition = snapshotLine.Length;
        }

        public void StartNaturalText()
        {
            naturalTextStart = linePosition;
        }

        public void EndNaturalText()
        {
            if (naturalTextSpans != null && linePosition > naturalTextStart)
            {
                naturalTextSpans.Add(new SnapshotSpan(snapshotLine.Start + naturalTextStart, linePosition - naturalTextStart));
            }
            naturalTextStart = -1;
        }
    }
}