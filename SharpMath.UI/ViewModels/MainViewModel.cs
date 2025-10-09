using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows;
using SharpMath.Core;
using System.Collections.ObjectModel;
using System.Linq.Expressions;
using System.IO;
using System.Windows.Media.Imaging;
using System.Windows.Controls;
using System.Windows.Media;
using CSharpMath.Rendering.FrontEnd;
using CSharpMath.SkiaSharp;
using SkiaSharp;
using static SharpMath.Core.MatrixSolver;
using System.Text.RegularExpressions;
using MatrixConverter = SharpMath.Core.MatrixConverter;
using CSharpMath.Structures;

namespace SharpMath.UI.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private MathPainter mathPainter;

        private readonly IParser parser = new AdvancedMathParser();
        private readonly IParser latexParser = new LatexParser();
        private IMathExpression lastSimplifiedExpression;

        // 运算类型列表
        public ObservableCollection<CalculateType> CalculateList { get; set; }

        public ObservableCollection<MatrixViewModel> MatrixList { get; } = new ObservableCollection<MatrixViewModel>();

        // 方程列表
        private List<EquationExpression> equationList = new List<EquationExpression>();
        private bool isGenerating = false; // 是否正在生成方程

        public ObservableCollection<VariableViewModel> VariablesToSubstitute { get; } = new ObservableCollection<VariableViewModel>();

        public MainViewModel()
        {
            CalculateList = new ObservableCollection<CalculateType>(Enum.GetValues(typeof(CalculateType)).Cast<CalculateType>());
            SelectedCalculateType = CalculateList.First();

            SyncToLatex();

            mathPainter = new MathPainter
            {
                FontSize = 14,
                TextColor = SKColors.Black
            };

            // 初始渲染
            RenderMath();
        }

        // 运算类型
        [ObservableProperty] private CalculateType selectedCalculateType;

        // 矩阵运算部分
        [ObservableProperty] private string matrixExpressionText = "";

        [ObservableProperty] private bool isScalarMultiply = true;
        [ObservableProperty] private string scalarValue = "2";
        [ObservableProperty] private string powerValue = "2";
        [ObservableProperty] private bool canAddOperator = false;

        [ObservableProperty] private bool isMatrixMode;
        [ObservableProperty] private string matrixRows = "3";
        [ObservableProperty] private string matrixCols = "3";
        [ObservableProperty] private bool isSingleMatrixMode;
        [ObservableProperty] private bool isMultiMatrixMode;
        [ObservableProperty] private bool canCalculateDeterminant;
        [ObservableProperty] private bool canCalculateInverse;
        [ObservableProperty] private bool canCalculateTrace;
        [ObservableProperty] private bool canCalculatePower;

        // 方程组输入部分
        [ObservableProperty] private bool isEquationSystemMode;
        [ObservableProperty] private string singleEquationInput = "";

        // Latex结果渲染部分
        [ObservableProperty] private BitmapImage? renderedImage;
        [ObservableProperty] private string? renderErrorMessage;
        [ObservableProperty] private bool hasRenderError;
        [ObservableProperty] private double canvasWidth = 80;
        [ObservableProperty] private double canvasHeight = 30;

        // 普通输入部分
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SolveCommand))]
        private string inputExpressionText = "2*x + 3*y + x*(x+y) - 3*x";

        [ObservableProperty] private string latexInputExpressionText = string.Empty;
        [ObservableProperty] private string latexResult = string.Empty;
        [ObservableProperty] private string stepsResult = string.Empty;
        [ObservableProperty] private string textOutput = string.Empty;
        [ObservableProperty] private string latexOutput = string.Empty;
        [ObservableProperty] private string errorMessage = string.Empty;
        [ObservableProperty] private bool isSubstitutionPanelVisible = false;

        #region 事件

        /// <summary>
        /// 运算类型变化事件
        /// </summary>
        partial void OnSelectedCalculateTypeChanged(CalculateType value)
        {
            IsEquationSystemMode = value == CalculateType.求解方程组;
            IsMatrixMode = value == CalculateType.矩阵运算;

            if ((int)value > 1)
            {
                IsSubstitutionPanelVisible = false;
            }

            if (IsEquationSystemMode || IsMatrixMode)
            {
                InputExpressionText = "";
                LatexInputExpressionText = "";
                equationList.Clear();
                MatrixList.Clear();
                MatrixExpressionText = "";
            }
            ErrorMessage = "";
            UpdateMatrixOperationAvailability();
        }

        /// <summary>
        /// 普通输入框的文本变化事件
        /// </summary>
        partial void OnInputExpressionTextChanged(string value)
        {
            if (!isGenerating)
            {
                SyncToLatex();
            }

            if (IsMatrixMode)
            {
                UpdateMatrixOperationAvailability();
            }
        }

        /// <summary>
        /// LaTeX输入框的文本变化事件
        /// </summary>
        partial void OnLatexInputExpressionTextChanged(string value)
        {
            if (!isGenerating)
            {
                SyncFromLatex();
            }
        }


        /// <summary>
        /// 更新可用的矩阵操作按钮的状态
        /// </summary>
        private void UpdateMatrixOperationAvailability()
        {
            try
            {
                IsSingleMatrixMode = MatrixList.Count == 1;
                IsMultiMatrixMode = MatrixList.Count >= 2;

                if (IsSingleMatrixMode)
                {
                    var matrixVm = MatrixList.First();
                    var matrix = matrixVm.MatrixText.ToMatrixExpression();
                    bool isSquare = matrix.Rows == matrix.Columns;
                    CanCalculateDeterminant = isSquare;
                    CanCalculateInverse = isSquare;
                    CanCalculateTrace = isSquare;
                    CanCalculatePower = isSquare;
                }
                else
                {
                    CanCalculateDeterminant = CanCalculateInverse = CanCalculateTrace = CanCalculatePower = false;
                }
                ErrorMessage = "";
            }
            catch
            {
                IsSingleMatrixMode = IsMultiMatrixMode = false;
                CanCalculateDeterminant = CanCalculateInverse = CanCalculateTrace = CanCalculatePower = false;
            }
        }

        #endregion

        #region 输入部分

        /// <summary>
        /// 生成一个矩阵模板并添加至输入框
        /// </summary>
        [RelayCommand]
        private void AddMatrix()
        {

            int rows = int.Parse(MatrixRows);
            int cols = int.Parse(MatrixCols);
            if (rows <= 0 || cols <= 0)
            {
                rows = cols = 3;
            }

            var matrixText = MatrixExt.GenerateMatrixText(rows, cols);
            if (!string.IsNullOrWhiteSpace(InputExpressionText))
            {

                InputExpressionText = $"{inputExpressionText}\r\n;\r\n{matrixText}";
            }
            else
            {
                InputExpressionText = matrixText;
            }

        }

        /// <summary>
        /// 添加一个方程到方程组中
        /// </summary>
        [RelayCommand]
        private void AddEquationToSystem()
        {
            if (string.IsNullOrWhiteSpace(SingleEquationInput))
            {
                return;
            }
            try
            {
                isGenerating = true;

                var inputList = SingleEquationInput.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var equaItem in inputList)
                {
                    var expression = parser.Parse(equaItem);
                    if (expression is EquationExpression equation)
                    {
                        equationList.Add(equation);

                        InputExpressionText = string.Join(" ; ", equationList.Select(e => e.ToString()));
                    }
                    else
                    {
                        ErrorMessage = $"{equaItem}不是一个有效的方程";
                        continue;
                    }
                }
                SingleEquationInput = "";
                ErrorMessage = "";
            }
            catch (Exception ex)
            {
                ErrorMessage = $"解析错误: {ex.Message}";
            }
            finally
            {
                SyncToLatex();
                isGenerating = false;
            }
        }

        [RelayCommand]
        private void SyncToLatex()
        {
            if (string.IsNullOrWhiteSpace(InputExpressionText))
            {
                return;
            }
            try
            {
                var expression = parser.Parse(InputExpressionText);
                LatexInputExpressionText = expression.ToLatex();
                ErrorMessage = "";
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error: {ex.Message}";
            }
        }

        [RelayCommand]
        private void SyncFromLatex()
        {
            if (string.IsNullOrWhiteSpace(LatexInputExpressionText))
            {
                return;
            }
            try
            {
                var expression = latexParser.Parse(LatexInputExpressionText);
                InputExpressionText = expression.ToString();
                ErrorMessage = "";
            }
            catch (Exception ex) { ErrorMessage = $"LaTeX Parse Error: {ex.Message}"; }
        }

        #endregion

        #region 结果输出部分

        /// <summary>
        /// 使用 LaTeX 渲染数学公式
        /// </summary>
        [RelayCommand]
        private void RenderMath()
        {
            try
            {
                // 重置错误状态
                HasRenderError = false;
                RenderErrorMessage = null;

                if (string.IsNullOrWhiteSpace(LatexResult))
                {
                    RenderedImage = null;
                    return;
                }

                Size defaultSize = new Size(80, 30);
                var point = new System.Drawing.PointF(10, 5); // 默认渲染位置

                string output = $"{LatexInputExpressionText}  =>  {LatexResult}";

                // 设置 LaTeX 内容
                mathPainter.LaTeX = output;

                // 测量渲染尺寸
                var measure = mathPainter.Measure();
                if (measure.IsEmpty)
                {
                    RenderedImage = null;
                    return;
                }

                // 计算画布尺寸
                int width = (int)Math.Ceiling(measure.Width) + (int)point.X * 2;
                int height = (int)Math.Ceiling(measure.Height) + (int)point.Y * 2;

                // 创建 SkiaSharp 画布
                var info = new SKImageInfo(width, height);

                using (var surface = SKSurface.Create(info))
                {
                    var canvas = surface.Canvas;
                    canvas.Clear(SKColors.White);

                    // 渲染数学公式
                    float renderY = Math.Abs(measure.Top) + point.Y; // 修正渲染位置
                    mathPainter.Draw(canvas, point.X, renderY);

                    // 转换为 WPF 可用的图像
                    using (var image = surface.Snapshot())
                    using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
                    {
                        var stream = new MemoryStream(data.ToArray());
                        var bitmapImage = new BitmapImage();
                        bitmapImage.BeginInit();
                        bitmapImage.StreamSource = stream;
                        bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                        bitmapImage.EndInit();
                        bitmapImage.Freeze();

                        RenderedImage = bitmapImage;

                        // 更新画布尺寸
                        CanvasWidth = Math.Max(width, defaultSize.Width);
                        CanvasHeight = Math.Max(height, defaultSize.Height);
                    }
                }
            }
            catch (Exception ex)
            {
                // 设置错误状态
                HasRenderError = true;
                RenderErrorMessage = $"渲染错误: {ex.Message}";
                RenderedImage = null;
            }
        }

        [RelayCommand]
        private void OcrFromFile()
        {
            InputExpressionText = "\\frac{d}{dx}(x^2)"; // Simulate OCR result
        }

        [RelayCommand]
        private void ClearTextOutput() => TextOutput = string.Empty;

        [RelayCommand]
        private void ClearLatexOutput() => LatexOutput = string.Empty;

        [RelayCommand]
        private void FormatTextOutput() => MessageBox.Show("Formatting feature coming soon!", "Info");

        [RelayCommand]
        private void FormatLatexOutput() => MessageBox.Show("Formatting feature coming soon!", "Info");

        #endregion

        #region 运算部分

        [RelayCommand(CanExecute = nameof(CanSolve))]
        private void Solve()
        {
            if (string.IsNullOrWhiteSpace(InputExpressionText))
            {
                return;
            }
            try
            {
                ErrorMessage = "";
                IsSubstitutionPanelVisible = false;
                VariablesToSubstitute.Clear();

                var result = ExecuteCalculation();
                OutputResult(result);

                // 获取表达式中的变量以展示 (方程/不等式运算不需要此步骤)
                if ((int)SelectedCalculateType < 2)
                {
                    var variables = VariableAppearanceVisitor.GetVariableOrder(result.FinalExpression);
                    if (variables.Any() && !(result.FinalExpression is EquationExpression))
                    {
                        foreach (var varName in variables)
                        {
                            VariablesToSubstitute.Add(new VariableViewModel { Name = $"{varName}: " });
                        }
                        IsSubstitutionPanelVisible = true;
                    }
                }

                RenderMath();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error: {ex.Message}";
            }
        }

        private void OutputResult(CalculationResult result)
        {
            lastSimplifiedExpression = result.FinalExpression;

            // 生成输出结果
            var textSb = new StringBuilder();
            var latexSb = new StringBuilder();
            foreach (var step in result.Steps)
            {
                textSb.AppendLine(step.Display(DisplayPattern.Text));
                latexSb.AppendLine(step.Display(DisplayPattern.Latex));
            }
            TextOutput = textSb.ToString();
            LatexOutput = latexSb.ToString();
            LatexResult = result.FinalExpression.ToLatex();
        }

        private bool CanSolve() => !string.IsNullOrWhiteSpace(InputExpressionText);

        /// <summary>
        /// 执行运算并返回结果
        /// </summary>
        /// <returns></returns>
        private CalculationResult ExecuteCalculation()
        {
            if (selectedCalculateType == CalculateType.矩阵运算)
            {
                var matrixModels = MatrixList.Select(t => new MatrixModel(t.Name, t.MatrixText)).ToList();

                var converter = new MatrixConverter(matrixModels);
                var (expressions, operations) = converter.ConvertToExpressions(InputExpressionText);

                var solver = new MatrixSolver(operations, expressions);
                var result = solver.Process();
                return result;
            }
            else
            {
                var expressionTree = parser.Parse(InputExpressionText);
                ISolver solver = ChooseSolver();
                var result = solver.Process(expressionTree, SortOrder.Normal);

                return result;
            }

        }

        private ISolver ChooseSolver()
        {
            // 不支持矩阵运算或以上模式
            if ((int)SelectedCalculateType > 3)
            {
                return null;
            }

            ISolver solver;

            var expressionTree = parser.Parse(InputExpressionText);

            if (SelectedCalculateType == CalculateType.求解方程组 || (SelectedCalculateType == CalculateType.自动检测 && expressionTree is EquationSystemExpression))
            {
                solver = new EquationSystemSolver();
            }
            else if (SelectedCalculateType == CalculateType.求解方程 || (SelectedCalculateType == CalculateType.自动检测 && expressionTree is EquationExpression))
            {
                solver = new EquationSolver();
            }
            else
            {
                solver = new SimplificationSolver();
            }

            return solver;
        }

        /// <summary>
        /// 使用用户输入的值进行代数运算
        /// </summary>
        [RelayCommand]
        private void CalculateWithValues()
        {
            if (lastSimplifiedExpression == null)
            {
                return;
            }
            try
            {
                var context = new Dictionary<string, double>();
                var valuesDisplay = new List<string>();

                foreach (var vm in VariablesToSubstitute)
                {
                    if (double.TryParse(vm.Value, out double val))
                    {
                        string key = vm.Name.Trim().TrimEnd(':');
                        context[key] = val;
                        valuesDisplay.Add($"{vm.Name} = {val}");
                    }
                    else
                    {
                        throw new FormatException($"Invalid value for variable '{vm.Name}': '{vm.Value}'");
                    }
                }

                double result = lastSimplifiedExpression.Evaluate(context);

                string valuesStr = string.Join(", ", valuesDisplay);
                TextOutput += $"\n--- 代数运算 ---\n代入值 [{valuesStr}] 后运算结果为: {result}\n";
                LatexOutput += $"\n---\\text{{代数运算}}---\n\\text{{代入值 [{valuesStr.Replace("*", "\\cdot")}] 后运算结果为: }}{result}\n";
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Calculation Error: {ex.Message}";
            }
        }

        #endregion

        #region 矩阵构建及运算

        [RelayCommand]
        private void AppendToMatrixExpression(object parameter)
        {
            ErrorMessage = "";
            string token = parameter.ToString();

            // 数乘
            if (IsScalarMultiply && token.Trim() == "×")
            {
                token = $"{token} {ScalarValue}";
            }
            // 乘方
            else if (token.Trim() == "^")
            {
                token = $"{token} {PowerValue}";
            }

            var formatResult = MatrixFormatter.AddAndFormat(MatrixExpressionText, token);
            if (formatResult.Success)
            {
                MatrixExpressionText = formatResult.FormattedExpression;
            }
            else
            {
                ErrorMessage = formatResult.ErrorMessage;
            }
        }

        [RelayCommand]
        private void ApplyMatrix()
        {
            var ret = MatrixExpressionValidator.ValidateCompleteness(MatrixExpressionText);
            if (ret.isComplete)
            {
                InputExpressionText = MatrixExpressionText;
                ErrorMessage = "";
                LatexResult = "";
                RenderMath();
            }
            else
            {
                ErrorMessage = ret.message;
            }
        }

        [RelayCommand]
        private void ResetMatrix()
        {
            MatrixExpressionText = ""; // 清空矩阵表达式
        }


        private void SolveMatrix(MatrixOperation? singleOp = null)
        {
            try
            {
                var matrixModel = MatrixList.Select(t => new MatrixModel(t.Name, t.MatrixText)).ToList().First();
                var expression = matrixModel?.ToMatrixExpression();

                var solver = new MatrixSolver(singleOp.Value);
                var result = solver.Process(expression, SortOrder.Normal);
                OutputResult(result);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"矩阵运算错误: {ex.Message}";
            }

        }


        /// <summary>
        /// 生成一个矩阵并添加到列表中
        /// </summary>
        [RelayCommand]
        private void AddMatrixToList()
        {
            try
            {
                int rows = int.Parse(MatrixRows);
                int cols = int.Parse(MatrixCols);
                string matrixText = MatrixExt.GenerateMatrixText(rows, cols);
                string name = GetNextMatrixName();
                MatrixList.Add(new MatrixViewModel { Name = name, MatrixText = matrixText });
                UpdateMatrixOperationAvailability();
            }
            catch (Exception ex) { ErrorMessage = $"生成矩阵错误: {ex.Message}"; }
        }

        [RelayCommand]
        private void RemoveMatrixElement()
        {
            var result = MatrixFormatter.RemoveLastElement(MatrixExpressionText);
            if (result.Success)
            {
                MatrixExpressionText = result.FormattedExpression;
            }
            else
            {
                ErrorMessage = result.ErrorMessage;
            }
        }


        private string GetNextMatrixName() => GetMatrixNameByIndex(MatrixList.Count);

        private string GetMatrixNameByIndex(int index)
        {
            if (index < 26)
            {
                return ((char)('A' + index)).ToString();
            }
            return $"M{index + 1}";
        }

        [RelayCommand]
        private void DeleteMatrix(MatrixViewModel matrixToDelete)
        {
            if (matrixToDelete == null)
            {
                return;
            }
            string oldName = matrixToDelete.Name;
            int removedIndex = MatrixList.IndexOf(matrixToDelete);

            MatrixList.Remove(matrixToDelete);

            var nameUpdates = new Dictionary<string, string>();
            for (int i = removedIndex; i < MatrixList.Count; i++)
            {
                string newName = GetMatrixNameByIndex(i);
                nameUpdates[MatrixList[i].Name] = newName;
                MatrixList[i].Name = newName;
            }

            string updatedExpression = MatrixExpressionText;
            foreach (var update in nameUpdates.OrderByDescending(kv => kv.Key.Length))
            {
                updatedExpression = Regex.Replace(updatedExpression, $@"\b{update.Key}\b", update.Value);
            }
            updatedExpression = Regex.Replace(updatedExpression, $@"\b{oldName}\b", "").Trim();
            MatrixExpressionText = Regex.Replace(updatedExpression, @"\s+", " "); 

            UpdateMatrixOperationAvailability();
        }

        [RelayCommand]
        private void ClearMatrixWorkspace()
        {
            MatrixList.Clear();
            MatrixExpressionText = "";
            ErrorMessage = "";
            UpdateMatrixOperationAvailability();
        }

        [RelayCommand] private void SolveDeterminant() => SolveMatrix(MatrixOperation.Determinant);
        [RelayCommand] private void SolveTranspose() => SolveMatrix(MatrixOperation.Transpose);
        [RelayCommand] private void SolveTrace() => SolveMatrix(MatrixOperation.Trace);
        [RelayCommand] private void SolveRank() => SolveMatrix(MatrixOperation.Rank);
        [RelayCommand] private void SolveInverse() => SolveMatrix(MatrixOperation.Inverse);
        [RelayCommand] private void SolvePower() => SolveMatrix(MatrixOperation.Power);

        #endregion

    }

}
