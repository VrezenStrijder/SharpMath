using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SharpMath.Core
{
    /// <summary>
    /// 实现了符号化简逻辑的表达式访问者
    /// </summary>
    public class SimplificationVisitor : IExpressionVisitor<IMathExpression>
    {
        private readonly List<CalculationStep> steps;
        private int stepIndex;
        private readonly SortOrder sortOrder;
        private readonly List<string> variableOrder;
        private bool isExpandingOnly = true; // 标记是否只进行展开, 不合并

        private readonly List<(IMathExpression original, IMathExpression result, string operationType)> pendingOperations;


        public IReadOnlyList<CalculationStep> Steps => steps;

        public SimplificationVisitor(IMathExpression originalExpression, SortOrder sortOrder, int initStepIndex = 0)
        {
            steps = new List<CalculationStep>();
            stepIndex = initStepIndex;
            this.sortOrder = sortOrder;
            variableOrder = VariableAppearanceVisitor.GetVariableOrder(originalExpression);
            pendingOperations = new List<(IMathExpression, IMathExpression, string)>();
        }

        public IMathExpression Simplify(IMathExpression expr)
        {
            IMathExpression currentExpr = expr;
            IMathExpression previousExpr;

            // 1：展开所有乘法, 但不合并同类项
            isExpandingOnly = true;
            do
            {
                previousExpr = currentExpr;
                pendingOperations.Clear();
                currentExpr = ExpandOnePass(currentExpr);

                if (currentExpr.ToString() != previousExpr.ToString())
                {
                    // 根据收集的操作生成描述
                    var description = GenerateStepDescription();
                    AddStep(currentExpr, description);
                }
            } while (currentExpr.ToString() != previousExpr.ToString());

            // 2：合并同类项, 但不排序
            isExpandingOnly = false;
            var terms = DecomposeFullExpression(currentExpr);
            var termsList = terms.ToList();
            var combined = Combine(termsList);
            var combinedList = combined.ToList();
            var unsortedResult = ReconstructWithoutSort(combinedList);

            if (unsortedResult.ToString() != currentExpr.ToString())
            {
                var description = StepDescriptionExtension.GetCombineDescription(termsList.Count, combinedList.Count);
                AddStep(unsortedResult, description);
                currentExpr = unsortedResult;
            }

            // 3：最终排序
            var comparer = new TermComparer(variableOrder, sortOrder);
            combinedList.Sort(comparer);
            var finalResult = ReconstructWithSort(combinedList);

            if (finalResult.ToString() != currentExpr.ToString())
            {
                var description = StepDescriptionExtension.GetSortDescription(sortOrder);
                AddStep(finalResult, description);

            }

            return finalResult;
        }

        private IMathExpression ExpandOnePass(IMathExpression expr)
        {
            return ExpandExpression(expr);
        }

        private IMathExpression ExpandExpression(IMathExpression expr)
        {
            if (expr is BinaryOperationExpression binOp)
            {
                var left = ExpandExpression(binOp.Left);
                var right = ExpandExpression(binOp.Right);

                // 常量折叠
                if (left is NumberExpression lNum && right is NumberExpression rNum)
                {
                    var result = new NumberExpression(new BinaryOperationExpression(lNum, rNum, binOp.OperationType).Evaluate());
                    pendingOperations.Add((binOp, result, "ConstantFolding"));
                    return result;
                }

                switch (binOp.OperationType)
                {
                    case BinaryOperationType.Add:
                    case BinaryOperationType.Subtract:
                        // 在展开阶段, 只递归处理子表达式, 不合并
                        return new BinaryOperationExpression(left, right, binOp.OperationType);

                    case BinaryOperationType.Multiply:
                        // 处理负数相乘情况
                        if (left is UnaryOperationExpression leftUnary &&
                            leftUnary.OperationType == UnaryOperationType.Negate &&
                            right is UnaryOperationExpression rightUnary &&
                            rightUnary.OperationType == UnaryOperationType.Negate)
                        {
                            // (-a) * (-b) = a * b
                            var positive = new BinaryOperationExpression(
                                leftUnary.Operand,
                                rightUnary.Operand,
                                BinaryOperationType.Multiply
                            );
                            pendingOperations.Add((binOp, positive, "NegativeMultiplication"));
                            return ExpandExpression(positive);
                        }

                        // 处理负数乘以正数情况
                        if (left is UnaryOperationExpression leftNeg &&
                            leftNeg.OperationType == UnaryOperationType.Negate)
                        {
                            // (-a) * b = -(a * b)
                            var innerProduct = new BinaryOperationExpression(
                                leftNeg.Operand,
                                right,
                                BinaryOperationType.Multiply
                            );
                            var negProduct = new UnaryOperationExpression(
                                ExpandExpression(innerProduct),
                                UnaryOperationType.Negate
                            );
                            pendingOperations.Add((binOp, negProduct, "NegativeMultiplication"));
                            return negProduct;
                        }

                        if (right is UnaryOperationExpression rightNeg &&
                            rightNeg.OperationType == UnaryOperationType.Negate)
                        {
                            // a * (-b) = -(a * b)
                            var innerProduct = new BinaryOperationExpression(
                                left,
                                rightNeg.Operand,
                                BinaryOperationType.Multiply
                            );
                            var negProduct = new UnaryOperationExpression(
                                ExpandExpression(innerProduct),
                                UnaryOperationType.Negate
                            );
                            pendingOperations.Add((binOp, negProduct, "NegativeMultiplication"));
                            return negProduct;
                        }

                        // 表达式展开
                        var expanded = ExpandMultiplication(left, right);
                        if (expanded.ToString() != new BinaryOperationExpression(left, right, BinaryOperationType.Multiply).ToString())
                        {
                            pendingOperations.Add((binOp, expanded, "Distribution"));
                        }
                        return expanded;

                    case BinaryOperationType.Power:
                        if (right is NumberExpression exp)
                        {
                            if (exp.Value == 0)
                            {
                                var result = new NumberExpression(1);
                                pendingOperations.Add((binOp, result, "PowerRule"));
                                return result;
                            }
                            if (exp.Value == 1)
                            {
                                pendingOperations.Add((binOp, left, "PowerRule"));
                                return left;
                            }
                            // 处理 sqrt(exp)^2 的情况 (适配根式的平方运算)
                            if (Math.Abs(exp.Value - 2) < 1e-10)
                            {
                                // 处理 sqrt(expr)^2 = expr
                                if (left is FunctionExpression func && func.Name.ToLower() == "sqrt" && func.Arguments.Count == 1)
                                {
                                    var innerExpr = ExpandExpression(func.Arguments[0]);
                                    pendingOperations.Add((binOp, innerExpr, "SquareRootSquare"));
                                    return innerExpr;
                                }

                                // 处理 (-expr)^2 = expr^2
                                if (left is UnaryOperationExpression unary && unary.OperationType == UnaryOperationType.Negate)
                                {
                                    // (-a)^2 = a^2
                                    var innerSquared = new BinaryOperationExpression(
                                        unary.Operand,
                                        new NumberExpression(2),
                                        BinaryOperationType.Power
                                    );
                                    var expandedInner = ExpandExpression(innerSquared);
                                    pendingOperations.Add((binOp, expandedInner, "NegativeSquare"));
                                    return expandedInner;
                                }

                                // 处理 (a + b)^2 或 (a - b)^2 的展开
                                if (left is BinaryOperationExpression binLeft &&
                                    (binLeft.OperationType == BinaryOperationType.Add ||
                                     binLeft.OperationType == BinaryOperationType.Subtract))
                                {
                                    var expandedSquare = ExpandSquareOfSum(binLeft);
                                    pendingOperations.Add((binOp, expandedSquare, "ExpandSquare"));
                                    return expandedSquare;
                                }
                            }
                        }
                        return new BinaryOperationExpression(left, right, binOp.OperationType);
                    default:
                        return new BinaryOperationExpression(left, right, binOp.OperationType);
                }
            }
            else if (expr is UnaryOperationExpression unary)
            {
                var operand = ExpandExpression(unary.Operand);
                if (operand is NumberExpression num)
                {
                    var result = new NumberExpression(-num.Value);
                    pendingOperations.Add((unary, result, "UnaryNegation"));
                    return result;
                }
                return new UnaryOperationExpression(operand, unary.OperationType);
            }
            else if (expr is FunctionExpression func)
            {
                var args = func.Arguments.Select(arg => ExpandExpression(arg)).ToList();
                if (args.All(arg => arg is NumberExpression))
                {
                    var result = new NumberExpression(func.Evaluate());
                    pendingOperations.Add((func, result, "FunctionEvaluation"));
                    return result;
                }
                return new FunctionExpression(func.Name, args);
            }
            return expr;
        }

        /// <summary>
        /// 展开 (a + b)^2 或 (a - b)^2
        /// </summary>
        private IMathExpression ExpandSquareOfSum(BinaryOperationExpression expr)
        {
            var a = expr.Left;
            var b = expr.Right;

            // a^2
            var a2 = new BinaryOperationExpression(a, new NumberExpression(2), BinaryOperationType.Power);

            // b^2
            var b2 = new BinaryOperationExpression(b, new NumberExpression(2), BinaryOperationType.Power);

            // 2ab
            var two = new NumberExpression(2);
            var ab = new BinaryOperationExpression(a, b, BinaryOperationType.Multiply);
            var twoAb = new BinaryOperationExpression(two, ab, BinaryOperationType.Multiply);

            if (expr.OperationType == BinaryOperationType.Add)
            {
                // (a + b)^2 = a^2 + 2ab + b^2
                var a2Plus2ab = new BinaryOperationExpression(a2, twoAb, BinaryOperationType.Add);
                return new BinaryOperationExpression(a2Plus2ab, b2, BinaryOperationType.Add);
            }
            else
            {
                // (a - b)^2 = a^2 - 2ab + b^2
                var a2Minus2ab = new BinaryOperationExpression(a2, twoAb, BinaryOperationType.Subtract);
                return new BinaryOperationExpression(a2Minus2ab, b2, BinaryOperationType.Add);
            }
        }

        private IMathExpression ExpandMultiplication(IMathExpression left, IMathExpression right)
        {
            var leftTerms = GetAdditionTerms(left);
            var rightTerms = GetAdditionTerms(right);

            // 如果任一边只有一项, 不需要展开
            if (leftTerms.Count == 1 && rightTerms.Count == 1)
            {
                return new BinaryOperationExpression(left, right, BinaryOperationType.Multiply);
            }

            // 展开乘法
            var expandedTerms = new List<(IMathExpression expr, bool isNegative)>();
            foreach (var (leftExpr, leftNeg) in leftTerms)
            {
                foreach (var (rightExpr, rightNeg) in rightTerms)
                {
                    var product = new BinaryOperationExpression(leftExpr, rightExpr, BinaryOperationType.Multiply);
                    var isNegative = leftNeg ^ rightNeg;
                    expandedTerms.Add((product, isNegative));
                }
            }

            // 重建表达式
            return ReconstructFromTerms(expandedTerms);
        }

        private List<(IMathExpression expr, bool isNegative)> GetAdditionTerms(IMathExpression expr)
        {
            var terms = new List<(IMathExpression, bool)>();
            GetAdditionTermsRecursive(expr, terms, false);
            return terms;
        }

        private void GetAdditionTermsRecursive(IMathExpression expr, List<(IMathExpression, bool)> terms, bool isNegative)
        {
            if (expr is BinaryOperationExpression binOp)
            {
                if (binOp.OperationType == BinaryOperationType.Add)
                {
                    GetAdditionTermsRecursive(binOp.Left, terms, isNegative);
                    GetAdditionTermsRecursive(binOp.Right, terms, isNegative);
                }
                else if (binOp.OperationType == BinaryOperationType.Subtract)
                {
                    GetAdditionTermsRecursive(binOp.Left, terms, isNegative);
                    GetAdditionTermsRecursive(binOp.Right, terms, !isNegative);
                }
                else
                {
                    terms.Add((expr, isNegative));
                }
            }
            else if (expr is UnaryOperationExpression unary && unary.OperationType == UnaryOperationType.Negate)
            {
                GetAdditionTermsRecursive(unary.Operand, terms, !isNegative);
            }
            else
            {
                terms.Add((expr, isNegative));
            }
        }

        private IMathExpression ReconstructFromTerms(List<(IMathExpression expr, bool isNegative)> terms)
        {
            if (terms.Count == 0) return new NumberExpression(0);

            IMathExpression result = null;
            foreach (var (expr, isNegative) in terms)
            {
                if (result == null)
                {
                    result = isNegative ? new UnaryOperationExpression(expr, UnaryOperationType.Negate) : expr;
                }
                else
                {
                    if (isNegative)
                    {
                        result = new BinaryOperationExpression(result, expr, BinaryOperationType.Subtract);
                    }
                    else
                    {
                        result = new BinaryOperationExpression(result, expr, BinaryOperationType.Add);
                    }
                }
            }
            return result;
        }

        private List<Term> DecomposeFullExpression(IMathExpression expr)
        {
            return Decompose(expr);
        }

        private List<Term> Decompose(IMathExpression expr)
        {
            if (expr is BinaryOperationExpression binOp &&
                (binOp.OperationType == BinaryOperationType.Add ||
                 binOp.OperationType == BinaryOperationType.Subtract))
            {
                var terms = Decompose(binOp.Left);
                var rightTerms = Decompose(binOp.Right);
                if (binOp.OperationType == BinaryOperationType.Subtract)
                {
                    rightTerms.ForEach(t => t.Coefficient *= -1);
                }
                terms.AddRange(rightTerms);
                return terms;
            }
            return new List<Term> { Term.FromExpression(expr) };
        }

        private IEnumerable<Term> Combine(IEnumerable<Term> terms)
        {
            return terms.GroupBy(t => t.CanonicalVariablePart)
                        .Select(g => new Term(g.Sum(t => t.Coefficient), g.First().Factors))
                        .Where(t => t.Coefficient != 0);
        }

        private IMathExpression ReconstructWithoutSort(IEnumerable<Term> terms)
        {
            // 不排序, 按原始顺序重建
            return ReconstructTerms(terms.ToList());
        }

        private IMathExpression ReconstructWithSort(List<Term> sortedTerms)
        {
            // 使用已排序的项重建
            return ReconstructTerms(sortedTerms);
        }

        private IMathExpression ReconstructTerms(List<Term> terms)
        {
            if (!terms.Any())
            {
                return new NumberExpression(0);
            }

            IMathExpression finalExpr = null;
            foreach (var term in terms)
            {
                var termExpr = term.ToExpression();
                if (finalExpr == null)
                {
                    finalExpr = termExpr;
                }
                else
                {
                    if (term.Coefficient < 0)
                    {
                        var positiveTerm = new Term(-term.Coefficient, term.Factors);
                        finalExpr = new BinaryOperationExpression(
                            finalExpr,
                            positiveTerm.ToExpression(),
                            BinaryOperationType.Subtract);
                    }
                    else
                    {
                        finalExpr = new BinaryOperationExpression(
                            finalExpr,
                            termExpr,
                            BinaryOperationType.Add);
                    }
                }
            }
            return finalExpr;
        }

        private void AddStep(IMathExpression expression, string description)
        {
            if (steps.Count > 0 && steps.Last().ResultingExpression.ToString() == expression.ToString())
            {
                return;
            }
            steps.Add(new CalculationStep(expression, stepIndex++, description));
        }

        private string GenerateStepDescription()
        {
            if (pendingOperations.Count == 0)
            {
                return "展开表达式";
            }

            var op = pendingOperations[0];

            // 如果只有一个操作，返回其描述
            if (pendingOperations.Count == 1)
            {
                return op.original.GetExpandOperationDescription(op.result, op.operationType);
            }

            // 如果有多个操作，生成组合描述
            var operationTypes = pendingOperations.Select(op => op.operationType).Distinct().ToList();
            if (operationTypes.Count == 1)
            {
                // 所有操作类型相同
                return op.original.GetExpandOperationDescription( 
                                pendingOperations[0].result,
                                operationTypes[0]);
            }

            // 混合操作类型
            return "展开并化简表达式";
        }


        public IMathExpression Visit(NumberExpression number) => number;
        public IMathExpression Visit(VariableExpression variable) => variable;
        public IMathExpression Visit(UnaryOperationExpression unary) => unary;
        public IMathExpression Visit(FunctionExpression function) => function;
        public IMathExpression Visit(BinaryOperationExpression binary) => binary;
        public IMathExpression Visit(EquationExpression equation) => equation;

        public IMathExpression Visit(EquationSystemExpression system) => system;

        public IMathExpression Visit(MatrixExpression matrix) => matrix;

    }
}