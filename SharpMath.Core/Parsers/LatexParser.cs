using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace SharpMath.Core
{
    public class LatexParser : IParser
    {
        private string latex;
        private int position;

        public IMathExpression Parse(string input)
        {
            latex = input.Replace(" ", "").Replace("\\cdot", "*");
            position = 0;
            return ParseExpression();
        }

        private IMathExpression ParseExpression()
        {
            var expr = ParseTerm();
            while (position < latex.Length)
            {
                char op = latex[position];
                if (op == '+' || op == '-')
                {
                    position++;
                    var right = ParseTerm();
                    var opType = op == '+' ? BinaryOperationType.Add : BinaryOperationType.Subtract;
                    expr = new BinaryOperationExpression(expr, right, opType);
                }
                else if (op == '=')
                {
                    position++;
                    var right = ParseExpression(); // Parse the rest of the expression
                    expr = new EquationExpression(expr, right);
                    break; // Equation is the root
                }
                else { break; }
            }
            return expr;
        }

        private IMathExpression ParseTerm()
        {
            var expr = ParseFactor();
            while (position < latex.Length)
            {
                char op = latex[position];
                if (op == '*' || op == '/')
                {
                    position++;
                    var right = ParseFactor();
                    var opType = op == '*' ? BinaryOperationType.Multiply : BinaryOperationType.Divide;
                    expr = new BinaryOperationExpression(expr, right, opType);
                }
                else { break; }
            }
            return expr;
        }

        private IMathExpression ParseFactor()
        {
            IMathExpression expr;
            if (position < latex.Length && latex[position] == '-')
            {
                position++;
                expr = new UnaryOperationExpression(ParseFactor(), UnaryOperationType.Negate);
            }
            else if (position < latex.Length && latex[position] == '(')
            {
                position++; // Skip '('
                expr = ParseExpression();
                if (position >= latex.Length || latex[position] != ')')
                {
                    throw new FormatException("Mismatched parentheses in LaTeX.");
                }
                position++; // Skip ')'
            }
            else if (position < latex.Length && latex[position] == '\\')
            {
                expr = ParseCommand();
            }
            else
            {
                expr = ParseNumberOrVariable();
            }

            if (position < latex.Length && latex[position] == '^')
            {
                position++;
                var exponent = ParseGroup();
                expr = new BinaryOperationExpression(expr, exponent, BinaryOperationType.Power);
            }

            return expr;
        }

        private IMathExpression ParseCommand()
        {
            position++; // Skip '\'
            var commandMatch = Regex.Match(latex.Substring(position), @"^[a-zA-Z]+");
            if (!commandMatch.Success)
            {
                throw new FormatException("Invalid LaTeX command.");
            }
            var command = commandMatch.Value;
            position += command.Length;

            if (command == "frac")
            {
                var numerator = ParseGroup();
                var denominator = ParseGroup();
                return new BinaryOperationExpression(numerator, denominator, BinaryOperationType.Divide);
            }
            if (command == "sqrt")
            {
                var argument = ParseGroup();
                return new FunctionExpression("sqrt", new List<IMathExpression> { argument });
            }
            // Add other functions like sin, cos, log
            var args = new List<IMathExpression>();
            if (position < latex.Length && latex[position] == '(')
            {
                position++; // Skip '('
                args.Add(ParseExpression());
                if (position >= latex.Length || latex[position] != ')')
                {
                    throw new FormatException("Mismatched parentheses in LaTeX function.");
                }
                position++; // Skip ')'
            }
            return new FunctionExpression(command, args);
        }

        private IMathExpression ParseGroup()
        {
            if (position >= latex.Length) throw new FormatException("Expected group in LaTeX not found.");
            if (latex[position] == '{')
            {
                position++; // Skip '{'
                var expr = ParseExpression();
                if (position >= latex.Length || latex[position] != '}')
                {
                    throw new FormatException("Mismatched braces in LaTeX.");
                }
                position++; // Skip '}'
                return expr;
            }
            return ParseFactor(); // For single-character groups like x^2
        }

        private IMathExpression ParseNumberOrVariable()
        {
            var numMatch = Regex.Match(latex.Substring(position), @"^[0-9]+(\.[0-9]+)?");
            if (numMatch.Success)
            {
                position += numMatch.Length;
                return new NumberExpression(double.Parse(numMatch.Value));
            }
            var varMatch = Regex.Match(latex.Substring(position), @"^[a-zA-Z]");
            if (varMatch.Success)
            {
                position += varMatch.Length;
                return new VariableExpression(varMatch.Value);
            }
            throw new FormatException("Unexpected token in LaTeX string.");
        }
    }

}
