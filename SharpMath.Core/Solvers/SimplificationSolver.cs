using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace SharpMath.Core
{
    /// <summary>
    /// 符号化简求解器
    /// </summary>
    public class SimplificationSolver : ISolver
    {
        public CalculationResult Process(IMathExpression expression, SortOrder sortOrder = SortOrder.Normal)
        {
            var steps = new List<CalculationStep> { new CalculationStep(expression, 0) };

            var visitor = new SimplificationVisitor(expression, sortOrder, 1);
            var finalExpression = visitor.Simplify(expression);

            steps.AddRange(visitor.Steps);

            if (steps.Last().ResultingExpression.ToString() != finalExpression.ToString())
            {
                steps.Add(new CalculationStep(finalExpression, steps.Count));
            }

            return new CalculationResult
            {
                OriginalExpression = expression,
                FinalExpression = finalExpression,
                Steps = steps
            };
        }
    }


    public interface ISolver
    {
        CalculationResult Process(IMathExpression expression, SortOrder sortOrder = SortOrder.Normal);
    }

}
