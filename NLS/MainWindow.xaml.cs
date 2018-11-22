using System;
using System.Linq;
using System.Windows;
using System.Data;
using NLS.Models;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
using System.Collections.Generic;
using System.IO;

namespace NLS
{
  
    public partial class MainWindow : Window
    {
        char separator = System.Globalization.CultureInfo.CurrentCulture.NumberFormat.CurrencyDecimalSeparator[0];
        DataTable paramTable;
        DataTable observedTable;
        public List<Vector<double>> dataX;
        public Vector<double> dataY;
        char pLetter = 'a';
        int pNumber = 0;
        char vLetter = 'x';
        int vNumber = 0;
        string function = "";
        SolverOptions options;
        Model model;
        List<Vector<double>> iterations;
        public MainWindow()
        {
            InitializeComponent();
            CreateTableParamsBttn.Click += CreateTableParamsBttn_Click;
            Optimus.Click += Optimus_Click;
            SaveParamsBttn.Click += SaveParamsBttn_Click;
            observedFromFileBttn.Click += ObservedFromFileBttn_Click;
            tabControl.SelectionChanged += TabControl_SelectionChanged;
            autoset.Click += Autoset_Click;
            autosetother.Click += Autosetother_Click;
            delobserved.Click += Delobserved_Click;
            options = new SolverOptions();
            model = new Model();
            InputInitDeltaParam.Text = "0.001";
            InputInitDeltaValue.Text = "0.001";
            InputMaxIterations.Text = "100";
            isCholecky.IsChecked = true;
            autoset.IsChecked = true;
            autosetother.IsChecked = true;
            rbng.IsChecked = true;

            InputMaxIterations.IsEnabled = false;
            InputInitDeltaValue.IsEnabled = false;
            InputInitDeltaParam.IsEnabled = false;
            InputPointCount.IsEnabled = false;
            isCholecky.IsEnabled = false;

            InputLambda.IsEnabled = false;
            InputLambdaFactor.IsEnabled = false;
            InputMinStepSize.IsEnabled = false;
            InputInitStepSize.IsEnabled = false;
            InputStepSizeFactor.IsEnabled = false;
        }

        private void Autosetother_Click(object sender, RoutedEventArgs e)
        {
            if (autosetother.IsChecked == true)
            {
                InputLambda.IsEnabled = false;
                InputLambdaFactor.IsEnabled = false;
                InputMinStepSize.IsEnabled = false;
                InputInitStepSize.IsEnabled = false;
                InputStepSizeFactor.IsEnabled = false;
            }
            else
            {
                InputLambda.IsEnabled = true;
                InputLambdaFactor.IsEnabled = true;
                InputMinStepSize.IsEnabled = true;
                InputInitStepSize.IsEnabled = true;
                InputStepSizeFactor.IsEnabled = true;
            }
        }

        private void Delobserved_Click(object sender, RoutedEventArgs e)
        {
            int rowIndex = gridObserved.SelectedIndex;
            observedTable.Rows.RemoveAt(rowIndex);
            gridObserved.Items.Refresh();
        }

        private void Autoset_Click(object sender, RoutedEventArgs e)
        {
            if(autoset.IsChecked == true)
            {
                InputMaxIterations.IsEnabled = false;
                InputInitDeltaValue.IsEnabled = false;
                InputInitDeltaParam.IsEnabled = false;
                InputPointCount.IsEnabled = false;
                isCholecky.IsEnabled = false;
            }
            else
            {
                InputMaxIterations.IsEnabled = true;
                InputInitDeltaValue.IsEnabled = true;
                InputInitDeltaParam.IsEnabled = true;
                InputPointCount.IsEnabled = true;
                isCholecky.IsEnabled = true;
            }
        }

        private void TabControl_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            
            if(e.RemovedItems.Contains(tabItemConfigs) && e.AddedItems.Contains(tabItemData))
            {
                UpdateSolverType();
                UpdateAccuracyOptions();
                UpdateOtherOptions();
            }

        }

        private void ObservedFromFileBttn_Click(object sender, RoutedEventArgs e)
        {
            ReadDataFromFile();
        }

        private void SaveParamsBttn_Click(object sender, RoutedEventArgs e)
        {
            SaveParameters();
        }
        
        private void Optimus_Click(object sender, RoutedEventArgs e)
        {
            SaveParameters();
            SaveDataFromGrid();
            UpdateSolverType();
            
            options.pointCount = observedTable.Rows.Count;
            iterations = new List<Vector<double>>();
            function = InputFuncBox.Text;
            model.__init(function, pNumber, vNumber, options);
            switch (options.typeSolver)
            {
                case SolverType.Cauchy:
                {
                    model.SteepestDescent(options, dataX, dataY, ref iterations);
                    break;
                }
                case SolverType.NewtonGauss:
                {
                    model.GaussNewton(options, dataY, dataX, ref iterations);
                    break;
                }
                case SolverType.LevenbergMarquardt:
                {
                    model.LevenbergMarquardt(options, dataY, dataX, ref iterations);
                    break;
                }
            }
            System.Diagnostics.Process.Start("notepad.exe", "results.txt");
        }

        private void CreateTableParamsBttn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string func = InputFuncBox.Text;

                GetParametersNumber(func, pLetter, ref pNumber);
                BuildParamsPlaceHolder(pNumber, pLetter);

                GetVariablesNumber(func, vLetter, ref vNumber);
                BuildObservationsPlaceHolder(vNumber, vLetter);
            }
            catch(FormatException ex)
            {
                MessageBox.Show(ex.Message);
            }

        }
        public void GetVariablesNumber(string func, char vLetter, ref int vNumber)
        {
            //пока сообщение
            if (!func.Contains(vLetter))
                throw new FormatException("Функция не содержит переменных");

            vNumber = 0;
            while (true)
            {
                if (func.Contains($"{vLetter}{vNumber}"))
                {
                    ++vNumber;
                }
                else
                    break;
            }
        }
        public void BuildObservationsPlaceHolder(int vNum, char vLetter)
        {
            /*DataTable*/ observedTable = new DataTable("vars");
            DataColumn column;
            DataRow row;

            column = new DataColumn
            {
                DataType = Type.GetType("System.Double"),
                ColumnName = $"y",
                ReadOnly = false,
                Unique = false
            };
            observedTable.Columns.Add(column);

            for (int i = 0; i < vNum; ++i)
            {
                column = new DataColumn
                {
                    DataType = Type.GetType("System.Double"),
                    ColumnName = $"{vLetter}{i}",
                    ReadOnly = false,
                    Unique = false
                };
                observedTable.Columns.Add(column);
            }

            for (int i = 0; i < vNum + 1; ++i)
            {
                row = observedTable.NewRow();
                row["y"] = 0;

                for (int j = 0; j < vNum; ++j)
                {
                    row[$"{vLetter}{j}"] = 0;
                }
                observedTable.Rows.Add(row);
            }

            gridObserved.DataContext = observedTable.DefaultView;
            gridObserved.CanUserAddRows = true;
            observedNum.Text = $"({gridObserved.Items.Count})";
        
        }
        public void GetParametersNumber(string func, char pLetter, ref int pNumber)
        {
            if (!func.Contains(pLetter))
                throw new FormatException("Функция не содержит параметров");
            pNumber = 0;
            while (true)
            {
                if (func.Contains($"{pLetter}{pNumber}"))
                {
                    ++pNumber;
                }
                else
                    break;
            }
        }
        public void BuildParamsPlaceHolder(int pNum, char pLetter)
        {
            /*DataTable*/ paramTable = new DataTable("params");
            DataColumn column;
            DataRow row;

            for(int i = 0; i < pNum; ++i)
            {
                column = new DataColumn();
                column.DataType = Type.GetType("System.Int32");
                column.ColumnName = $"{pLetter}{i}";
                column.ReadOnly = false;
                column.Unique = false;
                paramTable.Columns.Add(column);
            }


            row = paramTable.NewRow();
            
            for(int i = 0; i < pNum; ++i)
            {
                row[$"{pLetter}{i}"] = 1;               
            }
            paramTable.Rows.Add(row);

            gridParams.DataContext = paramTable.DefaultView;
            gridParams.CanUserAddRows = false;
        }

        public void SaveParameters()
        {
            Vector<double> parameters = new DenseVector(pNumber);
            DataRow row = paramTable.Rows[0];

            for (int i = 0; i < pNumber; ++i)
            {
                parameters[i] = Convert.ToDouble(row[$"{pLetter}{i}"], System.Globalization.CultureInfo.InvariantCulture);
            }
            options.initialParameters = parameters;
        }
        public void ReadDataFromFile()
        {
            string filename = "";
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog
            {
                DefaultExt = ".txt",

                Filter = "Текстовые документы (.txt)|*.txt"
            };

            bool? result = dlg.ShowDialog();
            if (result == true)
            {
                filename = dlg.FileName;
            }
            else
            {
                return;
            }
            observedTable = new DataTable("observed");
            DataColumn column;
            DataRow row;

            column = new DataColumn
            {
                DataType = Type.GetType("System.Double"),
                ColumnName = $"y",
                ReadOnly = false,
                Unique = false
            };
            observedTable.Columns.Add(column);

            for (int i = 0; i < vNumber; ++i)
            {
                column = new DataColumn
                {
                    DataType = Type.GetType("System.Double"),
                    ColumnName = $"{vLetter}{i}",
                    ReadOnly = false,
                    Unique = false
                };
                observedTable.Columns.Add(column);
            }

            string[] Lines = File.ReadAllText(filename).Split('\n');
            for (int i = 0; i < Lines.Length - 1; ++i)
            {
                string[] values = Lines[i].Split(' ');

                if (values.Length != (vNumber + 1))
                {
                    MessageBox.Show("В данных в указанном файле какая-то недостача.");
                    return;
                }
                    

                row = observedTable.NewRow();
                row["y"] = Convert.ToDouble(values[0], System.Globalization.CultureInfo.InvariantCulture);

                for (int j = 1; j < vNumber + 1; ++j)
                {
                    row[$"{vLetter}{j-1}"] = values[j];
                }
                observedTable.Rows.Add(row);
            }
            gridObserved.DataContext = observedTable.DefaultView;
            gridObserved.CanUserAddRows = true;
            observedNum.Text = $"({gridObserved.Items.Count})";
        }

        public void SaveDataFromGrid()
        {
            Vector<double> data;
            DataRow row;
            dataY = new DenseVector(gridObserved.Items.Count);
            dataX = new List<Vector<double>>();
            for(int i = 0; i < observedTable.Rows.Count; ++i)
            {
                row = observedTable.Rows[i];
                dataY[i] = Convert.ToDouble(row["y"]);
                data = new DenseVector(vNumber);
                for(int j = 0; j < vNumber; ++j)
                {
                    data[j] = Convert.ToDouble(row[$"{vLetter}{j}"]);
                }
                dataX.Add(data);
            }
        }
       public void UpdateSolverType()
        {

            if (rbns.IsChecked == true)
                options.typeSolver = SolverType.Cauchy;
            else if (rbng.IsChecked == true)
                options.typeSolver = SolverType.NewtonGauss;
            else if (rblm.IsChecked == true)
                options.typeSolver = SolverType.LevenbergMarquardt;
           /* else if (rbdfp.IsChecked == true)
                options.typeSolver = SolverType.DFP;
            else if (rbbfgs.IsChecked == true)
                options.typeSolver = SolverType.BFGS;*/
        }

       public void UpdateAccuracyOptions()
        {
            options.maximumIterations = Convert.ToInt32(InputMaxIterations.Text);
            options.minimumDeltaValue = Convert.ToDouble(InputInitDeltaValue.Text.Replace('.', ','));
            options.minimumDeltaParameters = Convert.ToDouble(InputInitDeltaParam.Text.Replace('.', ','));

            options.useCholecky = isCholecky.IsChecked == true ? true : false;

        }

       public void UpdateOtherOptions()
        {
            options.lambdaInitial = Convert.ToDouble(InputLambda.Text, System.Globalization.CultureInfo.InvariantCulture);
            options.lambdaFactor = Convert.ToDouble(InputLambdaFactor.Text, System.Globalization.CultureInfo.InvariantCulture);
            options.StepSizeInitial = Convert.ToDouble(InputInitStepSize.Text, System.Globalization.CultureInfo.InvariantCulture);
            options.MinimumStepSize = Convert.ToDouble(InputMinStepSize.Text, System.Globalization.CultureInfo.InvariantCulture);
            options.StepSizeFactor = Convert.ToDouble(InputStepSizeFactor.Text, System.Globalization.CultureInfo.InvariantCulture);

        }
    }
}
