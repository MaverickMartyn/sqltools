using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;
using System.Windows.Media;

namespace SqlTools.Classifiers
{
    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = "Sql-Keyword")]
    [Name("Sql-Keyword")]
    [UserVisible(true)]
    [Order(Before = Priority.High, After = Priority.High)]
    internal sealed class SqlKeyworkFormat : ClassificationFormatDefinition
    {
        public SqlKeyworkFormat()
        {
            DisplayName = "Sql-Keyword";
            ForegroundColor = new Color() { R = 10, G = 100, B = 200 }; // Bluish color that is visible in light and dark default themes
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = "Sql-Operator")]
    [Name("Sql-Operator")]
    [UserVisible(true)]
    [Order(Before = Priority.High, After = Priority.High)]
    internal sealed class SqlOperatorFormat : ClassificationFormatDefinition
    {
        public SqlOperatorFormat()
        {
            DisplayName = "Sql-Operator";
            ForegroundColor = Colors.Gray;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = "Sql-Function")]
    [Name("Sql-Function")]
    [UserVisible(true)]
    [Order(Before = Priority.High, After = Priority.High)]
    internal sealed class SqlDunctionFormat : ClassificationFormatDefinition
    {
        public SqlDunctionFormat()
        {
            DisplayName = "Sql-Function";
            ForegroundColor = Colors.Magenta;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = "Sql-Variable")]
    [Name("Sql-Variable")]
    [UserVisible(true)]
    [Order(Before = Priority.High, After = Priority.High)]
    internal sealed class SqlVariableFormat : ClassificationFormatDefinition
    {
        public SqlVariableFormat()
        {
            DisplayName = "Sql-Variable";
            ForegroundColor = Colors.Green;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = "Sql-Literal")]
    [Name("Sql-Literal")]
    [UserVisible(true)]
    [Order(Before = Priority.High, After = Priority.High)]
    internal sealed class SqlLiteralFormat : ClassificationFormatDefinition
    {
        public SqlLiteralFormat()
        {
            DisplayName = "Sql-Literal";
            ForegroundColor = new Color() { R = 156, G = 220, B = 254 };
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = "Sql-StringLiteral")]
    [Name("Sql-StringLiteral")]
    [UserVisible(true)]
    [Order(Before = Priority.High, After = Priority.High)]
    internal sealed class SqlStringLiteralFormat : ClassificationFormatDefinition
    {
        public SqlStringLiteralFormat()
        {
            DisplayName = "Sql-StringLiteral";
            ForegroundColor = Colors.Black;
            BackgroundColor = Colors.Cyan;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = "Sql-Comment")]
    [Name("Sql-Comment")]
    [UserVisible(true)]
    [Order(Before = Priority.High, After = Priority.High)]
    internal sealed class SqlCommentFormat : ClassificationFormatDefinition
    {
        public SqlCommentFormat()
        {
            DisplayName = "Sql-Comment";
            ForegroundColor = Colors.ForestGreen;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = "Sql-Defined")]
    [Name("Sql-Defined")]
    [UserVisible(true)]
    [Order(Before = Priority.High, After = Priority.High)]
    internal sealed class SqlDefineFormat : ClassificationFormatDefinition
    {
        public SqlDefineFormat()
        {
            DisplayName = "Sql-Defined";
            ForegroundColor = new Color() { R = 116, G = 83, B = 31 };
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = "Sql-Workflow")]
    [Name("Sql-Workflow")]
    [UserVisible(true)]
    [Order(Before = Priority.High, After = Priority.High)]
    internal sealed class SqlWorkflowFormat : ClassificationFormatDefinition
    {
        public SqlWorkflowFormat()
        {
            DisplayName = "Sql-Workflow";
            ForegroundColor = new Color() { R = 255, G = 69, B = 0 };
        }
    }
}
