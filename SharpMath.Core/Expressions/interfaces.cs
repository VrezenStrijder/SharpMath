using System;
using System.Collections.Generic;
using System.Text;

namespace SharpMath.Core
{

    /// <summary>
    /// 数学表达式接口
    /// </summary>
    public interface IMathExpression
    {
        /// <summary>
        /// 将表达式转换为其LaTeX表示形式
        /// </summary>
        string ToLatex();

        /// <summary>
        /// 将表达式转换为可读的字符串
        /// </summary>
        string ToString();

        /// <summary>
        /// 计算表达式的数值(无参数)
        /// </summary>
        double Evaluate();

        /// <summary>
        /// 计算表达式的数值
        /// </summary>
        /// <param name="args">包含变量名和其对应数值的字典</param>
        double Evaluate(Dictionary<string, double> args);

        /// <summary>
        /// 接受一个访问者, 实现Visitor设计模式
        /// </summary>
        /// <param name="visitor">要接受的访问者</param>
        /// <typeparam name="T">访问者返回的类型</typeparam>
        /// <returns>访问者处理此节点后的结果</returns>
        T Accept<T>(IExpressionVisitor<T> visitor);

        /// <summary>
        /// 获取当前表达式节点的运算符优先级
        /// </summary>
        int GetPrecedence();
    }



    /// <summary>
    /// 为表达式树节点访问者接口
    /// </summary>
    public interface IExpressionVisitor<T>
    {
        T Visit(NumberExpression number);

        T Visit(VariableExpression variable);
        T Visit(BinaryOperationExpression binary);
        T Visit(UnaryOperationExpression unary);
        T Visit(FunctionExpression function);

        T Visit(EquationExpression equation);

        T Visit(EquationSystemExpression system);

        T Visit(MatrixExpression matrix);

    }

}
