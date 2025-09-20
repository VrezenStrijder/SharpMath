using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace SharpMath.Core
{
    public class ExpressionCase
    {
        public static void Case1()
        {
            var parser = new MathExpressionParser();

            // 示例1：使用默认运算符
            var expr1 = parser.Parse("10 % 3");
            Console.WriteLine($"10 % 3 = {expr1.Compile()()}");

            // 示例2：注册自定义二元运算符
            // 注册一个"**"作为幂运算符（Python风格）
            parser.RegisterBinaryOperator("**", 3, (left, right) =>
            {
                var powMethod = typeof(Math).GetMethod("Pow");
                return Expression.Call(powMethod, left, right);
            });

            var expr2 = parser.Parse("2 ** 8");
            Console.WriteLine($"2 ** 8 = {expr2.Compile()()}");

            // 示例3：注册自定义比较运算符
            parser.RegisterBinaryOperator(">=", 0, (left, right) =>
                Expression.Condition(
                    Expression.GreaterThanOrEqual(left, right),
                    Expression.Constant(1.0),
                    Expression.Constant(0.0)
                ));

            parser.RegisterBinaryOperator("<=", 0, (left, right) =>
                Expression.Condition(
                    Expression.LessThanOrEqual(left, right),
                    Expression.Constant(1.0),
                    Expression.Constant(0.0)
                ));

            var expr3 = parser.ParseWithVariables("x >= 5", "x");
            var func3 = (Func<double, double>)expr3.Compile();
            Console.WriteLine($"Is 6 >= 5? {func3(6)}");
            Console.WriteLine($"Is 4 >= 5? {func3(4)}");

            // 示例4：注册自定义逻辑运算符
            parser.RegisterBinaryOperator("&&", -1, (left, right) =>
                Expression.Condition(
                    Expression.And(
                        Expression.NotEqual(left, Expression.Constant(0.0)),
                        Expression.NotEqual(right, Expression.Constant(0.0))
                    ),
                    Expression.Constant(1.0),
                    Expression.Constant(0.0)
                ));

            parser.RegisterBinaryOperator("||", -2, (left, right) =>
                Expression.Condition(
                    Expression.Or(
                        Expression.NotEqual(left, Expression.Constant(0.0)),
                        Expression.NotEqual(right, Expression.Constant(0.0))
                    ),
                    Expression.Constant(1.0),
                    Expression.Constant(0.0)
                ));

            var expr4 = parser.ParseWithVariables("(x > 0) && (y > 0)", "x", "y");
            var func4 = (Func<double, double, double>)expr4.Compile();
            Console.WriteLine($"(2 > 0) && (3 > 0) = {func4(2, 3)}");

            // 示例5：注册自定义数学运算符
            // 阶乘运算符（简化版，仅用于演示）
            parser.RegisterBinaryOperator("!", 4, (left, right) =>
            {
                // 这里简化处理，实际应该是一元后缀运算符
                var factorialMethod = typeof(CustomMath).GetMethod("Factorial");
                return Expression.Call(factorialMethod, left);
            });

            // 示例6：注册向量点积运算符
            parser.RegisterBinaryOperator("·", 2, (left, right) =>
                Expression.Multiply(left, right)); // 简化版本

            var expr6 = parser.ParseWithVariables("a·b + c·d", "a", "b", "c", "d");
            var func6 = (Func<double, double, double, double, double>)expr6.Compile();
            Console.WriteLine($"2·3 + 4·5 = {func6(2, 3, 4, 5)}");

            // 示例7：创建一个科学计算解析器
            var scientificParser = CreateScientificParser();
            var expr7 = scientificParser.Parse("2 × 10 ^ 3");
            Console.WriteLine($"2 × 10 ^ 3 = {expr7.Compile()()}");

            // 示例8：创建一个编程风格的解析器
            var programmingParser = CreateProgrammingParser();
            var expr8 = programmingParser.ParseWithVariables("x << 2", "x");
            var func8 = (Func<double, double>)expr8.Compile();
            Console.WriteLine($"8 << 2 = {func8(8)}");
        }

        /// <summary>
        /// 科学计算示例
        /// </summary>
        private static MathExpressionParser CreateScientificParser()
        {
            var parser = new MathExpressionParser();

            // 使用×作为乘法符号
            parser.RegisterBinaryOperator("×", 2, (left, right) => Expression.Multiply(left, right));

            // 使用÷作为除法符号
            parser.RegisterBinaryOperator("÷", 2, (left, right) => Expression.Divide(left, right));

            // 注册更多科学计算常量
            parser.RegisterConstant("g", 9.80665); // 重力加速度
            parser.RegisterConstant("c", 299792458); // 光速
            parser.RegisterConstant("h", 6.62607015e-34); // 普朗克常数

            return parser;
        }

        /// <summary>
        /// 创建编程风格的解析器
        /// </summary>
        /// <returns></returns>
        private static MathExpressionParser CreateProgrammingParser()
        {
            var parser = new MathExpressionParser();

            // 位运算符
            parser.RegisterBinaryOperator("<<", 1, (left, right) =>
                Expression.Convert(
                    Expression.LeftShift(
                        Expression.Convert(left, typeof(int)),
                        Expression.Convert(right, typeof(int))
                    ),
                    typeof(double)
                ));

            parser.RegisterBinaryOperator(">>", 1, (left, right) =>
                Expression.Convert(
                    Expression.RightShift(
                        Expression.Convert(left, typeof(int)),
                        Expression.Convert(right, typeof(int))
                    ),
                    typeof(double)
                ));

            parser.RegisterBinaryOperator("&", 0, (left, right) =>
                Expression.Convert(
                    Expression.And(
                        Expression.Convert(left, typeof(int)),
                        Expression.Convert(right, typeof(int))
                    ),
                    typeof(double)
                ));

            parser.RegisterBinaryOperator("|", -1, (left, right) =>
                Expression.Convert(
                    Expression.Or(
                        Expression.Convert(left, typeof(int)),
                        Expression.Convert(right, typeof(int))
                    ),
                    typeof(double)
                ));

            return parser;
        }

        /// <summary>
        /// 自定义数学函数示例
        /// </summary>
        public static class CustomMath
        {
            public static double Factorial(double n)
            {
                if (n < 0) throw new ArgumentException("Factorial is not defined for negative numbers");
                if (n == 0 || n == 1) return 1;

                double result = 1;
                for (int i = 2; i <= (int)n; i++)
                {
                    result *= i;
                }
                return result;
            }
        }


        /// <summary>
        /// 表达式工厂示例
        /// </summary>
        public static void Case2()
        {
            Console.WriteLine("\n--- Parser Factory Demo ---");

            // 科学计算器
            var sciParser = ExpressionParserFactory.CreateParser(ExpressionParserFactory.ParserType.Scientific);
            var sciExpr = sciParser.Parse("2 × g");
            Console.WriteLine($"2 × g = {sciExpr.Compile()()}");

            // 金融计算器
            var finParser = ExpressionParserFactory.CreateParser(ExpressionParserFactory.ParserType.Financial);
            var finExpr = finParser.Parse("15 %of 200");
            Console.WriteLine($"15% of 200 = {finExpr.Compile()()}");

            // 编程计算器
            var progParser = ExpressionParserFactory.CreateParser(ExpressionParserFactory.ParserType.Programming);
            var progExpr = progParser.Parse("16 >> 2");
            Console.WriteLine($"16 >> 2 = {progExpr.Compile()()}");
        }

        public static void Case3()
        {
            Console.WriteLine("\n--- MathExpression Wrapper Demo ---");

            // 创建表达式
            var quadratic = new MathExpression("a*x^2 + b*x + c");
            Console.WriteLine($"Expression: {quadratic.Expression}");
            Console.WriteLine($"Variables: {string.Join(", ", quadratic.Variables)}");

            // 使用位置参数求值
            var result1 = quadratic.Evaluate(1, -2, 1, 2); // a=1, b=-2, c=1, x=2
            Console.WriteLine($"f(1, -2, 1, 2) = {result1}");

            // 使用字典求值
            var values = new Dictionary<string, double>
            {
                { "a", 1 },
                { "b", -2 },
                { "c", 1 },
                { "x", 2 }
            };

            var result2 = quadratic.Evaluate(values);
            Console.WriteLine($"f(a=1, b=-2, c=1, x=2) = {result2}");

            // 创建一个物理公式
            var kineticEnergy = new MathExpression("0.5 * m * v^2");
            var ke = kineticEnergy.Evaluate(new Dictionary<string, double> { { "m", 10 }, { "v", 5 } });
            Console.WriteLine($"Kinetic Energy (m=10, v=5) = {ke} J");

            // 创建一个统计公式
            var normalDist = new MathExpression("(1/sqrt(2*pi*sigma^2)) * exp(-((x-mu)^2)/(2*sigma^2))");
            var pdf = normalDist.Evaluate(new Dictionary<string, double>
            {
                { "x", 0 },
                { "mu", 0 },
                { "sigma", 1 }
            });
            Console.WriteLine($"Normal PDF at x=0 (μ=0, σ=1) = {pdf}");
        }

    }

    /// <summary>
    /// 表达式工厂
    /// </summary>
    public class ExpressionParserFactory
    {
        public enum ParserType
        {
            Standard,
            Scientific,
            Programming,
            Financial
        }

        public static MathExpressionParser CreateParser(ParserType type)
        {
            var parser = new MathExpressionParser();

            switch (type)
            {
                case ParserType.Scientific:
                    ConfigureScientificParser(parser);
                    break;
                case ParserType.Programming:
                    ConfigureProgrammingParser(parser);
                    break;
                case ParserType.Financial:
                    ConfigureFinancialParser(parser);
                    break;
                case ParserType.Standard:
                default:
                    // 使用默认配置
                    break;
            }

            return parser;
        }

        private static void ConfigureScientificParser(MathExpressionParser parser)
        {
            parser.RegisterBinaryOperator("×", 2, (left, right) => Expression.Multiply(left, right));
            parser.RegisterBinaryOperator("÷", 2, (left, right) => Expression.Divide(left, right));
            parser.RegisterBinaryOperator("±", 1, (left, right) =>
                Expression.Add(left, Expression.Multiply(right, Expression.Constant(-1.0))));

            parser.RegisterConstant("g", 9.80665);
            parser.RegisterConstant("c", 299792458);
            parser.RegisterConstant("h", 6.62607015e-34);
            parser.RegisterConstant("k", 1.380649e-23); // 玻尔兹曼常数
            parser.RegisterConstant("na", 6.02214076e23); // 阿伏伽德罗常数
        }

        private static void ConfigureProgrammingParser(MathExpressionParser parser)
        {
            // 位运算
            parser.RegisterBinaryOperator("<<", 1, (left, right) =>
                Expression.Convert(
                    Expression.LeftShift(
                        Expression.Convert(left, typeof(int)),
                        Expression.Convert(right, typeof(int))
                    ),
                    typeof(double)
                ));

            parser.RegisterBinaryOperator(">>", 1, (left, right) =>
                Expression.Convert(
                    Expression.RightShift(
                        Expression.Convert(left, typeof(int)),
                        Expression.Convert(right, typeof(int))
                    ),
                    typeof(double)
                ));

            // 三元运算符的简化版本（使用函数代替）
            // parser.RegisterFunction("if", ...);
        }

        private static void ConfigureFinancialParser(MathExpressionParser parser)
        {
            // 百分比运算符
            parser.RegisterBinaryOperator("%of", 2, (left, right) =>
                Expression.Multiply(Expression.Divide(left, Expression.Constant(100.0)), right));

            // 复利计算
            parser.RegisterBinaryOperator("^t", 3, (left, right) =>
            {
                var powMethod = typeof(Math).GetMethod("Pow");
                return Expression.Call(powMethod, left, right);
            });

            // 金融常量
            parser.RegisterConstant("basis", 365.0); // 天数基准
        }
    }

}
