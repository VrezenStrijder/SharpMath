using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SharpMath.Core.Parsers.IndependentParser
{
    /// <summary>
    /// 数学函数表达式
    /// (结合MathExpressionParser使用, 用于函数表达式快速运算)
    /// </summary>
    public class MathExpression
    {
        private readonly MathExpressionParser parser;
        private readonly string expression;
        private readonly string[] variables;
        private readonly Delegate compiledExpression;

        public MathExpression(string expression)
        {
            parser = new MathExpressionParser();
            this.expression = expression;

            // 自动检测变量
            var tokens = parser.Tokenize(expression);
            var detectedVars = parser.ExtractVariables(tokens);
            variables = detectedVars.OrderBy(v => v).ToArray();

            // 编译表达式
            if (variables.Length == 0)
            {
                var lambda = parser.Parse(expression);
                compiledExpression = lambda.Compile();
            }
            else
            {
                var lambda = parser.ParseWithVariables(expression, variables);
                compiledExpression = lambda.Compile();
            }
        }

        public string Expression => expression;
        public string[] Variables => (string[])variables.Clone();

        public double Evaluate(params double[] values)
        {
            if (variables.Length == 0)
            {
                return (double)compiledExpression.DynamicInvoke();
            }

            if (values.Length != variables.Length)
            {
                throw new ArgumentException($"Expected {variables.Length} values for variables: {string.Join(", ", variables)}");
            }

            var args = values.Cast<object>().ToArray();
            return (double)compiledExpression.DynamicInvoke(args);
        }

        public double Evaluate(Dictionary<string, double> variableValues)
        {
            var values = new double[variables.Length];
            for (int i = 0; i < variables.Length; i++)
            {
                if (!variableValues.ContainsKey(variables[i]))
                {
                    throw new ArgumentException($"Value for variable '{variables[i]}' not provided.");
                }
                values[i] = variableValues[variables[i]];
            }
            return Evaluate(values);
        }
    }

}
