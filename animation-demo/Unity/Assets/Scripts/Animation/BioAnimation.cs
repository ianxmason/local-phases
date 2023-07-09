using UnityEngine;
using System;
using System.Collections.Generic;
using DeepLearning;
#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(Actor))]
public class BioAnimation : MonoBehaviour {

	public bool DrawTrajectory = true;
	public bool DrawFiLMParameters = false;
	public bool DrawFootContacts = false;
	public bool DrawStateMachine = false;
	public bool DrawPerformance = false;

	public bool UseGait = false;
	public bool UseOneHot = false;
	public bool UseVelocity = true;
	public bool UsePerStyleVelocities = true;

	[HideInInspector] public Controller Controller;

	private Actor Actor;
	private NeuralNetworkLocal NN;
	private Trajectory Trajectory;
	//private HeightMap HeightMap;

	//Trajectory for 60 Hz Framerate
	private const int Framerate = 60;
	private const int Points = 111; // 121;
	private const int PointSamples = 12; //13;
	private const int PastPoints = 60;
	private const int FuturePoints = 60;
	private const int RootPointIndex = 60;
	private const int PointDensity = 10;


	private float NetworkPredictionTime = 0f;
	private float DrawingTime = 0f;
	private float PreprocessingTime = 0f;
	private float PostprocessingTime = 0f;

	private Vector3 TargetDirection;
	private Vector3 TargetVelocity;
	private float TargetGain = 0.25f;
	private float TargetDecay = 0f; // 0.05f;
	private float Correction = 1f;
	private float[] alpha_1 = new float[512];
	private float[] beta_1 = new float[512];
	private float[] alpha_2 = new float[512];
	private float[] beta_2 = new float[512];
	// private float[] alpha_1 = new float[128];
	// private float[] beta_1 = new float[128];
	// private float[] alpha_2 = new float[128];
	// private float[] beta_2 = new float[128];
	private float[] rcontacts = new float[60];
	private float[] lcontacts = new float[60];
	private float[] smoothrcontacts = new float[60];
	private float[] smoothlcontacts = new float[60];

	private UltimateIK IKModel;
	private UltimateIK.Model[] IKModels = new UltimateIK.Model[8];
	private UltimateIK.Model[] FootIKModels = new UltimateIK.Model[2];
	private bool ArmIKBool = true;
	private bool FootIKBool = true;
	private float pow = 2f; // 0.9f;
	private float thresh = 0.2f; //0.15f;
	[HideInInspector] public Vector3[] NNPositions;
	[HideInInspector] public Vector3[] NNFwds;
	[HideInInspector] public Vector3[] NNUps;
	[HideInInspector] public Vector3[] NNVelocities;
	private string style_1 = "0";
	private string style_2 = "0";
	private string style_3 = "0";
	private float style_interp = 0f;
	private Vector3 style_interp_vector = new Vector3(0f, 0f, 0f);
	private float style_interp_x = 0f;
	private float style_interp_y = 0f;
	private Vector2 marker_pos = new Vector2(0f, 0f);
	private Vector2 triangle_left = new Vector2(0.06f, 0.09f);
	private Vector2 triangle_right = new Vector2(0.16f, 0.09f);
	private Vector2 triangle_top = new Vector2(0.11f, 0.19f);  // I think equilateral should be 0.0866 above base in carrtesian co-ordinates - instead is 0.1 above base (barycentric???)

	private float[] prediction_times = new float[1000];
	private int current_frame_pred = 0;

	void Reset() {
		Controller = new Controller();
	}

	void Awake() {
		Actor = GetComponent<Actor>();
		NN = GetComponent<NeuralNetworkLocal>();
		// Trajectory = new Trajectory(Points, Controller.GetNames(), Controller.GetNames(), transform.position, transform.forward);
		Trajectory = new Trajectory(Points, Controller.GetStyleNames(), Controller.GetGaitNames(), transform.position, transform.forward);
		// Controller.Styles.Length = 0. Currently we use no styles and no gaits
		for(int i=0; i<Trajectory.Points.Length; i++) {
			if(Controller.Gaits.Length > 0) {
				Trajectory.Points[i].Styles[0] = 1f;
				// Trajectory.Points[i].Actions[0] = 1f;
				Trajectory.Points[i].Gaits[0] = 1f;
			}
			Trajectory.Points[i].Phase = Mathf.Repeat(2f * (float)i / (float)(Trajectory.Points.Length-1), 1f);
			// Trajectory.Points[i].Contacts = new float[2];
		}

		for(int i=0; i<4; i++){
			// Right arm, one bone at a time
			IKModels[i] = UltimateIK.BuildModel(Actor.Bones[i+7].Transform, Actor.Bones[i+8].Transform);
			// Left arm
			IKModels[i+4] = UltimateIK.BuildModel(Actor.Bones[i+12].Transform, Actor.Bones[i+13].Transform);
		}

		// Foot IK
		FootIKModels[0] = UltimateIK.BuildModel(Actor.Bones[17].Transform, Actor.Bones[20].Transform);  // Hip (17) as root, Toe (20) as objective
		FootIKModels[1] = UltimateIK.BuildModel(Actor.Bones[21].Transform, Actor.Bones[24].Transform);

		// Allows for model predictions and not IK corrections to be fed in again
		NNPositions = new Vector3[Actor.Bones.Length];
		NNFwds = new Vector3[Actor.Bones.Length];
		NNUps = new Vector3[Actor.Bones.Length];
		NNVelocities = new Vector3[Actor.Bones.Length];
		for(int i=0; i<Actor.Bones.Length; i++) {
			NNPositions[i] = Actor.Bones[i].Transform.position;
			NNFwds[i] = Actor.Bones[i].Transform.forward;
			NNUps[i] = Actor.Bones[i].Transform.up;
			NNVelocities[i] = Actor.Bones[i].Velocity;
		}

		for(int i=0; i<rcontacts.Length; i++){
			rcontacts[i] = 0;
			lcontacts[i] = 0;
			smoothrcontacts[i] = 0;
			smoothlcontacts[i] = 0;
		}
	}

	void Start() {
		Utility.SetFPS(60);
	}

	void LateUpdate() {
		PredictTrajectory();
		if(NN.Parameters != null) {
			Animate();
		}
	}

	public Actor GetActor() {
		return Actor;
	}

	public Trajectory GetTrajectory() {
		return Trajectory;
	}

	private void PredictTrajectory() {
		//Calculate Bias
		// float bias = Controller.PoolBias(Trajectory.Points[RootPointIndex].Styles);  // We moved bias from styles to gaits
		// float bias = Controller.PoolBias(Trajectory.Points[RootPointIndex].Gaits);
		// float bias = 1f;
		float[] single_weight = {1f};  // quick hack we can use the multiplier to increase speed for a style this way
		float bias;
		if(!UsePerStyleVelocities){
			if(UseGait){
				bias = Controller.PoolVelocity(Controller.GetGait());
			}
			else{
				bias = Controller.PoolVelocity(single_weight);
			}
		}
		else if(UseGait){
			Debug.Log("Cannot use Per Style Velocities With Gait Label!");
			bias = 0f;
		}
		else{
			bias = Controller.PerStyleVelocity(single_weight, Convert.ToInt32(style_1), Convert.ToInt32(style_2), style_interp);
		}		
		// Debug.Log(bias);
		//Determine Control
		float turn = Controller.QueryTurn();
		Vector3 move = Controller.QueryMove();
		bool control = turn != 0f || move != Vector3.zero;

		//Update Target Direction / Velocity / Correction
		// TargetDirection = Vector3.Lerp(TargetDirection, Quaternion.AngleAxis(turn * 60f, Vector3.up) * Trajectory.Points[RootPointIndex].GetDirection(), control ? TargetGain : TargetDecay);
		TargetDirection = Vector3.Slerp(TargetDirection, Quaternion.AngleAxis(turn * 60f, Vector3.up) * Trajectory.Points[RootPointIndex].GetDirection(), control ? TargetGain : TargetDecay);
		TargetVelocity = Vector3.Lerp(TargetVelocity, bias * (Quaternion.LookRotation(TargetDirection, Vector3.up) * move).normalized, control ? TargetGain : TargetDecay);
		// TargetVelocity = Vector3.Lerp(TargetVelocity, (Quaternion.LookRotation(TargetDirection, Vector3.up) * move).normalized, control ? TargetGain : TargetDecay); // may need to add bias back
		// TrajectoryCorrection = Utility.Inter	polate(TrajectoryCorrection, Mathf.Max(move.normalized.magnitude, Mathf.Abs(turn)), control ? TargetGain : TargetDecay);

		//Predict Future Trajectory
		Vector3[] positions = new Vector3[Trajectory.Points.Length];
		positions[RootPointIndex] = Trajectory.Points[RootPointIndex].GetTransformation().GetPosition();
		for(int i=RootPointIndex+1; i<Trajectory.Points.Length; i++) {
			float bias_pos = 0.75f;
			float bias_dir = 1.25f;
			// float bias_vel = 1.50f;
			float weight = (float)(i - RootPointIndex) / (float)FuturePoints;
			float scale_pos = 1.0f - Mathf.Pow(1.0f - weight, bias_pos);
			float scale_dir = 1.0f - Mathf.Pow(1.0f - weight, bias_dir);
			// float scale_vel = 1.0f - Mathf.Pow(1.0f - weight, bias_vel);

			float scale = 1f / (Trajectory.Points.Length - (RootPointIndex + 1f));

			positions[i] = positions[i-1] + 
				Vector3.Lerp(
				Trajectory.Points[i].GetPosition() - Trajectory.Points[i-1].GetPosition(), 
				scale * TargetVelocity,
				scale_pos
				);

			Trajectory.Points[i].SetDirection(Vector3.Lerp(Trajectory.Points[i].GetDirection(), TargetDirection, scale_dir));
			// Trajectory.Points[i].SetVelocity(Vector3.Lerp(Trajectory.Points[i].GetVelocity(), TargetVelocity, scale_vel));
			Trajectory.Points[i].Phase = Mathf.Repeat(Trajectory.Points[RootPointIndex].Phase + (i-RootPointIndex)*(Trajectory.Points[RootPointIndex].Phase - Trajectory.Points[RootPointIndex-1].Phase), 1f);
		}
		for(int i=RootPointIndex+1; i<Trajectory.Points.Length; i++) {
			Trajectory.Points[i].SetPosition(positions[i]);
		}

		// Control = Utility.Interpolate(Control, Controller.GetStyle(), 0.25f);
		// float[] signal = ArrayExtensions.Sub(Control, Trajectory.Points[RootPointIndex].Styles);
		// for(int i=RootPointIndex; i<Trajectory.Points.Length; i++) {
		// 	Trajectory.Points[i].Signals = signal;
		// }

	}

	private void FootIK(UltimateIK.Model ik, float weight) {
		ik.Activation = UltimateIK.ACTIVATION.Constant;
		ik.Objectives.First().SetTarget(ik.Bones.Last().Transform.position, weight);
		ik.Objectives.First().SetTarget(ik.Bones.Last().Transform.rotation);
		ik.Iterations = 50;
		ik.Solve();
	}

	private float SmoothStep(float x, float power, float threshold) {
		// As contact prediction is not binary 1 or 0, this helps inmprove the value for IK
		//Validate
		x = Mathf.Clamp(x, 0f, 1f);
		power = Mathf.Max(power, 0f);
		threshold = Mathf.Clamp(threshold, 0f, 1f);

		//Skew X
		if(threshold == 0f || threshold == 1f) {
			x = 1f - threshold;
		} else {
			if(threshold < 0.5f) {
				x = 1f - Mathf.Pow(1f-x, 0.5f / threshold);
			}
			if(threshold > 0.5f) {
				x = Mathf.Pow(x, 0.5f / (1f-threshold));
			}
		}

		//Evaluate Y
		if(x < 0.5f) {
			return 0.5f*Mathf.Pow(2f*x, power);
		}
		if(x > 0.5f) {
			return 1f - 0.5f*Mathf.Pow(2f-2f*x, power);
		}
		return 0.5f;
	}





	private void Animate() {
		//Preprocess
		System.DateTime t1 = Utility.GetTimestamp();

		//Root Point
		Trajectory.Point rootPoint = Trajectory.Points[RootPointIndex];

		//Calculate Root
		Matrix4x4 root = rootPoint.GetTransformation();
		// root[1,3] = 0f; //For flat terrain

		NN.ResetPivot();
		//Input Trajectory Positions / Directions / Velocities / Styles
		for(int i=0; i<PointSamples; i++) {
			Vector3 pos = GetSample(i).GetPosition().GetRelativePositionTo(root);
			Vector3 dir = GetSample(i).GetDirection().GetRelativeDirectionTo(root);
			// Vector3 vel = GetSample(i).GetVelocity().GetRelativeDirectionTo(root);
			NN.Feed(pos.x);
			NN.Feed(pos.z);
			NN.Feed(dir.x);
			NN.Feed(dir.z);
			// NN.Feed(vel.x);
			// NN.Feed(vel.z);
			// NN.Feed(GetSample(i).Styles);
			// NN.Feed(GetSample(i).Signals);
		}

		// Input Bone Positions / Velocities
		for(int i=0; i<Actor.Bones.Length; i++) {
			// NN.Feed(Actor.Bones[i].Transform.position.GetRelativePositionTo(root));
			NN.Feed(NNPositions[i].GetRelativePositionTo(root));
			// NN.Feed(Actor.Bones[i].Transform.forward.GetRelativeDirectionTo(root));
			NN.Feed(NNFwds[i].GetRelativeDirectionTo(root));
			// NN.Feed(Actor.Bones[i].Transform.up.GetRelativeDirectionTo(root));
			NN.Feed(NNUps[i].GetRelativeDirectionTo(root));
			if(UseVelocity){
				// NN.Feed(Actor.Bones[i].Velocity.GetRelativeDirectionTo(root));
				NN.Feed(NNVelocities[i].GetRelativeDirectionTo(root));
			}
		}

		// We do not feed phase as it is handled within the PFNN_FiLM class

		// Similarly we do not currently feed the style as we haven't yet decided the best way to change it

		// Feed gaits if required - otherwise comment out (or add public boolean flag)
		if(UseGait){
			for(int i=0; i<rootPoint.Gaits.Length; i++){
				Debug.Log("Feeding gait labels");
				Debug.Log(Controller.GetGait()[0]);
				Debug.Log(Controller.GetGait()[1]);
				NN.Feed(Controller.GetGait()[i]);
			}
		}

		if(UseOneHot){
			if(UseGait){
				Debug.Log("No model trained with gaits and one hot labels.");
			}
			else{
				for(int i=0; i<100; i++){ //hardcoded one hot dimensionality
					if(i==Convert.ToInt32(style_1)){
						NN.Feed(1f - style_interp);
					}
					else if(i==Convert.ToInt32(style_2)){
						NN.Feed(style_interp);
					}
					else{
						NN.Feed(0f);
					}
				}
			}
		}

		PreprocessingTime = (float)Utility.GetElapsedTime(t1);

		//Predict
		System.DateTime t2 = Utility.GetTimestamp();
		// if (NN is PFNNBVAE) {
		// 	((PFNNBVAE)NN).Predict_and_Change(LatentChanges, multiply, shift);
		// } else {
		// 	NN.Predict();
		// }

        // NN.Predict(Convert.ToInt32(style_1), Convert.ToInt32(style_2), style_interp);
		NN.Predict3Way(Convert.ToInt32(style_1), Convert.ToInt32(style_2),  Convert.ToInt32(style_3), style_interp_vector);
		NetworkPredictionTime = (float)Utility.GetElapsedTime(t2);

		// for tun time-comparisons
		prediction_times[current_frame_pred % 1000] = NetworkPredictionTime;
		current_frame_pred += 1;
		if (current_frame_pred >= 1000){
			Debug.Log(string.Format("Average prediction time: {0}s", prediction_times.Sum() / 1000f));
		}

		//Postprocess
		System.DateTime t3 = Utility.GetTimestamp();
		NN.ResetPivot();
		//Update Past State
		for(int i=0; i<RootPointIndex; i++) {
			Trajectory.Points[i].SetPosition(Trajectory.Points[i+1].GetPosition());
			Trajectory.Points[i].SetDirection(Trajectory.Points[i+1].GetDirection());
			Trajectory.Points[i].Phase = Trajectory.Points[i+1].Phase;
			// for(int j=0; j<Trajectory.Points[i].Styles.Length; j++) {
			// 	Trajectory.Points[i].Styles[j] = Trajectory.Points[i+1].Styles[j];
			// }
			// for(int j=0; j<Trajectory.Points[i].Actions.Length; j++) {
			// 	Trajectory.Points[i].Actions[j] = Trajectory.Points[i+1].Actions[j];
			// }
			// for(int j=0; j<Trajectory.Points[i].Contacts.Length; j++) {
			// 	Trajectory.Points[i].Contacts[j] = Trajectory.Points[i+1].Contacts[j];
			// }
		}

		// I DO NOT CURRENTLY EXPORT A ROOT TRANSFORM 
		//Update Root
		// Vector3 rootTransformation = NN.ReadVector3();
		// root *= Matrix4x4.TRS(new Vector3(rootTransformation.x, 0f, rootTransformation.z), Quaternion.AngleAxis(rootTransformation.y, Vector3.up), Vector3.one);;
		// rootPoint.SetTransformation(root);
		// float[] style = NN.Read(Controller.Styles.Length);
		// for(int j=0; j<style.Length; j++) {
		// 	rootPoint.Styles[j] = Mathf.Clamp(style[j], 0f, 1f);
		// }

		//Update Current to Future Trajectory
		for(int i=6; i<PointSamples; i++) {
			Vector3 pos = new Vector3(NN.Read(), 0f, NN.Read()).GetRelativePositionFrom(root);
			Vector3 dir = new Vector3(NN.Read(), 0f, NN.Read()).normalized.GetRelativeDirectionFrom(root);
			// Vector3 vel = new Vector3(NN.Read(), 0f, NN.Read()).GetRelativeDirectionFrom(root);
			// pos = Vector3.Lerp(GetSample(i).GetPosition() + vel / Framerate, pos, 0.5f);

			// blend this with current position and direction
			float weight = 0.15f;
			pos = Vector3.Lerp(pos, GetSample(i).GetPosition(), weight);  
			dir = Vector3.Slerp(dir, GetSample(i).GetDirection(), weight);

			GetSample(i).SetPosition(pos);
			GetSample(i).SetDirection(dir);
			// GetSample(i).SetVelocity(vel);				
			// GetSample(i).Styles = NN.Read(Controller.Styles.Length);	
		}

		//Read Posture
		Vector3[] positions = new Vector3[Actor.Bones.Length];
		Vector3[] forwards = new Vector3[Actor.Bones.Length];
		Vector3[] upwards = new Vector3[Actor.Bones.Length];
		Vector3[] velocities = new Vector3[Actor.Bones.Length];
		for(int i=0; i<Actor.Bones.Length; i++) {
			Vector3 position = new Vector3(NN.Read(), NN.Read(), NN.Read()).GetRelativePositionFrom(root);
			Vector3 forward = new Vector3(NN.Read(), NN.Read(), NN.Read()).normalized.GetRelativeDirectionFrom(root);
			Vector3 upward = new Vector3(NN.Read(), NN.Read(), NN.Read()).normalized.GetRelativeDirectionFrom(root);
			if(UseVelocity){
				Vector3 velocity = new Vector3(NN.Read(), NN.Read(), NN.Read()).GetRelativeDirectionFrom(root);
				positions[i] = Vector3.Lerp(Actor.Bones[i].Transform.position + velocity / Framerate, position, 0.5f);
				velocities[i] = velocity;
			}
			else{
				positions[i] = position; 
			}			
			forwards[i] = forward;
			upwards[i] = upward;
		}

		// Don't read or update phase as this is handled in PFNN_FiLM

		//Interpolate Current to Future Trajectory
		for(int i=RootPointIndex; i<Trajectory.Points.Length; i++) {
			int prevSampleIndex = GetPreviousSample(i).GetIndex() / PointDensity;
			int nextSampleIndex = GetNextSample(i).GetIndex() / PointDensity;
			float weight = (float)(i % PointDensity) / PointDensity;
			Trajectory.Point sample = Trajectory.Points[i];
			Trajectory.Point prevSample = GetSample(prevSampleIndex);
			Trajectory.Point nextSample = GetSample(nextSampleIndex);
			sample.SetPosition(Vector3.Lerp(prevSample.GetPosition(), nextSample.GetPosition(), weight));
			sample.SetDirection(Vector3.Slerp(prevSample.GetDirection(), nextSample.GetDirection(), weight));
			// for(int j=0; j<Controller.Styles.Length; j++) {
			// 	sample.Styles[j] = Mathf.Lerp(prevSample.Styles[j], nextSample.Styles[j], weight);
			// }
		}


		//Assign Posture
		transform.position = root.GetPosition();
		transform.rotation = root.GetRotation();
		for(int i=0; i<Actor.Bones.Length; i++) {
			if(ArmIKBool){
				if(i==8 || i==9 || i==10 || i==11 || i==13 || i==14 || i==15 || i==16){ // don't set right or left arm positions - they remain set as previous frame
				}
				else{
					Actor.Bones[i].Transform.position = positions[i];
				}
			}
			else{
				Actor.Bones[i].Transform.position = positions[i];
			}				
			Actor.Bones[i].Transform.rotation = Quaternion.LookRotation(forwards[i], upwards[i]);
			// Actor.Bones[i].Transform.rotation = rotations[i];
			if(UseVelocity){
				Actor.Bones[i].Velocity = velocities[i];
			}		
		}

		// Foot Contact IK
		for(int i=0; i<rcontacts.Length-1; i++){
			rcontacts[i] = rcontacts[i+1];
			lcontacts[i] = lcontacts[i+1];
			smoothrcontacts[i] = smoothrcontacts[i+1];
			smoothlcontacts[i] = smoothlcontacts[i+1];
		}

		// For PFNN
		// float phaseupdate = NN.Read(); // We don't need this update just to increase the pivot by one so we get the contacts correctly
		// For LPN
		float[] phaseupdate = NN.Read(16);
		// For MANN - read nothing

		rcontacts[59] = NN.Read();
		lcontacts[59] = NN.Read();
		smoothrcontacts[59] = SmoothStep(rcontacts[59], pow, thresh);
		smoothlcontacts[59] = SmoothStep(lcontacts[59], pow, thresh);

		// float test = NN.Read();
		// Debug.Log(test); // should be out of bounds if read the right size phaseupdate in for LPN/MANN/PFNN

		// Debug.Log(smoothrcontacts[59]);
		// Debug.Log(FootIKBool);
		if(FootIKBool){
			// Debug.Log("Contacts");
			// Debug.Log(lcontacts[59]);
			// Debug.Log(SmoothStep(lcontacts[59], pow, thresh));

			// FootIK(FootIKModels[0], 1-rcontacts[59]);
			// FootIK(FootIKModels[1], 1-lcontacts[59]);

			// FootIK(FootIKModels[0], 1-SmoothStep(rcontacts[59], pow, thresh));
			// FootIK(FootIKModels[1], 1-SmoothStep(lcontacts[59], pow, thresh));

			// Should be indentical to above 2 lines
			FootIK(FootIKModels[0], 1-smoothrcontacts[59]);
			FootIK(FootIKModels[1], 1-smoothlcontacts[59]);
		}



		//IK Post-Processing
		// for(int i=0; i<Actor.Bones.Length; i++) {
		// 	foreach(FootIK ik in Actor.Bones[i].Transform.GetComponents<FootIK>()) {
		// 		ik.Solve();
		// 	}
		// }
		if(ArmIKBool){
			// Arms one bone at a time - Effective
			for(int i=8; i<12; i++) {
				IKModels[i-8].Objectives[0].SetTarget(positions[i]);
				IKModels[i-8].Objectives[0].SolveRotation = false;  // off because don't want to use predicted rotation as target, want to fix it.
				IKModels[i-8].Solve();
			}
			for(int i=13; i<17; i++) {
				IKModels[i-9].Objectives[0].SetTarget(positions[i]);
				IKModels[i-9].Objectives[0].SolveRotation = false;  
				IKModels[i-9].Solve();
			}
		}

		// Only prediction
		// for(int i=0; i<Actor.Bones.Length; i++) { // set NNPositions to be fed into NN next
		// 	NNPositions[i] = positions[i]; 
		// 	NNFwds[i] = forwards[i];
		// 	NNUps[i] = upwards[i];
		// 	if(UseVelocity){
		// 		NNVelocities[i] = velocities[i];
		// 	}
		// }

		// After IK
		for(int i=0; i<Actor.Bones.Length; i++) {
			NNPositions[i] = Actor.Bones[i].Transform.position;
			NNFwds[i] = Actor.Bones[i].Transform.forward;
			NNUps[i] = Actor.Bones[i].Transform.up;
			if(UseVelocity){
				NNVelocities[i] = Actor.Bones[i].Velocity;
			}
		}

		if(DrawFiLMParameters){
			Matrix film_1 = NN.GetMatrix("style_1_alpha_1"); // ("Style_"+Convert.ToInt32(style_1).ToString("D3")+"_scale1");
			Matrix film_2 = NN.GetMatrix("style_1_beta_1"); // ("Style_"+Convert.ToInt32(style_1).ToString("D3")+"_shift1");
			Matrix film_3 = NN.GetMatrix("style_1_alpha_2"); // ("Style_"+Convert.ToInt32(style_1).ToString("D3")+"_scale2");
			Matrix film_4 = NN.GetMatrix("style_1_beta_2"); // ("Style_"+Convert.ToInt32(style_1).ToString("D3")+"_shift2");
			for(int i=0; i<alpha_1.Length; i++){  
				alpha_1[i] = film_1.GetValue(i, 0);
			}
			for(int i=0; i<beta_1.Length; i++){
				beta_1[i] = film_2.GetValue(i, 0);
			}
			// // if(film.GetRows() == alpha_1.Length + beta_1.Length + alpha_2.Length + beta_2.Length){ // 2 film layers
			// // 	for(int i=0; i<alpha_2.Length; i++){
			// // 		alpha_2[i] = film.GetValue(i + alpha_1.Length + beta_1.Length, 0);
			// // 	}
			// // 	for(int i=0; i<beta_2.Length; i++){
			// // 		beta_2[i] = film.GetValue(i + alpha_1.Length + beta_1.Length + alpha_2.Length, 0);
			// // 	}
			// // }
			// // else{
			// // 	for(int i=0; i<alpha_2.Length; i++){
			// // 		alpha_2[i] = 0f;
			// // 	}
			// // 	for(int i=0; i<beta_2.Length; i++){
			// // 		beta_2[i] = 0f;
			// // 	}
			// // }
			for(int i=0; i<alpha_2.Length; i++){
				alpha_2[i] = film_3.GetValue(i, 0);
			}
			for(int i=0; i<beta_2.Length; i++){
				beta_2[i] = film_4.GetValue(i, 0);
			}
		}
		
		// if(DrawFiLMParameters){
		// 	float[] FiLMParameters = (PFNN_FILM)NN.GetFiLMParameters(curr_style); 
		// 	for(int i=0; i<alpha_1.Length; i++){  
		// 		alpha_1[i] = FiLMParameters[i];
		// 	}
		// 	for(int i=0; i<beta_1.Length; i++){
		// 		beta_1[i] = FiLMParameters[i + alpha_1.Length];
		// 	}
		// 	// if(film.GetRows() == alpha_1.Length + beta_1.Length + alpha_2.Length + beta_2.Length){ // 2 film layers
		// 	// 	for(int i=0; i<alpha_2.Length; i++){
		// 	// 		alpha_2[i] = film.GetValue(i + alpha_1.Length + beta_1.Length, 0);
		// 	// 	}
		// 	// 	for(int i=0; i<beta_2.Length; i++){
		// 	// 		beta_2[i] = film.GetValue(i + alpha_1.Length + beta_1.Length + alpha_2.Length, 0);
		// 	// 	}
		// 	// }
		// 	// else{
		// 	// 	for(int i=0; i<alpha_2.Length; i++){
		// 	// 		alpha_2[i] = 0f;
		// 	// 	}
		// 	// 	for(int i=0; i<beta_2.Length; i++){
		// 	// 		beta_2[i] = 0f;
		// 	// 	}
		// 	// }
		// 	for(int i=0; i<alpha_2.Length; i++){
		// 		alpha_2[i] = FiLMParameters[i + alpha_1.Length + beta_1.Length];
		// 	}
		// 	for(int i=0; i<beta_2.Length; i++){
		// 		beta_2[i] = FiLMParameters[i + alpha_1.Length + beta_1.Length + alpha_2.Length];
		// 	}
		// }

		PostprocessingTime = (float)Utility.GetElapsedTime(t3);
	}

	private Trajectory.Point GetSample(int index) {
		return Trajectory.Points[Mathf.Clamp(index*10, 0, Trajectory.Points.Length-1)];
	}

	private Trajectory.Point GetPreviousSample(int index) {
		return GetSample(index / 10);
	}

	private Trajectory.Point GetNextSample(int index) {
		if(index % 10 == 0) {
			return GetSample(index / 10);
		} else {
			return GetSample(index / 10 + 1);
		}
	}

	void OnGUI() {
		if(NN.Parameters == null) {
			return;
		}
		if(DrawPerformance) {
			UltiDraw.Begin();
			UltiDraw.DrawGUILabel(0.025f, 0.9f, 0.01f, string.Format("{0:0.0} ms for Preprocessing", PreprocessingTime * 1000.0f, 1.0f / PreprocessingTime));
			UltiDraw.DrawGUILabel(0.025f, 0.9f, 0.01f, string.Format("{0:0.0} ms for Preprocessing", PreprocessingTime * 1000.0f, 1.0f / PreprocessingTime));
			UltiDraw.DrawGUILabel(0.025f, 0.925f, 0.01f, string.Format("{0:0.0} ms for Postprocessing", PostprocessingTime * 1000.0f, 1.0f / PostprocessingTime));
			UltiDraw.DrawGUILabel(0.025f, 0.95f, 0.01f, string.Format("{0:0.0} ms for Drawing", DrawingTime * 1000.0f, 1.0f / DrawingTime));
			UltiDraw.DrawGUILabel(0.025f, 0.975f, 0.01f, string.Format("{0:0.0} ms for Network", NetworkPredictionTime * 1000.0f, 1.0f / NetworkPredictionTime));
			UltiDraw.End();
		}

		// IK options
		ArmIKBool = GUI.Toggle(Utility.GetGUIRect(0.001f, 0.4f, 0.075f, 0.04f), ArmIKBool, "ArmIK");
		FootIKBool = GUI.Toggle(Utility.GetGUIRect(0.001f, 0.45f, 0.075f, 0.04f), FootIKBool, "FootIK");
		GUI.Label(Utility.GetGUIRect(0.001f, 0.5f, 0.074f, 0.1f), "Smoothing Power");
		pow =  Convert.ToSingle(GUI.TextField(Utility.GetGUIRect(0.075f, 0.515f, 0.05f, 0.05f), pow.ToString()));
		GUI.Label(Utility.GetGUIRect(0.001f, 0.57f, 0.074f, 0.1f), "Smoothing Threshold");
		thresh =  Convert.ToSingle(GUI.TextField(Utility.GetGUIRect(0.075f, 0.585f, 0.05f, 0.05f), thresh.ToString()));

		// Ability to change runtimestyle from game view
		// style_1 =  GUI.TextField(Utility.GetGUIRect(0.005f, 0.945f, 0.05f, 0.05f), style_1);		
		// style_interp = GUI.HorizontalSlider(Utility.GetGUIRect(0.055f, 0.945f, 0.10f, 0.05f), style_interp, 0f, 1f);
		// GUI.Label(Utility.GetGUIRect(0.090f, 0.955f, 0.04f, 0.04f), style_interp.ToString());
		// style_2 =  GUI.TextField(Utility.GetGUIRect(0.155f, 0.945f, 0.05f, 0.05f), style_2);

		// For 3 styles
		style_1 =  GUI.TextField(Utility.GetGUIRect(0.005f, 0.925f, 0.05f, 0.05f), style_1);
		style_2 =  GUI.TextField(Utility.GetGUIRect(0.160f, 0.925f, 0.05f, 0.05f), style_2);
		style_3 =  GUI.TextField(Utility.GetGUIRect(0.0825f, 0.75f, 0.05f, 0.05f), style_3);
		style_interp_x = GUI.HorizontalSlider(Utility.GetGUIRect(0.0575f, 0.945f, 0.10f, 0.05f), style_interp_x, 0f, 1f);
		style_interp_y = GUI.VerticalSlider(Utility.GetGUIRect(0.035f, 0.800f, 0.05f, 0.10f), style_interp_y, 1f, 0f);
		float style_interp_x_clamp =  triangle_left.x + 0.05f * (style_interp_y) + style_interp_x * 0.1f * (1 - style_interp_y); // clamps within triangle
		float style_interp_y_clamp = triangle_left.y + style_interp_y * 0.1f;
		marker_pos.x = style_interp_x_clamp;
		marker_pos.y = style_interp_y_clamp;  
		// To get interpolation values we want to use barycentric co-ordinates
		// https://stackoverflow.com/questions/8697521/interpolation-of-a-triangle
		// https://codeplea.com/triangular-interpolation
		float p_x = style_interp_x_clamp;
		float p_y = style_interp_y_clamp;		
		float x_v1 = triangle_left.x;
		float y_v1 = triangle_left.y;
		float x_v2 = triangle_right.x;
		float y_v2 = triangle_right.y;	
		float x_v3 = triangle_top.x;
		float y_v3 = triangle_top.y;	
		float w_v1 = ((y_v2 - y_v3) * (p_x - x_v3) + (x_v3 - x_v2) * (p_y - y_v3)) / ((y_v2 - y_v3) * (x_v1 - x_v3) + (x_v3 - x_v2) * (y_v1 - y_v3));
		float w_v2 = ((y_v3 - y_v1) * (p_x - x_v3) + (x_v1 - x_v3) * (p_y - y_v3)) / ((y_v2 - y_v3) * (x_v1 - x_v3) + (x_v3 - x_v2) * (y_v1 - y_v3));
		float w_v3 = 1 - w_v1 - w_v2;
		// Label is ugly better demo without
		// UltiDraw.Begin();
		// UltiDraw.DrawGUILabel(0.065f, 0.720f, 0.015f, string.Format("Barycentric ({0}, {1}, {2})", w_v1, w_v2, w_v3));
		// UltiDraw.End();
		style_interp = 0f;
		style_interp_vector.x = w_v1;
		style_interp_vector.y = w_v2;
		style_interp_vector.z = w_v3;
	}

	void OnRenderObject() {
		System.DateTime timestamp = Utility.GetTimestamp();
		if(Application.isPlaying) {
			if(NN.Parameters == null) {
				return;
			}

			if(DrawTrajectory) {
				Trajectory.Draw(10);
			}

			if(DrawFiLMParameters) {
				UltiDraw.Begin();

				Color[] colors = UltiDraw.GetRainbowColors(Controller.Styles.Length);
				// UltiDraw.DrawGUIBars(new Vector2(0.12f, 0.93f), new Vector2(0.2f, 0.1f), alpha_1, 01f, 1f, 0.001f, UltiDraw.DarkGrey, UltiDraw.White);
				// UltiDraw.DrawGUIBars(new Vector2(0.35f, 0.93f), new Vector2(0.2f, 0.1f), beta_1, 01f, 1f, 0.001f, UltiDraw.DarkGrey, UltiDraw.White);				
				// UltiDraw.DrawGUIBars(new Vector2(0.58f, 0.93f), new Vector2(0.2f, 0.1f), alpha_2, 01f, 1f, 0.001f, UltiDraw.DarkGrey, UltiDraw.White);
				// UltiDraw.DrawGUIBars(new Vector2(0.81f, 0.93f), new Vector2(0.2f, 0.1f), beta_2, 01f, 1f, 0.001f, UltiDraw.DarkGrey, UltiDraw.White);
				UltiDraw.DrawGUIBars(new Vector2(0.12f, 0.93f), new Vector2(0.2f, 0.1f), alpha_1, -5f, 5f, 0.001f, UltiDraw.DarkGrey, UltiDraw.White);
				UltiDraw.DrawGUIBars(new Vector2(0.35f, 0.93f), new Vector2(0.2f, 0.1f), beta_1, -5f, 5f, 0.001f, UltiDraw.DarkGrey, UltiDraw.White);				
				UltiDraw.DrawGUIBars(new Vector2(0.58f, 0.93f), new Vector2(0.2f, 0.1f), alpha_2, -5f, 5f, 0.001f, UltiDraw.DarkGrey, UltiDraw.White);
				UltiDraw.DrawGUIBars(new Vector2(0.81f, 0.93f), new Vector2(0.2f, 0.1f), beta_2, -5f, 5f, 0.001f, UltiDraw.DarkGrey, UltiDraw.White);
				UltiDraw.End();
			}

			// Foot contacts to examine NN output
			if(DrawFootContacts) {
				UltiDraw.Begin();
				UltiDraw.DrawGUIFunction(new Vector2(0.12f, 0.8f), new Vector2(0.2f, 0.1f), lcontacts, -0.1f, 1.1f, UltiDraw.DarkGrey, UltiDraw.Cyan);
				UltiDraw.DrawGUIFunction(new Vector2(0.35f, 0.8f), new Vector2(0.2f, 0.1f), smoothlcontacts, -0.1f, 1.1f, UltiDraw.DarkGrey, UltiDraw.Cyan);
				UltiDraw.DrawGUIFunction(new Vector2(0.58f, 0.8f), new Vector2(0.2f, 0.1f), rcontacts, -0.1f, 1.1f, UltiDraw.DarkGrey, UltiDraw.Cyan);
				UltiDraw.DrawGUIFunction(new Vector2(0.81f, 0.8f), new Vector2(0.2f, 0.1f), smoothrcontacts, -0.1f, 1.1f, UltiDraw.DarkGrey, UltiDraw.Cyan);
				UltiDraw.End();
			}

			// Would prefer this in OnGUI but triangle/circle doesn't render when in there - check this with Sebastian		
			// For displayin barcyentric co-ordinates for 3 styles	
			UltiDraw.Begin();
			UltiDraw.DrawGUITriangle(triangle_left, triangle_top, triangle_right, UltiDraw.DarkGrey);
			UltiDraw.DrawGUICircle(marker_pos, 0.005f, UltiDraw.Red);
			UltiDraw.End();


			DrawingTime = (float)Utility.GetElapsedTime(timestamp);
		}
	}

	void OnDrawGizmos() {
		if(!Application.isPlaying) {
			OnRenderObject();
		}
	}

	#if UNITY_EDITOR
	[CustomEditor(typeof(BioAnimation))]
	public class BioAnimation_Editor : Editor {

		public BioAnimation Target;

		void Awake() {
			Target = (BioAnimation)target;
		}

		public override void OnInspectorGUI() {
			Undo.RecordObject(Target, Target.name);

			DrawDefaultInspector();

			Target.Controller.Inspector();

			if(GUI.changed) {
				EditorUtility.SetDirty(Target);
			}
		}

	}
	#endif
}