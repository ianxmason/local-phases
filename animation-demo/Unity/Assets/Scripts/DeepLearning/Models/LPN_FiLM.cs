using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DeepLearning {

	public class LPN_FiLM : NeuralNetworkLocal {

		public int EP_XDim = 348;
		public int EP_HDim = 512;
        public int EP_YDim = 342;

        public int GT_XDim = 8;
        public int GT_HDim = 32;
        public int num_experts = 8;  // == GT_YDim
		

		// public int PhaseIndex = 324;
        public int num_styles = 95;
		// public bool isPhaseFunctionedFilm = false;

		private Matrix Xmean, Xstd, Ymean, Ystd, GT_Xmean, GT_Xstd;
		private Matrix mu, sig, style_1_alpha_1, style_1_beta_1, style_1_alpha_2, style_1_beta_2, style_2_alpha_1, style_2_beta_1, style_2_alpha_2, style_2_beta_2, style_3_alpha_1, style_3_beta_1, style_3_alpha_2, style_3_beta_2;
		private Matrix GT_W0, GT_W1, GT_W2, GT_b0, GT_b1, GT_b2;
		private Matrix GT_X, GT_Y, W0, W1, W2, b0, b1, b2;
        private Matrix[]  EP_W0, EP_W1, EP_W2, EP_b0, EP_b1, EP_b2;
        private Matrix[]  Alphas_1, Betas_1, Alphas_2, Betas_2;

		private float[] LocalPhases = new float[8];
		private int[] LocalPhaseIndexes =  new int[] { 324, 325, 326, 327, 328, 329, 330, 331 };  // hardcoded for now, if need functionality to be flexible we can make it so
		private int[] LocalPhaseUpdateIndexes = new int[] { 332, 333, 334, 335, 336, 337, 338, 339 }; // hardcoded for now, if need functionality to be flexible we can make it so
		private float Damping;

		private const float M_PI = 3.14159265358979323846f;

		private float[] FiLMParameters = new float[512*4];
		public LPN_FiLM() {
			
		}

		protected override void StoreParametersDerived() {
			Parameters.Store(Folder+"/Xmean.bin", EP_XDim*1, "Xmean");
			Parameters.Store(Folder+"/Xstd.bin", EP_XDim*1, "Xstd");
			Parameters.Store(Folder+"/Ymean.bin", EP_YDim*1, "Ymean");
			Parameters.Store(Folder+"/Ystd.bin", EP_YDim*1, "Ystd");

			// For normalising the local phases input to gating
			Parameters.Store(Folder+"/GT_Xmean.bin", GT_XDim*1, "GT_Xmean");
			Parameters.Store(Folder+"/GT_Xstd.bin", GT_XDim*1, "GT_Xstd");
			// Parameters.Store(Folder+"/GT_Ymean.bin", GT_XDim*2, "GT_Ymean");  //*2 for local phase and local phase update
			// Parameters.Store(Folder+"/GT_Ystd.bin", GT_XDim*2, "GT_Ystd");

            Parameters.Store(Folder+"/wc0_w.bin", GT_XDim*GT_HDim, "GT_W0");
			Parameters.Store(Folder+"/wc0_b.bin", GT_HDim*1, "GT_b0");
            Parameters.Store(Folder+"/wc1_w.bin", GT_HDim*GT_HDim, "GT_W1");
			Parameters.Store(Folder+"/wc1_b.bin", GT_HDim*1, "GT_b1");
            Parameters.Store(Folder+"/wc2_w.bin", GT_HDim*num_experts, "GT_W2");
			Parameters.Store(Folder+"/wc2_b.bin", num_experts*1, "GT_b2");

			for(int i=0; i<num_experts; i++) {
				Parameters.Store(Folder+"/cp0_a"+i.ToString("D1")+".bin", EP_HDim*EP_XDim, "EP_W0_"+i.ToString("D1"));
				Parameters.Store(Folder+"/cp1_a"+i.ToString("D1")+".bin", EP_HDim*EP_HDim, "EP_W1_"+i.ToString("D1"));
				Parameters.Store(Folder+"/cp2_a"+i.ToString("D1")+".bin", EP_YDim*EP_HDim, "EP_W2_"+i.ToString("D1"));
				Parameters.Store(Folder+"/cp0_b"+i.ToString("D1")+".bin", EP_HDim*1, "EP_b0_"+i.ToString("D1"));
				Parameters.Store(Folder+"/cp1_b"+i.ToString("D1")+".bin", EP_HDim*1, "EP_b1_"+i.ToString("D1"));
				Parameters.Store(Folder+"/cp2_b"+i.ToString("D1")+".bin", EP_YDim*1, "EP_b2_"+i.ToString("D1"));
			}

            for (int i=0; i<num_styles; i++){
                Parameters.Store(Folder+"/Style_"+i.ToString("D3")+"_scale1.bin", EP_HDim*1, "Style_"+i.ToString("D3")+"_scale1");
                Parameters.Store(Folder+"/Style_"+i.ToString("D3")+"_shift1.bin", EP_HDim*1, "Style_"+i.ToString("D3")+"_shift1");
                Parameters.Store(Folder+"/Style_"+i.ToString("D3")+"_scale2.bin", EP_HDim*1, "Style_"+i.ToString("D3")+"_scale2");
                Parameters.Store(Folder+"/Style_"+i.ToString("D3")+"_shift2.bin", EP_HDim*1, "Style_"+i.ToString("D3")+"_shift2");

			}			           
		}

		protected override void LoadDerived() {
			Xmean = CreateMatrix(EP_XDim, 1, Parameters.Load("Xmean"));
			Xstd = CreateMatrix(EP_XDim, 1, Parameters.Load("Xstd"));
			Ymean = CreateMatrix(EP_YDim, 1, Parameters.Load("Ymean"));
			Ystd = CreateMatrix(EP_YDim, 1, Parameters.Load("Ystd"));
			GT_Xmean = CreateMatrix(GT_XDim, 1, Parameters.Load("GT_Xmean"));
			GT_Xstd = CreateMatrix(GT_XDim, 1, Parameters.Load("GT_Xstd"));
            GT_W0 = CreateMatrix(GT_HDim, GT_XDim, Parameters.Load("GT_W0"));
			GT_W1 = CreateMatrix(GT_HDim, GT_HDim, Parameters.Load("GT_W1"));
			GT_W2 = CreateMatrix(num_experts, GT_HDim, Parameters.Load("GT_W2"));
			GT_b0 = CreateMatrix(GT_HDim, 1, Parameters.Load("GT_b0"));
			GT_b1 = CreateMatrix(GT_HDim, 1, Parameters.Load("GT_b1"));
			GT_b2 = CreateMatrix(num_experts, 1, Parameters.Load("GT_b2"));
			EP_W0 = new Matrix[num_experts];
			EP_W1 = new Matrix[num_experts];
			EP_W2 = new Matrix[num_experts];
			EP_b0 = new Matrix[num_experts];
			EP_b1 = new Matrix[num_experts];
			EP_b2 = new Matrix[num_experts];
            Alphas_1 = new Matrix[num_styles];
            Betas_1 = new Matrix[num_styles];
            Alphas_2 = new Matrix[num_styles];
            Betas_2 = new Matrix[num_styles];			
			for(int i=0; i<num_experts; i++) {
				EP_W0[i] = CreateMatrix(EP_HDim, EP_XDim, Parameters.Load("EP_W0_"+i.ToString("D1")));
				EP_W1[i] = CreateMatrix(EP_HDim, EP_HDim, Parameters.Load("EP_W1_"+i.ToString("D1")));
				EP_W2[i] = CreateMatrix(EP_YDim, EP_HDim, Parameters.Load("EP_W2_"+i.ToString("D1")));
				EP_b0[i] = CreateMatrix(EP_HDim, 1, Parameters.Load("EP_b0_"+i.ToString("D1")));
				EP_b1[i] = CreateMatrix(EP_HDim, 1, Parameters.Load("EP_b1_"+i.ToString("D1")));
				EP_b2[i] = CreateMatrix(EP_YDim, 1, Parameters.Load("EP_b2_"+i.ToString("D1")));
			}
            for (int i=0; i<num_styles; i++){
                Alphas_1[i] = CreateMatrix(EP_HDim, 1, Parameters.Load("Style_"+i.ToString("D3")+"_scale1"));
                Betas_1[i] = CreateMatrix(EP_HDim, 1, Parameters.Load("Style_"+i.ToString("D3")+"_shift1"));
                Alphas_2[i] = CreateMatrix(EP_HDim, 1, Parameters.Load("Style_"+i.ToString("D3")+"_scale2"));
				Betas_2[i] = CreateMatrix(EP_HDim, 1, Parameters.Load("Style_"+i.ToString("D3")+"_shift2"));
			}		

			X = CreateMatrix(EP_XDim, 1, "X");
			Y = CreateMatrix(EP_YDim, 1, "Y");
            GT_X = CreateMatrix(GT_XDim, 1, "GT_X");
            GT_Y = CreateMatrix(num_experts, 1, "GT_Y");
            W0 = CreateMatrix(EP_HDim, EP_XDim, "W0");
			W1 = CreateMatrix(EP_HDim, EP_HDim, "W1");
			W2 = CreateMatrix(EP_YDim, EP_HDim, "W2");
			b0 = CreateMatrix(EP_HDim, 1, "b0");
			b1 = CreateMatrix(EP_HDim, 1, "b1");
			b2 = CreateMatrix(EP_YDim, 1, "b2");

            mu = CreateMatrix(EP_HDim, 1, "mu");
            sig = CreateMatrix(EP_HDim, 1, "sig");
			style_1_alpha_1 = CreateMatrix(EP_HDim, 1, "style_1_alpha_1");
            style_1_beta_1 = CreateMatrix(EP_HDim, 1, "style_1_beta_1");
			style_1_alpha_2 = CreateMatrix(EP_HDim, 1, "style_1_alpha_2");
            style_1_beta_2 = CreateMatrix(EP_HDim, 1, "style_1_beta_2");
			style_2_alpha_1 = CreateMatrix(EP_HDim, 1, "style_2_alpha_1");
            style_2_beta_1 = CreateMatrix(EP_HDim, 1, "style_2_beta_1");
			style_2_alpha_2 = CreateMatrix(EP_HDim, 1, "style_2_alpha_2");
            style_2_beta_2 = CreateMatrix(EP_HDim, 1, "style_2_beta_2");
			style_3_alpha_1 = CreateMatrix(EP_HDim, 1, "style_3_alpha_1");
            style_3_beta_1 = CreateMatrix(EP_HDim, 1, "style_3_beta_1");
			style_3_alpha_2 = CreateMatrix(EP_HDim, 1, "style_3_alpha_2");
            style_3_beta_2 = CreateMatrix(EP_HDim, 1, "style_3_beta_2");
			// Phase = 0f;
			Damping = 0f;
		}
		
		protected override void UnloadDerived() {
			
		}

		// This function for 2-way interpolation
		public override void Predict(int style_1, int style_2, float style_interp) {
			//Normalise Input
			Normalise(X, Xmean, Xstd, Y);

			// Feed in phases and normalise
			for(int i=0; i<LocalPhases.Length; i++) {				
				GT_X.SetValue(i, 0, LocalPhases[i]);
			}
			Normalise(GT_X, GT_Xmean, GT_Xstd, GT_Y);

			//Process PFNN
			// int index = (int)((Phase / (2f*M_PI)) * 50f);
			// Layer(Y, W0[index], b0[index], Y).ELU();
			// Layer(Y, W1[index], b1[index], Y).ELU();
			// Layer(Y, W2[index], b2[index], Y);

           
			// Load FiLM params into holders and interpolate them
            for(int i=1; i<EP_HDim; i++){
                style_1_alpha_1.SetValue(i, 0, Alphas_1[style_1].GetValue(i, 0));
                style_1_beta_1.SetValue(i, 0, Betas_1[style_1].GetValue(i, 0));
                style_1_alpha_2.SetValue(i, 0, Alphas_2[style_1].GetValue(i, 0));
                style_1_beta_2.SetValue(i, 0, Betas_2[style_1].GetValue(i, 0));
                style_2_alpha_1.SetValue(i, 0, Alphas_1[style_2].GetValue(i, 0));
                style_2_beta_1.SetValue(i, 0, Betas_1[style_2].GetValue(i, 0));
                style_2_alpha_2.SetValue(i, 0, Alphas_2[style_2].GetValue(i, 0));
                style_2_beta_2.SetValue(i, 0, Betas_2[style_2].GetValue(i, 0));
			}
			Scale(style_1_alpha_1, (1f-style_interp), style_1_alpha_1);
			Scale(style_1_beta_1, (1f-style_interp), style_1_beta_1);
			Scale(style_1_alpha_2, (1f-style_interp), style_1_alpha_2);
			Scale(style_1_beta_2, (1f-style_interp), style_1_beta_2);

			Scale(style_2_alpha_1, (style_interp), style_2_alpha_1);
			Scale(style_2_beta_1, (style_interp), style_2_beta_1);
			Scale(style_2_alpha_2, (style_interp), style_2_alpha_2);
			Scale(style_2_beta_2, (style_interp), style_2_beta_2);

			Add(style_1_alpha_1, style_2_alpha_1, style_1_alpha_1);
			Add(style_1_beta_1, style_2_beta_1, style_1_beta_1);
			Add(style_1_alpha_2, style_2_alpha_2, style_1_alpha_2);
			Add(style_1_beta_2, style_2_beta_2, style_1_beta_2);

            // Get gating weights
            Layer(GT_Y, GT_W0, GT_b0, GT_Y).ELU();
            Layer(GT_Y, GT_W1, GT_b1, GT_Y).ELU();
            Layer(GT_Y, GT_W2, GT_b2, GT_Y).SoftMax();

            //Generate LPN Network Weights as blend of experts
			W0.SetZero(); b0.SetZero();
			W1.SetZero(); b1.SetZero();
			W2.SetZero(); b2.SetZero();
			for(int i=0; i<num_experts; i++) {
				float weight = GT_Y.GetValue(i, 0);
				Blend(W0, EP_W0[i], weight);
				Blend(b0, EP_b0[i], weight);
				Blend(W1, EP_W1[i], weight);
				Blend(b1, EP_b1[i], weight);
				Blend(W2, EP_W2[i], weight);
				Blend(b2, EP_b2[i], weight);
			}

			 // Add instance norm and FiLM modulation to forward pass
            Layer(Y, W0, b0, Y);
            float mu_val = ColMean(Y, 0);
            float sig_val = ColStd(Y, 0);
			SetVector(mu, mu_val, EP_HDim);
			SetVector(sig, sig_val, EP_HDim);
            Normalise(Y, mu, sig, Y);  // may need to add very small constant to sig
            PointwiseProduct(Y, style_1_alpha_1, Y);
            Add(Y, style_1_beta_1, Y).ELU();

			Layer(Y, W1, b1, Y);
        	mu_val = ColMean(Y, 0);
            sig_val = ColStd(Y, 0);
			SetVector(mu, mu_val, EP_HDim);
			SetVector(sig, sig_val, EP_HDim);
            Normalise(Y, mu, sig, Y);
            PointwiseProduct(Y, style_1_alpha_2, Y);
            Add(Y, style_1_beta_2, Y).ELU();

			Layer(Y, W2, b2, Y);

			//Renormalise Output
			Renormalise(Y, Ymean, Ystd, Y);

			//Update Local Phases
			for(int i=0; i<LocalPhases.Length; i++) {
				// No need to consider damping as it is always set to 0 
				// TO CONSIDER/TRY: should we set max and min as -1, 1 since we use sine and cosine?
				// If not required then it seems unnecessary but may add stability

				// No interpolation
				// LocalPhases[i] = LocalPhases[i] + Y.GetValue(LocalPhaseUpdateIndexes[i], 0);

				// Interpolation
				LocalPhases[i] = 0.5f * Y.GetValue(LocalPhaseIndexes[i], 0) + 0.5f *(LocalPhases[i] + Y.GetValue(LocalPhaseUpdateIndexes[i], 0));				
			}
				
			// Phase = Mathf.Repeat(Phase + (1f-Damping)*GetOutput(PhaseIndex)*2f*Mathf.PI, 2f*Mathf.PI);
		}

		// This function for 3-way interpolation
		public override void Predict3Way(int style_1, int style_2, int style_3, Vector3 style_interp_vector) {
			//Normalise Input
			Normalise(X, Xmean, Xstd, Y);

			// Feed in phases and normalise
			for(int i=0; i<LocalPhases.Length; i++) {				
				GT_X.SetValue(i, 0, LocalPhases[i]);
			}
			Normalise(GT_X, GT_Xmean, GT_Xstd, GT_Y);
           
			// Load FiLM params into holders and interpolate them
            for(int i=1; i<EP_HDim; i++){
                style_1_alpha_1.SetValue(i, 0, Alphas_1[style_1].GetValue(i, 0));
                style_1_beta_1.SetValue(i, 0, Betas_1[style_1].GetValue(i, 0));
                style_1_alpha_2.SetValue(i, 0, Alphas_2[style_1].GetValue(i, 0));
                style_1_beta_2.SetValue(i, 0, Betas_2[style_1].GetValue(i, 0));
                style_2_alpha_1.SetValue(i, 0, Alphas_1[style_2].GetValue(i, 0));
                style_2_beta_1.SetValue(i, 0, Betas_1[style_2].GetValue(i, 0));
                style_2_alpha_2.SetValue(i, 0, Alphas_2[style_2].GetValue(i, 0));
                style_2_beta_2.SetValue(i, 0, Betas_2[style_2].GetValue(i, 0));
                style_3_alpha_1.SetValue(i, 0, Alphas_1[style_3].GetValue(i, 0));
                style_3_beta_1.SetValue(i, 0, Betas_1[style_3].GetValue(i, 0));
                style_3_alpha_2.SetValue(i, 0, Alphas_2[style_3].GetValue(i, 0));
                style_3_beta_2.SetValue(i, 0, Betas_2[style_3].GetValue(i, 0));
            }
			Scale(style_1_alpha_1, style_interp_vector.x, style_1_alpha_1);
			Scale(style_1_beta_1, style_interp_vector.x, style_1_beta_1);
			Scale(style_1_alpha_2, style_interp_vector.x, style_1_alpha_2);
			Scale(style_1_beta_2, style_interp_vector.x, style_1_beta_2);

			Scale(style_2_alpha_1, style_interp_vector.y, style_2_alpha_1);
			Scale(style_2_beta_1, style_interp_vector.y, style_2_beta_1);
			Scale(style_2_alpha_2, style_interp_vector.y, style_2_alpha_2);
			Scale(style_2_beta_2, style_interp_vector.y, style_2_beta_2);

			Scale(style_3_alpha_1, style_interp_vector.z, style_3_alpha_1);
			Scale(style_3_beta_1, style_interp_vector.z, style_3_beta_1);
			Scale(style_3_alpha_2, style_interp_vector.z, style_3_alpha_2);
			Scale(style_3_beta_2, style_interp_vector.z, style_3_beta_2);

			Add(style_1_alpha_1, style_2_alpha_1, style_1_alpha_1);
			Add(style_1_beta_1, style_2_beta_1, style_1_beta_1);
			Add(style_1_alpha_2, style_2_alpha_2, style_1_alpha_2);
			Add(style_1_beta_2, style_2_beta_2, style_1_beta_2);

			Add(style_1_alpha_1, style_3_alpha_1, style_1_alpha_1);
			Add(style_1_beta_1, style_3_beta_1, style_1_beta_1);
			Add(style_1_alpha_2, style_3_alpha_2, style_1_alpha_2);
			Add(style_1_beta_2, style_3_beta_2, style_1_beta_2);

			 // Get gating weights
            Layer(GT_Y, GT_W0, GT_b0, GT_Y).ELU();
            Layer(GT_Y, GT_W1, GT_b1, GT_Y).ELU();
            Layer(GT_Y, GT_W2, GT_b2, GT_Y).SoftMax();

            //Generate LPN Network Weights as blend of experts
			W0.SetZero(); b0.SetZero();
			W1.SetZero(); b1.SetZero();
			W2.SetZero(); b2.SetZero();
			for(int i=0; i<num_experts; i++) {
				float weight = GT_Y.GetValue(i, 0);
				Blend(W0, EP_W0[i], weight);
				Blend(b0, EP_b0[i], weight);
				Blend(W1, EP_W1[i], weight);
				Blend(b1, EP_b1[i], weight);
				Blend(W2, EP_W2[i], weight);
				Blend(b2, EP_b2[i], weight);
			}

			 // Add instance norm and FiLM modulation to forward pass
            Layer(Y, W0, b0, Y);
            float mu_val = ColMean(Y, 0);
            float sig_val = ColStd(Y, 0);
			SetVector(mu, mu_val, EP_HDim);
			SetVector(sig, sig_val, EP_HDim);
            Normalise(Y, mu, sig, Y);  // may need to add very small constant to sig
            PointwiseProduct(Y, style_1_alpha_1, Y);
            Add(Y, style_1_beta_1, Y).ELU();

			Layer(Y, W1, b1, Y);
        	mu_val = ColMean(Y, 0);
            sig_val = ColStd(Y, 0);
			SetVector(mu, mu_val, EP_HDim);
			SetVector(sig, sig_val, EP_HDim);
            Normalise(Y, mu, sig, Y);
            PointwiseProduct(Y, style_1_alpha_2, Y);
            Add(Y, style_1_beta_2, Y).ELU();

			Layer(Y, W2, b2, Y);

			//Renormalise Output
			Renormalise(Y, Ymean, Ystd, Y);

			//Update Local Phases
			for(int i=0; i<LocalPhases.Length; i++) {
				// No need to consider damping as it is always set to 0 
				// TO CONSIDER/TRY: should we set max and min as -1, 1 since we use sine and cosine?
				// If not required then it seems unnecessary but may add stability

				// No interpolation
				// LocalPhases[i] = LocalPhases[i] + Y.GetValue(LocalPhaseUpdateIndexes[i], 0);

				// Interpolation
				LocalPhases[i] = 0.5f * Y.GetValue(LocalPhaseIndexes[i], 0) + 0.5f *(LocalPhases[i] + Y.GetValue(LocalPhaseUpdateIndexes[i], 0));				
			}
				
			// Phase = Mathf.Repeat(Phase + (1f-Damping)*GetOutput(PhaseIndex)*2f*Mathf.PI, 2f*Mathf.PI);
		}

		public void SetDamping(float value) {
			Damping = value;
		}

		public float GetPhase() {
			// return Phase / (2f*Mathf.PI);
			return 0f;
		}

		public float[] GetFiLMParameters(int style) {
			for(int i=0; i<EP_HDim; i++){
				FiLMParameters[i] = GetVectorValue(Alphas_1[style], i);
				FiLMParameters[i + EP_HDim] = GetVectorValue(Betas_1[style], i);
				FiLMParameters[i + 2*EP_HDim] = GetVectorValue(Alphas_2[style], i);
				FiLMParameters[i + 3*EP_HDim] = GetVectorValue(Betas_2[style], i);
			}
			return FiLMParameters;
		}

	}

}