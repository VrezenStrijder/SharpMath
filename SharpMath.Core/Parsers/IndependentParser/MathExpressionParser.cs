using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace SharpMath.Core.Parsers.IndependentParser
{
    /// <summary>
    /// 数学表达式解析器
    /// (独立版本, 可直接返回表达式树)
    /// </summary>
    public class MathExpressionParser
    {
        private readonly Dictionary<string, OperatorDefinition> operators;
        private readonly HashSet<string> mathFunctions;
        private readonly Dictionary<string, double> constants;
        private string operatorPattern;

        public MathExpressionParser()
        {
            operators = new Dictionary<string, OperatorDefinition>();
            mathFunctions = new HashSet<string>
            {
                "sin", "cos", "tan", "asin", "acos", "atan", "sinh", "cosh", "tanh",
                "abs", "sqrt", "exp", "log", "log10", "pow", "min", "max",
                "floor", "ceiling", "round", "sign"
            };
            constants = new Dictionary<string, double>
            {
                { "pi", Math.PI },
                { "e", Math.E }
            };

            // 注册默认运算符
            RegisterDefaultOperators();
            UpdateOperatorPattern();
        }


        private void RegisterDefaultOperators()
        {
            // 基本算术运算符
            RegisterBinaryOperator("+", 1, (left, right) => Expression.Add(left, right));
            RegisterBinaryOperator("-", 1, (left, right) => Expression.Subtract(left, right));
            RegisterBinaryOperator("*", 2, (left, right) => Expression.Multiply(left, right));
            RegisterBinaryOperator("/", 2, (left, right) => Expression.Divide(left, right));

            // 幂运算
            RegisterBinaryOperator("^", 3, (left, right) =>
            {
                var powMethod = typeof(Math).GetMethod("Pow");
                return Expression.Call(powMethod, left, right);
            });

            // 取模运算
            RegisterBinaryOperator("%", 2, (left, right) => Expression.Modulo(left, right));

            #region 比较运算符

            RegisterBinaryOperator(">", 0, (left, right) =>
                Expression.Condition(
                    Expression.GreaterThan(left, right),
                    Expression.Constant(1.0),
                    Expression.Constant(0.0)
                ));

            RegisterBinaryOperator("<", 0, (left, right) =>
                Expression.Condition(
                    Expression.LessThan(left, right),
                    Expression.Constant(1.0),
                    Expression.Constant(0.0)
                ));

            RegisterBinaryOperator(">=", 0, (left, right) =>
                Expression.Condition(
                    Expression.GreaterThanOrEqual(left, right),
                    Expression.Constant(1.0),
                    Expression.Constant(0.0)
                ));

            RegisterBinaryOperator("<=", 0, (left, right) =>
                Expression.Condition(
                    Expression.LessThanOrEqual(left, right),
                    Expression.Constant(1.0),
                    Expression.Constant(0.0)
                ));

            RegisterBinaryOperator("==", 0, (left, right) =>
                Expression.Condition(
                    Expression.Equal(left, right),
                    Expression.Constant(1.0),
                    Expression.Constant(0.0)
                ));

            RegisterBinaryOperator("!=", 0, (left, right) =>
                Expression.Condition(
                    Expression.NotEqual(left, right),
                    Expression.Constant(1.0),
                    Expression.Constant(0.0)
                ));

            #endregion
        }

        /// <summary>
        /// 注册二元运算符
        /// </summary>
        /// <param name="symbol">符号</param>
        /// <param name="precedence">优先级</param>
        /// <param name="operation">符号的操作</param>
        public void RegisterBinaryOperator(string symbol, int precedence, Func<Expression, Expression, Expression> operation)
        {
            operators[symbol] = new OperatorDefinition
            {
                Symbol = symbol,
                Precedence = precedence,
                Type = OperatorType.Binary,
                BinaryOperation = operation
            };
            UpdateOperatorPattern();
        }

        /// <summary>
        /// 注册一元运算符
        /// </summary>
        /// <param name="symbol">符号</param>
        /// <param name="precedence">优先级</param>
        /// <param name="operation">符号的操作</param>
        public void RegisterUnaryOperator(string symbol, int precedence, Func<Expression, Expression> operation)
        {
            operators[symbol] = new OperatorDefinition
            {
                Symbol = symbol,
                Precedence = precedence,
                Type = OperatorType.Unary,
                UnaryOperation = operation
            };
            UpdateOperatorPattern();
        }

        private void UpdateOperatorPattern()
        {
            // 转义特殊字符并创建运算符模式
            var escapedOperators = operators.Keys
                .Select(op => Regex.Escape(op))
                .OrderByDescending(op => op.Length); // 长的运算符优先匹配

            operatorPattern = string.Join("|", escapedOperators);
        }

        /// <summary>
        /// 注册自定义函数
        /// </summary>
        /// <param name="name">函数名</param>
        /// <param name="method">函数方法</param>
        public void RegisterFunction(string name, MethodInfo method)
        {
            mathFunctions.Add(name.ToLower());
        }

        /// <summary>
        /// 注册常量
        /// </summary>
        /// <param name="name">常量名</param>
        /// <param name="value">常量值</param>
        public void RegisterConstant(string name, double value)
        {
            constants[name.ToLower()] = value;
        }

        /// <summary>
        /// 常量定义
        /// </summary>
        private static readonly Dictionary<string, double> Constants = new Dictionary<string, double>
        {
            { "pi", Math.PI },
            { "e", Math.E }
        };

        public Expression<Func<double>> Parse(string expression)
        {
            var tokens = Tokenize(expression);
            var variables = ExtractVariables(tokens);

            if (variables.Count > 0)
            {
                throw new ArgumentException($"Expression contains variables: {string.Join(", ", variables)}. Use ParseWithVariables instead.");
            }

            var rpn = ConvertToRPN(tokens);
            var expressionTree = BuildExpressionTree(rpn);
            return Expression.Lambda<Func<double>>(expressionTree);
        }

        public LambdaExpression ParseWithVariables(string expression, params string[] variableNames)
        {
            var tokens = Tokenize(expression);
            var foundVariables = ExtractVariables(tokens);

            // 如果没有指定变量名, 使用找到的所有变量
            if (variableNames == null || variableNames.Length == 0)
            {
                variableNames = foundVariables.OrderBy(v => v).ToArray();
            }

            // 创建参数表达式
            var parameters = new Dictionary<string, ParameterExpression>();
            var parameterExpressions = new List<ParameterExpression>();

            foreach (var varName in variableNames)
            {
                var param = Expression.Parameter(typeof(double), varName);
                parameters[varName] = param;
                parameterExpressions.Add(param);
            }

            // 检查是否有未定义的变量
            var undefinedVars = foundVariables.Except(variableNames).ToList();
            if (undefinedVars.Count > 0)
            {
                throw new ArgumentException($"Undefined variables: {string.Join(", ", undefinedVars)}");
            }

            var rpn = ConvertToRPN(tokens);
            var expressionTree = BuildExpressionTree(rpn, parameters);

            // 动态创建正确的Func类型
            var funcType = GetFuncType(parameterExpressions.Count);
            return Expression.Lambda(funcType, expressionTree, parameterExpressions);
        }

        /// <summary>
        /// 编译带变量的表达式
        /// </summary>
        public Delegate CompileWithVariables(string expression, params string[] variableNames)
        {
            var lambda = ParseWithVariables(expression, variableNames);
            return lambda.Compile();
        }

        /// <summary>
        /// 计算表达式值
        /// </summary>
        /// <param name="expression">表达式</param>
        /// <param name="variableValues">变量列表</param>
        public double Evaluate(string expression, Dictionary<string, double> variableValues = null)
        {
            var tokens = Tokenize(expression);
            var variables = ExtractVariables(tokens);

            if (variables.Count == 0)
            {
                var expr = Parse(expression);
                return expr.Compile()();
            }
            else
            {
                if (variableValues == null)
                {
                    throw new ArgumentException("Variable values must be provided for expressions with variables.");
                }

                var variableNames = variables.OrderBy(v => v).ToArray();
                var lambda = ParseWithVariables(expression, variableNames);
                var compiled = lambda.Compile();

                // 准备参数值
                var args = new object[variableNames.Length];
                for (int i = 0; i < variableNames.Length; i++)
                {
                    if (!variableValues.ContainsKey(variableNames[i]))
                    {
                        throw new ArgumentException($"Value for variable '{variableNames[i]}' not provided.");
                    }
                    args[i] = variableValues[variableNames[i]];
                }

                return (double)compiled.DynamicInvoke(args);
            }
        }

        public List<Token> Tokenize(string expression)
        {
            var tokens = new List<Token>();

            // 构建正则表达式模式
            var numberPattern = @"\d+\.?\d*";
            var identifierPattern = @"[a-zA-Z_]\w*";
            var parenthesisPattern = @"[()]";
            var whitespacePattern = @"\s+";

            var pattern = $@"({numberPattern})|({identifierPattern})|({operatorPattern})|({parenthesisPattern})|({whitespacePattern})";
            var regex = new Regex(pattern);
            var matches = regex.Matches(expression);

            Token previousToken = null;
            foreach (Match match in matches)
            {
                if (match.Groups[1].Success) // Number
                {
                    tokens.Add(new Token(TokenType.Number, match.Value));
                }
                else if (match.Groups[2].Success) // Identifier (Function, Variable, or Constant)
                {
                    var value = match.Value;
                    var lowerValue = value.ToLower();

                    if (mathFunctions.Contains(lowerValue))
                    {
                        tokens.Add(new Token(TokenType.Function, lowerValue));
                    }
                    else if (constants.ContainsKey(lowerValue))
                    {
                        tokens.Add(new Token(TokenType.Constant, lowerValue));
                    }
                    else
                    {
                        tokens.Add(new Token(TokenType.Variable, value));
                    }
                }
                else if (match.Groups[3].Success) // Operator
                {
                    var value = match.Value;

                    // 检查是否是一元负号
                    if (value == "-" && IsUnaryContext(previousToken))
                    {
                        tokens.Add(new Token(TokenType.UnaryOperator, value));
                    }
                    else
                    {
                        tokens.Add(new Token(TokenType.Operator, value));
                    }
                }
                else if (match.Groups[4].Success) // Parenthesis
                {
                    var value = match.Value;
                    if (value == "(")
                        tokens.Add(new Token(TokenType.LeftParen, value));
                    else if (value == ")")
                        tokens.Add(new Token(TokenType.RightParen, value));
                }
                // Ignore whitespace (group 5)

                // 更新前一个token(忽略空白)
                if (tokens.Count > 0)
                {
                    previousToken = tokens[tokens.Count - 1];
                }
            }

            return tokens;
        }

        // 判断是否是一元运算符的上下文
        private bool IsUnaryContext(Token previousToken)
        {
            if (previousToken == null) return true; // 表达式开头

            return previousToken.Type == TokenType.Operator ||
                   previousToken.Type == TokenType.LeftParen ||
                   previousToken.Type == TokenType.Function ||
                   previousToken.Type == TokenType.UnaryOperator;
        }

        public HashSet<string> ExtractVariables(List<Token> tokens)
        {
            var variables = new HashSet<string>();
            foreach (var token in tokens)
            {
                if (token.Type == TokenType.Variable && !Constants.ContainsKey(token.Value.ToLower()))
                {
                    variables.Add(token.Value);
                }
            }
            return variables;
        }

        private Type GetFuncType(int paramCount)
        {
            switch (paramCount)
            {
                case 0: return typeof(Func<double>);
                case 1: return typeof(Func<double, double>);
                case 2: return typeof(Func<double, double, double>);
                case 3: return typeof(Func<double, double, double, double>);
                case 4: return typeof(Func<double, double, double, double, double>);
                case 5: return typeof(Func<double, double, double, double, double, double>);
                case 6: return typeof(Func<double, double, double, double, double, double, double>);
                case 7: return typeof(Func<double, double, double, double, double, double, double, double>);
                case 8: return typeof(Func<double, double, double, double, double, double, double, double, double>);
                default:
                    throw new ArgumentException($"Too many parameters: {paramCount}. Maximum supported is 8.");
            }
        }

        private List<Token> ConvertToRPN(List<Token> tokens)
        {
            var output = new List<Token>();
            var operatorStack = new Stack<Token>();

            for (int i = 0; i < tokens.Count; i++)
            {
                var token = tokens[i];

                switch (token.Type)
                {
                    case TokenType.Number:
                    case TokenType.Variable:
                    case TokenType.Constant:
                        output.Add(token);
                        break;

                    case TokenType.Function:
                        operatorStack.Push(token);
                        break;

                    case TokenType.UnaryOperator:
                        // 一元负号有最高优先级
                        operatorStack.Push(token);
                        break;

                    case TokenType.Operator:
                        var currentOp = operators[token.Value];

                        while (operatorStack.Count > 0 &&
                               (operatorStack.Peek().Type == TokenType.Operator ||
                                operatorStack.Peek().Type == TokenType.UnaryOperator))
                        {
                            if (operatorStack.Peek().Type == TokenType.UnaryOperator)
                            {
                                // 一元负号总是有更高的优先级
                                break;
                            }
                            else if (operators.ContainsKey(operatorStack.Peek().Value))
                            {
                                var stackOp = operators[operatorStack.Peek().Value];
                                if (stackOp.Precedence >= currentOp.Precedence)
                                {
                                    output.Add(operatorStack.Pop());
                                }
                                else
                                {
                                    break;
                                }
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
                            output.Add(operatorStack.Pop());
                        }
                        if (operatorStack.Count > 0)
                            operatorStack.Pop(); // Remove left parenthesis

                        if (operatorStack.Count > 0 && operatorStack.Peek().Type == TokenType.Function)
                        {
                            output.Add(operatorStack.Pop());
                        }
                        break;
                }
            }

            while (operatorStack.Count > 0)
            {
                output.Add(operatorStack.Pop());
            }

            return output;
        }

        private Expression BuildExpressionTree(List<Token> rpn, Dictionary<string, ParameterExpression> parameters = null)
        {
            var stack = new Stack<Expression>();

            foreach (var token in rpn)
            {
                switch (token.Type)
                {
                    case TokenType.Number:
                        stack.Push(Expression.Constant(double.Parse(token.Value)));
                        break;

                    case TokenType.Constant:
                        stack.Push(Expression.Constant(constants[token.Value]));
                        break;

                    case TokenType.Variable:
                        if (parameters != null && parameters.ContainsKey(token.Value))
                        {
                            stack.Push(parameters[token.Value]);
                        }
                        else
                        {
                            throw new ArgumentException($"Unknown variable: {token.Value}");
                        }
                        break;

                    case TokenType.UnaryOperator:
                        if (stack.Count < 1)
                        {
                            throw new ArgumentException("Unary minus requires 1 operand");
                        }
                        var operand = stack.Pop();
                        stack.Push(Expression.Negate(operand));
                        break;

                    case TokenType.Operator:
                        if (!operators.ContainsKey(token.Value))
                        {
                            throw new ArgumentException($"Unknown operator: '{token.Value}'");
                        }

                        var op = operators[token.Value];

                        if (op.Type == OperatorType.Binary)
                        {
                            if (stack.Count < 2)
                            {
                                throw new ArgumentException($"Binary operator '{token.Value}' requires 2 operands");
                            }
                            var right = stack.Pop();
                            var left = stack.Pop();
                            stack.Push(op.BinaryOperation(left, right));
                        }
                        else // Unary operator
                        {
                            if (stack.Count < 1)
                            {
                                throw new ArgumentException($"Unary operator '{token.Value}' requires 1 operand");
                            }
                            var unaryOperand = stack.Pop();
                            stack.Push(op.UnaryOperation(unaryOperand));
                        }
                        break;

                    case TokenType.Function:
                        var functionName = char.ToUpper(token.Value[0]) + token.Value.Substring(1);
                        MethodInfo method;

                        if (token.Value == "pow" || token.Value == "min" || token.Value == "max")
                        {
                            if (stack.Count < 2)
                            {
                                throw new ArgumentException($"Function {token.Value} requires 2 arguments");
                            }
                            var arg2 = stack.Pop();
                            var arg1 = stack.Pop();
                            method = typeof(Math).GetMethod(functionName, new[] { typeof(double), typeof(double) });
                            stack.Push(Expression.Call(method, arg1, arg2));
                        }
                        else
                        {
                            if (stack.Count < 1)
                            {
                                throw new ArgumentException($"Function {token.Value} requires 1 argument");
                            }
                            var arg = stack.Pop();
                            method = typeof(Math).GetMethod(functionName, new[] { typeof(double) });
                            if (method == null && functionName == "Ln")
                            {
                                method = typeof(Math).GetMethod("Log", new[] { typeof(double) });
                            }
                            stack.Push(Expression.Call(method, arg));
                        }
                        break;
                }
            }

            if (stack.Count != 1)
            {
                throw new ArgumentException($"Invalid expression. Stack contains {stack.Count} items instead of 1.");
            }
            return stack.Pop();
        }


    }

}
