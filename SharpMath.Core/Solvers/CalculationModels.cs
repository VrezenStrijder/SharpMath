using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SharpMath.Core
{
    /// <summary>
    /// 计算过程中的步骤
    /// </summary>
    public class CalculationStep
    {
        public CalculationStep(IMathExpression resultingExpression, int stepIndex = 0, string description = "", bool descriptionBehindExpression = true)
        {
            ResultingExpression = resultingExpression;
            StepIndex = stepIndex;
            Description = description;
            DescriptionBehindExpression = descriptionBehindExpression;
        }

        public IMathExpression ResultingExpression { get; }

        public string Description { get; }

        public int StepIndex { get; set; }

        /// <summary>
        /// 是否将描述放在表达式后面
        /// </summary>
        public bool DescriptionBehindExpression { get; set; } = true;

        public string Display(DisplayPattern pattern)
        {
            switch (pattern)
            {
                case DisplayPattern.Text:
                    return ToString();
                case DisplayPattern.Latex:
                    if (string.IsNullOrEmpty(Description))
                    {
                        return $"{(StepIndex > 0 ? "    => " : string.Empty)}{ResultingExpression.ToLatex()}";
                    }
                    return $"{(StepIndex > 0 ? "    => " : string.Empty)}{ResultingExpression.ToLatex()}  // {Description}";
                default:
                    return ToString();
            }
        }

        public override string ToString()
        {
            if (string.IsNullOrEmpty(Description))
            {
                return $"{(StepIndex > 0 ? "    => " : string.Empty)}{ResultingExpression.ToString()}";
            }

            if (DescriptionBehindExpression)
            {
                return $"{(StepIndex > 0 ? "    => " : string.Empty)}{ResultingExpression}  // {Description}";
            }
            else
            {
                return $"// {Description}\r\n{(StepIndex > 0 ? "    => " : string.Empty)}{ResultingExpression}";
            }
        }
    }

    public enum DisplayPattern
    {
        Text,
        Latex
    }

    /// <summary>
    /// 运算结果
    /// </summary>
    public class CalculationResult
    {
        public IMathExpression OriginalExpression { get; set; }

        public IMathExpression FinalExpression { get; set; }

        public IReadOnlyList<CalculationStep> Steps { get; set; } = new List<CalculationStep>();

        public string AnswerText => FinalExpression.ToString();
    }


    /// <summary>
    /// 步骤描述扩展方法
    /// </summary>
    public static class StepDescriptionExtension
    {
        public static string GetExpandOperationDescription(this IMathExpression original, IMathExpression result, string operationType)
        {
            switch (operationType)
            {
                case "ConstantFolding":
                    return GetConstantFoldingDescription(original);
                case "Distribution":
                    return "应用分配律展开乘法";
                case "PowerRule":
                    return GetPowerRuleDescription(original);
                case "UnaryNegation":
                    return GetUnaryNegationDescription(original);
                case "FunctionEvaluation":
                    return GetFunctionEvaluationDescription(original);
                case "SquareRootSquare":
                    return "化简平方根的平方";
                case "NegativeSquare":
                    return "化简负平方根的平方";
                case "ExpandSquare":
                    return "展开平方";
                default:
                    return "展开表达式";
            }
        }

        private static string GetConstantFoldingDescription(this IMathExpression expr)
        {
            if (expr is BinaryOperationExpression binOp &&
                binOp.Left is NumberExpression left &&
                binOp.Right is NumberExpression right)
            {
                var result = new BinaryOperationExpression(left, right, binOp.OperationType).Evaluate();
                switch (binOp.OperationType)
                {
                    case BinaryOperationType.Add:
                        return $"常量加法: {left.Value} + {right.Value} = {result}";
                    case BinaryOperationType.Subtract:
                        return $"常量减法: {left.Value} - {right.Value} = {result}";
                    case BinaryOperationType.Multiply:
                        return $"常量乘法: {left.Value} × {right.Value} = {result}";
                    case BinaryOperationType.Divide:
                        return $"常量除法: {left.Value} ÷ {right.Value} = {result}";
                    case BinaryOperationType.Power:
                        return $"常量幂运算: {left.Value}^{right.Value} = {result}";
                }
            }
            return "常量运算";
        }

        private static string GetPowerRuleDescription(this IMathExpression expr)
        {
            if (expr is BinaryOperationExpression binOp &&
                binOp.OperationType == BinaryOperationType.Power &&
                binOp.Right is NumberExpression exp)
            {
                if (exp.Value == 0)
                {
                    return $"幂运算: {binOp.Left}^0 = 1";
                }
                if (exp.Value == 1)
                {
                    return $"幂运算: {binOp.Left}^1 = {binOp.Left}";
                }
            }
            return "幂运算";
        }

        private static string GetUnaryNegationDescription(this IMathExpression expr)
        {
            if (expr is UnaryOperationExpression unary && unary.Operand is NumberExpression num)
            {
                return $"计算负数: -{num.Value} = {-num.Value}";
            }
            return "应用负号";
        }

        private static string GetFunctionEvaluationDescription(this IMathExpression expr)
        {
            if (expr is FunctionExpression func)
            {
                var argValues = func.Arguments
                    .OfType<NumberExpression>()
                    .Select(n => n.Value.ToString())
                    .ToList();

                if (argValues.Count == func.Arguments.Count)
                {
                    var result = func.Evaluate();
                    return $"计算函数值: {func.Name}({string.Join(", ", argValues)}) = {result}";
                }
            }
            return "计算函数";
        }

        public static string GetCombineDescription(int originalTermCount, int combinedTermCount)
        {
            var mergedCount = originalTermCount - combinedTermCount;
            if (mergedCount > 0)
            {
                return $"合并同类项 (合并了{mergedCount}项)";
            }
            else if (originalTermCount != combinedTermCount)
            {
                return "整理表达式";
            }
            return "合并同类项";
        }

        public static string GetSortDescription(SortOrder sortOrder)
        {
            return sortOrder == SortOrder.Normal
                ? "顺序排序"
                : "逆序排序";
        }

        public static string GetDistributionDescription(IMathExpression left, IMathExpression right)
        {
            bool leftIsSum = IsAdditionOrSubtraction(left);
            bool rightIsSum = IsAdditionOrSubtraction(right);

            if (leftIsSum && rightIsSum)
            {
                return "展开 (多项式) × (多项式)";
            }
            else if (leftIsSum || rightIsSum)
            {
                return "应用分配律";
            }
            else
            {
                return "展开乘法";
            }
        }

        private static bool IsAdditionOrSubtraction(this IMathExpression expr)
        {
            return expr is BinaryOperationExpression binOp &&
                   (binOp.OperationType == BinaryOperationType.Add ||
                    binOp.OperationType == BinaryOperationType.Subtract);
        }
    }


}
