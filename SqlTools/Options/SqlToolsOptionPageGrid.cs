using Microsoft.VisualStudio.Shell;
using System.ComponentModel;

namespace SqlTools.Options
{
    public class SqlToolsOptionPageGrid : DialogPage
    {
        private bool _enableAutoCompleteSuggestions = true;

        public SqlToolsOptionPageGrid() { }

        [Category("General Options")]
        [DisplayName("Enable auto-complete suggestions")]
        [Description("Provides intellisense suggestions when editing a multiline string.")]
        public bool EnableAutoCompleteSuggestions
        {
            get { return _enableAutoCompleteSuggestions; }
            set
            {
                _enableAutoCompleteSuggestions = value;
            }
        }
    }
}
