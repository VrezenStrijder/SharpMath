using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SharpMath.Core
{
    public interface IParser
    {
        IMathExpression Parse(string input);
    }

    public class AdvancedMathParser : IParser
    {
        private class OperatorInfo
        {
            public OperatorInfo(int precedence, bool isRightAssociative)
            {
                Precedence = precedence;
                IsRightAssociative = isRightAssociative;
            }

            public int Precedence { get; }

            public bool IsRightAssociative { get; }


        }

        private readonly Dictionary<string, OperatorInfo> operators = new Dictionary<string, OperatorInfo>
        {
            // 等式运算符
            {"=", new OperatorInfo(-1, false)},
            // 比较运算符
            {"==", new OperatorInfo(0, false)},
            {"!=", new OperatorInfo(0, false)},
            {">", new OperatorInfo(0, false)},
            {"<", new OperatorInfo(0, false)},
            {">=", new OperatorInfo(0, false)},
            {"<=", new OperatorInfo(0, false)},
            // 算数运算符
            {"+", new OperatorInfo(1, false)},
            {"-", new OperatorInfo(1, false)},
            {"*", new OperatorInfo(2, false)},
            {"/", new OperatorInfo(2, false)},
            {"%", new OperatorInfo(2, false)},
            {"^", new OperatorInfo(3, true)}
        };

        private readonly Dictionary<string, double> constants = new Dictionary<string, double>
        {
            { "pi", Math.PI },
            { "e", Math.E }
        };

        private readonly Dictionary<string, int> functions = new Dictionary<string, int>
        {
            {"sin", 1}, {"cos", 1}, {"tan", 1}, {"asin", 1}, {"acos", 1},
            {"atan", 1}, {"sinh", 1}, {"cosh", 1}, {"tanh", 1},
            {"sqrt", 1}, {"log", 1}, {"log10", 1},{"abs", 1},
            {"ceiling", 1}, {"floor", 1}, {"round", 1}, {"sign", 1},
            {"truncate", 1}, {"frac", 1}, {"pow", 2}, {"atan2", 2},
            {"exp", 1},{"max", -1}, {"min", -1}, {"sum", -1}, {"mean", -1},
            {"product", -1}, {"variance", -1}, {"stddev", -1}
        };

        public IMathExpression Parse(string input)
        {
            input = input.Trim();

            // 优先检查是否为矩阵或矩阵列表
            //if (input.StartsWith("[") && input.EndsWith("]"))
            //{
            //    return ParseMatrixOrMatrixList(input);
            //}

            // 判定是否为方程组(以;分隔区别多个表达式)
            var equationStrings = input.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            if (equationStrings.Length > 1 && equationStrings.All(s => s.Contains("=")))
            {
                return ParseEquationSystem(equationStrings);
            }

            return ParseSingle(input);
        }

        #region 输入类型检测

        //private IMathExpression ParseMatrixOrMatrixList(string input)
        //{
        //    // 检查顶层是否为矩阵列表 [[...],[...]];[[...],[...]]
        //    if (input.Count(c => c == '[') > 2 && input.Contains("];["))
        //    {
        //        // Todo: 矩阵列表解析
        //    }
        //    return ParseMatrix(input);
        //}

        //private MatrixExpression ParseMatrix(string input)
        //{
        //    try
        //    {
        //        // 移除外层括号
        //        input = input.Substring(1, input.Length - 2).Trim();
        //        var rows = Regex.Split(input, @"\],\[");
        //        rows[0] = rows[0].TrimStart('[');
        //        rows[rows.Length - 1] = rows[rows.Length - 1].TrimEnd(']');

        //        var numRows = rows.Length;
        //        if (numRows == 0) return new MatrixExpression(new double[0, 0]);

        //        var numCols = rows[0].Split(',').Length;
        //        var values = new double[numRows, numCols];

        //        for (int i = 0; i < numRows; i++)
        //        {
        //            var cols = rows[i].Split(',').Select(s => s.Trim()).ToArray();
        //            if (cols.Length != numCols) throw new FormatException("矩阵的列数必须一致");
        //            for (int j = 0; j < numCols; j++)
        //            {
        //                values[i, j] = double.Parse(cols[j]);
        //            }
        //        }
        //        return new MatrixExpression(values);
        //    }
        //    catch (Exception ex)
        //    {
        //        throw new FormatException("无效的矩阵格式", ex);
        //    }
        //}

        private EquationSystemExpression ParseEquationSystem(string[] equationStrings)
        {
            var equations = new List<EquationExpression>();
            foreach (var eqStr in equationStrings)
            {
                var expr = ParseSingle(eqStr.Trim());
                if (expr is EquationExpression eq)
                {
                    equations.Add(eq);
                }
                else
                {
                    throw new FormatException($"'{eqStr}' is not a valid equation for the system.");
                }
            }
            return new EquationSystemExpression(equations);
        }

        #endregion

        private IMathExpression ParseSingle(string input)
        {
            var tokens = Tokenize(input);
            var rpn = ConvertToRPN(tokens);
            return BuildExpressionTree(rpn);
        }

        private List<Token> Tokenize(string expression)
        {
            var tokens = new List<Token>();
            var opPattern = string.Join("|", operators.Keys.Select(Regex.Escape).OrderByDescending(s => s.Length));
            var pattern = $@"([0-9]+\.?[0-9]*|\.[0-9]+)|([a-zA-Z_][a-zA-Z0-9_]*)|({opPattern})|(\(|\)|,)";
            var regex = new Regex(pattern);
            var matches = regex.Matches(expression);

            Token previousToken = null;
            foreach (Match match in matches)
            {
                var value = match.Value;
                Token currentToken;
                if (double.TryParse(value, out _))
                {
                    currentToken = new Token(TokenType.Number, value);
                }
                else if (value == "(")
                {
                    currentToken = new Token(TokenType.LeftParen, value);
                }
                else if (value == ")")
                {
                    currentToken = new Token(TokenType.RightParen, value);
                }
                else if (value == ",")
                {
                    currentToken = new Token(TokenType.Comma, value);
                }
                else if (operators.ContainsKey(value))
                {
                    if (value == "-" && IsUnaryContext(previousToken))
                    {
                        currentToken = new Token(TokenType.UnaryOperator, value);
                    }
                    else
                    {
                        currentToken = new Token(TokenType.Operator, value);
                    }
                }
                //else if (operators.ContainsKey(value))
                //{
                //    if (value == "-" && (previousToken == null || previousToken.Type == TokenType.Operator || previousToken.Type == TokenType.LeftParen || previousToken.Type == TokenType.Comma))
                //    {
                //        currentToken = new Token(TokenType.UnaryOperator, value);
                //    }
                //    else
                //    {
                //        currentToken = new Token(TokenType.Operator, value);
                //    }
                //}
                else if (char.IsLetter(value[0]))
                {
                    var lowerValue = value.ToLower();
                    if (functions.ContainsKey(lowerValue))
                    {
                        currentToken = new Token(TokenType.Function, value);
                    }
                    else if (constants.ContainsKey(lowerValue))
                    {
                        currentToken = new Token(TokenType.Constant, value);
                    }
                    else
                    {
                        currentToken = new Token(TokenType.Variable, value);
                    }
                }
                else
                {
                    throw new ArgumentException($"Unknown token: {value}");
                }
                tokens.Add(currentToken);
                previousToken = currentToken;
            }
            return tokens;
        }

        /// <summary>
        /// 判断是否是一元负号的上下文
        /// </summary>
        private bool IsUnaryContext(Token previousToken)
        {
            if (previousToken == null) return true; // 表达式开头

            return previousToken.Type == TokenType.Operator ||
                   previousToken.Type == TokenType.UnaryOperator || // 重要：连续的负号
                   previousToken.Type == TokenType.LeftParen ||
                   previousToken.Type == TokenType.Comma ||
                   previousToken.Type == TokenType.Function;
        }


        private List<Token> ConvertToRPN(List<Token> tokens)
        {
            var outputQueue = new List<Token>();
            var operatorStack = new Stack<Token>();
            var functionArgCount = new Stack<int>(); // 跟踪每个函数的参数数量

            foreach (var token in tokens)
            {
                switch (token.Type)
                {
                    case TokenType.Number:
                    case TokenType.Variable:
                    case TokenType.Constant:
                        outputQueue.Add(token);
                        break;

                    case TokenType.Function:
                        operatorStack.Push(token);
                        functionArgCount.Push(1); // 至少有一个参数
                        break;

                    case TokenType.Comma:
                        // 增加当前函数的参数计数
                        if (functionArgCount.Count > 0)
                        {
                            var count = functionArgCount.Pop();
                            functionArgCount.Push(count + 1);
                        }

                        while (operatorStack.Count > 0 && operatorStack.Peek().Type != TokenType.LeftParen)
                        {
                            outputQueue.Add(operatorStack.Pop());
                        }
                        break;

                    case TokenType.UnaryOperator:
                        operatorStack.Push(token);
                        break;

                    case TokenType.Operator:
                        while (operatorStack.Count > 0 && operatorStack.Peek().Type == TokenType.Operator)
                        {
                            var op1 = operators[token.Value];
                            var op2 = operators[operatorStack.Peek().Value];
                            if ((!op1.IsRightAssociative && op1.Precedence <= op2.Precedence) ||
                                (op1.IsRightAssociative && op1.Precedence < op2.Precedence))
                            {
                                outputQueue.Add(operatorStack.Pop());
                            }
                            else
                            {
                                break;
                            }
                        }
                        operatorStack.Push(token);
                        break;

                    case TokenType.LeftParen:
                        operatorStack.Push(token);
                        break;

                    case TokenType.RightParen:
                        while (operatorStack.Count > 0 && operatorStack.Peek().Type != TokenType.LeftParen)
                        {
                            outputQueue.Add(operatorStack.Pop());
                        }
                        if (operatorStack.Count > 0)
                        {
                            operatorStack.Pop(); // Remove left paren
                        }

                        // 如果下一个是函数，添加参数计数信息
                        if (operatorStack.Count > 0 && operatorStack.Peek().Type == TokenType.Function)
                        {
                            var funcToken = operatorStack.Pop();
                            var argCount = functionArgCount.Count > 0 ? functionArgCount.Pop() : 1;

                            // 为可变参数函数添加参数计数标记
                            if (functions.TryGetValue(funcToken.Value.ToLower(), out int expectedArgs) && expectedArgs == -1)
                            {
                                outputQueue.Add(new Token(TokenType.Number, argCount.ToString())); // 添加参数计数
                            }
                            outputQueue.Add(funcToken);
                        }
                        break;
                }
            }

            while (operatorStack.Count > 0)
            {
                outputQueue.Add(operatorStack.Pop());
            }

            return outputQueue;
        }

        private IMathExpression BuildExpressionTree(List<Token> rpn)
        {
            var stack = new Stack<IMathExpression>();

            for (int i = 0; i < rpn.Count; i++)
            {
                var token = rpn[i];
                switch (token.Type)
                {
                    case TokenType.Number:
                        stack.Push(new NumberExpression(double.Parse(token.Value)));
                        break;

                    case TokenType.Constant:
                        stack.Push(new NumberExpression(constants[token.Value.ToLower()]));
                        break;

                    case TokenType.Variable:
                        stack.Push(new VariableExpression(token.Value));
                        break;

                    case TokenType.UnaryOperator:
                        if (stack.Count < 1)
                            throw new ArgumentException("Invalid expression: not enough operands for unary operator");
                        stack.Push(new UnaryOperationExpression(stack.Pop(), UnaryOperationType.Negate));
                        break;

                    case TokenType.Operator:
                        if (stack.Count < 2)
                            throw new ArgumentException("Invalid expression: not enough operands for binary operator");

                        var right = stack.Pop();
                        var left = stack.Pop();

                        // 处理等式
                        if (token.Value == "=")
                        {
                            stack.Push(new EquationExpression(left, right));
                            break;
                        }

                        var opType = token.Value switch
                        {
                            "+" => BinaryOperationType.Add,
                            "-" => BinaryOperationType.Subtract,
                            "*" => BinaryOperationType.Multiply,
                            "/" => BinaryOperationType.Divide,
                            "^" => BinaryOperationType.Power,
                            "%" => BinaryOperationType.Modulo,
                            ">" => BinaryOperationType.GreaterThan,
                            "<" => BinaryOperationType.LessThan,
                            ">=" => BinaryOperationType.GreaterThanOrEqual,
                            "<=" => BinaryOperationType.LessThanOrEqual,
                            "==" => BinaryOperationType.Equal,
                            "!=" => BinaryOperationType.NotEqual,
                            _ => throw new InvalidOperationException($"Unknown operator: {token.Value}")
                        };
                        stack.Push(new BinaryOperationExpression(left, right, opType));
                        break;

                    case TokenType.Function:
                        var funcName = token.Value;
                        if (!functions.TryGetValue(funcName.ToLower(), out int expectedArgCount))
                        {
                            throw new ArgumentException($"Unknown function: {funcName}");
                        }

                        var args = new List<IMathExpression>();

                        if (expectedArgCount == -1) // Variable argument function
                        {
                            // 检查前一个token是否是参数计数
                            if (i > 0 && rpn[i - 1].Type == TokenType.Number &&
                                int.TryParse(rpn[i - 1].Value, out int actualArgCount))
                            {
                                // 从栈中弹出参数计数(它已经被作为NumberExpression压入栈中)
                                stack.Pop(); // 移除参数计数

                                // 收集实际参数
                                for (int j = 0; j < actualArgCount; j++)
                                {
                                    if (stack.Count == 0)
                                        throw new ArgumentException($"Not enough arguments for function {funcName}");
                                    args.Insert(0, stack.Pop());
                                }
                            }
                            else
                            {
                                // 如果没有参数计数，假设只有一个参数
                                if (stack.Count == 0)
                                    throw new ArgumentException($"Not enough arguments for function {funcName}");
                                args.Add(stack.Pop());
                            }
                        }
                        else // Fixed argument function
                        {
                            for (int j = 0; j < expectedArgCount; j++)
                            {
                                if (stack.Count == 0)
                                    throw new ArgumentException($"Not enough arguments for function {funcName}");
                                args.Insert(0, stack.Pop());
                            }
                        }

                        stack.Push(new FunctionExpression(funcName, args));
                        break;
                }
            }

            if (stack.Count != 1)
                throw new ArgumentException("Invalid expression: stack should contain exactly one element");

            return stack.Pop();
        }


    }
}
