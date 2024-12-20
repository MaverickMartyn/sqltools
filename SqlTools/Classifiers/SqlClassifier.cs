﻿using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Tagging;
using SqlTools.ClassExtensions;
using SqlTools.NaturalTextTaggers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace SqlTools.Classifiers
{
    internal class SqlClassifier : IClassifier, IDisposable
    {
        //https://github.com/EWSoftware/VSSpellChecker/
        //https://github.com/fbdegroot/SqlSyntaxHighlighting
        private readonly char[] keywordPrefixCharacters = new[] { '\t', ' ', '"', '(' };
        private readonly char[] keywordPostfixCharacters = new[] { '\t', ' ', '"', ')', '(', ',', '\'', ';' };
        private readonly char[] functionPrefixCharacters = new[] { '\t', ' ', '"', ',', '(' };
        private readonly char[] functionPostfixCharacters = new[] { '\t', '(' };

        private readonly List<string> detects = new List<string>
        {
            "select", "insert", "delete", "update", "merge", "create", "alter", "drop", "exec", "execute", "from", "join", "where", "order by"
        };

        private readonly List<string> keywords = new List<string> {
            "select", "insert", "delete", "update",
            "into", "values", "truncate", "distinct", "with", "from",
            "union", "except", "intersect", "where", "of", "off", "rule",
            "group by", "order by", "asc", "desc", "over", "offsets", "limit",
            "on", "as", "go", "database", "allocate", "deallocate", "dump", "tsequal",
            "create", "alter", "drop", "add", "column", "constraint", "cascade", "identity", "check", "nocheck", "unique",
            "table", "function", "procedure", "index", "view", "schema", "trigger", "close", "percent", "plan",
            "declare", "set", "full", "collate", "varying", "nonclustered", "statistics", "national",
            "if", "begin", "else", "end", "for", "while", "goto", "break", "revert", "revoke", "browse",
            "case", "when", "then", "restrict", "kill", "load", "merge", "current",
            "transaction", "commit", "rollback", "external", "external", "raiserror", "file", "fillfactor", "read", "foreign",
            "reconfigure", "freetext", "authorization", "replication", "backup", "restore", "distributed",
            "exec", "execute", "return", "returns", "print", "use", "exit", "openxml", "openrowset", "openquery",
            "dbcc", "fetch", "open", "next", "inserted", "deleted", "print",
            "bigint", "numeric", "bit", "smallint", "decimal", "smallmoney", "int", "tinyint", "money", "float", "real",
            "date", "datetimeoffset", "datetime2", "smalldatetime", "datetime", "time", "timestamp",
            "char", "varchar", "text", "nchar", "nvarchar", "ntext", "grant", "deny",
            "binary", "varbinary", "image", "using",
            "cursor", "hierarchyid", "uniqueidentifier", "sql_variant", "xml",
            "pivot", "unpivot", "loop"
        };

        private readonly char[] operators = new char[]
        {
            '*', ',', '.', ';', '(', ')', '[', ']', '=', '>', '<', '+', '-', '/', '%', '~', '&', '^', '|', '\'', ':'
        };

        private readonly List<string> logicals = new List<string>
        {
            "join", "inner join", "outer join", "left outer join", "right outer join", "left join", "right join", "cross join", "and", "or", "is", "null", "not", "in", "like", "between", "having", "exists", "all", "any", "some"
        };


        private readonly List<string> functions = new List<string> {
            "top", "count", "count_big", "sum", "min", "max", "avg", "row_number",
            "abs", "degrees", "rand", "acos", "exp", "round", "asin", "floor", "sign", "atan", "log", "sin", "atan2", "log10", "sqrt", "ceiling", "pi", "square", "cos", "power", "tan", "cot", "radians",
            "newid", "isnull", "coalesce", "choose", "iif",
            "left", "right", "substring", "trim", "ltrim", "rtrim", "upper", "lower", "charindex", "len", "stuff", "format", "unicode",
            "reverse", "soundex", "quotename", "patindex","string_agg", "string_split", "string_escape", "str", "concat", "concat_ws",
            "space", "translate", "ascii", "replicate", "difference",
            "getdate", "getutcdate", "isdate", "dateadd", "datediff", "datepart", "datename", "eomonth", "smalldatetimefromparts", "timefromparts", "sysutcdatetime", "switchoffset",
            "convert", "cast", "parse", "try_convert", "try_cast", "try_parse",
            "json", "json_value", "json_query", "json_modify",
            "@@datefirst", "@@cursor_rows", "@@fetch_status", "@@dbts", "@@langid", "@@language", "@@lock_timeout", "@@max_connections", "@@max_precision", "@@nestlevel", "@@options", "@@remserver", "@@servername", "@@servicename", "@@textsize", "@@version"
        };

        private readonly Regex variables = new Regex(@"(?:^|[""\s(+,=])(?<Variable>@[a-z0-9_]+)(?:$|[""\s)+,;])", RegexOptions.IgnoreCase | RegexOptions.Multiline);

        private readonly Regex defines = new Regex(@"(?:[.:])(?<Variable>[a-z0-9_]+)(?:\s*\()", RegexOptions.IgnoreCase | RegexOptions.Multiline);

        private readonly Regex literals = new Regex(@"(?:^|[""\s(+,=-])(?<Variable>[0-9_.,]+)(?:$|[""\s)+,;])", RegexOptions.IgnoreCase | RegexOptions.Multiline);
        private readonly Regex stringLiterals = new Regex(@"(?:^|[""\s(+,=\w])(?<Variable>'.*?')(?:$|[""\s)+,;])", RegexOptions.IgnoreCase | RegexOptions.Multiline);

        private readonly Regex comments = new Regex(@"(?<Variable>\-\-\s*.+)$", RegexOptions.IgnoreCase | RegexOptions.Multiline);
        private readonly Regex alternateComments = new Regex(@"(?<Variable>\/\*\s*(.+?)\*\/)", RegexOptions.IgnoreCase | RegexOptions.Multiline);
        private readonly Regex multilineCommentBegin = new Regex(@"(?<Variable>\/\*\s*(.+?)((?:\*\/)|$))", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        private readonly Regex multilineCommentEnd = new Regex(@"(?<Variable>(.+?)\*\/)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        private readonly Regex inCommentBegin = new Regex(@"(?:\/\*.+?)", RegexOptions.IgnoreCase | RegexOptions.Multiline);
        private readonly Regex inCommentEnd = new Regex(@"\*\/", RegexOptions.IgnoreCase | RegexOptions.Multiline);
        private readonly Regex userFunctions = new Regex(@"(?<Variable>\w+)\(", RegexOptions.IgnoreCase | RegexOptions.Singleline);

        private readonly IClassificationType keywordType;
        private readonly IClassificationType operatorType;
        private readonly IClassificationType functionType;
        private readonly IClassificationType variableType;
        private readonly IClassificationType literalType;
        private readonly IClassificationType stringLiteralType;
        private readonly IClassificationType commentType;
        private readonly IClassificationType definedType;
        private readonly IClassificationType workflowType;
        readonly ITagAggregator<NaturalTextTag> _tagger;
        readonly IClassificationFormatMapService _service;

        internal SqlClassifier(ITagAggregator<NaturalTextTag> tagger, IClassificationTypeRegistryService registry, IClassificationFormatMapService format)
        {
            _tagger = tagger;
            _service = format;
            keywordType = registry.GetClassificationType("Sql-Keyword");
            operatorType = registry.GetClassificationType("Sql-Operator");
            functionType = registry.GetClassificationType("Sql-Function");
            variableType = registry.GetClassificationType("Sql-Variable");
            literalType = registry.GetClassificationType("Sql-Literal");
            stringLiteralType = registry.GetClassificationType("Sql-StringLiteral");
            commentType = registry.GetClassificationType("Sql-Comment");
            definedType = registry.GetClassificationType("Sql-Defined");
            workflowType = registry.GetClassificationType("Sql-Workflow");
        }

#pragma warning disable 67
        public event EventHandler<ClassificationChangedEventArgs> ClassificationChanged;
#pragma warning restore 67

        public IList<ClassificationSpan> GetClassificationSpans(SnapshotSpan span)
        {
            IList<ClassificationSpan> classifiedSpans = new List<ClassificationSpan>();

            foreach (IMappingTagSpan<NaturalTextTag> tagSpan in _tagger.GetTags(span).ToList())
            {
                bool isMultiLineComment = IsInMultiLineComment(span, tagSpan);
                SnapshotSpan snapshot = tagSpan.Span.GetSpans(span.Snapshot).First();

                string text = snapshot.GetText().ToLowerInvariant();

                int index = -1;

                // If string literal is not multiline, check if it contains certain words before before classifying.
                if (!tagSpan.Tag.State.IsMultiLine())
                {
                    bool detected = false;
                    foreach (var detect in detects)
                    {
                        while (snapshot.Length > index + 1 && (index = text.IndexOf(detect, index + 1)) > -1)
                        {
                            //classifiedSpans.Add(new ClassificationSpan(new SnapshotSpan(snapshot.Start, text.Length), literalType));
                            detected = true;
                        }
                    }

                    if (!detected) return classifiedSpans;
                }
                //else
                //    classifiedSpans.Add(new ClassificationSpan(new SnapshotSpan(snapshot.Start, text.Length), literalType));

                // multi-line comments
                var mlcBeginMatch = multilineCommentBegin.Match(text);
                var mlcEndMatch = multilineCommentEnd.Match(text);
                if (mlcBeginMatch.Success)
                {
                    if (!mlcBeginMatch.Groups["Variable"].Value.EndsWith("*/"))
                        isMultiLineComment = true;
                    classifiedSpans.Add(new ClassificationSpan(new SnapshotSpan(snapshot.Start + mlcBeginMatch.Groups["Variable"].Index, mlcBeginMatch.Groups["Variable"].Length), commentType));
                }
                else if (mlcEndMatch.Success)
                {
                    classifiedSpans.Add(new ClassificationSpan(new SnapshotSpan(snapshot.Start + mlcEndMatch.Groups["Variable"].Index, mlcEndMatch.Groups["Variable"].Length), commentType));
                    isMultiLineComment = false;
                }
                else if (isMultiLineComment)
                    classifiedSpans.Add(new ClassificationSpan(new SnapshotSpan(snapshot.Start, snapshot.Length), commentType));

                if (isMultiLineComment)
                    continue;

                // comments
                ClassifyCommentsMatches(comments.Matches(text), classifiedSpans, snapshot);
                ClassifyCommentsMatches(alternateComments.Matches(text), classifiedSpans, snapshot);

                // keywords
                foreach (string keyword in keywords)
                {
                    while (snapshot.Length > index + 1 && (index = text.IndexOf(keyword, index + 1)) > -1)
                    {
                        if ((index > 0 && !keywordPrefixCharacters.Contains(text[index - 1])) ||
                            (index + keyword.Length < text.Length && !keywordPostfixCharacters.Contains(text[index + keyword.Length])))
                            continue;

                        classifiedSpans.Add(new ClassificationSpan(new SnapshotSpan(snapshot.Start + index, keyword.Length), keywordType));
                    }
                }

                // operators
                foreach (string logical in logicals)
                {
                    while (snapshot.Length > index + 1 && (index = text.IndexOf(logical, index + 1)) > -1)
                    {
                        if ((index > 0 && !keywordPrefixCharacters.Contains(text[index - 1])) ||
                           (index + logical.Length < text.Length && keywordPostfixCharacters.Contains(text[index + logical.Length]) == false))
                            continue;
                        classifiedSpans.Add(new ClassificationSpan(new SnapshotSpan(snapshot.Start + index, logical.Length), operatorType));
                    }
                }

                // functions
                foreach (string function in functions)
                {
                    while (snapshot.Length > index + 1 && (index = text.IndexOf(function, index + 1)) > -1)
                    {
                        if ((index > 0 && !functionPrefixCharacters.Contains(text[index - 1])) ||
                            (index + function.Length < text.Length && functionPostfixCharacters.Contains(text[index + function.Length]) == false))
                            continue;

                        //if ((index > 0 && text.Substring(0, index - 1).IndexOf('{') > 0) || (index + function.Length < text.Length && text.Substring(index + function.Length).IndexOf('}') > 0))
                        //    continue;

                        classifiedSpans.Add(new ClassificationSpan(new SnapshotSpan(snapshot.Start + index, function.Length), functionType));
                    }
                }

                foreach (Match match in userFunctions.Matches(text))
                {
                    classifiedSpans.Add(new ClassificationSpan(new SnapshotSpan(snapshot.Start + match.Groups["Variable"].Index, match.Groups["Variable"].Length), functionType));
                }

                // variables
                foreach (Match match in variables.Matches(text))
                    classifiedSpans.Add(new ClassificationSpan(new SnapshotSpan(snapshot.Start + match.Groups["Variable"].Index, match.Groups["Variable"].Length), variableType));

                // user defined
                foreach (Match match in defines.Matches(text))
                    classifiedSpans.Add(new ClassificationSpan(new SnapshotSpan(snapshot.Start + match.Groups["Variable"].Index, match.Groups["Variable"].Length), definedType));

                // literals
                foreach (Match match in literals.Matches(text))
                {
                    classifiedSpans.Add(new ClassificationSpan(new SnapshotSpan(snapshot.Start + match.Groups["Variable"].Index, match.Groups["Variable"].Length), literalType));
                }

                // string literals
                foreach (Match match in stringLiterals.Matches(text))
                {
                    classifiedSpans.Add(new ClassificationSpan(new SnapshotSpan(snapshot.Start + match.Groups["Variable"].Index, match.Groups["Variable"].Length), stringLiteralType));
                }

                // operators
                foreach (char op in operators)
                {
                    while (snapshot.Length > index + 1 && (index = text.IndexOf(op, index + 1)) > -1)
                    {
                        classifiedSpans.Add(new ClassificationSpan(new SnapshotSpan(snapshot.Start + index, 1), operatorType));
                    }
                }
            }

            return RemoveSpansInsideCommentsAndOutsideSnapshotSpan(classifiedSpans, span);
        }

        private IList<ClassificationSpan> RemoveSpansInsideCommentsAndOutsideSnapshotSpan(IList<ClassificationSpan> classifiedSpans, SnapshotSpan snapshotSpan)
        {
            var comments = classifiedSpans.Where(cs => cs.ClassificationType == commentType);
            var others = classifiedSpans.Except(comments);
            return classifiedSpans.Where(cs =>
                cs.Span.OverlapsWith(snapshotSpan)
                && (cs.ClassificationType == commentType || !comments.Any(c => c.Span.OverlapsWith(cs.Span)))).ToList();
        }

        private bool IsInMultiLineComment(SnapshotSpan span, IMappingTagSpan<NaturalTextTag> tagSpan)
        {
            // Analyze the text before the span
            // Logic to determine if the text ends with an open multi-line comment
            // Compares the number of comment opening-tags, vs the number of closing-tags.

            if (!tagSpan.Tag.State.IsMultiLine())
                return false; // The string literal is not multiline, so neither will any SQL comments be.
            
            // Grab the starting position of the string literal and grab the preceeding text.
            var firstTag = _tagger.GetTags(new SnapshotSpan(span.Snapshot, 0, span.Start.Position))
                .OrderByDescending(t => t.Span.Start.GetPoint(span.Snapshot.TextBuffer, PositionAffinity.Successor).Value.Position).TakeWhile(t => t.Tag.State == tagSpan.Tag.State).LastOrDefault() ?? tagSpan;
            var beginning = firstTag.Span.Start.GetPoint(span.Snapshot.TextBuffer, PositionAffinity.Successor).Value.Position;
            var currentPosition = tagSpan.Span.Start.GetPoint(span.Snapshot.TextBuffer, PositionAffinity.Successor).Value.Position;
            var preceedingText = span.Snapshot.GetText(beginning, currentPosition - beginning);

            return inCommentBegin.Matches(preceedingText).Count > inCommentEnd.Matches(preceedingText).Count;
        }

        private void ClassifyCommentsMatches(MatchCollection matchCollection, IList<ClassificationSpan> classifiedSpans, SnapshotSpan snapshot)
        {
            foreach (Match match in matchCollection)
            {
                classifiedSpans.Add(new ClassificationSpan(new SnapshotSpan(snapshot.Start + match.Groups["Variable"].Index, match.Groups["Variable"].Length), commentType));
            }
        }

        void IDisposable.Dispose()
        {
        }

    }
}
