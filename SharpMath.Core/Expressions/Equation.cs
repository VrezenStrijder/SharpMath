using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SharpMath.Core
{
    /// <summary>
    /// 等式表达式
    /// </summary>
    public class EquationExpression : BinaryOperationExpression
    {
        public EquationExpression(IMathExpression left, IMathExpression right)
            : base(left, right, BinaryOperationType.Equal)
        {
        }

        public override string ToString() => $"{Left} = {Right}";

        public new string ToLatex() => $"{Left.ToLatex()} = {Right.ToLatex()}";

        public new T Accept<T>(IExpressionVisitor<T> visitor) => visitor.Visit(this);

        public new double Evaluate(Dictionary<string, double> args)
        {
            throw new InvalidOperationException("方程不支持单独带入值计算.");
        }

    }

    /// <summary>
    /// 方程组表达式
    /// </summary>
    public class EquationSystemExpression : IMathExpression
    {

        public EquationSystemExpression(List<EquationExpression> equations)
        {
            Equations = equations;
            Variables = new HashSet<string>();

            // 收集所有变量
            var detector = new VariableDetector();
            foreach (var eq in equations)
            {
                eq.Accept(detector);
            }
            Variables = detector.Variables;
        }

        public List<EquationExpression> Equations { get; }

        public HashSet<string> Variables { get; }


        public double Evaluate() => throw new InvalidOperationException("方程组不支持单独运算");

        public double Evaluate(Dictionary<string, double> args) => throw new InvalidOperationException("方程组不支持代入值运算");

        public string ToLatex()
        {
            if (Equations.Count == 0)
            {
                return "";
            }

            var sb = new StringBuilder();
            sb.AppendLine(@"\left\{ \begin{array}{l}");
            for (int i = 0; i < Equations.Count; i++)
            {
                sb.Append($"  {Equations[i].ToLatex()}");
                if (i < Equations.Count - 1)
                {
                    sb.AppendLine(" \\\\"); // 为除最后一个外的所有方程式添加换行符
                }
                else
                {
                    sb.AppendLine();
                }
            }
            sb.AppendLine(@"\end{array} \right.");
            return sb.ToString();

            //var sb = new StringBuilder();
            //sb.AppendLine("\\begin{cases}");
            //foreach (var eq in Equations)
            //{
            //    sb.AppendLine($"  {eq.ToLatex()} \\\\");
            //}
            //sb.AppendLine("\\end{cases}");
            //return sb.ToString();
        }

        public override string ToString()
        {
            return string.Join(", ", Equations.Select(eq => $"[{eq}]"));
        }

        public T Accept<T>(IExpressionVisitor<T> visitor) => visitor.Visit(this);

        public int GetPrecedence() => -2;

    }




}
