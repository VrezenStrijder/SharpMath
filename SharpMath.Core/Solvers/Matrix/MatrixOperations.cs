using System;
using System.Collections.Generic;
using System.Text;

namespace SharpMath.Core
{
    public class MatrixOperations
    {
        /// <summary>
        /// 计算行列式
        /// </summary>
        /// <param name="matrix"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static double Determinant(MatrixExpression matrix)
        {
            if (matrix.Rows != matrix.Columns)
            {
                throw new ArgumentException("行列式只能计算方阵");
            }
            return CalculateDeterminant(matrix.Values);
        }

        private static double CalculateDeterminant(double[,] matrix)
        {
            int n = matrix.GetLength(0);
            if (n == 1) return matrix[0, 0];
            if (n == 2) return matrix[0, 0] * matrix[1, 1] - matrix[0, 1] * matrix[1, 0];

            double det = 0;
            for (int j = 0; j < n; j++)
            {
                det += Math.Pow(-1, j) * matrix[0, j] * CalculateDeterminant(GetMinor(matrix, 0, j));
            }
            return det;
        }

        private static double[,] GetMinor(double[,] matrix, int row, int col)
        {
            int n = matrix.GetLength(0);
            var minor = new double[n - 1, n - 1];
            int r = 0;
            for (int i = 0; i < n; i++)
            {
                if (i == row) continue;
                int c = 0;
                for (int j = 0; j < n; j++)
                {
                    if (j == col) continue;
                    minor[r, c] = matrix[i, j];
                    c++;
                }
                r++;
            }
            return minor;
        }

        /// <summary>
        /// 转置矩阵
        /// </summary>
        public static MatrixExpression Transpose(MatrixExpression matrix)
        {
            var result = new double[matrix.Columns, matrix.Rows];
            for (int i = 0; i < matrix.Rows; i++)
            {
                for (int j = 0; j < matrix.Columns; j++)
                {
                    result[j, i] = matrix.Values[i, j];
                }
            }

            string name = $"{MatrixConverter.ConvertOperationToSymbol(MatrixOperation.Transpose)}({matrix.Name})";
            return new MatrixExpression(result, name);
        }

        /// <summary>
        /// 矩阵迹
        /// </summary>
        public static double Trace(MatrixExpression matrix)
        {
            if (matrix.Rows != matrix.Columns)
            {
                throw new ArgumentException("迹只能计算方阵");
            }
            double trace = 0;
            for (int i = 0; i < matrix.Rows; i++)
            {
                trace += matrix.Values[i, i];
            }
            return trace;
        }

        /// <summary>
        /// 矩阵的秩
        /// </summary>
        public static int Rank(MatrixExpression matrix)
        {
            var copy = (double[,])matrix.Values.Clone();
            int rows = copy.GetLength(0);
            int cols = copy.GetLength(1);
            int rank = 0;

            for (int row = 0; row < Math.Min(rows, cols); row++)
            {
                // 找主元
                int maxRow = row;
                for (int i = row + 1; i < rows; i++)
                {
                    if (Math.Abs(copy[i, row]) > Math.Abs(copy[maxRow, row]))
                    {
                        maxRow = i;
                    }
                }

                if (Math.Abs(copy[maxRow, row]) < 1e-10)
                {
                    continue;
                }
                // 交换行
                if (maxRow != row)
                {
                    for (int j = 0; j < cols; j++)
                    {
                        (copy[row, j], copy[maxRow, j]) = (copy[maxRow, j], copy[row, j]);
                    }
                }

                rank++;

                // 消元
                for (int i = row + 1; i < rows; i++)
                {
                    double factor = copy[i, row] / copy[row, row];
                    for (int j = row; j < cols; j++)
                    {
                        copy[i, j] -= factor * copy[row, j];
                    }
                }
            }

            return rank;
        }

        /// <summary>
        /// 逆矩阵
        /// </summary>
        public static MatrixExpression Inverse(MatrixExpression matrix)
        {
            if (matrix.Rows != matrix.Columns)
            {
                throw new ArgumentException("只有方阵才有逆矩阵");
            }
            int n = matrix.Rows;
            var augmented = new double[n, 2 * n];

            // 构造增广矩阵 [A | I]
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    augmented[i, j] = matrix.Values[i, j];
                    augmented[i, j + n] = (i == j) ? 1 : 0;
                }
            }

            // 高斯-约旦消元
            for (int i = 0; i < n; i++)
            {
                // 找主元
                int maxRow = i;
                for (int k = i + 1; k < n; k++)
                {
                    if (Math.Abs(augmented[k, i]) > Math.Abs(augmented[maxRow, i]))
                    {
                        maxRow = k;
                    }
                }

                if (Math.Abs(augmented[maxRow, i]) < 1e-10)
                {
                    throw new InvalidOperationException("矩阵不可逆");
                }
                // 交换行
                if (maxRow != i)
                {
                    for (int j = 0; j < 2 * n; j++)
                    {
                        (augmented[i, j], augmented[maxRow, j]) = (augmented[maxRow, j], augmented[i, j]);
                    }
                }

                // 归一化
                double pivot = augmented[i, i];
                for (int j = 0; j < 2 * n; j++)
                {
                    augmented[i, j] /= pivot;
                }

                // 消元
                for (int k = 0; k < n; k++)
                {
                    if (k != i)
                    {
                        double factor = augmented[k, i];
                        for (int j = 0; j < 2 * n; j++)
                        {
                            augmented[k, j] -= factor * augmented[i, j];
                        }
                    }
                }
            }

            // 提取逆矩阵
            var inverse = new double[n, n];
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    inverse[i, j] = augmented[i, j + n];
                }
            }

            string name = $"{MatrixConverter.ConvertOperationToSymbol(MatrixOperation.Inverse)}({matrix.Name}) ";
            return new MatrixExpression(inverse, name);
        }

        /// <summary>
        /// 矩阵幂
        /// </summary>
        public static MatrixExpression Power(MatrixExpression matrix, int n)
        {
            if (matrix.Rows != matrix.Columns)
            {
                throw new ArgumentException("只有方阵才能计算幂");
            }

            string name = $"{matrix.Name} {MatrixConverter.ConvertOperationToSymbol(MatrixOperation.Power)} {n}";

            if (n == 0)
            {
                // 返回单位矩阵
                var identity = new double[matrix.Rows, matrix.Rows];
                for (int i = 0; i < matrix.Rows; i++)
                {
                    identity[i, i] = 1;
                }
                return new MatrixExpression(identity, name);
            }

            var result = matrix;
            for (int i = 1; i < n; i++)
            {
                result = Multiply(result, matrix);
            }
            result.Name = name;
            return result;
        }

        /// <summary>
        /// 矩阵加法
        /// </summary>
        public static MatrixExpression Add(MatrixExpression a, MatrixExpression b)
        {
            if (a.Rows != b.Rows || a.Columns != b.Columns)
            {
                throw new ArgumentException("矩阵维度不匹配");
            }
            var result = new double[a.Rows, a.Columns];
            for (int i = 0; i < a.Rows; i++)
            {
                for (int j = 0; j < a.Columns; j++)
                {
                    result[i, j] = a.Values[i, j] + b.Values[i, j];
                }
            }
            string name = $"{a.Name} {MatrixConverter.ConvertOperationToSymbol(MatrixOperation.Add)} {b.Name}";
            return new MatrixExpression(result, name);
        }

        /// <summary>
        /// 矩阵减法
        /// </summary>
        public static MatrixExpression Subtract(MatrixExpression a, MatrixExpression b)
        {
            if (a.Rows != b.Rows || a.Columns != b.Columns)
            {
                throw new ArgumentException("矩阵维度不匹配");
            }
            var result = new double[a.Rows, a.Columns];
            for (int i = 0; i < a.Rows; i++)
            {
                for (int j = 0; j < a.Columns; j++)
                {
                    result[i, j] = a.Values[i, j] - b.Values[i, j];
                }
            }

            string name = $"{a.Name} {MatrixConverter.ConvertOperationToSymbol(MatrixOperation.Subtract)} {b.Name}";
            return new MatrixExpression(result, name);
        }

        /// <summary>
        /// 矩阵点乘
        /// </summary>
        public static MatrixExpression HadamardProduct(MatrixExpression a, MatrixExpression b)
        {
            if (a.Rows != b.Rows || a.Columns != b.Columns)
            {
                throw new ArgumentException("矩阵维度必须相同才能进行点乘");
            }
            var result = new double[a.Rows, a.Columns];
            for (int i = 0; i < a.Rows; i++)
            {
                for (int j = 0; j < a.Columns; j++)
                {
                    result[i, j] = a.Values[i, j] * b.Values[i, j];
                }
            }

            string name = $"{a.Name} {MatrixConverter.ConvertOperationToSymbol(MatrixOperation.HadamardProduct)} {b.Name}";
            return new MatrixExpression(result, name);
        }

        /// <summary>
        /// 矩阵乘法
        /// </summary>
        public static MatrixExpression Multiply(MatrixExpression a, MatrixExpression b)
        {
            if (a.Columns != b.Rows)
            {
                throw new ArgumentException("矩阵维度不匹配");
            }
            var result = new double[a.Rows, b.Columns];
            for (int i = 0; i < a.Rows; i++)
            {
                for (int j = 0; j < b.Columns; j++)
                {
                    for (int k = 0; k < a.Columns; k++)
                    {
                        result[i, j] += a.Values[i, k] * b.Values[k, j];
                    }
                }
            }

            string name = $"{a.Name} {MatrixConverter.ConvertOperationToSymbol(MatrixOperation.Multiply)} {b.Name}";
            return new MatrixExpression(result, name);
        }

        /// <summary>
        /// 矩阵数乘
        /// </summary>
        public static MatrixExpression ScalarMultiply(MatrixExpression matrix, double scalar)
        {
            var result = new double[matrix.Rows, matrix.Columns];
            for (int i = 0; i < matrix.Rows; i++)
            {
                for (int j = 0; j < matrix.Columns; j++)
                {
                    result[i, j] = matrix.Values[i, j] * scalar;
                }
            }

            string name = $"{scalar} {MatrixConverter.ConvertOperationToSymbol(MatrixOperation.ScalarMultiply)} {matrix.Name}";
            return new MatrixExpression(result, name);
        }

    }
}
