using System;
using System.Collections.Generic;
using System.Text;
using static SharpMath.Core.RadicalTermCollector;

namespace SharpMath.Core
{
    /// <summary>
    /// 根式项收集器
    /// </summary>
    public class RadicalTermCollector : IExpressionVisitor<List<RadicalTerm>>
    {

        public List<RadicalTerm> Visit(NumberExpression number)
        {
            return new List<RadicalTerm>
        {
            new RadicalTerm { Expression = number, IsPositive = true, ContainsRadical = false }
        };
        }

        public List<RadicalTerm> Visit(VariableExpression variable)
        {
            return new List<RadicalTerm>
        {
            new RadicalTerm { Expression = variable, IsPositive = true, ContainsRadical = false }
        };
        }

        public List<RadicalTerm> Visit(BinaryOperationExpression binary)
        {
            if (binary.OperationType == BinaryOperationType.Add ||
                binary.OperationType == BinaryOperationType.Subtract)
            {
                var leftTerms = binary.Left.Accept(this);
                var rightTerms = binary.Right.Accept(this);

                if (binary.OperationType == BinaryOperationType.Subtract)
                {
                    foreach (var term in rightTerms)
                    {
                        term.IsPositive = !term.IsPositive;
                    }
                }

                leftTerms.AddRange(rightTerms);
                return leftTerms;
            }
            else
            {
                var hasRadical = ContainsSquareRoot(binary);
                return new List<RadicalTerm>
            {
                new RadicalTerm { Expression = binary, IsPositive = true, ContainsRadical = hasRadical }
            };
            }
        }

        public List<RadicalTerm> Visit(UnaryOperationExpression unary)
        {
            var terms = unary.Operand.Accept(this);
            foreach (var term in terms)
            {
                term.IsPositive = !term.IsPositive;
            }
            return terms;
        }

        public List<RadicalTerm> Visit(FunctionExpression function)
        {
            var hasRadical = function.Name.ToLower() == "sqrt";
            return new List<RadicalTerm>
        {
            new RadicalTerm { Expression = function, IsPositive = true, ContainsRadical = hasRadical }
        };
        }

        public List<RadicalTerm> Visit(EquationExpression equation)
        {
            // 不应该在方程内部调用
            throw new InvalidOperationException();
        }

        public List<RadicalTerm> Visit(EquationSystemExpression system)
        {
            throw new InvalidOperationException();
        }

        public List<RadicalTerm> Visit(MatrixExpression matrix)
        {
            throw new InvalidOperationException();
        }

        private bool ContainsSquareRoot(IMathExpression expr)
        {
            var detector = new SquareRootDetector();
            expr.Accept(detector);
            return detector.HasSquareRoot;
        }
    }

    public class RadicalTerm
    {
        public IMathExpression Expression { get; set; }
        public bool IsPositive { get; set; } // 表示是加还是减
        public bool ContainsRadical { get; set; }
    }

    /// <summary>
    /// 检查根式定义域
    /// </summary>
    public class RadicalDomainChecker : IExpressionVisitor<object>
    {
        private readonly Dictionary<string, double> variableValues;
        public bool IsValid { get; private set; } = true;

        public RadicalDomainChecker(Dictionary<string, double> variableValues)
        {
            this.variableValues = variableValues;
        }

        public object Visit(FunctionExpression function)
        {
            if (function.Name.ToLower() == "sqrt" && function.Arguments.Count > 0)
            {
                // 计算根式内表达式的值
                var substitutor = new VariableSubstitutionVisitor(variableValues);
                var substituted = function.Arguments[0].Accept(substitutor);

                try
                {
                    double value = substituted.Evaluate();
                    if (value < -1e-10) // 考虑浮点数精度
                    {
                        IsValid = false;
                    }
                }
                catch
                {
                    IsValid = false;
                }
            }

            // 继续检查子表达式
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
}
