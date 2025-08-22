using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SharpMath.Core
{
    /// <summary>
    /// 数值常量表达式
    /// </summary>
    public class NumberExpression : IMathExpression
    {
        /// <summary>
        /// 构造一个数值表达式
        /// </summary>
        /// <param name="value">数值</param>
        public NumberExpression(double value)
        {
            Value = value;
        }

        /// <summary>
        /// 数值的具体值
        /// </summary>
        public double Value { get; }


        public double Evaluate() => Value;

        public double Evaluate(Dictionary<string, double> args) => Value;

        public string ToLatex() => Value.ToString();
        public override string ToString() => Value.ToString();
        public T Accept<T>(IExpressionVisitor<T> visitor) => visitor.Visit(this);

        public int GetPrecedence() => 100; // 最高优先级

    }

    /// <summary>
    /// 变量表达式
    /// </summary>
    public class VariableExpression : IMathExpression
    {        /// <summary>
             /// 构造一个变量表达式
             /// </summary>
             /// <param name="name">变量名</param>
        public VariableExpression(string name)
        {
            Name = name;
        }

        /// <summary>
        /// 变量名
        /// </summary>
        public string Name { get; }

        public double Evaluate() => throw new InvalidOperationException($"Cannot evaluate variable '{Name}' without a value.");

        public double Evaluate(Dictionary<string, double> args)
        {
            if (args != null && args.TryGetValue(Name, out double value))
            {
                return value;
            }
            throw new ArgumentException($"Value for variable '{Name}' was not provided in the context.");
        }

        public string ToLatex() => Name;
        public override string ToString() => Name;
        public T Accept<T>(IExpressionVisitor<T> visitor) => visitor.Visit(this);

        public int GetPrecedence() => 100;
    }


    /// <summary>
    /// 一元运算表达式
    /// </summary>
    public class UnaryOperationExpression : IMathExpression
    {
        public UnaryOperationExpression(IMathExpression operand, UnaryOperationType opType)
        {
            Operand = operand;
            OperationType = opType;
        }

        public IMathExpression Operand { get; }
        public UnaryOperationType OperationType { get; }


        public double Evaluate() => -Operand.Evaluate();

        public double Evaluate(Dictionary<string, double> args) => -Operand.Evaluate(args);

        public string ToLatex() => $"-{WrapWithBracketsIfNeeded(Operand, true)}";

        public override string ToString() => $"-{WrapWithBracketsIfNeeded(Operand, false)}";

        private string WrapWithBracketsIfNeeded(IMathExpression expr, bool isLatex)
        {
            return expr.GetPrecedence() < this.GetPrecedence()
                ? $"({(isLatex ? expr.ToLatex() : expr.ToString())})"
                : (isLatex ? expr.ToLatex() : expr.ToString());
        }

        public int GetPrecedence() => 4;

        public T Accept<T>(IExpressionVisitor<T> visitor) => visitor.Visit(this);
    }


    /// <summary>
    /// 二元运算表达式
    /// </summary>
    public class BinaryOperationExpression : IMathExpression
    {
        public BinaryOperationExpression(IMathExpression left, IMathExpression right, BinaryOperationType opType)
        {
            Left = left;
            Right = right;
            OperationType = opType;
        }

        public IMathExpression Left { get; }
        public IMathExpression Right { get; }
        public BinaryOperationType OperationType { get; }

        public double Evaluate()
        {
            double leftVal = Left.Evaluate();
            double rightVal = Right.Evaluate();
            switch (OperationType)
            {
                case BinaryOperationType.Add: return leftVal + rightVal;
                case BinaryOperationType.Subtract: return leftVal - rightVal;
                case BinaryOperationType.Multiply: return leftVal * rightVal;
                case BinaryOperationType.Divide: return rightVal == 0 ? double.NaN : leftVal / rightVal;
                case BinaryOperationType.Power: return Math.Pow(leftVal, rightVal);
                case BinaryOperationType.Modulo: return leftVal % rightVal;
                // 以下为比较运算符(true: 1, false: 0)
                case BinaryOperationType.GreaterThan: return leftVal > rightVal ? 1.0 : 0.0;
                case BinaryOperationType.LessThan: return leftVal < rightVal ? 1.0 : 0.0;
                case BinaryOperationType.GreaterThanOrEqual: return leftVal >= rightVal ? 1.0 : 0.0;
                case BinaryOperationType.LessThanOrEqual: return leftVal <= rightVal ? 1.0 : 0.0;
                case BinaryOperationType.Equal: return leftVal == rightVal ? 1.0 : 0.0;
                case BinaryOperationType.NotEqual: return leftVal != rightVal ? 1.0 : 0.0;

                default: throw new NotImplementedException();
            }
        }

        public double Evaluate(Dictionary<string, double> args)
        {
            double leftVal = Left.Evaluate(args);
            double rightVal = Right.Evaluate(args);

            switch (OperationType)
            {
                case BinaryOperationType.Add: return leftVal + rightVal;
                case BinaryOperationType.Subtract: return leftVal - rightVal;
                case BinaryOperationType.Multiply: return leftVal * rightVal;
                case BinaryOperationType.Divide: return rightVal == 0 ? double.NaN : leftVal / rightVal;
                case BinaryOperationType.Power: return Math.Pow(leftVal, rightVal);
                case BinaryOperationType.Modulo: return leftVal % rightVal;
                // 以下为比较运算符(true: 1, false: 0)
                case BinaryOperationType.GreaterThan: return leftVal > rightVal ? 1.0 : 0.0;
                case BinaryOperationType.LessThan: return leftVal < rightVal ? 1.0 : 0.0;
                case BinaryOperationType.GreaterThanOrEqual: return leftVal >= rightVal ? 1.0 : 0.0;
                case BinaryOperationType.LessThanOrEqual: return leftVal <= rightVal ? 1.0 : 0.0;
                case BinaryOperationType.Equal: return leftVal == rightVal ? 1.0 : 0.0;
                case BinaryOperationType.NotEqual: return leftVal != rightVal ? 1.0 : 0.0;

                default: throw new NotImplementedException();
            }
        }


        public string ToLatex()
        {
            string left = Left is BinaryOperationExpression ? $"({Left.ToLatex()})" : Left.ToLatex();
            string right = Right is BinaryOperationExpression ? $"({Right.ToLatex()})" : Right.ToLatex();

            switch (OperationType)
            {
                case BinaryOperationType.Add: return $"{left} + {right}";
                case BinaryOperationType.Subtract: return $"{left} - {right}";
                case BinaryOperationType.Multiply: return $"{left} \\cdot {right}";
                case BinaryOperationType.Divide: return $"\\frac{{{Left.ToLatex()}}}{{{Right.ToLatex()}}}";
                case BinaryOperationType.Power: return $"{left}^{{{Right.ToLatex()}}}";
                case BinaryOperationType.Modulo: return $"{left} \\% {right}";
                case BinaryOperationType.GreaterThan: return $"{left} > {right}";
                case BinaryOperationType.LessThan: return $"{left} < {right}";
                case BinaryOperationType.GreaterThanOrEqual: return $"{left} \\ge {right}";
                case BinaryOperationType.LessThanOrEqual: return $"{left} \\le {right}";
                case BinaryOperationType.Equal: return $"{left} = {right}";
                case BinaryOperationType.NotEqual: return $"{left} \\ne {right}";

                default: return $"{left} {GetOperatorString()} {right}";
            }
        }

        public override string ToString()
        {
            // Special case for power with integer exponent for superscript
            if (OperationType == BinaryOperationType.Power && Right is NumberExpression num && num.Value == (int)num.Value)
            {
                string leftStr = Left.GetPrecedence() < this.GetPrecedence() ? $"({Left})" : Left.ToString();
                return $"{leftStr}{SuperScript.From(num.Value.ToString())}";
            }

            // Add parentheses only when necessary based on precedence
            string left = Left.GetPrecedence() < this.GetPrecedence() ? $"({Left})" : Left.ToString();
            string right = Right.GetPrecedence() <= this.GetPrecedence() ? $"({Right})" : Right.ToString();

            return $"{left} {GetOperatorString()} {right}";
        }

        private string GetOperatorString()
        {
            switch (OperationType)
            {
                case BinaryOperationType.Add: return "+";
                case BinaryOperationType.Subtract: return "-";
                case BinaryOperationType.Multiply: return "*";
                case BinaryOperationType.Divide: return "/";
                case BinaryOperationType.Power: return "^";
                case BinaryOperationType.Modulo: return "%";
                case BinaryOperationType.GreaterThan: return ">";
                case BinaryOperationType.LessThan: return "<";
                case BinaryOperationType.GreaterThanOrEqual: return ">=";
                case BinaryOperationType.LessThanOrEqual: return "<=";
                case BinaryOperationType.Equal: return "==";
                case BinaryOperationType.NotEqual: return "!=";
                default: return "?";

            }
        }

        public int GetPrecedence()
        {
            return OperationType switch
            {
                BinaryOperationType.Add => 1,
                BinaryOperationType.Subtract => 1,
                BinaryOperationType.Multiply => 2,
                BinaryOperationType.Divide => 2,
                BinaryOperationType.Modulo => 2,
                BinaryOperationType.Power => 3,
                BinaryOperationType.GreaterThan => 0,
                BinaryOperationType.LessThan => 0,
                BinaryOperationType.GreaterThanOrEqual => 0,
                BinaryOperationType.LessThanOrEqual => 0,
                BinaryOperationType.Equal => 0,
                BinaryOperationType.NotEqual => 0,
                _ => 0,
            };
        }

        public T Accept<T>(IExpressionVisitor<T> visitor) => visitor.Visit(this);
    }

    /// <summary>
    /// 函数表达式
    /// </summary>
    public class FunctionExpression : IMathExpression
    {
        public FunctionExpression(string name, List<IMathExpression> arguments)
        {
            Name = name;
            Arguments = arguments;
        }

        public string Name { get; }
        public IReadOnlyList<IMathExpression> Arguments { get; }


        private static readonly Dictionary<string, Func<double[], double>> FuncEvaluators = new Dictionary<string, Func<double[], double>>
        {
            // 三角函数
            {"sin", args => Math.Sin(args[0])},
            {"cos", args => Math.Cos(args[0])},
            {"tan", args => Math.Tan(args[0])},
            {"asin", args => Math.Asin(args[0])},
            {"acos", args => Math.Acos(args[0])},
            {"atan", args => Math.Atan(args[0])},
            {"atan2", args => Math.Atan2(args[0], args[1])},
            {"sinh", args => Math.Sinh(args[0])},
            {"cosh", args => Math.Cosh(args[0])},
            {"tanh", args => Math.Tanh(args[0])},
            // 指数/对数
            {"sqrt", args => Math.Sqrt(args[0])},
            {"log", args => Math.Log(args[0])},
            {"log10", args => Math.Log10(args[0])},
            {"pow", args => Math.Pow(args[0], args[1])},
            // 小数/余数处理
            {"abs", args => Math.Abs(args[0])},
            {"ceiling", args => Math.Ceiling(args[0])},
            {"floor", args => Math.Floor(args[0])},
            {"round", args => Math.Round(args[0])},
            {"sign", args => Math.Sign(args[0])},
            // 数值限制/转换
            {"exp", args => Math.Exp(args[0])},
            {"max", args => args.Max()},
            {"min", args => args.Min()},
            {"trunc", args => Math.Truncate(args[0])},
            {"frac", args => args[0] - Math.Truncate(args[0])},
            // 统计函数
            {"mean", args => args.Average()},
            {"sum", args => args.Sum()},
            {"product", args => args.Aggregate(1.0, (acc, val) => acc * val)}, // 连乘
            {"variance", args =>args.Length < 2 ? 0 : args.Select(x => Math.Pow(x - args.Average(), 2)).Sum() / (args.Length -1)}, // 方差
            {"stddev", args => args.Length < 2 ? 0 : Math.Sqrt(args.Select(x => Math.Pow(x - args.Average(), 2)).Sum() / (args.Length -1))}  // 标准差
        };

        public double Evaluate()
        {
            if (FuncEvaluators.TryGetValue(Name.ToLower(), out var evaluator))
            {
                var argValues = Arguments.Select(a => a.Evaluate()).ToArray();
                return evaluator(argValues);
            }
            throw new NotImplementedException($"Function '{Name}' is not implemented for evaluation.");
        }

        public double Evaluate(Dictionary<string, double> args)
        {
            if (FuncEvaluators.TryGetValue(Name.ToLower(), out var evaluator))
            {
                var argValues = Arguments.Select(a => a.Evaluate(args)).ToArray();
                return evaluator(argValues);
            }
            throw new NotImplementedException($"Function '{Name}' is not implemented for evaluation.");
        }


        public string ToLatex()
        {
            var args = string.Join(", ", Arguments.Select(a => a.ToLatex()));
            var funcName = Name.ToLower();
            if (funcName == "sqrt")
            {
                return $"\\sqrt{{{args}}}";
            }
            return $"\\mathrm{{{funcName}}}({args})";
        }

        public override string ToString()
        {
            var args = string.Join(", ", Arguments.Select(a => a.ToString()));
            return $"{Name.ToLower()}({args})";
        }

        public int GetPrecedence() => 100;

        public T Accept<T>(IExpressionVisitor<T> visitor) => visitor.Visit(this);
    }

    public enum UnaryOperationType
    {
        Negate
    }

    public enum BinaryOperationType
    {
        Add,
        Subtract,
        Multiply,
        Divide,
        Power,
        Modulo,
        GreaterThan,
        LessThan,
        GreaterThanOrEqual,
        LessThanOrEqual,
        Equal,
        NotEqual
    }

}
