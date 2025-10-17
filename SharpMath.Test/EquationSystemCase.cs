using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpMath.Core;

namespace SharpMath.Test
{
    public class EquationSystemCase
    {

        public static void SimpleCase()
        {
            IParser parser = new AdvancedMathParser();

            var equations = new List<EquationExpression>
            {
                new EquationExpression(
                    parser.Parse("2*x + 3*y - z"),
                    parser.Parse("5")
                ),
                new EquationExpression(
                    parser.Parse("x - y + 2*z"),
                    parser.Parse("5")
                ),
                new EquationExpression(
                    parser.Parse("3*x + y + z"),
                    parser.Parse("8")
                )
            };

            var system = new EquationSystemExpression(equations);
            var systemSolver = new EquationSystemSolver();
            var result = systemSolver.Process(system);

            Console.WriteLine(result.AnswerText);
        }
    }


}
