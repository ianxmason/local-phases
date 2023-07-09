using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[System.Serializable]
public class Controller {

	public bool Inspect = false;
	public float RunTimeStyle = 0f;

	public KeyCode Forward = KeyCode.W;
	public KeyCode Back = KeyCode.S;
	public KeyCode Left = KeyCode.A;
	public KeyCode Right = KeyCode.D;
	public KeyCode TurnLeft = KeyCode.Q;
	public KeyCode TurnRight = KeyCode.E;

	public Style[] Styles = new Style[0];
	public Gait[] Gaits = new Gait[0];
	public string VelocitiesPath = "Assets/Demo/Style/Parameters/Multiclip_All_Velocity_100_FC";
	private float[] WalkVelocities = new float[95]; //95 styles
	private float[] RunVelocities = new float[95]; //95 styles
	public Controller() {
		// Could make ReadBinary method similar to Parameters.cs to be reusable
		if(File.Exists(VelocitiesPath+"/Walk_Velocities.bin")){
			using (BinaryReader reader = new BinaryReader(File.Open(VelocitiesPath+"/Walk_Velocities.bin", FileMode.Open))){
				for(int i=0; i<WalkVelocities.Length; i++){
					WalkVelocities[i] = reader.ReadSingle();
				}
			}
		}
		else{
			Debug.Log("File " + VelocitiesPath+"/Walk_Velocities.bin" + " does not exist.");
		}
		if(File.Exists(VelocitiesPath+"/Run_Velocities.bin")){
			using (BinaryReader reader = new BinaryReader(File.Open(VelocitiesPath+"/Run_Velocities.bin", FileMode.Open))){
				for(int i=0; i<RunVelocities.Length; i++){
					RunVelocities[i] = reader.ReadSingle();
				}
			}
		}
		else{
			Debug.Log("File " + VelocitiesPath+"/Run_Velocities.bin" + " does not exist.");
		}
	}
	public float[] GetStyle() {
		float[] style = new float[Styles.Length];
		for(int i=0; i<Styles.Length; i++) {
			style[i] = Styles[i].Query() ? 1f : 0f;
		}
		return style;
	}

	public float[] GetGait() {
		float[] gait = new float[Gaits.Length];
		for(int i=0; i<Gaits.Length; i++) {
			gait[i] = Gaits[i].Query() ? 1f : 0f;
		}
		return gait;
	}

	public string[] GetStyleNames() {
		string[] names = new string[Styles.Length];
		for(int i=0; i<names.Length; i++) {
			names[i] = Styles[i].Name;
		}
		return names;
	}

	public string[] GetGaitNames() {
		string[] names = new string[Gaits.Length];
		for(int i=0; i<names.Length; i++) {
			names[i] = Gaits[i].Name;
		}
		return names;
	}

	public float GetRunTimeStyle(){
		return RunTimeStyle;
	}

	public Vector3 QueryMove() {
		Vector3 move = Vector3.zero;
		if(InputHandler.GetKey(Forward)) {
			move.z += 1f;
		}
		if(InputHandler.GetKey(Back)) {
			move.z -= 1f;
		}
		if(InputHandler.GetKey(Left)) {
			move.x -= 1f;
		}
		if(InputHandler.GetKey(Right)) {
			move.x += 1f;
		}
		return move;
	}

	public float QueryTurn() {
		float turn = 0f;
		if(InputHandler.GetKey(TurnLeft)) {
			turn -= 1f;
		}
		if(InputHandler.GetKey(TurnRight)) {
			turn += 1f;
		}
		return turn;
	}

	public void SetStyleCount(int count) {
		count = Mathf.Max(count, 0);
		if(Styles.Length != count) {
			int size = Styles.Length;
			System.Array.Resize(ref Styles, count);
			for(int i=size; i<count; i++) {
				Styles[i] = new Style();
			}
		}
	}

	public void SetGaitCount(int count) {
		count = Mathf.Max(count, 0);
		if(Gaits.Length != count) {
			int size = Gaits.Length;
			System.Array.Resize(ref Gaits, count);
			for(int i=size; i<count; i++) {
				Gaits[i] = new Gait();
			}
		}
	}


	public bool QueryAnyStyle() {
		for(int i=0; i<Styles.Length; i++) {
			if(Styles[i].Query()) {
				return true;
			}
		}
		return false;
	}

	public bool QueryAnyGait() {
		for(int i=0; i<Gaits.Length-1; i++) { // We don't want to query idle
			if(Gaits[i].Query()) {
				return true;
			}
		}
		return false;
	}

	// public float PoolBias(float[] weights) {
	// 	float bias = 0f;
	// 	for(int i=0; i<weights.Length; i++) {
	// 		float _bias = Styles[i].Bias;
	// 		float max = 0f;
	// 		for(int j=0; j<Styles[i].Multipliers.Length; j++) {
	// 			if(InputHandler.GetKey(Styles[i].Multipliers[j].Key)) {
	// 				max = Mathf.Max(max, Styles[i].Bias * Styles[i].Multipliers[j].Value);
	// 			}
	// 		}
	// 		for(int j=0; j<Styles[i].Multipliers.Length; j++) {
	// 			if(InputHandler.GetKey(Styles[i].Multipliers[j].Key)) {
	// 				_bias = Mathf.Min(max, _bias * Styles[i].Multipliers[j].Value);
	// 			}
	// 		}
	// 		bias += weights[i] * _bias;
	// 	}
	// 	return bias;
	// }

	// public float PoolBias(float[] weights) {
	// 	float bias = 0f;
	// 	for(int i=0; i<weights.Length; i++) {
	// 		float _bias = Gaits[i].Bias;
	// 		float max = 0f;
	// 		for(int j=0; j<Gaits[i].Multipliers.Length; j++) {
	// 			if(InputHandler.GetKey(Gaits[i].Multipliers[j].Key)) {
	// 				max = Mathf.Max(max, Gaits[i].Bias * Gaits[i].Multipliers[j].Value);
	// 			}
	// 		}
	// 		for(int j=0; j<Gaits[i].Multipliers.Length; j++) {
	// 			if(InputHandler.GetKey(Gaits[i].Multipliers[j].Key)) {
	// 				_bias = Mathf.Min(max, _bias * Gaits[i].Multipliers[j].Value);
	// 			}
	// 		}
	// 		bias += weights[i] * _bias;
	// 	}
	// 	return bias;
	// }

	public float PoolVelocity(float[] weights) {
		float bias = 0f;
		for(int i=0; i<weights.Length; i++) {
			float _bias = Gaits[i].Bias;
			float max = 0f;
			for(int j=0; j<Gaits[i].Multipliers.Length; j++) {
				if(InputHandler.GetKey(Gaits[i].Multipliers[j].Key)) {
					max = Mathf.Max(max, Gaits[i].Bias * Gaits[i].Multipliers[j].Value);
				}
			}
			for(int j=0; j<Gaits[i].Multipliers.Length; j++) {
				if(InputHandler.GetKey(Gaits[i].Multipliers[j].Key)) {
					_bias = Mathf.Min(max, _bias * Gaits[i].Multipliers[j].Value);
				}
			}
			bias += weights[i] * _bias;
		}
		return bias;
	}

	public float PerStyleVelocity(float[] weights, int style_1, int style_2, float style_interp){ 
		 /*Assumes controller setup correctly to use PoolVelocity with UseGait off (as it is set up now so left shift as multiplier)
		 Is there a better way to ask if leftshift is pressed than Gaits[i].Multipliers[j].Key
		 */
		float bias = 0f;
		for(int i=0; i<weights.Length; i++) {
			float _bias = (1f - style_interp) * WalkVelocities[style_1] + style_interp * WalkVelocities[style_2];
			for(int j=0; j<Gaits[i].Multipliers.Length; j++) {
				if(InputHandler.GetKey(Gaits[i].Multipliers[j].Key)) {
					_bias =  (1f - style_interp) * RunVelocities[style_1] + style_interp * RunVelocities[style_2];
				}
			}
			bias += weights[i] * _bias;
		}
		return bias;
		
	}

	[System.Serializable]
	public class Style {
		public string Name;
		public float Bias = 1f;
		public float Transition = 0.1f;
		public KeyCode[] Keys = new KeyCode[0];
		public bool[] Negations = new bool[0];
		public Multiplier[] Multipliers = new Multiplier[0];

		public bool Query() {
			if(Keys.Length == 0) {
				return true;
			}

			bool active = false;

			for(int i=0; i<Keys.Length; i++) {
				if(!Negations[i]) {
					if(Keys[i] == KeyCode.None) {
						if(!InputHandler.anyKey) {
							active = true;
						}
					} else {
						if(InputHandler.GetKey(Keys[i])) {
							active = true;
						}
					}
				}
			}

			for(int i=0; i<Keys.Length; i++) {
				if(Negations[i]) {
					if(Keys[i] == KeyCode.None) {
						if(!InputHandler.anyKey) {
							active = false;
						}
					} else {
						if(InputHandler.GetKey(Keys[i])) {
							active = false;
						}
					}
				}
			}

			return active;
		}

		public void SetKeyCount(int count) {
			count = Mathf.Max(count, 0);
			if(Keys.Length != count) {
				System.Array.Resize(ref Keys, count);
				System.Array.Resize(ref Negations, count);
			}
		}

		public void AddMultiplier() {
			ArrayExtensions.Add(ref Multipliers, new Multiplier());
		}

		public void RemoveMultiplier() {
			ArrayExtensions.Shrink(ref Multipliers);
		}

		[System.Serializable]
		public class Multiplier {
			public KeyCode Key;
			public float Value;
		}
	}

	[System.Serializable]
	public class Gait {
		public string Name;
		public float Bias = 1f;
		public float Transition = 0.1f;
		public KeyCode[] Keys = new KeyCode[0];
		public bool[] Negations = new bool[0];
		public Multiplier[] Multipliers = new Multiplier[0];

		public bool Query() {
			if(Keys.Length == 0) {
				return true;
			}

			bool active = false;

			for(int i=0; i<Keys.Length; i++) {
				if(!Negations[i]) {
					if(Keys[i] == KeyCode.None) {
						if(!InputHandler.anyKey) {
							active = true;
						}
					} else {
						if(InputHandler.GetKey(Keys[i])) {
							active = true;
						}
					}
				}
			}

			for(int i=0; i<Keys.Length; i++) {
				if(Negations[i]) {
					if(Keys[i] == KeyCode.None) {
						if(!InputHandler.anyKey) {
							active = false;
						}
					} else {
						if(InputHandler.GetKey(Keys[i])) {
							active = false;
						}
					}
				}
			}

			return active;
		}

		public void SetKeyCount(int count) {
			count = Mathf.Max(count, 0);
			if(Keys.Length != count) {
				System.Array.Resize(ref Keys, count);
				System.Array.Resize(ref Negations, count);
			}
		}

		public void AddMultiplier() {
			ArrayExtensions.Add(ref Multipliers, new Multiplier());
		}

		public void RemoveMultiplier() {
			ArrayExtensions.Shrink(ref Multipliers);
		}

		[System.Serializable]
		public class Multiplier {
			public KeyCode Key;
			public float Value;
		}
	}

	#if UNITY_EDITOR
	public void Inspector() {
		Utility.SetGUIColor(Color.grey);
		using(new GUILayout.VerticalScope ("Box")) {
			Utility.ResetGUIColor();
			if(Utility.GUIButton("Controller", UltiDraw.DarkGrey, UltiDraw.White)) {
				Inspect = !Inspect;
			}

			if(Inspect) {
				using(new EditorGUILayout.VerticalScope ("Box")) {
					RunTimeStyle = EditorGUILayout.FloatField("Run Time Style", RunTimeStyle);
					Forward = (KeyCode)EditorGUILayout.EnumPopup("Forward", Forward);
					Back = (KeyCode)EditorGUILayout.EnumPopup("Backward", Back);
					Left = (KeyCode)EditorGUILayout.EnumPopup("Left", Left);
					Right = (KeyCode)EditorGUILayout.EnumPopup("Right", Right);
					TurnLeft = (KeyCode)EditorGUILayout.EnumPopup("Turn Left", TurnLeft);
					TurnRight = (KeyCode)EditorGUILayout.EnumPopup("Turn Right", TurnRight);
					SetStyleCount(EditorGUILayout.IntField("Styles", Styles.Length));
					for(int i=0; i<Styles.Length; i++) {
						Utility.SetGUIColor(UltiDraw.Grey);
						using(new EditorGUILayout.VerticalScope ("Box")) {

							Utility.ResetGUIColor();
							Styles[i].Name = EditorGUILayout.TextField("Name", Styles[i].Name);
							Styles[i].Bias = EditorGUILayout.FloatField("Bias", Styles[i].Bias);
							Styles[i].Transition = EditorGUILayout.Slider("Transition", Styles[i].Transition, 0f, 1f);
							Styles[i].SetKeyCount(EditorGUILayout.IntField("Keys", Styles[i].Keys.Length));

							for(int j=0; j<Styles[i].Keys.Length; j++) {
								EditorGUILayout.BeginHorizontal();
								Styles[i].Keys[j] = (KeyCode)EditorGUILayout.EnumPopup("Key", Styles[i].Keys[j]);
								Styles[i].Negations[j] = EditorGUILayout.Toggle("Negate", Styles[i].Negations[j]);
								EditorGUILayout.EndHorizontal();
							}

							for(int j=0; j<Styles[i].Multipliers.Length; j++) {
								Utility.SetGUIColor(Color.grey);
								using(new GUILayout.VerticalScope ("Box")) {
									Utility.ResetGUIColor();
									Styles[i].Multipliers[j].Key = (KeyCode)EditorGUILayout.EnumPopup("Key", Styles[i].Multipliers[j].Key);
									Styles[i].Multipliers[j].Value = EditorGUILayout.FloatField("Value", Styles[i].Multipliers[j].Value);
								}
							}
							
							if(Utility.GUIButton("Add Multiplier", UltiDraw.DarkGrey, UltiDraw.White)) {
								Styles[i].AddMultiplier();
							}
							if(Utility.GUIButton("Remove Multiplier", UltiDraw.DarkGrey, UltiDraw.White)) {
								Styles[i].RemoveMultiplier();
							}
						}
					}

					SetGaitCount(EditorGUILayout.IntField("Gaits", Gaits.Length));
					for(int i=0; i<Gaits.Length; i++) {
						Utility.SetGUIColor(UltiDraw.Grey);
						using(new EditorGUILayout.VerticalScope ("Box")) {

							Utility.ResetGUIColor();
							Gaits[i].Name = EditorGUILayout.TextField("Name", Gaits[i].Name);
							Gaits[i].Bias = EditorGUILayout.FloatField("Bias", Gaits[i].Bias);
							Gaits[i].Transition = EditorGUILayout.Slider("Transition", Gaits[i].Transition, 0f, 1f);
							Gaits[i].SetKeyCount(EditorGUILayout.IntField("Keys", Gaits[i].Keys.Length));

							for(int j=0; j<Gaits[i].Keys.Length; j++) {
								EditorGUILayout.BeginHorizontal();
								Gaits[i].Keys[j] = (KeyCode)EditorGUILayout.EnumPopup("Key", Gaits[i].Keys[j]);
								Gaits[i].Negations[j] = EditorGUILayout.Toggle("Negate", Gaits[i].Negations[j]);
								EditorGUILayout.EndHorizontal();
							}

							for(int j=0; j<Gaits[i].Multipliers.Length; j++) {
								Utility.SetGUIColor(Color.grey);
								using(new GUILayout.VerticalScope ("Box")) {
									Utility.ResetGUIColor();
									Gaits[i].Multipliers[j].Key = (KeyCode)EditorGUILayout.EnumPopup("Key", Gaits[i].Multipliers[j].Key);
									Gaits[i].Multipliers[j].Value = EditorGUILayout.FloatField("Value", Gaits[i].Multipliers[j].Value);
								}
							}
							
							if(Utility.GUIButton("Add Multiplier", UltiDraw.DarkGrey, UltiDraw.White)) {
								Gaits[i].AddMultiplier();
							}
							if(Utility.GUIButton("Remove Multiplier", UltiDraw.DarkGrey, UltiDraw.White)) {
								Gaits[i].RemoveMultiplier();
							}
						}
					}
				}
			}
		}
	}
	#endif

}
