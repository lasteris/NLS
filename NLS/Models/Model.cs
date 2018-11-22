

namespace NLS.Models
{
    using System;
    using MathNet.Numerics.LinearAlgebra;
    using MathNet.Numerics.LinearAlgebra.Double;
    using org.mariuszgromada.math.mxparser;
    using System.Collections.Generic;
    using System.IO;

    public class Model
    {
        private string formula;
        private int vNumber;
        private int pNumber;
        private double lambdaInitial;
        private double lambdaFactor;
        private double MinimumStepSize;
        private double StepSizeInitial;
        private double StepSizeFactor;

        private Expression exp;
        private List<Argument> vList;
        private List<Argument> pList;

        public string Formula { get => formula; set => formula = value; }
        public double LambdaInitial { get => lambdaInitial; set => lambdaInitial = value; }
        public double LambdaFactor { get => lambdaFactor; set => lambdaFactor = value; }

        public void __init(string formula, int pNum, int vNum, SolverOptions solverOptions)
        {
            Formula = formula;
            exp = new Expression(Formula);
            vNumber = vNum;
            pNumber = pNum;
            LambdaInitial = solverOptions.lambdaInitial; //for LM
            LambdaFactor = solverOptions.lambdaFactor; //for LM
            MinimumStepSize = solverOptions.MinimumStepSize;
            StepSizeInitial = solverOptions.StepSizeInitial;
            StepSizeFactor = solverOptions.StepSizeFactor;
            //create variables
            vList = new List<Argument>();
            for (int i = 0; i < vNumber; ++i)
            {
                vList.Add(new Argument($"x{i}"));
            }
            exp.addArguments(vList.ToArray());

            //create parameters
            pList = new List<Argument>();
            for (int i = 0; i < pNumber; ++i)
            {
                pList.Add(new Argument($"a{i}"));
            }
            exp.addArguments(pList.ToArray());


        }
        public void SetArguments(Vector<double> X, Vector<double> parametersCurrent)
        {
            //set current vars
            for (int i = 0; i < vNumber; ++i)
            {
                exp.setArgumentValue($"x{i}", X[i]);
            }
            //set current params
            for (int i = 0; i < pNumber; ++i)
            {
                exp.setArgumentValue($"a{i}", parametersCurrent[i]);
            }
        }

        public void GetValue(Vector<double> X, Vector<double> parameters, out double y)
        {
            exp.setExpressionString(Formula);
            SetArguments(X, parameters);
            y = exp.calculate();
        }

        public void GetGradient(Vector<double> X, Vector<double> parameters, ref Vector<double> gradient)
        {
            SetArguments(X, parameters);
            for (int i = 0; i < pNumber; ++i)
            {
                exp.setExpressionString($"der({Formula},{pList[i].getArgumentName()})");
                gradient[i] = exp.calculate();
            }
        }
        public void GetResidualVector(int pointCount, List<Vector<double>> dataX, Vector<double> dataY,
            Vector<double> parameters, ref Vector<double> residuals)
        {
            for (int i = 0; i < pointCount; ++i)
            {
                GetValue(dataX[i], parameters, out double y);
                residuals[i] = y - dataY[i];
            }
        }

        public void GetJacobian(int pointCount, List<Vector<double>> dataX, Vector<double> parameters, ref Matrix<double> jacobian)
        {
            int parametersCount = parameters.Count;

            for (int i = 0; i < pointCount; ++i)
            {
                Vector<double> gradient = new DenseVector(parametersCount);
                GetGradient(dataX[i], parameters, ref gradient);
                jacobian.SetRow(i, gradient);
            }
        }

        public void GetObjectiveValue(int pointCount, List<Vector<double>> dataX, Vector<double> dataY,
            Vector<double> parameters, out double value)
        {
            value = 0.0;
            double y = 0.0;

            for (int i = 0; i < pointCount; ++i)
            {
                GetValue(dataX[i], parameters, out y);
                value += Math.Pow(y - dataY[i], 2.0);
            }
            value *= 0.5;
        }

        bool BreakCondition(double valueCurrent, double valueNew, int iterationCount, Vector<double> parametersCurrent,
            Vector<double> parametersNew, SolverOptions solverOptions)
        {
            return (Math.Abs(valueNew - valueCurrent) <= solverOptions.minimumDeltaValue ||
                parametersNew.Subtract(parametersCurrent).Norm(2.0) <= solverOptions.minimumDeltaParameters ||
                iterationCount >= solverOptions.maximumIterations);
        }


        public void GaussNewton(SolverOptions solverOptions,
            Vector<double> dataY, List<Vector<double>> dataX, ref List<Vector<double>> iterations)
        {
            if (File.Exists("results.txt"))
                File.Delete("results.txt");

            string stream = string.Empty;
            int pointCount = solverOptions.pointCount;
            int parametersCount = solverOptions.initialParameters.Count;
            Vector<double> parametersCurrent = new DenseVector(solverOptions.initialParameters.AsArray());
            Vector<double> parametersNew = new DenseVector(parametersCount);

            double valueNew;
            GetObjectiveValue(pointCount, dataX, dataY, parametersCurrent, out double valueCurrent);
            #region запись в файл
            stream = "Начальное приближение:\r\nA(";
            foreach (var param in parametersCurrent)
            {
                stream += $"{param} ";
            }
            stream = stream + ")\r\n";
            stream += $"Количество параметров: {parametersCount}\r\n";
            stream += $"Количество наблюдений(уравнений): {pointCount}\r\n";
            stream += $"Текущее значение минимизации: {valueCurrent}\r\n";
            stream += "\r\n________________________________________________________\r\n";
            using (StreamWriter sw = File.AppendText("results.txt"))
            {
                sw.Write(stream);
            }
            #endregion

            while (true)
            {
                Matrix<double> jacobian = new DenseMatrix(pointCount, parametersCount);
                Vector<double> residual = new DenseVector(pointCount);

                GetJacobian(pointCount, dataX, parametersCurrent, ref jacobian);
                GetResidualVector(pointCount, dataX, dataY, parametersCurrent, ref residual);

                #region запись в файл
                stream = "";
                stream += $"Итерация {iterations.Count}.\r\n";
                stream += "J(";
                for (int i = 0; i < jacobian.RowCount; ++i)
                {
                    for (int j = 0; j < jacobian.ColumnCount; ++j)
                    {
                        stream += $"{jacobian.At(i, j)} ";
                    }
                    stream += "\r\n";
                }
                stream += ")\r\n";

                stream += "R(";
                foreach (var item in residual)
                {
                    stream += $"{item}";
                }
                stream += ")\r\n";
                #endregion
                Vector<double> step;
                if(solverOptions.useCholecky)
                     step = jacobian.Transpose().Multiply(jacobian).Cholesky().Solve(jacobian.Transpose().Multiply(residual));
                else
                     step = jacobian.Transpose().Multiply(jacobian).Inverse().Multiply(jacobian.Transpose().Multiply(residual));


                parametersCurrent.Subtract(step, parametersNew);

                GetObjectiveValue(pointCount, dataX, dataY, parametersNew, out valueNew);

                Vector<double> iterator = new DenseVector(parametersCount);
                parametersNew.CopyTo(iterator);
                iterations.Add(iterator);

                if (BreakCondition(valueCurrent, valueNew, iterations.Count, parametersCurrent, parametersNew, solverOptions))
                {
                    #region запись в файл
                    stream += $"\r\nРасчет окончен. Итерация {iterations.Count}\r\n";
                    stream += $"Значение функции: {valueCurrent}\r\n";
                    stream += $"Значение нормы: {step.Norm(2.0)}\r\n";

                    using (StreamWriter sw = File.AppendText("results.txt"))
                    {
                        sw.Write(stream);
                    }
                    #endregion
                    break;
                }

                parametersNew.CopyTo(parametersCurrent);
                valueCurrent = valueNew;

                #region запись в файл
                stream += $"Текущее значение минимизации: {valueCurrent}\r\nТекущий вектор шага: S(";
                foreach (var item in step)
                {
                    stream += $" {item}";
                }
                stream += ")\r\n";
                #endregion
            }
            #region запись
            stream = "______________________Приближения параметров по итерациям______________________\r\n";
            foreach (var item in iterations)
            {
                for (int i = 0; i < item.Count; ++i)
                {
                    stream = stream + item[i] + "; ";
                }
                stream += "\r\n________________________________________________________\r\n";
            }



            using (StreamWriter sw = File.AppendText("results.txt"))
            {
                sw.Write(stream);
            }
            #endregion
        }
        public void LevenbergMarquardt(SolverOptions solverOptions,
            Vector<double> dataY, List<Vector<double>> dataX, ref List<Vector<double>> iterations)
        {
            if (File.Exists("results.txt"))
                File.Delete("results.txt");

            string stream = string.Empty;
            int pointCount = solverOptions.pointCount;
            int parametersCount = solverOptions.initialParameters.Count;
            double lambda = LambdaInitial;

            double[] parameters = solverOptions.initialParameters.AsArray();
            Vector<double> parametersCurrent = new DenseVector(parameters);
            Vector<double> parametersNew = new DenseVector(parametersCount);


            double valueCurrent;
            double valueNew;

            GetObjectiveValue(pointCount, dataX, dataY, parametersCurrent, out valueCurrent);
            #region запись в файл
            stream = "Начальное приближение:\r\nA(";
            foreach (var param in parametersCurrent)
            {
                stream += $"{param} ";
            }
            stream = stream + ")\r\n";
            stream += $"Количество параметров: {parametersCount}\r\n";
            stream += $"Количество наблюдений(уравнений): {pointCount}\r\n";
            stream += $"Текущее значение минимизации: {valueCurrent}\r\n";
            stream += "\r\n________________________________________________________\r\n";
            using (StreamWriter sw = File.AppendText("results.txt"))
            {
                sw.Write(stream);
            }
            #endregion

            while (true)
            {
                Matrix<double> jacobian = new DenseMatrix(pointCount, parametersCount);
                Vector<double> residual = new DenseVector(pointCount);
                #region запись в файл
                stream = "";
                stream += $"Итерация {iterations.Count}.\r\n";
                stream += "J(";
                for (int i = 0; i < jacobian.RowCount; ++i)
                {
                    for (int j = 0; j < jacobian.ColumnCount; ++j)
                    {
                        stream += $"{jacobian.At(i, j)} ";
                    }
                    stream += "\r\n";
                }
                stream += ")\r\n";

                stream += "R(";
                foreach (var item in residual)
                {
                    stream += $"{item}";
                }
                stream += ")\r\n";
                #endregion
                GetJacobian(pointCount, dataX, parametersCurrent, ref jacobian);
                GetResidualVector(pointCount, dataX, dataY, parametersCurrent, ref residual);

                // compute approximate Hessian
                Matrix<double> hessian = jacobian.Transpose().Multiply(jacobian);

                // create diagonal matrix for proper scaling
                Matrix<double> diagonal = new DiagonalMatrix(parametersCount, parametersCount, hessian.Diagonal().ToArray());

                // Вычисляем шаг Левенберга-Марквардта
                Vector<double> step = (hessian.Add(diagonal.Multiply(lambda))).Cholesky().Solve(jacobian.Transpose().Multiply(residual));

                // обновить параметры модели
                parametersCurrent.Subtract(step, parametersNew);

                GetObjectiveValue(pointCount, dataX, dataY, parametersNew, out valueNew);

                Vector<double> iterator = new DenseVector(parametersCount);
                parametersNew.CopyTo(iterator);
                iterations.Add(iterator);

                if (BreakCondition(valueCurrent, valueNew, iterations.Count, parametersCurrent, parametersNew, solverOptions))
                {
                    #region запись в файл
                    stream += $"\r\nРасчет окончен. Итерация {iterations.Count}\r\n";
                    stream += $"Значение функции: {valueCurrent}\r\n";
                    stream += $"Значение нормы: {step.Norm(2.0)}\r\n";

                    using (StreamWriter sw = File.AppendText("results.txt"))
                    {
                        sw.Write(stream);
                    }
                    #endregion
                    break;
                }
                if (valueNew < valueCurrent)
                {
                    lambda = (lambda / LambdaFactor);
                    parametersNew.CopyTo(parametersCurrent);
                    valueCurrent = valueNew;
                }
                else
                {
                    lambda = (lambda * lambdaFactor);
                }
                #region запись в файл
                stream += $"Текущее значение минимизации: {valueCurrent}\r\nТекущий вектор шага: S(";
                foreach (var item in step)
                {
                    stream += $" {item}";
                }
                stream += ")\r\n";
                stream += $"значение множителя: {lambda}\r\n";
                #endregion
            }
            #region запись
            stream = "______________________Приближения параметров по итерациям______________________\r\n";
            foreach (var item in iterations)
            {
                for (int i = 0; i < item.Count; ++i)
                {
                    stream = stream + item[i] + "; ";
                }
                stream += "\r\n________________________________________________________\r\n";
            }



            using (StreamWriter sw = File.AppendText("results.txt"))
            {
                sw.Write(stream);
            }
            #endregion
        }
        public void DFP(SolverOptions solverOptions, int pointCount,
           Vector<double> dataY, List<Vector<double>> dataX, ref List<Vector<double>> iterations)
        {

        }
        public void BFGS(SolverOptions solverOptions, int pointCount,
           Vector<double> dataY, List<Vector<double>> dataX, ref List<Vector<double>> iterations)
        {

        }

        public void SteepestDescent(SolverOptions solverOptions,
            List<Vector<double>> dataX, Vector<double> dataY, ref List<Vector<double>> iterations)
        {
            if (File.Exists("results.txt"))
                File.Delete("results.txt");

            int parametersCount = pNumber;
            int pointCount = solverOptions.pointCount;
            string stream = string.Empty;
            double[] parameters = solverOptions.initialParameters.AsArray();
            Vector<double> parametersCurrent = new DenseVector(parameters);
            Vector<double> parametersNew = new DenseVector(pNumber);

            double valueCurrent;
            double valueNew;

            GetObjectiveValue(pointCount, dataX, dataY, parametersCurrent, out valueCurrent);

            double stepSize = StepSizeInitial;
            #region запись в файл
            stream = "Начальное приближение:\r\nA(";
            foreach (var param in parametersCurrent)
            {
                stream += $"{param} ";
            }
            stream = stream + ")\r\n";
            stream += $"Количество параметров: {parametersCount}\r\n";
            stream += $"Количество наблюдений(уравнений): {pointCount}\r\n";
            stream += $"Текущее значение минимизации: {valueCurrent}\r\n";
            stream += $"Начальное значение шага: {stepSize}\r\n";
            stream += "\r\n________________________________________________________\r\n";
            using (StreamWriter sw = File.AppendText("results.txt"))
            {
                sw.Write(stream);
            }
            #endregion
            while (true)
            {
                Matrix<double> jacobian = new DenseMatrix(pointCount, parametersCount);
                Vector<double> residual = new DenseVector(pointCount);

                GetJacobian(pointCount, dataX, parametersCurrent, ref jacobian);
                GetResidualVector(pointCount, dataX, dataY, parametersCurrent, ref residual);
                #region запись в файл
                stream = "";
                stream += $"Итерация {iterations.Count}.\r\n";
                stream += "J(";
                for (int i = 0; i < jacobian.RowCount; ++i)
                {
                    for (int j = 0; j < jacobian.ColumnCount; ++j)
                    {
                        stream += $"{jacobian.At(i, j)} ";
                    }
                    stream += "\r\n";
                }
                stream += ")\r\n";

                stream += "R(";
                foreach (var item in residual)
                {
                    stream += $"{item}";
                }
                stream += ")\r\n";
                #endregion
                // compute steepest descent orientation
                Vector<double> step = jacobian.Transpose().Multiply(residual).Normalize(2.0);

                do
                {
                    // update estimated model parameters using steepest descent step elongated by stepSize
                    parametersNew = parametersCurrent.Subtract(step.Multiply(stepSize));
                    GetObjectiveValue(pointCount, dataX, dataY, parametersNew, out valueNew);

                    if (valueNew >= valueCurrent)
                    {
                        // the step was too long to decrease function value - shorten the step
                        stepSize /= StepSizeFactor;
                    }
                }
                while (
                    // iterate until function value is decreased or the step is too show
                    valueNew >= valueCurrent &&
                    stepSize >= MinimumStepSize);

                Vector<double> iterator = new DenseVector(parametersCount);
                parametersNew.CopyTo(iterator);
                iterations.Add(iterator);

                if (stepSize <= MinimumStepSize ||
                    BreakCondition(
                        valueCurrent,
                        valueNew,
                        iterations.Count,
                        parametersCurrent,
                        parametersNew,
                        solverOptions))
                {
                    #region запись в файл
                    stream += $"\r\nРасчет окончен. Итерация {iterations.Count}\r\n";
                    stream += $"Значение функции: {valueCurrent}\r\n";
                    stream += $"Значение нормы: {step.Norm(2.0)}\r\n";

                    using (StreamWriter sw = File.AppendText("results.txt"))
                    {
                        sw.Write(stream);
                    }
                    #endregion
                    break;
                }

                if (valueNew < valueCurrent)
                {
                    // the was short enough to decreate function value - elongate the step
                    stepSize *= StepSizeFactor;
                }

                parametersNew.CopyTo(parametersCurrent);
                valueCurrent = valueNew;
                #region запись в файл
                stream += $"Текущее значение минимизации: {valueCurrent}\r\nТекущий вектор шага: S(";
                foreach (var item in step)
                {
                    stream += $" {item}";
                }
                stream += ")\r\n";
                stream += $"значение множителя: {stepSize}\r\n";
                #endregion
            }
            #region запись
            stream = "______________________Приближения параметров по итерациям______________________\r\n";
            foreach (var item in iterations)
            {
                for (int i = 0; i < item.Count; ++i)
                {
                    stream = stream + item[i] + "; ";
                }
                stream += "\r\n________________________________________________________\r\n";
            }



            using (StreamWriter sw = File.AppendText("results.txt"))
            {
                sw.Write(stream);
            }
            #endregion
        }
    }
}

