using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SharpMath.Core
{
    // <summary>
    /// 代数项
    /// </summary>
    public class Term
    {
        public Term(double coefficient, List<Factor> factors)
        {
            Coefficient = coefficient;
            Factors = factors.OrderBy(f => f.Base.ToString()).ToList();
            CanonicalVariablePart = string.Join("*", Factors.Select(f => f.ToString()));
            Degree = Factors.Sum(f => (f.Exponent as NumberExpression)?.Value ?? 1);
            Variables = new HashSet<string>(Factors.Select(f => f.Base.ToString()));
        }

        public double Coefficient { get; set; }

        public List<Factor> Factors { get; }

        public double Degree { get; }

        public bool IsConstant => Factors.Count == 0;

        public HashSet<string> Variables { get; }

        /// <summary>
        /// 规范的变量部分字符串, 用于比较同类项
        /// </summary>
        public string CanonicalVariablePart { get; }

        public static Term FromExpression(IMathExpression expr)
        {
            if (expr is NumberExpression num) return new Term(num.Value, new List<Factor>());
            if (expr is UnaryOperationExpression unary && unary.OperationType == UnaryOperationType.Negate)
            {
                var innerTerm = FromExpression(unary.Operand);
                innerTerm.Coefficient *= -1;
                return innerTerm;
            }

            var factors = new Dictionary<string, Factor>();
            var coefficient = 1.0;
            var stack = new Stack<IMathExpression>();
            stack.Push(expr);

            while (stack.Any())
            {
                var current = stack.Pop();
                if (current is BinaryOperationExpression mult && mult.OperationType == BinaryOperationType.Multiply)
                {
                    stack.Push(mult.Left);
                    stack.Push(mult.Right);
                }
                else if (current is NumberExpression c) { coefficient *= c.Value; }
                else
                {
                    var baseExpr = current;
                    var expExpr = (IMathExpression)new NumberExpression(1);
                    if (current is BinaryOperationExpression pow && pow.OperationType == BinaryOperationType.Power)
                    {
                        baseExpr = pow.Left;
                        expExpr = pow.Right;
                    }
                    var key = baseExpr.ToString();
                    if (factors.TryGetValue(key, out var existing))
                    {
                        var newExp = new SimplificationVisitor(expr, SortOrder.Normal).Simplify(new BinaryOperationExpression(existing.Exponent, expExpr, BinaryOperationType.Add));
                        factors[key] = new Factor(baseExpr, newExp);
                    }
                    else { factors[key] = new Factor(baseExpr, expExpr); }
                }
            }
            return new Term(coefficient, factors.Values.ToList());
        }

        public IMathExpression ToExpression()
        {
            if (IsConstant) return new NumberExpression(Coefficient);

            IMathExpression expr = null;
            foreach (var factor in Factors)
            {
                var factorExpr = (factor.Exponent is NumberExpression n && n.Value == 1)
                    ? factor.Base
                    : new BinaryOperationExpression(factor.Base, factor.Exponent, BinaryOperationType.Power);
                if (expr == null) expr = factorExpr;
                else expr = new BinaryOperationExpression(expr, factorExpr, BinaryOperationType.Multiply);
            }

            if (Coefficient == 1) return expr;
            if (Coefficient == -1) return new UnaryOperationExpression(expr, UnaryOperationType.Negate);
            return new BinaryOperationExpression(new NumberExpression(Coefficient), expr, BinaryOperationType.Multiply);
        }
        public double GetPowerOf(string varName)
        {
            var factor = Factors.FirstOrDefault(f => f.Base.ToString() == varName);
            return (factor?.Exponent as NumberExpression)?.Value ?? 0;
        }
    }

    /// <summary>
    /// 因子
    /// </summary>
    public class Factor
    {
        public Factor(IMathExpression @base, IMathExpression exponent)
        {
            Base = @base;
            Exponent = exponent;
        }

        public IMathExpression Base { get; }

        public IMathExpression Exponent { get; }


        public override string ToString()
        {
            if (Exponent is NumberExpression numExp && numExp.Value == 1)
            {
                return Base.ToString();
            }
            return $"{Base}^{Exponent}";
        }
    }

    /// <summary>
    /// 自定义排序
    /// </summary>
    public class TermComparer : IComparer<Term>
    {
        private readonly List<string> variableOrder;
        private readonly SortOrder sortOrder;

        public TermComparer(List<string> variableOrder, SortOrder sortOrder)
        {
            this.variableOrder = variableOrder;
            this.sortOrder = sortOrder;
        }

        public int Compare(Term x, Term y)
        {
            // 常数项总是在最后
            if (x.IsConstant && !y.IsConstant) return 1;
            if (!x.IsConstant && y.IsConstant) return -1;
            if (x.IsConstant && y.IsConstant) return 0;

            // 获取每个项的主导变量(按出现顺序的第一个变量)
            var xPrimaryVar = GetPrimaryVariable(x);
            var yPrimaryVar = GetPrimaryVariable(y);

            // 如果主导变量不同, 按变量出现顺序排序
            if (xPrimaryVar != yPrimaryVar)
            {
                var xIndex = variableOrder.IndexOf(xPrimaryVar);
                var yIndex = variableOrder.IndexOf(yPrimaryVar);
                return sortOrder == SortOrder.Normal ? xIndex.CompareTo(yIndex) : yIndex.CompareTo(xIndex);
            }

            // 主导变量相同, 比较总次数(所有变量的幂次之和)
            var xTotalDegree = x.Degree;
            var yTotalDegree = y.Degree;
            if (xTotalDegree != yTotalDegree)
            {
                return sortOrder == SortOrder.Normal ?
                    yTotalDegree.CompareTo(xTotalDegree) :
                    xTotalDegree.CompareTo(yTotalDegree);
            }

            // 总次数相同, 按变量出现顺序逐个比较幂次
            foreach (var varName in variableOrder)
            {
                var xPower = x.GetPowerOf(varName);
                var yPower = y.GetPowerOf(varName);

                if (xPower != yPower)
                {
                    return sortOrder == SortOrder.Normal ?
                        yPower.CompareTo(xPower) :
                        xPower.CompareTo(yPower);
                }
            }

            return 0;
        }

        private string GetPrimaryVariable(Term term)
        {
            // 返回按出现顺序的第一个变量
            foreach (var varName in variableOrder)
            {
                if (term.Variables.Contains(varName))
                {
                    return varName;
                }
            }
            return string.Empty;
        }
    }


    public enum SortOrder
    {
        Normal,
        Reversed
    }

}
