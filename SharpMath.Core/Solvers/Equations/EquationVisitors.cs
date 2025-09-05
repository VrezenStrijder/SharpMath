using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SharpMath.Core
{

    /// <summary>
    /// 变量检测器
    /// </summary>
    public class VariableDetector : IExpressionVisitor<object>
    {
        public HashSet<string> Variables { get; } = new HashSet<string>();

        public object Visit(NumberExpression number) => null;

        public object Visit(VariableExpression variable)
        {
            Variables.Add(variable.Name);
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
            throw new InvalidOperationException();
        }
    }

    /// <summary>
    /// 平方根检测器
    /// </summary>
    public class SquareRootDetector : IExpressionVisitor<object>
    {
        public bool HasSquareRoot { get; private set; }

        public object Visit(FunctionExpression function)
        {
            if (function.Name.ToLower() == "sqrt")
            {
                HasSquareRoot = true;
            }
            foreach (var arg in function.Arguments)
            {
                arg.Accept(this);
            }
            return null;
        }

        public object Visit(NumberExpression number) => null;
        public object Visit(VariableExpression variable) => null;
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
            throw new InvalidOperationException();
        }
    }


    /// <summary>
    /// 变量替换访问器
    /// </summary>
    public class VariableSubstitutionVisitor : IExpressionVisitor<IMathExpression>
    {
        private readonly Dictionary<string, double> variableValues;

        public VariableSubstitutionVisitor(Dictionary<string, double> variableValues)
        {
            this.variableValues = variableValues;
        }

        public IMathExpression Visit(NumberExpression number) => number;

        public IMathExpression Visit(VariableExpression variable)
        {
            if (variableValues.TryGetValue(variable.Name, out double value))
            {
                return new NumberExpression(value);
            }
            return variable;
        }

        public IMathExpression Visit(BinaryOperationExpression binary)
        {
            var left = binary.Left.Accept(this);
            var right = binary.Right.Accept(this);
            return new BinaryOperationExpression(left, right, binary.OperationType);
        }

        public IMathExpression Visit(UnaryOperationExpression unary)
        {
            var operand = unary.Operand.Accept(this);
            return new UnaryOperationExpression(operand, unary.OperationType);
        }

        public IMathExpression Visit(FunctionExpression function)
        {
            var args = function.Arguments.Select(arg => arg.Accept(this)).ToList();
            return new FunctionExpression(function.Name, args);
        }

        public IMathExpression Visit(EquationExpression equation)
        {
            var left = equation.Left.Accept(this);
            var right = equation.Right.Accept(this);
            return new EquationExpression(left, right);
        }

        public IMathExpression Visit(EquationSystemExpression system)
        {
            foreach (var eq in system.Equations)
            {
                eq.Accept(this);
            }
            return null;
        }

        public IMathExpression Visit(MatrixExpression matrix)
        {
            throw new InvalidOperationException();
        }
    }

}
