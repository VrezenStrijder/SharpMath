using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SharpMath.Core
{
    /// <summary>
    /// 方程求解器
    /// </summary>
    public class EquationSolver : ISolver
    {
        private readonly string variableName;
        private readonly List<CalculationStep> steps;
        private int stepIndex;

        public EquationSolver(string variableName = null)
        {
            this.variableName = variableName;
            steps = new List<CalculationStep>();
            stepIndex = 0;
        }

        public CalculationResult Process(IMathExpression expression, SortOrder sortOrder = SortOrder.Normal)
        {
            if (!(expression is EquationExpression equation))
            {
                throw new ArgumentException("Expression must be an equation");
            }

            // 检测变量
            var detectedVariable = variableName ?? DetectVariable(equation);
            if (string.IsNullOrEmpty(detectedVariable))
            {
                throw new ArgumentException("表达式中不包含变量");
            }

            // 添加原始方程
            AddStep(equation, "原始方程");

            // 求解方程
            var solutions = SolveEquation(equation, detectedVariable);

            // 返回结果(对于多解的情况，返回第一个解作为主要结果)
            return new CalculationResult
            {
                OriginalExpression = expression,
                FinalExpression = solutions.First(),
                Steps = steps
            };
        }

        private List<EquationExpression> SolveEquation(EquationExpression equation, string variable)
        {
            // 检查是否包含根式
            if (ContainsSquareRoot(equation))
            {
                return SolveRadicalEquation(equation, variable);
            }

            // 标准化方程：移项到左边
            var standardized = StandardizeEquation(equation);

            // 化简左边
            var simplifier = new SimplificationVisitor(standardized.Left, SortOrder.Normal);
            var simplified = simplifier.Simplify(standardized.Left);
            if (simplified.ToString() != standardized.Left.ToString())
            {
                standardized = new EquationExpression(simplified, standardized.Right);
                AddStep(standardized, "化简表达式");
            }

            // 分析方程类型
            var degree = GetPolynomialDegree(simplified, variable);

            switch (degree)
            {
                case 1:
                    return new List<EquationExpression> { SolveLinearEquation(standardized, variable) };
                case 2:
                    return SolveQuadraticEquation(standardized, variable);
                default:
                    throw new NotSupportedException($"不支持求解{degree}次方程");
            }
        }

        /// <summary>
        /// 求解一元一次方程 ax + b = 0
        /// </summary>
        private EquationExpression SolveLinearEquation(EquationExpression equation, string variable)
        {
            var terms = DecomposeExpression(equation.Left);
            var variableTerms = terms.Where(t => t.Variables.Contains(variable)).ToList();
            var constantTerms = terms.Where(t => !t.Variables.Contains(variable)).ToList();

            var a = CalculateCoefficient(variableTerms, variable, 1);
            var b = constantTerms.Sum(t => t.Coefficient);

            if (Math.Abs(a) < 1e-10)
            {
                throw new InvalidOperationException("方程无解或有无穷多解");
            }

            // 步骤：ax + b = 0 → ax = -b
            if (Math.Abs(b) > 1e-10)
            {
                var leftExpr = ReconstructExpression(variableTerms);
                var rightExpr = new NumberExpression(-b);
                var newEquation = new EquationExpression(leftExpr, rightExpr);
                AddStep(newEquation, "移动常数项");
            }

            // 步骤：ax = -b → x = -b/a
            var solution = -b / a;
            var finalEquation = new EquationExpression(new VariableExpression(variable), new NumberExpression(solution));
            AddStep(finalEquation, $"两边同时除以 {a}");

            return finalEquation;
        }

        /// <summary>
        /// 求解一元二次方程 ax² + bx + c = 0
        /// </summary>
        private List<EquationExpression> SolveQuadraticEquation(EquationExpression equation, string variable)
        {
            var terms = DecomposeExpression(equation.Left);

            // 提取系数
            var a = CalculateCoefficient(terms, variable, 2);
            var b = CalculateCoefficient(terms, variable, 1);
            var c = CalculateCoefficient(terms, variable, 0);

            if (Math.Abs(a) < 1e-10)
            {
                // 退化为一次方程
                return new List<EquationExpression> { SolveLinearEquation(equation, variable) };
            }

            // 显示标准形式
            var standardForm = $"{a}*{variable}² + {b}*{variable} + {c} = 0";
            AddStep(equation, $"标准形式: {standardForm}");

            // 计算判别式
            var discriminant = b * b - 4 * a * c;
            AddStep(equation, $"判别式 Δ = b² - 4ac = {b}² - 4×{a}×{c} = {discriminant}");

            var solutions = new List<EquationExpression>();

            if (discriminant < -1e-10)
            {
                AddStep(equation, "Δ < 0，方程无实数解");
                throw new InvalidOperationException("方程无实数解");
            }
            else if (Math.Abs(discriminant) < 1e-10)
            {
                // 一个重根
                var x = -b / (2 * a);
                var solution = new EquationExpression(
                    new VariableExpression(variable),
                    new NumberExpression(x)
                );
                AddStep(solution, $"方程有一个重根: {variable} = -b/(2a) = {x}");
                solutions.Add(solution);
            }
            else
            {
                // 两个不同的根
                var sqrtDiscriminant = Math.Sqrt(discriminant);
                var x1 = (-b + sqrtDiscriminant) / (2 * a);
                var x2 = (-b - sqrtDiscriminant) / (2 * a);

                // 显示求根公式
                AddStep(equation, $"使用求根公式: {variable} = (-b ± √Δ) / (2a)");

                var solution1 = new EquationExpression(
                    new VariableExpression(variable + "₁"),
                    new NumberExpression(x1)
                );
                AddStep(solution1, $"{variable}₁ = (-{b} + √{discriminant}) / (2×{a}) = {x1}");
                solutions.Add(solution1);

                var solution2 = new EquationExpression(
                    new VariableExpression(variable + "₂"),
                    new NumberExpression(x2)
                );
                AddStep(solution2, $"{variable}₂ = (-{b} - √{discriminant}) / (2×{a}) = {x2}");
                solutions.Add(solution2);
            }

            return solutions;
        }

        /// <summary>
        /// 求解根式方程
        /// </summary>
        private List<EquationExpression> SolveRadicalEquation(EquationExpression equation, string variable)
        {
            AddStep(equation, "检测到根式方程");

            // 步骤1：隔离根式项
            var isolated = IsolateRadical(equation, variable);
            if (isolated != null && isolated.ToString() != equation.ToString())
            {
                AddStep(isolated, "隔离根式项");
            }

            // 步骤2：两边平方消除根式
            var squared = SquareBothSides(isolated ?? equation);
            AddStep(squared, "两边平方");

            // 步骤3：化简并求解
            var simplifier = new SimplificationVisitor(squared.Left, SortOrder.Normal);
            var simplifiedLeft = simplifier.Simplify(squared.Left);
            var simplifiedRight = simplifier.Simplify(squared.Right);

            var newEquation = new EquationExpression(simplifiedLeft, simplifiedRight);
            if (newEquation.ToString() != squared.ToString())
            {
                AddStep(newEquation, "化简");
            }

            // 递归求解(现在应该是多项式方程)
            var solutions = SolveEquation(newEquation, variable);

            // 步骤4：验证解(根式方程可能产生增根)
            AddStep(equation, "验证解(检查增根)");
            var validSolutions = new List<EquationExpression>();

            foreach (var solution in solutions)
            {
                if (solution.Right is NumberExpression numExpr)
                {
                    if (IsValidSolution(equation, variable, numExpr.Value))
                    {
                        validSolutions.Add(solution);
                        AddStep(solution, $"验证: {variable} = {numExpr.Value} 是有效解");
                    }
                    else
                    {
                        AddStep(solution, $"验证: {variable} = {numExpr.Value} 是增根(舍去)");
                    }
                }
            }

            if (validSolutions.Count == 0)
            {
                throw new InvalidOperationException("方程无有效解");
            }

            return validSolutions;
        }

        /// <summary>
        /// 标准化方程(移项到左边)
        /// </summary>
        private EquationExpression StandardizeEquation(EquationExpression equation)
        {
            if (IsZero(equation.Right))
            {
                return equation;
            }

            var leftSide = new BinaryOperationExpression(equation.Left, equation.Right, BinaryOperationType.Subtract);
            var standardized = new EquationExpression(leftSide, new NumberExpression(0));
            AddStep(standardized, "移项到左边");
            return standardized;
        }

        /// <summary>
        /// 获取多项式的次数
        /// </summary>
        private int GetPolynomialDegree(IMathExpression expr, string variable)
        {
            var terms = DecomposeExpression(expr);
            var variableTerms = terms.Where(t => t.Variables.Contains(variable));

            if (!variableTerms.Any())
            {
                return 0;
            }

            return (int)variableTerms.Max(t => t.GetPowerOf(variable));
        }

        /// <summary>
        /// 计算特定次数项的系数
        /// </summary>
        private double CalculateCoefficient(List<Term> terms, string variable, int degree)
        {
            return terms
                .Where(t => t.GetPowerOf(variable) == degree)
                .Sum(t => t.Coefficient);
        }

        /// <summary>
        /// 检查表达式是否包含平方根
        /// </summary>
        private bool ContainsSquareRoot(IMathExpression expr)
        {
            var visitor = new SquareRootDetector();
            expr.Accept(visitor);
            return visitor.HasSquareRoot;
        }

        /// <summary>
        /// 隔离根式项到方程一边
        /// </summary>
        private EquationExpression IsolateRadical(EquationExpression equation, string variable)
        {
            // 收集左边的项
            var leftCollector = new RadicalTermCollector();
            var leftTerms = equation.Left.Accept(leftCollector);

            // 收集右边的项
            var rightCollector = new RadicalTermCollector();
            var rightTerms = equation.Right.Accept(rightCollector);

            // 将所有项移到左边
            foreach (var term in rightTerms)
            {
                term.IsPositive = !term.IsPositive;
            }
            var allTerms = leftTerms.Concat(rightTerms).ToList();

            // 分离根式项和非根式项
            var radicalTerms = allTerms.Where(t => t.ContainsRadical).ToList();
            var nonRadicalTerms = allTerms.Where(t => !t.ContainsRadical).ToList();

            if (radicalTerms.Count == 0)
            {
                return equation; // 没有根式项
            }

            // 构建新的方程：根式项 = -非根式项
            IMathExpression leftSide = null;
            IMathExpression rightSide = null;

            // 构建左边(根式项)
            foreach (var term in radicalTerms)
            {
                if (leftSide == null)
                {
                    leftSide = term.IsPositive ? term.Expression :
                        new UnaryOperationExpression(term.Expression, UnaryOperationType.Negate);
                }
                else
                {
                    if (term.IsPositive)
                    {
                        leftSide = new BinaryOperationExpression(leftSide, term.Expression, BinaryOperationType.Add);
                    }
                    else
                    {
                        leftSide = new BinaryOperationExpression(leftSide, term.Expression, BinaryOperationType.Subtract);
                    }
                }
            }

            // 构建右边(非根式项的相反数)
            foreach (var term in nonRadicalTerms)
            {
                if (rightSide == null)
                {
                    rightSide = term.IsPositive ?
                        new UnaryOperationExpression(term.Expression, UnaryOperationType.Negate) :
                        term.Expression;
                }
                else
                {
                    if (term.IsPositive)
                    {
                        rightSide = new BinaryOperationExpression(rightSide, term.Expression, BinaryOperationType.Subtract);
                    }
                    else
                    {
                        rightSide = new BinaryOperationExpression(rightSide, term.Expression, BinaryOperationType.Add);
                    }
                }
            }

            // 如果只有根式项，右边为0
            if (rightSide == null)
            {
                rightSide = new NumberExpression(0);
            }

            // 化简右边
            var simplifier = new SimplificationVisitor(rightSide, SortOrder.Normal);
            rightSide = simplifier.Simplify(rightSide);

            return new EquationExpression(leftSide, rightSide);
        }


        /// <summary>
        /// 两边平方
        /// </summary>
        private EquationExpression SquareBothSides(EquationExpression equation)
        {
            var leftSquared = new BinaryOperationExpression(
                equation.Left,
                new NumberExpression(2),
                BinaryOperationType.Power
            );
            var rightSquared = new BinaryOperationExpression(
                equation.Right,
                new NumberExpression(2),
                BinaryOperationType.Power
            );
            return new EquationExpression(leftSquared, rightSquared);
        }

        /// <summary>
        /// 验证解是否有效(用于根式方程)
        /// </summary>
        private bool IsValidSolution(EquationExpression originalEquation, string variable, double value)
        {
            try
            {
                // 创建变量替换字典
                var substitutions = new Dictionary<string, double> { { variable, value } };
                var substitutor = new VariableSubstitutionVisitor(substitutions);

                // 替换变量
                var substitutedEquation = originalEquation.Accept(substitutor) as EquationExpression;

                // 计算左边和右边的值
                double leftValue = substitutedEquation.Left.Evaluate();
                double rightValue = substitutedEquation.Right.Evaluate();

                // 检查是否相等(考虑浮点数精度)
                bool isEqual = Math.Abs(leftValue - rightValue) < 1e-10;

                // 对于根式方程，还需要检查根式内的值是否非负
                if (isEqual && ContainsSquareRoot(originalEquation))
                {
                    // 检查所有根式内的表达式是否非负
                    var radicalChecker = new RadicalDomainChecker(substitutions);
                    originalEquation.Accept(radicalChecker);
                    return radicalChecker.IsValid;
                }

                return isEqual;
            }
            catch (Exception)
            {
                // 如果求值过程中出现错误(如除以0、负数开方等)，则不是有效解
                return false;
            }
        }


        private EquationExpression SolveForVariable(EquationExpression equation, string variable)
        {
            var currentEquation = equation;

            // 1：将所有项移到左边(右边变为0)
            if (!IsZero(currentEquation.Right))
            {
                var leftSide = new BinaryOperationExpression(
                    currentEquation.Left,
                    currentEquation.Right,
                    BinaryOperationType.Subtract
                );
                currentEquation = new EquationExpression(leftSide, new NumberExpression(0));
                AddStep(currentEquation, "移项到左边");
            }

            // 2：展开和化简左边
            var simplifier = new SimplificationVisitor(currentEquation.Left, SortOrder.Normal);
            var simplified = simplifier.Simplify(currentEquation.Left);
            if (simplified.ToString() != currentEquation.Left.ToString())
            {
                currentEquation = new EquationExpression(simplified, currentEquation.Right);
                AddStep(currentEquation, "化简表达式");
            }

            // 3：分离变量项和常数项
            var terms = DecomposeExpression(simplified);
            var variableTerms = terms.Where(t => t.Variables.Contains(variable)).ToList();
            var constantTerms = terms.Where(t => !t.Variables.Contains(variable)).ToList();

            // 计算变量系数和常数
            var coefficient = CalculateVariableCoefficient(variableTerms, variable);
            var constant = constantTerms.Sum(t => t.Coefficient);

            // 4：移动常数到右边
            if (Math.Abs(constant) > 1e-10)
            {
                var leftExpr = ReconstructExpression(variableTerms);
                var rightExpr = new NumberExpression(-constant);
                currentEquation = new EquationExpression(leftExpr, rightExpr);
                AddStep(currentEquation, "移动常数项到右边");
            }

            // 5：除以系数
            if (Math.Abs(coefficient - 1) > 1e-10 && Math.Abs(coefficient) > 1e-10)
            {
                var result = -constant / coefficient;
                currentEquation = new EquationExpression(
                    new VariableExpression(variable),
                    new NumberExpression(result)
                );
                AddStep(currentEquation, $"两边同时除以 {coefficient}");
            }
            else if (Math.Abs(coefficient) < 1e-10)
            {
                // 无解或无穷多解
                throw new InvalidOperationException("方程无解或有无穷多解");
            }

            return currentEquation;
        }

        private string DetectVariable(EquationExpression equation)
        {
            var visitor = new VariableDetector();
            equation.Accept(visitor);
            return visitor.Variables.FirstOrDefault();
        }

        private bool IsZero(IMathExpression expr)
        {
            return expr is NumberExpression num && Math.Abs(num.Value) < 1e-10;
        }

        private List<Term> DecomposeExpression(IMathExpression expr)
        {
            if (expr is BinaryOperationExpression binOp)
            {
                if (binOp.OperationType == BinaryOperationType.Add ||
                    binOp.OperationType == BinaryOperationType.Subtract)
                {
                    var leftTerms = DecomposeExpression(binOp.Left);
                    var rightTerms = DecomposeExpression(binOp.Right);

                    if (binOp.OperationType == BinaryOperationType.Subtract)
                    {
                        rightTerms.ForEach(t => t.Coefficient *= -1);
                    }

                    leftTerms.AddRange(rightTerms);
                    return leftTerms;
                }
            }

            return new List<Term> { Term.FromExpression(expr) };
        }

        private double CalculateVariableCoefficient(List<Term> variableTerms, string variable)
        {
            double coefficient = 0;
            foreach (var term in variableTerms)
            {
                // 简单情况：ax 形式
                if (term.Degree == 1 && term.GetPowerOf(variable) == 1)
                {
                    coefficient += term.Coefficient;
                }
                //Todo: 更复杂的情况需要更多处理
            }
            return coefficient;
        }

        private IMathExpression ReconstructExpression(List<Term> terms)
        {
            if (!terms.Any()) return new NumberExpression(0);

            IMathExpression result = null;
            foreach (var term in terms)
            {
                var termExpr = term.ToExpression();
                if (result == null)
                {
                    result = termExpr;
                }
                else
                {
                    if (term.Coefficient < 0)
                    {
                        var positiveTerm = new Term(-term.Coefficient, term.Factors);
                        result = new BinaryOperationExpression(
                            result,
                            positiveTerm.ToExpression(),
                            BinaryOperationType.Subtract
                        );
                    }
                    else
                    {
                        result = new BinaryOperationExpression(
                            result,
                            termExpr,
                            BinaryOperationType.Add
                        );
                    }
                }
            }
            return result ?? new NumberExpression(0);
        }

        private void AddStep(EquationExpression equation, string description)
        {
            steps.Add(new CalculationStep(equation, stepIndex++, description));
        }


    }

}
