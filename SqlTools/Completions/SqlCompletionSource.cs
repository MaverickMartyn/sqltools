﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.Core.Imaging;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using SqlTools.Options;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace SqlTools.Completions
{
    public class SqlCompletionSource : IAsyncCompletionSource
    {
        private SqlCatalog Catalog { get; }
        private ITextStructureNavigatorSelectorService StructureNavigatorSelector { get; }

        //https://github.com/microsoft/VSSDK-Extensibility-Samples
        //http://glyphlist.azurewebsites.net/knownmonikers/
        //https://docs.microsoft.com/en-us/dotnet/api/microsoft.visualstudio.imaging.knownimageids.operator?view=visualstudiosdk-2022
        // ImageElements may be shared by CompletionFilters and CompletionItems. The automationName parameter should be localized.
        private static readonly ImageElement KeywordIcon = new ImageElement(new ImageId(new Guid("ae27a6b0-e345-4288-96df-5eaf394ee369"), 1589), "Keyword");
        private static readonly ImageElement FunctionIcon = new ImageElement(new ImageId(new Guid("ae27a6b0-e345-4288-96df-5eaf394ee369"), 1913), "Function");
        private static readonly ImageElement OperatorIcon = new ImageElement(new ImageId(new Guid("ae27a6b0-e345-4288-96df-5eaf394ee369"), 2174), "Operator");
        private static readonly ImageElement VariableIcon = new ImageElement(new ImageId(new Guid("ae27a6b0-e345-4288-96df-5eaf394ee369"), 1747), "Variable");
        private static readonly ImageElement DataTypeIcon = new ImageElement(new ImageId(new Guid("ae27a6b0-e345-4288-96df-5eaf394ee369"), 616), "DataType");
        private static readonly ImageElement TableIcon = new ImageElement(new ImageId(new Guid("ae27a6b0-e345-4288-96df-5eaf394ee369"), 3032), "Table");
        private static readonly ImageElement UnknownIcon = new ImageElement(new ImageId(new Guid("ae27a6b0-e345-4288-96df-5eaf394ee369"), 2025), "Unknown");

        // CompletionFilters are rendered in the UI as buttons
        // The displayText should be localized. Alt + Access Key toggles the filter button.
        private static readonly CompletionFilter KeywordFilter = new CompletionFilter("Keyword", "K", KeywordIcon);
        private static readonly CompletionFilter FunctionFilter = new CompletionFilter("Function", "F", FunctionIcon);
        private static readonly CompletionFilter OperatorFilter = new CompletionFilter("Operator", "O", OperatorIcon);
        private static readonly CompletionFilter VariableFilter = new CompletionFilter("Variable", "V", VariableIcon);
        private static readonly CompletionFilter DataTypeFilter = new CompletionFilter("DataType", "D", DataTypeIcon);
        private static readonly CompletionFilter UnknownFilter = new CompletionFilter("Unknown", "U", UnknownIcon);

        // CompletionItem takes array of CompletionFilters.
        // In this example, items assigned "MetalloidFilters" are visible in the list if user selects either MetalFilter or NonMetalFilter.
        private static readonly ImmutableArray<CompletionFilter> KeywordFilters = ImmutableArray.Create(KeywordFilter);
        private static readonly ImmutableArray<CompletionFilter> FunctionFilters = ImmutableArray.Create(FunctionFilter);
        private static readonly ImmutableArray<CompletionFilter> OperatorFilters = ImmutableArray.Create(OperatorFilter);
        private static readonly ImmutableArray<CompletionFilter> VariableFilters = ImmutableArray.Create(VariableFilter);
        private static readonly ImmutableArray<CompletionFilter> DataTypeFilters = ImmutableArray.Create(DataTypeFilter);
        private static readonly ImmutableArray<CompletionFilter> UnknownFilters = ImmutableArray.Create(UnknownFilter);

        private static readonly ImmutableArray<string> detects = ImmutableArray.Create(new string[] { "select", "insert", "delete", "update", "create", "alter", "drop", "exec", "execute", "from", "join", "where", "group", " order" });
        private SqlToolsOptionPageGrid _optionsDialog;

        public SqlCompletionSource(SqlCatalog catalog, ITextStructureNavigatorSelectorService structureNavigatorSelector)
        {
            Catalog = catalog;
            StructureNavigatorSelector = structureNavigatorSelector;
        }

        public CompletionStartData InitializeCompletion(CompletionTrigger trigger, SnapshotPoint triggerLocation, CancellationToken token)
        {
            // We don't trigger completion when user typed
            if (char.IsNumber(trigger.Character)         // a number
                || char.IsPunctuation(trigger.Character) // punctuation
                || trigger.Character == '\n'             // new line
                                                         //|| trigger.Reason == CompletionTriggerReason.Backspace
                                                         //|| trigger.Reason == CompletionTriggerReason.Deletion
                )
            {
                return CompletionStartData.DoesNotParticipateInCompletion;
            }

            // We participate in completion and provide the "applicable to span".
            // This span is used:
            // 1. To search (filter) the list of all completion items
            // 2. To highlight (bold) the matching part of the completion items
            // 3. In standard cases, it is replaced by content of completion item upon commit.

            // If you want to extend a language which already has completion, don't provide a span, e.g.
            // return CompletionStartData.ParticipatesInCompletionIfAny

            // If you provide a language, but don't have any items available at this location,
            // consider providing a span for extenders who can't parse the codem e.g.
            // return CompletionStartData(CompletionParticipation.DoesNotProvideItems, spanForOtherExtensions);

            var tokenSpan = FindTokenSpanAtPosition(triggerLocation);
            return new CompletionStartData(CompletionParticipation.ProvidesItems, tokenSpan);
        }

        private SnapshotSpan FindTokenSpanAtPosition(SnapshotPoint triggerLocation)
        {
            // This method is not really related to completion,
            // we mostly work with the default implementation of ITextStructureNavigator 
            // You will likely use the parser of your language
            ITextStructureNavigator navigator = StructureNavigatorSelector.GetTextStructureNavigator(triggerLocation.Snapshot.TextBuffer);
            TextExtent extent = navigator.GetExtentOfWord(triggerLocation);
            if (triggerLocation.Position > 0 && (!extent.IsSignificant || !extent.Span.GetText().Any(c => char.IsLetterOrDigit(c))))
            {
                // Improves span detection over the default ITextStructureNavigation result
                extent = navigator.GetExtentOfWord(triggerLocation - 1);
            }

            var tokenSpan = triggerLocation.Snapshot.CreateTrackingSpan(extent.Span, SpanTrackingMode.EdgeInclusive);

            var snapshot = triggerLocation.Snapshot;
            var tokenText = tokenSpan.GetText(snapshot);
            if (string.IsNullOrWhiteSpace(tokenText))
            {
                // The token at this location is empty. Return an empty span, which will grow as user types.
                return new SnapshotSpan(triggerLocation, 0);
            }

            // Trim quotes and new line characters.
            int startOffset = 0;
            int endOffset = 0;

            if (tokenText.Length > 0)
            {
                if (tokenText.StartsWith("\""))
                    startOffset = 1;
            }
            if (tokenText.Length - startOffset > 0)
            {
                if (tokenText.EndsWith("\"\r\n"))
                    endOffset = 3;
                else if (tokenText.EndsWith("\r\n"))
                    endOffset = 2;
                else if (tokenText.EndsWith("\"\n"))
                    endOffset = 2;
                else if (tokenText.EndsWith("\n"))
                    endOffset = 1;
                else if (tokenText.EndsWith("\""))
                    endOffset = 1;
            }

            return new SnapshotSpan(tokenSpan.GetStartPoint(snapshot) + startOffset, tokenSpan.GetEndPoint(snapshot) - endOffset);
        }

        public async Task<CompletionContext> GetCompletionContextAsync(IAsyncCompletionSession session, CompletionTrigger trigger, SnapshotPoint triggerLocation, SnapshotSpan applicableToSpan, CancellationToken token)
        {
            if (_optionsDialog is null)
            {
                var package = await AsyncServiceProvider.GlobalProvider.GetServiceAsync(typeof(SqlToolsPackage)) as SqlToolsPackage;
                _optionsDialog = package.GetDialogPage(typeof(SqlToolsOptionPageGrid)) as SqlToolsOptionPageGrid;
            }

            // TODO: Add check to avoid autocomplete in SQL comments and member names.

            if (!_optionsDialog.EnableAutoCompleteSuggestions || !IsCaretInsideStringLiteral(session.TextView, session.TextView.TextBuffer))
            {
                return await Task.FromResult(new CompletionContext(new List<CompletionItem>().ToImmutableArray()));
            }

            // See whether we are in the key or value portion of the pair
            var lineStart = triggerLocation.GetContainingLine().Start;

            var spanBeforeCaret = new SnapshotSpan(lineStart, triggerLocation);
            var textBeforeCaret = triggerLocation.Snapshot.GetText(spanBeforeCaret);

            var colonIndex = textBeforeCaret.IndexOf(':');
            var colonExistsBeforeCaret = colonIndex != -1;

            // User is likely in the key portion of the pair
            if (!colonExistsBeforeCaret)
                return await Task.FromResult(GetContextForKey());

            // User is likely in the value portion of the pair. Try to provide extra items based on the key.
            var KeyExtractingRegex = new Regex(@"^""[\W*|\s*](\w+)[\W*|\s*]$");
            var key = KeyExtractingRegex.Match(textBeforeCaret);
            var candidateName = key.Success ? key.Groups.Count > 0 && key.Groups[1].Success ? key.Groups[1].Value : string.Empty : string.Empty;
            return await Task.FromResult(GetContextForValue(candidateName));
        }

        private bool IsCaretInsideStringLiteral(ITextView textView, ITextBuffer textBuffer)
        {
            SnapshotPoint caretPosition = textView.Caret.Position.BufferPosition;

            // Get the syntax tree of the current document
            if (!textBuffer.Properties.TryGetProperty(typeof(SyntaxTree), out SyntaxTree syntaxTree))
            {
                // If the syntax tree is not available, create and cache it
                syntaxTree = CSharpSyntaxTree.ParseText(textBuffer.CurrentSnapshot.GetText());
                if (syntaxTree == null)
                {
                    // Parsing failed, cannot determine if caret is inside string literal
                    return false;
                }
                // Cache the syntax tree
                textBuffer.Properties.AddProperty(typeof(SyntaxTree), syntaxTree);
            }

            // Get the root node of the syntax tree
            SyntaxNode root = syntaxTree.GetRoot();

            // Find the token at the caret position
            SyntaxToken token = root.FindToken(caretPosition);

            // Check if the token is a string literal token
            if (token.IsKind(SyntaxKind.StringLiteralToken)
                || token.IsKind(SyntaxKind.InterpolatedStringToken)
                || token.IsKind(SyntaxKind.InterpolatedStringTextToken)
                || token.IsKind(SyntaxKind.SingleLineRawStringLiteralToken)
                || token.IsKind(SyntaxKind.MultiLineRawStringLiteralToken)
                || token.IsKind(SyntaxKind.Utf8StringLiteralToken)
                || token.IsKind(SyntaxKind.Utf8SingleLineRawStringLiteralToken)
                || token.IsKind(SyntaxKind.Utf8MultiLineRawStringLiteralToken))
            {
                // Get the parent node of the token
                SyntaxNode parentNode = token.Parent;

                // Check if the parent node is a string literal expression
                if (parentNode.IsKind(SyntaxKind.InterpolatedStringText)
                    || (parentNode is LiteralExpressionSyntax literalExpression &&
                    (literalExpression.IsKind(SyntaxKind.StringLiteralExpression) ||
                     literalExpression.IsKind(SyntaxKind.InterpolatedStringExpression) ||
                     literalExpression.IsKind(SyntaxKind.Utf8StringLiteralExpression))))
                {
                    // The caret is inside a string literal
                    return true;
                }
            }

            // The caret is not inside a string literal
            return false;
        }

        /// <summary>
        /// Returns completion items applicable to the value portion of the key-value pair
        /// </summary>
        private CompletionContext GetContextForValue(string key)
        {
            // Provide a few items based on the key
            ImmutableArray<CompletionItem> itemsBasedOnKey = ImmutableArray<CompletionItem>.Empty;
            if (!string.IsNullOrEmpty(key))
            {
                var matchingElements = Catalog.Keywords.Where(n => n.Name.StartsWith(key, StringComparison.OrdinalIgnoreCase)).OrderBy(n => n.Name);
                if (matchingElements.Count() > 0)
                {
                    var itemsBuilder = ImmutableArray.CreateBuilder<CompletionItem>();
                    itemsBuilder.AddRange(matchingElements.Select(e => MakeItemFromElement(e)));
                    itemsBasedOnKey = itemsBuilder.ToImmutable();
                }
            }
            // We would like to allow user to type anything, so we create SuggestionItemOptions
            //var suggestionOptions = new SuggestionItemOptions("Type anything...", $"Please enter value for {key}");

            return new CompletionContext(itemsBasedOnKey); //, suggestionOptions);
        }

        /// <summary>
        /// Returns completion items applicable to the key portion of the key-value pair
        /// </summary>
        private CompletionContext GetContextForKey()
        {
            var context = new CompletionContext(Catalog.Keywords.Select(n => MakeItemFromElement(n)).ToImmutableArray());
            return context;
        }

        /// <summary>
        /// Builds a <see cref="CompletionItem"/> based on <see cref="ElementCatalog.Element"/>
        /// </summary>
        private CompletionItem MakeItemFromElement(SqlCatalog.Keyword keyword)
        {
            ImageElement icon = null;
            ImmutableArray<CompletionFilter> filters;

            switch (keyword.Category)
            {
                case SqlCatalog.Category.Keyword:
                    icon = KeywordIcon;
                    filters = KeywordFilters;
                    break;
                case SqlCatalog.Category.Operator:
                    icon = OperatorIcon;
                    filters = OperatorFilters;
                    break;
                case SqlCatalog.Category.Function:
                    icon = FunctionIcon;
                    filters = FunctionFilters;
                    break;
                case SqlCatalog.Category.Variable:
                    icon = VariableIcon;
                    filters = VariableFilters;
                    break;
                case SqlCatalog.Category.DataType:
                    icon = DataTypeIcon;
                    filters = DataTypeFilters;
                    break;
            }
            var item = new CompletionItem(
                displayText: keyword.Name.ToUpper(),
                source: this,
                icon: icon,
                filters: filters,
                suffix: keyword.Category.ToString(), //keyword.Symbol
                insertText: keyword.Name.ToUpper(),
                sortText: $"keyword {keyword.Category}",
                filterText: $"{keyword.Name} {keyword.Category}",
                attributeIcons: ImmutableArray<ImageElement>.Empty);

            // Each completion item we build has a reference to the element in the property bag.
            // We use this information when we construct the tooltip.
            item.Properties.AddProperty(nameof(SqlCatalog.Keyword), keyword);

            return item;
        }

        /// <summary>
        /// Provides detailed element information in the tooltip
        /// </summary>
        public Task<object> GetDescriptionAsync(IAsyncCompletionSession session, CompletionItem item, CancellationToken token)
        {
            if (item.Properties.TryGetProperty<SqlCatalog.Keyword>(nameof(SqlCatalog.Keyword), out var matchingElement))
            {
                return Task.FromResult<object>($"{matchingElement.Name} is {GetCategoryName(matchingElement.Category)}");
            }
            return Task.FromResult<object>(null);
        }

        private string GetCategoryName(SqlCatalog.Category category)
        {
            switch (category)
            {
                case SqlCatalog.Category.Keyword: return "a keyword";
                case SqlCatalog.Category.Function: return "a function";
                case SqlCatalog.Category.Operator: return "an operator";
                case SqlCatalog.Category.Variable: return "a variable";
                case SqlCatalog.Category.DataType: return "a datatype";
                default: return "an uncategorized element";
            }
        }

    }
}
