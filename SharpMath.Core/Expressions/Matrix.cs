using System;
using System.Collections.Generic;
using System.Text;

namespace SharpMath.Core
{
    /// <summary>
    /// 矩阵表达式
    /// </summary>
    public class MatrixExpression : IMathExpression
    {
        public MatrixExpression(double[,] values, string name = "")
        {
            Values = values;
            Rows = values.GetLength(0);
            Columns = values.GetLength(1);
            Name = name;
        }

        public string Name { get; set; }

        public double[,] Values { get; }

        public int Rows { get; }

        public int Columns { get; }

        public double Evaluate() => throw new InvalidOperationException("矩阵表达式不支持单独的计算");

        public double Evaluate(Dictionary<string, double> args) => throw new InvalidOperationException("矩阵表达式不支持单独的计算");

        public string ToLatex()
        {
            var sb = new StringBuilder();
            sb.Append("\\begin{pmatrix}");
            for (int i = 0; i < Rows; i++)
            {
                for (int j = 0; j < Columns; j++)
                {
                    sb.Append(Values[i, j]);
                    if (j < Columns - 1) sb.Append(" & ");
                }
                if (i < Rows - 1) sb.Append(" \\\\ ");
            }
            sb.Append("\\end{pmatrix}");
            return sb.ToString();
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[");
            for (int i = 0; i < Rows; i++)
            {
                sb.Append("  [");
                for (int j = 0; j < Columns; j++)
                {
                    sb.Append($"{Values[i, j],8:F3}");
                    if (j < Columns - 1) sb.Append(", ");
                }
                sb.Append("]");
                if (i < Rows - 1) sb.AppendLine(",");
            }
            sb.AppendLine("\n]");
            return sb.ToString();
        }

        public T Accept<T>(IExpressionVisitor<T> visitor) => visitor.Visit(this);

        public int GetPrecedence() => 100;

    }

    public class MatrixModel
    {
        public MatrixModel()
        {
        }

        public MatrixModel(string key, string matrixText)
        {
            Key = key;
            MatrixText = matrixText;
        }

        /// <summary>
        /// 矩阵的代号
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        /// 矩阵文本
        /// </summary>
        public string MatrixText { get; set; }
    }

    /// <summary>
    /// 矩阵扩展
    /// (用于矩阵文本和矩阵表达式的相互转换)
    /// </summary>
    public static class MatrixExt
    {
        /// 生成矩阵文本模板
        /// </summary>
        /// <param name="rows">行数</param>
        /// <param name="columns">列数</param>
        /// <param name="fillValue">填充值</param>
        /// <returns>矩阵文本</returns>
        public static string GenerateMatrixText(int rows, int columns, double fillValue = 0)
        {
            var sb = new StringBuilder();

            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < columns; j++)
                {
                    sb.Append(fillValue);
                    if (j < columns - 1)
                    {
                        sb.Append("    "); // 四个空格
                    }
                }
                if (i < rows - 1)
                {
                    sb.AppendLine();
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// 生成带示例数据的矩阵文本
        /// </summary>
        /// <param name="rows">行数</param>
        /// <param name="columns">列数</param>
        /// <param name="startValue">起始值</param>
        /// <returns>矩阵文本</returns>
        public static string GenerateMatrixTextWithSampleData(int rows, int columns, int startValue = 1)
        {
            var sb = new StringBuilder();
            int value = startValue;

            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < columns; j++)
                {
                    sb.Append(value++);
                    if (j < columns - 1)
                    {
                        sb.Append("    "); // 四个空格
                    }
                }
                if (i < rows - 1)
                {
                    sb.AppendLine();
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// 从矩阵文本直接创建MatrixExpression
        /// </summary>
        /// <param name="matrixText">矩阵文本</param>
        /// <returns>MatrixExpression对象</returns>
        public static MatrixExpression ToMatrixExpression(this string matrixText, string matrixName = "")
        {
            var matrix = ParseMatrixText(matrixText);
            return new MatrixExpression(matrix, matrixName);
        }

        public static MatrixExpression ToMatrixExpression(this MatrixModel matrix)
        {
            var matrixArray = ParseMatrixText(matrix.MatrixText);
            return new MatrixExpression(matrixArray, matrix.Key);
        }

        /// <summary>
        /// 将MatrixExpression转换为矩阵文本
        /// </summary>
        /// <param name="matrixExpr">MatrixExpression对象</param>
        /// <param name="format">数值格式</param>
        /// <returns>矩阵文本</returns>
        public static string ToMatrixText(this MatrixExpression matrixExpr, string format = "G")
        {
            return ToMatrixText(matrixExpr.Values, format);
        }

        /// <summary>
        /// 从矩阵文本解析为二维数组
        /// </summary>
        /// <param name="matrixText">矩阵文本</param>
        /// <returns>二维数组</returns>
        public static double[,] ParseMatrixText(string matrixText)
        {
            if (string.IsNullOrWhiteSpace(matrixText))
            {
                throw new ArgumentException("矩阵文本不能为空");
            }

            // 按行分割
            var lines = matrixText.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0)
            {
                throw new ArgumentException("矩阵文本格式错误：没有有效的行");
            }

            // 解析第一行以确定列数
            var firstRowValues = ParseRow(lines[0]);
            int rows = lines.Length;
            int columns = firstRowValues.Length;

            if (columns == 0)
            {
                throw new ArgumentException("矩阵文本格式错误：第一行没有有效的数值");
            }

            // 创建二维数组
            var matrix = new double[rows, columns];

            // 填充第一行
            for (int j = 0; j < columns; j++)
            {
                matrix[0, j] = firstRowValues[j];
            }

            // 解析并填充其余行
            for (int i = 1; i < rows; i++)
            {
                var rowValues = ParseRow(lines[i]);
                if (rowValues.Length != columns)
                {
                    throw new ArgumentException($"矩阵文本格式错误：第{i + 1}行的列数({rowValues.Length})与第一行的列数({columns})不一致");
                }

                for (int j = 0; j < columns; j++)
                {
                    matrix[i, j] = rowValues[j];
                }
            }

            return matrix;
        }

        /// <summary>
        /// 将二维数组转换为矩阵文本
        /// </summary>
        /// <param name="matrix">二维数组</param>
        /// <param name="format">数值格式，默认为"G"</param>
        /// <returns>矩阵文本</returns>
        public static string ToMatrixText(this double[,] matrix, string format = "G")
        {
            var sb = new StringBuilder();
            int rows = matrix.GetLength(0);
            int columns = matrix.GetLength(1);

            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < columns; j++)
                {
                    sb.Append(matrix[i, j].ToString(format));
                    if (j < columns - 1)
                    {
                        sb.Append("    "); // 四个空格
                    }
                }
                if (i < rows - 1)
                {
                    sb.AppendLine();
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// 解析一行文本
        /// </summary>
        private static double[] ParseRow(string row)
        {
            // 使用正则表达式分割，支持多个空格
            var values = row.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            var result = new double[values.Length];

            for (int i = 0; i < values.Length; i++)
            {
                if (!double.TryParse(values[i], out result[i]))
                {
                    throw new ArgumentException($"无法解析数值：'{values[i]}'");
                }
            }

            return result;
        }

        /// <summary>
        /// 生成C#代码格式的矩阵初始化字符串
        /// </summary>
        /// <param name="matrix">二维数组</param>
        /// <returns>C#代码字符串</returns>
        public static string ToCSharpCode(this double[,] matrix)
        {
            var sb = new StringBuilder();
            sb.AppendLine("new double[,]");
            sb.AppendLine("{");

            int rows = matrix.GetLength(0);
            int columns = matrix.GetLength(1);

            for (int i = 0; i < rows; i++)
            {
                sb.Append("    { ");
                for (int j = 0; j < columns; j++)
                {
                    sb.Append(matrix[i, j]);
                    if (j < columns - 1)
                    {
                        sb.Append(", ");
                    }
                }
                sb.Append(" }");
                if (i < rows - 1)
                {
                    sb.Append(",");
                }
                sb.AppendLine();
            }

            sb.Append("}");
            return sb.ToString();
        }

        /// <summary>
        /// 从矩阵文本生成C#代码
        /// </summary>
        /// <param name="matrixText">矩阵文本</param>
        /// <returns>C#代码字符串</returns>
        public static string ToCSharpCode(this string matrixText)
        {
            var matrix = ParseMatrixText(matrixText);
            return ToCSharpCode(matrix);
        }

    }
}
