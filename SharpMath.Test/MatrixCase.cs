using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpMath.Core;

namespace SharpMath.Test
{
    public class MatrixCase
    {
        public static void SimpleCase()
        {
            var matrixData = new double[,]
            {
                { 1, 2, 3 },
                { 4, 5, 6 },
                { 7, 8, 9 }
            };

            var matrix = new MatrixExpression(matrixData);
            var matrixSolver = new MatrixSolver(MatrixOperation.Determinant);
            var matrixResult = matrixSolver.Process(matrix);

            Console.WriteLine(matrixResult.AnswerText);
        }

        public static void MixedCase() {

            // 创建三个矩阵
            var matrixA = MatrixExt.ToMatrixExpression(@"1    2
                                                        3    4");

            var matrixB = MatrixExt.ToMatrixExpression(@"5    6
                                                        7    8");

            var matrixC = MatrixExt.ToMatrixExpression(@"1    0
                                                        0    1");

            // 示例1：A ⊙ B × C (先点乘，再叉乘)
            var operations1 = new List<MatrixOperationItem>
            {
                new MatrixOperationItem(MatrixOperation.HadamardProduct),
                new MatrixOperationItem(MatrixOperation.Multiply)
            };

            var expressions1 = new List<IMathExpression> { matrixA, matrixB, matrixC };

            var solver1 = new MatrixSolver(operations1, expressions1);
            var result1 = solver1.Process(null); // 多操作模式不需要传入expression

            Console.WriteLine("运算: A ⊙ B × C");
            foreach (var step in result1.Steps)
            {
                Console.WriteLine(step.Display(DisplayPattern.Text));
            }

            // 示例2：2 × A + B^2
            var operations2 = new List<MatrixOperationItem>
            {
                new MatrixOperationItem(MatrixOperation.ScalarMultiply, scalarValue: 2),
                new MatrixOperationItem(MatrixOperation.Power, powerValue: 2),
                new MatrixOperationItem(MatrixOperation.Add)
            };

            var expressions2 = new List<IMathExpression> { matrixA, matrixB };

            var solver2 = new MatrixSolver(operations2, expressions2);
            var result2 = solver2.Process(null);

            Console.WriteLine("\n运算: 2 × A + B^2");
            foreach (var step in result2.Steps)
            {
                Console.WriteLine(step.Display(DisplayPattern.Text));
            }

            // 示例3：单操作
            var solver3 = new MatrixSolver(MatrixOperation.Determinant);
            var result3 = solver3.Process(matrixA);

            Console.WriteLine("\n运算: det(A)");
            foreach (var step in result3.Steps)
            {
                Console.WriteLine(step.Display(DisplayPattern.Text));
            }
        }

        public static void ConvertCase()
        {
            // 准备矩阵数据

            var matrixList = new List<MatrixModel>
            {
                new MatrixModel { Key = "A", MatrixText = "1    2\n3    4" },
                new MatrixModel { Key = "B3", MatrixText = "5    6\n7    8" },
                new MatrixModel { Key = "C11", MatrixText = "2    0\n0    2" },
                new MatrixModel { Key = "A5", MatrixText = "1    0\n0    1" },
                new MatrixModel { Key = "B4", MatrixText = "2    1\n1    2" },
                new MatrixModel { Key = "C", MatrixText = "3    3\n3    3" }
            };

            var converter = new MatrixConverter(matrixList);

            // 示例1: A + B3 ^ 2 - (2 × inverse(C11))
            Console.WriteLine("示例1: A + B3 ^ 2 - (2 × inverse(C11))");
            var expression1 = "A + B3 ^ 2 - (2 × inverse(C11))";
            try
            {
                var (expressions1, operations1) = converter.ConvertToExpressions(expression1);

                Console.WriteLine($"表达式数量: {expressions1.Count}");
                Console.WriteLine($"操作数量: {operations1.Count}");

                var solver1 = new MatrixSolver(operations1, expressions1);
                var result1 = solver1.Process(null);

                foreach (var step in result1.Steps)
                {
                    Console.WriteLine(step.Display(DisplayPattern.Text));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"错误: {ex.Message}");
            }

            // 示例2: trans(A5) × B4 - (2 × C)
            Console.WriteLine("\n示例2: trans(A5) × B4 - (2 × C)");
            var expression2 = "trans(A5) × B4 - (2 × C)";
            try
            {
                var (expressions2, operations2) = converter.ConvertToExpressions(expression2);

                var solver2 = new MatrixSolver(operations2, expressions2);
                var result2 = solver2.Process(null);

                foreach (var step in result2.Steps)
                {
                    Console.WriteLine(step.Display(DisplayPattern.Text));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"错误: {ex.Message}");
            }

        }
    }
}
