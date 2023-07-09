using System;
using System.Runtime.InteropServices;

//Eigen Plugin
public static class Eigen {
    //Default
    [DllImport("Eigen")]
    public static extern IntPtr Create(int rows, int cols);
    [DllImport("Eigen")]
    public static extern IntPtr Delete(IntPtr ptr);

    //Setters and Getters
    [DllImport("Eigen")]
    public static extern int GetRows(IntPtr ptr);
    [DllImport("Eigen")]
    public static extern int GetCols(IntPtr ptr);
    [DllImport("Eigen")]
    public static extern void SetZero(IntPtr ptr);
    [DllImport("Eigen")]
    public static extern void SetSize(IntPtr ptr, int rows, int cols);
    [DllImport("Eigen")]
    public static extern void SetValue(IntPtr ptr, int row, int col, float value);
    [DllImport("Eigen")]
    public static extern float GetValue(IntPtr ptr, int row, int col);
    [DllImport("Eigen")]
    public static extern void EvenCols(IntPtr IN, IntPtr OUT);
    [DllImport("Eigen")]
    public static extern void OddCols(IntPtr IN, IntPtr OUT);
    [DllImport("Eigen")]
    public static extern void EvenRows(IntPtr IN, IntPtr OUT);
    [DllImport("Eigen")]
    public static extern void OddRows(IntPtr IN, IntPtr OUT);

    //Arithmetics
    [DllImport("Eigen")]
    public static extern void Add(IntPtr lhs, IntPtr rhs, IntPtr OUT);
    [DllImport("Eigen")]
    public static extern void Subtract(IntPtr lhs, IntPtr rhs, IntPtr OUT);
    [DllImport("Eigen")]
    public static extern void Product(IntPtr lhs, IntPtr rhs, IntPtr OUT);
    [DllImport("Eigen")]
    public static extern void Scale(IntPtr lhs, float value, IntPtr OUT);
    [DllImport("Eigen")]
    public static extern void PointwiseProduct(IntPtr lhs, IntPtr rhs, IntPtr OUT);
    [DllImport("Eigen")]
    public static extern void PointwiseQuotient(IntPtr lhs, IntPtr rhs, IntPtr OUT);
    [DllImport("Eigen")]
    public static extern void PointwiseAbsolute(IntPtr IN, IntPtr OUT);
    [DllImport("Eigen")]
    public static extern float RowSum(IntPtr ptr, int row);
    [DllImport("Eigen")]
    public static extern float ColSum(IntPtr ptr, int col);
    [DllImport("Eigen")]
    public static extern float RowMean(IntPtr ptr, int row);
    [DllImport("Eigen")]
    public static extern float ColMean(IntPtr ptr, int col);
    [DllImport("Eigen")]
    public static extern float RowStd(IntPtr ptr, int row);
    [DllImport("Eigen")]
    public static extern float ColStd(IntPtr ptr, int col);

    // Eigenvalues and vectors for PCA
    [DllImport("Eigen")]
    public static extern void SelfAdjointEigenSolver3x3(IntPtr IN, IntPtr OUTVALUES, IntPtr OUTVECTORS);

    //Deep Learning Functions
    [DllImport("Eigen")]
    public static extern void Normalise(IntPtr IN, IntPtr mean, IntPtr std, IntPtr OUT);
    [DllImport("Eigen")]
    public static extern void Renormalise(IntPtr IN, IntPtr mean, IntPtr std, IntPtr OUT);
    //[DllImport("Eigen")]
    //public static extern void ILayer(IntPtr X, IntPtr W, IntPtr b);
    [DllImport("Eigen")]
    public static extern void Layer(IntPtr IN, IntPtr W, IntPtr b, IntPtr OUT);
    [DllImport("Eigen")]
    public static extern void Blend(IntPtr ptr, IntPtr W, float w);
    [DllImport("Eigen")]
    public static extern void BlendAll(IntPtr ptr, IntPtr[] W, float[] w, int length);
    [DllImport("Eigen")]
    public static extern void ELU(IntPtr ptr);
    [DllImport("Eigen")]
    public static extern void Sigmoid(IntPtr ptr);
    [DllImport("Eigen")]
    public static extern void TanH(IntPtr ptr);
    [DllImport("Eigen")]
    public static extern void SoftMax(IntPtr ptr);
    [DllImport("Eigen")]
    public static extern void LogSoftMax(IntPtr ptr);
    [DllImport("Eigen")]
    public static extern void SoftSign(IntPtr ptr);
    [DllImport("Eigen")]
    public static extern void Exp(IntPtr ptr);

    //CNN Experimental
    [DllImport("CNN")]
    public static extern void CreateCNN();
    [DllImport("CNN")]
    public static extern bool EraseCNN();
    [DllImport("CNN")]
    public static extern double PredictCNN();
    [DllImport("CNN")]
    public static extern bool SetWeightCNN(int layer, int index, double scalar);
    [DllImport("CNN")]
    public static extern bool SetBiasCNN(int layer, int index, double scalar);
    [DllImport("CNN")]
    public static extern bool SetInputCNN(int index, double scalar);
    [DllImport("CNN")]
    public static extern double GetOutputCNN(int index);

    //CAE Experimental
    [DllImport("CAE")]
    public static extern void CreateCAE();
    [DllImport("CAE")]
    public static extern bool EraseCAE();
    [DllImport("CAE")]
    public static extern double PredictCAE();
    [DllImport("CAE")]
    public static extern bool SetWeightCAE(int layer, int index, double scalar);
    [DllImport("CAE")]
    public static extern bool SetBiasCAE(int layer, int index, double scalar);
    [DllImport("CAE")]
    public static extern bool SetInputCAE(int index, double scalar);
    [DllImport("CAE")]
    public static extern double GetOutputCAE(int index);
}
