using System;
using System.Collections.Generic;
using System.Text;

namespace SharpMath.Core
{

    public class Token
    {
        public Token(TokenType type, string value)
        {
            Type = type;
            Value = value;
        }

        public TokenType Type { get; }

        public string Value { get; }

    }

    public enum TokenType
    {
        Number,
        Variable,
        Constant,
        Operator,
        Function,
        LeftParen,
        RightParen,
        UnaryOperator,      // 取负号
        Comma               // 逗号
    }

}
