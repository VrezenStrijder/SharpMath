public class MatrixExpressionConverter
{
    private readonly ObservableCollection<MatrixViewModel> matrixList;
    
    public MatrixExpressionConverter(ObservableCollection<MatrixViewModel> matrixList)
    {
        this.matrixList = matrixList;
    }

    /// <summary>
    /// 将矩阵表达式字符串转换为可执行的操作序列
    /// </summary>
    public (List<IMathExpression> expressions, List<MatrixSolver.Operation> operations) ConvertToExecutable(string expression)
    {
        // 1. 解析表达式
        var elements = MatrixFormatter.ParseExpression(expression);
        
        // 2. 转换为后缀表达式（逆波兰表示法）
        var rpnElements = ConvertToRPN(elements);
        
        // 3. 从RPN构建执行序列
        return BuildExecutionSequence(rpnElements);
    }

    /// <summary>
    /// 将中缀表达式转换为后缀表达式（RPN）
    /// </summary>
    private List<MatrixExpressionElement> ConvertToRPN(List<MatrixExpressionElement> elements)
    {
        var output = new List<MatrixExpressionElement>();
        var operatorStack = new Stack<MatrixExpressionElement>();
        
        foreach (var element in elements)
        {
            switch (element.Type)
            {
                case MatrixElementType.Variable:
                case MatrixElementType.Number:
                    output.Add(element);
                    break;
                    
                case MatrixElementType.Function:
                    operatorStack.Push(element);
                    break;
                    
                case MatrixElementType.Operator:
                    while (operatorStack.Count > 0 && 
                           operatorStack.Peek().Type == MatrixElementType.Operator)
                    {
                        var topOp = operatorStack.Peek();
                        var currentPrecedence = GetPrecedence(element.Value);
                        var topPrecedence = GetPrecedence(topOp.Value);
                        
                        if (topPrecedence >= currentPrecedence)
                        {
                            output.Add(operatorStack.Pop());
                        }
                        else
                        {
                            break;
                        }
                    }
                    operatorStack.Push(element);
                    break;
                    
                case MatrixElementType.LeftParen:
                    operatorStack.Push(element);
                    break;
                    
                case MatrixElementType.RightParen:
                    while (operatorStack.Count > 0 && 
                           operatorStack.Peek().Type != MatrixElementType.LeftParen)
                    {
                        output.Add(operatorStack.Pop());
                    }
                    
                    if (operatorStack.Count > 0)
                    {
                        operatorStack.Pop(); // 移除左括号
                    }
                    
                    // 如果栈顶是函数，也要弹出
                    if (operatorStack.Count > 0 && 
                        operatorStack.Peek().Type == MatrixElementType.Function)
                    {
                        output.Add(operatorStack.Pop());
                    }
                    break;
            }
        }
        
        // 弹出剩余的运算符
        while (operatorStack.Count > 0)
        {
            output.Add(operatorStack.Pop());
        }
        
        return output;
    }

    /// <summary>
    /// 从RPN构建执行序列 - 简化版本
    /// </summary>
    private (List<IMathExpression> expressions, List<MatrixSolver.Operation> operations) BuildExecutionSequence(List<MatrixExpressionElement> rpnElements)
    {
        var expressionStack = new Stack<StackItem>();
        var expressions = new List<IMathExpression>();
        var operations = new List<MatrixSolver.Operation>();
        
        foreach (var element in rpnElements)
        {
            switch (element.Type)
            {
                case MatrixElementType.Variable:
                    // 获取矩阵并添加到表达式列表
                    var matrix = GetMatrixExpression(element.Value);
                    expressions.Add(matrix);
                    expressionStack.Push(new StackItem { IsMatrix = true, Index = expressions.Count - 1 });
                    break;
                    
                case MatrixElementType.Number:
                    // 数字作为标量值存储
                    var value = double.Parse(element.Value);
                    expressionStack.Push(new StackItem { IsScalar = true, ScalarValue = value });
                    break;
                    
                case MatrixElementType.Operator:
                    ProcessOperatorSimplified(element.Value, expressionStack, expressions, operations);
                    break;
                    
                case MatrixElementType.Function:
                    ProcessFunctionSimplified(element.Value, expressionStack, expressions, operations);
                    break;
            }
        }
        
        return (expressions, operations);
    }

    /// <summary>
    /// 处理运算符 - 简化版本
    /// </summary>
    private void ProcessOperatorSimplified(string op, Stack<StackItem> stack, 
        List<IMathExpression> expressions, List<MatrixSolver.Operation> operations)
    {
        if (stack.Count < 2)
        {
            throw new InvalidOperationException($"运算符 {op} 需要两个操作数");
        }
        
        var right = stack.Pop();
        var left = stack.Pop();
        
        // 特殊处理数乘
        if (op == "×" && (left.IsScalar || right.IsScalar))
        {
            StackItem matrixItem;
            double scalarValue;
            
            if (left.IsScalar)
            {
                scalarValue = left.ScalarValue;
                matrixItem = right;
            }
            else
            {
                scalarValue = right.ScalarValue;
                matrixItem = left;
            }
            
            // 确保矩阵项是最后一个表达式
            if (matrixItem.Index != expressions.Count - 1)
            {
                // 需要将目标矩阵移到末尾或创建中间结果
                var targetMatrix = expressions[matrixItem.Index];
                expressions.Add(targetMatrix);
            }
            
            operations.Add(new MatrixSolver.Operation(MatrixOperation.ScalarMultiply, scalarValue));
            stack.Push(new StackItem { IsMatrix = true, Index = expressions.Count - 1 });
        }
        else if (op == "^" && right.IsScalar)
        {
            // 幂运算
            var power = (int)right.ScalarValue;
            
            // 确保左操作数是最后一个表达式
            if (left.Index != expressions.Count - 1)
            {
                var targetMatrix = expressions[left.Index];
                expressions.Add(targetMatrix);
            }
            
            operations.Add(new MatrixSolver.Operation(MatrixOperation.Power, powerValue: power));
            stack.Push(new StackItem { IsMatrix = true, Index = expressions.Count - 1 });
        }
        else
        {
            // 普通二元运算
            if (!left.IsMatrix || !right.IsMatrix)
            {
                throw new InvalidOperationException($"运算符 {op} 需要两个矩阵操作数");
            }
            
            // 确保操作数在正确的位置
            EnsureOperandsInPosition(left, right, expressions);
            
            var operation = ConvertToMatrixOperation(op);
            operations.Add(new MatrixSolver.Operation(operation));
            stack.Push(new StackItem { IsMatrix = true, Index = expressions.Count - 1 });
        }
    }

    /// <summary>
    /// 处理函数 - 简化版本
    /// </summary>
    private void ProcessFunctionSimplified(string func, Stack<StackItem> stack,
        List<IMathExpression> expressions, List<MatrixSolver.Operation> operations)
    {
        if (stack.Count < 1)
        {
            throw new InvalidOperationException($"函数 {func} 需要一个参数");
        }
        
        var operand = stack.Pop();
        
        if (!operand.IsMatrix)
        {
            throw new InvalidOperationException($"函数 {func} 需要矩阵参数");
        }
        
        // 确保操作数是最后一个表达式
        if (operand.Index != expressions.Count - 1)
        {
            var targetMatrix = expressions[operand.Index];
            expressions.Add(targetMatrix);
        }
        
        var operation = ConvertFunctionToOperation(func);
        operations.Add(new MatrixSolver.Operation(operation));
        stack.Push(new StackItem { IsMatrix = true, Index = expressions.Count - 1 });
    }

    /// <summary>
    /// 确保操作数在正确的位置
    /// </summary>
    private void EnsureOperandsInPosition(StackItem left, StackItem right, List<IMathExpression> expressions)
    {
        // MatrixSolver期望：当前结果作为左操作数，下一个矩阵作为右操作数
        // 如果左操作数不是最后一个，需要调整
        
        if (left.Index != expressions.Count - 1)
        {
            // 如果右操作数紧跟在左操作数后面，交换它们的位置
            if (right.Index == left.Index + 1)
            {
                var temp = expressions[left.Index];
                expressions[left.Index] = expressions[right.Index];
                expressions[right.Index] = temp;
            }
            else
            {
                // 否则，将左操作数复制到末尾
                var leftMatrix = expressions[left.Index];
                expressions.Add(leftMatrix);
            }
        }
        
        // 确保右操作数在表达式列表中
        if (right.Index >= expressions.Count)
        {
            throw new InvalidOperationException("右操作数索引超出范围");
        }
    }

    /// <summary>
    /// 获取运算符优先级
    /// </summary>
    private int GetPrecedence(string op)
    {
        var precedenceMap = MatrixFormatter.GetSupportedOperators();
        return precedenceMap.ContainsKey(op) ? precedenceMap[op] : 0;
    }

    /// <summary>
    /// 转换运算符到矩阵操作
    /// </summary>
    private MatrixOperation ConvertToMatrixOperation(string op)
    {
        switch (op)
        {
            case "+": return MatrixOperation.Add;
            case "-": return MatrixOperation.Subtract;
            case "×": return MatrixOperation.Multiply;
            case "⊙": return MatrixOperation.HadamardProduct;
            case "^": return MatrixOperation.Power;
            default: throw new NotSupportedException($"不支持的运算符: {op}");
        }
    }

    /// <summary>
    /// 转换函数到矩阵操作
    /// </summary>
    private MatrixOperation ConvertFunctionToOperation(string func)
    {
        switch (func.ToLower())
        {
            case "inverse": return MatrixOperation.Inverse;
            case "trans": return MatrixOperation.Transpose;
            default: throw new NotSupportedException($"不支持的函数: {func}");
        }
    }

    /// <summary>
    /// 获取矩阵表达式
    /// </summary>
    private MatrixExpression GetMatrixExpression(string matrixName)
    {
        var matrixViewModel = matrixList.FirstOrDefault(m => m.Name == matrixName);
        if (matrixViewModel == null)
        {
            throw new ArgumentException($"找不到矩阵: {matrixName}");
        }
        
        return MatrixTextHelper.ParseToMatrixExpression(matrixViewModel.MatrixText);
    }

    // 简化的栈项类
    private class StackItem
    {
        public bool IsMatrix { get; set; }
        public bool IsScalar { get; set; }
        public int Index { get; set; }  // 在expressions列表中的索引
        public double ScalarValue { get; set; }  // 标量值
    }
}

// 使用示例
public class MatrixExpressionExample
{
    public static void Demo()
    {
        // 准备矩阵数据
        var matrixList = new ObservableCollection<MatrixViewModel>
        {
            new MatrixViewModel { Name = "A", MatrixText = "1    2\n3    4" },
            new MatrixViewModel { Name = "B3", MatrixText = "5    6\n7    8" },
            new MatrixViewModel { Name = "C11", MatrixText = "2    0\n0    2" },
            new MatrixViewModel { Name = "A5", MatrixText = "1    0\n0    1" },
            new MatrixViewModel { Name = "B4", MatrixText = "2    1\n1    2" },
            new MatrixViewModel { Name = "C", MatrixText = "3    3\n3    3" }
        };

        var converter = new MatrixExpressionConverter(matrixList);
        
        // 示例1: A + B3 ^ 2 - (2 × inverse(C11))
        Console.WriteLine("示例1: A + B3 ^ 2 - (2 × inverse(C11))");
        var expression1 = "A + B3 ^ 2 - (2 × inverse(C11))";
        try
        {
            var (expressions1, operations1) = converter.ConvertToExecutable(expression1);
            
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
            var (expressions2, operations2) = converter.ConvertToExecutable(expression2);
            
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