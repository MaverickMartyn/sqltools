using SqlTools.NaturalTextTaggers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SqlTools.ClassExtensions
{
    internal static class MiscExtensions
    {
        internal static bool IsMultiLine(this State that)
            => that == State.MultiLineString || that == State.RawString;
    }
}
