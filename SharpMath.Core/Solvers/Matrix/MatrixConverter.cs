using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace SharpMath.Core
{


    public class MatrixConverter
    {
        private readonly List<MatrixModel> matrixList;

        public MatrixConverter(List<MatrixModel> matrixList)
        {
            this.matrixList = matrixList;
        }

        public MatrixConverter(Dictionary<string, string> matrixDict)
        {
            this.matrixList = new List<MatrixModel>();
            foreach (var item in matrixDict)
            {
                this.matrixList.Add(new MatrixModel(item.Key, item.Value));
            }
        }


        /// <summary>
        /// 将矩阵表达式字符串转换为可执行的操作序列
        /// </summary>
        public (List<IMathExpression> expressions, List<MatrixOperationItem> operations) ConvertToExpressions(string expression)
        {
            // 1. 解析表达式
            var elements = MatrixFormatter.ParseExpression(expression);

            // 2. 转换为后缀表达式(逆波兰表示法)
            var rpnElements = ConvertToRPN(elements);

            // 3. 从RPN构建执行序列
            return BuildExecutionSequenceWithSources(rpnElements);
        }

        /// <summary>
        /// 将中缀表达式转换为后缀表达式(RPN)
        /// </summary>
        private List<MatrixExpressionElement> ConvertToRPN(List<MatrixExpressionElement> elements)
        {
            var output = new List<MatrixExpressionElement>();
            var operatorStack = new Stack<MatrixExpressionElement>();

            foreach (var element in elements)
            {
                switch (element.Type)
                {
                    case MatrixElementType.Variable:
                    case MatrixElementType.Number:
                        output.Add(element);
                        break;

                    case MatrixElementType.Function:
                        operatorStack.Push(element);
                        break;

                    case MatrixElementType.Operator:
                        while (operatorStack.Count > 0 &&
                               operatorStack.Peek().Type == MatrixElementType.Operator)
                        {
                            var topOp = operatorStack.Peek();
                            var currentPrecedence = GetPrecedence(element.Value);
                            var topPrecedence = GetPrecedence(topOp.Value);

                            if (topPrecedence >= currentPrecedence)
                            {
                                output.Add(operatorStack.Pop());
                            }
                            else
                            {
                                break;
                            }
                        }
                        operatorStack.Push(element);
                        break;

                    case MatrixElementType.LeftParen:
                        operatorStack.Push(element);
                        break;

                    case MatrixElementType.RightParen:
                        while (operatorStack.Count > 0 &&
                               operatorStack.Peek().Type != MatrixElementType.LeftParen)
                        {
                            output.Add(operatorStack.Pop());
                        }

                        if (operatorStack.Count > 0)
                        {
                            operatorStack.Pop(); // 移除左括号
                        }

                        // 如果栈顶是函数，也要弹出
                        if (operatorStack.Count > 0 &&
                            operatorStack.Peek().Type == MatrixElementType.Function)
                        {
                            output.Add(operatorStack.Pop());
                        }
                        break;
                }
            }

            // 弹出剩余的运算符
            while (operatorStack.Count > 0)
            {
                output.Add(operatorStack.Pop());
            }

            return output;
        }

        #region 从RPN构建执行序列(包含操作数来源)

        /// <summary>
        /// 从RPN构建执行序列 - 带操作数来源信息
        /// </summary>
        private (List<IMathExpression> expressions, List<MatrixOperationItem> operations) BuildExecutionSequenceWithSources(List<MatrixExpressionElement> rpnElements)
        {
            var evaluationStack = new Stack<StackItem>();
            var expressions = new List<IMathExpression>();
            var operations = new List<MatrixOperationItem>();
            var matrixNameToIndex = new Dictionary<string, int>(); // 矩阵名称 -> 表达式索引

            foreach (var element in rpnElements)
            {
                switch (element.Type)
                {
                    case MatrixElementType.Variable:
                        // 如果矩阵还没有添加，添加它
                        if (!matrixNameToIndex.ContainsKey(element.Value))
                        {
                            var matrix = GetMatrixExpression(element.Value);
                            expressions.Add(matrix);
                            matrixNameToIndex[element.Value] = expressions.Count - 1;
                        }

                        evaluationStack.Push(new StackItem
                        {
                            Type = StackItemType.Matrix,
                            Source = new OperandSource(OperandSourceType.Expression, matrixNameToIndex[element.Value])
                        });
                        break;

                    case MatrixElementType.Number:
                        evaluationStack.Push(new StackItem
                        {
                            Type = StackItemType.Scalar,
                            ScalarValue = double.Parse(element.Value)
                        });
                        break;

                    case MatrixElementType.Operator:
                        ProcessOperatorWithSource(element.Value, evaluationStack, expressions, operations);
                        break;

                    case MatrixElementType.Function:
                        ProcessFunctionWithSource(element.Value, evaluationStack, expressions, operations);
                        break;
                }
            }

            return (expressions, operations);
        }


        /// <summary>
        /// 处理运算符 - 带操作数来源
        /// </summary>
        private void ProcessOperatorWithSource(string op, Stack<StackItem> stack, List<IMathExpression> expressions, List<MatrixOperationItem> operations)
        {
            if (stack.Count < 2)
            {
                throw new InvalidOperationException($"运算符 {op} 需要两个操作数");
            }

            var right = stack.Pop();
            var left = stack.Pop();

            // 特殊处理数乘
            if (op == "×" && (left.Type == StackItemType.Scalar || right.Type == StackItemType.Scalar))
            {
                StackItem matrixItem;
                double scalarValue;

                if (left.Type == StackItemType.Scalar)
                {
                    scalarValue = left.ScalarValue;
                    matrixItem = right;
                }
                else
                {
                    scalarValue = right.ScalarValue;
                    matrixItem = left;
                }

                var operation = new MatrixOperationItem(MatrixOperation.ScalarMultiply, scalarValue)
                {
                    LeftOperandSource = matrixItem.Source
                };

                operations.Add(operation);

                // 结果压栈
                stack.Push(new StackItem
                {
                    Type = StackItemType.Result,
                    Source = new OperandSource(OperandSourceType.Result, operations.Count - 1)
                });
            }
            else if (op == "^" && right.Type == StackItemType.Scalar)
            {
                // 幂运算
                var power = (int)right.ScalarValue;

                var operation = new MatrixOperationItem(MatrixOperation.Power, powerValue: power)
                {
                    LeftOperandSource = left.Source
                };

                operations.Add(operation);

                stack.Push(new StackItem
                {
                    Type = StackItemType.Result,
                    Source = new OperandSource(OperandSourceType.Result, operations.Count - 1)
                });
            }
            else
            {
                // 普通二元运算
                if (left.Type == StackItemType.Scalar || right.Type == StackItemType.Scalar)
                {
                    throw new InvalidOperationException($"运算符 {op} 需要两个矩阵操作数");
                }

                var matrixOp = ConvertToMatrixOperation(op);
                var operation = new MatrixOperationItem(matrixOp)
                {
                    LeftOperandSource = left.Source,
                    RightOperandSource = right.Source
                };

                operations.Add(operation);

                stack.Push(new StackItem
                {
                    Type = StackItemType.Result,
                    Source = new OperandSource(OperandSourceType.Result, operations.Count - 1)
                });
            }
        }

        /// <summary>
        /// 处理函数 - 带操作数来源
        /// </summary>
        private void ProcessFunctionWithSource(string func, Stack<StackItem> stack, List<IMathExpression> expressions, List<MatrixOperationItem> operations)
        {
            if (stack.Count < 1)
            {
                throw new InvalidOperationException($"函数 {func} 需要一个参数");
            }

            var operand = stack.Pop();

            if (operand.Type == StackItemType.Scalar)
            {
                throw new InvalidOperationException($"函数 {func} 需要矩阵参数");
            }

            var matrixOp = ConvertFunctionToOperation(func);
            var operation = new MatrixOperationItem(matrixOp)
            {
                LeftOperandSource = operand.Source
            };

            operations.Add(operation);

            stack.Push(new StackItem
            {
                Type = StackItemType.Result,
                Source = new OperandSource(OperandSourceType.Result, operations.Count - 1)
            });
        }

        #endregion


        /// <summary>
        /// 获取运算符优先级
        /// </summary>
        private int GetPrecedence(string op)
        {
            var precedenceMap = MatrixFormatter.GetSupportedOperators();
            return precedenceMap.ContainsKey(op) ? precedenceMap[op] : 0;
        }

        /// <summary>
        /// 转换运算符到矩阵操作
        /// </summary>
        public static MatrixOperation ConvertToMatrixOperation(string op)
        {
            switch (op)
            {
                case "+": return MatrixOperation.Add;
                case "-": return MatrixOperation.Subtract;
                case "×": return MatrixOperation.Multiply;
                case "⊙": return MatrixOperation.HadamardProduct;
                case "^": return MatrixOperation.Power;
                default: throw new NotSupportedException($"不支持的运算符: {op}");
            }
        }

        /// <summary>
        /// 转换函数到矩阵操作
        /// </summary>
        public static MatrixOperation ConvertFunctionToOperation(string func)
        {
            switch (func.ToLower())
            {
                case "inverse": return MatrixOperation.Inverse;
                case "trans": return MatrixOperation.Transpose;
                default: throw new NotSupportedException($"不支持的函数: {func}");
            }
        }

        public static string ConvertOperationToSymbol(MatrixOperation operation)
        {
            switch (operation)
            {
                case MatrixOperation.Add: return "+";
                case MatrixOperation.Subtract: return "-";
                case MatrixOperation.Multiply: return "×";
                case MatrixOperation.HadamardProduct: return "⊙";
                case MatrixOperation.Power: return "^";
                case MatrixOperation.Inverse: return "Inverse";
                case MatrixOperation.Transpose: return "Trans";

                default: throw new NotSupportedException($"不支持的矩阵操作: {operation}");
            }
        }


        /// <summary>
        /// 获取矩阵表达式
        /// </summary>
        private MatrixExpression GetMatrixExpression(string matrixName)
        {
            var matrixViewModel = matrixList.FirstOrDefault(m => m.Key == matrixName);
            if (matrixViewModel == null)
            {
                throw new ArgumentException($"找不到矩阵: {matrixName}");
            }

            return matrixViewModel.MatrixText.ToMatrixExpression(matrixName);
        }


        private class StackItem
        {
            public StackItemType Type { get; set; }
            public OperandSource Source { get; set; }
            public double ScalarValue { get; set; }
        }

        private enum StackItemType
        {
            Matrix,
            Scalar,
            Result
        }

    }

}
