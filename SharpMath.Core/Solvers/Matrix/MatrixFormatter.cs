using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SharpMath.Core
{

    public class MatrixFormatter
    {
        #region 成员定义

        // 运算符优先级定义
        private static Dictionary<string, int> OperatorPrecedence = new Dictionary<string, int>
        {
            { "+", 1 },
            { "-", 1 },
            { "⊙", 2 },
            { "×", 2 },
            { "^", 3 },
        };

        // 需要进行交换的运算符(通常要附加其他条件,比如操作的一方为数字)
        private static HashSet<string> SwapOperators = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "×"
        };

        // 函数定义(函数具有最高优先级)
        private static HashSet<string> Functions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "inverse", "trans"
        };

        #endregion

        #region 成员扩展

        /// <summary>
        /// 添加运算符到运算符优先级字典
        /// </summary>
        public static void AddOperator(string operatorSymbol, int precedence)
        {
            OperatorPrecedence[operatorSymbol] = precedence;
        }

        /// <summary>
        /// 移除运算符
        /// </summary>
        public static void RemoveOperator(string operatorSymbol)
        {
            OperatorPrecedence.Remove(operatorSymbol);
        }

        /// <summary>
        /// 添加函数到函数集合
        /// </summary>
        public static void AddFunction(string functionName)
        {
            Functions.Add(functionName);
        }

        /// <summary>
        /// 移除函数
        /// </summary>
        public static void RemoveFunction(string functionName)
        {
            Functions.Remove(functionName);
        }

        /// <summary>
        /// 获取所有支持的运算符
        /// </summary>
        public static IReadOnlyDictionary<string, int> GetSupportedOperators()
        {
            return new ReadOnlyDictionary<string, int>(OperatorPrecedence);
        }

        /// <summary>
        /// 获取所有支持的函数
        /// </summary>
        public static IReadOnlyCollection<string> GetSupportedFunctions()
        {
            return Functions.ToList().AsReadOnly();
        }

        #endregion

        #region 新增成员

        /// <summary>
        /// 为表达式添加新字符串并格式化
        /// </summary>
        /// <param name="originalExpression">原始表达式</param>
        /// <param name="newString">要添加的新字符串</param>
        /// <returns>格式化结果</returns>
        public static MatrixFormatResult AddAndFormat(string originalExpression, string newString)
        {
            try
            {
                // 清理输入
                originalExpression = originalExpression?.Trim() ?? "";
                newString = newString?.Trim() ?? "";

                if (string.IsNullOrEmpty(newString))
                {
                    return new MatrixFormatResult(true, originalExpression);
                }

                // 解析原始表达式和新字符串
                var originalElements = ParseExpression(originalExpression);
                var newElements = ParseExpression(newString);

                // 验证添加的合法性
                var validationResult = ValidateAddition(originalElements, newElements);
                if (!validationResult.Success)
                {
                    return validationResult;
                }

                // 合并表达式
                var combinedElements = CombineExpressions(originalElements, newElements);

                // 格式化表达式
                var formattedExpression = FormatExpression(combinedElements);

                return new MatrixFormatResult(true, formattedExpression);
            }
            catch (Exception ex)
            {
                return new MatrixFormatResult(false, originalExpression, $"格式化错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 合并表达式
        /// </summary>
        private static List<MatrixExpressionElement> CombineExpressions(List<MatrixExpressionElement> original, List<MatrixExpressionElement> newElements)
        {
            var result = new List<MatrixExpressionElement>(original);

            // 如果新增的是单一函数，需要特殊处理
            if (newElements.Count == 1 && newElements[0].Type == MatrixElementType.Function)
            {
                return HandleFunctionAddition(result, newElements[0]);
            }

            // 如果新增的是单一运算符，直接添加到末尾
            if (newElements.Count == 1 && newElements[0].Type == MatrixElementType.Operator)
            {
                result.Add(newElements[0]);
                return result;
            }

            // 如果新增的是单一括号，直接添加到末尾
            if (newElements.Count == 1 &&
                (newElements[0].Type == MatrixElementType.LeftParen || newElements[0].Type == MatrixElementType.RightParen))
            {
                result.Add(newElements[0]);
                return result;
            }

            // 如果新增的包含运算符和操作数，需要根据优先级处理
            if (newElements.Count > 1 && newElements.Any(e => e.Type == MatrixElementType.Operator))
            {
                result = HandleOperatorPrecedence(result, newElements);
            }
            else
            {
                // 单一变量、数字或其他元素，直接添加
                result.AddRange(newElements);
            }

            return result;
        }

        /// <summary>
        /// 处理运算符优先级
        /// </summary>
        private static List<MatrixExpressionElement> HandleOperatorPrecedence(List<MatrixExpressionElement> original, List<MatrixExpressionElement> newElements)
        {
            // 查找新增元素中的第一个运算符
            var firstOperatorIndex = newElements.FindIndex(e => e.Type == MatrixElementType.Operator);

            if (firstOperatorIndex < 0)
            {
                // 没有运算符，直接添加
                original.AddRange(newElements);
                return original;
            }

            var newOperator = newElements[firstOperatorIndex].Value;
            var beforeOperator = newElements.Take(firstOperatorIndex).ToList();
            var afterOperator = newElements.Skip(firstOperatorIndex + 1).ToList();

            // 特殊处理：如果新增的是 "运算符 + 操作数" 的形式（如 "* 2"）
            if (firstOperatorIndex == 0 && afterOperator.Count > 0 && original.Count > 0 && SwapOperators.Contains(newOperator))
            {
                return HandleOperatorOperandPattern(original, newOperator, beforeOperator, afterOperator);
            }

            // 默认情况：直接添加
            original.AddRange(newElements);
            return original;
        }

        /// <summary>
        /// 处理 "运算符 + 操作数" 模式
        /// </summary>
        private static List<MatrixExpressionElement> HandleOperatorOperandPattern(List<MatrixExpressionElement> original, string newOperator,
            List<MatrixExpressionElement> beforeOperator, List<MatrixExpressionElement> afterOperator)
        {
            var result = new List<MatrixExpressionElement>(original);

            // 查找原表达式中最后一个运算符
            var lastOperatorIndex = FindLastOperatorIndex(original);

            if (lastOperatorIndex >= 0)
            {
                var lastOperator = original[lastOperatorIndex].Value;
                var newOpPrecedence = OperatorPrecedence.GetValueOrDefault(newOperator, 0);
                var lastOpPrecedence = OperatorPrecedence.GetValueOrDefault(lastOperator, 0);

                // 如果新运算符优先级更高，需要重新组织
                if (newOpPrecedence > lastOpPrecedence)
                {
                    // 找到最后一个操作数
                    var lastOperandInfo = FindLastOperand(original);

                    if (lastOperandInfo.startIndex >= 0)
                    {
                        // 提取最后一个操作数
                        var lastOperand = original.Skip(lastOperandInfo.startIndex).Take(lastOperandInfo.length).ToList();

                        // 移除原来的最后一个操作数
                        result.RemoveRange(lastOperandInfo.startIndex, lastOperandInfo.length);

                        // 重新组织为：原表达式 + (新操作数 新运算符 最后操作数)
                        // 例如：A * B + "* 2" -> A * (2 * B)
                        var reorganizedExpression = new List<MatrixExpressionElement>();
                        reorganizedExpression.Add(new MatrixExpressionElement("(", MatrixElementType.LeftParen));

                        // 添加新的操作数（afterOperator）
                        reorganizedExpression.AddRange(afterOperator);

                        // 添加新运算符
                        reorganizedExpression.Add(new MatrixExpressionElement(newOperator, MatrixElementType.Operator));

                        // 添加原来的最后一个操作数
                        reorganizedExpression.AddRange(lastOperand);

                        reorganizedExpression.Add(new MatrixExpressionElement(")", MatrixElementType.RightParen));

                        result.AddRange(reorganizedExpression);
                        return result;
                    }
                }
            }

            // 优先级相同或更低，或者没有之前的运算符，按正常顺序添加
            result.Add(new MatrixExpressionElement(newOperator, MatrixElementType.Operator));
            result.AddRange(afterOperator);
            return result;
        }

        /// <summary>
        /// 验证添加的合法性
        /// </summary>
        private static MatrixFormatResult ValidateAddition(List<MatrixExpressionElement> original, List<MatrixExpressionElement> newElements)
        {
            if (newElements.Count == 0)
            {
                return new MatrixFormatResult(true, "");
            }
            var lastOriginal = original.LastOrDefault();
            var firstNew = newElements.First();

            // 如果原表达式以左括号结尾
            if (lastOriginal?.Type == MatrixElementType.LeftParen)
            {
                // 左括号后只能跟函数、变量或数字，不能跟运算符
                if (firstNew.Type == MatrixElementType.Operator)
                {
                    return new MatrixFormatResult(false, "", "左括号后不能直接添加运算符，需要函数、变量或数字");
                }
            }

            // 如果新增的是单一变量
            if (newElements.Count == 1 && newElements[0].Type == MatrixElementType.Variable)
            {
                // 检查原表达式末尾是否也是变量
                if (lastOriginal?.Type == MatrixElementType.Variable)
                {
                    return new MatrixFormatResult(false, "", "不能在变量后直接添加变量，中间需要运算符或函数");
                }
            }

            // 如果新增的是单一运算符
            if (newElements.Count == 1 && newElements[0].Type == MatrixElementType.Operator)
            {
                // 运算符不能跟在左括号后面
                if (lastOriginal?.Type == MatrixElementType.LeftParen)
                {
                    return new MatrixFormatResult(false, "", "左括号后不能直接添加运算符");
                }

                // 运算符不能是表达式的第一个元素（除非是负号等一元运算符）
                if (original.Count == 0)
                {
                    return new MatrixFormatResult(false, "", "表达式不能以运算符开始");
                }

                // 运算符不能跟在另一个运算符后面
                if (lastOriginal?.Type == MatrixElementType.Operator)
                {
                    return new MatrixFormatResult(false, "", "不能连续添加运算符");
                }

                return new MatrixFormatResult(true, "");
            }

            // 如果新增的是单一右括号
            if (newElements.Count == 1 && newElements[0].Type == MatrixElementType.RightParen)
            {
                // 检查是否有匹配的左括号
                if (!HasUnmatchedLeftParen(original))
                {
                    return new MatrixFormatResult(false, "", "没有匹配的左括号");
                }

                // 右括号不能跟在左括号或运算符后面
                if (lastOriginal?.Type == MatrixElementType.LeftParen || lastOriginal?.Type == MatrixElementType.Operator)
                {
                    return new MatrixFormatResult(false, "", "右括号前需要有操作数");
                }

                return new MatrixFormatResult(true, "");
            }

            // 如果新增的是单一左括号
            if (newElements.Count == 1 && newElements[0].Type == MatrixElementType.LeftParen)
            {
                // 左括号可以跟在运算符后面，或者作为表达式开始
                return new MatrixFormatResult(true, "");
            }

            // 如果新增的是单一函数
            if (newElements.Count == 1 && newElements[0].Type == MatrixElementType.Function)
            {
                return new MatrixFormatResult(true, "");
            }

            // 新增验证规则1：检查函数参数是否为纯数字
            var functionValidation = MatrixExpressionValidator.ValidateFunctionArguments(original, newElements);
            if (!functionValidation.Success)
            {
                return functionValidation;
            }

            // 新增验证规则2：检查纯数字是否参与了禁止的运算
            var numberOperationValidation = MatrixExpressionValidator.ValidateNumberOperations(original, newElements);
            if (!numberOperationValidation.Success)
            {
                return numberOperationValidation;
            }

            return new MatrixFormatResult(true, "");
        }

        /// <summary>
        /// 检查是否有未匹配的左括号
        /// </summary>
        private static bool HasUnmatchedLeftParen(List<MatrixExpressionElement> elements)
        {
            int parenCount = 0;

            foreach (var element in elements)
            {
                if (element.Type == MatrixElementType.LeftParen)
                    parenCount++;
                else if (element.Type == MatrixElementType.RightParen)
                    parenCount--;
            }

            return parenCount > 0;
        }

        #endregion

        #region 移除成员
        /// <summary>
        /// 从当前表达式中移除最后一个元素
        /// </summary>
        /// <param name="expression">当前表达式</param>
        /// <returns>移除结果</returns>
        public static MatrixFormatResult RemoveLastElement(string expression)
        {
            try
            {
                if (string.IsNullOrEmpty(expression))
                {
                    return new MatrixFormatResult(true, "", "表达式为空，无法移除元素");
                }

                var elements = ParseExpression(expression);
                if (elements.Count == 0)
                {
                    return new MatrixFormatResult(true, "", "表达式为空，无法移除元素");
                }

                var modifiedElements = RemoveLastElementFromList(elements);
                var formattedExpression = FormatExpression(modifiedElements);

                return new MatrixFormatResult(true, formattedExpression);
            }
            catch (Exception ex)
            {
                return new MatrixFormatResult(false, expression, $"移除元素时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 从元素列表中移除最后一个完整元素
        /// </summary>
        private static List<MatrixExpressionElement> RemoveLastElementFromList(List<MatrixExpressionElement> elements)
        {
            if (elements.Count == 0)
                return elements;

            var result = new List<MatrixExpressionElement>(elements);
            var lastElement = elements.Last();

            switch (lastElement.Type)
            {
                case MatrixElementType.Variable:
                case MatrixElementType.Number:
                case MatrixElementType.Operator:
                    // 简单元素，直接移除
                    result.RemoveAt(result.Count - 1);
                    break;

                case MatrixElementType.RightParen:
                    // 移除整个括号表达式
                    result = RemoveParenthesesExpression(result);
                    break;

                case MatrixElementType.LeftParen:
                    // 检查是否是函数调用的左括号
                    if (result.Count >= 2 && result[result.Count - 2].Type == MatrixElementType.Function)
                    {
                        // 移除函数名和左括号
                        result.RemoveRange(result.Count - 2, 2);
                    }
                    else
                    {
                        // 只移除左括号
                        result.RemoveAt(result.Count - 1);
                    }
                    break;

                case MatrixElementType.Function:
                    // 移除函数名，如果后面跟着左括号也一起移除
                    result.RemoveAt(result.Count - 1);
                    if (result.Count > 0 && result.Last().Type == MatrixElementType.LeftParen)
                    {
                        result.RemoveAt(result.Count - 1);
                    }
                    break;
            }

            return result;
        }

        /// <summary>
        /// 移除括号表达式
        /// </summary>
        private static List<MatrixExpressionElement> RemoveParenthesesExpression(List<MatrixExpressionElement> elements)
        {
            var result = new List<MatrixExpressionElement>(elements);
            int parenCount = 0;
            int startIndex = -1;

            // 从后往前找到匹配的左括号
            for (int i = result.Count - 1; i >= 0; i--)
            {
                var element = result[i];

                if (element.Type == MatrixElementType.RightParen)
                {
                    parenCount++;
                }
                else if (element.Type == MatrixElementType.LeftParen)
                {
                    parenCount--;

                    if (parenCount == 0)
                    {
                        startIndex = i;

                        // 检查是否是函数调用
                        if (i > 0 && result[i - 1].Type == MatrixElementType.Function)
                        {
                            startIndex = i - 1;
                        }
                        break;
                    }
                }
            }

            if (startIndex >= 0)
            {
                // 移除从startIndex到末尾的所有元素
                int removeCount = result.Count - startIndex;
                result.RemoveRange(startIndex, removeCount);
            }
            else
            {
                // 如果没找到匹配的左括号，只移除右括号
                result.RemoveAt(result.Count - 1);
            }

            return result;
        }

        #endregion

        #region 矩阵表达式解析

        /// <summary>
        /// 获取表达式的最后一个元素信息
        /// </summary>
        /// <param name="expression">表达式</param>
        /// <returns>最后一个元素的信息</returns>
        public static (bool success, string elementValue, MatrixElementType elementType, string message) GetLastElementInfo(string expression)
        {
            try
            {
                if (string.IsNullOrEmpty(expression))
                {
                    return (false, "", MatrixElementType.Unknown, "表达式为空");
                }

                var elements = ParseExpression(expression);
                if (elements.Count == 0)
                {
                    return (false, "", MatrixElementType.Unknown, "表达式为空");
                }

                var lastElement = elements.Last();
                return (true, lastElement.Value, lastElement.Type, "");
            }
            catch (Exception ex)
            {
                return (false, "", MatrixElementType.Unknown, $"获取最后元素信息时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 解析表达式为元素列表
        /// </summary>
        public static List<MatrixExpressionElement> ParseExpression(string expression)
        {
            var elements = new List<MatrixExpressionElement>();
            if (string.IsNullOrEmpty(expression))
            {
                return elements;
            }
            // 动态构建正则表达式模式
            var pattern = BuildRegexPattern();
            var matches = Regex.Matches(expression, pattern);

            foreach (Match match in matches)
            {
                var value = match.Value.Trim();
                if (string.IsNullOrEmpty(value))
                {
                    continue;
                }
                var type = GetElementType(value);
                elements.Add(new MatrixExpressionElement(value, type));
            }

            return elements;
        }

        /// <summary>
        /// 动态构建正则表达式模式
        /// </summary>
        private static string BuildRegexPattern()
        {
            var patternParts = new List<string>();

            // 1. 函数名(按长度降序排列，避免短函数名匹配长函数名的前缀)
            if (Functions.Count > 0)
            {
                var sortedFunctions = Functions.OrderByDescending(f => f.Length).ToList();
                var functionPattern = string.Join("|", sortedFunctions.Select(Regex.Escape));
                patternParts.Add($"({functionPattern})");
            }

            // 2. 运算符(按长度降序排列，确保多字符运算符优先匹配)
            if (OperatorPrecedence.Count > 0)
            {
                var sortedOperators = OperatorPrecedence.Keys.OrderByDescending(op => op.Length).ToList();
                var operatorPattern = string.Join("|", sortedOperators.Select(Regex.Escape));
                patternParts.Add($"({operatorPattern})");
            }

            // 3. 变量(字母开头，后跟字母数字)
            patternParts.Add(@"([A-Za-z][A-Za-z0-9]*)");

            // 4. 数字(整数或小数)
            patternParts.Add(@"(\d+(?:\.\d+)?)");

            // 5. 括号
            patternParts.Add(@"([()])"); ;

            // 6. 空白字符(用于分隔，但会被过滤掉)
            patternParts.Add(@"(\s+)");

            // 组合所有模式
            return string.Join("|", patternParts);
        }

        /// <summary>
        /// 确定元素类型
        /// </summary>
        private static MatrixElementType GetElementType(string value)
        {
            // 检查括号
            if (value == "(")
            {
                return MatrixElementType.LeftParen;
            }
            if (value == ")")
            {
                return MatrixElementType.RightParen;
            }
            // 检查函数(使用动态函数列表)
            if (Functions.Contains(value))
            {
                return MatrixElementType.Function;
            }
            // 检查运算符(使用动态运算符列表)
            if (OperatorPrecedence.ContainsKey(value))
            {
                return MatrixElementType.Operator;
            }
            // 检查数字
            if (Regex.IsMatch(value, @"^\d+(?:\.\d+)?$"))
            {
                return MatrixElementType.Number;
            }
            // 检查变量
            if (Regex.IsMatch(value, @"^[A-Za-z][A-Za-z0-9]*$"))
            {
                return MatrixElementType.Variable;
            }
            return MatrixElementType.Unknown;
        }


        /// <summary>
        /// 处理函数添加
        /// </summary>
        private static List<MatrixExpressionElement> HandleFunctionAddition(List<MatrixExpressionElement> original, MatrixExpressionElement function)
        {
            var result = new List<MatrixExpressionElement>(original);
            var lastElement = original.LastOrDefault();

            // 如果原表达式以运算符结尾（如 A1*），直接添加函数和左括号
            if (lastElement?.Type == MatrixElementType.Operator)
            {
                result.Add(function);
                result.Add(new MatrixExpressionElement("(", MatrixElementType.LeftParen));
                return result;
            }

            // 如果原表达式以变量或数字结尾，函数需要应用到最后一个操作数
            if (lastElement?.Type == MatrixElementType.Variable || lastElement?.Type == MatrixElementType.Number)
            {
                // 找到最后一个操作数的起始位置
                var lastOperandInfo = FindLastOperand(original);

                if (lastOperandInfo.startIndex >= 0)
                {
                    // 提取最后一个操作数
                    var lastOperand = original.Skip(lastOperandInfo.startIndex).Take(lastOperandInfo.length).ToList();

                    // 移除原来的最后一个操作数
                    result.RemoveRange(lastOperandInfo.startIndex, lastOperandInfo.length);

                    // 添加函数调用
                    result.Add(function);
                    result.Add(new MatrixExpressionElement("(", MatrixElementType.LeftParen));
                    result.AddRange(lastOperand);

                    return result;
                }
            }

            // 默认情况：直接添加函数和左括号
            result.Add(function);
            result.Add(new MatrixExpressionElement("(", MatrixElementType.LeftParen));
            return result;
        }

        /// <summary>
        /// 找到最后一个操作数的位置和长度
        /// </summary>
        public static (int startIndex, int length) FindLastOperand(List<MatrixExpressionElement> elements)
        {
            if (elements.Count == 0)
            {
                return (-1, 0);
            }
            int endIndex = elements.Count - 1;
            int startIndex = endIndex;
            int parenCount = 0;

            // 从后往前扫描，找到完整的操作数
            for (int i = endIndex; i >= 0; i--)
            {
                var element = elements[i];

                // 处理括号
                if (element.Type == MatrixElementType.RightParen)
                {
                    parenCount++;
                }
                else if (element.Type == MatrixElementType.LeftParen)
                {
                    parenCount--;

                    // 如果是函数调用的左括号，需要包含函数名
                    if (parenCount == 0 && i > 0 && elements[i - 1].Type == MatrixElementType.Function)
                    {
                        startIndex = i - 1;
                        break;
                    }
                    else if (parenCount == 0)
                    {
                        startIndex = i;
                        break;
                    }
                }
                else if (parenCount == 0)
                {
                    // 在括号外遇到运算符，停止
                    if (element.Type == MatrixElementType.Operator)
                    {
                        startIndex = i + 1;
                        break;
                    }

                    // 更新起始位置
                    startIndex = i;

                    // 如果到达开头
                    if (i == 0)
                    {
                        break;
                    }
                }
            }

            int length = endIndex - startIndex + 1;
            return (startIndex, Math.Max(0, length));
        }


        /// <summary>
        /// 找到最后一个运算符的索引
        /// </summary>
        public static int FindLastOperatorIndex(List<MatrixExpressionElement> elements)
        {
            for (int i = elements.Count - 1; i >= 0; i--)
            {
                if (elements[i].Type == MatrixElementType.Operator)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// 格式化表达式为字符串
        /// </summary>
        private static string FormatExpression(List<MatrixExpressionElement> elements)
        {
            if (elements.Count == 0)
            {
                return "";
            }
            var result = new StringBuilder();

            for (int i = 0; i < elements.Count; i++)
            {
                var current = elements[i];

                // 添加当前元素
                result.Append(current.Value);

                // 添加空格(根据需要)
                if (i < elements.Count - 1)
                {
                    var next = elements[i + 1];

                    if (ShouldAddSpace(current, next))
                    {
                        result.Append(" ");
                    }
                }
            }
            return result.ToString();
        }

        /// <summary>
        /// 判断是否需要添加空格
        /// </summary>
        private static bool ShouldAddSpace(MatrixExpressionElement current, MatrixExpressionElement next)
        {
            // 左括号后不加空格
            if (current.Type == MatrixElementType.LeftParen)
            {
                return false;
            }
            // 右括号前不加空格
            if (next.Type == MatrixElementType.RightParen)
            {
                return false;
            }
            // 函数和左括号之间不加空格
            if (current.Type == MatrixElementType.Function && next.Type == MatrixElementType.LeftParen)
            {
                return false;
            }
            // 运算符前后加空格
            if (current.Type == MatrixElementType.Operator)
            {
                return true;
            }
            if (next.Type == MatrixElementType.Operator)
            {
                return true;
            }
            // 函数前加空格(除非前面是运算符或左括号)
            if (next.Type == MatrixElementType.Function &&
                current.Type != MatrixElementType.Operator &&
                current.Type != MatrixElementType.LeftParen)
            {
                return true;
            }
            // 变量和数字之间加空格
            if ((current.Type == MatrixElementType.Variable || current.Type == MatrixElementType.Number) &&
                (next.Type == MatrixElementType.Variable || next.Type == MatrixElementType.Number))
            {
                return true;
            }
            return false;
        }

        #endregion

    }

    /// <summary>
    /// 矩阵验证类
    /// </summary>
    public static class MatrixExpressionValidator
    {
        // 纯数字不能参与的运算符列表
        public static readonly HashSet<string> NumberDenyList = new HashSet<string>
        {
           "+", "-","⊙"
        };

        /// <summary>
        /// 验证表达式的括号是否匹配
        /// </summary>
        public static bool ValidateParentheses(string expression)
        {
            var elements = MatrixFormatter.ParseExpression(expression);
            var stack = new Stack<char>();

            foreach (var element in elements)
            {
                if (element.Type == MatrixElementType.LeftParen)
                {
                    stack.Push('(');
                }
                else if (element.Type == MatrixElementType.RightParen)
                {
                    if (stack.Count == 0)
                    {
                        return false;
                    }
                    stack.Pop();
                }
            }

            return stack.Count == 0;
        }

        /// <summary>
        /// 检查表达式是否完整
        /// </summary>
        public static (bool isComplete, string message) ValidateCompleteness(string expression)
        {
            var elements = MatrixFormatter.ParseExpression(expression);

            if (elements.Count == 0)
            {
                return (true, "表达式为空");
            }
            var lastElement = elements.Last();

            // 检查是否以运算符结尾
            if (lastElement.Type == MatrixElementType.Operator)
            {
                return (false, "表达式以运算符结尾，需要添加操作数");
            }

            // 检查是否有未闭合的函数调用
            if (lastElement.Type == MatrixElementType.LeftParen)
            {
                // 查找对应的函数
                for (int i = elements.Count - 2; i >= 0; i--)
                {
                    if (elements[i].Type == MatrixElementType.Function)
                    {
                        return (false, $"函数 '{elements[i].Value}' 的参数列表未完成");
                    }
                    if (elements[i].Type == MatrixElementType.Operator)
                    {
                        break;
                    }
                }
                return (false, "存在未闭合的左括号");
            }

            // 检查括号匹配
            if (!ValidateParentheses(expression))
            {
                return (false, "括号不匹配");
            }

            return (true, "表达式完整");
        }

        /// <summary>
        /// 验证表达式的完整性（包括新的规则）
        /// </summary>
        public static MatrixFormatResult ValidateExpression(string expression)
        {
            try
            {
                var elements = MatrixFormatter.ParseExpression(expression);

                // 检查函数参数
                var functionValidation = ValidateFunctionArgumentsInExpression(elements);
                if (!functionValidation.Success)
                {
                    return functionValidation;
                }

                // 检查数字运算限制
                var numberValidation = ValidateCompleteExpressionForNumberOperations(elements);
                if (!numberValidation.Success)
                {
                    return numberValidation;
                }

                // 检查括号匹配
                if (!ValidateParentheses(expression))
                {
                    return new MatrixFormatResult(false, expression, "括号不匹配");
                }

                return new MatrixFormatResult(true, expression, "表达式验证通过");
            }
            catch (Exception ex)
            {
                return new MatrixFormatResult(false, expression, $"验证时发生错误: {ex.Message}");
            }
        }


        /// <summary>
        /// 验证表达式中的函数参数
        /// </summary>
        public static MatrixFormatResult ValidateFunctionArgumentsInExpression(List<MatrixExpressionElement> elements)
        {
            for (int i = 0; i < elements.Count - 2; i++)
            {
                var current = elements[i];
                var next = elements[i + 1];
                var afterNext = elements[i + 2];

                // 检查模式：函数 + 左括号 + 纯数字
                if (current.Type == MatrixElementType.Function &&
                    next.Type == MatrixElementType.LeftParen &&
                    afterNext.Type == MatrixElementType.Number)
                {
                    return new MatrixFormatResult(false, "", $"函数 '{current.Value}' 不能接受纯数字参数 '{afterNext.Value}'，只能接受变量");
                }
            }

            return new MatrixFormatResult(true, "");
        }

        /// <summary>
        /// 验证完整表达式中的数字运算限制
        /// </summary>
        public static MatrixFormatResult ValidateCompleteExpressionForNumberOperations(List<MatrixExpressionElement> elements)
        {
            for (int i = 1; i < elements.Count - 1; i++)
            {
                var prev = elements[i - 1];
                var current = elements[i];
                var next = elements[i + 1];

                // 检查模式：变量 + 禁止运算符 + 纯数字
                if (prev.Type == MatrixElementType.Variable &&
                    current.Type == MatrixElementType.Operator &&
                    NumberDenyList.Contains(current.Value) &&
                    next.Type == MatrixElementType.Number)
                {
                    return new MatrixFormatResult(false, "",
                        $"变量 '{prev.Value}' 不能与纯数字 '{next.Value}' 使用运算符 '{current.Value}'");
                }
            }

            return new MatrixFormatResult(true, "");
        }

        /// <summary>
        /// 验证函数调用的参数
        /// </summary>
        public static MatrixFormatResult ValidateFunctionArguments(List<MatrixExpressionElement> original, List<MatrixExpressionElement> newElements)
        {
            // 检查是否在函数调用中添加纯数字
            if (original.Count > 0)
            {
                var lastElement = original.Last();

                // 如果最后一个元素是左括号，检查前面是否是函数
                if (lastElement.Type == MatrixElementType.LeftParen && original.Count >= 2)
                {
                    var beforeParen = original[original.Count - 2];
                    if (beforeParen.Type == MatrixElementType.Function)
                    {
                        // 检查新增的第一个元素是否为纯数字
                        if (newElements.Count > 0 && newElements[0].Type == MatrixElementType.Number)
                        {
                            return new MatrixFormatResult(false, "", $"函数 '{beforeParen.Value}' 不能接受纯数字参数 '{newElements[0].Value}'，只能接受变量");
                        }
                    }
                }
            }

            // 检查新增的函数调用是否针对纯数字
            if (newElements.Count == 1 && newElements[0].Type == MatrixElementType.Function)
            {
                if (original.Count > 0)
                {
                    var lastOriginal = original.Last();
                    if (lastOriginal.Type == MatrixElementType.Number)
                    {
                        return new MatrixFormatResult(false, "", $"函数 '{newElements[0].Value}' 不能应用于纯数字 '{lastOriginal.Value}'，只能应用于变量");
                    }

                    // 检查最后一个操作数是否为纯数字
                    var lastOperandInfo = MatrixFormatter.FindLastOperand(original);
                    if (lastOperandInfo.startIndex >= 0 && lastOperandInfo.length == 1)
                    {
                        var lastOperand = original[lastOperandInfo.startIndex];
                        if (lastOperand.Type == MatrixElementType.Number)
                        {
                            return new MatrixFormatResult(false, "", $"函数 '{newElements[0].Value}' 不能应用于纯数字 '{lastOperand.Value}'，只能应用于变量");
                        }
                    }
                }
            }

            return new MatrixFormatResult(true, "");
        }

        /// <summary>
        /// 验证纯数字运算
        /// </summary>
        public static MatrixFormatResult ValidateNumberOperations(List<MatrixExpressionElement> original, List<MatrixExpressionElement> newElements)
        {
            // 情况1：在纯数字后添加禁止的运算符
            if (original.Count > 0 && newElements.Count > 0)
            {
                var lastOriginal = original.Last();
                var firstNew = newElements[0];

                if (lastOriginal.Type == MatrixElementType.Number &&
                    firstNew.Type == MatrixElementType.Operator &&
                    MatrixExpressionValidator.NumberDenyList.Contains(firstNew.Value))
                {
                    return new MatrixFormatResult(false, "", $"纯数字 '{lastOriginal.Value}' 不能使用运算符 '{firstNew.Value}'");
                }
            }

            // 情况2：添加包含禁止运算符和纯数字的表达式
            for (int i = 0; i < newElements.Count - 1; i++)
            {
                var current = newElements[i];
                var next = newElements[i + 1];

                // 检查：变量 + 禁止运算符 + 纯数字
                if (current.Type == MatrixElementType.Operator &&
                    MatrixExpressionValidator.NumberDenyList.Contains(current.Value) &&
                    next.Type == MatrixElementType.Number)
                {
                    // 需要检查运算符前面的操作数
                    var beforeOperator = GetOperandBeforeOperator(original, newElements, i);
                    if (beforeOperator != null && beforeOperator.Type == MatrixElementType.Variable)
                    {
                        return new MatrixFormatResult(false, "",
                            $"变量 '{beforeOperator.Value}' 不能与纯数字 '{next.Value}' 使用运算符 '{current.Value}'");
                    }
                }
            }

            // 情况3：检查完整表达式中的禁止组合
            var combinedElements = new List<MatrixExpressionElement>(original);
            combinedElements.AddRange(newElements);

            return ValidateCompleteExpressionForNumberOperations(combinedElements);
        }

        /// <summary>
        /// 获取运算符前面的操作数
        /// </summary>
        private static MatrixExpressionElement GetOperandBeforeOperator(List<MatrixExpressionElement> original, List<MatrixExpressionElement> newElements, int operatorIndex)
        {
            if (operatorIndex > 0)
            {
                // 运算符前面有元素
                return newElements[operatorIndex - 1];
            }
            else if (original.Count > 0)
            {
                // 运算符是新增元素的第一个，检查原表达式的最后一个元素
                return original.Last();
            }

            return null;
        }

    }


    /// <summary>
    /// 矩阵表达式元素
    /// </summary>
    public class MatrixExpressionElement
    {
        public string Value { get; set; }
        public MatrixElementType Type { get; set; }

        public MatrixExpressionElement(string value, MatrixElementType type)
        {
            Value = value;
            Type = type;
        }

        public override string ToString()
        {
            return $"{Value}({Type})";
        }
    }

    /// <summary>
    /// 矩阵表达式格式化结果
    /// </summary>
    public class MatrixFormatResult
    {
        public bool Success { get; set; }
        public string FormattedExpression { get; set; }
        public string ErrorMessage { get; set; }

        public MatrixFormatResult(bool success, string result, string error = null)
        {
            Success = success;
            FormattedExpression = result;
            ErrorMessage = error;
        }
    }

    // 表达式元素类型
    public enum MatrixElementType
    {
        Variable,
        Number,
        Operator,
        Function,
        LeftParen,
        RightParen,
        Unknown
    }
}
