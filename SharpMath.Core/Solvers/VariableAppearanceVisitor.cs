using System;
using System.Collections.Generic;
using System.Text;

namespace SharpMath.Core
{
    /// <summary>
    /// 用于提取变量出现顺序的访问者
    /// </summary>
    public class VariableAppearanceVisitor : IExpressionVisitor<object>
    {
        private readonly List<string> variables = new List<string>();

        public static List<string> GetVariableOrder(IMathExpression expression)
        {
            var visitor = new VariableAppearanceVisitor();
            expression.Accept(visitor);
            return visitor.variables;
        }

        public object Visit(NumberExpression number) => null;

        public object Visit(VariableExpression variable)
        {
            if (!variables.Contains(variable.Name))
            {
                variables.Add(variable.Name);
            }
            return null;
        }

        public object Visit(BinaryOperationExpression binary)
        {
            binary.Left.Accept(this); 
            binary.Right.Accept(this);
            return null;
        }

        public object Visit(UnaryOperationExpression unary)
        {
            unary.Operand.Accept(this);
            return null;
        }

        public object Visit(FunctionExpression function)
        {
            foreach (var arg in function.Arguments)
            {
                arg.Accept(this);
            }
            return null;
        }

        public object Visit(EquationExpression equation)
        {
            equation.Left.Accept(this);
            equation.Right.Accept(this);
            return null;
        }

        public object Visit(EquationSystemExpression system)
        {
            foreach (var eq in system.Equations)
            {
                eq.Accept(this);
            }
            return null;
        }

        public object Visit(MatrixExpression matrix)
        {
            matrix.Accept(this);
            return null;
        }
    }

}
