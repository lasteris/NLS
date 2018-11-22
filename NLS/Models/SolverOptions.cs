

namespace NLS.Models
{
    using MathNet.Numerics.LinearAlgebra.Double;
    using MathNet.Numerics.LinearAlgebra;
    using System.Collections.Generic;

   
    public enum SolverType
    {
        Cauchy, NewtonGauss, LevenbergMarquardt, DFP, BFGS
    }
    public class SolverOptions
    {
        public int pointCount;
        public double minimumDeltaValue;
        public double minimumDeltaParameters;
        public int maximumIterations;
        public Vector<double> initialParameters;
        public SolverType typeSolver;
        public bool useCholecky;
        public double lambdaInitial;
        public double lambdaFactor;
        public double MinimumStepSize;
        public double StepSizeInitial;
        public double StepSizeFactor;

        public SolverOptions()
        {
        }

        
    }
}
