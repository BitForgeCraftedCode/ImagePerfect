using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ImagePerfect.Helpers
{
    public static class TagValidationHelper
    {
        public static bool IsTagValid(string tag)
        {
            if (string.IsNullOrEmpty(tag)) return false;
            return tag.All(character => char.IsAsciiLetterOrDigit(character));
        }
    }
}
