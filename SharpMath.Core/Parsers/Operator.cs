using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace SharpMath.Core
{
    /// <summary>
    /// 运算符
    /// </summary>
    public class OperatorDefinition
    {
        public string Symbol { get; set; }

        public int Precedence { get; set; }

        public OperatorType Type { get; set; }

        public Func<Expression, Expression, Expression> BinaryOperation { get; set; }

        public Func<Expression, Expression> UnaryOperation { get; set; }


    }

    public enum OperatorType
    {
        Binary,
        Unary
    }
}
