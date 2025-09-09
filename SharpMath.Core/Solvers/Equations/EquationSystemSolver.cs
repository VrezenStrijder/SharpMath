using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SharpMath.Core
{

    /// <summary>
    /// 方程组求解器
    /// </summary>
    public class EquationSystemSolver : ISolver
    {
        private readonly List<CalculationStep> steps;
        private int stepIndex;

        public EquationSystemSolver()
        {
            steps = new List<CalculationStep>();
            stepIndex = 0;
        }

        public CalculationResult Process(IMathExpression expression, SortOrder sortOrder = SortOrder.Normal)
        {
            if (!(expression is EquationSystemExpression system))
            {
                throw new ArgumentException("Expression must be an equation system");
            }

            AddStep(system, "原始方程组");

            // 将方程组转换为线性方程组
            var linearSystem = ConvertToLinearSystem(system);

            if (linearSystem == null)
            {
                throw new NotSupportedException("目前只支持线性方程组");
            }

            // 使用高斯消元法求解
            var solution = SolveLinearSystem(linearSystem);

            return new CalculationResult
            {
                OriginalExpression = expression,
                FinalExpression = solution,
                Steps = steps
            };
        }

        private LinearSystem ConvertToLinearSystem(EquationSystemExpression system)
        {
            var variables = system.Variables.OrderBy(v => v).ToList();
            var n = system.Equations.Count;
            var m = variables.Count;

            if (n == 0 || m == 0) return null;

            var coefficients = new double[n, m];
            var constants = new double[n];

            for (int i = 0; i < n; i++)
            {
                var equation = system.Equations[i];

                // 标准化方程(移项到左边)
                var leftExpr = new BinaryOperationExpression(equation.Left, equation.Right, BinaryOperationType.Subtract);

                // 化简
                var simplifier = new SimplificationVisitor(leftExpr, SortOrder.Normal);
                var simplified = simplifier.Simplify(leftExpr);

                // 提取系数
                var terms = DecomposeExpression(simplified);

                foreach (var term in terms)
                {
                    if (term.IsConstant)
                    {
                        constants[i] = -term.Coefficient;
                    }
                    else if (term.Degree == 1 && term.Variables.Count == 1)
                    {
                        var varName = term.Variables.First();
                        var varIndex = variables.IndexOf(varName);
                        if (varIndex >= 0)
                        {
                            coefficients[i, varIndex] = term.Coefficient;

                        }
                    }
                    else
                    {
                        // 非线性项
                        return null;
                    }
                }
            }

            return new LinearSystem
            {
                Coefficients = coefficients,
                Constants = constants,
                Variables = variables
            };
        }

        /// <summary>
        /// 求解线性方程组(高斯消元法)
        /// </summary>
        private IMathExpression SolveLinearSystem(LinearSystem system)
        {
            var n = system.Coefficients.GetLength(0); // 方程数
            var m = system.Coefficients.GetLength(1); // 变量数

            // 构造增广矩阵
            var augmented = new double[n, m + 1];
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < m; j++)
                {
                    augmented[i, j] = system.Coefficients[i, j];
                }
                augmented[i, m] = system.Constants[i];
            }

            AddStep(CreateAugmentedMatrixExpression(augmented, system.Variables), "构造增广矩阵");

            // 高斯消元
            int rank = 0;
            for (int col = 0; col < m && rank < n; col++)
            {
                // 找主元
                int pivotRow = -1;
                double maxVal = 0;
                for (int row = rank; row < n; row++)
                {
                    if (Math.Abs(augmented[row, col]) > maxVal)
                    {
                        maxVal = Math.Abs(augmented[row, col]);
                        pivotRow = row;
                    }
                }

                if (maxVal < 1e-10) continue; // 该列全为0

                // 交换行
                if (pivotRow != rank)
                {
                    for (int j = 0; j <= m; j++)
                    {
                        (augmented[rank, j], augmented[pivotRow, j]) = (augmented[pivotRow, j], augmented[rank, j]);
                    }
                    AddStep(CreateAugmentedMatrixExpression(augmented, system.Variables), $"交换第{rank + 1}行和第{pivotRow + 1}行");
                }

                // 归一化主元行
                double pivot = augmented[rank, col];
                for (int j = 0; j <= m; j++)
                {
                    augmented[rank, j] /= pivot;
                }
                AddStep(CreateAugmentedMatrixExpression(augmented, system.Variables), $"第{rank + 1}行除以{pivot:F3}");

                // 消元
                for (int row = 0; row < n; row++)
                {
                    if (row != rank && Math.Abs(augmented[row, col]) > 1e-10)
                    {
                        double factor = augmented[row, col];
                        for (int j = 0; j <= m; j++)
                        {
                            augmented[row, j] -= factor * augmented[rank, j];
                        }
                    }
                }
                AddStep(CreateAugmentedMatrixExpression(augmented, system.Variables), $"消去第{col + 1}列的其他元素");

                rank++;
            }

            // 检查解的情况
            var solution = AnalyzeSolution(augmented, system.Variables, rank);
            return solution;
        }

        private IMathExpression AnalyzeSolution(double[,] augmented, List<string> variables, int rank)
        {
            int n = augmented.GetLength(0);
            int m = augmented.GetLength(1) - 1;

            // 检查是否有矛盾(0 = 非0)
            for (int i = rank; i < n; i++)
            {
                bool allZero = true;
                for (int j = 0; j < m; j++)
                {
                    if (Math.Abs(augmented[i, j]) > 1e-10)
                    {
                        allZero = false;
                        break;
                    }
                }
                if (allZero && Math.Abs(augmented[i, m]) > 1e-10)
                {
                    AddStep(new VariableExpression("无解"), "方程组无解(存在矛盾)");
                    throw new InvalidOperationException("方程组无解");
                }
            }

            // 检查是否有唯一解
            if (rank == m)
            {
                // 唯一解
                var solutions = new List<EquationExpression>();
                for (int i = 0; i < m; i++)
                {
                    var varExpr = new VariableExpression(variables[i]);
                    var valueExpr = new NumberExpression(augmented[i, m]);
                    solutions.Add(new EquationExpression(varExpr, valueExpr));
                }

                var result = new EquationSystemExpression(solutions);
                AddStep(result, "方程组的解");
                return result;
            }
            else
            {
                // 无穷多解
                AddStep(new VariableExpression("无穷多解"), $"方程组有无穷多解(秩={rank} < 变量数={m})");

                // 表示通解
                var freeVars = new List<string>();
                var basicVars = new List<(string var, IMathExpression expr)>();

                // 识别自由变量
                var pivotCols = new bool[m];
                for (int row = 0; row < rank; row++)
                {
                    for (int col = 0; col < m; col++)
                    {
                        if (Math.Abs(augmented[row, col] - 1) < 1e-10)
                        {
                            pivotCols[col] = true;
                            break;
                        }
                    }
                }

                for (int i = 0; i < m; i++)
                {
                    if (!pivotCols[i])
                    {
                        freeVars.Add(variables[i]);
                    }
                }

                // 构造通解
                var generalSolution = new List<EquationExpression>();
                for (int row = 0; row < rank; row++)
                {
                    int pivotCol = -1;
                    for (int col = 0; col < m; col++)
                    {
                        if (Math.Abs(augmented[row, col] - 1) < 1e-10)
                        {
                            pivotCol = col;
                            break;
                        }
                    }

                    if (pivotCol >= 0)
                    {
                        // 使用 IMathExpression 类型
                        IMathExpression expr = new NumberExpression(augmented[row, m]);

                        // 减去自由变量的贡献
                        for (int col = pivotCol + 1; col < m; col++)
                        {
                            if (Math.Abs(augmented[row, col]) > 1e-10)
                            {
                                var coeff = augmented[row, col];
                                var varExpr = new VariableExpression(variables[col]);
                                IMathExpression term;

                                if (Math.Abs(coeff - 1) < 1e-10)
                                {
                                    // 系数为1，直接使用变量
                                    term = varExpr;
                                }
                                else if (Math.Abs(coeff + 1) < 1e-10)
                                {
                                    // 系数为-1，使用负号
                                    term = new UnaryOperationExpression(varExpr, UnaryOperationType.Negate);
                                }
                                else
                                {
                                    // 其他系数
                                    term = new BinaryOperationExpression(new NumberExpression(Math.Abs(coeff)), varExpr, BinaryOperationType.Multiply
                                    );

                                    if (coeff < 0)
                                    {
                                        term = new UnaryOperationExpression(term, UnaryOperationType.Negate);
                                    }
                                }

                                // 从expr中减去这一项
                                expr = new BinaryOperationExpression(expr, term, BinaryOperationType.Subtract);
                            }
                        }

                        generalSolution.Add(new EquationExpression(new VariableExpression(variables[pivotCol]), expr));
                    }
                }

                // 添加自由变量
                foreach (var freeVar in freeVars)
                {
                    generalSolution.Add(new EquationExpression(
                        new VariableExpression(freeVar),
                        new VariableExpression(freeVar + " (自由)")
                    ));
                }

                return new EquationSystemExpression(generalSolution);
            }
        }

        private IMathExpression CreateAugmentedMatrixExpression(double[,] augmented, List<string> variables)
        {
            // 创建一个表示增广矩阵状态的方程组
            int n = augmented.GetLength(0);
            int m = augmented.GetLength(1) - 1;

            var equations = new List<EquationExpression>();

            for (int i = 0; i < n; i++)
            {
                IMathExpression leftExpr = null;
                bool hasTerms = false;

                for (int j = 0; j < m; j++)
                {
                    if (Math.Abs(augmented[i, j]) > 1e-10)
                    {
                        var coeff = augmented[i, j];
                        var varExpr = new VariableExpression(variables[j]);
                        IMathExpression term;

                        if (Math.Abs(coeff - 1) < 1e-10)
                        {
                            term = varExpr;
                        }
                        else if (Math.Abs(coeff + 1) < 1e-10)
                        {
                            term = new UnaryOperationExpression(varExpr, UnaryOperationType.Negate);
                        }
                        else
                        {
                            term = new BinaryOperationExpression(new NumberExpression(Math.Abs(coeff)), varExpr, BinaryOperationType.Multiply);

                            if (coeff < 0)
                            {
                                term = new UnaryOperationExpression(term, UnaryOperationType.Negate);
                            }
                        }

                        if (leftExpr == null)
                        {
                            leftExpr = term;
                        }
                        else
                        {
                            if (coeff > 0 && hasTerms)
                            {
                                leftExpr = new BinaryOperationExpression(leftExpr, term, BinaryOperationType.Add);
                            }
                            else
                            {
                                leftExpr = new BinaryOperationExpression(leftExpr, term, BinaryOperationType.Add);
                            }
                        }
                        hasTerms = true;
                    }
                }

                if (leftExpr == null)
                {
                    leftExpr = new NumberExpression(0);
                }

                var rightExpr = new NumberExpression(augmented[i, m]);
                equations.Add(new EquationExpression(leftExpr, rightExpr));
            }

            return new EquationSystemExpression(equations);
        }
        private List<Term> DecomposeExpression(IMathExpression expr)
        {
            // 复用之前的实现
            if (expr is BinaryOperationExpression binOp &&
                (binOp.OperationType == BinaryOperationType.Add ||
                 binOp.OperationType == BinaryOperationType.Subtract))
            {
                var terms = DecomposeExpression(binOp.Left);
                var rightTerms = DecomposeExpression(binOp.Right);
                if (binOp.OperationType == BinaryOperationType.Subtract)
                {
                    rightTerms.ForEach(t => t.Coefficient *= -1);
                }
                terms.AddRange(rightTerms);
                return terms;
            }
            return new List<Term> { Term.FromExpression(expr) };
        }

        private void AddStep(IMathExpression expression, string description)
        {
            steps.Add(new CalculationStep(expression, stepIndex++, description));
        }

        private class LinearSystem
        {
            public double[,] Coefficients { get; set; }
            public double[] Constants { get; set; }
            public List<string> Variables { get; set; }
        }
    }

}
