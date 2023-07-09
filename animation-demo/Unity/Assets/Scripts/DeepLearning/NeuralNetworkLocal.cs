using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DeepLearning {

	public abstract class NeuralNetworkLocal : MonoBehaviour {

		public string Folder = "";
        public string Destination = "";
        public Parameters Parameters = null;

        public Matrix X, Y;
        private int Pivot = -1;

        private List<Matrix> Matrices = new List<Matrix>();

        protected abstract void StoreParametersDerived();

        protected abstract void LoadDerived();

        protected abstract void UnloadDerived();
        
        public abstract void Predict(int style_1, int style_2, float style_interp);
        public abstract void Predict3Way(int style_1, int style_2, int style_3, Vector3 style_interp_vector);
        //  public abstract void Predict(float pca_1, float pca_2);

        void OnEnable() {
            Load();
        }

        void OnDisable() {
            Unload();
        }

        public void StoreParameters() {
            Parameters = ScriptableObject.CreateInstance<Parameters>();
            StoreParametersDerived();
			if(!Parameters.Validate()) {
				Parameters = null;
			} else {
                #if UNITY_EDITOR
				AssetDatabase.CreateAsset(Parameters, Destination + "/Parameters.asset");
                #endif
			}
        }
        public void Load() {
			if(Parameters == null) {
				Debug.Log("Building network failed because no parameters were saved.");
			} else {
                LoadDerived();
            }
        }
        public void Unload() {
			if(Parameters == null) {

			} else {
                UnloadDerived();
            }
        }

        public Matrix CreateMatrix(int rows, int cols, string id) {
            if(Matrices.Exists(x => x.ID == id)) {
                Debug.Log("Matrix with ID " + id + " already contained.");
                return null;
            }
            Matrix M = new Matrix(rows, cols, id);
            Matrices.Add(M);
            return M;
        }

        public Matrix CreateMatrix(int rows, int cols, Parameters.Buffer buffer) {
            if(Matrices.Exists(x => x.ID == buffer.ID)) {
                Debug.Log("Matrix with ID " + buffer.ID + " already contained.");
                return null;
            }
            Matrix M = new Matrix(rows, cols, buffer.ID);
            for(int row=0; row<rows; row++) {
                for(int col=0; col<cols; col++) {
                    M.SetValue(row, col, buffer.Values[row*cols + col]);
                }
            }
            Matrices.Add(M);
            return M;
        }

        public void DeleteMatrix(Matrix M) {
            int index = Matrices.IndexOf(M);
            if(index == -1) {
                Debug.Log("Matrix not found.");
                return;
            }
            Matrices.RemoveAt(index);
            M.Delete();
        }

        public Matrix GetMatrix(string id) {
            int index = Matrices.FindIndex(x => x.ID == id);
            if(index == -1) {
                return null;
            }
            return Matrices[index];
        }

        public string GetID(Matrix M) {
            int index = Matrices.IndexOf(M);
            if(index == -1) {
                return null;
            }
            return Matrices[index].ID;
        }

        public void SetPivot(int index) {
            Pivot = index;
        }

        public void ResetPivot() {
            Pivot = -1;
        }

		public void SetInput(int index, float value) {
			X.SetValue(index, 0, value);
		}

        public float GetInput(int index) {
            return X.GetValue(index, 0);
        }

        public void SetOutput(int index, float value) {
            Y.SetValue(index, 0, value);
        }

		public float GetOutput(int index) {
			return Y.GetValue(index, 0);
		}

		public void Feed(float value) {
            Pivot += 1;
			SetInput(Pivot, value);
		}

        public void Feed(float[] values) {
            for(int i=0; i<values.Length; i++) {
                Feed(values[i]);
            }
        }

        public void Feed(Vector2 vector) {
            Feed(vector.x);
            Feed(vector.y);
        }

        public void Feed(Vector3 vector) {
            Feed(vector.x);
            Feed(vector.y);
            Feed(vector.z);
        }

		public float Read() {
            Pivot += 1;
			return GetOutput(Pivot);
		}

        public float[] Read(int count) {
            float[] values = new float[count];
            for(int i=0; i<count; i++) {
                values[i] = Read();
            }
            return values;
        }

        public Matrix Normalise(Matrix IN, Matrix mean, Matrix std, Matrix OUT) {
            if(IN.GetRows() != mean.GetRows() || IN.GetRows() != std.GetRows() || IN.GetCols() != mean.GetCols() || IN.GetCols() != std.GetCols()) {
                Debug.Log("Incompatible dimensions for normalisation.");
                return IN;
            } else {
                Eigen.Normalise(IN.Ptr, mean.Ptr, std.Ptr, OUT.Ptr);
                return OUT;
            }
        }
        
        public Matrix Renormalise(Matrix IN, Matrix mean, Matrix std, Matrix OUT) {
            if(IN.GetRows() != mean.GetRows() || IN.GetRows() != std.GetRows() || IN.GetCols() != mean.GetCols() || IN.GetCols() != std.GetCols()) {
                Debug.Log("Incompatible dimensions for renormalisation.");
                return IN;
            } else {
                Eigen.Renormalise(IN.Ptr, mean.Ptr, std.Ptr, OUT.Ptr);
                return OUT;
            }
        }

        public Matrix EvenCols(Matrix IN, Matrix OUT) {
            if(IN.GetCols() % 2 != 0) {
                Debug.Log("Matrix does not have an even number of columns - cannot extract EvenCols");
                return IN;
            } else {
                Eigen.EvenCols(IN.Ptr, OUT.Ptr);
                return OUT;
            }            
        }

        public Matrix OddCols(Matrix IN, Matrix OUT) {
            if(IN.GetCols() % 2 != 0) {
                Debug.Log("Matrix does not have an even number of columns - cannot extract OddCols");
                return IN;
            } else {
                Eigen.OddCols(IN.Ptr, OUT.Ptr);
                return OUT;
            } 
        }

        public Matrix EvenRows(Matrix IN, Matrix OUT) {
            if(IN.GetRows() % 2 != 0) {
                Debug.Log("Matrix does not have an even number of rows - cannot extract EvenRows");
                return IN;
            } else {
                Eigen.EvenRows(IN.Ptr, OUT.Ptr);
                return OUT;
            }            
        }

        public Matrix OddRows(Matrix IN, Matrix OUT) {
            if(IN.GetRows() % 2 != 0) {
                Debug.Log("Matrix does not have an even number of rows - cannot extract OddRows");
                return IN;
            } else {
                Eigen.OddRows(IN.Ptr, OUT.Ptr);
                return OUT;
            } 
        }

        public Matrix Layer(Matrix IN, Matrix W, Matrix b, Matrix OUT) {
            if(IN.GetRows() != W.GetCols() || W.GetRows() != b.GetRows() || IN.GetCols() != b.GetCols()) {
                Debug.Log("Incompatible dimensions for layer feed-forward.");
                return IN;
            } else {
                Eigen.Layer(IN.Ptr, W.Ptr, b.Ptr, OUT.Ptr);
                return OUT;
            }
        }

        public Matrix Blend(Matrix M, Matrix W, float w) {
            if(M.GetRows() != W.GetRows() || M.GetCols() != W.GetCols()) {
                Debug.Log("Incompatible dimensions for blending.");
                return M;
            } else {
                Eigen.Blend(M.Ptr, W.Ptr, w);
                return M;
            }
        }

        public Matrix PointwiseProduct(Matrix lhs, Matrix rhs, Matrix OUT) {
            if(lhs.GetRows() != rhs.GetRows() || lhs.GetCols() != rhs.GetCols()) {
                Debug.Log("Incompatible Matrix dimensions.");
            } else {
                Eigen.PointwiseProduct(lhs.Ptr, rhs.Ptr, OUT.Ptr);
            }
            return OUT;
        }

        public Matrix Add(Matrix lhs, Matrix rhs, Matrix OUT) {
            if(lhs.GetRows() != rhs.GetRows() || lhs.GetCols() != rhs.GetCols()) {
                Debug.Log("Incompatible Matrix dimensions.");
            } else {
                Eigen.Add(lhs.Ptr, rhs.Ptr, OUT.Ptr);
            }
            return OUT;
        }

        public Matrix Scale(Matrix lhs, float value, Matrix OUT) {
            Eigen.Scale(lhs.Ptr, value, OUT.Ptr);
            return OUT;
        }

        public float ColMean(Matrix IN, int col) {
            return  IN.ColMean(col);
        }

        public float ColStd(Matrix IN, int col) {
            return  IN.ColStd(col);
        }

        public Matrix SetVector(Matrix IN, float value, int dim) {
            for(int i=0; i<dim; i++){
                IN.SetValue(i, 0, value);
            }			
            return IN;
		}

        public float GetVectorValue(Matrix IN, int index) {
            return IN.GetValue(index, 0);
        }

	}

	#if UNITY_EDITOR
	[CustomEditor(typeof(NeuralNetworkLocal), true)]
	public class NeuralNetworkLocal_Editor : Editor {

		public NeuralNetworkLocal Target;

		void Awake() {
			Target = (NeuralNetworkLocal)target;
		}

		public override void OnInspectorGUI() {
			Undo.RecordObject(Target, Target.name);
	
            DrawDefaultInspector();
            if(Utility.GUIButton("Store Parameters", UltiDraw.DarkGrey, UltiDraw.White)) {
                Target.StoreParameters();
            }

			if(GUI.changed) {
				EditorUtility.SetDirty(Target);
			}
		}
        
	}
	#endif

}
