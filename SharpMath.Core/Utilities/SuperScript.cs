using System;
using System.Collections.Generic;
using System.Text;

namespace SharpMath.Core
{
    /// <summary>
    /// 提供将数字转换为Unicode上标字符的功能
    /// </summary>
    public class SuperScript
    {
        private static readonly char[] ss = new char[] { '\u207B', '\u2070', '\u00B9', '\u00B2', '\u00B3', '\u2074', '\u2075', '\u2076', '\u2077', '\u2078', '\u2079' };

        /// <summary>
        /// 将单个数字转换为上标字符
        /// </summary>
        /// <param name="i">取值范围为-1到9</param>
        public static char Get(int i)
        {
            if (i >= -1 && i <= 9)
            {
                return ss[i + 1];
            }
            return '\0';
        }

        /// <summary>
        /// 将一个数字字符串转换为上标字符串
        /// </summary>
        public static string From(string numberStr)
        {
            var result = new System.Text.StringBuilder();
            foreach (char c in numberStr)
            {
                if (c == '-')
                {
                    result.Append(Get(-1));
                }
                else if (char.IsDigit(c))
                {
                    result.Append(Get(int.Parse(c.ToString())));
                }
                else
                {
                    // For non-digits like '.', just append the original character wrapped in ^()
                    return $"^({numberStr})";
                }
            }
            return result.ToString();
        }
    }
}
