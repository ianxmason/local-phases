# if defined _WIN32 || defined __CYGWIN__
#   define EXPORT_API __declspec(dllexport)
# else
#   define EXPORT_API  __attribute__ ((visibility("default")))
# endif

//#include "stdafx.h" //Use when compiling from Visual Studio
#include "Eigen/Dense"

using namespace Eigen;
extern "C" {
	EXPORT_API MatrixXf* Create(int rows, int cols) {
		return new MatrixXf(MatrixXf::Zero(rows, cols));
	}

	EXPORT_API void Delete(MatrixXf* ptr) {
		delete(ptr);
	}

	EXPORT_API int GetRows(MatrixXf* ptr) {
		return (*ptr).rows();
	}

	EXPORT_API int GetCols(MatrixXf* ptr) {
		return (*ptr).cols();
	}

	EXPORT_API void SetZero(MatrixXf* ptr) {
		*ptr = (*ptr).Zero((*ptr).rows(), (*ptr).cols());
	}

	EXPORT_API void SetSize(MatrixXf* ptr, int rows, int cols) {
		(*ptr).conservativeResize(rows, cols);
	}


	EXPORT_API void EvenCols(MatrixXf* IN, MatrixXf* OUT) {
		// Requires column major order and  an even number of columns
		// Based on https://stackoverflow.com/questions/20368446/extract-every-other-row-or-column-of-an-eigen-matrix-as-a-new-matrix
		*OUT = MatrixXf::Map((*IN).data(), GetRows(IN) * 2, GetCols(IN)/2).topRows(GetRows(IN));
	}

	EXPORT_API void OddCols(MatrixXf* IN, MatrixXf* OUT) {
		// Requires column major order and  an even number of columns
		// Based on https://stackoverflow.com/questions/20368446/extract-every-other-row-or-column-of-an-eigen-matrix-as-a-new-matrix
		*OUT = MatrixXf::Map((*IN).data(), GetRows(IN) * 2, GetCols(IN)/2).bottomRows(GetRows(IN));
	}

	EXPORT_API void EvenRows(MatrixXf* IN, MatrixXf* OUT) {
		// Requires column major order and  an even number of rows
		// Based on https://stackoverflow.com/questions/20368446/extract-every-other-row-or-column-of-an-eigen-matrix-as-a-new-matrix
		*OUT = MatrixXf::Map((*IN).data(), GetRows(IN)/2, GetCols(IN), Stride<Dynamic,2>(GetRows(IN),2));
	}

	EXPORT_API void OddRows(MatrixXf* IN, MatrixXf* OUT) {
		// THIS IS UNTESTED - MAY NOT BE CORRECT
		// Requires column major order and  an even number of rows
		// Based on https://stackoverflow.com/questions/20368446/extract-every-other-row-or-column-of-an-eigen-matrix-as-a-new-matrix
		*OUT = MatrixXf::Map((*IN).data()+1, GetRows(IN)/2, GetCols(IN), Stride<Dynamic,2>(GetRows(IN),2));
	}

	EXPORT_API void Add(MatrixXf* lhs, MatrixXf* rhs, MatrixXf* out) {
		*out = *lhs + *rhs;
	}

	EXPORT_API void Subtract(MatrixXf* lhs, MatrixXf* rhs, MatrixXf* out) {
		*out = *lhs - *rhs;
	}

	EXPORT_API void Product(MatrixXf* lhs, MatrixXf* rhs, MatrixXf* out) {
		*out = *lhs * *rhs;
	}

	EXPORT_API void Scale(MatrixXf* lhs, float value, MatrixXf* out) {
		*out = *lhs * value;
	}

	EXPORT_API void SetValue(MatrixXf* ptr, int row, int col, float value) {
		(*ptr)(row, col) = value;
	}

	EXPORT_API float GetValue(MatrixXf* ptr, int row, int col) {
		return (*ptr)(row, col);
	}

	EXPORT_API void PointwiseProduct(MatrixXf* lhs, MatrixXf* rhs, MatrixXf* out) {
		*out = (*lhs).cwiseProduct(*rhs);
	}

	EXPORT_API void PointwiseQuotient(MatrixXf* lhs, MatrixXf* rhs, MatrixXf* out) {
		*out = (*lhs).cwiseQuotient(*rhs);
	}

	EXPORT_API void PointwiseAbsolute(MatrixXf* in, MatrixXf* out) {
		*out = (*in).cwiseAbs();
	}

	EXPORT_API float RowSum(MatrixXf* ptr, int row) {
		return (*ptr).row(row).sum();
	}

	EXPORT_API float ColSum(MatrixXf* ptr, int col) {
		return (*ptr).col(col).sum();
	}

	EXPORT_API float RowMean(MatrixXf* ptr, int row) {
		return (*ptr).row(row).mean();
	}

	EXPORT_API float ColMean(MatrixXf* ptr, int col) {
		return (*ptr).col(col).mean();
	}

	EXPORT_API float RowStd(MatrixXf* ptr, int row) {
		MatrixXf diff = (*ptr).row(row) - (*ptr).row(row).mean() * MatrixXf::Ones(1, (*ptr).rows());
		diff = diff.cwiseProduct(diff);
		return std::sqrt(diff.sum() / (*ptr).cols());
	}

	EXPORT_API float ColStd(MatrixXf* ptr, int col) {
		MatrixXf diff = (*ptr).col(col) - (*ptr).col(col).mean() * MatrixXf::Ones((*ptr).rows(), 1);
		diff = diff.cwiseProduct(diff);
		return std::sqrt(diff.sum() / (*ptr).rows());
	}

	EXPORT_API void Normalise(MatrixXf* ptr, MatrixXf* mean, MatrixXf* std, MatrixXf* out) {
		*out = (*ptr - *mean).cwiseQuotient(*std);
	}

	EXPORT_API void Renormalise(MatrixXf* ptr, MatrixXf* mean, MatrixXf* std, MatrixXf* out) {
		*out = (*ptr).cwiseProduct(*std) + *mean;
	}

	EXPORT_API void SelfAdjointEigenSolver3x3(MatrixXf* ptr, MatrixXf* outValues, MatrixXf* outVectors) {
		SelfAdjointEigenSolver<MatrixXf> eigensolver(*ptr);
		*outVectors = eigensolver.eigenvectors();
		*outValues = eigensolver.eigenvalues();
	}

	// }

	//EXPORT_API void ILayer (MatrixXf* X, MatrixXf* W, MatrixXf* b) {
	//	(*X).noalias() = *W * *X + *b;
	//}

	EXPORT_API void Layer(MatrixXf* in, MatrixXf* W, MatrixXf* b, MatrixXf* out) {
		*out = *W * *in + *b;
	}

	EXPORT_API void Blend(MatrixXf* in, MatrixXf* W, float w) {
		(*in).noalias() += w * *W;
	}

	EXPORT_API void RELU(MatrixXf* ptr) {
		*ptr = (*ptr).cwiseMax(0.0f);
	}

	EXPORT_API void ELU(MatrixXf* ptr) {
		*ptr = ((*ptr).array().cwiseMax(0.0f) + (*ptr).array().cwiseMin(0.0f).exp() - 1.0f).matrix();
		//int rows = (*ptr).rows();
		//for (int i = 0; i<rows; i++) {
		//	(*ptr)(i, 0) = (std::max)((*ptr)(i, 0), 0.0f) + std::exp((std::min)((*ptr)(i, 0), 0.0f)) - 1.0f;
		//}
	}

	EXPORT_API void Sigmoid(MatrixXf* ptr) {
		int rows = (*ptr).rows();
		for (int i = 0; i<rows; i++) {
			(*ptr)(i, 0) = 1.0f / (1.0f + std::exp(-(*ptr)(i, 0)));
		}
	}

	EXPORT_API void TanH(MatrixXf* ptr) {
		int rows = (*ptr).rows();
		for (int i = 0; i<rows; i++) {
			(*ptr)(i, 0) = std::tanh((*ptr)(i, 0));
		}
	}

	EXPORT_API void SoftMax(MatrixXf* ptr) {
		float frac = 0.0f;
		int rows = (*ptr).rows();
		for (int i = 0; i<rows; i++) {
			(*ptr)(i, 0) = std::exp((*ptr)(i, 0));
			frac += (*ptr)(i, 0);
		}
		for (int i = 0; i<rows; i++) {
			(*ptr)(i, 0) /= frac;
		}
	}

	EXPORT_API void LogSoftMax(MatrixXf* ptr) {
		float frac = 0.0f;
		int rows = (*ptr).rows();
		for (int i = 0; i<rows; i++) {
			(*ptr)(i, 0) = std::exp((*ptr)(i, 0));
			frac += (*ptr)(i, 0);
		}
		for (int i = 0; i<rows; i++) {
			(*ptr)(i, 0) = std::log((*ptr)(i, 0) / frac);
		}
	}

	EXPORT_API void SoftSign(MatrixXf* ptr) {
		int rows = (*ptr).rows();
		for (int i = 0; i<rows; i++) {
			(*ptr)(i, 0) /= 1 + std::abs((*ptr)(i, 0));
		}
	}

	EXPORT_API void Exp(MatrixXf* ptr) {
		int rows = (*ptr).rows();
		for (int i = 0; i<rows; i++) {
			(*ptr)(i, 0) = std::exp((*ptr)(i, 0));
		}
	}
}