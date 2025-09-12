using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static SharpMath.Core.MatrixSolver;

namespace SharpMath.Core
{
    /// <summary>
    /// 矩阵求解器
    /// </summary>
    public class MatrixSolver : ISolver
    {
        private readonly List<MatrixOperationItem> operations;
        private readonly List<IMathExpression> expressions;
        private readonly bool isSingleOperation;

        public MatrixSolver(MatrixOperation operation, int powerExponent = 2)
        {
            this.operations = new List<MatrixOperationItem> { new MatrixOperationItem(operation, powerValue: powerExponent) };
            this.expressions = new List<IMathExpression>();
            this.isSingleOperation = true;
        }

        public MatrixSolver(MatrixOperationItem operation)
        {
            this.operations = new List<MatrixOperationItem> { operation };
            this.expressions = new List<IMathExpression>();
            this.isSingleOperation = true;
        }

        public MatrixSolver(List<MatrixOperationItem> operations, List<IMathExpression> expressions)
        {
            this.operations = operations;
            this.expressions = expressions;
            this.isSingleOperation = false;
        }

        public CalculationResult Process(IMathExpression expression = null, SortOrder sortOrder = SortOrder.Normal)
        {
            if (isSingleOperation)
            {
                // 单矩阵运算
                return ProcessSingleOperation(expression);
            }
            else
            {
                // 多矩阵运算
                return ProcessMultipleOperations();
            }
        }

        private CalculationResult ProcessSingleOperation(IMathExpression expression)
        {
            var steps = new List<CalculationStep>();
            IMathExpression result = null;

            if (expression is MatrixExpression matrix)
            {
                steps.Add(new CalculationStep(matrix, 0, "原始矩阵", false));
                var operation = operations[0];

                switch (operation.Type)
                {
                    case MatrixOperation.Determinant:
                        var det = MatrixOperations.Determinant(matrix);
                        result = new NumberExpression(det);
                        steps.Add(new CalculationStep(result, 1, $"行列式: det({matrix.Name}) = {det}", false));
                        break;

                    case MatrixOperation.Transpose:
                        result = MatrixOperations.Transpose(matrix);
                        steps.Add(new CalculationStep(result, 1, $"转置矩阵: {matrix.Name}ᵀ", false));
                        break;

                    case MatrixOperation.Trace:
                        var trace = MatrixOperations.Trace(matrix);
                        result = new NumberExpression(trace);
                        steps.Add(new CalculationStep(result, 1, $"迹: Tr({matrix.Name}) = {trace}", false));
                        break;

                    case MatrixOperation.Rank:
                        var rank = MatrixOperations.Rank(matrix);
                        result = new NumberExpression(rank);
                        steps.Add(new CalculationStep(result, 1, $"秩: rank({matrix.Name}) = {rank}", false));
                        break;

                    case MatrixOperation.Inverse:
                        try
                        {
                            result = MatrixOperations.Inverse(matrix);
                            steps.Add(new CalculationStep(result, 1, $"逆: {matrix.Name}⁻¹", false));
                        }
                        catch (InvalidOperationException ex)
                        {
                            result = new VariableExpression("不可逆");
                            steps.Add(new CalculationStep(result, 1, ex.Message, false));
                        }
                        break;

                    case MatrixOperation.Power:
                        result = MatrixOperations.Power(matrix, operation.PowerValue ?? 2);
                        steps.Add(new CalculationStep(result, 1, $"A^{operation.PowerValue ?? 2}", false));
                        break;

                    case MatrixOperation.ScalarMultiply:
                        if (operation.ScalarValue.HasValue)
                        {
                            result = MatrixOperations.ScalarMultiply(matrix, operation.ScalarValue.Value);
                            steps.Add(new CalculationStep(result, 1, $"{operation.ScalarValue.Value} × A", false));
                        }
                        break;
                }
            }

            return new CalculationResult
            {
                OriginalExpression = expression,
                FinalExpression = result ?? expression,
                Steps = steps
            };
        }

        private CalculationResult ProcessMultipleOperations()
        {
            var steps = new List<CalculationStep>();

            if (expressions.Count == 0)
            {
                throw new ArgumentException("没有提供矩阵表达式");
            }

            // 添加原始矩阵
            for (int i = 0; i < expressions.Count; i++)
            {
                if (expressions[i] is MatrixExpression matrix)
                {
                    steps.Add(new CalculationStep(matrix, steps.Count, $"矩阵{(string.IsNullOrEmpty(matrix.Name) ? (i + 1).ToString() : matrix.Name)}", false));
                }
            }

            // 存储操作结果
            var operationResults = new List<IMathExpression>();

            // 执行运算
            for (int i = 0; i < operations.Count; i++)
            {
                var operation = operations[i];
                IMathExpression operationResult = null;
                string description = "";

                try
                {
                    // 获取操作数
                    IMathExpression leftOperand = GetOperand(operation.LeftOperandSource, expressions, operationResults);
                    IMathExpression rightOperand = null;

                    if (operation.RightOperandSource != null)
                    {
                        rightOperand = GetOperand(operation.RightOperandSource, expressions, operationResults);
                    }

                    // 执行操作
                    switch (operation.Type)
                    {
                        // 单操作数运算
                        case MatrixOperation.Determinant:
                            if (leftOperand is MatrixExpression m1)
                            {
                                var det = MatrixOperations.Determinant(m1);
                                operationResult = new NumberExpression(det);
                                description = $"计算行列式 det({(operationResult as MatrixExpression)?.Name}) = {det}";
                            }
                            break;

                        case MatrixOperation.Transpose:
                            if (leftOperand is MatrixExpression m2)
                            {
                                operationResult = MatrixOperations.Transpose(m2);
                                description = $"转置 ({(operationResult as MatrixExpression)?.Name}ᵀ)";
                            }
                            break;

                        case MatrixOperation.Inverse:
                            if (leftOperand is MatrixExpression m3)
                            {
                                operationResult = MatrixOperations.Inverse(m3);
                                description = $"求逆 ({(operationResult as MatrixExpression)?.Name}⁻¹) ";
                            }
                            break;

                        case MatrixOperation.Trace:
                            if (leftOperand is MatrixExpression m4)
                            {
                                var trace = MatrixOperations.Trace(m4);
                                operationResult = new NumberExpression(trace);
                                description = $"计算迹 Tr({(operationResult as MatrixExpression)?.Name})= {trace}";
                            }
                            break;

                        case MatrixOperation.Rank:
                            if (leftOperand is MatrixExpression m5)
                            {
                                var rank = MatrixOperations.Rank(m5);
                                operationResult = new NumberExpression(rank);
                                description = $"计算秩 rank({(operationResult as MatrixExpression)?.Name}) = {rank}";
                            }
                            break;

                        case MatrixOperation.Power:
                            if (leftOperand is MatrixExpression m6)
                            {
                                var power = operation.PowerValue ?? 2;
                                operationResult = MatrixOperations.Power(m6, power);
                                description = $"计算 {power} 次幂 ({(operationResult as MatrixExpression)?.Name}^{power})";
                            }
                            break;

                        case MatrixOperation.ScalarMultiply:
                            if (leftOperand is MatrixExpression m7 && operation.ScalarValue.HasValue)
                            {
                                operationResult = MatrixOperations.ScalarMultiply(m7, operation.ScalarValue.Value);
                                description = $"数乘 ({operation.ScalarValue.Value} × {(operationResult as MatrixExpression)?.Name})";
                            }
                            break;

                        // 双操作数运算
                        case MatrixOperation.Add:
                            if (leftOperand is MatrixExpression left1 && rightOperand is MatrixExpression right1)
                            {
                                operationResult = MatrixOperations.Add(left1, right1);
                                description = $"加法 ({(operationResult as MatrixExpression)?.Name})";
                            }
                            break;

                        case MatrixOperation.Subtract:
                            if (leftOperand is MatrixExpression left2 && rightOperand is MatrixExpression right2)
                            {
                                operationResult = MatrixOperations.Subtract(left2, right2);
                                description = $"减法 ({(operationResult as MatrixExpression)?.Name})";
                            }
                            break;

                        case MatrixOperation.Multiply:
                            if (leftOperand is MatrixExpression left3 && rightOperand is MatrixExpression right3)
                            {
                                operationResult = MatrixOperations.Multiply(left3, right3);
                                description = $"乘法 ({(operationResult as MatrixExpression)?.Name})";
                            }
                            break;

                        case MatrixOperation.HadamardProduct:
                            if (leftOperand is MatrixExpression left4 && rightOperand is MatrixExpression right4)
                            {
                                operationResult = MatrixOperations.HadamardProduct(left4, right4);
                                description = $"Hadamard积 ({(operationResult as MatrixExpression)?.Name})";
                            }
                            break;
                    }

                    if (operationResult != null)
                    {
                        operationResults.Add(operationResult);
                        steps.Add(new CalculationStep(operationResult, steps.Count, description, false));
                    }
                }
                catch (Exception ex)
                {
                    steps.Add(new CalculationStep(
                        new VariableExpression($"错误: {ex.Message}"),
                        steps.Count,
                        $"运算失败: {operation.Type}"
                    ));
                    break;
                }
            }

            // 最终结果是最后一个操作的结果
            var finalResult = operationResults.Count > 0 ? operationResults.Last() : expressions.FirstOrDefault();

            return new CalculationResult
            {
                OriginalExpression = expressions[0],
                FinalExpression = finalResult,
                Steps = steps
            };
        }

        /// <summary>
        /// 根据操作数来源获取操作数
        /// </summary>
        private IMathExpression GetOperand(OperandSource source, List<IMathExpression> expressions, List<IMathExpression> results)
        {
            if (source == null)
            {
                throw new ArgumentException("操作数来源不能为空");
            }

            switch (source.Type)
            {
                case OperandSourceType.Expression:
                    if (source.Index < 0 || source.Index >= expressions.Count)
                    {
                        throw new IndexOutOfRangeException($"表达式索引 {source.Index} 超出范围");
                    }
                    return expressions[source.Index];

                case OperandSourceType.Result:
                    if (source.Index < 0 || source.Index >= results.Count)
                    {
                        throw new IndexOutOfRangeException($"结果索引 {source.Index} 超出范围");
                    }
                    return results[source.Index];

                default:
                    throw new ArgumentException($"未知的操作数来源类型: {source.Type}");
            }
        }

    }

    public class MatrixOperationItem
    {
        public MatrixOperationItem(MatrixOperation type, double? scalarValue = null, int? powerValue = null)
        {
            Type = type;
            ScalarValue = scalarValue;
            PowerValue = powerValue;
        }

        public MatrixOperation Type { get; set; }
        public double? ScalarValue { get; set; } // 用于标量乘法
        public int? PowerValue { get; set; }     // 用于幂运算

        public OperandSource LeftOperandSource { get; set; }

        public OperandSource RightOperandSource { get; set; }

    }

    public class OperandSource
    {
        public OperandSource(OperandSourceType type, int index)
        {
            Type = type;
            Index = index;
        }

        public OperandSourceType Type { get; set; }
        public int Index { get; set; } // 表达式列表中的索引或操作结果的索引
    }

    public enum OperandSourceType
    {
        Expression,  // 来自原始表达式列表
        Result      // 来自之前操作的结果
    }


    public enum MatrixOperation
    {
        // 单矩阵运算
        Determinant,
        Transpose,
        Trace,
        Rank,
        Inverse,
        Power,
        // 矩阵间运算
        Add,
        Subtract,
        Multiply,        // 矩阵乘法(叉乘)
        HadamardProduct, // 点乘
        // 标量运算
        ScalarMultiply,  // 数乘
    }
}
